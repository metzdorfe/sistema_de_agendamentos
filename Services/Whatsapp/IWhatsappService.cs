namespace EspacoFestas.Services.Whatsapp;

/// <summary>Estado da conexão, como o servidor informa (GET /status).</summary>
public sealed record WhatsappStatus(string Estado, string? Qr, string? Numero, string? Nome, string? Erro)
{
    public const string Desconectado = "desconectado";
    public const string AguardandoQr = "aguardando_qr";
    public const string Conectando = "conectando";
    public const string Conectado = "conectado";
    public const string ComErro = "erro";
    /// <summary>Estado local: o servidor não está rodando (WhatsApp desligado ou falhou ao iniciar).</summary>
    public const string Parado = "parado";

    public bool EstaConectado => Estado == Conectado;

    /// <summary>Texto para a tela (o estado nunca é mostrado só pela cor).</summary>
    public string Descricao => Estado switch
    {
        Conectado => string.IsNullOrEmpty(Nome) ? "Conectado" : $"Conectado como {Nome}",
        AguardandoQr => "Aguardando a leitura do QR code",
        Conectando => "Conectando...",
        ComErro => Erro ?? "Erro na conexão",
        Parado => Erro ?? "Serviço do WhatsApp parado",
        _ => "Desconectado"
    };

    public static WhatsappStatus ServidorParado(string? motivo = null) => new(Parado, null, null, null, motivo);
}

/// <summary>Resposta de um envio: { ok, id, erro, codigo } do servidor.</summary>
public sealed record WhatsappResultado(bool Ok, string? Id, string? Codigo, string? Erro)
{
    public static WhatsappResultado Falha(string codigo, string erro) => new(false, null, codigo, erro);
}

/// <summary>
/// Envio pelo WhatsApp. As telas e o <see cref="WhatsappMensagens"/> só falam com esta interface, nunca com HTTP:
/// assim dá para trocar o servidor local (Baileys) pela API oficial no futuro sem mexer nelas.
/// </summary>
public interface IWhatsappService
{
    Task<WhatsappStatus> ObterStatusAsync();

    /// <summary>Começa a conexão: sem sessão salva, o status passa a trazer o QR code.</summary>
    Task<WhatsappStatus> ConectarAsync();

    Task<WhatsappResultado> EnviarTextoAsync(string telefone, string texto);

    /// <param name="caminhoRelativo">PDF relativo à pasta Arquivos\ (o servidor recusa qualquer coisa fora dela).</param>
    Task<WhatsappResultado> EnviarDocumentoAsync(string telefone, string caminhoRelativo, string nomeArquivo, string legenda);

    /// <summary>Logout e apaga a sessão: a próxima conexão pede QR de novo.</summary>
    Task DesconectarAsync();
}
