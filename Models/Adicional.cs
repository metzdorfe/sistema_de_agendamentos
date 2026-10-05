namespace EspacoFestas.Models;

public class Adicional
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public decimal ValorVenda { get; set; }
    public decimal ValorCusto { get; set; }
    public bool CobraPorQuantidade { get; set; }
    public bool Ativo { get; set; } = true;

    public ICollection<ServicoAdicional> ServicoAdicionais { get; set; } = new List<ServicoAdicional>();
}
