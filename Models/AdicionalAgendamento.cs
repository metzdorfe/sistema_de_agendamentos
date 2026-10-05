namespace EspacoFestas.Models;

public class AdicionalAgendamento
{
    public int Id { get; set; }
    public int AgendamentoId { get; set; }
    public Agendamento? Agendamento { get; set; }
    public int AdicionalId { get; set; }
    public Adicional? Adicional { get; set; }
    public short Quantidade { get; set; } = 1;
    public decimal ValorUnitario { get; set; }
    public decimal ValorCustoUnitario { get; set; }
}
