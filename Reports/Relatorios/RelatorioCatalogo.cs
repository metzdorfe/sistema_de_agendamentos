using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Reports.Relatorios;

/// <summary>RF19: catálogo de serviços em PDF (sem enviar), com os preços atuais. Só PDF.</summary>
public sealed class RelatorioCatalogo : IRelatorio
{
    public string Nome => "Catálogo de serviços";
    public string NomeLog => "Catalogo";
    public string Descricao => "Serviços ativos com faixas de preço e adicionais, com os preços atuais. Só PDF.";
    public string Icone => "IconeCatalogo";
    public CamposFiltro Filtros => CamposFiltro.Servico;
    public bool GeraXlsx => false;

    public bool TemDados(FiltrosRelatorio filtros)
    {
        using var contexto = App.CriarContexto();
        return contexto.Servicos.AsNoTracking().Any(s => filtros.ServicoId == null ? s.Ativo : s.Id == filtros.ServicoId);
    }

    public void GerarPdf(FiltrosRelatorio filtros, string caminho) => CatalogoPdf.Gerar(caminho, filtros.ServicoId);

    public void GerarXlsx(FiltrosRelatorio filtros, string caminho) =>
        throw new NotSupportedException("O catálogo só é gerado em PDF.");
}
