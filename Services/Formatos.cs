using System.Globalization;

namespace EspacoFestas.Services;

/// <summary>Formatos brasileiros (RNF08): R$, vírgula decimal, dd/mm/aaaa.</summary>
public static class Formatos
{
    public static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    public static string Moeda(decimal valor) => valor.ToString("C2", Brasil);

    public static string Data(DateTime? data) => data?.ToString("dd/MM/yyyy", Brasil) ?? "-";

    public static string Hora(TimeSpan hora) => hora.ToString(@"hh\:mm");

    public static string Decimal(decimal valor) => valor.ToString("N2", Brasil);

    /// <summary>Aceita "1.500,00", "1500,5", "R$ 1.500,00" e também "1500.50".</summary>
    public static bool TentarLerDecimal(string? texto, out decimal valor)
    {
        valor = 0;
        var limpo = (texto ?? string.Empty).Replace("R$", string.Empty).Replace("%", string.Empty).Trim();
        if (limpo.Length == 0)
        {
            return false;
        }

        if (!limpo.Contains(',') && limpo.Count(c => c == '.') == 1 && limpo.Length - limpo.IndexOf('.') <= 3)
        {
            return decimal.TryParse(limpo, NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
        }

        return decimal.TryParse(limpo, NumberStyles.Number, Brasil, out valor);
    }

    /// <summary>Intervalo curto de datas: "4 – 10 de outubro de 2026", "28 de set. – 4 de out. de 2026" ou com os dois anos.</summary>
    public static string PeriodoCurto(DateTime inicio, DateTime fim)
    {
        if (inicio.Year != fim.Year)
        {
            return $"{inicio.ToString("d MMM yyyy", Brasil)} – {fim.ToString("d MMM yyyy", Brasil)}";
        }

        return inicio.Month == fim.Month
            ? $"{inicio.Day} – {fim.ToString("d 'de' MMMM 'de' yyyy", Brasil)}"
            : $"{inicio.ToString("d MMM", Brasil)} – {fim.ToString("d MMM yyyy", Brasil)}";
    }

    public static string NomeStatus(Models.StatusAgendamento status) => status switch
    {
        Models.StatusAgendamento.Orcamento => "Orçamento",
        Models.StatusAgendamento.Agendado => "Agendado",
        Models.StatusAgendamento.Faturado => "Faturado",
        _ => "Cancelado"
    };

    public static string Capitalizar(string texto) =>
        texto.Length == 0 ? texto : char.ToUpper(texto[0], Brasil) + texto[1..];
}
