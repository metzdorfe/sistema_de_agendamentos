using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using EspacoFestas.Models;
using EspacoFestas.Services;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Views
{
    /// <summary>Financeiro em três abas: visão geral (RF22), contas a receber (RF13) e caixa (RF15).</summary>
    public partial class FinanceiroView : UserControl
    {
        public const int AbaVisao = 0;
        public const int AbaContasReceber = 1;
        public const int AbaCaixaLancamentos = 2;

        private const string PeriodoHoje = "Hoje";
        private const string PeriodoSemana = "Esta semana";
        private const string PeriodoMes = "Este mês";
        private const string PeriodoMesPassado = "Mês passado";
        private const string PeriodoTrimestre = "Últimos 3 meses";
        private const string PeriodoAno = "Este ano";
        private static readonly string[] Periodos = [PeriodoHoje, PeriodoSemana, PeriodoMes, PeriodoMesPassado, PeriodoTrimestre, PeriodoAno];
        /// <summary>Até esse número de dias o gráfico mostra uma barra por dia; acima, uma por mês.</summary>
        private const int DiasGraficoDiario = 31;

        private int _aba;
        private bool _carregado;

        public FinanceiroView(int aba = AbaVisao)
        {
            InitializeComponent();
            _aba = aba;
            CmbPeriodo.ItemsSource = Periodos;
            CmbPeriodo.SelectedItem = PeriodoMes;
            Loaded += (_, _) =>
            {
                _carregado = true;
                MostrarAba(_aba);
            };
        }

        public void MostrarAba(int aba)
        {
            _aba = aba;
            AbaVisaoGeral.IsChecked = aba == AbaVisao;
            AbaReceber.IsChecked = aba == AbaContasReceber;
            AbaCaixa.IsChecked = aba == AbaCaixaLancamentos;
            PainelVisaoGeral.Visibility = aba == AbaVisao ? Visibility.Visible : Visibility.Collapsed;
            PainelReceber.Visibility = aba == AbaContasReceber ? Visibility.Visible : Visibility.Collapsed;
            PainelCaixa.Visibility = aba == AbaCaixaLancamentos ? Visibility.Visible : Visibility.Collapsed;
            CmbPeriodo.Visibility = aba == AbaContasReceber ? Visibility.Hidden : Visibility.Visible;
            MainWindow.Atual.MarcarSubFinanceiro(aba);
            Atualizar();
        }

        private void Atualizar()
        {
            if (!_carregado)
            {
                return;
            }

            switch (_aba)
            {
                case AbaContasReceber:
                    CarregarReceber();
                    break;
                case AbaCaixaLancamentos:
                    CarregarCaixa();
                    break;
                default:
                    CarregarVisaoGeral();
                    break;
            }
        }

        private (DateTime Inicio, DateTime Fim) PeriodoSelecionado()
        {
            var hoje = DateTime.Today;
            var inicioMes = new DateTime(hoje.Year, hoje.Month, 1);
            // Semana de domingo a sábado, como no calendário
            var inicioSemana = hoje.AddDays(-(int)hoje.DayOfWeek);
            return (CmbPeriodo.SelectedItem as string) switch
            {
                PeriodoHoje => (hoje, hoje),
                PeriodoSemana => (inicioSemana, inicioSemana.AddDays(6)),
                PeriodoMesPassado => (inicioMes.AddMonths(-1), inicioMes.AddDays(-1)),
                PeriodoTrimestre => (inicioMes.AddMonths(-2), inicioMes.AddMonths(1).AddDays(-1)),
                PeriodoAno => (new DateTime(hoje.Year, 1, 1), new DateTime(hoje.Year, 12, 31)),
                _ => (inicioMes, inicioMes.AddMonths(1).AddDays(-1))
            };
        }

        /// <summary>Período do mesmo tamanho imediatamente antes (ontem, semana passada, meses anteriores), para a variação.</summary>
        private static (DateTime Inicio, DateTime Fim) PeriodoAnterior(DateTime inicio, DateTime fim)
        {
            bool mesesInteiros = inicio.Day == 1 && fim.AddDays(1).Day == 1;
            if (!mesesInteiros)
            {
                int dias = (fim - inicio).Days + 1;
                return (inicio.AddDays(-dias), inicio.AddDays(-1));
            }

            int meses = ((fim.Year - inicio.Year) * 12) + fim.Month - inicio.Month + 1;
            return (inicio.AddMonths(-meses), inicio.AddDays(-1));
        }

        // ------------------------------------------------------------------
        // Visão geral
        // ------------------------------------------------------------------

        private void CarregarVisaoGeral()
        {
            var (inicio, fim) = PeriodoSelecionado();
            var (inicioAnterior, fimAnterior) = PeriodoAnterior(inicio, fim);
            var hoje = DateTime.Today;

            using var contexto = App.CriarContexto();
            decimal Faturado(DateTime de, DateTime ate) => contexto.Agendamentos
                .Where(a => a.Status == StatusAgendamento.Faturado && a.DataEvento >= de && a.DataEvento <= ate)
                .Sum(a => (decimal?)a.ValorTotal) ?? 0;
            decimal Recebido(DateTime de, DateTime ate) => contexto.Financeiros
                .Where(f => f.Tipo == TipoLancamento.Entrada && f.DataLancamento >= de && f.DataLancamento <= ate)
                .Sum(f => (decimal?)f.Valor) ?? 0;

            decimal faturado = Faturado(inicio, fim);
            decimal recebido = Recebido(inicio, fim);
            TxtFaturado.Text = Formatos.Moeda(faturado);
            TxtRecebido.Text = Formatos.Moeda(recebido);
            MostrarVariacao(TxtFaturadoVariacao, faturado, Faturado(inicioAnterior, fimAnterior));
            MostrarVariacao(TxtRecebidoVariacao, recebido, Recebido(inicioAnterior, fimAnterior));

            // Em aberto: parcelas com vencimento no período
            var abertas = contexto.Recebers.Where(r => r.Status == StatusReceber.Aberto);
            var abertasPeriodo = abertas.Where(r => r.DataVencimento >= inicio && r.DataVencimento <= fim);
            TxtAberto.Text = Formatos.Moeda(abertasPeriodo.Sum(r => (decimal?)r.ValorParcela) ?? 0);
            int qtdAbertas = abertasPeriodo.Count();
            TxtAbertoDetalhe.Text = qtdAbertas switch
            {
                0 => "Nada vence no período",
                1 => "1 parcela vence no período",
                _ => $"{qtdAbertas} parcelas vencem no período"
            };

            var vencidas = abertas.Where(r => r.DataVencimento < hoje);
            int qtdVencidas = vencidas.Count();
            TxtVencidas.Text = Formatos.Moeda(vencidas.Sum(r => (decimal?)r.ValorParcela) ?? 0);
            TxtVencidasDetalhe.Text = qtdVencidas switch
            {
                0 => "Nenhuma parcela vencida",
                1 => "1 parcela vencida",
                _ => $"{qtdVencidas} parcelas vencidas"
            };
            if (qtdVencidas > 0)
            {
                CartaoVencidas.SetResourceReference(Border.BorderBrushProperty, "CorErro");
                TxtVencidas.SetResourceReference(TextBlock.ForegroundProperty, "CorErro");
                IconeVencidas.Visibility = Visibility.Visible;
            }
            else
            {
                CartaoVencidas.SetResourceReference(Border.BorderBrushProperty, "CorBorda");
                TxtVencidas.SetResourceReference(TextBlock.ForegroundProperty, "CorTexto");
                IconeVencidas.Visibility = Visibility.Collapsed;
            }

            // Gráfico do período: uma barra por dia (até um mês) ou por mês
            bool diario = (fim - inicio).Days < DiasGraficoDiario;
            var faturados = contexto.Agendamentos
                .Where(a => a.Status == StatusAgendamento.Faturado && a.DataEvento >= inicio && a.DataEvento <= fim)
                .Select(a => new { a.DataEvento, a.ValorTotal })
                .AsEnumerable()
                .GroupBy(a => diario ? a.DataEvento.Date : new DateTime(a.DataEvento.Year, a.DataEvento.Month, 1))
                .ToDictionary(g => g.Key, g => g.Sum(a => a.ValorTotal));
            var pontos = diario
                ? Enumerable.Range(0, (fim - inicio).Days + 1).Select(i => inicio.AddDays(i))
                : Enumerable.Range(0, ((fim.Year - inicio.Year) * 12) + fim.Month - inicio.Month + 1).Select(i => inicio.AddMonths(i));
            TxtTituloGrafico.Text = diario ? "Faturamento por dia" : "Faturamento por mês";
            TxtSubtituloGrafico.Text = $"Eventos faturados, pela data do evento ({Formatos.PeriodoCurto(inicio, fim)})";
            DesenharGrafico(pontos.Select(p => (p, faturados.GetValueOrDefault(p))).ToList(), diario);

            var ultimos = contexto.Financeiros.AsNoTracking()
                .Where(f => f.DataLancamento >= inicio && f.DataLancamento <= fim)
                .OrderByDescending(f => f.DataLancamento).ThenByDescending(f => f.Id)
                .Take(8)
                .Select(f => new { f.DataLancamento, f.Descricao, f.Valor, f.Tipo, Titulo = f.Agendamento != null ? f.Agendamento.Titulo : null })
                .ToList();
            ListaLancamentos.Children.Clear();
            foreach (var lancamento in ultimos)
            {
                ListaLancamentos.Children.Add(CriarLinhaLancamento(
                    lancamento.Descricao ?? lancamento.Titulo ?? (lancamento.Tipo == TipoLancamento.Entrada ? "Entrada" : "Saída"),
                    Formatos.Data(lancamento.DataLancamento), lancamento.Valor, lancamento.Tipo == TipoLancamento.Entrada));
            }

            VazioLancamentos.Visibility = ultimos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void MostrarVariacao(TextBlock texto, decimal atual, decimal anterior)
        {
            if (anterior == 0)
            {
                texto.Text = "Sem base no período anterior";
                texto.SetResourceReference(TextBlock.ForegroundProperty, "CorTextoSecundario");
                return;
            }

            var variacao = (atual - anterior) / anterior * 100;
            texto.Text = $"{(variacao >= 0 ? "▲" : "▼")} {Math.Abs(variacao):N0}% vs período anterior";
            texto.SetResourceReference(TextBlock.ForegroundProperty, variacao >= 0 ? "CorSucesso" : "CorErro");
        }

        /// <summary>Barras finas de um só tom, topo arredondado, eixo discreto e tooltip por barra.</summary>
        /// <param name="diario">Uma barra por dia (rótulo = dia) em vez de por mês.</param>
        private void DesenharGrafico(List<(DateTime Data, decimal Valor)> dados, bool diario)
        {
            Grafico.Children.Clear();
            Grafico.RowDefinitions.Clear();
            Grafico.ColumnDefinitions.Clear();
            Grafico.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grafico.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            decimal maximo = dados.Max(d => d.Valor);
            if (maximo == 0)
            {
                var vazio = new TextBlock { Text = "Nenhum evento faturado no período.", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                vazio.SetResourceReference(StyleProperty, "TextoAuxiliar");
                Grafico.Children.Add(vazio);
            }

            var linhaBase = new Border { BorderThickness = new Thickness(0, 0, 0, 1), VerticalAlignment = VerticalAlignment.Bottom };
            linhaBase.SetResourceReference(Border.BorderBrushProperty, "CorBorda");
            Grid.SetColumnSpan(linhaBase, dados.Count);
            Grafico.Children.Add(linhaBase);

            for (int i = 0; i < dados.Count; i++)
            {
                var (data, valor) = dados[i];
                Grafico.ColumnDefinitions.Add(new ColumnDefinition());
                // Muitas barras (mês por dia): rótulo só de 5 em 5 dias
                var nome = !diario ? Formatos.Capitalizar(data.ToString("MMM", Formatos.Brasil).TrimEnd('.'))
                    : dados.Count <= 7 ? $"{data.ToString("ddd", Formatos.Brasil).TrimEnd('.')} {data.Day}"
                    : data.Day == 1 || data.Day % 5 == 0 ? data.Day.ToString() : string.Empty;
                var periodo = diario ? data.ToString("dd/MM/yyyy (dddd)", Formatos.Brasil) : Formatos.Capitalizar(data.ToString("MMMM 'de' yyyy", Formatos.Brasil));

                // Coluna inteira como área de hover (alvo maior que a barra)
                var coluna = new Grid
                {
                    Background = Brushes.Transparent,
                    ToolTip = $"{periodo}: {Formatos.Moeda(valor)}"
                };
                coluna.RowDefinitions.Add(new RowDefinition { Height = new GridLength(maximo == 0 ? 1 : (double)(maximo * 1.15m - valor), GridUnitType.Star) });
                coluna.RowDefinitions.Add(new RowDefinition { Height = new GridLength(maximo == 0 ? 0 : (double)valor, GridUnitType.Star) });

                if (valor > 0)
                {
                    var barra = new Border { Width = dados.Count > 12 ? 10 : 28, CornerRadius = new CornerRadius(dados.Count > 12 ? 2 : 4, dados.Count > 12 ? 2 : 4, 0, 0), Margin = new Thickness(0, 0, 0, 1) };
                    barra.SetResourceReference(Border.BackgroundProperty, "CorBotaoPrincipal");
                    Grid.SetRow(barra, 1);
                    coluna.Children.Add(barra);
                    coluna.MouseEnter += (_, _) => barra.SetResourceReference(Border.BackgroundProperty, "CorBotaoPrincipalHover");
                    coluna.MouseLeave += (_, _) => barra.SetResourceReference(Border.BackgroundProperty, "CorBotaoPrincipal");

                    if (valor == maximo)
                    {
                        // Rótulo direto só no maior valor
                        var rotulo = new TextBlock
                        {
                            Text = Formatos.Moeda(valor),
                            FontSize = 11,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Bottom,
                            // Margem negativa: em colunas estreitas (gráfico por dia) o rótulo passa da largura da coluna sem cortar
                            Margin = new Thickness(-60, 0, -60, 4)
                        };
                        rotulo.SetResourceReference(TextBlock.ForegroundProperty, "CorTextoSecundario");
                        coluna.Children.Add(rotulo);
                    }
                }

                Grid.SetColumn(coluna, i);
                Grafico.Children.Add(coluna);

                var legenda = new TextBlock { Text = nome, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
                legenda.SetResourceReference(TextBlock.ForegroundProperty, "CorTextoSecundario");
                Grid.SetColumn(legenda, i);
                Grid.SetRow(legenda, 1);
                Grafico.Children.Add(legenda);
            }
        }

        private static DockPanel CriarLinhaLancamento(string descricao, string data, decimal valor, bool entrada)
        {
            var valorTexto = new TextBlock
            {
                Text = (entrada ? "+ " : "− ") + Formatos.Moeda(valor),
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            Typography.SetNumeralAlignment(valorTexto, FontNumeralAlignment.Tabular);
            valorTexto.SetResourceReference(TextBlock.ForegroundProperty, entrada ? "CorSucesso" : "CorErro");
            DockPanel.SetDock(valorTexto, Dock.Right);

            var textos = new StackPanel();
            textos.Children.Add(new TextBlock { Text = descricao, TextTrimming = TextTrimming.CharacterEllipsis });
            var dataTexto = new TextBlock { Text = data, FontSize = 12, Margin = new Thickness(0, 2, 0, 0) };
            dataTexto.SetResourceReference(TextBlock.ForegroundProperty, "CorTextoSecundario");
            textos.Children.Add(dataTexto);

            var linha = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
            linha.Children.Add(valorTexto);
            linha.Children.Add(textos);
            return linha;
        }

        // ------------------------------------------------------------------
        // Contas a receber
        // ------------------------------------------------------------------

        private void CarregarReceber()
        {
            var hoje = DateTime.Today;
            using var contexto = App.CriarContexto();
            var consulta = contexto.Recebers.AsNoTracking();
            if (FiltroAbertas.IsChecked == true)
            {
                consulta = consulta.Where(r => r.Status == StatusReceber.Aberto);
            }
            else if (FiltroVencidas.IsChecked == true)
            {
                consulta = consulta.Where(r => r.Status == StatusReceber.Aberto && r.DataVencimento < hoje);
            }
            else if (FiltroPagas.IsChecked == true)
            {
                consulta = consulta.Where(r => r.Status == StatusReceber.Pago);
            }

            var linhas = consulta
                .OrderBy(r => r.DataVencimento)
                .Select(r => new
                {
                    r.Id,
                    r.DataVencimento,
                    Cliente = r.Agendamento!.Cliente!.Nome,
                    Evento = r.Agendamento.Titulo,
                    r.Agendamento.DataEvento,
                    r.NumeroParcela,
                    Especie = r.Especie!.Descricao,
                    r.ValorParcela,
                    r.Status
                })
                .AsEnumerable()
                .Select(r => new ParcelaLinha(
                    r.Id,
                    r.Status == StatusReceber.Aberto,
                    Formatos.Data(r.DataVencimento),
                    r.Cliente,
                    $"{r.Evento} · {Formatos.Data(r.DataEvento)}",
                    $"{r.NumeroParcela}ª",
                    r.Especie,
                    Formatos.Moeda(r.ValorParcela),
                    r.Status switch
                    {
                        StatusReceber.Pago => "Paga",
                        StatusReceber.Cancelado => "Cancelada",
                        _ => r.DataVencimento < hoje ? "Vencida" : "Aberta"
                    }))
                .ToList();

            GradeReceber.ItemsSource = linhas;
            VazioReceber.Visibility = linhas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            GradeReceber.Visibility = linhas.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            AtualizarBotaoBaixa();
        }

        private void AtualizarBotaoBaixa()
        {
            bool aberta = GradeReceber.SelectedItem is ParcelaLinha { Aberta: true };
            BtnBaixa.IsEnabled = aberta;
            BtnBaixa.ToolTip = aberta ? "Receber a parcela selecionada" : "Selecione uma parcela em aberto";
        }

        private void GradeReceber_SelectionChanged(object sender, SelectionChangedEventArgs e) => AtualizarBotaoBaixa();

        private void GradeReceber_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement { DataContext: ParcelaLinha { Aberta: true } })
            {
                DarBaixa();
            }
        }

        private void BtnBaixa_Click(object sender, RoutedEventArgs e) => DarBaixa();

        /// <summary>Abre a tela de quitar a parcela selecionada; ao gravar, recarrega a lista.</summary>
        private void DarBaixa()
        {
            if (GradeReceber.SelectedItem is not ParcelaLinha { Aberta: true } linha)
            {
                return;
            }

            var janela = new BaixaParcelaWindow(linha.Id);
            MainWindow.Atual.MostrarModal(janela);
            if (janela.Baixou)
            {
                CarregarReceber();
            }
        }

        // ------------------------------------------------------------------
        // Caixa
        // ------------------------------------------------------------------

        private void CarregarCaixa()
        {
            var (inicio, fim) = PeriodoSelecionado();
            using var contexto = App.CriarContexto();
            var lancamentos = contexto.Financeiros.AsNoTracking()
                .Where(f => f.DataLancamento >= inicio && f.DataLancamento <= fim)
                .OrderByDescending(f => f.DataLancamento).ThenByDescending(f => f.Id)
                .Select(f => new
                {
                    f.DataLancamento,
                    f.Descricao,
                    f.Valor,
                    f.Tipo,
                    Especie = f.Especie!.Descricao,
                    Agendamento = f.Agendamento != null ? f.Agendamento.Titulo : null,
                    Parcela = f.Receber != null ? (short?)f.Receber.NumeroParcela : null,
                    AgendamentoParcela = f.Receber != null ? f.Receber.Agendamento!.Titulo : null
                })
                .ToList();

            decimal entradas = lancamentos.Where(l => l.Tipo == TipoLancamento.Entrada).Sum(l => l.Valor);
            decimal saidas = lancamentos.Where(l => l.Tipo == TipoLancamento.Saida).Sum(l => l.Valor);
            TxtEntradas.Text = Formatos.Moeda(entradas);
            TxtSaidas.Text = Formatos.Moeda(saidas);
            TxtSaldo.Text = Formatos.Moeda(entradas - saidas);

            var linhas = lancamentos.Select(l => new LancamentoLinha(
                Formatos.Data(l.DataLancamento),
                l.Descricao ?? (l.Tipo == TipoLancamento.Entrada ? "Entrada" : "Saída"),
                l.Parcela is not null ? $"Parcela {l.Parcela} · {l.AgendamentoParcela}"
                    : l.Agendamento is not null ? $"Agendamento · {l.Agendamento}"
                    : "Avulso",
                l.Especie,
                (l.Tipo == TipoLancamento.Entrada ? "+ " : "− ") + Formatos.Moeda(l.Valor),
                l.Tipo == TipoLancamento.Entrada)).ToList();

            GradeCaixa.ItemsSource = linhas;
            VazioCaixa.Visibility = linhas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            GradeCaixa.Visibility = linhas.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Aba_Click(object sender, RoutedEventArgs e) =>
            MostrarAba(sender == AbaReceber ? AbaContasReceber : sender == AbaCaixa ? AbaCaixaLancamentos : AbaVisao);

        private void FiltroReceber_Click(object sender, RoutedEventArgs e) => CarregarReceber();

        private void CmbPeriodo_SelectionChanged(object sender, SelectionChangedEventArgs e) => Atualizar();

        public sealed record ParcelaLinha(int Id, bool Aberta, string Vencimento, string Cliente, string Evento, string Parcela, string Especie,
            string Valor, string Status);

        public sealed record LancamentoLinha(string Data, string Descricao, string Origem, string Especie, string Valor, bool Entrada);
    }
}
