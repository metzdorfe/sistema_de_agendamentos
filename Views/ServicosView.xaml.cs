using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EspacoFestas.Reports;
using EspacoFestas.Services;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Views
{
    /// <summary>Lista só dos espaços; DJ, buffet e decoração são adicionais, dentro de cada serviço (RF02).</summary>
    public partial class ServicosView : UserControl
    {
        private List<ServicoLinha> _todos = [];

        public ServicosView()
        {
            InitializeComponent();
            Loaded += (_, _) => Carregar();
        }

        private void Carregar()
        {
            using var contexto = App.CriarContexto();
            _todos = contexto.Servicos.AsNoTracking()
                .OrderBy(s => s.Nome)
                .Select(s => new
                {
                    s.Id,
                    s.Nome,
                    s.Ativo,
                    Menor = s.Faixas.Where(f => f.Ativo).Min(f => (decimal?)f.Valor),
                    Faixas = s.Faixas.Count(f => f.Ativo),
                    Adicionais = s.ServicoAdicionais.Count
                })
                .AsEnumerable()
                .Select(s => new ServicoLinha(s.Id, s.Nome, s.Menor is null ? "-" : Formatos.Moeda(s.Menor.Value), s.Faixas, s.Adicionais, s.Ativo))
                .ToList();

            Filtrar();
        }

        private void Filtrar()
        {
            var busca = TxtBusca.Text.Trim();
            var filtrados = _todos
                .Where(s => ChkInativos.IsChecked == true || s.Ativo)
                .Where(s => busca.Length == 0 || s.Nome.Contains(busca, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            Grade.ItemsSource = filtrados;
            bool vazio = filtrados.Count == 0;
            Vazio.Visibility = vazio ? Visibility.Visible : Visibility.Collapsed;
            Grade.Visibility = vazio ? Visibility.Collapsed : Visibility.Visible;
            bool semCadastro = _todos.Count == 0;
            TxtVazio.Text = semCadastro ? "Nenhum serviço ainda" : "Nenhum serviço encontrado";
            TxtVazioAjuda.Visibility = BtnVazio.Visibility = semCadastro ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Filtro_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                Filtrar();
            }
        }

        private void Grade_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement { DataContext: ServicoLinha linha })
            {
                MainWindow.Atual.Navegar(new ServicoView(linha.Id));
            }
        }

        private void BtnNovo_Click(object sender, RoutedEventArgs e) => MainWindow.Atual.Navegar(new ServicoView(null));

        private void BtnCatalogo_Click(object sender, RoutedEventArgs e) => GerarCatalogo(null, "Catálogo de serviços");

        /// <summary>Gera o PDF direto na pasta Downloads (como num navegador) e oferece abrir ou mostrar na pasta.</summary>
        internal static void GerarCatalogo(int? servicoId, string nome)
        {
            var caminho = Downloads.NovoCaminho(nome, "pdf");
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                CatalogoPdf.Gerar(caminho, servicoId);
                RelatorioLog.Sucesso("Catalogo", "PDF", servicoId is null ? "servico=Todos" : $"servico={nome}", caminho);
            }
            catch (Exception ex)
            {
                RelatorioLog.Erro("Catalogo", "PDF", servicoId is null ? "servico=Todos" : $"servico={nome}", ex.Message);
                Mouse.OverrideCursor = null;
                Dialogo.Avisar("Não foi possível gerar o catálogo", ex.Message);
                return;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            Dialogo.ArquivoSalvo(caminho);
        }

        public sealed record ServicoLinha(int Id, string Nome, string APartirDe, int Faixas, int Adicionais, bool Ativo);
    }
}
