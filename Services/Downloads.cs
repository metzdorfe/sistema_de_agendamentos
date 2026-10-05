using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace EspacoFestas.Services;

/// <summary>
/// Arquivos gerados pelo sistema (relatórios, catálogo) vão direto para a pasta Downloads do usuário, como num navegador:
/// sem a janela "Salvar como", com nome automático e sem sobrescrever (ganha " (2)", " (3)"...).
/// </summary>
public static class Downloads
{
    private static readonly Guid PastaDownloadsId = new("374DE290-123F-4565-9164-39C4925E467B");

    /// <summary>Pasta Downloads do Windows (respeita se o usuário a moveu); sem ela, %USERPROFILE%\Downloads.</summary>
    public static string Pasta
    {
        get
        {
            try
            {
                if (SHGetKnownFolderPath(PastaDownloadsId, 0, IntPtr.Zero, out var caminho) == 0 && !string.IsNullOrWhiteSpace(caminho))
                {
                    return caminho;
                }
            }
            catch (Exception)
            {
                // Sem a API (ou falhou): usa o padrão abaixo
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
    }

    /// <summary>Caminho livre em Downloads: "Nome - 04-10-2026.pdf", ou "Nome - 04-10-2026 (2).pdf" se já existir.</summary>
    public static string NovoCaminho(string nome, string extensao)
    {
        var pasta = Pasta;
        Directory.CreateDirectory(pasta);
        var baseNome = $"{string.Join("_", nome.Split(Path.GetInvalidFileNameChars()))} - {DateTime.Today:dd-MM-yyyy}";
        var caminho = Path.Combine(pasta, $"{baseNome}.{extensao}");
        for (int i = 2; File.Exists(caminho); i++)
        {
            caminho = Path.Combine(pasta, $"{baseNome} ({i}).{extensao}");
        }

        return caminho;
    }

    public static void Abrir(string caminho) => Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });

    /// <summary>Abre o Explorer na pasta, com o arquivo selecionado.</summary>
    public static void MostrarNaPasta(string caminho) => Process.Start("explorer.exe", $"/select,\"{caminho}\"");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token, out string caminho);
}
