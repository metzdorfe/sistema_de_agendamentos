using System.IO;

namespace EspacoFestas.Services;

/// <summary>
/// Pastas do sistema, sempre a partir da pasta do executável (nunca caminho fixo como C:\FestAgenda),
/// para o sistema continuar funcionando se a pasta for movida. Ver stack-projeto.md.
/// <code>
/// FestAgenda.exe, *.dll, config.ini
/// BD\           banco (basefestagenda.fdb)
/// Arquivos\     imagens e outros arquivos; o banco guarda só o caminho relativo a esta pasta
/// Log\          RelatorioLog.log, Erros.log, whatsapp.log; preservada na desinstalação
/// Utilitarios\  ferramentas de manutenção (backup, restauração, isql)
/// Whatsapp\     servidor local do WhatsApp (node\, server\) e a sessão (sessao\)
/// </code>
/// </summary>
public static class AppPaths
{
    public const string NomeConfigIni = "config.ini";

    public static string Raiz { get; } = AppContext.BaseDirectory;

    public static string BD => Path.Combine(Raiz, "BD");

    public static string Arquivos => Path.Combine(Raiz, "Arquivos");

    public static string Utilitarios => Path.Combine(Raiz, "Utilitarios");

    /// <summary>Pasta de logs na raiz da instalação (C:\FestAgenda\Log), fora de Arquivos\.</summary>
    public static string Log => Path.Combine(Raiz, "Log");

    public static string ConfigIni => Path.Combine(Raiz, NomeConfigIni);

    /// <summary>Servidor do WhatsApp: node\ (Node portátil), server\ (código e node_modules) e sessao\ (login por QR).</summary>
    public static string Whatsapp => Path.Combine(Raiz, "Whatsapp");

    /// <summary>Sessão do WhatsApp; preservada na atualização e na desinstalação.</summary>
    public static string WhatsappSessao => Path.Combine(Whatsapp, "sessao");

    /// <summary>Subpasta de Arquivos\ (ex.: "Empresa"), criada sob demanda.</summary>
    public static string PastaArquivos(string subpasta)
    {
        var pasta = Path.Combine(Arquivos, subpasta);
        Directory.CreateDirectory(pasta);
        return pasta;
    }

    /// <summary>Caminho relativo à pasta Arquivos\ (como fica gravado no banco) → caminho completo.</summary>
    public static string? ArquivoCompleto(string? caminhoRelativo) =>
        string.IsNullOrWhiteSpace(caminhoRelativo) ? null : Path.GetFullPath(Path.Combine(Arquivos, caminhoRelativo));

    /// <summary>Caminho completo dentro de Arquivos\ → relativo, para gravar no banco (ex.: Empresa\logo.png).</summary>
    public static string ArquivoRelativo(string caminhoCompleto) => Path.GetRelativePath(Arquivos, caminhoCompleto);

    /// <summary>Caminho do config.ini relativo à pasta do sistema ou absoluto.</summary>
    public static string Resolver(string caminho) =>
        Path.IsPathRooted(caminho) ? caminho : Path.GetFullPath(Path.Combine(Raiz, caminho));
}
