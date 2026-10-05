using System.Windows;

namespace EspacoFestas.Controls;

/// <summary>Propriedade herdada que indica a navbar recolhida (só ícones), para os templates dos itens.</summary>
public static class Navbar
{
    public static readonly DependencyProperty RecolhidaProperty =
        DependencyProperty.RegisterAttached("Recolhida", typeof(bool), typeof(Navbar),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetRecolhida(DependencyObject elemento) => (bool)elemento.GetValue(RecolhidaProperty);

    public static void SetRecolhida(DependencyObject elemento, bool valor) => elemento.SetValue(RecolhidaProperty, valor);
}
