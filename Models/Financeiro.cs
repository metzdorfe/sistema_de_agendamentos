namespace EspacoFestas.Models;

public class Financeiro
{
    public long Id { get; set; }
    public DateTime DataLancamento { get; set; }
    public TipoLancamento Tipo { get; set; }
    public decimal Valor { get; set; }
    public int EspecieId { get; set; }
    public Especie? Especie { get; set; }
    public int? ReceberId { get; set; }
    public Receber? Receber { get; set; }
    public int? AgendamentoId { get; set; }
    public Agendamento? Agendamento { get; set; }
    public string? Descricao { get; set; }
    /// <summary>Só em saída de estorno: a entrada estornada.</summary>
    public long? LancamentoEstornadoId { get; set; }
    public Financeiro? LancamentoEstornado { get; set; }
    /// <summary>Suprimento (entrada) ou sangria (saída): só movimenta o dinheiro do caixa, não é receita nem despesa.</summary>
    public bool MovimentacaoCaixa { get; set; }
}
