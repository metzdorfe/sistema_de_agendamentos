using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EspacoFestas.Controls;
using EspacoFestas.Services;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Views
{
    /// <summary>
    /// Quitar (dar baixa em) uma parcela do Receber: mostra os dados da parcela, calcula multa e juros pela data
    /// do recebimento (RN18) e grava o valor pago na espécie usada (RN20), com entrada no caixa.
    /// Pago menor que o total: nova parcela com a diferença ou perdão (RN19).
    /// </summary>
    public partial class BaixaParcelaWindow : Window
    {
        private readonly int _receberId;
        private CalculoBaixa _calculo;
        private bool _carregando = true;

        /// <summary>Verdadeiro se a baixa foi gravada.</summary>
        public bool Baixou { get; private set; }

        public BaixaParcelaWindow(int receberId)
        {
            InitializeComponent();
            _receberId = receberId;
            _calculo = FinanceiroService.CalcularBaixa(receberId, DateTime.Today);

            TxtSubtitulo.Text = $"Parcela {_calculo.Numero}/{_calculo.TotalParcelas} · {_calculo.Cliente}";
            TxtCliente.Text = _calculo.Cliente;
            TxtEvento.Text = $"{_calculo.Evento} · {Formatos.Data(_calculo.DataEvento)}";
            TxtParcela.Text = $"{_calculo.Numero} de {_calculo.TotalParcelas}";
            TxtEspecieCombinada.Text = _calculo.Especie;
            TxtVencimento.Text = Formatos.Data(_calculo.Vencimento);
            TxtValorParcela.Text = Formatos.Moeda(_calculo.ValorParcela);
            TxtRegraJuros.Text = $"Em atraso: multa de {Formatos.Decimal(App.Sistema.PercentualMulta)}% e juros de " +
                                 $"{Formatos.Decimal(App.Sistema.PercentualJurosMes)}% ao mês, por dia (RN18). Os valores podem ser ajustados.";

            using (var contexto = App.CriarContexto())
            {
                var especies = contexto.Especies.AsNoTracking()
                    .Where(e => e.Ativo || e.Id == _calculo.EspecieId)
                    .OrderBy(e => e.Descricao)
                    .Select(e => new EspecieOpcao(e.Id, e.Descricao))
                    .ToList();
                CmbEspecie.ItemsSource = especies;
                CmbEspecie.SelectedItem = especies.FirstOrDefault(e => e.Id == _calculo.EspecieId);
            }

            TxtData.Text = Formatos.Data(DateTime.Today);
            AplicarCalculo();
            _carregando = false;
            AtualizarTotais();
            Loaded += (_, _) =>
            {
                TemaService.AjustarBarraTitulo(this);
                TxtRecebido.Focus();
                TxtRecebido.SelectAll();
            };
        }

        /// <summary>Preenche multa, juros e o valor pago com o cálculo da data atual.</summary>
        private void AplicarCalculo()
        {
            bool antes = _carregando;
            _carregando = true;
            TxtMulta.Text = Formatos.Decimal(_calculo.Multa);
            TxtJuros.Text = Formatos.Decimal(_calculo.Juros);
            TxtRecebido.Text = Formatos.Decimal(_calculo.Total);
            _carregando = antes;

            SeloAtraso.Visibility = _calculo.DiasAtraso > 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtAtraso.Text = _calculo.DiasAtraso == 1 ? "1 dia de atraso" : $"{_calculo.DiasAtraso} dias de atraso";
            TxtVencimentoDiferenca.Text = Formatos.Data((Mascara.LerData(TxtData.Text) ?? DateTime.Today).AddDays(30));
            AtualizarTotais();
        }

        private static decimal Ler(TextBox campo) => Formatos.TentarLerDecimal(campo.Text, out var valor) ? valor : 0;

        private decimal Total => _calculo.ValorParcela + Ler(TxtMulta) + Ler(TxtJuros);

        private void AtualizarTotais()
        {
            if (_carregando)
            {
                return;
            }

            TxtTotal.Text = Formatos.Moeda(Total);
            decimal recebido = Ler(TxtRecebido);
            decimal diferenca = Total - recebido;
            PainelDiferenca.Visibility = recebido > 0 && diferenca > 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtDiferenca.Text = $"Faltam {Formatos.Moeda(diferenca)} para quitar. O que fazer com a diferença?";
            TxtErro.Visibility = Visibility.Collapsed;
        }

        private void Valores_Changed(object sender, TextChangedEventArgs e) => AtualizarTotais();

        /// <summary>Mudou a data do recebimento: recalcula multa e juros para ela.</summary>
        private void TxtData_LostFocus(object sender, RoutedEventArgs e)
        {
            if (Mascara.LerData(TxtData.Text) is not { } data)
            {
                return;
            }

            var novo = FinanceiroService.CalcularBaixa(_receberId, data);
            if (novo.DiasAtraso != _calculo.DiasAtraso)
            {
                _calculo = novo;
                AplicarCalculo();
            }
        }

        private void BtnCalendario_Click(object sender, RoutedEventArgs e) =>
            SeletorData.Abrir(TxtData, CaixaData, () => TxtData_LostFocus(TxtData, e));

        private void TxtVencimentoDiferenca_GotFocus(object sender, RoutedEventArgs e) => OpcaoNovaParcela.IsChecked = true;

        private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            var data = Mascara.LerData(TxtData.Text);
            if (data is null)
            {
                MostrarErro("Informe a data do recebimento.");
                return;
            }

            if (data > DateTime.Today)
            {
                MostrarErro("A data do recebimento não pode ser futura.");
                return;
            }

            if (CmbEspecie.SelectedItem is not EspecieOpcao especie)
            {
                MostrarErro("Escolha a espécie usada no pagamento.");
                return;
            }

            decimal recebido = Ler(TxtRecebido);
            if (recebido <= 0)
            {
                MostrarErro("Informe o valor pago.");
                return;
            }

            DateTime? vencimentoDiferenca = null;
            if (recebido < Total && OpcaoNovaParcela.IsChecked == true)
            {
                vencimentoDiferenca = Mascara.LerData(TxtVencimentoDiferenca.Text);
                if (vencimentoDiferenca is null || vencimentoDiferenca <= data)
                {
                    MostrarErro("Informe um vencimento válido, depois do recebimento, para a parcela da diferença.");
                    return;
                }
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                FinanceiroService.Baixar(_receberId, data.Value, especie.Id, Ler(TxtMulta), Ler(TxtJuros), recebido, vencimentoDiferenca);
            }
            catch (RegraNegocioException ex)
            {
                MostrarErro(ex.Message);
                return;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            Baixou = true;
            DialogResult = true;
        }

        private void MostrarErro(string texto)
        {
            TxtErro.Text = texto;
            TxtErro.Visibility = Visibility.Visible;
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !CmbEspecie.IsDropDownOpen)
            {
                e.Handled = true;
                DialogResult = false;
            }
        }

        private void Cabecalho_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not Button && e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
