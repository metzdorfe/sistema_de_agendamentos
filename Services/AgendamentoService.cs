using EspacoFestas.Data;
using EspacoFestas.Models;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Services;

/// <summary>Regra de negócio violada ao salvar; a tela mostra a mensagem junto do campo (Campo).</summary>
public sealed class RegraNegocioException(string campo, string mensagem) : Exception(mensagem)
{
    public string Campo { get; } = campo;
}

/// <summary>O que a tela de agendamento manda gravar.</summary>
public sealed class DadosAgendamento
{
    public int ClienteId { get; init; }
    public string Titulo { get; init; } = string.Empty;
    public DateTime DataEvento { get; init; }
    public TimeSpan HoraInicio { get; init; }
    public TimeSpan HoraFim { get; init; }
    public short? QuantidadeConvidados { get; init; }
    public decimal ValorDesconto { get; init; }
    public List<(int ServicoId, int FaixaId, decimal Valor)> Servicos { get; init; } = [];
    public List<(int AdicionalId, short Quantidade, decimal Valor, decimal Custo)> Adicionais { get; init; } = [];
    public decimal ValorSinal { get; init; }
    public int? EspecieSinalId { get; init; }
}

/// <summary>Resumo para a confirmação do cancelamento (RN22–RN27).</summary>
public sealed record AvaliacaoCancelamento(bool Permitido, string? Impedimento, decimal ValorPago, bool DentroDoPrazo,
    int ParcelasAbertas, int DiasAteEvento)
{
    /// <summary>RN24: dentro do prazo, tudo que foi pago é devolvido.</summary>
    public bool ComDevolucao => DentroDoPrazo && ValorPago > 0;
}

/// <summary>Regras do agendamento (fluxo-sistema.md, seção 3 e 6).</summary>
public static class AgendamentoService
{
    /// <summary>
    /// RN10/RN11: com "um agendamento por dia" ligado, o serviço fica ocupado na data por agendamentos
    /// Agendados, Faturados ou Orçamentos com sinal (entrada no caixa ligada a ele). Orçamento sem sinal não bloqueia.
    /// </summary>
    public static HashSet<int> ServicosOcupados(EspacoFestasContext contexto, DateTime data, int? ignorarAgendamentoId)
    {
        if (!App.Sistema.BloqueioUmPorDia)
        {
            return [];
        }

        int ignorar = ignorarAgendamentoId ?? 0;
        return contexto.ServicoAgendamentos.AsNoTracking()
            .Where(sa => sa.Agendamento!.DataEvento == data.Date && sa.AgendamentoId != ignorar
                && (sa.Agendamento.Status == StatusAgendamento.Agendado
                    || sa.Agendamento.Status == StatusAgendamento.Faturado
                    || (sa.Agendamento.Status == StatusAgendamento.Orcamento
                        && sa.Agendamento.Lancamentos.Any(l => l.Tipo == TipoLancamento.Entrada))))
            .Select(sa => sa.ServicoId)
            .Distinct()
            .ToHashSet();
    }

    /// <summary>Grava o agendamento com os itens e o sinal. Retorna o ID.</summary>
    public static int Salvar(int? id, DadosAgendamento dados, StatusAgendamento status)
    {
        if (dados.Servicos.Count == 0)
        {
            throw new RegraNegocioException("Servicos", "Adicione pelo menos um serviço (RN05).");
        }

        using var contexto = App.CriarContexto();
        using var transacao = contexto.Database.BeginTransaction();

        Agendamento agendamento;
        if (id is null)
        {
            agendamento = new Agendamento { DataCadastro = DateTime.Today };
            contexto.Agendamentos.Add(agendamento);
        }
        else
        {
            agendamento = contexto.Agendamentos
                .Include(a => a.Servicos)
                .Include(a => a.Adicionais)
                .Single(a => a.Id == id);

            // RN17: faturado é imutável; cancelado não volta
            if (agendamento.Status is StatusAgendamento.Faturado or StatusAgendamento.Cancelado)
            {
                throw new RegraNegocioException("Geral", "Este agendamento não pode mais ser alterado.");
            }
        }

        bool temSinal = dados.ValorSinal > 0
            || (id is not null && contexto.Financeiros.Any(f => f.AgendamentoId == id && f.Tipo == TipoLancamento.Entrada));
        bool bloqueiaData = status is StatusAgendamento.Agendado or StatusAgendamento.Faturado
            || (status == StatusAgendamento.Orcamento && temSinal);

        // RN10/RN11: disponibilidade validada de novo ao gravar (a tela pode estar aberta há tempo)
        if (bloqueiaData)
        {
            var ocupados = ServicosOcupados(contexto, dados.DataEvento, id);
            var conflito = dados.Servicos.Select(s => s.ServicoId).Where(ocupados.Contains).ToList();
            if (conflito.Count > 0)
            {
                var nomes = contexto.Servicos.Where(s => conflito.Contains(s.Id)).Select(s => s.Nome).ToList();
                throw new RegraNegocioException("Servicos",
                    $"{string.Join(", ", nomes)} já está reservado em {Formatos.Data(dados.DataEvento)} (um agendamento por dia, RN10).");
            }
        }

        agendamento.ClienteId = dados.ClienteId;
        agendamento.Titulo = dados.Titulo;
        agendamento.DataEvento = dados.DataEvento.Date;
        agendamento.HoraInicio = dados.HoraInicio;
        agendamento.HoraFim = dados.HoraFim;
        agendamento.QuantidadeConvidados = dados.QuantidadeConvidados;
        agendamento.ValorDesconto = dados.ValorDesconto;
        agendamento.Status = status;

        // Itens: substituídos inteiros (nada referencia o ID das linhas). Valores copiados do lançamento (RN08).
        contexto.ServicoAgendamentos.RemoveRange(agendamento.Servicos);
        contexto.AdicionalAgendamentos.RemoveRange(agendamento.Adicionais);
        agendamento.Servicos = dados.Servicos
            .Select(s => new ServicoAgendamento { ServicoId = s.ServicoId, ServicoFaixaId = s.FaixaId, ValorUnitario = s.Valor })
            .ToList();
        agendamento.Adicionais = dados.Adicionais
            .Select(a => new AdicionalAgendamento
            {
                AdicionalId = a.AdicionalId,
                Quantidade = a.Quantidade,
                ValorUnitario = a.Valor,
                ValorCustoUnitario = a.Custo
            })
            .ToList();

        // RN09
        agendamento.ValorTotal = CalcularTotal(dados.Servicos.Sum(s => s.Valor),
            dados.Adicionais.Sum(a => a.Quantidade * a.Valor), dados.ValorDesconto);

        contexto.SaveChanges();

        // RN12: sinal vai direto para o caixa, ligado ao agendamento, sem parcela
        if (dados.ValorSinal > 0 && dados.EspecieSinalId is { } especie)
        {
            contexto.Financeiros.Add(new Financeiro
            {
                DataLancamento = DateTime.Today,
                Tipo = TipoLancamento.Entrada,
                Valor = dados.ValorSinal,
                EspecieId = especie,
                AgendamentoId = agendamento.Id,
                Descricao = Limitar($"Sinal – {agendamento.Titulo}", 200)
            });
            contexto.SaveChanges();
        }

        transacao.Commit();
        return agendamento.Id;
    }

    /// <summary>RN09: total = serviços + adicionais − desconto.</summary>
    public static decimal CalcularTotal(decimal servicos, decimal adicionais, decimal desconto) => servicos + adicionais - desconto;

    /// <summary>Valor já pago pelo cliente (sinal, à vista e parcelas baixadas, menos estornos de baixa).</summary>
    public static decimal ValorPago(EspacoFestasContext contexto, int agendamentoId) =>
        PagamentosPorEspecie(contexto, agendamentoId).Sum(p => p.Valor);

    /// <summary>
    /// Valor pago de vários agendamentos de uma vez (relatórios), com a mesma regra do <see cref="ValorPago"/>.
    /// Recebe a consulta dos agendamentos (vira subconsulta no banco, sem lista de IDs).
    /// </summary>
    public static Dictionary<int, decimal> ValoresPagos(EspacoFestasContext contexto, IQueryable<Agendamento> agendamentos)
    {
        var diretos = contexto.Financeiros.AsNoTracking()
            .Where(f => f.Tipo == TipoLancamento.Entrada && agendamentos.Any(a => a.Id == f.AgendamentoId))
            .Select(f => new { Id = f.AgendamentoId!.Value, f.Tipo, f.Valor })
            .ToList();
        var parcelas = contexto.Financeiros.AsNoTracking()
            .Where(f => f.Receber != null && agendamentos.Any(a => a.Id == f.Receber.AgendamentoId))
            .Select(f => new { Id = f.Receber!.AgendamentoId, f.Tipo, f.Valor })
            .ToList();

        return diretos.Concat(parcelas)
            .GroupBy(f => f.Id)
            .ToDictionary(g => g.Key, g => g.Sum(f => f.Tipo == TipoLancamento.Entrada ? f.Valor : -f.Valor));
    }

    public static AvaliacaoCancelamento AvaliarCancelamento(int agendamentoId)
    {
        using var contexto = App.CriarContexto();
        var agendamento = contexto.Agendamentos.AsNoTracking().Single(a => a.Id == agendamentoId);
        int dias = (agendamento.DataEvento.Date - DateTime.Today).Days;

        if (agendamento.Status == StatusAgendamento.Cancelado)
        {
            return new AvaliacaoCancelamento(false, "O agendamento já está cancelado.", 0, false, 0, dias);
        }

        // RN27: só até a data do evento, inclusive
        if (dias < 0)
        {
            return new AvaliacaoCancelamento(false, "O cancelamento só pode ser feito até a data do evento (RN27).", 0, false, 0, dias);
        }

        int abertas = contexto.Recebers.Count(r => r.AgendamentoId == agendamentoId && r.Status == StatusReceber.Aberto);
        decimal pago = ValorPago(contexto, agendamentoId);
        return new AvaliacaoCancelamento(true, null, pago, dias >= App.Sistema.PrazoCancelamentoDias, abertas, dias);
    }

    /// <summary>RN22–RN25, RN27: cancela, encerra parcelas em aberto e, dentro do prazo, devolve o que foi pago.</summary>
    public static void Cancelar(int agendamentoId, string motivo)
    {
        var avaliacao = AvaliarCancelamento(agendamentoId);
        if (!avaliacao.Permitido)
        {
            throw new RegraNegocioException("Geral", avaliacao.Impedimento!);
        }

        using var contexto = App.CriarContexto();
        using var transacao = contexto.Database.BeginTransaction();
        var agendamento = contexto.Agendamentos.Single(a => a.Id == agendamentoId);

        // RN23: parcelas em aberto não são cobradas
        foreach (var parcela in contexto.Recebers.Where(r => r.AgendamentoId == agendamentoId && r.Status == StatusReceber.Aberto))
        {
            parcela.Status = StatusReceber.Cancelado;
        }

        // RN24: dentro do prazo, devolve tudo que foi pago (sinal e parcelas), como saída ligada ao agendamento
        if (avaliacao.ComDevolucao)
        {
            foreach (var (especieId, valor) in PagamentosPorEspecie(contexto, agendamentoId).Where(p => p.Valor > 0))
            {
                contexto.Financeiros.Add(new Financeiro
                {
                    DataLancamento = DateTime.Today,
                    Tipo = TipoLancamento.Saida,
                    Valor = valor,
                    EspecieId = especieId,
                    AgendamentoId = agendamentoId,
                    Descricao = Limitar($"Devolução por cancelamento – {agendamento.Titulo}", 200)
                });
            }
        }

        agendamento.Status = StatusAgendamento.Cancelado;
        agendamento.DataCancelamento = DateTime.Today;
        agendamento.MotivoCancelamento = Limitar(motivo.Trim(), 200);
        contexto.SaveChanges();
        transacao.Commit();
    }

    /// <summary>
    /// Pago por espécie: entradas ligadas ao agendamento (sinal, à vista) + baixas de parcelas dele, menos estornos de baixa.
    /// Saídas ligadas direto ao agendamento não entram: podem ser despesas do evento (ex.: garçom), não devolução.
    /// </summary>
    private static List<(int EspecieId, decimal Valor)> PagamentosPorEspecie(EspacoFestasContext contexto, int agendamentoId) =>
        contexto.Financeiros.AsNoTracking()
            .Where(f => (f.AgendamentoId == agendamentoId && f.Tipo == TipoLancamento.Entrada)
                || (f.Receber != null && f.Receber.AgendamentoId == agendamentoId))
            .Select(f => new { f.EspecieId, f.Tipo, f.Valor })
            .AsEnumerable()
            .GroupBy(f => f.EspecieId)
            .Select(g => (g.Key, g.Sum(f => f.Tipo == TipoLancamento.Entrada ? f.Valor : -f.Valor)))
            .ToList();

    private static string Limitar(string texto, int tamanho) => texto.Length <= tamanho ? texto : texto[..tamanho];
}
