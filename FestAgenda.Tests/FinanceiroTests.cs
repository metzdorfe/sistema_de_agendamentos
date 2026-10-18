using EspacoFestas;
using EspacoFestas.Models;
using EspacoFestas.Services;
using Microsoft.EntityFrameworkCore;

namespace FestAgenda.Tests;

/// <summary>Regras de agendamento, faturamento, baixa, estorno, cancelamento e caixa, contra um Firebird de verdade.</summary>
[Collection(ColecaoBanco.Nome)]
public class FinanceiroTests(BancoTeste banco)
{
    /// <summary>Cada teste usa uma data própria, para o bloqueio de um agendamento por dia não misturar os casos.</summary>
    private static int _dias;

    private DateTime NovaData() => DateTime.Today.AddDays(400 + Interlocked.Increment(ref _dias));

    private void ExigirBanco() => Assert.SkipWhen(banco.Indisponivel is not null, banco.Indisponivel ?? string.Empty);

    private DadosAgendamento Dados(DateTime data, decimal sinal = 0, decimal valor = 1000m, decimal desconto = 0) => new()
    {
        ClienteId = banco.ClienteId,
        Titulo = "Aniversário",
        DataEvento = data,
        HoraInicio = new TimeSpan(18, 0, 0),
        HoraFim = new TimeSpan(23, 0, 0),
        ValorDesconto = desconto,
        Servicos = [(banco.ServicoId, banco.FaixaId, valor)],
        ValorSinal = sinal,
        EspecieSinalId = sinal > 0 ? banco.Pix : null
    };

    private static decimal Pago(int id)
    {
        using var contexto = App.CriarContexto();
        return AgendamentoService.ValorPago(contexto, id);
    }

    private bool Ocupado(DateTime data)
    {
        using var contexto = App.CriarContexto();
        return AgendamentoService.ServicosOcupados(contexto, data, null).Contains(banco.ServicoId);
    }

    private static long Sinal(int id)
    {
        using var contexto = App.CriarContexto();
        return contexto.Financeiros.Where(f => f.AgendamentoId == id && f.Tipo == TipoLancamento.Entrada).Max(f => f.Id);
    }

    private static List<Receber> Parcelas(int id)
    {
        using var contexto = App.CriarContexto();
        return contexto.Recebers.AsNoTracking().Where(r => r.AgendamentoId == id).OrderBy(r => r.NumeroParcela).ToList();
    }

    /// <summary>Agendado e faturado: 1ª parcela recebida hoje e as demais a cada 30 dias.</summary>
    private int Faturado(DateTime data, int parcelas, decimal sinal = 0) => Faturado(Dados(data, sinal), parcelas);

    private int Faturado(DadosAgendamento dados, int parcelas = 1)
    {
        int id = AgendamentoService.Salvar(null, dados, StatusAgendamento.Agendado);
        using var contexto = App.CriarContexto();
        decimal aFaturar = contexto.Agendamentos.Single(a => a.Id == id).ValorTotal - AgendamentoService.ValorPago(contexto, id);
        var plano = FinanceiroService.GerarParcelas(aFaturar, parcelas, ModoVencimento.Intervalo, 0, 30, true, DateTime.Today);
        FinanceiroService.Faturar(id, banco.Cartao, plano);
        return id;
    }

    // ---- RN05, RN09, RN32

    [Fact]
    public void AgendamentoSemServicoEhRecusado()
    {
        ExigirBanco();
        var dados = Dados(NovaData());
        dados.Servicos.Clear();
        Assert.Throws<RegraNegocioException>(() => AgendamentoService.Salvar(null, dados, StatusAgendamento.Orcamento));
    }

    [Fact]
    public void TotalGravadoDescontaODesconto()
    {
        ExigirBanco();
        int id = AgendamentoService.Salvar(null, Dados(NovaData(), desconto: 150m), StatusAgendamento.Orcamento);
        using var contexto = App.CriarContexto();
        Assert.Equal(850m, contexto.Agendamentos.Single(a => a.Id == id).ValorTotal);
    }

    [Fact]
    public void SinalMaiorQueOTotalEhRecusado()
    {
        ExigirBanco();
        var erro = Assert.Throws<RegraNegocioException>(() =>
            AgendamentoService.Salvar(null, Dados(NovaData(), sinal: 1000.01m), StatusAgendamento.Orcamento));
        Assert.Equal("Sinal", erro.Campo);
    }

    [Fact]
    public void TotalNaoPodeFicarMenorQueOJaPago()
    {
        ExigirBanco();
        var data = NovaData();
        int id = AgendamentoService.Salvar(null, Dados(data, sinal: 300m), StatusAgendamento.Orcamento);
        Assert.Throws<RegraNegocioException>(() => AgendamentoService.Salvar(id, Dados(data, valor: 200m), StatusAgendamento.Orcamento));
        Assert.Throws<RegraNegocioException>(() => AgendamentoService.Salvar(id, Dados(data, sinal: 700.01m), StatusAgendamento.Orcamento));
    }

    // ---- RN10, RN11, RN12

    [Fact]
    public void OrcamentoSemSinalNaoBloqueiaEComSinalBloqueia()
    {
        ExigirBanco();
        var data = NovaData();
        int id = AgendamentoService.Salvar(null, Dados(data), StatusAgendamento.Orcamento);
        Assert.False(Ocupado(data));

        AgendamentoService.Salvar(id, Dados(data, sinal: 100m), StatusAgendamento.Orcamento);
        Assert.True(Ocupado(data));
        Assert.Throws<RegraNegocioException>(() => AgendamentoService.Salvar(null, Dados(data), StatusAgendamento.Agendado));
    }

    [Fact]
    public void EditarOProprioAgendamentoNaoSeBloqueia()
    {
        ExigirBanco();
        var data = NovaData();
        int id = AgendamentoService.Salvar(null, Dados(data), StatusAgendamento.Agendado);
        AgendamentoService.Salvar(id, Dados(data, valor: 1200m), StatusAgendamento.Agendado);
    }

    [Fact]
    public void SinalVaiParaOCaixaSemCriarParcela()
    {
        ExigirBanco();
        int id = AgendamentoService.Salvar(null, Dados(NovaData(), sinal: 250m), StatusAgendamento.Orcamento);
        Assert.Equal(250m, Pago(id));
        Assert.Empty(Parcelas(id));
    }

    // ---- RN14, RN15, RN17

    [Fact]
    public void FaturamentoComSomaDiferenteDoValorAFaturarEhRecusado()
    {
        ExigirBanco();
        int id = AgendamentoService.Salvar(null, Dados(NovaData(), sinal: 100m), StatusAgendamento.Agendado);
        var plano = FinanceiroService.GerarParcelas(1000m, 2, ModoVencimento.Intervalo, 0, 30, false, DateTime.Today);
        Assert.Throws<RegraNegocioException>(() => FinanceiroService.Faturar(id, banco.Cartao, plano));
    }

    [Fact]
    public void EspecieSemParcelamentoSoAceitaUmaParcela()
    {
        ExigirBanco();
        int id = AgendamentoService.Salvar(null, Dados(NovaData()), StatusAgendamento.Agendado);
        var plano = FinanceiroService.GerarParcelas(1000m, 2, ModoVencimento.Intervalo, 0, 30, false, DateTime.Today);
        Assert.Throws<RegraNegocioException>(() => FinanceiroService.Faturar(id, banco.Pix, plano));
    }

    [Fact]
    public void FaturadoNaoPodeSerAlterado()
    {
        ExigirBanco();
        var data = NovaData();
        int id = Faturado(data, 2);
        Assert.Throws<RegraNegocioException>(() => AgendamentoService.Salvar(id, Dados(data), StatusAgendamento.Agendado));
        Assert.Throws<RegraNegocioException>(() => FinanceiroService.Faturar(id, banco.Cartao, []));
    }

    [Fact]
    public void FaturamentoGravaRecebidaHojeNoCaixaEAsOutrasNoReceber()
    {
        ExigirBanco();
        int id = Faturado(NovaData(), 3, sinal: 100m);
        var parcelas = Parcelas(id);
        Assert.Equal([StatusReceber.Pago, StatusReceber.Aberto, StatusReceber.Aberto], parcelas.Select(p => p.Status));
        Assert.Equal(900m, parcelas.Sum(p => p.ValorParcela));
        Assert.Equal(400m, Pago(id));
    }

    // ---- RN18

    [Fact]
    public void JurosEMultaDoExemploDoDocumento()
    {
        ExigirBanco();
        int id = AgendamentoService.Salvar(null, Dados(NovaData()), StatusAgendamento.Agendado);
        FinanceiroService.Faturar(id, banco.Cartao, [new ParcelaPlano(1, DateTime.Today.AddDays(-15), 1000m, false)]);
        var calculo = FinanceiroService.CalcularBaixa(Parcelas(id)[0].Id, DateTime.Today);
        Assert.Equal(15, calculo.DiasAtraso);
        Assert.Equal(20m, calculo.Multa);
        Assert.Equal(10m, calculo.Juros);
        Assert.Equal(1030m, calculo.Total);
    }

    [Fact]
    public void ParcelaNoDiaDoVencimentoNaoTemJurosNemMulta()
    {
        ExigirBanco();
        int id = AgendamentoService.Salvar(null, Dados(NovaData()), StatusAgendamento.Agendado);
        FinanceiroService.Faturar(id, banco.Cartao, [new ParcelaPlano(1, DateTime.Today, 1000m, false)]);
        var calculo = FinanceiroService.CalcularBaixa(Parcelas(id)[0].Id, DateTime.Today);
        Assert.Equal(0, calculo.DiasAtraso);
        Assert.Equal(1000m, calculo.Total);
    }

    [Fact]
    public void MultaArredondaOCincoParaCima()
    {
        ExigirBanco();
        int id = AgendamentoService.Salvar(null, Dados(NovaData(), valor: 333.25m), StatusAgendamento.Agendado);
        FinanceiroService.Faturar(id, banco.Cartao, [new ParcelaPlano(1, DateTime.Today.AddDays(-1), 333.25m, false)]);
        // 2% de 333,25 = 6,665
        Assert.Equal(6.67m, FinanceiroService.CalcularBaixa(Parcelas(id)[0].Id, DateTime.Today).Multa);
    }

    // ---- RN19, RN20, RN21

    [Fact]
    public void BaixaParcialCriaParcelaDaDiferencaEEstornoCancelaEla()
    {
        ExigirBanco();
        int id = Faturado(NovaData(), 2);
        var segunda = Parcelas(id)[1];
        FinanceiroService.Baixar(segunda.Id, DateTime.Today, banco.Pix, 0, 0, 300m, DateTime.Today.AddDays(30));

        var diferenca = Parcelas(id).Single(p => p.ParcelaOrigemId == segunda.Id);
        Assert.Equal(200m, diferenca.ValorParcela);
        Assert.Equal(StatusReceber.Pago, Parcelas(id)[1].Status);
        Assert.Equal(800m, Pago(id));

        FinanceiroService.EstornarBaixa(segunda.Id);
        var estornada = Parcelas(id).Single(p => p.Id == segunda.Id);
        Assert.Equal(StatusReceber.Aberto, estornada.Status);
        Assert.Equal(0m, estornada.ValorRecebido);
        Assert.Null(estornada.DataRecebimento);
        Assert.Equal(StatusReceber.Cancelado, Parcelas(id).Single(p => p.Id == diferenca.Id).Status);
        Assert.Equal(500m, Pago(id));

        using var contexto = App.CriarContexto();
        var saida = contexto.Financeiros.Single(f => f.ReceberId == segunda.Id && f.Tipo == TipoLancamento.Saida);
        Assert.Equal(300m, saida.Valor);
        Assert.Equal(banco.Pix, saida.EspecieId);
    }

    [Fact]
    public void BaixaParcialSemDiferencaPerdoa()
    {
        ExigirBanco();
        int id = Faturado(NovaData(), 2);
        FinanceiroService.Baixar(Parcelas(id)[1].Id, DateTime.Today, banco.Cartao, 0, 0, 450m, null);
        Assert.Equal(2, Parcelas(id).Count);
        Assert.All(Parcelas(id), p => Assert.Equal(StatusReceber.Pago, p.Status));
    }

    [Fact]
    public void BaixaComValorZeroOuAcimaDoTotalEhRecusada()
    {
        ExigirBanco();
        int id = Faturado(NovaData(), 2);
        int parcela = Parcelas(id)[1].Id;
        Assert.Throws<RegraNegocioException>(() => FinanceiroService.Baixar(parcela, DateTime.Today, banco.Pix, 0, 0, 0m, null));
        Assert.Throws<RegraNegocioException>(() => FinanceiroService.Baixar(parcela, DateTime.Today, banco.Pix, 0, 0, 500.01m, null));
    }

    [Fact]
    public void EstornoComParcelaDaDiferencaJaPagaEhRecusado()
    {
        ExigirBanco();
        int id = Faturado(NovaData(), 2);
        int segunda = Parcelas(id)[1].Id;
        FinanceiroService.Baixar(segunda, DateTime.Today, banco.Pix, 0, 0, 300m, DateTime.Today.AddDays(30));
        int diferenca = Parcelas(id).Single(p => p.ParcelaOrigemId == segunda).Id;
        FinanceiroService.Baixar(diferenca, DateTime.Today, banco.Pix, 0, 0, 200m, null);

        Assert.Throws<RegraNegocioException>(() => FinanceiroService.EstornarBaixa(segunda));
        FinanceiroService.EstornarBaixa(diferenca);
        FinanceiroService.EstornarBaixa(segunda);
        Assert.Equal(500m, Pago(id));
    }

    [Fact]
    public void EstornarDuasVezesEhRecusado()
    {
        ExigirBanco();
        int id = Faturado(NovaData(), 2);
        int primeira = Parcelas(id)[0].Id;
        FinanceiroService.EstornarBaixa(primeira);
        Assert.Throws<RegraNegocioException>(() => FinanceiroService.EstornarBaixa(primeira));
    }

    // ---- RN26

    [Fact]
    public void EstornoDeSinalLiberaADataEZeraOPago()
    {
        ExigirBanco();
        var data = NovaData();
        int id = AgendamentoService.Salvar(null, Dados(data, sinal: 100m), StatusAgendamento.Orcamento);
        long sinal = Sinal(id);

        FinanceiroService.Estornar(sinal);
        Assert.Equal(0m, Pago(id));
        Assert.False(Ocupado(data));
        Assert.Throws<RegraNegocioException>(() => FinanceiroService.Estornar(sinal));
    }

    [Fact]
    public void EstornoDeSinalDeFaturadoEhRecusado()
    {
        ExigirBanco();
        int id = Faturado(NovaData(), 2, sinal: 100m);
        Assert.Throws<RegraNegocioException>(() => FinanceiroService.Estornar(Sinal(id)));
    }

    // ---- RN22–RN27

    [Fact]
    public void CancelamentoDentroDoPrazoDevolveTudoECancelaAsAbertas()
    {
        ExigirBanco();
        int id = Faturado(NovaData(), 3, sinal: 100m);
        AgendamentoService.Cancelar(id, "Cliente desistiu");

        Assert.Equal(0m, Pago(id));
        Assert.All(Parcelas(id).Where(p => p.NumeroParcela > 1), p => Assert.Equal(StatusReceber.Cancelado, p.Status));
        using var contexto = App.CriarContexto();
        var agendamento = contexto.Agendamentos.Single(a => a.Id == id);
        Assert.Equal(StatusAgendamento.Cancelado, agendamento.Status);
        Assert.Equal("Cliente desistiu", agendamento.MotivoCancelamento);
        Assert.Equal(400m, contexto.Financeiros.Where(f => f.Tipo == TipoLancamento.Saida
            && (f.AgendamentoId == id || f.Receber!.AgendamentoId == id)).Sum(f => f.Valor));
        Assert.Throws<RegraNegocioException>(() => FinanceiroService.Estornar(Sinal(id)));
    }

    [Fact]
    public void CancelamentoForaDoPrazoNaoDevolve()
    {
        ExigirBanco();
        // Um dia antes do limite do prazo de devolução (só este teste usa datas perto de hoje)
        var data = DateTime.Today.AddDays(App.Sistema.PrazoCancelamentoDias - 1);
        int id = AgendamentoService.Salvar(null, Dados(data, sinal: 200m), StatusAgendamento.Agendado);
        var avaliacao = AgendamentoService.AvaliarCancelamento(id);
        Assert.False(avaliacao.DentroDoPrazo);
        AgendamentoService.Cancelar(id, "Fora do prazo");
        Assert.Equal(200m, Pago(id));
    }

    [Fact]
    public void CancelamentoDepoisDoEventoEhRecusado()
    {
        ExigirBanco();
        var ontem = DateTime.Today.AddDays(-1);
        int id = AgendamentoService.Salvar(null, Dados(ontem), StatusAgendamento.Agendado);
        Assert.False(AgendamentoService.AvaliarCancelamento(id).Permitido);
        Assert.Throws<RegraNegocioException>(() => AgendamentoService.Cancelar(id, "Tarde demais"));
    }

    [Fact]
    public void CanceladoNaoBloqueiaAData()
    {
        ExigirBanco();
        var data = NovaData();
        int id = AgendamentoService.Salvar(null, Dados(data), StatusAgendamento.Agendado);
        Assert.True(Ocupado(data));
        AgendamentoService.Cancelar(id, "Teste");
        Assert.False(Ocupado(data));
    }

    // ---- RF12, RF15: despesa do evento e lançamento avulso

    [Fact]
    public void DespesaDoEventoNaoMexeNoPagoNemNaDevolucao()
    {
        ExigirBanco();
        int id = AgendamentoService.Salvar(null, Dados(NovaData(), sinal: 300m), StatusAgendamento.Agendado);
        FinanceiroService.Lancar(DateTime.Today, TipoLancamento.Saida, 150m, banco.Pix, "Garçom", id);
        Assert.Equal(300m, Pago(id));

        AgendamentoService.Cancelar(id, "Teste");
        Assert.Equal(0m, Pago(id));
        using var contexto = App.CriarContexto();
        Assert.Equal(300m, contexto.Financeiros.Where(f => f.AgendamentoId == id && f.LancamentoEstornadoId != null).Sum(f => f.Valor));
    }

    [Fact]
    public void EntradaLigadaAAgendamentoEhRecusada()
    {
        ExigirBanco();
        int id = AgendamentoService.Salvar(null, Dados(NovaData()), StatusAgendamento.Agendado);
        Assert.Throws<RegraNegocioException>(() =>
            FinanceiroService.Lancar(DateTime.Today, TipoLancamento.Entrada, 10m, banco.Pix, "Teste", id));
    }

    [Fact]
    public void LancamentoAvulsoValidaOsCampos()
    {
        ExigirBanco();
        Assert.Throws<RegraNegocioException>(() => FinanceiroService.Lancar(DateTime.Today, TipoLancamento.Saida, 0m, banco.Pix, "Material", null));
        Assert.Throws<RegraNegocioException>(() => FinanceiroService.Lancar(DateTime.Today, TipoLancamento.Saida, 10m, banco.Pix, "  ", null));
        Assert.Throws<RegraNegocioException>(() => FinanceiroService.Lancar(DateTime.Today.AddDays(1), TipoLancamento.Saida, 10m, banco.Pix, "Material", null));
    }

    [Fact]
    public void EntradaAvulsaPodeSerEstornadaUmaVez()
    {
        ExigirBanco();
        long entrada = FinanceiroService.Lancar(DateTime.Today, TipoLancamento.Entrada, 80m, banco.Pix, "Venda de sobra de bolo", null);
        FinanceiroService.Estornar(entrada);
        Assert.Throws<RegraNegocioException>(() => FinanceiroService.Estornar(entrada));

        using var contexto = App.CriarContexto();
        var saida = contexto.Financeiros.Single(f => f.LancamentoEstornadoId == entrada);
        Assert.Equal(TipoLancamento.Saida, saida.Tipo);
        Assert.Null(saida.AgendamentoId);
    }

    // ---- RN34: suprimento e sangria

    [Fact]
    public void SuprimentoESangriaNaoSeLigamAEventoNemSeEstornam()
    {
        ExigirBanco();
        int id = AgendamentoService.Salvar(null, Dados(NovaData()), StatusAgendamento.Agendado);
        Assert.Throws<RegraNegocioException>(() =>
            FinanceiroService.Lancar(DateTime.Today, TipoLancamento.Saida, 50m, banco.Pix, "Sangria", id, movimentacaoCaixa: true));

        long suprimento = FinanceiroService.Lancar(DateTime.Today, TipoLancamento.Entrada, 200m, banco.Pix, "Troco", null, movimentacaoCaixa: true);
        FinanceiroService.Lancar(DateTime.Today, TipoLancamento.Saida, 150m, banco.Pix, "Depósito", null, movimentacaoCaixa: true);
        Assert.Throws<RegraNegocioException>(() => FinanceiroService.Estornar(suprimento));
    }

    [Fact]
    public void BancoRecusaMovimentacaoDeCaixaLigadaAEvento()
    {
        ExigirBanco();
        int id = AgendamentoService.Salvar(null, Dados(NovaData()), StatusAgendamento.Agendado);
        using var contexto = App.CriarContexto();
        contexto.Financeiros.Add(new Financeiro
        {
            DataLancamento = DateTime.Today, Tipo = TipoLancamento.Saida, Valor = 1m, EspecieId = banco.Pix, AgendamentoId = id, MovimentacaoCaixa = true
        });
        Assert.Throws<DbUpdateException>(() => contexto.SaveChanges());
    }

    [Fact]
    public void SaidaNaoSeEstorna()
    {
        ExigirBanco();
        long saida = FinanceiroService.Lancar(DateTime.Today, TipoLancamento.Saida, 30m, banco.Pix, "Material", null);
        var erro = Assert.Throws<RegraNegocioException>(() => FinanceiroService.Estornar(saida));
        Assert.Contains("entrada avulsa", erro.Message);
    }

    // ---- RF16: lucro do evento

    [Fact]
    public void LucroEhReceitaMenosCustoDosAdicionaisMenosDespesas()
    {
        ExigirBanco();
        var dados = Dados(NovaData(), desconto: 100m);
        dados.Adicionais.Add((banco.AdicionalId, (short)50, 10m, 4m));
        int id = Faturado(dados);
        FinanceiroService.Lancar(DateTime.Today, TipoLancamento.Saida, 150m, banco.Pix, "Garçom", id);
        FinanceiroService.Lancar(DateTime.Today, TipoLancamento.Saida, 80m, banco.Pix, "Limpeza", id);

        using var contexto = App.CriarContexto();
        var lucro = AgendamentoService.CalcularLucro(contexto, id);
        Assert.Equal(1400m, lucro.Receita);           // 1000 + 50 × 10 − 100
        Assert.Equal(200m, lucro.CustoAdicionais);    // 50 × 4
        Assert.Equal(230m, lucro.TotalDespesas);
        Assert.Equal(970m, lucro.Lucro);
        Assert.Equal(["Garçom", "Limpeza"], lucro.Despesas.Select(d => d.Descricao));
    }

    [Fact]
    public void EstornoDeParcelaNaoContaComoDespesa()
    {
        ExigirBanco();
        int id = Faturado(NovaData(), 2);
        FinanceiroService.EstornarBaixa(Parcelas(id)[0].Id);
        using var contexto = App.CriarContexto();
        Assert.Equal(0m, AgendamentoService.CalcularLucro(contexto, id).TotalDespesas);
    }

    // ---- Relatórios usam a mesma conta do valor pago

    [Fact]
    public void ValoresPagosDoRelatorioBatemComOValorPago()
    {
        ExigirBanco();
        int faturado = Faturado(NovaData(), 2, sinal: 100m);
        int cancelado = Faturado(NovaData(), 2);
        AgendamentoService.Cancelar(cancelado, "Teste");

        using var contexto = App.CriarContexto();
        var pagos = AgendamentoService.ValoresPagos(contexto, contexto.Agendamentos.Where(a => a.Id == faturado || a.Id == cancelado));
        Assert.Equal(Pago(faturado), pagos.GetValueOrDefault(faturado));
        Assert.Equal(Pago(cancelado), pagos.GetValueOrDefault(cancelado));
        Assert.Equal(0m, pagos.GetValueOrDefault(cancelado));
    }

    // ---- Banco: CHECKs batem com a aplicação

    [Fact]
    public void BancoRecusaEstornoMarcadoEmEntrada()
    {
        ExigirBanco();
        long entrada = FinanceiroService.Lancar(DateTime.Today, TipoLancamento.Entrada, 5m, banco.Pix, "Teste", null);
        using var contexto = App.CriarContexto();
        contexto.Financeiros.Add(new Financeiro
        {
            DataLancamento = DateTime.Today, Tipo = TipoLancamento.Entrada, Valor = 5m, EspecieId = banco.Pix, LancamentoEstornadoId = entrada
        });
        Assert.Throws<DbUpdateException>(() => contexto.SaveChanges());
    }

    [Fact]
    public void BancoRecusaLancamentoComDuasOrigens()
    {
        ExigirBanco();
        int id = Faturado(NovaData(), 2);
        using var contexto = App.CriarContexto();
        contexto.Financeiros.Add(new Financeiro
        {
            DataLancamento = DateTime.Today, Tipo = TipoLancamento.Saida, Valor = 1m, EspecieId = banco.Pix,
            AgendamentoId = id, ReceberId = Parcelas(id)[0].Id
        });
        Assert.Throws<DbUpdateException>(() => contexto.SaveChanges());
    }
}
