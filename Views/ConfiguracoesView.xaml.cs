using System.Windows;
using System.Windows.Controls;
using EspacoFestas.Models;
using EspacoFestas.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace EspacoFestas.Views
{
    /// <summary>
    /// Configurações (RF20, RF21): parâmetros, espécies, empresa, aparência e sobre.
    /// Tudo gravado na tabela CONFIGURACAO, exceto as espécies, que têm tabela própria.
    /// </summary>
    public partial class ConfiguracoesView : UserControl
    {
        public const int AbaParametrosIndice = 0;
        public const int AbaEspeciesIndice = 1;
        public const int AbaEmpresa = 2;
        public const int AbaAparenciaIndice = 3;
        public const int AbaSobreIndice = 4;

        /// <summary>Logo gravada hoje (caminho relativo a Arquivos\).</summary>
        private string? _caminhoLogo;

        /// <summary>Imagem escolhida e ainda não salva (caminho completo do arquivo de origem).</summary>
        private string? _logoEscolhida;

        private int? _especieEditando;

        public ConfiguracoesView(int aba = AbaParametrosIndice)
        {
            InitializeComponent();
            TxtVersao.Text = $"Versão {App.Versao.TrimStart('v')}";
            TxtVersaoDetalhe.Text = App.Versao.TrimStart('v');
            CarregarConfiguracao();
            MostrarAba(aba);
        }

        private void MostrarAba(int aba)
        {
            var abas = new RadioButton[] { AbaParametros, AbaEspecies, AbaEmpresaBotao, AbaAparencia, AbaSobre };
            var paineis = new FrameworkElement[] { PainelParametros, PainelEspecies, PainelEmpresa, PainelAparencia, PainelSobre };
            for (int i = 0; i < abas.Length; i++)
            {
                abas[i].IsChecked = i == aba;
                paineis[i].Visibility = i == aba ? Visibility.Visible : Visibility.Collapsed;
            }

            if (aba == AbaEspeciesIndice)
            {
                CarregarEspecies();
            }
        }

        private void Aba_Click(object sender, RoutedEventArgs e)
        {
            var abas = new object[] { AbaParametros, AbaEspecies, AbaEmpresaBotao, AbaAparencia, AbaSobre };
            MostrarAba(Array.IndexOf(abas, sender));
        }

        private void CarregarConfiguracao()
        {
            var c = App.Sistema;
            ChkBloqueio.IsChecked = c.BloqueioUmPorDia;
            TxtJuros.Text = Formatos.Decimal(c.PercentualJurosMes);
            TxtMulta.Text = Formatos.Decimal(c.PercentualMulta);
            TxtPrazo.Text = c.PrazoCancelamentoDias.ToString();

            TxtEmpresaNome.Text = c.NomeEmpresa;
            TxtEmpresaCnpj.Text = c.CnpjEmpresa;
            TxtEmpresaTelefone.Text = c.TelefoneEmpresa;
            TxtEmpresaEmail.Text = c.EmailEmpresa;
            TxtEmpresaEndereco.Text = c.EnderecoEmpresa;
            _caminhoLogo = c.CaminhoLogoEmpresa;
            _logoEscolhida = null;
            MostrarLogo();

            OpcaoEscuro.IsChecked = c.Tema == Tema.Escuro;
            OpcaoClaro.IsChecked = c.Tema == Tema.Claro;
            OpcaoSistema.IsChecked = c.Tema == Tema.SeguirWindows;
        }

        /// <summary>Grava as alterações na linha única da CONFIGURACAO e avisa o resto do sistema.</summary>
        private static void Salvar(Action<Configuracao> alterar)
        {
            using var contexto = App.CriarContexto();
            var configuracao = contexto.Configuracoes.Single(c => c.Id == 1);
            alterar(configuracao);
            contexto.SaveChanges();
            App.AtualizarSistema(configuracao);
        }

        // ------------------------------------------------------------------
        // Parâmetros
        // ------------------------------------------------------------------

        private void BtnSalvarParametros_Click(object sender, RoutedEventArgs e)
        {
            if (!Formatos.TentarLerDecimal(TxtJuros.Text, out var juros) || juros is < 0 or > 100)
            {
                MostrarMensagem(TxtMensagemParametros, "Juros ao mês inválido.", erro: true);
                return;
            }

            if (!Formatos.TentarLerDecimal(TxtMulta.Text, out var multa) || multa is < 0 or > 100)
            {
                MostrarMensagem(TxtMensagemParametros, "Multa inválida.", erro: true);
                return;
            }

            if (!short.TryParse(TxtPrazo.Text.Trim(), out var prazo) || prazo < 0)
            {
                MostrarMensagem(TxtMensagemParametros, "Prazo de cancelamento inválido.", erro: true);
                return;
            }

            Salvar(c =>
            {
                c.BloqueioUmPorDia = ChkBloqueio.IsChecked == true;
                c.PercentualJurosMes = juros;
                c.PercentualMulta = multa;
                c.PrazoCancelamentoDias = prazo;
            });
            MostrarMensagem(TxtMensagemParametros, "Parâmetros salvos.", erro: false);
        }

        // ------------------------------------------------------------------
        // Espécies
        // ------------------------------------------------------------------

        private void CarregarEspecies()
        {
            using var contexto = App.CriarContexto();
            var especies = contexto.Especies.AsNoTracking().OrderByDescending(e => e.Ativo).ThenBy(e => e.Descricao).ToList();
            ListaEspecies.ItemsSource = especies;
            VazioEspecies.Visibility = especies.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ListaEspecies_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListaEspecies.SelectedItem is not Especie especie)
            {
                return;
            }

            _especieEditando = especie.Id;
            TxtTituloEspecie.Text = "Editar espécie";
            TxtEspecieDescricao.Text = especie.Descricao;
            ChkParcelamento.IsChecked = especie.PermiteParcelamento;
            ChkEspecieAtiva.IsChecked = especie.Ativo;
            TxtMensagemEspecie.Text = string.Empty;
        }

        private void BtnNovaEspecie_Click(object sender, RoutedEventArgs e)
        {
            _especieEditando = null;
            ListaEspecies.SelectedItem = null;
            TxtTituloEspecie.Text = "Nova espécie";
            TxtEspecieDescricao.Text = string.Empty;
            ChkParcelamento.IsChecked = false;
            ChkEspecieAtiva.IsChecked = true;
            TxtMensagemEspecie.Text = string.Empty;
            TxtEspecieDescricao.Focus();
        }

        private void BtnSalvarEspecie_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtEspecieDescricao.Text))
            {
                MostrarMensagem(TxtMensagemEspecie, "Informe a descrição.", erro: true);
                return;
            }

            using (var contexto = App.CriarContexto())
            {
                Especie especie;
                if (_especieEditando is null)
                {
                    especie = new Especie();
                    contexto.Especies.Add(especie);
                }
                else
                {
                    especie = contexto.Especies.Single(x => x.Id == _especieEditando);
                }

                especie.Descricao = TxtEspecieDescricao.Text.Trim();
                especie.PermiteParcelamento = ChkParcelamento.IsChecked == true;
                especie.Ativo = ChkEspecieAtiva.IsChecked == true;
                contexto.SaveChanges();
            }

            CarregarEspecies();
            BtnNovaEspecie_Click(sender, e);
            MostrarMensagem(TxtMensagemEspecie, "Espécie salva.", erro: false);
        }

        // ------------------------------------------------------------------
        // Empresa
        // ------------------------------------------------------------------

        private void MostrarLogo()
        {
            // Caminho completo (imagem recém-escolhida) também funciona: Path.Combine mantém caminhos absolutos
            var imagem = Imagens.Carregar(_logoEscolhida ?? _caminhoLogo);
            ImgLogo.Source = imagem;
            IconeSemLogo.Visibility = imagem is null ? Visibility.Visible : Visibility.Collapsed;
            BtnRemoverLogo.Visibility = imagem is null ? Visibility.Collapsed : Visibility.Visible;
        }

        private void BtnEscolherLogo_Click(object sender, RoutedEventArgs e)
        {
            var dialogo = new OpenFileDialog { Title = "Escolher logo", Filter = "Imagens (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg" };
            if (dialogo.ShowDialog() != true)
            {
                return;
            }

            if (Imagens.Carregar(dialogo.FileName) is null)
            {
                MostrarMensagem(TxtMensagemEmpresa, "Não foi possível ler essa imagem.", erro: true);
                return;
            }

            _logoEscolhida = dialogo.FileName;
            MostrarLogo();
            MostrarMensagem(TxtMensagemEmpresa, "Logo carregada. Clique em Salvar alterações para gravar.", erro: false);
        }

        private void BtnRemoverLogo_Click(object sender, RoutedEventArgs e)
        {
            _logoEscolhida = null;
            _caminhoLogo = null;
            MostrarLogo();
        }

        private void BtnSalvarEmpresa_Click(object sender, RoutedEventArgs e)
        {
            var cnpj = Documentos.SoDigitos(TxtEmpresaCnpj.Text);
            var email = TxtEmpresaEmail.Text.Trim();
            if (string.IsNullOrWhiteSpace(TxtEmpresaNome.Text))
            {
                MostrarMensagem(TxtMensagemEmpresa, "Informe o nome da empresa.", erro: true);
                return;
            }

            if (cnpj.Length > 0 && !Documentos.CnpjValido(cnpj))
            {
                MostrarMensagem(TxtMensagemEmpresa, "CNPJ inválido.", erro: true);
                return;
            }

            if (email.Length > 0 && !Documentos.EmailValido(email))
            {
                MostrarMensagem(TxtMensagemEmpresa, "E-mail inválido.", erro: true);
                return;
            }

            // Logo: copia a imagem escolhida para Arquivos\Empresa (nome único, máx. 512 px) e grava só o caminho relativo
            var logoAnterior = App.Sistema.CaminhoLogoEmpresa;
            if (_logoEscolhida is not null)
            {
                try
                {
                    _caminhoLogo = Imagens.Salvar(_logoEscolhida, Imagens.PastaEmpresa, "logo");
                    _logoEscolhida = null;
                }
                catch (Exception ex) when (ex is NotSupportedException or System.IO.IOException or UnauthorizedAccessException or ArgumentException)
                {
                    MostrarMensagem(TxtMensagemEmpresa, $"Não foi possível copiar a logo para a pasta Arquivos: {ex.Message}", erro: true);
                    return;
                }
            }

            Salvar(c =>
            {
                c.NomeEmpresa = TxtEmpresaNome.Text.Trim();
                c.CnpjEmpresa = cnpj.Length > 0 ? cnpj : null;
                c.TelefoneEmpresa = Documentos.SoDigitos(TxtEmpresaTelefone.Text) is { Length: > 0 } telefone ? telefone : null;
                c.EmailEmpresa = email.Length > 0 ? email : null;
                c.EnderecoEmpresa = string.IsNullOrWhiteSpace(TxtEmpresaEndereco.Text) ? null : TxtEmpresaEndereco.Text.Trim();
                c.CaminhoLogoEmpresa = _caminhoLogo;
            });

            if (!string.Equals(logoAnterior, _caminhoLogo, StringComparison.OrdinalIgnoreCase))
            {
                Imagens.ApagarSeExistir(logoAnterior);
            }

            MostrarMensagem(TxtMensagemEmpresa, "Dados da empresa salvos.", erro: false);
        }

        /// <summary>Confere os dígitos verificadores ao sair do campo (a máscara já formata durante a digitação).</summary>
        private void TxtEmpresaCnpj_LostFocus(object sender, RoutedEventArgs e)
        {
            var cnpj = Documentos.SoDigitos(TxtEmpresaCnpj.Text);
            Controls.Estilo.SetErro(TxtEmpresaCnpj, cnpj.Length > 0 && !Documentos.CnpjValido(cnpj));
        }

        // ------------------------------------------------------------------
        // Aparência
        // ------------------------------------------------------------------

        private void OpcaoTema_Click(object sender, RoutedEventArgs e)
        {
            var tema = sender == OpcaoClaro ? Tema.Claro : sender == OpcaoSistema ? Tema.SeguirWindows : Tema.Escuro;
            TemaService.Aplicar(tema);
            Salvar(c => c.Tema = tema);
        }

        private static void MostrarMensagem(TextBlock texto, string mensagem, bool erro)
        {
            texto.Text = mensagem;
            texto.SetResourceReference(TextBlock.ForegroundProperty, erro ? "CorErro" : "CorSucesso");
        }
    }
}
