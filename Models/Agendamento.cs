namespace EspacoFestas.Models;

public class Agendamento
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public DateTime DataEvento { get; set; }
    public TimeSpan HoraInicio { get; set; }
    public TimeSpan HoraFim { get; set; }
    public short? QuantidadeConvidados { get; set; }
    public StatusAgendamento Status { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal ValorTotal { get; set; }
    public DateTime? DataCancelamento { get; set; }
    public string? MotivoCancelamento { get; set; }
    public string? Observacao { get; set; }
    public DateTime DataCadastro { get; set; } = DateTime.Today;

    public ICollection<ServicoAgendamento> Servicos { get; set; } = new List<ServicoAgendamento>();
    public ICollection<AdicionalAgendamento> Adicionais { get; set; } = new List<AdicionalAgendamento>();
    public ICollection<Receber> Parcelas { get; set; } = new List<Receber>();
    public ICollection<Financeiro> Lancamentos { get; set; } = new List<Financeiro>();
}
