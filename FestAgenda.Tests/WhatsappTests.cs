using System.Diagnostics;
using EspacoFestas;
using EspacoFestas.Models;
using EspacoFestas.Services;
using EspacoFestas.Services.Whatsapp;
using Microsoft.EntityFrameworkCore;

namespace FestAgenda.Tests;

/// <summary>Servidor falso: responde o que o teste mandar e guarda o que recebeu.</summary>
internal sealed class WhatsappFalso(WhatsappResultado resposta) : IWhatsappService
{
    public List<(string Telefone, string Texto, string? Anexo)> Enviados { get; } = [];

    public Task<WhatsappStatus> ObterStatusAsync() => Task.FromResult(new WhatsappStatus(WhatsappStatus.Conectado, null, "5547999990000", "Teste", null));

    public Task<WhatsappStatus> ConectarAsync() => ObterStatusAsync();

    public Task<WhatsappResultado> EnviarTextoAsync(string telefone, string texto)
    {
        Enviados.Add((telefone, texto, null));
        return Task.FromResult(resposta);
    }

    public Task<WhatsappResultado> EnviarDocumentoAsync(string telefone, string caminhoRelativo, string nomeArquivo, string legenda)
    {
        Enviados.Add((telefone, legenda, caminhoRelativo));
        return Task.FromResult(resposta);
    }

    public Task DesconectarAsync() => Task.CompletedTask;
}

[Collection(ColecaoBanco.Nome)]
public class WhatsappTests(BancoTeste banco)
{
    private static readonly WhatsappResultado Sucesso = new(true, "ID-1", null, null);

    private void ExigirBanco() => Assert.SkipWhen(banco.Indisponivel is not null, banco.Indisponivel ?? string.Empty);

    private static void Configurar(bool ativo, short limite = 10)
    {
        App.Sistema.WhatsappAtivo = ativo;
        App.Sistema.WhatsappLimiteDiario = limite;
    }

    private static WhatsappMensagem Ultima()
    {
        using var contexto = App.CriarContexto();
        return contexto.WhatsappMensagens.AsNoTracking().OrderByDescending(m => m.Id).First();
    }

    private static void ApagarHistorico()
    {
        using var contexto = App.CriarContexto();
        contexto.WhatsappMensagens.ExecuteDelete();
    }

    // ---- Regras puras

    [Fact]
    public void PreencheVariaveisEDeixaAsDesconhecidas()
    {
        var texto = WhatsappMensagens.Preencher("Olá, {cliente}! {inexistente} Total: {valor}",
            new Dictionary<string, string> { ["cliente"] = "Maria", ["valor"] = "R$ 10,00" });
        Assert.Equal("Olá, Maria! {inexistente} Total: R$ 10,00", texto);
    }

    [Theory]
    [InlineData("(47) 99999-1234", "5547999991234")]
    [InlineData("+55 47 99999-1234", "5547999991234")]
    [InlineData("4733334444", "554733334444")]
    [InlineData("2099999999", null)]      // DDD 20 não existe
    [InlineData("47899991234", null)]     // 9 dígitos sem começar com 9
    [InlineData("999991234", null)]       // sem DDD
    public void NormalizaTelefoneComAsMesmasRegrasDoServidor(string entrada, string? esperado) =>
        Assert.Equal(esperado, WhatsappMensagens.NormalizarTelefone(entrada));

    [Fact]
    public void ClienteSemConsentimentoOuTelefoneInvalidoNaoRecebe()
    {
        Assert.Null(WhatsappMensagens.MotivoNaoEnvia(new Cliente { Nome = "A", Telefone = "47999991234" }));
        Assert.NotNull(WhatsappMensagens.MotivoNaoEnvia(new Cliente { Nome = "A", Telefone = "47999991234", AceitaWhatsapp = false }));
        Assert.NotNull(WhatsappMensagens.MotivoNaoEnvia(new Cliente { Nome = "A", Telefone = "123" }));
    }

    // ---- Envio e histórico

    [Fact]
    public async Task EnvioComSucessoFicaRegistradoComoEnviado()
    {
        ExigirBanco();
        Configurar(ativo: true);
        var servidor = new WhatsappFalso(Sucesso);
        var resultado = await WhatsappMensagens.EnviarAsync(
            new NovaMensagem(TipoMensagem.Avulsa, banco.ClienteId, null, "(47) 99999-1234", "Olá"), servidor);

        Assert.True(resultado.Ok);
        Assert.Equal(("5547999991234", "Olá", (string?)null), servidor.Enviados.Single());
        var registro = Ultima();
        Assert.Equal(StatusMensagem.Enviada, registro.Status);
        Assert.NotNull(registro.DataEnvio);
        Assert.Equal(1, registro.Tentativas);
    }

    [Fact]
    public async Task FalhaFicaRegistradaEPodeSerReenviada()
    {
        ExigirBanco();
        Configurar(ativo: true);
        var resultado = await WhatsappMensagens.EnviarAsync(
            new NovaMensagem(TipoMensagem.Avulsa, banco.ClienteId, null, "47999991234", "Olá"),
            new WhatsappFalso(WhatsappResultado.Falha("NUMERO_SEM_WHATSAPP", "sem")));

        Assert.False(resultado.Ok);
        var falha = Ultima();
        Assert.Equal(StatusMensagem.Falha, falha.Status);
        Assert.Equal("Este número não tem WhatsApp.", falha.MensagemErro);

        Assert.True((await WhatsappMensagens.ReenviarAsync(falha.Id, new WhatsappFalso(Sucesso))).Ok);
        var reenviada = Ultima();
        Assert.Equal(StatusMensagem.Enviada, reenviada.Status);
        Assert.Equal(2, reenviada.Tentativas);
        Assert.Null(reenviada.MensagemErro);

        // Já enviada: não reenvia
        Assert.False((await WhatsappMensagens.ReenviarAsync(falha.Id, new WhatsappFalso(Sucesso))).Ok);
    }

    [Fact]
    public async Task ServidorForaDoArViraFalhaSemExcecao()
    {
        ExigirBanco();
        Configurar(ativo: true);
        var resultado = await WhatsappMensagens.EnviarAsync(
            new NovaMensagem(TipoMensagem.Avulsa, banco.ClienteId, null, "47999991234", "Olá"),
            new WhatsappFalso(WhatsappResultado.Falha("NAO_CONECTADO", "O serviço do WhatsApp está parado.")));
        Assert.Equal("NAO_CONECTADO", resultado.Codigo);
        Assert.Equal(StatusMensagem.Falha, Ultima().Status);
    }

    [Fact]
    public async Task WhatsappDesligadoNaoEnviaNemRegistra()
    {
        ExigirBanco();
        Configurar(ativo: false);
        int antes;
        using (var contexto = App.CriarContexto())
        {
            antes = contexto.WhatsappMensagens.Count();
        }

        var servidor = new WhatsappFalso(Sucesso);
        var resultado = await WhatsappMensagens.EnviarAsync(new NovaMensagem(TipoMensagem.Avulsa, null, null, "47999991234", "Olá"), servidor);
        Assert.Equal("DESLIGADO", resultado.Codigo);
        Assert.Empty(servidor.Enviados);
        using (var contexto = App.CriarContexto())
        {
            Assert.Equal(antes, contexto.WhatsappMensagens.Count());
        }
    }

    [Fact]
    public async Task LimiteDiarioBloqueiaOExcedente()
    {
        ExigirBanco();
        ApagarHistorico();
        Configurar(ativo: true, limite: 2);
        var servidor = new WhatsappFalso(Sucesso);
        for (int i = 0; i < 2; i++)
        {
            Assert.True((await WhatsappMensagens.EnviarAsync(new NovaMensagem(TipoMensagem.Avulsa, null, null, "47999991234", $"Msg {i}"), servidor)).Ok);
        }

        var terceira = await WhatsappMensagens.EnviarAsync(new NovaMensagem(TipoMensagem.Avulsa, null, null, "47999991234", "Msg 3"), servidor);
        Assert.Equal("LIMITE", terceira.Codigo);
        Assert.Equal(2, servidor.Enviados.Count);
        Assert.Equal(StatusMensagem.Falha, Ultima().Status);
        Configurar(ativo: true);
    }

    [Fact]
    public void VariaveisDoOrcamentoTrazemTotalPagoESaldo()
    {
        ExigirBanco();
        var dados = new DadosAgendamento
        {
            ClienteId = banco.ClienteId,
            Titulo = "Aniversário da Ana",
            DataEvento = DateTime.Today.AddDays(900),
            HoraInicio = new TimeSpan(19, 0, 0),
            HoraFim = new TimeSpan(23, 0, 0),
            Servicos = [(banco.ServicoId, banco.FaixaId, 1000m)],
            ValorSinal = 300m,
            EspecieSinalId = banco.Pix
        };
        int id = AgendamentoService.Salvar(null, dados, StatusAgendamento.Orcamento);

        using var contexto = App.CriarContexto();
        var valores = WhatsappMensagens.VariaveisAgendamento(contexto, id);
        Assert.Equal("Cliente", valores["cliente"]);
        Assert.Equal("orçamento", valores["status"]);
        Assert.Equal("19:00 às 23:00", valores["hora"]);
        Assert.Equal("Salão", valores["servicos"]);
        Assert.Equal(Formatos.Moeda(1000m), valores["valor"]);
        Assert.Equal(Formatos.Moeda(300m), valores["pago"]);
        Assert.Equal(Formatos.Moeda(700m), valores["saldo"]);
    }

    // ---- Servidor Node de verdade, em modo simulado

    [Fact]
    public async Task SupervisorSobeOServidorEnviaEEncerraSemDeixarProcesso()
    {
        ExigirBanco();
        Assert.SkipWhen(!NodeDisponivel(), "Node.js não encontrado no PATH.");
        Environment.SetEnvironmentVariable("WHATSAPP_MOCK", "1");
        Configurar(ativo: true);
        var servidor = WhatsappServidor.Atual;
        try
        {
            await servidor.IniciarAsync();
            Assert.True(servidor.Rodando);
            Assert.Equal(WhatsappStatus.Desconectado, (await servidor.ObterStatusAsync()).Estado);

            await servidor.ConectarAsync();
            Assert.Equal(WhatsappStatus.AguardandoQr, (await servidor.ObterStatusAsync()).Estado);
            for (int i = 0; i < 50 && !(await servidor.ObterStatusAsync()).EstaConectado; i++)
            {
                await Task.Delay(100, TestContext.Current.CancellationToken);
            }

            Assert.True((await servidor.ObterStatusAsync()).EstaConectado);
            Assert.True((await servidor.EnviarTextoAsync("5547999991234", "Olá")).Ok);
            Assert.Equal("NUMERO_SEM_WHATSAPP", (await servidor.EnviarTextoAsync("554733330000", "Olá")).Codigo);
            Assert.Equal("ARQUIVO_INVALIDO", (await servidor.EnviarDocumentoAsync("5547999991234", "..\\fora.pdf", "x.pdf", "")).Codigo);
        }
        finally
        {
            servidor.Parar();
            Environment.SetEnvironmentVariable("WHATSAPP_MOCK", null);
        }

        Assert.False(servidor.Rodando);
        Assert.Equal(WhatsappStatus.Parado, (await servidor.ObterStatusAsync()).Estado);
    }

    private static bool NodeDisponivel()
    {
        try
        {
            using var processo = Process.Start(new ProcessStartInfo("node", "--version") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
            processo!.WaitForExit(5000);
            return processo.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
