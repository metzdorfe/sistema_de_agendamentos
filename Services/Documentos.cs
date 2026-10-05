namespace EspacoFestas.Services;

/// <summary>Validação e máscara de CPF, CNPJ, telefone e CEP. No banco vão só os dígitos (RN01).</summary>
public static class Documentos
{
    public static string SoDigitos(string? texto) =>
        new((texto ?? string.Empty).Where(char.IsDigit).ToArray());

    public static bool CpfValido(string digitos)
    {
        if (digitos.Length != 11 || digitos.Distinct().Count() == 1)
        {
            return false;
        }

        return DigitoVerificador(digitos[..9], 10) == digitos[9] - '0'
            && DigitoVerificador(digitos[..10], 11) == digitos[10] - '0';
    }

    public static bool CnpjValido(string digitos)
    {
        if (digitos.Length != 14 || digitos.Distinct().Count() == 1)
        {
            return false;
        }

        int[] pesos1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] pesos2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        return DigitoCnpj(digitos[..12], pesos1) == digitos[12] - '0'
            && DigitoCnpj(digitos[..13], pesos2) == digitos[13] - '0';
    }

    public static bool CpfCnpjValido(string digitos) =>
        digitos.Length == 11 ? CpfValido(digitos) : CnpjValido(digitos);

    public static string FormatarCpfCnpj(string? digitos) => digitos?.Length switch
    {
        11 => $"{digitos[..3]}.{digitos[3..6]}.{digitos[6..9]}-{digitos[9..]}",
        14 => $"{digitos[..2]}.{digitos[2..5]}.{digitos[5..8]}/{digitos[8..12]}-{digitos[12..]}",
        _ => digitos ?? string.Empty
    };

    public static string FormatarTelefone(string? texto)
    {
        var d = SoDigitos(texto);
        return d.Length switch
        {
            11 => $"({d[..2]}) {d[2..7]}-{d[7..]}",
            10 => $"({d[..2]}) {d[2..6]}-{d[6..]}",
            _ => texto?.Trim() ?? string.Empty
        };
    }

    public static string FormatarCep(string? digitos) =>
        digitos?.Length == 8 ? $"{digitos[..5]}-{digitos[5..]}" : digitos ?? string.Empty;

    public static bool EmailValido(string email)
    {
        int arroba = email.IndexOf('@');
        return arroba > 0 && arroba == email.LastIndexOf('@') && email.IndexOf('.', arroba) > arroba + 1
            && !email.EndsWith('.') && !email.Contains(' ');
    }

    private static int DigitoVerificador(string base_, int pesoInicial)
    {
        int soma = 0;
        for (int i = 0; i < base_.Length; i++)
        {
            soma += (base_[i] - '0') * (pesoInicial - i);
        }

        int resto = soma * 10 % 11;
        return resto == 10 ? 0 : resto;
    }

    private static int DigitoCnpj(string base_, int[] pesos)
    {
        int soma = 0;
        for (int i = 0; i < base_.Length; i++)
        {
            soma += (base_[i] - '0') * pesos[i];
        }

        int resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
