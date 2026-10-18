using EspacoFestas.Services;

namespace FestAgenda.Tests;

/// <summary>Regras que não dependem do banco.</summary>
public class RegrasTests
{
    // ---- RN01: CPF/CNPJ

    [Theory]
    [InlineData("52998224725")]
    [InlineData("11222333000181")]
    public void CpfCnpjValido(string digitos) => Assert.True(Documentos.CpfCnpjValido(digitos));

    [Theory]
    [InlineData("52998224724")]      // dígito verificador errado
    [InlineData("11111111111")]      // todos iguais
    [InlineData("11222333000182")]
    [InlineData("00000000000000")]
    public void CpfCnpjInvalido(string digitos) => Assert.False(Documentos.CpfCnpjValido(digitos));

    [Fact]
    public void CpfComMascaraViraSoDigitos() =>
        Assert.True(Documentos.CpfCnpjValido(Documentos.SoDigitos("529.982.247-25")));

    // ---- Log de erros não grava CPF/telefone inteiros

    [Fact]
    public void LogMascaraNumerosLongos() =>
        Assert.Equal("key value is (\"CPF_CNPJ\" = '*******4725') at :line 123",
            ErroLog.Mascarar("key value is (\"CPF_CNPJ\" = '52998224725') at :line 123"));

    // ---- RN09: total

    [Fact]
    public void TotalEhServicosMaisAdicionaisMenosDesconto() =>
        Assert.Equal(1150.50m, AgendamentoService.CalcularTotal(1000m, 200.50m, 50m));

    // ---- Parcelamento (RN13–RN16)

    [Fact]
    public void CemEmTresVezesFechaOsCentavosNaUltima()
    {
        var parcelas = FinanceiroService.GerarParcelas(100m, 3, ModoVencimento.Intervalo, 0, 30, false, new DateTime(2026, 1, 15));
        Assert.Equal([33.33m, 33.33m, 33.34m], parcelas.Select(p => p.Valor));
        Assert.Equal(100m, parcelas.Sum(p => p.Valor));
    }

    [Fact]
    public void DiaFixo31CaiNoUltimoDiaDoMes()
    {
        var parcelas = FinanceiroService.GerarParcelas(300m, 3, ModoVencimento.DiaFixo, 31, 0, false, new DateTime(2026, 1, 15));
        Assert.Equal([new DateTime(2026, 1, 31), new DateTime(2026, 2, 28), new DateTime(2026, 3, 31)], parcelas.Select(p => p.Vencimento));
    }

    [Fact]
    public void DiaFixoQueJaPassouComecaNoMesSeguinte()
    {
        var parcelas = FinanceiroService.GerarParcelas(200m, 2, ModoVencimento.DiaFixo, 10, 0, false, new DateTime(2026, 1, 15));
        Assert.Equal([new DateTime(2026, 2, 10), new DateTime(2026, 3, 10)], parcelas.Select(p => p.Vencimento));
    }

    [Fact]
    public void PrimeiraRecebidaHojeVenceHojeEAsOutrasSeguemOIntervalo()
    {
        var hoje = new DateTime(2026, 1, 15);
        var parcelas = FinanceiroService.GerarParcelas(300m, 3, ModoVencimento.Intervalo, 0, 30, true, hoje);
        Assert.True(parcelas[0].RecebidaHoje);
        Assert.Equal([hoje, hoje.AddDays(30), hoje.AddDays(60)], parcelas.Select(p => p.Vencimento));
        Assert.All(parcelas.Skip(1), p => Assert.False(p.RecebidaHoje));
    }

    [Fact]
    public void ZeroParcelasEhRecusado() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FinanceiroService.GerarParcelas(100m, 0, ModoVencimento.Intervalo, 0, 30, false, DateTime.Today));
}
