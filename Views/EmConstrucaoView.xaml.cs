using System.Windows;
using System.Windows.Controls;

namespace EspacoFestas.Views
{
    /// <summary>Estado vazio para as telas ainda não desenhadas (agendamento, faturamento).</summary>
    public partial class EmConstrucaoView : UserControl
    {
        public EmConstrucaoView(string titulo, string? detalhe, string mensagem)
        {
            InitializeComponent();
            TxtTitulo.Text = titulo;
            TxtDetalhe.Text = detalhe ?? string.Empty;
            TxtDetalhe.Visibility = detalhe is null ? Visibility.Collapsed : Visibility.Visible;
            TxtMensagem.Text = mensagem;
        }

        private void BtnVoltar_Click(object sender, RoutedEventArgs e) => MainWindow.Atual.Navegar(new AgendaView());
    }
}
