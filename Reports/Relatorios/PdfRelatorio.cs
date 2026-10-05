using System.IO;
using EspacoFestas.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EspacoFestas.Reports.Relatorios;

/// <summary>Cores e cabeçalho comuns aos PDFs (relatórios e catálogo): paleta roxa do sistema, sem gradiente.</summary>
public static class PdfEstilo
{
    public const string Destaque = "#5A189A";
    public const string Principal = "#7B2CBF";
    public const string Texto = "#1A0B2E";
    public const string Cinza = "#6B5B7E";
    public const string Divisoria = "#E4DCEF";
    public const string FundoCabecalho = "#F3EBFD";

    /// <summary>Logo da empresa (Arquivos\, só o caminho fica no banco); arquivo sumido = sem logo.</summary>
    public static byte[]? LogoEmpresa()
    {
        var caminho = AppPaths.ArquivoCompleto(App.Sistema.CaminhoLogoEmpresa);
        return caminho is not null && File.Exists(caminho) ? File.ReadAllBytes(caminho) : null;
    }

    /// <summary>Logo e nome da empresa à esquerda; título e linhas auxiliares à direita.</summary>
    public static void Cabecalho(IContainer container, string titulo, params string?[] linhasDireita)
    {
        var empresa = App.Sistema;
        var logo = LogoEmpresa();
        container.PaddingBottom(16).BorderBottom(1).BorderColor(Divisoria).PaddingBottom(12).Row(linha =>
        {
            if (logo is { Length: > 0 })
            {
                linha.ConstantItem(56).Height(56).Image(logo).FitArea();
                linha.ConstantItem(14);
            }

            linha.RelativeItem().Column(coluna =>
            {
                coluna.Item().Text(App.NomeEmpresa).FontSize(18).SemiBold().FontColor(Texto);
                var contatos = new[]
                {
                    string.IsNullOrEmpty(empresa.CnpjEmpresa) ? null : $"CNPJ {Documentos.FormatarCpfCnpj(empresa.CnpjEmpresa)}",
                    Documentos.FormatarTelefone(empresa.TelefoneEmpresa),
                    empresa.EmailEmpresa
                }.Where(t => !string.IsNullOrWhiteSpace(t));
                coluna.Item().Text(string.Join("  ·  ", contatos)).FontSize(9).FontColor(Cinza);
                if (!string.IsNullOrWhiteSpace(empresa.EnderecoEmpresa))
                {
                    coluna.Item().Text(empresa.EnderecoEmpresa).FontSize(9).FontColor(Cinza);
                }
            });

            linha.AutoItem().AlignBottom().Column(coluna =>
            {
                coluna.Item().AlignRight().Text(titulo).FontSize(12).SemiBold().FontColor(Destaque);
                foreach (var texto in linhasDireita.Where(t => !string.IsNullOrWhiteSpace(t)))
                {
                    coluna.Item().AlignRight().Text(texto!).FontSize(9).FontColor(Cinza);
                }
            });
        });
    }

    public static void Rodape(IContainer container) =>
        container.AlignCenter().Text(texto =>
        {
            texto.DefaultTextStyle(t => t.FontSize(8).FontColor(Cinza));
            texto.Span("Página ");
            texto.CurrentPageNumber();
            texto.Span(" de ");
            texto.TotalPages();
        });
}

/// <summary>PDF dos relatórios em tabela: cabeçalho da empresa, filtros usados e data de geração; uma tabela por seção.</summary>
public static class PdfRelatorio
{
    public static void Gerar(string caminho, string titulo, string filtros, IReadOnlyList<Secao> secoes, bool paisagem)
    {
        Document.Create(documento => documento.Page(pagina =>
        {
            pagina.Size(paisagem ? PageSizes.A4.Landscape() : PageSizes.A4);
            pagina.Margin(32);
            pagina.DefaultTextStyle(t => t.FontSize(9).FontColor(PdfEstilo.Texto));

            pagina.Header().Element(c => PdfEstilo.Cabecalho(c, titulo, filtros, $"Gerado em {DateTime.Now:dd/MM/yyyy HH:mm}"));

            pagina.Content().Column(coluna =>
            {
                coluna.Spacing(20);
                foreach (var secao in secoes)
                {
                    coluna.Item().Element(c => Tabela(c, secao));
                }
            });

            pagina.Footer().Element(PdfEstilo.Rodape);
        })).GeneratePdf(caminho);
    }

    private static void Tabela(IContainer container, Secao secao)
    {
        container.Column(bloco =>
        {
            bloco.Item().BorderLeft(3).BorderColor(PdfEstilo.Principal).PaddingLeft(8)
                .Text(secao.Titulo).FontSize(11).Bold().FontColor(PdfEstilo.Texto);
            if (!string.IsNullOrWhiteSpace(secao.Subtitulo))
            {
                bloco.Item().PaddingTop(3).PaddingLeft(11).Text(secao.Subtitulo).FontSize(9).FontColor(PdfEstilo.Cinza);
            }

            bloco.Item().PaddingTop(8).Table(tabela =>
            {
                tabela.ColumnsDefinition(colunas =>
                {
                    foreach (var c in secao.Colunas)
                    {
                        colunas.RelativeColumn(c.Peso);
                    }
                });

                tabela.Header(cabecalho =>
                {
                    foreach (var c in secao.Colunas)
                    {
                        var celula = cabecalho.Cell().Background(PdfEstilo.FundoCabecalho).PaddingVertical(5).PaddingHorizontal(4);
                        (Direita(c) ? celula.AlignRight() : celula).Text(c.Titulo).SemiBold().FontColor(PdfEstilo.Destaque);
                    }
                });

                foreach (var linha in secao.Linhas)
                {
                    for (int i = 0; i < secao.Colunas.Count; i++)
                    {
                        var celula = tabela.Cell().BorderBottom(0.5f).BorderColor(PdfEstilo.Divisoria).PaddingVertical(4).PaddingHorizontal(4);
                        (Direita(secao.Colunas[i]) ? celula.AlignRight() : celula).Text(Texto(linha[i]));
                    }
                }

                if (secao.Totais is { } totais)
                {
                    for (int i = 0; i < secao.Colunas.Count; i++)
                    {
                        var celula = tabela.Cell().BorderTop(1).BorderColor(PdfEstilo.Destaque).PaddingVertical(5).PaddingHorizontal(4);
                        (Direita(secao.Colunas[i]) ? celula.AlignRight() : celula).Text(Texto(totais[i])).Bold();
                    }
                }
            });
        });
    }

    private static bool Direita(Coluna coluna) => coluna.Tipo is TipoColuna.Moeda or TipoColuna.Inteiro;

    private static string Texto(object? valor) => valor switch
    {
        null => string.Empty,
        DateTime data => Formatos.Data(data),
        decimal numero => Formatos.Moeda(numero),
        _ => valor.ToString() ?? string.Empty
    };
}
