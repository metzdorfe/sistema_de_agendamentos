namespace EspacoFestas.Models;

public class Especie
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public bool PermiteParcelamento { get; set; }
    public bool Ativo { get; set; } = true;
}
