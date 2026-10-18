namespace EspacoFestas.Models;

public enum StatusAgendamento
{
    Orcamento,
    Agendado,
    Faturado,
    Cancelado
}

public enum StatusReceber
{
    Aberto,
    Pago,
    Cancelado
}

public enum TipoLancamento
{
    Entrada,
    Saida
}

public enum TipoMensagem
{
    Catalogo,
    Orcamento,
    LembreteEvento,
    CobrancaParcela,
    Avulsa
}

public enum StatusMensagem
{
    Pendente,
    Enviada,
    Falha,
    Cancelada
}

public enum Tema
{
    Escuro,
    Claro,
    SeguirWindows
}
