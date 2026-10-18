using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using EspacoFestas.Services;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Views
{
    /// <summary>
    /// Abertura: lê o config.ini, conecta no Firebird (embarcado ou servidor) e carrega a CONFIGURACAO (tema, dados da empresa).
    /// Fica no mínimo ~1 s para não piscar; em caso de erro mostra a mensagem com as opções.
    /// </summary>
    public partial class SplashWindow : Window
    {
        private static readonly TimeSpan TempoMinimo = TimeSpan.FromSeconds(1);

        public SplashWindow()
        {
            InitializeComponent();
            TxtVersao.Text = App.Versao;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e) => await Carregar();

        private async Task Carregar()
        {
            PainelErro.Visibility = Visibility.Collapsed;
            PainelProgresso.Visibility = Visibility.Visible;
            Progresso.BeginAnimation(RangeBase.ValueProperty, null);
            Progresso.Value = 0;
            var cronometro = Stopwatch.StartNew();

            try
            {
                await Passo("Lendo configuração...", 20, App.CarregarConfiguracaoIni);
                await Passo("Conectando ao banco...", 55, () =>
                {
                    using var contexto = App.CriarContexto();
                    contexto.Database.OpenConnection();
                });
                // Versão nova instalada por cima de um banco antigo: aplica as alterações que faltam (com backup antes)
                await Passo("Verificando o banco...", 65, () => AtualizacaoBanco.Executar(texto => Dispatcher.Invoke(() => TxtPasso.Text = texto)));
                await Passo("Carregando configurações...", 85, App.CarregarSistema);

                AnimarProgresso(100);

                var restante = TempoMinimo - cronometro.Elapsed;
                await Task.Delay(restante > TimeSpan.FromMilliseconds(250) ? restante : TimeSpan.FromMilliseconds(250));

                TemaService.Aplicar(App.Sistema.Tema);

                // WhatsApp ligado: sobe o servidor em segundo plano; se falhar, o sistema abre igual (RN30)
                if (App.Sistema.WhatsappAtivo)
                {
                    _ = Services.Whatsapp.WhatsappServidor.Atual.IniciarAsync();
                }

                var principal = new MainWindow();
                Application.Current.MainWindow = principal;
                Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                principal.Show();
                Close();
            }
            catch (Exception ex)
            {
                ErroLog.Registrar(ex, "Splash");
                MostrarErro(ex);
            }
        }

        private async Task Passo(string texto, double progresso, Action acao)
        {
            TxtPasso.Text = texto;
            await Task.Run(acao);
            AnimarProgresso(progresso);
        }

        private void AnimarProgresso(double valor) =>
            Progresso.BeginAnimation(RangeBase.ValueProperty,
                new DoubleAnimation(valor, TimeSpan.FromMilliseconds(250)) { EasingFunction = new QuadraticEase() });

        private void MostrarErro(Exception ex)
        {
            PainelProgresso.Visibility = Visibility.Collapsed;
            PainelErro.Visibility = Visibility.Visible;

            var raiz = ex is AggregateException { InnerException: not null } agregada ? agregada.InnerException! : ex;
            (TxtErroTitulo.Text, TxtErro.Text) = raiz switch
            {
                FileNotFoundException => ("Arquivo de configuração não encontrado",
                    $"Crie o {AppPaths.NomeConfigIni} com a conexão do banco (use o config.exemplo.ini como modelo)."),
                InvalidDataException => ("Arquivo de configuração inválido", raiz.Message),
                FbException fb => ("Não foi possível conectar ao banco", fb.Message),
                _ => ("Não foi possível abrir o sistema", raiz.Message)
            };
        }

        private void BtnConfigurar_Click(object sender, RoutedEventArgs e)
        {
            var exemplo = Path.Combine(AppPaths.Raiz, "config.exemplo.ini");
            if (!File.Exists(AppPaths.ConfigIni) && File.Exists(exemplo))
            {
                File.Copy(exemplo, AppPaths.ConfigIni);
            }

            if (File.Exists(AppPaths.ConfigIni))
            {
                Process.Start(new ProcessStartInfo(AppPaths.ConfigIni) { UseShellExecute = true });
            }
        }

        private async void BtnTentar_Click(object sender, RoutedEventArgs e) => await Carregar();

        private void BtnSair_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not Button && e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
