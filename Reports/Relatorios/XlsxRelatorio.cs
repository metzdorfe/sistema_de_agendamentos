using ClosedXML.Excel;

namespace EspacoFestas.Reports.Relatorios;

/// <summary>
/// XLSX dos relatórios (ClosedXML, MIT): uma aba por seção, cabeçalho em negrito na primeira linha (congelada),
/// valores como números com formato R$ e datas como data, linha de totais e colunas ajustadas.
/// A última aba ("Informações") traz empresa, filtros usados e data de geração.
/// </summary>
public static class XlsxRelatorio
{
    private const string FormatoMoeda = "\"R$\" #,##0.00";
    private const string FormatoData = "dd/mm/yyyy";

    public static void Gerar(string caminho, string titulo, string filtros, IReadOnlyList<Secao> secoes)
    {
        using var pasta = new XLWorkbook();
        var nomesUsados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var secao in secoes)
        {
            var aba = pasta.Worksheets.Add(NomeAba(secao.Titulo, nomesUsados));
            for (int c = 0; c < secao.Colunas.Count; c++)
            {
                aba.Cell(1, c + 1).Value = secao.Colunas[c].Titulo;
            }

            var cabecalho = aba.Range(1, 1, 1, secao.Colunas.Count);
            cabecalho.Style.Font.Bold = true;
            cabecalho.Style.Fill.BackgroundColor = XLColor.FromHtml("#F3EBFD");
            cabecalho.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            aba.SheetView.FreezeRows(1);

            int linha = 2;
            foreach (var valores in secao.Linhas)
            {
                Preencher(aba, linha++, secao.Colunas, valores);
            }

            if (secao.Totais is { } totais)
            {
                Preencher(aba, linha, secao.Colunas, totais);
                var total = aba.Range(linha, 1, linha, secao.Colunas.Count);
                total.Style.Font.Bold = true;
                total.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }

            aba.Columns(1, secao.Colunas.Count).AdjustToContents(8.0, 60.0);
        }

        var info = pasta.Worksheets.Add(NomeAba("Informações", nomesUsados));
        info.Cell(1, 1).Value = "Relatório";
        info.Cell(1, 2).Value = titulo;
        info.Cell(2, 1).Value = "Empresa";
        info.Cell(2, 2).Value = App.NomeEmpresa;
        info.Cell(3, 1).Value = "Filtros";
        info.Cell(3, 2).Value = filtros;
        info.Cell(4, 1).Value = "Gerado em";
        info.Cell(4, 2).Value = DateTime.Now;
        info.Cell(4, 2).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        info.Cell(4, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        info.Column(1).Style.Font.Bold = true;
        info.Columns(1, 2).AdjustToContents();

        pasta.SaveAs(caminho);
    }

    private static void Preencher(IXLWorksheet aba, int linha, IReadOnlyList<Coluna> colunas, object?[] valores)
    {
        for (int c = 0; c < colunas.Count; c++)
        {
            var celula = aba.Cell(linha, c + 1);
            switch (valores[c])
            {
                case null:
                    break;
                case DateTime data:
                    celula.Value = data;
                    celula.Style.DateFormat.Format = FormatoData;
                    break;
                case decimal numero:
                    celula.Value = numero;
                    celula.Style.NumberFormat.Format = FormatoMoeda;
                    break;
                case int inteiro:
                    celula.Value = inteiro;
                    break;
                default:
                    celula.Value = valores[c]!.ToString();
                    break;
            }
        }
    }

    /// <summary>Nome de aba válido no Excel: até 31 caracteres, sem : \ / ? * [ ] e sem repetir.</summary>
    private static string NomeAba(string titulo, HashSet<string> usados)
    {
        var limpo = new string(titulo.Where(c => !":\\/?*[]".Contains(c)).ToArray()).Trim();
        if (limpo.Length > 31)
        {
            limpo = limpo[..31];
        }

        var nome = limpo.Length == 0 ? "Planilha" : limpo;
        for (int i = 2; !usados.Add(nome); i++)
        {
            var sufixo = $" ({i})";
            nome = (limpo.Length + sufixo.Length > 31 ? limpo[..(31 - sufixo.Length)] : limpo) + sufixo;
        }

        return nome;
    }
}
