using System.IO;
using System.Text;
using EspacoFestas;
using EspacoFestas.Models;
using EspacoFestas.Services;
using FirebirdSql.Data.FirebirdClient;
using FirebirdSql.Data.Isql;

namespace FestAgenda.Tests;

/// <summary>
/// Banco Firebird novo para os testes: criado no servidor local a partir de Database\schema.sql e dados-iniciais.sql,
/// com um cliente e dois serviços, e apagado no fim. Sem servidor no ar, os testes de banco são pulados.
/// </summary>
public sealed class BancoTeste : IDisposable
{
    private readonly string _conexao = string.Empty;

    /// <summary>Preenchido quando o banco não pôde ser criado (os testes de banco são pulados com esse motivo).</summary>
    public string? Indisponivel { get; }

    public int ClienteId { get; }
    public int ServicoId { get; }
    public int FaixaId { get; }
    /// <summary>Adicional cobrado por quantidade: venda 10, custo 4.</summary>
    public int AdicionalId { get; }
    /// <summary>Espécie com parcelamento (Cartão de crédito).</summary>
    public int Cartao { get; }
    /// <summary>Espécie sem parcelamento (Pix).</summary>
    public int Pix { get; }

    public BancoTeste()
    {
        var arquivo = Path.Combine(Path.GetTempPath(), $"festagenda-teste-{Guid.NewGuid():N}.fdb");
        _conexao = new FbConnectionStringBuilder
        {
            DataSource = "localhost",
            Port = 3050,
            Database = arquivo,
            UserID = "SYSDBA",
            Password = "masterkey",
            Charset = "UTF8"
        }.ToString();

        try
        {
            FbConnection.CreateDatabase(_conexao, pageSize: 16384, forcedWrites: false);
        }
        catch (Exception ex)
        {
            Indisponivel = $"Firebird local (localhost:3050) indisponível: {ex.Message}";
            return;
        }

        foreach (var script in new[] { "schema.sql", "dados-iniciais.sql" })
        {
            using var conexao = new FbConnection(_conexao);
            conexao.Open();
            var sql = new FbScript(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Database", script), Encoding.UTF8));
            sql.Parse();
            var execucao = new FbBatchExecution(conexao);
            execucao.AppendSqlStatements(sql);
            execucao.Execute(autoCommit: true);
        }

        // O sistema lê a conexão do config.ini da própria pasta: aponta para o banco de teste
        File.WriteAllText(AppPaths.ConfigIni,
            $"[Banco]\nTipo=Servidor\nServidor=localhost\nPorta=3050\nCaminho={arquivo}\nUsuario=SYSDBA\nSenha=masterkey\n");
        App.CarregarConfiguracaoIni();
        App.CarregarSistema();

        using var contexto = App.CriarContexto();
        var cliente = new Cliente { Nome = "Cliente Teste", Telefone = "47999999999" };
        var servico = new Servico { Nome = "Salão" };
        var faixa = new ServicoFaixa { Servico = servico, Nome = "Até 100 pessoas", Valor = 1000m };
        var adicional = new Adicional { Nome = "Docinhos", ValorVenda = 10m, ValorCusto = 4m, CobraPorQuantidade = true };
        contexto.AddRange(cliente, servico, faixa, adicional);
        contexto.SaveChanges();
        ClienteId = cliente.Id;
        ServicoId = servico.Id;
        FaixaId = faixa.Id;
        AdicionalId = adicional.Id;
        Cartao = contexto.Especies.Single(e => e.Descricao == "Cartão de crédito").Id;
        Pix = contexto.Especies.Single(e => e.Descricao == "Pix").Id;
    }

    public void Dispose()
    {
        if (Indisponivel is not null)
        {
            return;
        }

        FbConnection.ClearAllPools();
        FbConnection.DropDatabase(_conexao);
    }
}

[CollectionDefinition(Nome, DisableParallelization = true)]
public sealed class ColecaoBanco : ICollectionFixture<BancoTeste>
{
    public const string Nome = "Banco";
}
