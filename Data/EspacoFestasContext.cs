using EspacoFestas.Models;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Data;

public class EspacoFestasContext : DbContext
{
    public EspacoFestasContext(DbContextOptions<EspacoFestasContext> options) : base(options)
    {
    }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Servico> Servicos => Set<Servico>();
    public DbSet<ServicoFaixa> ServicoFaixas => Set<ServicoFaixa>();
    public DbSet<Adicional> Adicionais => Set<Adicional>();
    public DbSet<ServicoAdicional> ServicoAdicionais => Set<ServicoAdicional>();
    public DbSet<Especie> Especies => Set<Especie>();
    public DbSet<Agendamento> Agendamentos => Set<Agendamento>();
    public DbSet<ServicoAgendamento> ServicoAgendamentos => Set<ServicoAgendamento>();
    public DbSet<AdicionalAgendamento> AdicionalAgendamentos => Set<AdicionalAgendamento>();
    public DbSet<Receber> Recebers => Set<Receber>();
    public DbSet<Financeiro> Financeiros => Set<Financeiro>();
    public DbSet<Configuracao> Configuracoes => Set<Configuracao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServicoAdicional>().HasKey(sa => new { sa.ServicoId, sa.AdicionalId });

        modelBuilder.Entity<Servico>()
            .HasMany(s => s.Faixas)
            .WithOne(f => f.Servico)
            .HasForeignKey(f => f.ServicoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ServicoAdicional>()
            .HasOne(sa => sa.Servico)
            .WithMany(s => s.ServicoAdicionais)
            .HasForeignKey(sa => sa.ServicoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ServicoAdicional>()
            .HasOne(sa => sa.Adicional)
            .WithMany(a => a.ServicoAdicionais)
            .HasForeignKey(sa => sa.AdicionalId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Agendamento>()
            .HasOne(a => a.Cliente)
            .WithMany(c => c.Agendamentos)
            .HasForeignKey(a => a.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ServicoAgendamento>()
            .HasOne(sa => sa.Agendamento)
            .WithMany(a => a.Servicos)
            .HasForeignKey(sa => sa.AgendamentoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ServicoAgendamento>()
            .HasOne(sa => sa.Servico)
            .WithMany()
            .HasForeignKey(sa => sa.ServicoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ServicoAgendamento>()
            .HasOne(sa => sa.ServicoFaixa)
            .WithMany()
            .HasForeignKey(sa => sa.ServicoFaixaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AdicionalAgendamento>()
            .HasOne(aa => aa.Agendamento)
            .WithMany(a => a.Adicionais)
            .HasForeignKey(aa => aa.AgendamentoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AdicionalAgendamento>()
            .HasOne(aa => aa.Adicional)
            .WithMany()
            .HasForeignKey(aa => aa.AdicionalId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Receber>()
            .HasOne(r => r.Agendamento)
            .WithMany(a => a.Parcelas)
            .HasForeignKey(r => r.AgendamentoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Receber>()
            .HasOne(r => r.Especie)
            .WithMany()
            .HasForeignKey(r => r.EspecieId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Receber>()
            .HasOne(r => r.ParcelaOrigem)
            .WithMany()
            .HasForeignKey(r => r.ParcelaOrigemId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Financeiro>()
            .HasOne(f => f.Receber)
            .WithMany(r => r.Lancamentos)
            .HasForeignKey(f => f.ReceberId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Financeiro>()
            .HasOne(f => f.Agendamento)
            .WithMany(a => a.Lancamentos)
            .HasForeignKey(f => f.AgendamentoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Financeiro>()
            .HasOne(f => f.Especie)
            .WithMany()
            .HasForeignKey(f => f.EspecieId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Cliente>()
            .Property(c => c.DataCadastro).HasColumnType("DATE");
        modelBuilder.Entity<Agendamento>()
            .Property(a => a.DataEvento).HasColumnType("DATE");
        modelBuilder.Entity<Agendamento>()
            .Property(a => a.HoraInicio).HasColumnType("TIME");
        modelBuilder.Entity<Agendamento>()
            .Property(a => a.HoraFim).HasColumnType("TIME");
        modelBuilder.Entity<Agendamento>()
            .Property(a => a.DataCancelamento).HasColumnType("DATE");
        modelBuilder.Entity<Agendamento>()
            .Property(a => a.DataCadastro).HasColumnType("DATE");
        modelBuilder.Entity<Receber>()
            .Property(r => r.DataVencimento).HasColumnType("DATE");
        modelBuilder.Entity<Receber>()
            .Property(r => r.DataRecebimento).HasColumnType("DATE");
        modelBuilder.Entity<Financeiro>()
            .Property(f => f.DataLancamento).HasColumnType("DATE");

        // Tamanhos dos VARCHAR, iguais ao schema.sql e ao MaxLength das telas (ajustes v2, seção 4)
        modelBuilder.Entity<Cliente>(c =>
        {
            c.Property(x => x.Nome).HasMaxLength(100);
            c.Property(x => x.CpfCnpj).HasMaxLength(14);
            c.Property(x => x.Telefone).HasMaxLength(11);
            c.Property(x => x.Email).HasMaxLength(100);
            c.Property(x => x.Logradouro).HasMaxLength(100);
            c.Property(x => x.Numero).HasMaxLength(10);
            c.Property(x => x.Complemento).HasMaxLength(60);
            c.Property(x => x.Bairro).HasMaxLength(60);
            c.Property(x => x.Cidade).HasMaxLength(60);
            c.Property(x => x.Uf).HasMaxLength(2).IsFixedLength();
            c.Property(x => x.Cep).HasMaxLength(8).IsFixedLength();
        });
        modelBuilder.Entity<Servico>(s =>
        {
            s.Property(x => x.Nome).HasMaxLength(80);
            s.Property(x => x.Descricao).HasMaxLength(255);
        });
        modelBuilder.Entity<ServicoFaixa>().Property(f => f.Nome).HasMaxLength(80);
        modelBuilder.Entity<Adicional>(a =>
        {
            a.Property(x => x.Nome).HasMaxLength(80);
            a.Property(x => x.Descricao).HasMaxLength(255);
        });
        modelBuilder.Entity<Especie>().Property(e => e.Descricao).HasMaxLength(80);
        modelBuilder.Entity<Agendamento>(a =>
        {
            a.Property(x => x.Titulo).HasMaxLength(80);
            a.Property(x => x.MotivoCancelamento).HasMaxLength(200);
            a.Property(x => x.Observacao).HasMaxLength(500);
        });

        modelBuilder.Entity<Agendamento>()
            .Property(a => a.Status)
            .HasConversion(
                v => Conversoes.StatusAgendamentoParaChar(v),
                v => Conversoes.CharParaStatusAgendamento(v))
            .HasColumnType("CHAR(1)");

        modelBuilder.Entity<Receber>()
            .Property(r => r.Status)
            .HasConversion(
                v => Conversoes.StatusReceberParaChar(v),
                v => Conversoes.CharParaStatusReceber(v))
            .HasColumnType("CHAR(1)");

        modelBuilder.Entity<Financeiro>()
            .Property(f => f.Tipo)
            .HasConversion(
                v => Conversoes.TipoLancamentoParaChar(v),
                v => Conversoes.CharParaTipoLancamento(v))
            .HasColumnType("CHAR(1)");

        modelBuilder.Entity<Configuracao>()
            .Property(c => c.Tema)
            .HasConversion(
                v => Conversoes.TemaParaChar(v),
                v => Conversoes.CharParaTema(v))
            .HasColumnType("CHAR(1)");

        modelBuilder.Entity<Configuracao>()
            .Property(c => c.Id).ValueGeneratedNever();

        // Convenção de nomes: PascalCase (C#) -> MAIÚSCULO_COM_UNDERSCORE (Firebird). Ver stack-projeto.md.
        // Usa o nome da classe (singular), não o nome do DbSet (plural), como base do nome da tabela.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(NomeBanco.Converter(entity.ClrType.Name));

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(NomeBanco.Converter(property.Name));
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}
