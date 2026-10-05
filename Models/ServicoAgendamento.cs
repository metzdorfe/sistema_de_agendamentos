namespace EspacoFestas.Models;

public class ServicoAgendamento
{
    public int Id { get; set; }
    public int AgendamentoId { get; set; }
    public Agendamento? Agendamento { get; set; }
    public int ServicoId { get; set; }
    public Servico? Servico { get; set; }
    public int ServicoFaixaId { get; set; }
    public ServicoFaixa? ServicoFaixa { get; set; }
    public decimal ValorUnitario { get; set; }
}
