namespace EspacoFestas.Models;

/// <summary>Mensagem de WhatsApp: fila e histórico (nada é apagado).</summary>
public class WhatsappMensagem
{
    public int Id { get; set; }
    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public int? AgendamentoId { get; set; }
    public Agendamento? Agendamento { get; set; }
    public int? ReceberId { get; set; }
    public Receber? Receber { get; set; }
    /// <summary>Só dígitos, com 55 e DDD.</summary>
    public string Telefone { get; set; } = string.Empty;
    public TipoMensagem Tipo { get; set; }
    public string Texto { get; set; } = string.Empty;
    /// <summary>PDF enviado, relativo à pasta Arquivos\.</summary>
    public string? CaminhoAnexo { get; set; }
    public StatusMensagem Status { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime? DataEnvio { get; set; }
    public short Tentativas { get; set; }
    public string? MensagemErro { get; set; }
}
