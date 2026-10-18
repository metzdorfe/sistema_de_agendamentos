using System.IO;
using System.Text;

namespace EspacoFestas.Services;

/// <summary>
/// Erros não tratados em Log\Erros.log (UTF-8), para quem for dar suporte saber o que aconteceu.
/// Grava tipo, mensagem e pilha da exceção; nunca dados digitados (CPF, telefone etc.).
/// Passando de 1 MB, o arquivo vira Erros.1.log (e assim por diante, guardando os 3 últimos).
/// Falha ao gravar o log nunca derruba o sistema.
/// </summary>
public static class ErroLog
{
    public const string NomeArquivo = "Erros.log";
    private const long TamanhoMaximo = 1024 * 1024;
    private const int ArquivosGuardados = 3;

    private static readonly object Trava = new();

    public static string Caminho => Path.Combine(AppPaths.Log, NomeArquivo);

    /// <param name="origem">Onde foi pego (ex.: "Interface", "Tarefa", "Splash").</param>
    public static void Registrar(Exception erro, string origem) => Gravar(origem, erro.ToString());

    /// <summary>Problema sem exceção (ex.: o serviço do WhatsApp parou e não voltou).</summary>
    public static void Registrar(string descricao, string origem) => Gravar(origem, descricao);

    /// <summary>
    /// Números com 8 dígitos ou mais (CPF, CNPJ, telefone) saem mascarados, só com os 4 últimos: mensagens do banco
    /// podem trazer o valor (ex.: chave duplicada em UX_CLIENTE_CPF_CNPJ). Linhas da pilha (":line 123") não são afetadas.
    /// </summary>
    public static string Mascarar(string texto) =>
        System.Text.RegularExpressions.Regex.Replace(texto, @"\d{8,}", m => new string('*', m.Length - 4) + m.Value[^4..]);

    private static void Gravar(string origem, string detalhe)
    {
        var texto = new StringBuilder()
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(" | ").Append(origem).Append(" | v")
            .AppendLine(typeof(App).Assembly.GetName().Version?.ToString(3))
            .AppendLine(Mascarar(detalhe))
            .AppendLine(new string('-', 80))
            .ToString();

        try
        {
            lock (Trava)
            {
                Directory.CreateDirectory(AppPaths.Log);
                Rotacionar();
                File.AppendAllText(Caminho, texto, new UTF8Encoding(false));
            }
        }
        catch (Exception)
        {
            // Sem permissão, disco cheio etc.: segue sem log
        }
    }

    /// <summary>Erros.log cheio: Erros.2 → Erros.3, Erros.1 → Erros.2, Erros → Erros.1.</summary>
    private static void Rotacionar()
    {
        var atual = new FileInfo(Caminho);
        if (!atual.Exists || atual.Length < TamanhoMaximo)
        {
            return;
        }

        string Antigo(int n) => Path.Combine(AppPaths.Log, $"Erros.{n}.log");
        File.Delete(Antigo(ArquivosGuardados));
        for (int n = ArquivosGuardados - 1; n >= 1; n--)
        {
            if (File.Exists(Antigo(n)))
            {
                File.Move(Antigo(n), Antigo(n + 1));
            }
        }

        File.Move(Caminho, Antigo(1));
    }
}
