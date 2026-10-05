using EspacoFestas.Models;
using EspacoFestas.Services;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Reports.Relatorios;

/// <summary>
/// RF18: entradas do caixa no período agrupadas por cliente (sinal, à vista e baixas de parcelas),
/// com o total recebido e o pendente (parcelas em aberto com vencimento no período).
/// </summary>
public sealed class RelatorioCaixaCliente : RelatorioTabular
{
    public override string Nome => "Caixa por cliente";
    public override string NomeLog => "CaixaCliente";
    public override string Descricao => "Entradas do período agrupadas por cliente, com total recebido e pendente.";
    public override string Icone => "IconeClientes";
    public override CamposFiltro Filtros => CamposFiltro.Cliente | CamposFiltro.Periodo;

    private static readonly Coluna[] ColunasEntradas =
    [
        new("Data", TipoColuna.Data, 1.2f),
        new("Agendamento", TipoColuna.Texto, 2.4f),
        new("Origem", TipoColuna.Texto, 1.3f),
        new("Espécie", TipoColuna.Texto, 1.4f),
        new("Valor", TipoColuna.Moeda, 1.2f)
    ];

    private sealed record Entrada(int ClienteId, string Cliente, DateTime Data, string Agendamento, string Origem, string Especie, decimal Valor);

    private sealed record Totais(int ClienteId, string Cliente, decimal Recebido, decimal Pendente);

    private static (List<Entrada> Entradas, List<Totais> PorCliente) Consultar(FiltrosRelatorio filtros)
    {
        using var contexto = App.CriarContexto();
        var de = filtros.De!.Value;
        var ate = filtros.Ate!.Value;

        // Entrada ligada direto ao agendamento (sinal, à vista) ou à parcela (baixa)
        var entradas = contexto.Financeiros.AsNoTracking()
            .Where(f => f.Tipo == TipoLancamento.Entrada && f.DataLancamento >= de && f.DataLancamento <= ate
                && (f.AgendamentoId != null || f.ReceberId != null))
            .Select(f => new
            {
                ClienteId = f.AgendamentoId != null ? f.Agendamento!.ClienteId : f.Receber!.Agendamento!.ClienteId,
                Cliente = f.AgendamentoId != null ? f.Agendamento!.Cliente!.Nome : f.Receber!.Agendamento!.Cliente!.Nome,
                Titulo = f.AgendamentoId != null ? f.Agendamento!.Titulo : f.Receber!.Agendamento!.Titulo,
                f.DataLancamento,
                Parcela = f.ReceberId != null ? (short?)f.Receber!.NumeroParcela : null,
                Especie = f.Especie!.Descricao,
                f.Valor
            })
            .Where(f => filtros.ClienteId == null || f.ClienteId == filtros.ClienteId)
            .ToList()
            .Select(f => new Entrada(f.ClienteId, f.Cliente, f.DataLancamento, f.Titulo,
                f.Parcela is { } n ? $"Parcela {n}" : "Sinal / à vista", f.Especie, f.Valor))
            .OrderBy(e => e.Cliente, StringComparer.CurrentCultureIgnoreCase).ThenBy(e => e.Data)
            .ToList();

        var pendentes = contexto.Recebers.AsNoTracking()
            .Where(r => r.Status == StatusReceber.Aberto && r.DataVencimento >= de && r.DataVencimento <= ate
                && (filtros.ClienteId == null || r.Agendamento!.ClienteId == filtros.ClienteId))
            .Select(r => new { r.Agendamento!.ClienteId, Cliente = r.Agendamento.Cliente!.Nome, r.ValorParcela })
            .ToList();

        var porCliente = entradas.Select(e => (e.ClienteId, e.Cliente))
            .Concat(pendentes.Select(p => (p.ClienteId, p.Cliente)))
            .Distinct()
            .Select(c => new Totais(c.ClienteId, c.Cliente,
                entradas.Where(e => e.ClienteId == c.ClienteId).Sum(e => e.Valor),
                pendentes.Where(p => p.ClienteId == c.ClienteId).Sum(p => p.ValorParcela)))
            .OrderBy(c => c.Cliente, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return (entradas, porCliente);
    }

    private static Secao Resumo(List<Totais> porCliente)
    {
        var resumo = new Secao
        {
            Titulo = "Resumo por cliente",
            Subtitulo = "Pendente: parcelas em aberto com vencimento no período.",
            Colunas = [new("Cliente", TipoColuna.Texto, 3), new("Recebido", TipoColuna.Moeda, 1.2f), new("Pendente", TipoColuna.Moeda, 1.2f)]
        };
        foreach (var c in porCliente)
        {
            resumo.Linhas.Add([c.Cliente, c.Recebido, c.Pendente]);
        }

        resumo.Totais = [$"Total ({porCliente.Count} {(porCliente.Count == 1 ? "cliente" : "clientes")})", porCliente.Sum(c => c.Recebido), porCliente.Sum(c => c.Pendente)];
        return resumo;
    }

    /// <summary>Excel: aba de resumo + aba com todas as entradas (coluna Cliente), fácil de filtrar.</summary>
    protected override IReadOnlyList<Secao> Montar(FiltrosRelatorio filtros)
    {
        var (entradas, porCliente) = Consultar(filtros);
        var todas = new Secao { Titulo = "Entradas", Colunas = [new("Cliente", TipoColuna.Texto, 2), .. ColunasEntradas] };
        foreach (var e in entradas)
        {
            todas.Linhas.Add([e.Cliente, e.Data, e.Agendamento, e.Origem, e.Especie, e.Valor]);
        }

        todas.Totais = ["Total", null, null, null, null, entradas.Sum(e => e.Valor)];
        return [Resumo(porCliente), todas];
    }

    /// <summary>PDF: uma tabela por cliente (com recebido e pendente no subtítulo) e o resumo no fim.</summary>
    protected override IReadOnlyList<Secao> MontarPdf(FiltrosRelatorio filtros)
    {
        var (entradas, porCliente) = Consultar(filtros);
        var secoes = new List<Secao>();
        foreach (var c in porCliente)
        {
            var secao = new Secao
            {
                Titulo = c.Cliente,
                Subtitulo = $"Recebido {Formatos.Moeda(c.Recebido)}  ·  Pendente {Formatos.Moeda(c.Pendente)}",
                Colunas = ColunasEntradas
            };
            foreach (var e in entradas.Where(e => e.ClienteId == c.ClienteId))
            {
                secao.Linhas.Add([e.Data, e.Agendamento, e.Origem, e.Especie, e.Valor]);
            }

            secao.Totais = ["Recebido", null, null, null, c.Recebido];
            secoes.Add(secao);
        }

        secoes.Add(Resumo(porCliente));
        return secoes;
    }
}
