using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EspacoFestas.Services;

/// <summary>
/// Imagens do sistema ficam em arquivos dentro de Arquivos\ (ver AppPaths); o banco guarda só o caminho
/// relativo (ex.: CONFIGURACAO.CAMINHO_LOGO_EMPRESA = "Empresa\logo_20260930142501.png").
/// </summary>
public static class Imagens
{
    public const int TamanhoMaximo = 512;
    public const string PastaEmpresa = "Empresa";

    /// <summary>Lê a imagem pelo caminho relativo a Arquivos\. Arquivo apagado ou inválido → nulo (a tela mostra o placeholder).</summary>
    public static BitmapImage? Carregar(string? caminhoRelativo)
    {
        var caminho = AppPaths.ArquivoCompleto(caminhoRelativo);
        if (caminho is null || !File.Exists(caminho))
        {
            return null;
        }

        try
        {
            var imagem = new BitmapImage();
            imagem.BeginInit();
            // OnLoad + IgnoreImageCache: lê tudo e solta o arquivo (não trava a pasta nem pega versão antiga do cache)
            imagem.CacheOption = BitmapCacheOption.OnLoad;
            imagem.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            imagem.UriSource = new Uri(caminho);
            imagem.EndInit();
            imagem.Freeze();
            return imagem;
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or UnauthorizedAccessException or FileFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Copia a imagem escolhida para Arquivos\{subpasta}, com nome único, reduzida para no máximo 512 px e em PNG.
    /// Retorna o caminho relativo a Arquivos\, que é o que vai para o banco.
    /// </summary>
    public static string Salvar(string origem, string subpasta, string prefixo)
    {
        var original = new BitmapImage();
        original.BeginInit();
        original.CacheOption = BitmapCacheOption.OnLoad;
        original.UriSource = new Uri(origem);
        original.EndInit();

        BitmapSource final = original;
        double maior = Math.Max(original.PixelWidth, original.PixelHeight);
        if (maior > TamanhoMaximo)
        {
            double escala = TamanhoMaximo / maior;
            final = new TransformedBitmap(original, new ScaleTransform(escala, escala));
        }

        var destino = Path.Combine(AppPaths.PastaArquivos(subpasta), $"{prefixo}_{DateTime.Now:yyyyMMddHHmmssfff}.png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(final));
        using (var saida = File.Create(destino))
        {
            encoder.Save(saida);
        }

        return AppPaths.ArquivoRelativo(destino);
    }

    /// <summary>Apaga um arquivo antigo que deixou de ser usado (ex.: logo trocada). Falha aqui não atrapalha o usuário.</summary>
    public static void ApagarSeExistir(string? caminhoRelativo)
    {
        var caminho = AppPaths.ArquivoCompleto(caminhoRelativo);
        try
        {
            if (caminho is not null && caminho.StartsWith(AppPaths.Arquivos, StringComparison.OrdinalIgnoreCase) && File.Exists(caminho))
            {
                File.Delete(caminho);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fica um arquivo órfão em Arquivos\; não vale interromper o salvamento por isso.
        }
    }
}
