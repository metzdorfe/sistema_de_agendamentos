using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using EspacoFestas.Data;
using EspacoFestas.Models;
using EspacoFestas.Services;
using EspacoFestas.Views;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EspacoFestas
{
    public partial class App : Application
    {
        /// <summary>Nome do sistema. O nome da empresa cliente vem de CONFIGURACAO.NOME_EMPRESA.</summary>
        public const string NomeSistema = "Fest Agenda";

        /// <summary>Conexão com o banco, lida do config.ini (única coisa fora do banco, RNF07).</summary>
        public static IConfiguration ConfiguracaoIni { get; private set; } = null!;

        /// <summary>Linha única da tabela CONFIGURACAO, carregada na splash.</summary>
        public static Configuracao Sistema { get; private set; } = new();

        /// <summary>Disparado quando as Configurações são salvas (navbar atualiza nome e logo da empresa).</summary>
        public static event Action? SistemaAlterado;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var brasil = CultureInfo.GetCultureInfo("pt-BR");
            CultureInfo.DefaultThreadCurrentCulture = brasil;
            CultureInfo.DefaultThreadCurrentUICulture = brasil;
            Thread.CurrentThread.CurrentCulture = brasil;
            Thread.CurrentThread.CurrentUICulture = brasil;
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(brasil.IetfLanguageTag)));

            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            // Chamado pelo instalador depois de copiar os arquivos: atualiza o banco sem abrir janela.
            // Código de saída 0 = em dia/atualizado; 1 = falhou (detalhes em Log\AtualizacaoBanco.log).
            if (e.Args.Contains("--atualizar-banco", StringComparer.OrdinalIgnoreCase))
            {
                Shutdown(AtualizarBancoSemJanela());
                return;
            }

            // Uma instância só: o banco embarcado fica preso ao primeiro processo, e a segunda janela só mostraria erro.
            // Já aberto: traz a janela existente para a frente e sai.
            _instancia = new Mutex(initiallyOwned: true, @"Local\FestAgenda.Instancia", out bool primeira);
            if (!primeira)
            {
                TrazerInstanciaAberta();
                Shutdown(0);
                return;
            }

            DispatcherUnhandledException += ErroNaoTratado;
            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                ErroLog.Registrar(args.Exception, "Tarefa");
                args.SetObserved();
            };
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception erro)
                {
                    ErroLog.Registrar(erro, "Fatal");
                }
            };
            TemaService.AcompanharWindows();

            new SplashWindow().Show();
        }

        /// <summary>Encerra o servidor do WhatsApp junto (o Job Object garante isso mesmo se o app travar).</summary>
        protected override void OnExit(ExitEventArgs e)
        {
            Services.Whatsapp.WhatsappServidor.Atual.Parar();
            _instancia?.Dispose();
            base.OnExit(e);
        }

        /// <summary>Marca de "sistema aberto" (ver OnStartup); solta ao sair.</summary>
        private static Mutex? _instancia;

        private static void TrazerInstanciaAberta()
        {
            var atual = System.Diagnostics.Process.GetCurrentProcess();
            var outra = System.Diagnostics.Process.GetProcessesByName(atual.ProcessName)
                .FirstOrDefault(p => p.Id != atual.Id && p.MainWindowHandle != IntPtr.Zero);
            if (outra is null)
            {
                return;
            }

            // Só restaura se estiver minimizada (restaurar uma janela maximizada a tiraria do maximizado)
            const int Restaurar = 9;
            if (IsIconic(outra.MainWindowHandle))
            {
                ShowWindow(outra.MainWindowHandle, Restaurar);
            }

            SetForegroundWindow(outra.MainWindowHandle);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr janela);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr janela, int comando);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr janela);

        private static int AtualizarBancoSemJanela()
        {
            try
            {
                CarregarConfiguracaoIni();
                var aplicadas = AtualizacaoBanco.Executar();
                AtualizacaoBanco.Registrar(aplicadas.Count == 0 ? "Instalador: banco já estava em dia." : "Instalador: banco atualizado.");
                return 0;
            }
            catch (Exception ex)
            {
                AtualizacaoBanco.Registrar($"Instalador: falha ao atualizar o banco: {ex.Message}");
                return 1;
            }
        }

        public static void CarregarConfiguracaoIni()
        {
            if (!File.Exists(AppPaths.ConfigIni))
            {
                throw new FileNotFoundException($"Arquivo {AppPaths.NomeConfigIni} não encontrado na pasta do sistema.", AppPaths.ConfigIni);
            }

            ConfiguracaoIni = new ConfigurationBuilder()
                .SetBasePath(AppPaths.Raiz)
                .AddIniFile(AppPaths.NomeConfigIni, optional: false)
                .Build();

            if (string.IsNullOrWhiteSpace(LerIni("Caminho")))
            {
                throw new InvalidDataException($"O {AppPaths.NomeConfigIni} não informa o caminho do banco (seção [Banco], chave Caminho).");
            }

            var tipo = LerIni("Tipo");
            if (tipo is not null && !tipo.Equals("Embarcado", StringComparison.OrdinalIgnoreCase)
                && !tipo.Equals("Servidor", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"No {AppPaths.NomeConfigIni}, Tipo deve ser Embarcado ou Servidor (está \"{tipo}\").");
            }
        }

        /// <summary>Lê uma chave da seção [Banco], ignorando comentário no fim da linha ("valor ; comentário").</summary>
        private static string? LerIni(string chave)
        {
            var valor = ConfiguracaoIni[$"Banco:{chave}"];
            if (valor is null)
            {
                return null;
            }

            int comentario = valor.IndexOf(" ;", StringComparison.Ordinal);
            valor = (comentario >= 0 ? valor[..comentario] : valor).Trim();
            return valor.Length == 0 ? null : valor;
        }

        /// <summary>
        /// Embarcado (padrão): o Firebird Embedded que vai junto do sistema abre o arquivo direto, sem servidor instalado.
        /// Servidor: conecta num Firebird Server (rede). O caminho pode ser relativo à pasta do sistema ou absoluto.
        /// </summary>
        public static string MontarConexao()
        {
            bool servidor = string.Equals(LerIni("Tipo"), "Servidor", StringComparison.OrdinalIgnoreCase);
            var caminho = LerIni("Caminho")!;

            var conexao = new FbConnectionStringBuilder
            {
                UserID = LerIni("Usuario") ?? "SYSDBA",
                Password = LerIni("Senha") ?? "masterkey",
                Charset = "UTF8"
            };

            if (servidor)
            {
                conexao.ServerType = FbServerType.Default;
                conexao.DataSource = LerIni("Servidor") ?? "localhost";
                conexao.Port = int.TryParse(LerIni("Porta"), out var porta) ? porta : 3050;
                // No servidor local o caminho relativo é resolvido aqui; num servidor remoto, use o caminho da máquina dele
                conexao.Database = Path.IsPathRooted(caminho) || !EhLocal(conexao.DataSource) ? caminho : AppPaths.Resolver(caminho);
            }
            else
            {
                conexao.ServerType = FbServerType.Embedded;
                conexao.Database = AppPaths.Resolver(caminho);
                conexao.ClientLibrary = Path.Combine(AppPaths.Raiz, "fbclient.dll");
            }

            return conexao.ToString();
        }

        private static bool EhLocal(string servidor) =>
            servidor.Equals("localhost", StringComparison.OrdinalIgnoreCase) || servidor == "127.0.0.1";

        /// <summary>Banco embarcado ou em servidor nesta máquina (dá para fazer backup numa pasta local).</summary>
        public static bool BancoLocal =>
            !string.Equals(LerIni("Tipo"), "Servidor", StringComparison.OrdinalIgnoreCase) || EhLocal(LerIni("Servidor") ?? "localhost");

        public static EspacoFestasContext CriarContexto()
        {
            var options = new DbContextOptionsBuilder<EspacoFestasContext>()
                .UseFirebird(MontarConexao())
                .Options;

            return new EspacoFestasContext(options);
        }

        /// <summary>Lê a CONFIGURACAO (cria a linha ID = 1 se ainda não existir).</summary>
        public static void CarregarSistema()
        {
            using var contexto = CriarContexto();
            var configuracao = contexto.Configuracoes.AsNoTracking().SingleOrDefault(c => c.Id == 1);
            if (configuracao is null)
            {
                configuracao = new Configuracao { Id = 1, PercentualJurosMes = 2, PercentualMulta = 2, PrazoCancelamentoDias = 7 };
                contexto.Configuracoes.Add(configuracao);
                contexto.SaveChanges();
            }

            Sistema = configuracao;
        }

        public static void AtualizarSistema(Configuracao configuracao)
        {
            Sistema = configuracao;
            SistemaAlterado?.Invoke();
        }

        /// <summary>Nome da empresa cliente; enquanto não for cadastrado, um texto que convida a preencher.</summary>
        public static string NomeEmpresa =>
            string.IsNullOrWhiteSpace(Sistema.NomeEmpresa) ? "Sua empresa" : Sistema.NomeEmpresa;

        public static string Versao
        {
            get
            {
                var versao = typeof(App).Assembly.GetName().Version ?? new Version(1, 0, 0);
                return $"v{versao.Major}.{versao.Minor}.{versao.Build}";
            }
        }

        private void ErroNaoTratado(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            if (e.Exception is RegraNegocioException regra)
            {
                // Regra violada que a tela não tratou: é aviso ao usuário, não defeito
                Dialogo.Avisar("Não foi possível concluir", regra.Message);
                return;
            }

            ErroLog.Registrar(e.Exception, "Interface");
            var mensagem = e.Exception is DbUpdateException { InnerException: { } interna }
                ? interna.Message
                : e.Exception.Message;
            Dialogo.Avisar("Algo deu errado", $"{mensagem}\n\nDetalhes gravados em Log\\{ErroLog.NomeArquivo}.");
        }
    }
}
