using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EspacoFestas.Controls;
using EspacoFestas.Models;
using EspacoFestas.Services;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Views
{
    /// <summary>
    /// Lançamento feito à mão no caixa (RF15): avulso, entrada ou saída, despesa do evento (RF12), que é sempre saída
    /// e fica ligada ao agendamento, ou suprimento e sangria (RN34), que só movimentam o dinheiro do caixa.
    /// </summary>
    public partial class LancamentoWindow : Window
    {
        /// <summary>Eventos que aparecem para despesa: até 6 meses atrás, mais todos os futuros.</summary>
        private const int MesesDespesa = 6;

        private bool _carregando = true;

        /// <summary>Verdadeiro se o lançamento foi gravado.</summary>
        public bool Lancou { get; private set; }

        public LancamentoWindow()
        {
            InitializeComponent();

            using (var contexto = App.CriarContexto())
            {
                CmbEspecie.ItemsSource = contexto.Especies.AsNoTracking().Where(e => e.Ativo).OrderBy(e => e.Descricao)
                    .Select(e => new EspecieOpcao(e.Id, e.Descricao)).ToList();

                var inicio = DateTime.Today.AddMonths(-MesesDespesa);
                var eventos = contexto.Agendamentos.AsNoTracking()
                    .Where(a => a.Status != StatusAgendamento.Cancelado && a.DataEvento >= inicio)
                    .OrderBy(a => a.DataEvento).ThenBy(a => a.HoraInicio)
                    .Select(a => new { a.Id, a.DataEvento, a.Titulo, Cliente = a.Cliente!.Nome })
                    .AsEnumerable()
                    .Select(a => new EventoOpcao(a.Id, $"{Formatos.Data(a.DataEvento)} · {a.Titulo} · {a.Cliente}"))
                    .Prepend(new EventoOpcao(null, "Nenhum (lançamento avulso)"))
                    .ToList();
                CmbAgendamento.ItemsSource = eventos;
                CmbAgendamento.SelectedIndex = 0;
            }

            TxtData.Text = Formatos.Data(DateTime.Today);
            _carregando = false;
            Loaded += (_, _) =>
            {
                TemaService.AjustarBarraTitulo(this);
                TxtValor.Focus();
            };
        }

        private bool Movimentacao => OpcaoSuprimento.IsChecked == true || OpcaoSangria.IsChecked == true;

        /// <summary>
        /// Despesa do evento é sempre saída comum: entrada, suprimento e sangria desligam o evento.
        /// Suprimento e sangria já trazem a descrição, que pode ser trocada.
        /// </summary>
        private void Tipo_Checked(object sender, RoutedEventArgs e)
        {
            if (CmbAgendamento is null)
            {
                return;
            }

            bool semEvento = OpcaoSaida.IsChecked != true;
            if (semEvento)
            {
                CmbAgendamento.SelectedIndex = 0;
            }

            CmbAgendamento.IsEnabled = !semEvento;
            TxtAjudaAgendamento.Text = OpcaoEntrada.IsChecked == true
                ? "Entrada não se liga a evento: pagamento do cliente entra pelo sinal ou pelo faturamento. Saída lançada por engano? Lance aqui uma entrada do mesmo valor para corrigir."
                : Movimentacao
                    ? "Suprimento e sangria só movimentam o dinheiro do caixa: não contam como receita nem despesa."
                    : "Escolha o evento para a saída entrar no lucro dele. Sem evento, o lançamento é avulso.";

            if (TxtDescricao.Text.Trim() is "" or "Suprimento de caixa" or "Sangria de caixa")
            {
                TxtDescricao.Text = OpcaoSuprimento.IsChecked == true ? "Suprimento de caixa"
                    : OpcaoSangria.IsChecked == true ? "Sangria de caixa"
                    : string.Empty;
            }

            LimparErros();
        }

        private void Campo_Changed(object sender, RoutedEventArgs e)
        {
            if (!_carregando)
            {
                LimparErros();
            }
        }

        private void BtnCalendario_Click(object sender, RoutedEventArgs e) => SeletorData.Abrir(TxtData, CaixaData);

        private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            LimparErros();
            var data = Mascara.LerData(TxtData.Text);
            if (data is null)
            {
                MostrarErro(TxtData, "Informe a data.");
                return;
            }

            if (!Formatos.TentarLerDecimal(TxtValor.Text, out var valor) || valor <= 0)
            {
                MostrarErro(TxtValor, "Informe um valor maior que zero.");
                return;
            }

            if (CmbEspecie.SelectedItem is not EspecieOpcao especie)
            {
                MostrarErro(CmbEspecie, "Escolha a espécie.");
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtDescricao.Text))
            {
                MostrarErro(TxtDescricao, "Informe a descrição.");
                return;
            }

            var tipo = OpcaoEntrada.IsChecked == true || OpcaoSuprimento.IsChecked == true ? TipoLancamento.Entrada : TipoLancamento.Saida;
            var agendamentoId = (CmbAgendamento.SelectedItem as EventoOpcao)?.Id;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                FinanceiroService.Lancar(data.Value, tipo, valor, especie.Id, TxtDescricao.Text, agendamentoId, Movimentacao);
            }
            catch (RegraNegocioException ex)
            {
                Control? campo = ex.Campo switch
                {
                    "Data" => TxtData,
                    "Valor" => TxtValor,
                    "Descricao" => TxtDescricao,
                    "Agendamento" => CmbAgendamento,
                    _ => null
                };
                MostrarErro(campo, ex.Message);
                return;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            Lancou = true;
            DialogResult = true;
        }

        private void MostrarErro(Control? campo, string texto)
        {
            TxtErro.Text = texto;
            TxtErro.Visibility = Visibility.Visible;
            if (campo is not null)
            {
                Estilo.SetErro(campo, true);
                campo.Focus();
            }
        }

        private void LimparErros()
        {
            TxtErro.Visibility = Visibility.Collapsed;
            foreach (var campo in new Control[] { TxtData, TxtValor, CmbEspecie, TxtDescricao, CmbAgendamento })
            {
                Estilo.SetErro(campo, false);
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !CmbEspecie.IsDropDownOpen && !CmbAgendamento.IsDropDownOpen)
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
        private sealed record EventoOpcao(int? Id, string Texto)
        {
            public override string ToString() => Texto;
        }
    }
}
