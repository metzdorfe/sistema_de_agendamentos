namespace EspacoFestas.Models;

/// <summary>Texto padrão de um tipo de mensagem, com variáveis entre chaves ({cliente}, {data}...).</summary>
public class ModeloMensagem
{
    public int Id { get; set; }
    public TipoMensagem Tipo { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
}
