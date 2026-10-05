using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using EspacoFestas.Controls;
using EspacoFestas.Services;
using EspacoFestas.Views;

namespace EspacoFestas
{
    /// <summary>Janela principal: navbar fixa à esquerda e a tela atual à direita.</summary>
    public partial class MainWindow : Window
    {
        /// <summary>Abaixo dessa largura a navbar recolhe e mostra só os ícones.</summary>
        private const double LarguraRecolher = 1100;

        public MainWindow()
        {
            InitializeComponent();
            TemaService.AjustarBarraTitulo(this);
            AtualizarEmpresa();
            App.SistemaAlterado += AtualizarEmpresa;
            Closed += (_, _) => App.SistemaAlterado -= AtualizarEmpresa;
            Navegar(new AgendaView());
        }

        public static MainWindow Atual => (MainWindow)Application.Current.MainWindow;

        public void Navegar(UserControl tela)
        {
            Conteudo.Content = tela;
            Conteudo.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            MarcarItem(tela);
        }

        private void MarcarItem(UserControl tela)
        {
            RadioButton? item = tela switch
            {
                AgendaView => NavAgenda,
                ClientesView or ClienteFormView => NavClientes,
                ServicosView or ServicoView => NavServicos,
                FinanceiroView => NavFinanceiro,
                RelatoriosView => NavRelatorios,
                ConfiguracoesView => NavConfiguracoes,
                _ => null
            };

            if (item is not null)
            {
                item.IsChecked = true;
            }

            AtualizarSubFinanceiro();
        }

        /// <summary>Chamado pelo Financeiro quando muda de aba, para marcar o subitem da navbar.</summary>
        public void MarcarSubFinanceiro(int aba)
        {
            var subitem = aba switch { 1 => SubReceber, 2 => SubCaixa, _ => SubVisaoGeral };
            subitem.IsChecked = true;
        }

        private void AtualizarSubFinanceiro()
        {
            bool mostrar = Conteudo.Content is FinanceiroView && !Navbar.GetRecolhida(PainelNavbar);
            SubFinanceiro.Visibility = mostrar ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AtualizarEmpresa()
        {
            TxtNomeEmpresa.Text = App.NomeEmpresa;
            var logo = Imagens.Carregar(App.Sistema.CaminhoLogoEmpresa);
            ImgLogoEmpresa.Source = logo;
            TxtIniciais.Text = logo is null ? Iniciais(App.NomeEmpresa) : string.Empty;
        }

        /// <summary>Abre uma janela modal (popup) por cima da tela atual, com o fundo escurecido.</summary>
        public bool? MostrarModal(Window janela) => Modal.Mostrar(janela, this, Overlay);

        private static string Iniciais(string nome)
        {
            var palavras = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p.Length > 2)
                .Take(2)
                .Select(p => char.ToUpperInvariant(p[0]));
            var iniciais = string.Concat(palavras);
            return iniciais.Length > 0 ? iniciais : nome[..Math.Min(1, nome.Length)].ToUpperInvariant();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool recolher = ActualWidth < LarguraRecolher;
            if (Navbar.GetRecolhida(PainelNavbar) == recolher && e.PreviousSize.Width > 0)
            {
                return;
            }

            Navbar.SetRecolhida(PainelNavbar, recolher);
            ColunaNavbar.Width = new GridLength(recolher ? 64 : 240);
            TxtMarca.Visibility = recolher ? Visibility.Collapsed : Visibility.Visible;
            TextoEmpresa.Visibility = recolher ? Visibility.Collapsed : Visibility.Visible;
            Marca.Margin = recolher ? new Thickness(6, 0, 0, 28) : new Thickness(10, 0, 0, 28);
            BtnEmpresa.Padding = recolher ? new Thickness(0) : new Thickness(8);
            BtnEmpresa.HorizontalContentAlignment = recolher ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            ((DockPanel)PainelNavbar.Child).Margin = recolher ? new Thickness(8, 20, 8, 16) : new Thickness(12, 20, 12, 16);
            AtualizarSubFinanceiro();
        }

        private void NavAgenda_Click(object sender, RoutedEventArgs e) => Navegar(new AgendaView());

        private void NavClientes_Click(object sender, RoutedEventArgs e) => Navegar(new ClientesView());

        private void NavServicos_Click(object sender, RoutedEventArgs e) => Navegar(new ServicosView());

        private void NavFinanceiro_Click(object sender, RoutedEventArgs e) => Navegar(new FinanceiroView());

        private void NavRelatorios_Click(object sender, RoutedEventArgs e) => Navegar(new RelatoriosView());

        private void NavConfiguracoes_Click(object sender, RoutedEventArgs e) => Navegar(new ConfiguracoesView());

        private void BtnEmpresa_Click(object sender, RoutedEventArgs e) => Navegar(new ConfiguracoesView(ConfiguracoesView.AbaEmpresa));

        private void SubFinanceiro_Click(object sender, RoutedEventArgs e)
        {
            int aba = int.Parse((string)((RadioButton)sender).Tag);
            if (Conteudo.Content is FinanceiroView financeiro)
            {
                financeiro.MostrarAba(aba);
            }
            else
            {
                Navegar(new FinanceiroView(aba));
            }
        }
    }
}
