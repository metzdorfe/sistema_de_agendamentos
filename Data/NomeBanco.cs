using System.Text;

namespace EspacoFestas.Data;

/// <summary>Converte identificadores PascalCase do C# para MAIÚSCULO_COM_UNDERSCORE do banco (ver stack-projeto.md).</summary>
internal static class NomeBanco
{
    public static string Converter(string nome)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < nome.Length; i++)
        {
            char c = nome[i];
            if (char.IsUpper(c) && i > 0 && !char.IsUpper(nome[i - 1]))
            {
                sb.Append('_');
            }
            sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }
}
