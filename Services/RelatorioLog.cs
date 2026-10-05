using System.IO;
using System.Text;

namespace EspacoFestas.Services;

/// <summary>
/// RN31: toda geração de relatório (sucesso ou erro) vira uma linha em Log\RelatorioLog.log, em UTF-8:
/// <code>2026-10-04 22:30:15 | Agendamentos | PDF  | periodo=01/10/2026-31/10/2026; cliente=Todos | OK    | C:\...\agendamentos.pdf</code>
/// Falha ao gravar o log nunca impede o relatório.
/// </summary>
public static class RelatorioLog
{
    public const string NomeArquivo = "RelatorioLog.log";

    private static readonly object Trava = new();

    public static string Caminho => Path.Combine(AppPaths.Log, NomeArquivo);

    public static void Sucesso(string relatorio, string formato, string filtros, string arquivo) =>
        Registrar(relatorio, formato, filtros, "OK", arquivo);

    public static void Erro(string relatorio, string formato, string filtros, string mensagem) =>
        Registrar(relatorio, formato, filtros, "ERRO", mensagem);

    private static void Registrar(string relatorio, string formato, string filtros, string situacao, string detalhe)
    {
        var linha = string.Join(" | ",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            relatorio.PadRight(12),
            formato.PadRight(4),
            filtros,
            situacao.PadRight(5),
            detalhe.ReplaceLineEndings(" "));

        try
        {
            lock (Trava)
            {
                Directory.CreateDirectory(AppPaths.Log);
                File.AppendAllText(Caminho, linha + Environment.NewLine, new UTF8Encoding(false));
            }
        }
        catch (Exception)
        {
            // Sem permissão, disco cheio etc.: o relatório segue sem log
        }
    }
}
