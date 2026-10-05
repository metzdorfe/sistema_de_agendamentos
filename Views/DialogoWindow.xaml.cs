using System.Windows;
using System.Windows.Controls;

namespace EspacoFestas.Views
{
    public partial class DialogoWindow : Window
    {
        public DialogoWindow()
        {
            InitializeComponent();
            MouseLeftButtonDown += (_, e) =>
            {
                if (e.OriginalSource is not TextBox)
                {
                    DragMove();
                }
            };
        }

        private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            if (PainelResposta.Visibility == Visibility.Visible && string.IsNullOrWhiteSpace(TxtResposta.Text))
            {
                TxtErroResposta.Text = "Obrigatório.";
                TxtErroResposta.Visibility = Visibility.Visible;
                Controls.Estilo.SetErro(TxtResposta, true);
                TxtResposta.Focus();
                return;
            }

            DialogResult = true;
        }

        private void TxtResposta_TextChanged(object sender, TextChangedEventArgs e)
        {
            TxtErroResposta.Visibility = Visibility.Collapsed;
            Controls.Estilo.SetErro(TxtResposta, false);
            TxtContador.Text = $"{TxtResposta.Text.Length}/{TxtResposta.MaxLength}";
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        /// <summary>Clicou na ação extra (BtnExtra).</summary>
        public bool EscolheuExtra { get; private set; }

        private void BtnExtra_Click(object sender, RoutedEventArgs e)
        {
            EscolheuExtra = true;
            DialogResult = true;
        }
    }

    /// <summary>Mensagens e confirmações no visual do sistema (o MessageBox padrão não segue o tema).</summary>
    public static class Dialogo
    {
        public static bool Confirmar(string titulo, string mensagem, string textoConfirmar = "Confirmar", bool perigo = false)
        {
            var janela = Criar(titulo, mensagem);
            janela.BtnConfirmar.Content = textoConfirmar;
            if (perigo)
            {
                janela.BtnConfirmar.Style = (Style)Application.Current.FindResource("BotaoPerigo");
                janela.IconeTitulo.Dados = (System.Windows.Media.Geometry)Application.Current.FindResource("IconeAlerta");
                janela.IconeTitulo.SetResourceReference(Control.ForegroundProperty, "CorErro");
            }

            return janela.ShowDialog() == true;
        }

        /// <summary>Pede um texto obrigatório (ex.: motivo do cancelamento). Nulo = desistiu.</summary>
        /// <param name="limite">Tamanho máximo, igual ao VARCHAR do banco (ex.: AGENDAMENTO.MOTIVO_CANCELAMENTO = 200).</param>
        public static string? PedirTexto(string titulo, string mensagem, string rotulo, string textoConfirmar, bool perigo = false, int limite = 200)
        {
            var janela = Criar(titulo, mensagem);
            janela.Width = 460;
            janela.TxtResposta.MaxLength = limite;
            janela.TxtContador.Text = $"0/{limite}";
            janela.BtnConfirmar.Content = textoConfirmar;
            janela.BtnConfirmar.IsDefault = false;
            janela.BtnCancelar.Content = "Voltar";
            janela.PainelResposta.Visibility = Visibility.Visible;
            janela.TxtRotuloResposta.Text = rotulo;
            if (perigo)
            {
                janela.BtnConfirmar.Style = (Style)Application.Current.FindResource("BotaoPerigo");
                janela.IconeTitulo.Dados = (System.Windows.Media.Geometry)Application.Current.FindResource("IconeAlerta");
                janela.IconeTitulo.SetResourceReference(Control.ForegroundProperty, "CorErro");
            }

            janela.Loaded += (_, _) => janela.TxtResposta.Focus();
            return janela.ShowDialog() == true ? janela.TxtResposta.Text.Trim() : null;
        }

        /// <summary>Arquivo gerado e salvo em Downloads: oferece abrir ou mostrar na pasta.</summary>
        public static void ArquivoSalvo(string caminho)
        {
            var janela = Criar("Arquivo salvo em Downloads", System.IO.Path.GetFileName(caminho));
            janela.Width = 460;
            janela.IconeTitulo.Dados = (System.Windows.Media.Geometry)Application.Current.FindResource("IconeCatalogo");
            janela.BtnConfirmar.Content = "Abrir";
            janela.BtnCancelar.Content = "Fechar";
            janela.BtnExtra.Content = "Mostrar na pasta";
            janela.BtnExtra.Visibility = Visibility.Visible;
            if (janela.ShowDialog() != true)
            {
                return;
            }

            if (janela.EscolheuExtra)
            {
                Services.Downloads.MostrarNaPasta(caminho);
            }
            else
            {
                Services.Downloads.Abrir(caminho);
            }
        }

        public static void Avisar(string titulo, string mensagem)
        {
            var janela = Criar(titulo, mensagem);
            janela.BtnCancelar.Visibility = Visibility.Collapsed;
            janela.ShowDialog();
        }

        private static DialogoWindow Criar(string titulo, string mensagem)
        {
            var janela = new DialogoWindow();
            janela.TxtTitulo.Text = titulo;
            janela.TxtMensagem.Text = mensagem;

            var dono = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w != janela)
                       ?? Application.Current.MainWindow;
            if (dono is not null && dono.IsVisible && dono != janela)
            {
                janela.Owner = dono;
            }
            else
            {
                janela.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            return janela;
        }
    }
}
