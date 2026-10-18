using EspacoFestas.Models;
using EspacoFestas.Services;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Reports.Relatorios;

/// <summary>
/// RF25: faturamento por período. Considera os agendamentos com status Faturado pela DATA_EVENTO
/// (não existe data de faturamento no modelo); cancelados ficam fora. Resumo por mês e detalhe por agendamento.
/// </summary>
public sealed class RelatorioFaturamento : RelatorioTabular
{
    public override string Nome => "Faturamento por período";
    public override string NomeLog => "Faturamento";
    public override string Descricao => "Resumo por mês (faturados, total, recebido, em aberto e lucro) e o detalhe de cada agendamento faturado.";
    public override string Icone => "IconeRelatorios";
    public override CamposFiltro Filtros => CamposFiltro.Periodo;

    protected override IReadOnlyList<Secao> Montar(FiltrosRelatorio filtros)
    {
        using var contexto = App.CriarContexto();
        var consulta = contexto.Agendamentos.AsNoTracking()
            .Where(a => a.Status == StatusAgendamento.Faturado && a.DataEvento >= filtros.De!.Value && a.DataEvento <= filtros.Ate!.Value);
        var agendamentos = consulta
            .OrderBy(a => a.DataEvento).ThenBy(a => a.HoraInicio)
            .Select(a => new { a.Id, a.DataEvento, a.Titulo, Cliente = a.Cliente!.Nome, a.ValorTotal })
            .ToList();
        var pagos = AgendamentoService.ValoresPagos(contexto, consulta);

        // RF16: custos = custo dos adicionais + despesas do evento (saídas ligadas que não são estorno)
        var custoAdicionais = contexto.AdicionalAgendamentos.AsNoTracking()
            .Where(aa => consulta.Any(a => a.Id == aa.AgendamentoId))
            .GroupBy(aa => aa.AgendamentoId)
            .Select(g => new { Id = g.Key, Valor = g.Sum(aa => aa.Quantidade * aa.ValorCustoUnitario) })
            .ToDictionary(x => x.Id, x => x.Valor);
        var despesas = contexto.Financeiros.AsNoTracking()
            .Where(f => f.Tipo == TipoLancamento.Saida && f.LancamentoEstornadoId == null && consulta.Any(a => a.Id == f.AgendamentoId))
            .GroupBy(f => f.AgendamentoId!.Value)
            .Select(g => new { Id = g.Key, Valor = g.Sum(f => f.Valor) })
            .ToDictionary(x => x.Id, x => x.Valor);

        var linhas = agendamentos
            .Select(a => (a.DataEvento, a.Titulo, a.Cliente, Total: a.ValorTotal, Recebido: pagos.GetValueOrDefault(a.Id),
                Custos: custoAdicionais.GetValueOrDefault(a.Id) + despesas.GetValueOrDefault(a.Id)))
            .Select(a => (a.DataEvento, a.Titulo, a.Cliente, a.Total, a.Recebido, Aberto: a.Total - a.Recebido, a.Custos, Lucro: a.Total - a.Custos))
            .ToList();

        var resumo = new Secao
        {
            Titulo = "Resumo por mês",
            Subtitulo = "Agendamentos faturados, pela data do evento.",
            Colunas =
            [
                new("Mês", TipoColuna.Texto, 2),
                new("Faturados", TipoColuna.Inteiro, 1),
                new("Total faturado", TipoColuna.Moeda, 1.4f),
                new("Recebido", TipoColuna.Moeda, 1.4f),
                new("Em aberto", TipoColuna.Moeda, 1.4f),
                new("Lucro", TipoColuna.Moeda, 1.4f)
            ]
        };
        foreach (var mes in linhas.GroupBy(l => new DateTime(l.DataEvento.Year, l.DataEvento.Month, 1)).OrderBy(g => g.Key))
        {
            resumo.Linhas.Add([Formatos.Capitalizar(mes.Key.ToString("MMMM 'de' yyyy", Formatos.Brasil)), mes.Count(),
                mes.Sum(l => l.Total), mes.Sum(l => l.Recebido), mes.Sum(l => l.Aberto), mes.Sum(l => l.Lucro)]);
        }

        resumo.Totais = ["Total", linhas.Count, linhas.Sum(l => l.Total), linhas.Sum(l => l.Recebido), linhas.Sum(l => l.Aberto),
            linhas.Sum(l => l.Lucro)];

        var detalhe = new Secao
        {
            Titulo = "Detalhe por agendamento",
            Colunas =
            [
                new("Data", TipoColuna.Data, 1.3f),
                new("Título", TipoColuna.Texto, 2.2f),
                new("Cliente", TipoColuna.Texto, 2.2f),
                new("Total", TipoColuna.Moeda, 1.3f),
                new("Recebido", TipoColuna.Moeda, 1.3f),
                new("Em aberto", TipoColuna.Moeda, 1.3f),
                new("Custos", TipoColuna.Moeda, 1.3f),
                new("Lucro", TipoColuna.Moeda, 1.3f)
            ]
        };
        foreach (var l in linhas)
        {
            detalhe.Linhas.Add([l.DataEvento, l.Titulo, l.Cliente, l.Total, l.Recebido, l.Aberto, l.Custos, l.Lucro]);
        }

        detalhe.Totais = [$"Total ({linhas.Count})", null, null,
            linhas.Sum(l => l.Total), linhas.Sum(l => l.Recebido), linhas.Sum(l => l.Aberto), linhas.Sum(l => l.Custos), linhas.Sum(l => l.Lucro)];

        return [resumo, detalhe];
    }
}
