using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EspacoFestas.Controls;
using EspacoFestas.Services;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Views
{
    /// <summary>
    /// Faturamento com parcelamento: espécie, quantas vezes e vencimento por dia fixo do mês ou intervalo de dias.
    /// "Gerar parcelamento" mostra a grade (vencimentos ajustáveis); "Confirmar faturamento" grava no Receber e fatura.
    /// </summary>
    public partial class FaturamentoWindow : Window
    {
        private readonly ResumoFaturamento _resumo;
        private List<LinhaParcela> _parcelas = [];
        private bool _carregando = true;

        /// <summary>Verdadeiro se o agendamento foi faturado.</summary>
        public bool Faturou { get; private set; }

        public FaturamentoWindow(int agendamentoId)
        {
            InitializeComponent();
            _resumo = FinanceiroService.ObterResumo(agendamentoId);
            TxtEvento.Text = $"{_resumo.Titulo} · {_resumo.Cliente} · {Formatos.Data(_resumo.DataEvento)}";
            TxtTotal.Text = Formatos.Moeda(_resumo.Total);
            TxtPago.Text = Formatos.Moeda(_resumo.Pago);
            TxtAFaturar.Text = Formatos.Moeda(_resumo.AFaturar);
            TxtDiaFixo.Text = DateTime.Today.Day.ToString();

            using (var contexto = App.CriarContexto())
            {
                CmbEspecie.ItemsSource = contexto.Especies.AsNoTracking().Where(e => e.Ativo).OrderBy(e => e.Descricao)
                    .Select(e => new EspecieParcelamento(e.Id, e.Descricao, e.PermiteParcelamento)).ToList();
            }

            if (_resumo.AFaturar == 0)
            {
                // Sinal já cobre o total: fatura sem parcelas
                PainelPlano.Visibility = Visibility.Collapsed;
                AvisoQuitado.Visibility = Visibility.Visible;
                BtnConfirmar.IsEnabled = true;
                BtnConfirmar.ToolTip = null;
            }

            _carregando = false;
            Loaded += (_, _) => TemaService.AjustarBarraTitulo(this);
        }

        private EspecieParcelamento? Especie => CmbEspecie.SelectedItem as EspecieParcelamento;

        /// <summary>Qualquer mudança no plano invalida as parcelas geradas: precisa gerar de novo.</summary>
        private void Plano_Changed(object sender, RoutedEventArgs e)
        {
            if (_carregando)
            {
                return;
            }

            if (sender == CmbEspecie && Especie is { PermiteParcelamento: false })
            {
                TxtParcelas.Text = "1";
            }

            TxtParcelas.IsEnabled = Especie?.PermiteParcelamento != false;
            TxtAvisoEspecie.Visibility = Especie is { PermiteParcelamento: false } ? Visibility.Visible : Visibility.Collapsed;
            TxtErro.Visibility = Visibility.Collapsed;
            if (PainelParcelas.Visibility == Visibility.Visible)
            {
                PainelParcelas.Visibility = Visibility.Collapsed;
                BtnConfirmar.IsEnabled = false;
                BtnConfirmar.ToolTip = "Gere o parcelamento primeiro";
            }
        }

        private void TxtDiaFixo_GotFocus(object sender, RoutedEventArgs e) => OpcaoDiaFixo.IsChecked = true;

        private void TxtIntervalo_GotFocus(object sender, RoutedEventArgs e) => OpcaoIntervalo.IsChecked = true;

        private void BtnGerar_Click(object sender, RoutedEventArgs e)
        {
            if (Especie is null)
            {
                MostrarErro("Escolha a espécie.");
                return;
            }

            if (!int.TryParse(TxtParcelas.Text, out int quantidade) || quantidade is < 1 or > FinanceiroService.MaximoParcelas)
            {
                MostrarErro($"Informe em quantas vezes (1 a {FinanceiroService.MaximoParcelas}).");
                return;
            }

            if (quantidade > 1 && !Especie.PermiteParcelamento)
            {
                MostrarErro($"{Especie.Descricao} não permite parcelamento.");
                return;
            }

            bool diaFixo = OpcaoDiaFixo.IsChecked == true;
            int dia = 0, intervalo = 0;
            if (diaFixo && (!int.TryParse(TxtDiaFixo.Text, out dia) || dia is < 1 or > 31))
            {
                MostrarErro("Informe o dia fixo do mês (1 a 31).");
                return;
            }

            if (!diaFixo && (!int.TryParse(TxtIntervalo.Text, out intervalo) || intervalo < 1))
            {
                MostrarErro("Informe o intervalo em dias (1 ou mais).");
                return;
            }

            var plano = FinanceiroService.GerarParcelas(_resumo.AFaturar, quantidade, diaFixo ? ModoVencimento.DiaFixo : ModoVencimento.Intervalo,
                dia, intervalo, ChkPrimeiraHoje.IsChecked == true, DateTime.Today);
            _parcelas = plano.Select(p => new LinhaParcela(p.Numero, quantidade, p.Valor, p.RecebidaHoje) { VencimentoTexto = Formatos.Data(p.Vencimento) }).ToList();
            GradeParcelas.ItemsSource = _parcelas;
            TxtSomaParcelas.Text = $"{quantidade}x · total {Formatos.Moeda(plano.Sum(p => p.Valor))}";
            PainelParcelas.Visibility = Visibility.Visible;
            TxtErro.Visibility = Visibility.Collapsed;
            BtnConfirmar.IsEnabled = true;
            BtnConfirmar.ToolTip = null;
            PainelParcelas.BringIntoView();
        }

        private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            var plano = new List<ParcelaPlano>();
            foreach (var linha in _parcelas)
            {
                var vencimento = Mascara.LerData(linha.VencimentoTexto);
                if (vencimento is null)
                {
                    MostrarErro($"Vencimento inválido na parcela {linha.Rotulo}.");
                    return;
                }

                if (!linha.RecebidaHoje && vencimento < DateTime.Today)
                {
                    MostrarErro($"A parcela {linha.Rotulo} vence antes de hoje.");
                    return;
                }

                // Parcela recebida hoje vence hoje, mesmo que a data tenha sido editada na grade
                plano.Add(new ParcelaPlano(linha.Numero, linha.RecebidaHoje ? DateTime.Today : vencimento.Value, linha.Valor, linha.RecebidaHoje));
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                FinanceiroService.Faturar(_resumo.AgendamentoId, Especie?.Id, plano);
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

            Faturou = true;
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

        /// <summary>ToString: o ComboBox do sistema mostra o item selecionado pelo ToString.</summary>
        private sealed record EspecieParcelamento(int Id, string Descricao, bool PermiteParcelamento)
        {
            public override string ToString() => Descricao;
        }

        /// <summary>Linha da grade de parcelas; o vencimento é editável antes de confirmar.</summary>
        public sealed class LinhaParcela(int numero, int total, decimal valor, bool recebidaHoje)
        {
            public int Numero { get; } = numero;
            public string Rotulo { get; } = $"{numero}/{total}";
            public decimal Valor { get; } = valor;
            public string ValorTexto => Formatos.Moeda(Valor);
            public bool RecebidaHoje { get; } = recebidaHoje;
            public string Situacao => RecebidaHoje ? "Recebida hoje" : "A receber";
            public string VencimentoTexto { get; set; } = string.Empty;
        }
    }
}
