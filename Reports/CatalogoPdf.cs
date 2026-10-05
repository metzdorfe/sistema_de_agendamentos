using EspacoFestas.Reports.Relatorios;
using EspacoFestas.Services;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EspacoFestas.Reports;

/// <summary>
/// Catálogo de serviços em PDF (RF19): sai sempre com os preços atuais, direto dos cadastros.
/// Cabeçalho com logo e dados da empresa (CONFIGURACAO), o mesmo dos relatórios. Gerado localmente, sem internet (RNF10).
/// </summary>
public static class CatalogoPdf
{
    private const string Destaque = PdfEstilo.Destaque;
    private const string Texto = PdfEstilo.Texto;
    private const string Cinza = PdfEstilo.Cinza;
    private const string Divisoria = PdfEstilo.Divisoria;

    /// <param name="servicoId">Um serviço só; nulo = todos os serviços ativos.</param>
    public static void Gerar(string caminho, int? servicoId = null)
    {
        using var contexto = App.CriarContexto();
        var servicos = contexto.Servicos.AsNoTracking()
            .Where(s => servicoId == null ? s.Ativo : s.Id == servicoId)
            .OrderBy(s => s.Nome)
            .Select(s => new
            {
                s.Nome,
                s.Descricao,
                Faixas = s.Faixas.Where(f => f.Ativo).OrderBy(f => f.OrdemExibicao).Select(f => new { f.Nome, f.Valor }).ToList(),
                Adicionais = s.ServicoAdicionais.Where(sa => sa.Adicional!.Ativo).OrderBy(sa => sa.Adicional!.Nome)
                    .Select(sa => new { sa.Adicional!.Nome, sa.Adicional.ValorVenda, sa.Adicional.CobraPorQuantidade }).ToList()
            })
            .ToList();


        Document.Create(documento => documento.Page(pagina =>
        {
            pagina.Size(PageSizes.A4);
            pagina.Margin(40);
            pagina.DefaultTextStyle(t => t.FontSize(11).FontColor(Texto));

            pagina.Header().PaddingBottom(8).Element(c =>
                PdfEstilo.Cabecalho(c, "Catálogo de serviços", $"Preços válidos em {Formatos.Data(DateTime.Today)}"));

            pagina.Content().Column(coluna =>
            {
                coluna.Spacing(28);
                if (servicos.Count == 0)
                {
                    coluna.Item().Text("Nenhum serviço ativo cadastrado.").FontColor(Cinza);
                }

                foreach (var servico in servicos)
                {
                    coluna.Item().ShowEntire().Column(bloco =>
                    {
                        bloco.Item().BorderLeft(3).BorderColor(Destaque).PaddingLeft(10)
                            .Text(servico.Nome.ToUpper(Formatos.Brasil)).FontSize(14).Bold().FontColor(Texto);
                        if (!string.IsNullOrWhiteSpace(servico.Descricao))
                        {
                            bloco.Item().PaddingTop(6).Text(servico.Descricao).FontSize(10).FontColor(Cinza);
                        }

                        bloco.Item().PaddingTop(10).Column(faixas =>
                        {
                            foreach (var faixa in servico.Faixas)
                            {
                                LinhaPreco(faixas.Item(), faixa.Nome, faixa.Valor);
                            }
                        });

                        if (servico.Adicionais.Count > 0)
                        {
                            bloco.Item().PaddingTop(16).Text("ADICIONAIS").FontSize(10).SemiBold().LetterSpacing(0.05f).FontColor(Destaque);
                            bloco.Item().PaddingTop(4).Column(adicionais =>
                            {
                                foreach (var adicional in servico.Adicionais)
                                {
                                    var nome = adicional.CobraPorQuantidade ? $"• {adicional.Nome} (por unidade)" : $"• {adicional.Nome}";
                                    LinhaPreco(adicionais.Item(), nome, adicional.ValorVenda);
                                }
                            });
                        }
                    });
                }
            });

            pagina.Footer().Element(PdfEstilo.Rodape);
        })).GeneratePdf(caminho);
    }

    private static void LinhaPreco(IContainer container, string nome, decimal valor) =>
        container.BorderBottom(0.5f).BorderColor(Divisoria).PaddingVertical(6).Row(linha =>
        {
            linha.RelativeItem().Text(nome);
            linha.AutoItem().Text(Formatos.Moeda(valor)).SemiBold();
        });
}
