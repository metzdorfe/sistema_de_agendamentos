namespace EspacoFestas.Models;

public class ServicoFaixa
{
    public int Id { get; set; }
    public int ServicoId { get; set; }
    public Servico? Servico { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public short OrdemExibicao { get; set; }
    public bool Ativo { get; set; } = true;
}
