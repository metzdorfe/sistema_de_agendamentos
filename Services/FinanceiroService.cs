using EspacoFestas.Models;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Services;

/// <summary>Como as datas das parcelas são calculadas.</summary>
public enum ModoVencimento
{
    /// <summary>Todo mês no mesmo dia (ex.: dia 10). Dia que não existe no mês (31 em abril) cai no último dia.</summary>
    DiaFixo,
    /// <summary>De X em X dias, a partir de hoje.</summary>
    Intervalo
}

/// <summary>Uma parcela do plano de faturamento, antes de gravar.</summary>
public sealed record ParcelaPlano(int Numero, DateTime Vencimento, decimal Valor, bool RecebidaHoje);

/// <summary>O que a tela de faturamento mostra no topo.</summary>
public sealed record ResumoFaturamento(int AgendamentoId, string Titulo, string Cliente, DateTime DataEvento, StatusAgendamento Status,
    decimal Total, decimal Pago)
{
    /// <summary>RN14: o que ainda precisa ser faturado = total − sinais/pagamentos já feitos.</summary>
    public decimal AFaturar => Math.Max(Total - Pago, 0);
}

/// <summary>Dados de uma parcela para a tela de baixa, com multa e juros calculados para a data do recebimento.</summary>
public sealed record CalculoBaixa(int ReceberId, string Cliente, string Evento, DateTime DataEvento, short Numero, int TotalParcelas,
    int EspecieId, string Especie, DateTime Vencimento, decimal ValorParcela, int DiasAtraso, decimal Multa, decimal Juros)
{
    public decimal Total => ValorParcela + Multa + Juros;
}

/// <summary>Faturamento com parcelamento (RN13–RN16) e baixa de parcelas (RN18–RN20). Ver fluxo-sistema.md, seções 4 e 5.</summary>
public static class FinanceiroService
{
    public const int MaximoParcelas = 48;

    public static ResumoFaturamento ObterResumo(int agendamentoId)
    {
        using var contexto = App.CriarContexto();
        var a = contexto.Agendamentos.AsNoTracking()
            .Where(x => x.Id == agendamentoId)
            .Select(x => new { x.Id, x.Titulo, Cliente = x.Cliente!.Nome, x.DataEvento, x.Status, x.ValorTotal })
            .Single();
        return new ResumoFaturamento(a.Id, a.Titulo, a.Cliente, a.DataEvento, a.Status, a.ValorTotal,
            AgendamentoService.ValorPago(contexto, agendamentoId));
    }

    /// <summary>
    /// Divide o valor em parcelas iguais (a diferença de centavos vai na última) e calcula os vencimentos.
    /// Dia fixo: todo mês naquele dia, começando no próximo dia D depois de hoje. Intervalo: hoje + X, + 2X...
    /// Com a 1ª recebida hoje, ela vence hoje e as demais seguem a regra a partir da primeira data.
    /// </summary>
    public static List<ParcelaPlano> GerarParcelas(decimal valor, int quantidade, ModoVencimento modo, int diaFixo, int intervaloDias,
        bool primeiraRecebidaHoje, DateTime hoje)
    {
        if (quantidade < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidade));
        }

        decimal valorParcela = Math.Floor(valor / quantidade * 100) / 100;
        var parcelas = new List<ParcelaPlano>();
        int posicao = 0;
        for (int i = 1; i <= quantidade; i++)
        {
            bool recebida = primeiraRecebidaHoje && i == 1;
            var vencimento = recebida ? hoje : Vencimento(++posicao);
            var valorDaVez = i == quantidade ? valor - valorParcela * (quantidade - 1) : valorParcela;
            parcelas.Add(new ParcelaPlano(i, vencimento, valorDaVez, recebida));
        }

        return parcelas;

        DateTime Vencimento(int n)
        {
            if (modo == ModoVencimento.Intervalo)
            {
                return hoje.AddDays(intervaloDias * n);
            }

            // Primeiro mês: o atual, se o dia ainda não passou; senão o próximo
            var mesInicial = new DateTime(hoje.Year, hoje.Month, 1);
            if (diaFixo <= hoje.Day)
            {
                mesInicial = mesInicial.AddMonths(1);
            }

            var mes = mesInicial.AddMonths(n - 1);
            return new DateTime(mes.Year, mes.Month, Math.Min(diaFixo, DateTime.DaysInMonth(mes.Year, mes.Month)));
        }
    }

    /// <summary>
    /// Fatura o agendamento: grava as parcelas no Receber (a recebida hoje já entra paga, com entrada no caixa)
    /// e muda o status para Faturado (RN17: depois disso, nada muda).
    /// </summary>
    public static void Faturar(int agendamentoId, int? especieId, IReadOnlyList<ParcelaPlano> parcelas)
    {
        using var contexto = App.CriarContexto();
        using var transacao = contexto.Database.BeginTransaction();
        var agendamento = contexto.Agendamentos.Single(a => a.Id == agendamentoId);
        if (agendamento.Status is StatusAgendamento.Faturado or StatusAgendamento.Cancelado)
        {
            throw new RegraNegocioException("Geral", "Este agendamento já está faturado ou cancelado.");
        }

        decimal aFaturar = Math.Max(agendamento.ValorTotal - AgendamentoService.ValorPago(contexto, agendamentoId), 0);
        if (parcelas.Sum(p => p.Valor) != aFaturar)
        {
            throw new RegraNegocioException("Geral",
                $"A soma das parcelas ({Formatos.Moeda(parcelas.Sum(p => p.Valor))}) precisa ser igual ao valor a faturar ({Formatos.Moeda(aFaturar)}).");
        }

        if (parcelas.Count > 0)
        {
            var especie = contexto.Especies.AsNoTracking().Single(e => e.Id == especieId);
            if (parcelas.Count > 1 && !especie.PermiteParcelamento)
            {
                throw new RegraNegocioException("Geral", $"{especie.Descricao} não permite parcelamento: só 1 parcela.");
            }
        }

        foreach (var p in parcelas)
        {
            var receber = new Receber
            {
                AgendamentoId = agendamentoId,
                EspecieId = especieId!.Value,
                NumeroParcela = (short)p.Numero,
                ValorParcela = p.Valor,
                DataVencimento = p.Vencimento.Date,
                Status = p.RecebidaHoje ? StatusReceber.Pago : StatusReceber.Aberto
            };
            if (p.RecebidaHoje)
            {
                receber.ValorRecebido = p.Valor;
                receber.DataRecebimento = DateTime.Today;
                receber.Lancamentos.Add(new Financeiro
                {
                    DataLancamento = DateTime.Today,
                    Tipo = TipoLancamento.Entrada,
                    Valor = p.Valor,
                    EspecieId = especieId.Value,
                    Descricao = Limitar($"Parcela {p.Numero}/{parcelas.Count} – {agendamento.Titulo}")
                });
            }

            contexto.Recebers.Add(receber);
        }

        agendamento.Status = StatusAgendamento.Faturado;
        contexto.SaveChanges();
        transacao.Commit();
    }

    /// <summary>RN18: multa (sobre a parcela) e juros simples por dia de atraso, para a data do recebimento.</summary>
    public static CalculoBaixa CalcularBaixa(int receberId, DateTime dataRecebimento)
    {
        using var contexto = App.CriarContexto();
        var r = contexto.Recebers.AsNoTracking()
            .Where(x => x.Id == receberId)
            .Select(x => new
            {
                x.Id,
                x.AgendamentoId,
                Cliente = x.Agendamento!.Cliente!.Nome,
                x.Agendamento.Titulo,
                x.Agendamento.DataEvento,
                x.NumeroParcela,
                x.EspecieId,
                Especie = x.Especie!.Descricao,
                x.DataVencimento,
                x.ValorParcela,
                x.Status
            })
            .Single();
        if (r.Status != StatusReceber.Aberto)
        {
            throw new RegraNegocioException("Geral", "Esta parcela não está em aberto.");
        }

        int total = contexto.Recebers.Where(x => x.AgendamentoId == r.AgendamentoId).Max(x => (int)x.NumeroParcela);
        int dias = Math.Max((dataRecebimento.Date - r.DataVencimento.Date).Days, 0);
        decimal multa = 0, juros = 0;
        if (dias > 0)
        {
            multa = Math.Round(r.ValorParcela * App.Sistema.PercentualMulta / 100, 2, MidpointRounding.AwayFromZero);
            juros = Math.Round(r.ValorParcela * App.Sistema.PercentualJurosMes / 100 / 30 * dias, 2, MidpointRounding.AwayFromZero);
        }

        return new CalculoBaixa(r.Id, r.Cliente, r.Titulo, r.DataEvento, r.NumeroParcela, total, r.EspecieId, r.Especie,
            r.DataVencimento, r.ValorParcela, dias, multa, juros);
    }

    /// <summary>
    /// Baixa da parcela (RN19, RN20): fica Paga, com entrada no caixa na espécie usada. Recebido menor que o total:
    /// com <paramref name="vencimentoDiferenca"/>, gera nova parcela com a diferença; sem, a diferença é perdoada.
    /// </summary>
    public static void Baixar(int receberId, DateTime dataRecebimento, int especieId, decimal multa, decimal juros, decimal recebido,
        DateTime? vencimentoDiferenca)
    {
        if (recebido <= 0)
        {
            throw new RegraNegocioException("Recebido", "Informe o valor recebido.");
        }

        using var contexto = App.CriarContexto();
        using var transacao = contexto.Database.BeginTransaction();
        var parcela = contexto.Recebers.Include(r => r.Agendamento).Single(r => r.Id == receberId);
        if (parcela.Status != StatusReceber.Aberto)
        {
            throw new RegraNegocioException("Geral", "Esta parcela não está mais em aberto.");
        }

        decimal total = parcela.ValorParcela + multa + juros;
        if (recebido > total)
        {
            throw new RegraNegocioException("Recebido", $"O valor recebido não pode passar do total devido ({Formatos.Moeda(total)}).");
        }

        int totalParcelas = contexto.Recebers.Where(r => r.AgendamentoId == parcela.AgendamentoId).Max(r => (int)r.NumeroParcela);
        parcela.Status = StatusReceber.Pago;
        parcela.ValorMulta = multa;
        parcela.ValorJuros = juros;
        parcela.ValorRecebido = recebido;
        parcela.DataRecebimento = dataRecebimento.Date;
        contexto.Financeiros.Add(new Financeiro
        {
            DataLancamento = dataRecebimento.Date,
            Tipo = TipoLancamento.Entrada,
            Valor = recebido,
            EspecieId = especieId,
            ReceberId = parcela.Id,
            Descricao = Limitar($"Baixa parcela {parcela.NumeroParcela}/{totalParcelas} – {parcela.Agendamento!.Titulo}")
        });

        if (recebido < total && vencimentoDiferenca is { } vencimento)
        {
            contexto.Recebers.Add(new Receber
            {
                AgendamentoId = parcela.AgendamentoId,
                EspecieId = parcela.EspecieId,
                ParcelaOrigemId = parcela.Id,
                NumeroParcela = (short)(totalParcelas + 1),
                ValorParcela = total - recebido,
                DataVencimento = vencimento.Date,
                Status = StatusReceber.Aberto
            });
        }

        contexto.SaveChanges();
        transacao.Commit();
    }

    /// <summary>
    /// RN21: estorno de baixa feita por engano. A parcela volta a Aberta (sem juros, multa, valor e data do recebimento),
    /// sai do caixa o que tinha entrado (na mesma espécie) e a parcela da diferença, se houver, é cancelada.
    /// </summary>
    public static void EstornarBaixa(int receberId)
    {
        using var contexto = App.CriarContexto();
        using var transacao = contexto.Database.BeginTransaction();
        var parcela = contexto.Recebers.Include(r => r.Agendamento).Single(r => r.Id == receberId);
        if (parcela.Status != StatusReceber.Pago)
        {
            throw new RegraNegocioException("Geral", "Não é possível estornar: a parcela não está paga.");
        }

        if (parcela.Agendamento!.Status == StatusAgendamento.Cancelado)
        {
            throw new RegraNegocioException("Geral", "Não é possível estornar: o agendamento está cancelado.");
        }

        var entrada = contexto.Financeiros
            .Where(f => f.ReceberId == receberId && f.Tipo == TipoLancamento.Entrada
                && !contexto.Financeiros.Any(e => e.LancamentoEstornadoId == f.Id))
            .OrderByDescending(f => f.Id)
            .FirstOrDefault()
            ?? throw new RegraNegocioException("Geral", "Não é possível estornar: o recebimento desta parcela não está no caixa.");

        var diferencas = contexto.Recebers.Where(r => r.ParcelaOrigemId == receberId && r.Status != StatusReceber.Cancelado).ToList();
        if (diferencas.Any(r => r.Status == StatusReceber.Pago))
        {
            throw new RegraNegocioException("Geral", "Não é possível estornar: a parcela da diferença já foi paga. Estorne ela primeiro.");
        }

        foreach (var diferenca in diferencas)
        {
            diferenca.Status = StatusReceber.Cancelado;
        }

        contexto.Financeiros.Add(new Financeiro
        {
            DataLancamento = DateTime.Today,
            Tipo = TipoLancamento.Saida,
            Valor = entrada.Valor,
            EspecieId = entrada.EspecieId,
            ReceberId = parcela.Id,
            LancamentoEstornadoId = entrada.Id,
            Descricao = Limitar($"Estorno da parcela {parcela.NumeroParcela} – {parcela.Agendamento.Titulo}")
        });

        parcela.Status = StatusReceber.Aberto;
        parcela.ValorJuros = 0;
        parcela.ValorMulta = 0;
        parcela.ValorRecebido = 0;
        parcela.DataRecebimento = null;
        contexto.SaveChanges();
        transacao.Commit();
    }

    /// <summary>
    /// Estorno pelo caixa: baixa de parcela (RN21), sinal (RN26) ou entrada avulsa. Sinal estornado vira saída ligada
    /// ao agendamento; um orçamento sem outro sinal deixa de bloquear a data.
    /// </summary>
    public static void Estornar(long lancamentoId)
    {
        using var contexto = App.CriarContexto();
        var entrada = contexto.Financeiros.Include(f => f.Agendamento).AsNoTracking().Single(f => f.Id == lancamentoId);
        if (entrada.Tipo != TipoLancamento.Entrada)
        {
            throw new RegraNegocioException("Geral",
                "Não é possível estornar uma saída. Para corrigir uma saída lançada por engano, lance uma entrada avulsa do mesmo valor.");
        }

        if (entrada.MovimentacaoCaixa)
        {
            throw new RegraNegocioException("Geral", "Não é possível estornar suprimento. Para corrigir, faça uma sangria do mesmo valor.");
        }

        if (contexto.Financeiros.Any(f => f.LancamentoEstornadoId == lancamentoId))
        {
            throw new RegraNegocioException("Geral", "Não é possível estornar: este lançamento já foi estornado.");
        }

        if (entrada.ReceberId is { } receberId)
        {
            EstornarBaixa(receberId);
            return;
        }

        if (entrada.Agendamento?.Status == StatusAgendamento.Faturado)
        {
            throw new RegraNegocioException("Geral", "Não é possível estornar o sinal de um agendamento faturado. Se for o caso, cancele o agendamento.");
        }

        if (entrada.Agendamento?.Status == StatusAgendamento.Cancelado)
        {
            throw new RegraNegocioException("Geral", "Não é possível estornar: o agendamento está cancelado.");
        }

        using var transacao = contexto.Database.BeginTransaction();
        contexto.Financeiros.Add(new Financeiro
        {
            DataLancamento = DateTime.Today,
            Tipo = TipoLancamento.Saida,
            Valor = entrada.Valor,
            EspecieId = entrada.EspecieId,
            AgendamentoId = entrada.AgendamentoId,
            LancamentoEstornadoId = entrada.Id,
            Descricao = Limitar(entrada.Agendamento is { } agendamento
                ? $"Estorno de sinal – {agendamento.Titulo}"
                : $"Estorno – {entrada.Descricao}")
        });
        contexto.SaveChanges();
        transacao.Commit();
    }

    /// <summary>
    /// RF15/RF12: lançamento feito à mão no caixa. Sem agendamento é avulso (ex.: compra de material, entrada ou saída);
    /// com agendamento é despesa do evento (ex.: garçom), sempre saída, e entra no lucro do evento (RF16).
    /// <paramref name="movimentacaoCaixa"/>: suprimento (entrada) ou sangria (saída), sempre avulso, fora de receita e despesa (RN34).
    /// Retorna o ID do lançamento.
    /// </summary>
    public static long Lancar(DateTime data, TipoLancamento tipo, decimal valor, int especieId, string descricao, int? agendamentoId,
        bool movimentacaoCaixa = false)
    {
        if (movimentacaoCaixa && agendamentoId is not null)
        {
            throw new RegraNegocioException("Agendamento", "Suprimento e sangria não se ligam a evento.");
        }

        descricao = descricao.Trim();
        if (descricao.Length == 0)
        {
            throw new RegraNegocioException("Descricao", "Informe a descrição.");
        }

        if (valor <= 0)
        {
            throw new RegraNegocioException("Valor", "Informe um valor maior que zero.");
        }

        if (data.Date > DateTime.Today)
        {
            throw new RegraNegocioException("Data", "A data não pode ser futura.");
        }

        using var contexto = App.CriarContexto();
        if (agendamentoId is { } id)
        {
            if (tipo != TipoLancamento.Saida)
            {
                throw new RegraNegocioException("Agendamento",
                    "Não é possível lançar entrada ligada a um agendamento: pagamento do cliente entra pelo sinal ou pelo faturamento.");
            }

            if (contexto.Agendamentos.Where(a => a.Id == id).Select(a => a.Status).Single() == StatusAgendamento.Cancelado)
            {
                throw new RegraNegocioException("Agendamento", "Não é possível lançar despesa em agendamento cancelado.");
            }
        }

        var lancamento = new Financeiro
        {
            DataLancamento = data.Date,
            Tipo = tipo,
            Valor = valor,
            EspecieId = especieId,
            AgendamentoId = agendamentoId,
            MovimentacaoCaixa = movimentacaoCaixa,
            Descricao = Limitar(descricao)
        };
        contexto.Financeiros.Add(lancamento);
        contexto.SaveChanges();
        return lancamento.Id;
    }

    private static string Limitar(string texto) => texto.Length <= 200 ? texto : texto[..200];
}
