namespace EspacoFestas.Models;

public class Cliente
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? CpfCnpj { get; set; }
    public string Telefone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? Cep { get; set; }
    public DateTime DataCadastro { get; set; } = DateTime.Today;
    public bool Ativo { get; set; } = true;
    /// <summary>Consentimento para receber mensagens pelo WhatsApp (LGPD).</summary>
    public bool AceitaWhatsapp { get; set; } = true;

    public ICollection<Agendamento> Agendamentos { get; set; } = new List<Agendamento>();
}
