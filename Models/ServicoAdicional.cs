namespace EspacoFestas.Models;

public class ServicoAdicional
{
    public int ServicoId { get; set; }
    public Servico? Servico { get; set; }
    public int AdicionalId { get; set; }
    public Adicional? Adicional { get; set; }
}
