using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EspacoFestas.Services;

namespace EspacoFestas.Controls;

public enum TipoMascara
{
    Nenhuma,
    /// <summary>(99) 99999-9999 ou (99) 9999-9999.</summary>
    Telefone,
    /// <summary>000.000.000-00 até 11 dígitos; 00.000.000/0000-00 com 12 a 14.</summary>
    CpfCnpj,
    /// <summary>Só CNPJ: 00.000.000/0000-00.</summary>
    Cnpj,
    /// <summary>00000-000.</summary>
    Cep,
    /// <summary>HH:mm, 00:00 a 23:59.</summary>
    Hora,
    /// <summary>dd/MM/yyyy.</summary>
    Data,
    /// <summary>Só dígitos, até Maximo (ex.: convidados até 32767; o mínimo é conferido ao salvar).</summary>
    Inteiro,
    /// <summary>R$: dígitos e vírgula, 2 casas, até 9.999.999,99, sem negativo.</summary>
    Moeda,
    /// <summary>0 a 100, 2 casas.</summary>
    Percentual
}

/// <summary>
/// Máscara de entrada para TextBox, reaproveitada em todas as telas: <c>ctl:Mascara.Tipo="Telefone"</c>.
/// Bloqueia a digitação inválida (PreviewTextInput e colagem) em vez de só mostrar erro depois.
/// Colar texto já formatado (ex.: "(49) 99999-0000" ou "R$ 1.500,00") limpa para o que o campo aceita.
/// A máscara é só da tela: para gravar, use <see cref="Documentos.SoDigitos"/> ou <see cref="Formatos.TentarLerDecimal"/>.
/// </summary>
public static class Mascara
{
    private const decimal MaximoMoeda = 9_999_999.99m;

    public static readonly DependencyProperty TipoProperty =
        DependencyProperty.RegisterAttached("Tipo", typeof(TipoMascara), typeof(Mascara),
            new PropertyMetadata(TipoMascara.Nenhuma, TipoAlterado));

    public static TipoMascara GetTipo(DependencyObject elemento) => (TipoMascara)elemento.GetValue(TipoProperty);

    public static void SetTipo(DependencyObject elemento, TipoMascara valor) => elemento.SetValue(TipoProperty, valor);

    /// <summary>Limite superior da máscara Inteiro (padrão 32767, o SMALLINT do banco).</summary>
    public static readonly DependencyProperty MaximoProperty =
        DependencyProperty.RegisterAttached("Maximo", typeof(int), typeof(Mascara), new PropertyMetadata((int)short.MaxValue));

    public static int GetMaximo(DependencyObject elemento) => (int)elemento.GetValue(MaximoProperty);

    public static void SetMaximo(DependencyObject elemento, int valor) => elemento.SetValue(MaximoProperty, valor);

    private static readonly DependencyProperty FormatandoProperty =
        DependencyProperty.RegisterAttached("Formatando", typeof(bool), typeof(Mascara), new PropertyMetadata(false));

    private static void TipoAlterado(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox campo)
        {
            return;
        }

        campo.PreviewTextInput -= AoDigitar;
        campo.PreviewKeyDown -= AoTeclar;
        campo.TextChanged -= AoMudarTexto;
        DataObject.RemovePastingHandler(campo, AoColar);

        var tipo = (TipoMascara)e.NewValue;
        if (tipo == TipoMascara.Nenhuma)
        {
            return;
        }

        campo.MaxLength = tipo switch
        {
            TipoMascara.Telefone => 15,
            TipoMascara.CpfCnpj or TipoMascara.Cnpj => 18,
            TipoMascara.Cep => 9,
            TipoMascara.Hora => 5,
            TipoMascara.Data => 10,
            TipoMascara.Inteiro => 5,
            TipoMascara.Moeda => 12,
            TipoMascara.Percentual => 6,
            _ => campo.MaxLength
        };
        InputMethod.SetIsInputMethodEnabled(campo, false);
        campo.PreviewTextInput += AoDigitar;
        campo.PreviewKeyDown += AoTeclar;
        campo.TextChanged += AoMudarTexto;
        DataObject.AddPastingHandler(campo, AoColar);
    }

    // ------------------------------------------------------------------
    // Eventos
    // ------------------------------------------------------------------

    private static void AoDigitar(object sender, TextCompositionEventArgs e)
    {
        var campo = (TextBox)sender;
        var texto = GetTipo(campo) is TipoMascara.Moeda or TipoMascara.Percentual ? e.Text.Replace('.', ',') : e.Text;
        if (!Aceita(campo, TextoProposto(campo, texto)))
        {
            e.Handled = true;
            return;
        }

        // O ponto do teclado numérico vira vírgula nos valores
        if (texto != e.Text)
        {
            e.Handled = true;
            Inserir(campo, texto);
        }
    }

    /// <summary>Espaço não passa pelo PreviewTextInput; bloqueia aqui.</summary>
    private static void AoTeclar(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    private static void AoColar(object sender, DataObjectPastingEventArgs e)
    {
        var campo = (TextBox)sender;
        e.CancelCommand();
        if (e.DataObject.GetData(DataFormats.UnicodeText) is not string colado)
        {
            return;
        }

        var limpo = LimparColagem(GetTipo(campo), colado);
        if (limpo.Length > 0 && Aceita(campo, TextoProposto(campo, limpo)))
        {
            Inserir(campo, limpo);
        }
    }

    /// <summary>Reaplica a máscara depois de cada mudança, mantendo o cursor depois do mesmo dígito.</summary>
    private static void AoMudarTexto(object sender, TextChangedEventArgs e)
    {
        var campo = (TextBox)sender;
        if ((bool)campo.GetValue(FormatandoProperty))
        {
            return;
        }

        var tipo = GetTipo(campo);
        if (tipo is TipoMascara.Moeda or TipoMascara.Percentual or TipoMascara.Inteiro)
        {
            return;
        }

        var formatado = Formatar(tipo, Documentos.SoDigitos(campo.Text));
        if (formatado == campo.Text)
        {
            return;
        }

        int digitosAntesDoCursor = campo.Text[..Math.Min(campo.CaretIndex, campo.Text.Length)].Count(char.IsDigit);
        campo.SetValue(FormatandoProperty, true);
        try
        {
            campo.Text = formatado;
            campo.CaretIndex = PosicaoDoDigito(formatado, digitosAntesDoCursor);
        }
        finally
        {
            campo.SetValue(FormatandoProperty, false);
        }
    }

    // ------------------------------------------------------------------
    // Regras
    // ------------------------------------------------------------------

    private static string TextoProposto(TextBox campo, string inserido)
    {
        var texto = campo.Text;
        int inicio = Math.Min(campo.SelectionStart, texto.Length);
        int tamanho = Math.Min(campo.SelectionLength, texto.Length - inicio);
        return texto[..inicio] + inserido + texto[(inicio + tamanho)..];
    }

    private static void Inserir(TextBox campo, string texto)
    {
        int inicio = campo.SelectionStart;
        campo.SelectedText = texto;
        campo.SelectionLength = 0;
        campo.CaretIndex = Math.Min(inicio + texto.Length, campo.Text.Length);
    }

    private static bool Aceita(TextBox campo, string proposto)
    {
        var tipo = GetTipo(campo);
        if (tipo is TipoMascara.Moeda or TipoMascara.Percentual)
        {
            return DecimalValido(tipo, proposto);
        }

        // Nas máscaras de dígitos, os separadores são da máscara: só dígitos entram
        if (proposto.Any(c => !char.IsDigit(c) && !"()- ./:".Contains(c)))
        {
            return false;
        }

        var d = Documentos.SoDigitos(proposto);
        switch (tipo)
        {
            case TipoMascara.Telefone:
                return d.Length <= 11;
            case TipoMascara.CpfCnpj:
            case TipoMascara.Cnpj:
                return d.Length <= 14;
            case TipoMascara.Cep:
                return d.Length <= 8;
            case TipoMascara.Hora:
                return HoraParcialValida(d);
            case TipoMascara.Data:
                return DataParcialValida(d);
            case TipoMascara.Inteiro:
                if (d.Length == 0)
                {
                    return true;
                }

                return d.Length <= 5 && int.Parse(d, CultureInfo.InvariantCulture) <= GetMaximo(campo);
            default:
                return true;
        }
    }

    /// <summary>Hora sendo digitada: 0–2, depois 0–9 (0–3 se começou com 2), depois 0–5, depois 0–9. Rejeita "25:99".</summary>
    private static bool HoraParcialValida(string d)
    {
        if (d.Length > 4)
        {
            return false;
        }

        if (d.Length >= 1 && d[0] > '2')
        {
            return false;
        }

        if (d.Length >= 2 && d[0] == '2' && d[1] > '3')
        {
            return false;
        }

        return d.Length < 3 || d[2] <= '5';
    }

    /// <summary>Data sendo digitada: dia 01–31, mês 01–12; a data completa é conferida no calendário (ex.: 31/02).</summary>
    private static bool DataParcialValida(string d)
    {
        if (d.Length > 8)
        {
            return false;
        }

        if (d.Length >= 1 && d[0] > '3')
        {
            return false;
        }

        if (d.Length >= 2 && (d[..2] == "00" || int.Parse(d[..2]) > 31))
        {
            return false;
        }

        if (d.Length >= 3 && d[2] > '1')
        {
            return false;
        }

        if (d.Length >= 4 && (d[2..4] == "00" || int.Parse(d[2..4]) > 12))
        {
            return false;
        }

        return d.Length < 8 || LerData(Formatar(TipoMascara.Data, d)) is not null;
    }

    private static bool DecimalValido(TipoMascara tipo, string texto)
    {
        // Os pontos são separador de milhar (ex.: valor carregado "1.500,00"): ignorados
        var limpo = texto.Replace(".", string.Empty);
        if (limpo.Length == 0)
        {
            return true;
        }

        int virgula = limpo.IndexOf(',');
        if (limpo.Any(c => !char.IsDigit(c) && c != ',') || virgula != limpo.LastIndexOf(','))
        {
            return false;
        }

        var inteiro = virgula < 0 ? limpo : limpo[..virgula];
        var casas = virgula < 0 ? string.Empty : limpo[(virgula + 1)..];
        if (casas.Length > 2 || inteiro.Length > (tipo == TipoMascara.Moeda ? 7 : 3))
        {
            return false;
        }

        var valor = decimal.Parse((inteiro.Length == 0 ? "0" : inteiro) + (casas.Length > 0 ? "," + casas : string.Empty), Formatos.Brasil);
        return valor <= (tipo == TipoMascara.Moeda ? MaximoMoeda : 100m);
    }

    private static string LimparColagem(TipoMascara tipo, string colado)
    {
        if (tipo is TipoMascara.Moeda or TipoMascara.Percentual)
        {
            return Formatos.TentarLerDecimal(colado, out var valor) && valor >= 0
                ? valor.ToString("0.##", Formatos.Brasil)
                : string.Empty;
        }

        var digitos = Documentos.SoDigitos(colado);
        // Telefone colado com DDI (+55 49 99999-0000): descarta o 55
        if (tipo == TipoMascara.Telefone && digitos.Length is 12 or 13 && digitos.StartsWith("55"))
        {
            digitos = digitos[2..];
        }

        return digitos;
    }

    // ------------------------------------------------------------------
    // Formatação
    // ------------------------------------------------------------------

    /// <summary>Formata os dígitos (inclusive parciais, durante a digitação).</summary>
    public static string Formatar(TipoMascara tipo, string d) => tipo switch
    {
        TipoMascara.Telefone => d.Length switch
        {
            0 => string.Empty,
            <= 2 => $"({d}",
            <= 6 => $"({d[..2]}) {d[2..]}",
            <= 10 => $"({d[..2]}) {d[2..6]}-{d[6..]}",
            _ => $"({d[..2]}) {d[2..7]}-{d[7..]}"
        },
        TipoMascara.CpfCnpj when d.Length <= 11 => Aplicar(d, "000.000.000-00"),
        TipoMascara.CpfCnpj or TipoMascara.Cnpj => Aplicar(d, "00.000.000/0000-00"),
        TipoMascara.Cep => Aplicar(d, "00000-000"),
        TipoMascara.Hora => Aplicar(d, "00:00"),
        TipoMascara.Data => Aplicar(d, "00/00/0000"),
        _ => d
    };

    /// <summary>Encaixa os dígitos no modelo ("0" = dígito), sem separador sobrando no fim.</summary>
    private static string Aplicar(string digitos, string modelo)
    {
        var resultado = new System.Text.StringBuilder();
        int i = 0;
        foreach (char c in modelo)
        {
            if (i >= digitos.Length)
            {
                break;
            }

            resultado.Append(c == '0' ? digitos[i++] : c);
        }

        return resultado.ToString();
    }

    private static int PosicaoDoDigito(string texto, int digitos)
    {
        if (digitos == 0)
        {
            return texto.Length > 0 && !char.IsDigit(texto[0]) ? 1 : 0;
        }

        int vistos = 0;
        for (int i = 0; i < texto.Length; i++)
        {
            if (char.IsDigit(texto[i]) && ++vistos == digitos)
            {
                return i + 1;
            }
        }

        return texto.Length;
    }

    /// <summary>"dd/MM/yyyy" completo e existente no calendário (rejeita 31/02).</summary>
    public static DateTime? LerData(string? texto) =>
        DateTime.TryParseExact(texto?.Trim(), "dd/MM/yyyy", Formatos.Brasil, DateTimeStyles.None, out var data) ? data : null;

    /// <summary>"HH:mm" de 00:00 a 23:59.</summary>
    public static TimeSpan? LerHora(string? texto) =>
        TimeSpan.TryParseExact(texto?.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var hora) && hora < TimeSpan.FromDays(1)
            ? hora
            : null;
}
