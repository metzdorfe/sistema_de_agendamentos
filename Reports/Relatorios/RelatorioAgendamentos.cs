using EspacoFestas.Models;
using EspacoFestas.Services;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Reports.Relatorios;

/// <summary>RF17: agendamentos por período (data do evento), com cliente, status e serviço opcionais.</summary>
public sealed class RelatorioAgendamentos : RelatorioTabular
{
    public override string Nome => "Relatório de agendamentos";
    public override string NomeLog => "Agendamentos";
    public override string Descricao => "Por período, cliente, status e serviço: data, horário, título, cliente, serviço, status, total, pago e saldo.";
    public override string Icone => "IconeAgenda";
    public override CamposFiltro Filtros => CamposFiltro.Periodo | CamposFiltro.Cliente | CamposFiltro.Status | CamposFiltro.Servico;
    protected override bool Paisagem => true;

    protected override IReadOnlyList<Secao> Montar(FiltrosRelatorio filtros)
    {
        using var contexto = App.CriarContexto();
        var consulta = contexto.Agendamentos.AsNoTracking()
            .Where(a => a.DataEvento >= filtros.De!.Value && a.DataEvento <= filtros.Ate!.Value);
        if (filtros.ClienteId is { } clienteId)
        {
            consulta = consulta.Where(a => a.ClienteId == clienteId);
        }

        if (filtros.Status is { } status)
        {
            consulta = consulta.Where(a => a.Status == status);
        }

        if (filtros.ServicoId is { } servicoId)
        {
            consulta = consulta.Where(a => a.Servicos.Any(s => s.ServicoId == servicoId));
        }

        var agendamentos = consulta
            .OrderBy(a => a.DataEvento).ThenBy(a => a.HoraInicio)
            .Select(a => new
            {
                a.Id,
                a.DataEvento,
                a.HoraInicio,
                a.HoraFim,
                a.Titulo,
                Cliente = a.Cliente!.Nome,
                Servicos = a.Servicos.Select(s => s.Servico!.Nome).ToList(),
                a.Status,
                a.ValorTotal
            })
            .ToList();
        var pagos = AgendamentoService.ValoresPagos(contexto, consulta);

        bool temCancelado = agendamentos.Any(a => a.Status == StatusAgendamento.Cancelado);
        var secao = new Secao
        {
            Titulo = "Agendamentos",
            Subtitulo = temCancelado ? "Os totais de Total e Saldo não somam os cancelados." : null,
            Colunas =
            [
                new("Data", TipoColuna.Data, 1),
                new("Horário", TipoColuna.Texto, 1.1f),
                new("Título", TipoColuna.Texto, 2),
                new("Cliente", TipoColuna.Texto, 2),
                new("Serviço", TipoColuna.Texto, 2),
                new("Status", TipoColuna.Texto, 1.1f),
                new("Total", TipoColuna.Moeda, 1.2f),
                new("Pago", TipoColuna.Moeda, 1.2f),
                new("Saldo", TipoColuna.Moeda, 1.2f)
            ]
        };

        decimal somaTotal = 0, somaPago = 0, somaSaldo = 0;
        foreach (var a in agendamentos)
        {
            bool cancelado = a.Status == StatusAgendamento.Cancelado;
            decimal pago = pagos.GetValueOrDefault(a.Id);
            decimal saldo = cancelado ? 0 : a.ValorTotal - pago;
            var horario = $"{Formatos.Hora(a.HoraInicio)}–{Formatos.Hora(a.HoraFim)}";
            secao.Linhas.Add([a.DataEvento, horario, a.Titulo, a.Cliente, string.Join(", ", a.Servicos), Formatos.NomeStatus(a.Status), a.ValorTotal, pago, saldo]);

            somaPago += pago;
            if (!cancelado)
            {
                somaTotal += a.ValorTotal;
                somaSaldo += saldo;
            }
        }

        secao.Totais = [$"Total ({agendamentos.Count})", null, null, null, null, null, somaTotal, somaPago, somaSaldo];
        return [secao];
    }
}
