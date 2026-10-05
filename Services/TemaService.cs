using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using EspacoFestas.Models;
using Microsoft.Win32;

namespace EspacoFestas.Services;

/// <summary>
/// Troca o dicionário de cores (Themes/Dark.xaml ou Themes/Light.xaml) em tempo de execução.
/// As telas usam {DynamicResource ...}, então a troca vale na hora, sem reiniciar.
/// </summary>
public static class TemaService
{
    private const string ChaveRegistro = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const int DwmaUsarModoEscuro = 20;

    private static Tema _preferencia = Tema.Claro;

    public static bool EscuroAtivo { get; private set; }

    public static void Aplicar(Tema preferencia)
    {
        _preferencia = preferencia;
        bool escuro = preferencia switch
        {
            Tema.Escuro => true,
            Tema.SeguirWindows => !WindowsUsaTemaClaro(),
            _ => false
        };

        var dicionarios = Application.Current.Resources.MergedDictionaries;
        var novo = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{(escuro ? "Dark" : "Light")}.xaml")
        };
        dicionarios[0] = novo;
        EscuroAtivo = escuro;

        foreach (Window janela in Application.Current.Windows)
        {
            AjustarBarraTitulo(janela);
        }
    }

    /// <summary>Com "Seguir o Windows", acompanha a troca de tema feita no sistema com o app aberto.</summary>
    public static void AcompanharWindows()
    {
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && _preferencia == Tema.SeguirWindows)
            {
                Application.Current.Dispatcher.Invoke(() => Aplicar(Tema.SeguirWindows));
            }
        };
    }

    /// <summary>Deixa a barra de título da janela escura ou clara conforme o tema (Windows 10 20H1+).</summary>
    public static void AjustarBarraTitulo(Window janela)
    {
        var handle = new WindowInteropHelper(janela).Handle;
        if (handle == IntPtr.Zero)
        {
            janela.SourceInitialized += (_, _) => AjustarBarraTitulo(janela);
            return;
        }

        int valor = EscuroAtivo ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, DwmaUsarModoEscuro, ref valor, sizeof(int));

        // Força o Windows a redesenhar a barra de título já aberta
        if (janela.IsVisible && janela.WindowState == WindowState.Normal)
        {
            janela.Width += 1;
            janela.Width -= 1;
        }
    }

    private static bool WindowsUsaTemaClaro()
    {
        using var chave = Registry.CurrentUser.OpenSubKey(ChaveRegistro);
        return chave?.GetValue("AppsUseLightTheme") is int valor && valor == 1;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int atributo, ref int valor, int tamanho);
}
