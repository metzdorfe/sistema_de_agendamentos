using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EspacoFestas.Controls;
using EspacoFestas.Reports.Relatorios;
using EspacoFestas.Services;

namespace EspacoFestas.Views
{
    /// <summary>
    /// Relatórios (RF17, RF18, RF25) e o catálogo em PDF (RF19). Clicar num cartão abre o popup de filtros,
    /// que é o mesmo para todos (RelatorioFiltrosWindow).
    /// </summary>
    public partial class RelatoriosView : UserControl
    {
        private static readonly IRelatorio[] Relatorios =
        [
            new RelatorioAgendamentos(),
            new RelatorioCaixaCliente(),
            new RelatorioFaturamento(),
            new RelatorioCatalogo()
        ];

        public RelatoriosView()
        {
            InitializeComponent();
            foreach (var relatorio in Relatorios)
            {
                GradeRelatorios.Children.Add(CriarCartao(relatorio));
            }

            TxtLog.Text = $"Cada relatório gerado (ou que falhou) fica registrado em {RelatorioLog.Caminho}.";
        }

        private static Border CriarCartao(IRelatorio relatorio)
        {
            var icone = new Icone();
            icone.SetResourceReference(Icone.DadosProperty, relatorio.Icone);
            icone.SetResourceReference(ForegroundProperty, "CorDestaque");
            var fundoIcone = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(10),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = icone
            };
            fundoIcone.SetResourceReference(Border.BackgroundProperty, "CorPrincipalFundo");

            var titulo = new TextBlock { Text = relatorio.Nome, Margin = new Thickness(0, 16, 0, 6) };
            titulo.SetResourceReference(StyleProperty, "TextoTituloSecao");
            var descricao = new TextBlock { Text = relatorio.Descricao, FontSize = 14, TextWrapping = TextWrapping.Wrap };
            descricao.SetResourceReference(StyleProperty, "TextoAuxiliar");
            var formatos = new TextBlock { Text = relatorio.GeraXlsx ? "PDF · Excel (XLSX)" : "PDF", FontSize = 12, FontWeight = FontWeights.Medium, Margin = new Thickness(0, 12, 0, 0) };
            formatos.SetResourceReference(TextBlock.ForegroundProperty, "CorDestaque");

            var conteudo = new StackPanel();
            conteudo.Children.Add(fundoIcone);
            conteudo.Children.Add(titulo);
            conteudo.Children.Add(descricao);
            conteudo.Children.Add(formatos);

            var cartao = new Border { Margin = new Thickness(8, 0, 8, 16), Cursor = Cursors.Hand, Child = conteudo, Focusable = true };
            cartao.SetResourceReference(StyleProperty, "Cartao");
            cartao.MouseEnter += (_, _) => cartao.SetResourceReference(Border.BorderBrushProperty, "CorBotaoPrincipal");
            cartao.MouseLeave += (_, _) => cartao.SetResourceReference(Border.BorderBrushProperty, "CorBorda");
            cartao.MouseLeftButtonUp += (_, _) => Abrir(relatorio);
            cartao.KeyDown += (_, e) =>
            {
                if (e.Key is Key.Enter or Key.Space)
                {
                    Abrir(relatorio);
                }
            };
            return cartao;
        }

        private static void Abrir(IRelatorio relatorio) => MainWindow.Atual.MostrarModal(new RelatorioFiltrosWindow(relatorio));
    }
}
