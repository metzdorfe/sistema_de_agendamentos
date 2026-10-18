namespace EspacoFestas.Models;

public class Configuracao
{
    public int Id { get; set; } = 1;
    public bool BloqueioUmPorDia { get; set; } = true;
    public decimal PercentualJurosMes { get; set; }
    public decimal PercentualMulta { get; set; }
    public short PrazoCancelamentoDias { get; set; }
    public Tema Tema { get; set; } = Tema.Claro;
    public string? NomeEmpresa { get; set; }
    public string? CnpjEmpresa { get; set; }
    public string? TelefoneEmpresa { get; set; }
    public string? EmailEmpresa { get; set; }
    public string? EnderecoEmpresa { get; set; }

    /// <summary>Caminho da logo relativo à pasta Arquivos\ (ex.: Empresa\logo_20260930142501.png). Ver AppPaths.</summary>
    public string? CaminhoLogoEmpresa { get; set; }

    /// <summary>Liga o servidor local do WhatsApp (RN30: desligado, o sistema funciona igual).</summary>
    public bool WhatsappAtivo { get; set; }

    /// <summary>Máximo de mensagens enviadas por dia (RN29).</summary>
    public short WhatsappLimiteDiario { get; set; } = 10;
}
