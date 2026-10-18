using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using EspacoFestas.Controls;
using EspacoFestas.Models;
using EspacoFestas.Services;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Views
{
    /// <summary>Lista de clientes com busca por nome, telefone ou CPF/CNPJ (RF01).</summary>
    public partial class ClientesView : UserControl
    {
        private List<ClienteLinha> _todos = [];

        public ClientesView()
        {
            InitializeComponent();
            Loaded += (_, _) => Carregar();
        }

        private void Carregar()
        {
            using var contexto = App.CriarContexto();
            _todos = contexto.Clientes.AsNoTracking()
                .OrderBy(c => c.Nome)
                .Select(c => new
                {
                    c.Id,
                    c.Nome,
                    c.Telefone,
                    c.CpfCnpj,
                    c.Ativo
                })
                .AsEnumerable()
                .Select(c => new ClienteLinha(c.Id, c.Nome, c.Telefone, Documentos.FormatarTelefone(c.Telefone),
                    c.CpfCnpj is null ? null : Documentos.FormatarCpfCnpj(c.CpfCnpj), c.CpfCnpj, c.Ativo))
                .ToList();

            Filtrar();
        }

        private void Filtrar()
        {
            var busca = TxtBusca.Text.Trim();
            var digitos = Documentos.SoDigitos(busca);
            bool inativos = ChkInativos.IsChecked == true;

            var filtrados = _todos
                .Where(c => inativos || c.Ativo)
                .Where(c => busca.Length == 0
                    || c.Nome.Contains(busca, StringComparison.CurrentCultureIgnoreCase)
                    || (digitos.Length > 0 && (c.TelefoneDigitos.Contains(digitos)
                                               || (c.CpfCnpjDigitos ?? string.Empty).Contains(digitos))))
                .ToList();

            Grade.ItemsSource = filtrados;
            Vazio.Visibility = filtrados.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            Grade.Visibility = filtrados.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            bool semCadastro = _todos.Count == 0;
            TxtVazio.Text = semCadastro ? "Nenhum cliente ainda" : "Nenhum cliente encontrado";
            BtnVazio.Visibility = semCadastro ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TxtBusca_TextChanged(object sender, TextChangedEventArgs e) => Filtrar();

        private void ChkInativos_Changed(object sender, RoutedEventArgs e) => Filtrar();

        private void BtnNovo_Click(object sender, RoutedEventArgs e) => MainWindow.Atual.Navegar(new ClienteFormView());

        private void Grade_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Grade.SelectedItem is ClienteLinha linha && e.OriginalSource is FrameworkElement { DataContext: ClienteLinha })
            {
                MainWindow.Atual.Navegar(new ClienteFormView(linha.Id));
            }
        }

        private void BtnAcoes_Click(object sender, RoutedEventArgs e)
        {
            var botao = (Button)sender;
            var linha = (ClienteLinha)botao.DataContext;

            var editar = new MenuItem { Header = "Editar" };
            Estilo.SetIcone(editar, (System.Windows.Media.Geometry)FindResource("IconeEditar"));
            editar.Click += (_, _) => MainWindow.Atual.Navegar(new ClienteFormView(linha.Id));

            var ativar = new MenuItem { Header = linha.Ativo ? "Inativar" : "Ativar" };
            ativar.Click += (_, _) => AlternarAtivo(linha);

            // RF28: catálogo pelo WhatsApp
            var catalogo = new MenuItem { Header = "Enviar catálogo pelo WhatsApp" };
            Estilo.SetIcone(catalogo, (System.Windows.Media.Geometry)FindResource("IconeCatalogo"));
            catalogo.Click += (_, _) => EnviarCatalogo(linha);

            var menu = new ContextMenu { PlacementTarget = botao, Placement = PlacementMode.Bottom };
            menu.Items.Add(editar);
            menu.Items.Add(catalogo);
            menu.Items.Add(ativar);
            menu.IsOpen = true;
        }

        private static void EnviarCatalogo(ClienteLinha linha)
        {
            var janela = EnvioWhatsappWindow.Catalogo(linha.Id);
            MainWindow.Atual.MostrarModal(janela);
            if (janela.Enviou)
            {
                Dialogo.Avisar("Catálogo enviado", $"O catálogo foi enviado para {linha.Nome} pelo WhatsApp.");
            }
        }

        private void AlternarAtivo(ClienteLinha linha)
        {
            if (linha.Ativo && !Dialogo.Confirmar("Inativar cliente",
                    $"{linha.Nome} não vai mais aparecer para novos agendamentos. O histórico é mantido.", "Inativar"))
            {
                return;
            }

            using var contexto = App.CriarContexto();
            var cliente = contexto.Clientes.Single(c => c.Id == linha.Id);
            cliente.Ativo = !cliente.Ativo;
            contexto.SaveChanges();
            Carregar();
        }

        /// <param name="CpfCnpj">Formatado para exibir; nulo quando não informado.</param>
        public sealed record ClienteLinha(int Id, string Nome, string TelefoneDigitos, string Telefone, string? CpfCnpj,
            string? CpfCnpjDigitos, bool Ativo);
    }
}
