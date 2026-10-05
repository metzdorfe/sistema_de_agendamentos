namespace EspacoFestas.Models;

public class Servico
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public bool Ativo { get; set; } = true;

    public ICollection<ServicoFaixa> Faixas { get; set; } = new List<ServicoFaixa>();
    public ICollection<ServicoAdicional> ServicoAdicionais { get; set; } = new List<ServicoAdicional>();
}
