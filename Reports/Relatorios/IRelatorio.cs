using EspacoFestas.Models;
using EspacoFestas.Services;

namespace EspacoFestas.Reports.Relatorios;

/// <summary>Quais filtros o popup mostra para um relatório.</summary>
[Flags]
public enum CamposFiltro
{
    Nenhum = 0,
    Periodo = 1,
    Cliente = 2,
    Status = 4,
    Servico = 8
}

/// <summary>Filtros escolhidos no popup. Nulo = "Todos".</summary>
public sealed class FiltrosRelatorio
{
    public DateTime? De { get; init; }
    public DateTime? Ate { get; init; }
    public int? ClienteId { get; init; }
    public string ClienteNome { get; init; } = "Todos";
    public StatusAgendamento? Status { get; init; }
    public int? ServicoId { get; init; }
    public string ServicoNome { get; init; } = "Todos";

    public string StatusNome => Status is { } status ? Formatos.NomeStatus(status) : "Todos";

    /// <summary>Para o RelatorioLog: "periodo=01/10/2026-31/10/2026; cliente=Todos; status=Todos; servico=Salão Principal".</summary>
    public string DescreverLog(CamposFiltro campos) => string.Join("; ", Itens(campos, log: true));

    /// <summary>Para o cabeçalho do PDF e a aba de informações do Excel.</summary>
    public string DescreverLeitura(CamposFiltro campos) => string.Join("  ·  ", Itens(campos, log: false));

    private IEnumerable<string> Itens(CamposFiltro campos, bool log)
    {
        if (campos.HasFlag(CamposFiltro.Periodo))
        {
            yield return log ? $"periodo={Formatos.Data(De)}-{Formatos.Data(Ate)}" : $"Período: {Formatos.Data(De)} a {Formatos.Data(Ate)}";
        }

        if (campos.HasFlag(CamposFiltro.Cliente))
        {
            yield return log ? $"cliente={ClienteNome}" : $"Cliente: {ClienteNome}";
        }

        if (campos.HasFlag(CamposFiltro.Status))
        {
            yield return log ? $"status={StatusNome}" : $"Status: {StatusNome}";
        }

        if (campos.HasFlag(CamposFiltro.Servico))
        {
            yield return log ? $"servico={ServicoNome}" : $"Serviço: {ServicoNome}";
        }
    }
}

/// <summary>
/// Um relatório da tela Relatórios. O popup de filtros é genérico: cada relatório só declara seus filtros (<see cref="Filtros"/>)
/// e sabe gerar o PDF e o XLSX.
/// </summary>
public interface IRelatorio
{
    /// <summary>Título no cartão, no popup e no arquivo (ex.: "Relatório de agendamentos").</summary>
    string Nome { get; }

    /// <summary>Nome curto no RelatorioLog (ex.: "Agendamentos").</summary>
    string NomeLog { get; }

    string Descricao { get; }

    /// <summary>Chave do ícone (Themes/Icones.xaml).</summary>
    string Icone { get; }

    CamposFiltro Filtros { get; }

    /// <summary>Falso = só PDF (ex.: catálogo).</summary>
    bool GeraXlsx { get; }

    /// <summary>Há algo para os filtros? Sem dados, o popup avisa e não gera arquivo.</summary>
    bool TemDados(FiltrosRelatorio filtros);

    void GerarPdf(FiltrosRelatorio filtros, string caminho);

    void GerarXlsx(FiltrosRelatorio filtros, string caminho);
}

/// <summary>
/// Relatório em tabelas: monta as seções uma vez e o mesmo conteúdo sai em PDF (seções em sequência)
/// e em XLSX (uma aba por seção).
/// </summary>
public abstract class RelatorioTabular : IRelatorio
{
    public abstract string Nome { get; }
    public abstract string NomeLog { get; }
    public abstract string Descricao { get; }
    public abstract string Icone { get; }
    public abstract CamposFiltro Filtros { get; }
    public virtual bool GeraXlsx => true;

    /// <summary>PDF em paisagem (muitas colunas).</summary>
    protected virtual bool Paisagem => false;

    /// <summary>Seções do relatório; a primeira decide se há dados.</summary>
    protected abstract IReadOnlyList<Secao> Montar(FiltrosRelatorio filtros);

    /// <summary>Seções do PDF; por padrão as mesmas do Excel.</summary>
    protected virtual IReadOnlyList<Secao> MontarPdf(FiltrosRelatorio filtros) => Montar(filtros);

    public bool TemDados(FiltrosRelatorio filtros) => Montar(filtros) is [{ Linhas.Count: > 0 }, ..];

    public void GerarPdf(FiltrosRelatorio filtros, string caminho) =>
        PdfRelatorio.Gerar(caminho, Nome, filtros.DescreverLeitura(Filtros), MontarPdf(filtros), Paisagem);

    public void GerarXlsx(FiltrosRelatorio filtros, string caminho) =>
        XlsxRelatorio.Gerar(caminho, Nome, filtros.DescreverLeitura(Filtros), Montar(filtros));
}
