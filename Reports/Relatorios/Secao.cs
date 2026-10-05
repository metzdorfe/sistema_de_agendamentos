namespace EspacoFestas.Reports.Relatorios;

public enum TipoColuna
{
    Texto,
    /// <summary>DateTime; no Excel vira data de verdade (não texto).</summary>
    Data,
    /// <summary>decimal; no Excel vira número com formato R$ #,##0.00.</summary>
    Moeda,
    Inteiro
}

/// <param name="Peso">Largura relativa da coluna no PDF.</param>
public sealed record Coluna(string Titulo, TipoColuna Tipo = TipoColuna.Texto, float Peso = 1);

/// <summary>Uma tabela do relatório: título (no PDF) / nome da aba (no Excel), colunas, linhas e linha de totais.</summary>
public sealed class Secao
{
    public required string Titulo { get; init; }

    /// <summary>Texto curto abaixo do título no PDF (ex.: "Recebido R$ 1.000,00 · Pendente R$ 500,00").</summary>
    public string? Subtitulo { get; init; }

    public required IReadOnlyList<Coluna> Colunas { get; init; }

    /// <summary>Cada linha tem um valor por coluna: string, DateTime, decimal ou int (ou nulo).</summary>
    public List<object?[]> Linhas { get; } = [];

    /// <summary>Linha de totais (mesmo número de colunas; nulo = célula vazia). Nula = sem totais.</summary>
    public object?[]? Totais { get; set; }
}
