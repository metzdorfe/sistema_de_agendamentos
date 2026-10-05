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
                $"A soma das parcelas ({Formatos.Moeda(parcelas.Sum(p => p.Valor))}) precisa ser igual ao valor a faturar ({Formatos.Moeda(aFaturar)}) (RN14).");
        }

        if (parcelas.Count > 0)
        {
            var especie = contexto.Especies.AsNoTracking().Single(e => e.Id == especieId);
            if (parcelas.Count > 1 && !especie.PermiteParcelamento)
            {
                throw new RegraNegocioException("Geral", $"{especie.Descricao} não permite parcelamento: só 1 parcela (RN15).");
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
            multa = Math.Round(r.ValorParcela * App.Sistema.PercentualMulta / 100, 2);
            juros = Math.Round(r.ValorParcela * App.Sistema.PercentualJurosMes / 100 / 30 * dias, 2);
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

    private static string Limitar(string texto) => texto.Length <= 200 ? texto : texto[..200];
}
