using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using EspacoFestas.Controls;
using EspacoFestas.Models;
using EspacoFestas.Services;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Views
{
    /// <summary>
    /// Tela de agendamento em popup modal (RF07, RF08, RF09, RF11): cliente, evento, serviços com faixa (RN06),
    /// adicionais dos serviços escolhidos (RN07), resumo com total (RN09) e sinal (RN12).
    /// Faturado abre só para leitura (RN17), com o botão Cancelar agendamento.
    /// </summary>
    public partial class AgendamentoWindow : Window
    {
        private const int MaximoSugestoesCliente = 8;
        private static readonly string[] NomesDias = ["domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado"];

        private int? _id;
        private StatusAgendamento? _status;
        private bool _carregando = true;
        private bool _alterado;
        private bool _somenteLeitura;
        private bool _fechamentoConfirmado;
        private bool _ignorarBuscaCliente;
        private DateTime? _dataDisponibilidade;

        private List<ClienteOpcao> _clientes = [];
        private int? _clienteId;

        private Dictionary<int, ServicoCadastro> _cadastroServicos = [];
        private Dictionary<int, AdicionalCadastro> _cadastroAdicionais = [];
        private HashSet<int> _ocupados = [];

        private readonly ObservableCollection<ServicoLinhaAgendamento> _servicos = [];
        private readonly ObservableCollection<AdicionalLinhaAgendamento> _adicionais = [];

        /// <summary>Linhas de adicional já montadas, para não perder o que foi digitado ao tirar e pôr um serviço.</summary>
        private readonly Dictionary<int, AdicionalLinhaAgendamento> _linhasAdicionais = [];

        /// <summary>Adicionais já gravados neste agendamento: continuam aparecendo mesmo se o vínculo com o serviço mudou.</summary>
        private readonly HashSet<int> _adicionaisGravados = [];

        /// <param name="id">Agendamento existente; nulo = novo.</param>
        /// <param name="data">Data já preenchida (novo agendamento a partir do painel do dia).</param>
        public AgendamentoWindow(int? id, DateTime? data = null)
        {
            InitializeComponent();
            _id = id;
            ListaServicos.ItemsSource = _servicos;
            ListaAdicionais.ItemsSource = _adicionais;

            Carregar(data);

            // Qualquer mudança depois de carregado marca alterações não salvas; editar um campo com erro limpa o erro dele
            var errosDosCampos = new Dictionary<TextBox, TextBlock>
            {
                [TxtTituloEvento] = ErroTitulo,
                [TxtData] = ErroData,
                [TxtInicio] = ErroInicio,
                [TxtFim] = ErroFim,
                [TxtConvidados] = ErroConvidados
            };
            AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, e) =>
            {
                MarcarAlterado();
                if (e.OriginalSource is TextBox campo && errosDosCampos.TryGetValue(campo, out var mensagem))
                {
                    LimparErro(mensagem, campo);
                }
            }), true);
            AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler((_, _) => MarcarAlterado()), true);
            AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler((_, _) => MarcarAlterado()), true);

            Loaded += (_, _) =>
            {
                _carregando = false;
                if (_somenteLeitura)
                {
                    Dispatcher.BeginInvoke(AplicarSomenteLeitura, System.Windows.Threading.DispatcherPriority.Loaded);
                }
                else if (_clienteId is null)
                {
                    TxtCliente.Focus();
                }
                else
                {
                    TxtTituloEvento.Focus();
                }
            };
            Closing += Window_Closing;
        }

        /// <summary>Verdadeiro quando algo foi gravado (a Agenda recarrega).</summary>
        public bool Gravou { get; private set; }

        // ------------------------------------------------------------------
        // Carga
        // ------------------------------------------------------------------

        private void Carregar(DateTime? data)
        {
            using var contexto = App.CriarContexto();

            Agendamento? agendamento = null;
            if (_id is not null)
            {
                agendamento = contexto.Agendamentos.AsNoTracking()
                    .Include(a => a.Servicos)
                    .Include(a => a.Adicionais)
                    .Single(a => a.Id == _id);
                _status = agendamento.Status;
            }

            int clienteAtual = agendamento?.ClienteId ?? 0;
            _clientes = contexto.Clientes.AsNoTracking()
                .Where(c => c.Ativo || c.Id == clienteAtual)
                .OrderBy(c => c.Nome)
                .Select(c => new ClienteOpcao(c.Id, c.Nome, c.Telefone, c.CpfCnpj))
                .ToList();

            _cadastroServicos = contexto.Servicos.AsNoTracking()
                .Select(s => new
                {
                    s.Id,
                    s.Nome,
                    s.Ativo,
                    Faixas = s.Faixas.OrderBy(f => f.OrdemExibicao).ThenBy(f => f.Id)
                        .Select(f => new FaixaCadastro(f.Id, f.Nome, f.Valor, f.Ativo)).ToList(),
                    Adicionais = s.ServicoAdicionais.Select(sa => sa.AdicionalId).ToList()
                })
                .AsEnumerable()
                .ToDictionary(s => s.Id, s => new ServicoCadastro(s.Nome, s.Ativo, s.Faixas, s.Adicionais));

            _cadastroAdicionais = contexto.Adicionais.AsNoTracking()
                .Select(a => new { a.Id, a.Nome, a.CobraPorQuantidade, a.ValorVenda, a.ValorCusto, a.Ativo })
                .AsEnumerable()
                .ToDictionary(a => a.Id, a => new AdicionalCadastro(a.Nome, a.CobraPorQuantidade, a.ValorVenda, a.ValorCusto, a.Ativo));

            CmbEspecie.ItemsSource = contexto.Especies.AsNoTracking()
                .Where(e => e.Ativo)
                .OrderBy(e => e.Descricao)
                .Select(e => new EspecieOpcao(e.Id, e.Descricao))
                .ToList();

            if (agendamento is null)
            {
                if (data is not null)
                {
                    TxtData.Text = Formatos.Data(data);
                }
            }
            else
            {
                PreencherExistente(contexto, agendamento);
            }

            AtualizarDisponibilidade(contexto);
            ConfigurarEstado();
            AtualizarTotais();
        }

        private void PreencherExistente(Data.EspacoFestasContext contexto, Agendamento agendamento)
        {
            if (_clientes.FirstOrDefault(c => c.Id == agendamento.ClienteId) is { } cliente)
            {
                SelecionarCliente(cliente);
            }

            TxtTituloEvento.Text = agendamento.Titulo;
            TxtData.Text = Formatos.Data(agendamento.DataEvento);
            TxtInicio.Text = Formatos.Hora(agendamento.HoraInicio);
            TxtFim.Text = Formatos.Hora(agendamento.HoraFim);
            TxtConvidados.Text = agendamento.QuantidadeConvidados?.ToString() ?? string.Empty;
            TxtDesconto.Text = agendamento.ValorDesconto > 0 ? Formatos.Decimal(agendamento.ValorDesconto) : string.Empty;

            // Serviços: a faixa gravada aparece mesmo que tenha sido inativada depois; o valor é o gravado (RN08)
            foreach (var item in agendamento.Servicos)
            {
                var linha = CriarLinhaServico(item.ServicoId, item.ServicoFaixaId);
                var faixa = linha.Faixas.FirstOrDefault(f => f.Id == item.ServicoFaixaId);
                if (faixa is not null)
                {
                    linha.Escolher(faixa);
                }

                linha.ValorTexto = Formatos.Decimal(item.ValorUnitario);
                _servicos.Add(linha);
            }

            // Adicionais: valor de venda e custo gravados no lançamento (RN08)
            foreach (var item in agendamento.Adicionais)
            {
                var cadastro = _cadastroAdicionais.GetValueOrDefault(item.AdicionalId);
                var linha = new AdicionalLinhaAgendamento(item.AdicionalId, cadastro?.Nome ?? "Adicional",
                    cadastro?.CobraPorQuantidade ?? item.Quantidade > 1, item.ValorUnitario, item.ValorCustoUnitario)
                {
                    QuantidadeTexto = item.Quantidade.ToString(),
                    Selecionado = true
                };
                _linhasAdicionais[item.AdicionalId] = linha;
                _adicionaisGravados.Add(item.AdicionalId);
            }

            decimal pago = AgendamentoService.ValorPago(contexto, agendamento.Id);
            if (pago > 0)
            {
                TxtSinalPago.Text = $"Já pago pelo cliente (sinal e pagamentos): {Formatos.Moeda(pago)}.";
                TxtSinalPago.Visibility = Visibility.Visible;
                TxtRotuloSinal.Text = "Novo sinal (opcional)";
            }

            if (agendamento.Status == StatusAgendamento.Cancelado)
            {
                TxtSomenteLeitura.Text = $"Cancelado em {Formatos.Data(agendamento.DataCancelamento)}. Motivo: {agendamento.MotivoCancelamento}";
            }

            AtualizarAdicionais();
        }

        /// <summary>Botões, selo e modo leitura conforme o status.</summary>
        private void ConfigurarEstado()
        {
            if (_status is null)
            {
                return;
            }

            TxtTitulo.Text = "Agendamento";
            SeloStatus.Visibility = Visibility.Visible;
            (TxtStatus.Text, var estilo) = _status switch
            {
                StatusAgendamento.Orcamento => ("Orçamento", "SeloOrcamento"),
                StatusAgendamento.Agendado => ("Agendado", "SeloPrincipal"),
                StatusAgendamento.Faturado => ("Faturado", "SeloDestaque"),
                _ => ("Cancelado", "SeloErro")
            };
            SeloStatus.SetResourceReference(StyleProperty, estilo);

            switch (_status)
            {
                case StatusAgendamento.Agendado:
                    // Agendado não volta a orçamento; "Agendar" vira "Salvar"
                    BtnSalvarOrcamento.Visibility = Visibility.Collapsed;
                    BtnAgendar.Content = "Salvar";
                    BtnAgendarFaturar.Content = "Faturar";
                    break;

                case StatusAgendamento.Faturado:
                case StatusAgendamento.Cancelado:
                    _somenteLeitura = true;
                    AvisoSomenteLeitura.Visibility = Visibility.Visible;
                    if (_status == StatusAgendamento.Faturado)
                    {
                        TxtSomenteLeitura.Text = "Agendamento faturado: itens, valores, desconto e formas de pagamento não podem mais ser alterados (RN17). A única ação possível é cancelar.";
                    }

                    BtnCancelar.Visibility = BtnSalvarOrcamento.Visibility = BtnAgendar.Visibility = BtnAgendarFaturar.Visibility = Visibility.Collapsed;
                    PainelSinal.Visibility = Visibility.Collapsed;
                    break;
            }

            if (_status != StatusAgendamento.Cancelado)
            {
                BtnCancelarAgendamento.Visibility = Visibility.Visible;
                var avaliacao = AgendamentoService.AvaliarCancelamento(_id!.Value);
                BtnCancelarAgendamento.IsEnabled = avaliacao.Permitido;
                BtnCancelarAgendamento.ToolTip = avaliacao.Impedimento;
            }
        }

        /// <summary>Somente leitura: campos sem edição e sem os botões de alterar itens.</summary>
        private void AplicarSomenteLeitura()
        {
            BtnNovoCliente.Visibility = Visibility.Collapsed;
            PainelAdicionarServico.Visibility = Visibility.Collapsed;
            TxtAjudaServico.Visibility = Visibility.Collapsed;
            foreach (var elemento in Descendentes(Conteudo).Concat(Descendentes(Resumo)))
            {
                switch (elemento)
                {
                    case TextBox texto:
                        texto.IsReadOnly = true;
                        texto.IsTabStop = false;
                        break;
                    case Button botao:
                        botao.Visibility = Visibility.Collapsed;
                        break;
                    case ToggleButton or ComboBox:
                        ((Control)elemento).IsEnabled = false;
                        break;
                }
            }
        }

        private static IEnumerable<DependencyObject> Descendentes(DependencyObject raiz)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
            {
                var filho = VisualTreeHelper.GetChild(raiz, i);
                yield return filho;
                foreach (var neto in Descendentes(filho))
                {
                    yield return neto;
                }
            }
        }

        private void MarcarAlterado()
        {
            if (!_carregando)
            {
                _alterado = true;
            }
        }

        // ------------------------------------------------------------------
        // 1. Cliente: busca por nome, telefone ou CPF/CNPJ
        // ------------------------------------------------------------------

        private void TxtCliente_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_ignorarBuscaCliente)
            {
                return;
            }

            _clienteId = null;
            TxtClienteDetalhe.Visibility = Visibility.Collapsed;
            LimparErro(ErroCliente, TxtCliente);
            MostrarSugestoes();
        }

        private void MostrarSugestoes()
        {
            var busca = TxtCliente.Text.Trim();
            if (busca.Length == 0 || _clienteId is not null)
            {
                PopupClientes.IsOpen = false;
                return;
            }

            var digitos = Documentos.SoDigitos(busca);
            var comparador = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
            var achados = _clientes
                .Where(c => comparador.IndexOf(c.Nome, busca, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0
                    || (digitos.Length >= 3 && (Documentos.SoDigitos(c.Telefone).Contains(digitos) || (c.CpfCnpj ?? string.Empty).Contains(digitos))))
                .Take(MaximoSugestoesCliente)
                .ToList();

            ListaClientes.ItemsSource = achados;
            PopupClientes.IsOpen = achados.Count > 0;
        }

        private void TxtCliente_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (_clienteId is null && !_carregando)
            {
                MostrarSugestoes();
            }
        }

        private void TxtCliente_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!PopupClientes.IsOpen || ListaClientes.Items.Count == 0)
            {
                return;
            }

            if (e.Key == Key.Down)
            {
                ListaClientes.SelectedIndex = 0;
                (ListaClientes.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                SelecionarCliente((ClienteOpcao)ListaClientes.Items[0]);
                e.Handled = true;
            }
        }

        private void ListaClientes_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && ListaClientes.SelectedItem is ClienteOpcao cliente)
            {
                SelecionarCliente(cliente);
                e.Handled = true;
            }
        }

        private void ListaClientes_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement { DataContext: ClienteOpcao cliente })
            {
                SelecionarCliente(cliente);
            }
        }

        private void SelecionarCliente(ClienteOpcao cliente)
        {
            _clienteId = cliente.Id;
            _ignorarBuscaCliente = true;
            TxtCliente.Text = cliente.Nome;
            TxtCliente.CaretIndex = TxtCliente.Text.Length;
            _ignorarBuscaCliente = false;
            TxtClienteDetalhe.Text = cliente.Detalhe;
            TxtClienteDetalhe.Visibility = string.IsNullOrEmpty(cliente.Detalhe) ? Visibility.Collapsed : Visibility.Visible;
            PopupClientes.IsOpen = false;
            LimparErro(ErroCliente, TxtCliente);
            if (!_carregando)
            {
                TxtTituloEvento.Focus();
            }
        }

        private void BtnNovoCliente_Click(object sender, RoutedEventArgs e)
        {
            PopupClientes.IsOpen = false;
            var cadastro = new ClienteRapidoWindow(_clienteId is null ? TxtCliente.Text.Trim() : string.Empty);
            if (Modal.Mostrar(cadastro, this, Overlay) == true && cadastro.Cliente is { } novo)
            {
                _clientes.Add(novo);
                SelecionarCliente(novo);
            }
        }

        // ------------------------------------------------------------------
        // 2. Evento
        // ------------------------------------------------------------------

        private void BtnCalendario_Click(object sender, RoutedEventArgs e) =>
            SeletorData.Abrir(TxtData, CaixaData, () => TxtData_LostFocus(TxtData, e));

        private void TxtData_LostFocus(object sender, RoutedEventArgs e)
        {
            if (LerData(TxtData.Text) is { } data)
            {
                TxtData.Text = Formatos.Data(data);
                LimparErro(ErroData, TxtData);
            }

            AtualizarDiaSemana();
            if (LerData(TxtData.Text) != _dataDisponibilidade)
            {
                using var contexto = App.CriarContexto();
                AtualizarDisponibilidade(contexto);
            }
        }

        private void AtualizarDiaSemana()
        {
            var data = LerData(TxtData.Text);
            TxtDiaSemana.Text = data is null ? string.Empty : Formatos.Capitalizar(NomesDias[(int)data.Value.DayOfWeek]);
        }

        private void Hora_LostFocus(object sender, RoutedEventArgs e)
        {
            var campo = (TextBox)sender;
            if (LerHora(campo.Text) is { } hora)
            {
                campo.Text = Formatos.Hora(hora);
                LimparErro(campo == TxtInicio ? ErroInicio : ErroFim, campo);
            }

            AtualizarAvisoDiaSeguinte();
        }

        /// <summary>RN28: hora de fim menor que a de início = termina no dia seguinte.</summary>
        private void AtualizarAvisoDiaSeguinte()
        {
            bool diaSeguinte = LerHora(TxtInicio.Text) is { } inicio && LerHora(TxtFim.Text) is { } fim && fim < inicio;
            AvisoDiaSeguinte.Visibility = diaSeguinte ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Aceita 16/10/2026, 16/10/26, 16/10 (ano atual) e 16102026.</summary>
        private static DateTime? LerData(string texto)
        {
            var limpo = texto.Trim();
            if (limpo.Length == 0)
            {
                return null;
            }

            string[] formatos = ["dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy", "ddMMyyyy", "ddMMyy"];
            if (DateTime.TryParseExact(limpo, formatos, Formatos.Brasil, DateTimeStyles.None, out var data))
            {
                return data.Date;
            }

            return DateTime.TryParseExact(limpo, ["dd/MM", "d/M"], Formatos.Brasil, DateTimeStyles.None, out data)
                ? new DateTime(DateTime.Today.Year, data.Month, data.Day)
                : null;
        }

        /// <summary>Aceita 19:30, 19h30, 1930, 19 e 7:05.</summary>
        private static TimeSpan? LerHora(string texto)
        {
            var limpo = texto.Trim().ToLowerInvariant().Replace('h', ':').TrimEnd(':');
            int horas, minutos = 0;
            if (limpo.Contains(':'))
            {
                var partes = limpo.Split(':');
                if (partes.Length != 2 || !int.TryParse(partes[0], out horas) || !int.TryParse(partes[1], out minutos))
                {
                    return null;
                }
            }
            else if (limpo.Length is 1 or 2 && int.TryParse(limpo, out horas))
            {
            }
            else if (limpo.Length is 3 or 4 && int.TryParse(limpo, out var numero))
            {
                horas = numero / 100;
                minutos = numero % 100;
            }
            else
            {
                return null;
            }

            return horas is >= 0 and <= 23 && minutos is >= 0 and <= 59 ? new TimeSpan(horas, minutos, 0) : null;
        }

        // ------------------------------------------------------------------
        // 3. Serviços e faixas
        // ------------------------------------------------------------------

        /// <summary>
        /// RN03 + RN10: só serviços ativos, com faixa ativa e, com o bloqueio ligado, livres na data.
        /// Serviço já na grade que ficou ocupado na nova data é marcado com erro.
        /// </summary>
        private void AtualizarDisponibilidade(Data.EspacoFestasContext contexto)
        {
            var data = LerData(TxtData.Text);
            _dataDisponibilidade = data;
            _ocupados = data is null ? [] : AgendamentoService.ServicosOcupados(contexto, data.Value, _id);

            var naGrade = _servicos.Select(s => s.ServicoId).ToHashSet();
            var opcoes = _cadastroServicos
                .Where(s => s.Value.Ativo && s.Value.Faixas.Any(f => f.Ativa) && !_ocupados.Contains(s.Key) && !naGrade.Contains(s.Key))
                .OrderBy(s => s.Value.Nome)
                .Select(s => new ServicoOpcao(s.Key, s.Value.Nome))
                .ToList();

            CmbServico.ItemsSource = opcoes;
            CmbServico.SelectedItem = null;
            CmbServico.IsEnabled = opcoes.Count > 0;
            CmbServico.ToolTip = opcoes.Count > 0 ? null
                : _ocupados.Count > 0 ? "Nenhum outro serviço livre nesta data" : "Nenhum outro serviço disponível";
            TxtAjudaServico.Text = opcoes.Count == 0 && _servicos.Count == 0 && _ocupados.Count > 0
                ? $"Nenhum serviço livre em {Formatos.Data(data)}: com \"um agendamento por dia\" ligado, os serviços desta data já estão reservados."
                : "Escolha o serviço e marque a faixa de preço; o valor pode ser ajustado.";

            foreach (var linha in _servicos)
            {
                if (_ocupados.Contains(linha.ServicoId))
                {
                    linha.Erro = $"{linha.Nome} já está reservado em {Formatos.Data(data)} (RN10).";
                }
                else if (linha.Erro?.Contains("RN10") == true)
                {
                    linha.Erro = null;
                }
            }

            AtualizarDiaSemana();
        }

        private void CmbServico_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            BtnAdicionarServico.IsEnabled = CmbServico.SelectedItem is not null;

        private void BtnAdicionarServico_Click(object sender, RoutedEventArgs e)
        {
            if (CmbServico.SelectedItem is not ServicoOpcao opcao)
            {
                return;
            }

            _servicos.Add(CriarLinhaServico(opcao.Id, null));
            LimparErro(ErroServicos, null);
            AtualizarAdicionais();
            using (var contexto = App.CriarContexto())
            {
                AtualizarDisponibilidade(contexto);
            }

            AtualizarTotais();
        }

        /// <summary>Linha de serviço com as faixas ativas (e a faixa já gravada, se tiver sido inativada).</summary>
        private ServicoLinhaAgendamento CriarLinhaServico(int servicoId, int? faixaGravada)
        {
            var cadastro = _cadastroServicos[servicoId];
            var faixas = cadastro.Faixas
                .Where(f => f.Ativa || f.Id == faixaGravada)
                .Select(f => new FaixaOpcao(f.Id, f.Nome, f.Valor, f.Ativa))
                .ToList();
            var linha = new ServicoLinhaAgendamento(servicoId, cadastro.Nome, faixas);
            linha.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ServicoLinhaAgendamento.ValorTexto))
                {
                    if (linha.Erro is not null && !_ocupados.Contains(linha.ServicoId) && linha.FaixaSelecionada is not null)
                    {
                        linha.Erro = null;
                    }

                    AtualizarTotais();
                }
            };
            return linha;
        }

        /// <summary>
        /// Checked (e não Click) para pegar também teclado e acessibilidade. Se a faixa já é a escolhida
        /// (ex.: agendamento aberto do banco), não mexe: o valor gravado pode ter sido editado (RN06).
        /// </summary>
        private void Faixa_Checked(object sender, RoutedEventArgs e)
        {
            var radio = (RadioButton)sender;
            if (radio.Tag is ServicoLinhaAgendamento linha && radio.DataContext is FaixaOpcao faixa && linha.FaixaSelecionada != faixa)
            {
                linha.Escolher(faixa);
                AtualizarTotais();
            }
        }

        private void BtnRemoverServico_Click(object sender, RoutedEventArgs e)
        {
            var linha = (ServicoLinhaAgendamento)((FrameworkElement)sender).DataContext;
            _servicos.Remove(linha);
            AtualizarAdicionais();
            using (var contexto = App.CriarContexto())
            {
                AtualizarDisponibilidade(contexto);
            }

            AtualizarTotais();
            MarcarAlterado();
        }

        private void ValorServico_LostFocus(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is ServicoLinhaAgendamento { Valor: { } valor } linha)
            {
                linha.ValorTexto = Formatos.Decimal(valor);
            }
        }

        // ------------------------------------------------------------------
        // 4. Adicionais dos serviços escolhidos (RN07)
        // ------------------------------------------------------------------

        private void AtualizarAdicionais()
        {
            var ids = _servicos
                .SelectMany(s => _cadastroServicos[s.ServicoId].Adicionais)
                .Where(id => _cadastroAdicionais.TryGetValue(id, out var a) && a.Ativo)
                .Concat(_adicionaisGravados)
                .Distinct()
                .ToList();

            _adicionais.Clear();
            foreach (var id in ids.OrderBy(id => _linhasAdicionais.GetValueOrDefault(id)?.Nome ?? _cadastroAdicionais[id].Nome))
            {
                if (!_linhasAdicionais.TryGetValue(id, out var linha))
                {
                    // Valor de venda e custo copiados do cadastro agora (RN08)
                    var cadastro = _cadastroAdicionais[id];
                    linha = new AdicionalLinhaAgendamento(id, cadastro.Nome, cadastro.CobraPorQuantidade, cadastro.ValorVenda, cadastro.ValorCusto);
                    _linhasAdicionais[id] = linha;
                }

                linha.PropertyChanged -= LinhaAdicional_PropertyChanged;
                linha.PropertyChanged += LinhaAdicional_PropertyChanged;
                _adicionais.Add(linha);
            }

            bool vazio = _adicionais.Count == 0;
            VazioAdicionais.Text = _servicos.Count == 0
                ? "Escolha um serviço para ver os adicionais."
                : "Os serviços escolhidos não oferecem adicionais.";
            VazioAdicionais.Visibility = vazio ? Visibility.Visible : Visibility.Collapsed;
            CabecalhoAdicionais.Visibility = vazio ? Visibility.Collapsed : Visibility.Visible;
            VazioServicos.Visibility = _servicos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LinhaAdicional_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AdicionalLinhaAgendamento.Total))
            {
                AtualizarTotais();
            }
        }

        private void ValorAdicional_LostFocus(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is AdicionalLinhaAgendamento { Valor: { } valor } linha)
            {
                linha.ValorTexto = Formatos.Decimal(valor);
            }
        }

        // ------------------------------------------------------------------
        // 5. Resumo (RN09) e sinal
        // ------------------------------------------------------------------

        private decimal SubtotalServicos => _servicos.Sum(s => s.Valor ?? 0);

        private decimal SubtotalAdicionais => _adicionais.Sum(a => a.Total);

        private void AtualizarTotais()
        {
            if (TxtTotal is null)
            {
                return;
            }

            Formatos.TentarLerDecimal(TxtDesconto.Text, out var desconto);
            TxtSubtotalServicos.Text = Formatos.Moeda(SubtotalServicos);
            TxtSubtotalAdicionais.Text = Formatos.Moeda(SubtotalAdicionais);
            TxtTotal.Text = Formatos.Moeda(AgendamentoService.CalcularTotal(SubtotalServicos, SubtotalAdicionais, Math.Max(0, desconto)));
        }

        private void Totais_TextChanged(object sender, TextChangedEventArgs e)
        {
            LimparErro(ErroRodape, TxtDesconto);
            AtualizarTotais();
        }

        private void TxtDesconto_LostFocus(object sender, RoutedEventArgs e)
        {
            if (Formatos.TentarLerDecimal(TxtDesconto.Text, out var desconto))
            {
                TxtDesconto.Text = desconto == 0 ? string.Empty : Formatos.Decimal(desconto);
            }
        }

        private void Sinal_Changed(object sender, RoutedEventArgs e)
        {
            if (TxtSinal is null || CmbEspecie is null)
            {
                return;
            }

            Estilo.SetErro(TxtSinal, false);
            Estilo.SetErro(CmbEspecie, false);
            if (ErroRodape is not null && TxtDesconto is not null && !Estilo.GetErro(TxtDesconto))
            {
                ErroRodape.Visibility = Visibility.Collapsed;
            }
        }

        private void TxtSinal_LostFocus(object sender, RoutedEventArgs e)
        {
            if (Formatos.TentarLerDecimal(TxtSinal.Text, out var sinal))
            {
                TxtSinal.Text = sinal == 0 ? string.Empty : Formatos.Decimal(sinal);
            }
        }

        // ------------------------------------------------------------------
        // Validação inline (borda vermelha + mensagem curta, sem MessageBox)
        // ------------------------------------------------------------------

        private static void MostrarErro(TextBlock mensagem, Control? campo, string texto)
        {
            mensagem.Text = texto;
            mensagem.Visibility = Visibility.Visible;
            if (campo is not null)
            {
                Estilo.SetErro(campo, true);
            }
        }

        private static void LimparErro(TextBlock? mensagem, Control? campo)
        {
            if (mensagem is not null)
            {
                mensagem.Visibility = Visibility.Collapsed;
            }

            if (campo is not null)
            {
                Estilo.SetErro(campo, false);
            }
        }

        /// <summary>Valida tudo, mostra os erros junto dos campos e devolve os dados prontos para gravar.</summary>
        private DadosAgendamento? Validar()
        {
            var erros = new List<FrameworkElement>();
            void Erro(TextBlock mensagem, Control? campo, string texto)
            {
                MostrarErro(mensagem, campo, texto);
                erros.Add((FrameworkElement?)campo ?? mensagem);
            }

            TxtErroGeral.Visibility = Visibility.Collapsed;
            foreach (var (mensagem, campo) in new (TextBlock, Control)[]
                     {
                         (ErroCliente, TxtCliente), (ErroTitulo, TxtTituloEvento), (ErroData, TxtData), (ErroInicio, TxtInicio),
                         (ErroFim, TxtFim), (ErroConvidados, TxtConvidados), (ErroRodape, TxtDesconto)
                     })
            {
                LimparErro(mensagem, campo);
            }

            LimparErro(ErroServicos, null);
            Estilo.SetErro(TxtSinal, false);
            Estilo.SetErro(CmbEspecie, false);

            if (_clienteId is null)
            {
                Erro(ErroCliente, TxtCliente, TxtCliente.Text.Trim().Length == 0
                    ? "Escolha o cliente."
                    : "Escolha um cliente da lista ou cadastre um novo no +.");
            }

            if (string.IsNullOrWhiteSpace(TxtTituloEvento.Text))
            {
                Erro(ErroTitulo, TxtTituloEvento, "Informe o título do evento.");
            }

            var data = LerData(TxtData.Text);
            if (data is null)
            {
                Erro(ErroData, TxtData, TxtData.Text.Trim().Length == 0 ? "Informe a data." : "Data inválida.");
            }

            var inicio = LerHora(TxtInicio.Text);
            var fim = LerHora(TxtFim.Text);
            if (inicio is null)
            {
                Erro(ErroInicio, TxtInicio, TxtInicio.Text.Trim().Length == 0 ? "Obrigatório." : "Hora inválida.");
            }

            if (fim is null)
            {
                Erro(ErroFim, TxtFim, TxtFim.Text.Trim().Length == 0 ? "Obrigatório." : "Hora inválida.");
            }
            else if (fim == inicio)
            {
                Erro(ErroFim, TxtFim, "Igual ao início.");
            }

            short? convidados = null;
            if (TxtConvidados.Text.Trim().Length > 0)
            {
                if (short.TryParse(TxtConvidados.Text.Trim(), out var qtd) && qtd >= 1)
                {
                    convidados = qtd;
                }
                else
                {
                    Erro(ErroConvidados, TxtConvidados, "Entre 1 e 32767.");
                }
            }

            // RN05: pelo menos um serviço; cada um com faixa marcada (RN06) e valor válido
            if (_servicos.Count == 0)
            {
                Erro(ErroServicos, null, "Adicione pelo menos um serviço.");
            }

            foreach (var linha in _servicos)
            {
                if (_ocupados.Contains(linha.ServicoId))
                {
                    linha.Erro = $"{linha.Nome} já está reservado nesta data (RN10).";
                }
                else if (linha.FaixaSelecionada is null)
                {
                    linha.Erro = "Marque a faixa de preço.";
                }
                else if (linha.Valor is null)
                {
                    linha.Erro = "Valor inválido.";
                }
                else
                {
                    linha.Erro = null;
                }

                if (linha.TemErro)
                {
                    erros.Add(ListaServicos);
                }
            }

            var adicionaisComErro = _adicionais.Where(a => a.Selecionado && (a.QuantidadeErro || a.ValorErro)).ToList();
            if (adicionaisComErro.Count > 0)
            {
                erros.Add(ListaAdicionais);
            }

            decimal desconto = 0;
            if (TxtDesconto.Text.Trim().Length > 0 && (!Formatos.TentarLerDecimal(TxtDesconto.Text, out desconto) || desconto < 0))
            {
                Erro(ErroRodape, TxtDesconto, "Desconto inválido.");
            }
            else if (desconto > SubtotalServicos + SubtotalAdicionais)
            {
                Erro(ErroRodape, TxtDesconto, "O desconto é maior que o valor dos serviços e adicionais.");
            }

            decimal sinal = 0;
            var especie = CmbEspecie.SelectedItem as EspecieOpcao;
            if (TxtSinal.Text.Trim().Length > 0 && (!Formatos.TentarLerDecimal(TxtSinal.Text, out sinal) || sinal < 0))
            {
                Erro(ErroRodape, TxtSinal, "Valor do sinal inválido.");
            }
            else if (sinal > 0 && especie is null)
            {
                Erro(ErroRodape, CmbEspecie, "Escolha a espécie do sinal.");
            }

            if (erros.Count > 0)
            {
                erros[0].BringIntoView();
                return null;
            }

            return new DadosAgendamento
            {
                ClienteId = _clienteId!.Value,
                Titulo = TxtTituloEvento.Text.Trim(),
                DataEvento = data!.Value,
                HoraInicio = inicio!.Value,
                HoraFim = fim!.Value,
                QuantidadeConvidados = convidados,
                ValorDesconto = desconto,
                Servicos = _servicos.Select(s => (s.ServicoId, s.FaixaSelecionada!.Id, s.Valor!.Value)).ToList(),
                Adicionais = _adicionais.Where(a => a.Selecionado)
                    .Select(a => (a.AdicionalId, a.Quantidade!.Value, a.Valor!.Value, a.Custo))
                    .ToList(),
                ValorSinal = sinal,
                EspecieSinalId = sinal > 0 ? especie?.Id : null
            };
        }

        // ------------------------------------------------------------------
        // 6. Botões
        // ------------------------------------------------------------------

        private void BtnSalvarOrcamento_Click(object sender, RoutedEventArgs e) => Salvar(StatusAgendamento.Orcamento, faturar: false);

        private void BtnAgendar_Click(object sender, RoutedEventArgs e) => Salvar(StatusAgendamento.Agendado, faturar: false);

        private void BtnAgendarFaturar_Click(object sender, RoutedEventArgs e) => Salvar(StatusAgendamento.Agendado, faturar: true);

        private void Salvar(StatusAgendamento status, bool faturar)
        {
            var dados = Validar();
            if (dados is null)
            {
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                _id = AgendamentoService.Salvar(_id, dados, status);
            }
            catch (RegraNegocioException ex)
            {
                if (ex.Campo == "Servicos")
                {
                    MostrarErro(ErroServicos, null, ex.Message);
                    ErroServicos.BringIntoView();
                }
                else
                {
                    MostrarErro(TxtErroGeral, null, ex.Message);
                    Rolagem.ScrollToTop();
                }

                return;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            Gravou = true;
            _alterado = false;

            if (faturar)
            {
                // RF10: o agendamento já foi gravado como Agendado; desistir do faturamento mantém assim
                var faturamento = new FaturamentoWindow(_id.Value);
                Modal.Mostrar(faturamento, this, Overlay);
                if (!faturamento.Faturou)
                {
                    Dialogo.Avisar("Faturamento não concluído", "O agendamento foi salvo como Agendado. Para faturar depois, abra o agendamento e clique em Faturar.");
                }
            }

            Fechar(true);
        }

        private void BtnCancelarAgendamento_Click(object sender, RoutedEventArgs e)
        {
            if (_id is null)
            {
                return;
            }

            var avaliacao = AgendamentoService.AvaliarCancelamento(_id.Value);
            if (!avaliacao.Permitido)
            {
                MostrarErro(TxtErroGeral, null, avaliacao.Impedimento!);
                Rolagem.ScrollToTop();
                return;
            }

            var consequencias = new List<string>();
            if (avaliacao.ParcelasAbertas > 0)
            {
                consequencias.Add(avaliacao.ParcelasAbertas == 1
                    ? "A parcela em aberto será cancelada e não será cobrada."
                    : $"As {avaliacao.ParcelasAbertas} parcelas em aberto serão canceladas e não serão cobradas.");
            }

            if (avaliacao.ComDevolucao)
            {
                consequencias.Add($"Dentro do prazo de devolução: {Formatos.Moeda(avaliacao.ValorPago)} já pago será devolvido (saída no caixa).");
            }
            else if (avaliacao.ValorPago > 0)
            {
                consequencias.Add($"Fora do prazo de devolução ({App.Sistema.PrazoCancelamentoDias} dias antes do evento): os {Formatos.Moeda(avaliacao.ValorPago)} pagos continuam no caixa.");
            }

            var motivo = Dialogo.PedirTexto("Cancelar agendamento",
                string.Join(Environment.NewLine, consequencias.Prepend("O cancelamento não pode ser desfeito.")),
                "Motivo do cancelamento *", "Cancelar agendamento", perigo: true);
            if (motivo is null)
            {
                return;
            }

            AgendamentoService.Cancelar(_id.Value, motivo);
            Gravou = true;
            _alterado = false;
            Fechar(true);
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e) => TentarFechar();

        private void TentarFechar()
        {
            if (ConfirmarDescarte())
            {
                Fechar(Gravou);
            }
        }

        private bool ConfirmarDescarte() =>
            !_alterado || _somenteLeitura
            || Dialogo.Confirmar("Descartar alterações?", "As alterações deste agendamento ainda não foram salvas.", "Descartar", perigo: true);

        private void Fechar(bool resultado)
        {
            _fechamentoConfirmado = true;
            DialogResult = resultado;
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            // Alt+F4 também pergunta se há alterações não salvas
            if (!_fechamentoConfirmado && !ConfirmarDescarte())
            {
                e.Cancel = true;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            e.Handled = true;
            if (PopupClientes.IsOpen)
            {
                PopupClientes.IsOpen = false;
                TxtCliente.Focus();
            }
            else if (CmbServico.IsDropDownOpen || CmbEspecie.IsDropDownOpen)
            {
                CmbServico.IsDropDownOpen = CmbEspecie.IsDropDownOpen = false;
            }
            else
            {
                TentarFechar();
            }
        }

        private void Cabecalho_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not Button && e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private sealed record FaixaCadastro(int Id, string Nome, decimal Valor, bool Ativa);

        private sealed record ServicoCadastro(string Nome, bool Ativo, List<FaixaCadastro> Faixas, List<int> Adicionais);

        private sealed record AdicionalCadastro(string Nome, bool CobraPorQuantidade, decimal ValorVenda, decimal ValorCusto, bool Ativo);
    }
}
