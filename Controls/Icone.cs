using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EspacoFestas.Controls;

/// <summary>Ícone de traço fino (Lucide, grade 24x24). Cor = Foreground; tamanho = Width/Height.</summary>
public class Icone : Control
{
    public static readonly DependencyProperty DadosProperty =
        DependencyProperty.Register(nameof(Dados), typeof(Geometry), typeof(Icone));

    static Icone()
    {
        FocusableProperty.OverrideMetadata(typeof(Icone), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Icone), new FrameworkPropertyMetadata(false));
    }

    public Geometry? Dados
    {
        get => (Geometry?)GetValue(DadosProperty);
        set => SetValue(DadosProperty, value);
    }
}
