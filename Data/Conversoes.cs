using EspacoFestas.Models;

namespace EspacoFestas.Data;

/// <summary>Conversões enum &lt;-&gt; CHAR(1) usadas no mapeamento do EF (ver modelagem-banco.md).</summary>
internal static class Conversoes
{
    public static string StatusAgendamentoParaChar(StatusAgendamento status) => status switch
    {
        StatusAgendamento.Orcamento => "O",
        StatusAgendamento.Agendado => "A",
        StatusAgendamento.Faturado => "F",
        StatusAgendamento.Cancelado => "C",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static StatusAgendamento CharParaStatusAgendamento(string status) => status switch
    {
        "O" => StatusAgendamento.Orcamento,
        "A" => StatusAgendamento.Agendado,
        "F" => StatusAgendamento.Faturado,
        "C" => StatusAgendamento.Cancelado,
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static string StatusReceberParaChar(StatusReceber status) => status switch
    {
        StatusReceber.Aberto => "A",
        StatusReceber.Pago => "P",
        StatusReceber.Cancelado => "C",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static StatusReceber CharParaStatusReceber(string status) => status switch
    {
        "A" => StatusReceber.Aberto,
        "P" => StatusReceber.Pago,
        "C" => StatusReceber.Cancelado,
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static string TipoLancamentoParaChar(TipoLancamento tipo) =>
        tipo == TipoLancamento.Entrada ? "E" : "S";

    public static TipoLancamento CharParaTipoLancamento(string tipo) =>
        tipo == "E" ? TipoLancamento.Entrada : TipoLancamento.Saida;

    public static string TemaParaChar(Tema tema) => tema switch
    {
        Tema.Escuro => "E",
        Tema.Claro => "C",
        Tema.SeguirWindows => "S",
        _ => throw new ArgumentOutOfRangeException(nameof(tema))
    };

    public static Tema CharParaTema(string tema) => tema switch
    {
        "E" => Tema.Escuro,
        "S" => Tema.SeguirWindows,
        _ => Tema.Claro
    };
}
