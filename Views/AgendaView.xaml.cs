using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using EspacoFestas.Controls;
using EspacoFestas.Models;
using EspacoFestas.Services;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Views
{
    /// <summary>
    /// Tela principal: calendário nas visões Mês (pílulas por status e painel do dia), Semana e Dia (RF05, RF06, RF24).
    /// </summary>
    public partial class AgendaView : UserControl
    {
        private static readonly string[] DiasSemana = ["DOM", "SEG", "TER", "QUA", "QUI", "SEX", "SÁB"];
        private static readonly string[] NomesDias = ["Domingo", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado"];
        private const int PilulasPorDia = 2;

        /// <summary>Visão escolhida, lembrada enquanto o app estiver aberto.</summary>
        private static VisaoAgenda _visaoLembrada = VisaoAgenda.Mes;

        private VisaoAgenda _visao;
        /// <summary>Data de referência da visão ativa (o mês, a semana ou o dia que contém essa data).</summary>
        private DateTime _referencia = DateTime.Today;
        private DateTime? _diaSelecionado;
        private List<EventoAgenda> _eventos = [];

        public AgendaView()
        {
            InitializeComponent();
            foreach (var dia in DiasSemana)
            {
                var texto = new TextBlock
                {
                    Text = dia,
                    FontSize = 12,
                    FontWeight = FontWeights.Medium,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                texto.SetResourceReference(TextBlock.ForegroundProperty, "CorTextoSecundario");
                CabecalhoSemana.Children.Add(texto);
            }

            _visao = _visaoLembrada;
            (_visao switch { VisaoAgenda.Semana => AbaSemana, VisaoAgenda.Dia => AbaDia, _ => AbaMes }).IsChecked = true;
            Loaded += (_, _) => Mostrar();
        }

        private DateTime PrimeiroDoMes => new(_referencia.Year, _referencia.Month, 1);

        private static DateTime InicioSemana(DateTime data) => data.AddDays(-(int)data.DayOfWeek);

        /// <summary>Mostra a visão ativa na data de referência.</summary>
        private void Mostrar()
        {
            VisaoMes.Visibility = _visao == VisaoAgenda.Mes ? Visibility.Visible : Visibility.Collapsed;
            VisaoSemana.Visibility = _visao == VisaoAgenda.Semana ? Visibility.Visible : Visibility.Collapsed;
            VisaoDia.Visibility = _visao == VisaoAgenda.Dia ? Visibility.Visible : Visibility.Collapsed;

            (BtnAnterior.ToolTip, BtnProximo.ToolTip) = _visao switch
            {
                VisaoAgenda.Semana => ("Semana anterior", "Próxima semana"),
                VisaoAgenda.Dia => ("Dia anterior", "Próximo dia"),
                _ => ("Mês anterior", "Próximo mês")
            };

            switch (_visao)
            {
                case VisaoAgenda.Semana:
                    MostrarSemana();
                    break;
                case VisaoAgenda.Dia:
                    MostrarDiaUnico();
                    break;
                default:
                    MostrarMes();
                    break;
            }
        }

        private void MostrarMes()
        {
            var mes = PrimeiroDoMes;
            TxtPeriodo.Text = Formatos.Capitalizar(mes.ToString("MMMM yyyy", Formatos.Brasil));
            var primeiroDia = InicioSemana(mes);
            _eventos = Consultar(primeiroDia, primeiroDia.AddDays(41));

            GradeDias.Children.Clear();
            for (int i = 0; i < 42; i++)
            {
                GradeDias.Children.Add(CriarDia(primeiroDia.AddDays(i), i));
            }

            GradeDias.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
        }

        private static List<EventoAgenda> Consultar(DateTime inicio, DateTime fim)
        {
            using var contexto = App.CriarContexto();
            var linhas = contexto.Agendamentos.AsNoTracking()
                .Where(a => a.DataEvento >= inicio && a.DataEvento <= fim && a.Status != StatusAgendamento.Cancelado)
                .OrderBy(a => a.DataEvento).ThenBy(a => a.HoraInicio)
                .Select(a => new
                {
                    a.Id,
                    a.Titulo,
                    a.DataEvento,
                    a.HoraInicio,
                    a.HoraFim,
                    Cliente = a.Cliente!.Nome,
                    Telefone = a.Cliente.Telefone,
                    Servicos = a.Servicos.Select(s => s.Servico!.Nome).ToList(),
                    a.QuantidadeConvidados,
                    a.Status,
                    TemSinal = a.Lancamentos.Any(l => l.Tipo == TipoLancamento.Entrada)
                })
                .ToList();

            return linhas.Select(l => new EventoAgenda(l.Id, l.Titulo, l.DataEvento, l.HoraInicio, l.HoraFim, l.Cliente, l.Telefone,
                string.Join(", ", l.Servicos), l.QuantidadeConvidados, l.Status, l.TemSinal)).ToList();
        }

        private Border CriarDia(DateTime data, int indice)
        {
            bool doMes = data.Month == _referencia.Month;
            bool hoje = data == DateTime.Today;
            bool selecionado = data == _diaSelecionado;
            var eventosDia = _eventos.Where(e => e.Data == data).ToList();

            var celula = new Border
            {
                BorderThickness = new Thickness(0, 0, indice % 7 == 6 ? 0 : 1, indice >= 35 ? 0 : 1),
                Padding = new Thickness(8, 6, 8, 6),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                Tag = data
            };
            celula.SetResourceReference(Border.BorderBrushProperty, "CorBorda");
            if (selecionado)
            {
                celula.SetResourceReference(Border.BackgroundProperty, "CorSelecao");
            }

            var conteudo = new StackPanel();
            if (!doMes)
            {
                conteudo.Opacity = 0.35;
            }
            else if (data < DateTime.Today)
            {
                conteudo.Opacity = 0.55;
            }

            // Número do dia; hoje com borda de ênfase
            var numero = new TextBlock
            {
                Text = data.Day.ToString(),
                FontSize = 12,
                FontWeight = hoje ? FontWeights.Bold : FontWeights.Medium,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            numero.SetResourceReference(TextBlock.ForegroundProperty, hoje ? "CorEnfase" : doMes ? "CorTexto" : "CorTextoSecundario");
            var marcador = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(6),
                HorizontalAlignment = HorizontalAlignment.Left,
                BorderThickness = new Thickness(hoje ? 1.5 : 0),
                Child = numero
            };
            marcador.SetResourceReference(Border.BorderBrushProperty, "CorEnfase");
            conteudo.Children.Add(marcador);

            var visiveis = eventosDia.Count > PilulasPorDia ? eventosDia.Take(1) : eventosDia;
            foreach (var evento in visiveis)
            {
                conteudo.Children.Add(CriarPilula(evento));
            }

            if (eventosDia.Count > PilulasPorDia)
            {
                var mais = new TextBlock
                {
                    Text = $"+{eventosDia.Count - 1}",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(2, 4, 0, 0)
                };
                mais.SetResourceReference(TextBlock.ForegroundProperty, "CorDestaque");
                conteudo.Children.Add(mais);
            }

            celula.Child = conteudo;
            celula.MouseEnter += (_, _) =>
            {
                if (data != _diaSelecionado)
                {
                    celula.SetResourceReference(Border.BackgroundProperty, "CorSuperficieElevada");
                }
            };
            celula.MouseLeave += (_, _) =>
            {
                if (data != _diaSelecionado)
                {
                    celula.Background = Brushes.Transparent;
                }
            };
            celula.MouseLeftButtonUp += (_, _) => AbrirDia(data);
            return celula;
        }

        private static FrameworkElement CriarPilula(EventoAgenda evento)
        {
            var texto = new TextBlock
            {
                Text = $"{Formatos.Hora(evento.Inicio)} {evento.Titulo}",
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(6, 2, 6, 2)
            };
            var pilula = new Grid { Margin = new Thickness(0, 4, 0, 0), ToolTip = $"{evento.Titulo} · {evento.Cliente} · {evento.NomeStatus}" };
            pilula.Children.Add(FundoStatus(evento, 4, out var corTexto));
            texto.SetResourceReference(TextBlock.ForegroundProperty, corTexto);
            pilula.Children.Add(texto);
            return pilula;
        }

        /// <summary>
        /// Fundo na cor do status (pílula do mês e card da semana): Agendado/Faturado preenchido na cor principal,
        /// orçamento com sinal em âmbar preenchido, orçamento sem sinal só com contorno tracejado (não bloqueia a data).
        /// </summary>
        private static UIElement FundoStatus(EventoAgenda evento, double raio, out string corTexto)
        {
            switch (evento.Situacao)
            {
                case SituacaoAgenda.Confirmado:
                    corTexto = "CorTextoSobrePrincipal";
                    return Fundo("CorBotaoPrincipal");
                case SituacaoAgenda.OrcamentoComSinal:
                    corTexto = "CorTextoSobreOrcamento";
                    return Fundo("CorOrcamentoPreenchido");
                default:
                    corTexto = "CorOrcamento";
                    var contorno = new Rectangle { RadiusX = raio, RadiusY = raio, StrokeThickness = 1, StrokeDashArray = [3, 2] };
                    contorno.SetResourceReference(Shape.StrokeProperty, "CorOrcamento");
                    return contorno;
            }

            Border Fundo(string chave)
            {
                var fundo = new Border { CornerRadius = new CornerRadius(raio) };
                fundo.SetResourceReference(Border.BackgroundProperty, chave);
                return fundo;
            }
        }

        // ------------------------------------------------------------------
        // Visão Semana
        // ------------------------------------------------------------------

        private void MostrarSemana()
        {
            var inicio = InicioSemana(_referencia);
            var fim = inicio.AddDays(6);
            TxtPeriodo.Text = Formatos.PeriodoCurto(inicio, fim);
            _eventos = Consultar(inicio, fim);

            CabecalhoVisaoSemana.Children.Clear();
            ColunasSemana.Children.Clear();
            for (int i = 0; i < 7; i++)
            {
                var data = inicio.AddDays(i);
                CabecalhoVisaoSemana.Children.Add(CriarCabecalhoDiaSemana(data));

                var cards = new StackPanel { Margin = new Thickness(6, 6, 6, 12) };
                foreach (var evento in _eventos.Where(e => e.Data == data))
                {
                    cards.Children.Add(CriarCardSemana(evento));
                }

                var coluna = new Border { BorderThickness = new Thickness(0, 0, i == 6 ? 0 : 1, 0), Child = cards, Background = Brushes.Transparent };
                coluna.SetResourceReference(Border.BorderBrushProperty, "CorBorda");
                if (data == DateTime.Today)
                {
                    coluna.SetResourceReference(Border.BackgroundProperty, "CorSelecao");
                }
                else if (data < DateTime.Today)
                {
                    cards.Opacity = 0.7;
                }

                ColunasSemana.Children.Add(coluna);
            }

            ColunasSemana.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
        }

        /// <summary>"DOM" + número do dia; clicar abre o dia na visão Dia.</summary>
        private FrameworkElement CriarCabecalhoDiaSemana(DateTime data)
        {
            bool hoje = data == DateTime.Today;
            var nome = new TextBlock
            {
                Text = DiasSemana[(int)data.DayOfWeek],
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            nome.SetResourceReference(TextBlock.ForegroundProperty, "CorTextoSecundario");

            var numero = new TextBlock
            {
                Text = data.Day.ToString(),
                FontSize = 16,
                FontWeight = hoje ? FontWeights.Bold : FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            numero.SetResourceReference(TextBlock.ForegroundProperty, hoje ? "CorEnfase" : "CorTexto");

            var painel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            painel.Children.Add(nome);
            painel.Children.Add(numero);

            var cabecalho = new Border
            {
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = painel,
                ToolTip = "Ver o dia"
            };
            cabecalho.MouseLeftButtonUp += (_, _) => TrocarVisao(VisaoAgenda.Dia, data);
            return cabecalho;
        }

        /// <summary>Card compacto da semana: hora início–fim, título e cliente, na cor do status.</summary>
        private FrameworkElement CriarCardSemana(EventoAgenda evento)
        {
            var fundo = FundoStatus(evento, 6, out var corTexto);

            var textos = new StackPanel { Margin = new Thickness(8, 6, 8, 7) };
            var horario = new TextBlock { Text = $"{Formatos.Hora(evento.Inicio)}–{Formatos.Hora(evento.Fim)}", FontSize = 11, FontWeight = FontWeights.SemiBold };
            var titulo = new TextBlock
            {
                Text = evento.Titulo,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 34,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 0)
            };
            var cliente = new TextBlock { Text = evento.Cliente, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, Opacity = 0.85 };
            foreach (var texto in new[] { horario, titulo, cliente })
            {
                texto.SetResourceReference(TextBlock.ForegroundProperty, corTexto);
                textos.Children.Add(texto);
            }

            var card = new Grid
            {
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand,
                ToolTip = $"{evento.Titulo} · {evento.Cliente} · {evento.NomeStatus}"
            };
            card.Children.Add(fundo);
            card.Children.Add(textos);
            card.MouseEnter += (_, _) => card.Opacity = 0.85;
            card.MouseLeave += (_, _) => card.Opacity = 1;
            card.MouseLeftButtonUp += (_, _) => AbrirAgendamento(evento);
            return card;
        }

        // ------------------------------------------------------------------
        // Visão Dia
        // ------------------------------------------------------------------

        /// <summary>Um dia por vez, lista por horário. Evento que passa da meia-noite aparece no dia em que começa (RN28).</summary>
        private void MostrarDiaUnico()
        {
            var dia = _referencia.Date;
            TxtPeriodo.Text = $"{NomesDias[(int)dia.DayOfWeek]}, {dia.ToString("d 'de' MMMM 'de' yyyy", Formatos.Brasil)}";
            _eventos = Consultar(dia, dia);

            ListaVisaoDia.Children.Clear();
            foreach (var evento in _eventos)
            {
                var hora = new TextBlock
                {
                    Text = Formatos.Hora(evento.Inicio),
                    FontSize = 15,
                    FontWeight = FontWeights.SemiBold,
                    Width = 64,
                    Margin = new Thickness(0, 14, 0, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };
                hora.SetResourceReference(TextBlock.ForegroundProperty, "CorTexto");

                var linha = new DockPanel { MaxWidth = 880, HorizontalAlignment = HorizontalAlignment.Left };
                DockPanel.SetDock(hora, Dock.Left);
                linha.Children.Add(hora);
                linha.Children.Add(CriarCartaoEvento(evento));
                ListaVisaoDia.Children.Add(linha);
            }

            VazioVisaoDia.Visibility = _eventos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ListaVisaoDia.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
        }

        private void TrocarVisao(VisaoAgenda visao, DateTime? data = null)
        {
            if (data is { } d)
            {
                _referencia = d;
            }

            _visao = visao;
            _visaoLembrada = visao;
            (visao switch { VisaoAgenda.Semana => AbaSemana, VisaoAgenda.Dia => AbaDia, _ => AbaMes }).IsChecked = true;
            FecharDia();
            Mostrar();
        }

        private void AbaVisao_Click(object sender, RoutedEventArgs e)
        {
            var visao = sender == AbaSemana ? VisaoAgenda.Semana : sender == AbaDia ? VisaoAgenda.Dia : VisaoAgenda.Mes;
            // Saindo do Mês com um dia aberto no painel, a nova visão começa nesse dia
            bool painelAberto = _visao == VisaoAgenda.Mes && PainelDia.Visibility == Visibility.Visible;
            TrocarVisao(visao, painelAberto ? _diaSelecionado : null);
        }

        // ------------------------------------------------------------------
        // Painel do dia
        // ------------------------------------------------------------------

        private void AbrirDia(DateTime data)
        {
            _diaSelecionado = data;
            foreach (Border celula in GradeDias.Children)
            {
                if ((DateTime)celula.Tag == data)
                {
                    celula.SetResourceReference(Border.BackgroundProperty, "CorSelecao");
                }
                else
                {
                    celula.Background = Brushes.Transparent;
                }
            }

            var eventosDia = _eventos.Where(e => e.Data == data).ToList();
            TxtDiaTitulo.Text = $"{NomesDias[(int)data.DayOfWeek]}, {data.ToString("d 'de' MMMM 'de' yyyy", Formatos.Brasil)}";
            TxtDiaResumo.Text = eventosDia.Count switch
            {
                0 => "Sem eventos",
                1 => "1 evento",
                _ => $"{eventosDia.Count} eventos"
            };

            ListaDia.Children.Clear();
            foreach (var evento in eventosDia)
            {
                ListaDia.Children.Add(CriarCartaoEvento(evento));
            }

            VazioDia.Visibility = eventosDia.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (PainelDia.Visibility != Visibility.Visible)
            {
                AjustarLarguraPainel();
                PainelDia.Visibility = Visibility.Visible;
                Sombra.Visibility = Visibility.Visible;
                var facilidade = new CubicEase { EasingMode = EasingMode.EaseOut };
                DeslocamentoPainel.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(PainelDia.Width, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = facilidade });
                Sombra.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 0.6, TimeSpan.FromMilliseconds(220)));
            }
        }

        private Border CriarCartaoEvento(EventoAgenda evento)
        {
            bool orcamento = evento.Status == StatusAgendamento.Orcamento;

            var faixa = new Border { Width = 3, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 14, 0) };
            faixa.SetResourceReference(Border.BackgroundProperty, orcamento ? "CorOrcamento" : "CorBotaoPrincipal");

            var horario = new TextBlock { Text = $"{Formatos.Hora(evento.Inicio)} – {Formatos.Hora(evento.Fim)}", FontSize = 12 };
            horario.SetResourceReference(TextBlock.ForegroundProperty, "CorTextoSecundario");
            if (evento.Fim < evento.Inicio)
            {
                horario.Text += "  (termina no dia seguinte)";
            }

            var titulo = new TextBlock { Text = evento.Titulo, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 4) };
            var cliente = Auxiliar($"Cliente: {evento.Cliente} · {Documentos.FormatarTelefone(evento.Telefone)}");
            var detalhes = Auxiliar(string.Join(" · ", new[]
            {
                evento.Servicos,
                evento.Convidados is > 0 ? $"{evento.Convidados} pessoas" : null
            }.Where(t => !string.IsNullOrEmpty(t))));

            var selo = new Border { Child = new TextBlock { Text = evento.NomeStatus }, Margin = new Thickness(0, 10, 0, 0) };
            selo.SetResourceReference(StyleProperty, evento.Situacao switch
            {
                SituacaoAgenda.OrcamentoComSinal => "SeloOrcamento",
                SituacaoAgenda.OrcamentoSemSinal => "SeloOrcamentoContorno",
                _ => evento.Status == StatusAgendamento.Faturado ? "SeloDestaque" : "SeloPrincipal"
            });

            var seta = new Icone { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
            seta.SetResourceReference(Icone.DadosProperty, "IconeProximo");
            seta.SetResourceReference(ForegroundProperty, "CorTextoSecundario");

            var textos = new StackPanel();
            textos.Children.Add(horario);
            textos.Children.Add(titulo);
            textos.Children.Add(cliente);
            if (!string.IsNullOrEmpty(detalhes.Text))
            {
                textos.Children.Add(detalhes);
            }
            textos.Children.Add(selo);

            var linha = new DockPanel();
            DockPanel.SetDock(faixa, Dock.Left);
            DockPanel.SetDock(seta, Dock.Right);
            linha.Children.Add(faixa);
            linha.Children.Add(seta);
            linha.Children.Add(textos);

            var cartao = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 14, 12, 14),
                Margin = new Thickness(0, 0, 0, 12),
                Cursor = Cursors.Hand,
                Child = linha,
                ToolTip = "Abrir agendamento"
            };
            cartao.SetResourceReference(Border.BackgroundProperty, "CorFundo");
            cartao.SetResourceReference(Border.BorderBrushProperty, "CorBorda");
            cartao.MouseEnter += (_, _) => cartao.SetResourceReference(Border.BorderBrushProperty, "CorBotaoSecundario");
            cartao.MouseLeave += (_, _) => cartao.SetResourceReference(Border.BorderBrushProperty, "CorBorda");
            cartao.MouseLeftButtonUp += (_, _) => AbrirAgendamento(evento);
            return cartao;

            static TextBlock Auxiliar(string texto)
            {
                var bloco = new TextBlock { Text = texto, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis };
                bloco.SetResourceReference(TextBlock.ForegroundProperty, "CorTextoSecundario");
                return bloco;
            }
        }

        private void FecharDia()
        {
            if (PainelDia.Visibility != Visibility.Visible)
            {
                return;
            }

            var saida = new DoubleAnimation(0, PainelDia.Width, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            saida.Completed += (_, _) =>
            {
                PainelDia.Visibility = Visibility.Collapsed;
                Sombra.Visibility = Visibility.Collapsed;
            };
            DeslocamentoPainel.BeginAnimation(TranslateTransform.XProperty, saida);
            Sombra.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(180)));
        }

        private void AjustarLarguraPainel() => PainelDia.Width = Math.Min(400, Math.Max(280, ActualWidth - 64));

        private void AbrirAgendamento(EventoAgenda evento) => MostrarAgendamento(new AgendamentoWindow(evento.Id));

        /// <summary>"+ Novo Agendamento" do topo: sem data preenchida.</summary>
        private void BtnNovo_Click(object sender, RoutedEventArgs e) => MostrarAgendamento(new AgendamentoWindow(null));

        /// <summary>"Novo agendamento" do painel do dia: já com a data do dia.</summary>
        private void BtnNovoDia_Click(object sender, RoutedEventArgs e) => MostrarAgendamento(new AgendamentoWindow(null, _diaSelecionado));

        /// <summary>"Novo agendamento" da visão Dia vazia: já com a data do dia.</summary>
        private void BtnNovoVisaoDia_Click(object sender, RoutedEventArgs e) => MostrarAgendamento(new AgendamentoWindow(null, _referencia.Date));

        /// <summary>Agendamento em popup por cima da Agenda; ao gravar, recarrega a visão (e o painel do dia, se aberto).</summary>
        private void MostrarAgendamento(AgendamentoWindow janela)
        {
            MainWindow.Atual.MostrarModal(janela);
            if (!janela.Gravou)
            {
                return;
            }

            Mostrar();
            if (_visao == VisaoAgenda.Mes && PainelDia.Visibility == Visibility.Visible && _diaSelecionado is { } dia)
            {
                AbrirDia(dia);
            }
        }

        /// <summary>‹ ›: um mês, uma semana ou um dia, conforme a visão.</summary>
        private void Avancar(int sentido)
        {
            _referencia = _visao switch
            {
                VisaoAgenda.Semana => _referencia.AddDays(7 * sentido),
                VisaoAgenda.Dia => _referencia.AddDays(sentido),
                _ => PrimeiroDoMes.AddMonths(sentido)
            };
            FecharDia();
            Mostrar();
        }

        private void BtnAnterior_Click(object sender, RoutedEventArgs e) => Avancar(-1);

        private void BtnProximo_Click(object sender, RoutedEventArgs e) => Avancar(1);

        /// <summary>Volta para a data atual na visão ativa (no Mês, abre também o painel de hoje).</summary>
        private void BtnHoje_Click(object sender, RoutedEventArgs e)
        {
            _referencia = DateTime.Today;
            Mostrar();
            if (_visao == VisaoAgenda.Mes)
            {
                AbrirDia(DateTime.Today);
            }
        }

        private void BtnFecharDia_Click(object sender, RoutedEventArgs e) => FecharDia();

        private void Sombra_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => FecharDia();

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                FecharDia();
            }
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (PainelDia.Visibility == Visibility.Visible)
            {
                AjustarLarguraPainel();
            }
        }

        private enum VisaoAgenda
        {
            Mes,
            Semana,
            Dia
        }

        private enum SituacaoAgenda
        {
            Confirmado,
            OrcamentoComSinal,
            OrcamentoSemSinal
        }

        private sealed record EventoAgenda(int Id, string Titulo, DateTime Data, TimeSpan Inicio, TimeSpan Fim, string Cliente,
            string Telefone, string Servicos, short? Convidados, StatusAgendamento Status, bool TemSinal)
        {
            public SituacaoAgenda Situacao => Status != StatusAgendamento.Orcamento
                ? SituacaoAgenda.Confirmado
                : TemSinal ? SituacaoAgenda.OrcamentoComSinal : SituacaoAgenda.OrcamentoSemSinal;

            /// <summary>Status real (no painel do dia não se junta Agendado/Faturado).</summary>
            public string NomeStatus => Status switch
            {
                StatusAgendamento.Orcamento => TemSinal ? "Orçamento com sinal" : "Orçamento",
                StatusAgendamento.Agendado => "Agendado",
                StatusAgendamento.Faturado => "Faturado",
                _ => "Cancelado"
            };
        }
    }
}
