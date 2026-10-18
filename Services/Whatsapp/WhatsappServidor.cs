using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace EspacoFestas.Services.Whatsapp;

/// <summary>
/// Inicia e supervisiona o servidor local do WhatsApp (Node + Baileys, pasta Whatsapp\) e fala com ele por HTTP em
/// 127.0.0.1, com um token novo a cada início. Sem janela de console; preso a um Job Object para morrer junto com o sistema;
/// se cair, reinicia até 3 vezes com espera crescente. Desligado ou com falha, o resto do sistema segue normal (RN30).
/// </summary>
public sealed class WhatsappServidor : IWhatsappService
{
    public static WhatsappServidor Atual { get; } = new();

    private const int MaximoReinicios = 3;
    private static readonly TimeSpan TempoParaIniciar = TimeSpan.FromSeconds(20);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private Process? _processo;
    private HttpClient? _http;
    private bool _parando = true;
    private int _reinicios;
    private string? _falha;

    private WhatsappServidor()
    {
    }

    /// <summary>Servidor no ar e respondendo.</summary>
    public bool Rodando => _http is not null && _processo is { HasExited: false };

    /// <summary>Liga o servidor (para e sobe de novo se já estava rodando, ex.: o limite diário mudou).</summary>
    public async Task IniciarAsync()
    {
        Parar();
        _parando = false;
        _falha = null;
        _reinicios = 0;
        await SubirAsync();
    }

    /// <summary>Encerra o servidor (fecha a entrada padrão; se não sair em 3 s, mata a árvore).</summary>
    public void Parar()
    {
        _parando = true;
        var processo = _processo;
        _processo = null;
        _http?.Dispose();
        _http = null;
        if (processo is null)
        {
            return;
        }

        try
        {
            if (!processo.HasExited)
            {
                processo.StandardInput.Close();
                if (!processo.WaitForExit(3000))
                {
                    processo.Kill(entireProcessTree: true);
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or IOException)
        {
            // Já tinha saído
        }
        finally
        {
            processo.Dispose();
        }
    }

    private async Task SubirAsync()
    {
        var local = Localizar();
        if (local is null)
        {
            _falha = "Os arquivos do serviço do WhatsApp não foram encontrados (pasta Whatsapp). Reinstale o sistema.";
            return;
        }

        var (node, pasta) = local.Value;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var inicio = new ProcessStartInfo(node)
        {
            WorkingDirectory = pasta,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        inicio.ArgumentList.Add(Path.Combine("src", "servidor.js"));
        inicio.Environment["WHATSAPP_TOKEN"] = token;
        inicio.Environment["WHATSAPP_PORTA"] = "0";
        inicio.Environment["WHATSAPP_SESSAO"] = AppPaths.WhatsappSessao;
        inicio.Environment["WHATSAPP_ARQUIVOS"] = AppPaths.Arquivos;
        inicio.Environment["WHATSAPP_LOG"] = Path.Combine(AppPaths.Log, "whatsapp.log");
        inicio.Environment["WHATSAPP_LIMITE"] = App.Sistema.WhatsappLimiteDiario.ToString();

        Process processo;
        try
        {
            Directory.CreateDirectory(AppPaths.Arquivos);
            processo = Process.Start(inicio)!;
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException)
        {
            ErroLog.Registrar(ex, "WhatsApp");
            _falha = "Não foi possível iniciar o serviço do WhatsApp.";
            return;
        }

        JobObject.Vincular(processo);

        // O servidor escreve "PORTA=<n>" quando está ouvindo
        var porta = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        processo.OutputDataReceived += (_, e) =>
        {
            if (e.Data?.StartsWith("PORTA=", StringComparison.Ordinal) == true && int.TryParse(e.Data[6..], out var numero))
            {
                porta.TrySetResult(numero);
            }
        };
        processo.ErrorDataReceived += (_, _) => { };
        processo.EnableRaisingEvents = true;
        processo.Exited += (_, _) =>
        {
            porta.TrySetResult(0);
            AoSair(processo);
        };
        processo.BeginOutputReadLine();
        processo.BeginErrorReadLine();

        var pronto = await Task.WhenAny(porta.Task, Task.Delay(TempoParaIniciar));
        if (pronto != porta.Task || porta.Task.Result == 0)
        {
            _falha = "O serviço do WhatsApp não respondeu ao iniciar.";
            try
            {
                processo.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                // Já saiu
            }

            return;
        }

        var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{porta.Task.Result}/"), Timeout = TimeSpan.FromMinutes(3) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _processo = processo;
        _http = http;
    }

    /// <summary>Caiu sem ninguém pedir: sobe de novo, até 3 vezes (2 s, 4 s, 8 s).</summary>
    private void AoSair(Process processo)
    {
        if (_parando || !ReferenceEquals(processo, _processo))
        {
            return;
        }

        _processo = null;
        _http = null;
        if (_reinicios >= MaximoReinicios)
        {
            _falha = "O serviço do WhatsApp parou e não voltou. Desligue e ligue o WhatsApp em Configurações.";
            ErroLog.Registrar("O serviço do WhatsApp parou e não voltou depois de 3 tentativas.", "WhatsApp");
            return;
        }

        _reinicios++;
        var espera = TimeSpan.FromSeconds(Math.Pow(2, _reinicios));
        _ = Task.Run(async () =>
        {
            await Task.Delay(espera);
            if (!_parando)
            {
                await SubirAsync();
            }
        });
    }

    /// <summary>Instalado: Whatsapp\node\node.exe e Whatsapp\server. Desenvolvimento: node do PATH e a pasta Whatsapp\server do repositório.</summary>
    private static (string Node, string Pasta)? Localizar()
    {
        var pasta = Path.Combine(AppPaths.Whatsapp, "server");
        for (var dir = new DirectoryInfo(AppPaths.Raiz); !Existe(pasta) && dir is not null; dir = dir.Parent)
        {
            pasta = Path.Combine(dir.FullName, "Whatsapp", "server");
        }

        if (!Existe(pasta))
        {
            return null;
        }

        var node = Path.Combine(AppPaths.Whatsapp, "node", "node.exe");
        return (File.Exists(node) ? node : "node", pasta);

        static bool Existe(string pasta) =>
            File.Exists(Path.Combine(pasta, "src", "servidor.js")) && Directory.Exists(Path.Combine(pasta, "node_modules"));
    }

    private async Task<T?> ChamarAsync<T>(HttpMethod metodo, string rota, object? corpo = null) where T : class
    {
        var http = _http;
        if (http is null)
        {
            return null;
        }

        try
        {
            using var pedido = new HttpRequestMessage(metodo, rota) { Content = corpo is null ? null : JsonContent.Create(corpo) };
            using var resposta = await http.SendAsync(pedido);
            return await resposta.Content.ReadFromJsonAsync<T>(Json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or ObjectDisposedException)
        {
            return null;
        }
    }

    private string MotivoParado() =>
        _falha ?? (App.Sistema.WhatsappAtivo ? "O serviço do WhatsApp está iniciando ou parado." : "O WhatsApp está desligado em Configurações.");

    private WhatsappStatus Converter(RespostaStatus? resposta) =>
        resposta is null
            ? WhatsappStatus.ServidorParado(MotivoParado())
            : new WhatsappStatus(resposta.Estado ?? WhatsappStatus.Desconectado, resposta.Qr, resposta.Numero, resposta.Nome, resposta.Erro);

    private WhatsappResultado Converter(RespostaEnvio? resposta) =>
        resposta is null
            ? WhatsappResultado.Falha("NAO_CONECTADO", MotivoParado())
            : new WhatsappResultado(resposta.Ok, resposta.Id, resposta.Codigo, resposta.Erro);

    public async Task<WhatsappStatus> ObterStatusAsync() => Converter(await ChamarAsync<RespostaStatus>(HttpMethod.Get, "status"));

    public async Task<WhatsappStatus> ConectarAsync() => Converter(await ChamarAsync<RespostaStatus>(HttpMethod.Post, "conectar"));

    public async Task<WhatsappResultado> EnviarTextoAsync(string telefone, string texto) =>
        Converter(await ChamarAsync<RespostaEnvio>(HttpMethod.Post, "enviar/texto", new { telefone, texto }));

    public async Task<WhatsappResultado> EnviarDocumentoAsync(string telefone, string caminhoRelativo, string nomeArquivo, string legenda) =>
        Converter(await ChamarAsync<RespostaEnvio>(HttpMethod.Post, "enviar/documento",
            new { telefone, caminhoArquivo = caminhoRelativo, nomeArquivo, legenda }));

    public async Task DesconectarAsync() => await ChamarAsync<RespostaEnvio>(HttpMethod.Post, "desconectar");

    private sealed record RespostaStatus(bool Ok, string? Estado, string? Qr, string? Numero, string? Nome, string? Erro);

    private sealed record RespostaEnvio(bool Ok, string? Id, string? Codigo, string? Erro);
}
