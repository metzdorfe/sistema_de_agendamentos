using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EspacoFestas.Controls;
using EspacoFestas.Models;
using EspacoFestas.Reports.Relatorios;
using EspacoFestas.Services;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Views
{
    /// <summary>
    /// Popup de filtros dos relatórios (RF17, RF18, RF25): o usuário preenche, escolhe PDF ou XLSX, escolhe onde salvar
    /// e o sistema gera, registra no RelatorioLog (RN31) e oferece abrir o arquivo.
    /// </summary>
    public partial class RelatorioFiltrosWindow : Window
    {
        private const string Pdf = "PDF";
        private const string Xlsx = "XLSX";

        private readonly IRelatorio _relatorio;

        public RelatorioFiltrosWindow(IRelatorio relatorio)
        {
            InitializeComponent();
            _relatorio = relatorio;
            TxtTitulo.Text = relatorio.Nome;
            TxtDescricao.Text = relatorio.Descricao;

            var campos = relatorio.Filtros;
            PainelPeriodo.Visibility = Visivel(campos.HasFlag(CamposFiltro.Periodo));
            PainelCliente.Visibility = Visivel(campos.HasFlag(CamposFiltro.Cliente));
            PainelStatus.Visibility = Visivel(campos.HasFlag(CamposFiltro.Status));
            PainelServico.Visibility = Visivel(campos.HasFlag(CamposFiltro.Servico));
            BtnXlsx.Visibility = Visivel(relatorio.GeraXlsx);

            // Período padrão: o mês atual
            var inicioMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            TxtDe.Text = Formatos.Data(inicioMes);
            TxtAte.Text = Formatos.Data(inicioMes.AddMonths(1).AddDays(-1));
            CarregarOpcoes(campos);

            Loaded += (_, _) => TemaService.AjustarBarraTitulo(this);
        }

        private static Visibility Visivel(bool visivel) => visivel ? Visibility.Visible : Visibility.Collapsed;

        private void CarregarOpcoes(CamposFiltro campos)
        {
            using var contexto = App.CriarContexto();
            if (campos.HasFlag(CamposFiltro.Cliente))
            {
                var clientes = contexto.Clientes.AsNoTracking().OrderBy(c => c.Nome).Select(c => new Opcao(c.Id, c.Nome)).ToList();
                CmbCliente.ItemsSource = clientes.Prepend(Opcao.Todos).ToList();
                CmbCliente.SelectedIndex = 0;
            }

            if (campos.HasFlag(CamposFiltro.Servico))
            {
                // Catálogo: só serviços ativos; nos demais, também os inativos (histórico)
                var servicos = contexto.Servicos.AsNoTracking()
                    .Where(s => _relatorio.GeraXlsx || s.Ativo)
                    .OrderBy(s => s.Nome).Select(s => new Opcao(s.Id, s.Nome)).ToList();
                CmbServico.ItemsSource = servicos.Prepend(Opcao.Todos).ToList();
                CmbServico.SelectedIndex = 0;
            }

            if (campos.HasFlag(CamposFiltro.Status))
            {
                var status = new[] { StatusAgendamento.Orcamento, StatusAgendamento.Agendado, StatusAgendamento.Faturado, StatusAgendamento.Cancelado }
                    .Select(s => new Opcao((int)s, Formatos.NomeStatus(s)));
                CmbStatus.ItemsSource = status.Prepend(Opcao.Todos).ToList();
                CmbStatus.SelectedIndex = 0;
            }
        }

        /// <summary>Período: "de" e "até" obrigatórios, "de" não pode passar do "até".</summary>
        private FiltrosRelatorio? LerFiltros()
        {
            DateTime? de = null, ate = null;
            if (_relatorio.Filtros.HasFlag(CamposFiltro.Periodo))
            {
                de = Mascara.LerData(TxtDe.Text);
                ate = Mascara.LerData(TxtAte.Text);
                Estilo.SetErro(TxtDe, de is null);
                Estilo.SetErro(TxtAte, ate is null);
                if (de is null || ate is null)
                {
                    MostrarMensagem("Informe as datas \"de\" e \"até\" do período.");
                    return null;
                }

                if (de > ate)
                {
                    Estilo.SetErro(TxtDe, true);
                    MostrarMensagem("A data \"de\" não pode ser maior que a data \"até\".");
                    return null;
                }
            }

            var cliente = CmbCliente.SelectedItem as Opcao ?? Opcao.Todos;
            var status = CmbStatus.SelectedItem as Opcao ?? Opcao.Todos;
            var servico = CmbServico.SelectedItem as Opcao ?? Opcao.Todos;
            return new FiltrosRelatorio
            {
                De = de,
                Ate = ate,
                ClienteId = cliente.Id,
                ClienteNome = cliente.Nome,
                Status = status.Id is { } s ? (StatusAgendamento)s : null,
                ServicoId = servico.Id,
                ServicoNome = servico.Nome
            };
        }

        private void Gerar(string formato)
        {
            var filtros = LerFiltros();
            if (filtros is null)
            {
                return;
            }

            bool temDados;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                temDados = _relatorio.TemDados(filtros);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            if (!temDados)
            {
                MostrarMensagem("Nenhum registro para os filtros informados.");
                return;
            }

            // Vai direto para Downloads, como num navegador (sem "Salvar como")
            bool pdf = formato == Pdf;
            var descricaoLog = filtros.DescreverLog(_relatorio.Filtros);
            string caminho;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                caminho = Downloads.NovoCaminho(_relatorio.Nome, pdf ? "pdf" : "xlsx");
                if (pdf)
                {
                    _relatorio.GerarPdf(filtros, caminho);
                }
                else
                {
                    _relatorio.GerarXlsx(filtros, caminho);
                }

                RelatorioLog.Sucesso(_relatorio.NomeLog, formato, descricaoLog, caminho);
            }
            catch (Exception ex)
            {
                var motivo = ex is IOException ? "arquivo em uso" : ex.Message;
                RelatorioLog.Erro(_relatorio.NomeLog, formato, descricaoLog, motivo);
                Mouse.OverrideCursor = null;
                MostrarMensagem(ex is IOException
                    ? $"Não foi possível salvar na pasta Downloads: {ex.Message}"
                    : $"Não foi possível gerar o relatório: {ex.Message}");
                return;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            TxtMensagem.Visibility = Visibility.Collapsed;
            Dialogo.ArquivoSalvo(caminho);
        }

        private void MostrarMensagem(string texto)
        {
            TxtMensagem.Text = texto;
            TxtMensagem.Visibility = Visibility.Visible;
        }

        private void Filtro_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            TxtMensagem.Visibility = Visibility.Collapsed;
            Estilo.SetErro(TxtDe, false);
            Estilo.SetErro(TxtAte, false);
        }

        private void BtnPdf_Click(object sender, RoutedEventArgs e) => Gerar(Pdf);

        private void BtnXlsx_Click(object sender, RoutedEventArgs e) => Gerar(Xlsx);

        private void BtnCalendarioDe_Click(object sender, RoutedEventArgs e) => SeletorData.Abrir(TxtDe, CaixaDe);

        private void BtnCalendarioAte_Click(object sender, RoutedEventArgs e) => SeletorData.Abrir(TxtAte, CaixaAte);

        private void BtnFechar_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !CmbCliente.IsDropDownOpen && !CmbStatus.IsDropDownOpen && !CmbServico.IsDropDownOpen)
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

        /// <summary>Item dos ComboBox; Id nulo = "Todos". ToString: o ComboBox do sistema mostra o item selecionado pelo ToString.</summary>
        private sealed record Opcao(int? Id, string Nome)
        {
            public static readonly Opcao Todos = new(null, "Todos");

            public override string ToString() => Nome;
        }
    }
}
