using System.IO;
using System.Reflection;
using System.Text;
using FirebirdSql.Data.FirebirdClient;
using FirebirdSql.Data.Isql;
using FirebirdSql.Data.Services;

namespace EspacoFestas.Services;

/// <summary>
/// Atualiza a estrutura de um banco criado por uma versão anterior, aplicando os Database\alteracoes-*.sql que faltam
/// (embutidos no executável). Cada alteração sabe conferir, pela própria estrutura do banco, se já foi aplicada,
/// então não precisa de tabela de versão. Antes de alterar, faz backup (gbak) em Utilitarios\Backups.
/// Roda na splash e pelo instalador (FestAgenda.exe --atualizar-banco). O resultado vai para Log\AtualizacaoBanco.log.
/// </summary>
public static class AtualizacaoBanco
{
    public const string NomeLog = "AtualizacaoBanco.log";

    /// <param name="Arquivo">Script em Database\ (recurso embutido).</param>
    /// <param name="JaAplicada">SELECT que devolve um número maior que zero quando a alteração já está no banco.</param>
    private sealed record Alteracao(string Arquivo, string Descricao, string JaAplicada);

    /// <summary>Em ordem. Bancos anteriores a 29/09/2026 (antes do primeiro instalador) não são atualizados automaticamente.</summary>
    private static readonly Alteracao[] Alteracoes =
    [
        new("alteracoes-2026-09-30.sql", "logo da empresa em arquivo e tema claro padrão",
            "SELECT COUNT(*) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME = 'CONFIGURACAO' AND RDB$FIELD_NAME = 'CAMINHO_LOGO_EMPRESA'"),
        new("alteracoes-2026-10-04.sql", "tamanhos dos campos e telefone só com dígitos",
            "SELECT COUNT(*) FROM RDB$RELATION_FIELDS R JOIN RDB$FIELDS F ON F.RDB$FIELD_NAME = R.RDB$FIELD_SOURCE " +
            "WHERE R.RDB$RELATION_NAME = 'CLIENTE' AND R.RDB$FIELD_NAME = 'TELEFONE' AND F.RDB$CHARACTER_LENGTH = 11"),
        new("alteracoes-2026-10-18.sql", "estorno, suprimento e sangria, WhatsApp",
            "SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME = 'WHATSAPP_MENSAGEM'")
    ];

    private const string ConsultaBaseMinima =
        "SELECT COUNT(*) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME = 'AGENDAMENTO' AND RDB$FIELD_NAME = 'TITULO'";

    public static string CaminhoLog => Path.Combine(AppPaths.Log, NomeLog);

    /// <summary>Quais alterações faltam neste banco (vazio = está em dia).</summary>
    public static IReadOnlyList<string> Pendentes()
    {
        using var conexao = new FbConnection(App.MontarConexao());
        conexao.Open();
        if (Contar(conexao, ConsultaBaseMinima) == 0)
        {
            throw new InvalidOperationException(
                "O banco é de uma versão anterior a 29/09/2026 e não pode ser atualizado automaticamente. Use os scripts de Utilitarios\\Scripts.");
        }

        return Alteracoes.Where(a => Contar(conexao, a.JaAplicada) == 0).Select(a => a.Arquivo).ToList();
    }

    /// <summary>Aplica o que falta. Devolve os scripts aplicados (vazio = já estava em dia).</summary>
    /// <param name="passo">Texto para a splash (ex.: "Fazendo backup do banco...").</param>
    public static IReadOnlyList<string> Executar(Action<string>? passo = null)
    {
        var pendentes = Pendentes();
        if (pendentes.Count == 0)
        {
            return pendentes;
        }

        // O banco precisa estar livre para alterar a estrutura: fecha as conexões que o pool deixou abertas
        FbConnection.ClearAllPools();

        passo?.Invoke("Fazendo backup do banco...");
        var backup = FazerBackup();
        Registrar($"Atualização necessária: {string.Join(", ", pendentes)}. Backup: {backup ?? "não feito (banco em servidor remoto)"}");

        foreach (var arquivo in pendentes)
        {
            var alteracao = Alteracoes.Single(a => a.Arquivo == arquivo);
            passo?.Invoke($"Atualizando o banco ({alteracao.Descricao})...");
            try
            {
                using var conexao = new FbConnection(App.MontarConexao());
                conexao.Open();
                var script = new FbScript(LerScript(arquivo));
                script.Parse();
                var execucao = new FbBatchExecution(conexao);
                execucao.AppendSqlStatements(script);
                execucao.Execute(autoCommit: true);
                Registrar($"OK   {arquivo}");
            }
            catch (Exception ex)
            {
                Registrar($"ERRO {arquivo}: {ex.Message}");
                throw new InvalidOperationException(
                    $"Não foi possível atualizar o banco ({arquivo}): {ex.Message}" +
                    (backup is null ? string.Empty : $"\nO backup feito antes está em {backup} (use o restaurar.bat)."), ex);
            }
            finally
            {
                FbConnection.ClearAllPools();
            }
        }

        return pendentes;
    }

    /// <summary>Backup pelo serviço do Firebird (funciona no embarcado e no servidor local). Servidor remoto: sem backup aqui.</summary>
    private static string? FazerBackup()
    {
        if (!App.BancoLocal)
        {
            return null;
        }

        var pasta = Path.Combine(AppPaths.Utilitarios, "Backups");
        Directory.CreateDirectory(pasta);
        var destino = Path.Combine(pasta, $"antes-atualizacao-{DateTime.Now:yyyyMMdd-HHmmss}.fbk");
        var servico = new FbBackup(App.MontarConexao());
        servico.BackupFiles.Add(new FbBackupFile(destino));
        servico.Execute();
        return destino;
    }

    private static int Contar(FbConnection conexao, string sql)
    {
        using var comando = new FbCommand(sql, conexao);
        return Convert.ToInt32(comando.ExecuteScalar());
    }

    private static string LerScript(string arquivo)
    {
        var recurso = Assembly.GetExecutingAssembly().GetManifestResourceNames().Single(n => n.EndsWith(arquivo, StringComparison.OrdinalIgnoreCase));
        using var fluxo = Assembly.GetExecutingAssembly().GetManifestResourceStream(recurso)!;
        using var leitor = new StreamReader(fluxo, Encoding.UTF8);
        return leitor.ReadToEnd();
    }

    /// <summary>Uma linha por evento em Log\AtualizacaoBanco.log. Falha no log não impede a atualização.</summary>
    public static void Registrar(string texto)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Log);
            File.AppendAllText(CaminhoLog, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {texto.ReplaceLineEndings(" ")}{Environment.NewLine}", new UTF8Encoding(false));
        }
        catch (Exception)
        {
            // Sem permissão etc.: segue sem log
        }
    }
}
