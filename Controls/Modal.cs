using System.Windows;
using System.Windows.Media.Animation;

namespace EspacoFestas.Controls;

/// <summary>Janela modal (popup) por cima de outra, com o fundo da janela de trás escurecido.</summary>
public static class Modal
{
    /// <param name="overlay">Elemento semitransparente da janela de trás (cor CorOverlay), mostrado enquanto o popup está aberto.</param>
    public static bool? Mostrar(Window janela, Window dono, UIElement overlay)
    {
        janela.Owner = dono;

        // Nunca maior que a janela de trás (tamanho mínimo do sistema: 1024×640)
        if (dono.ActualHeight > 0 && !double.IsNaN(janela.Height))
        {
            janela.Height = Math.Min(janela.Height, dono.ActualHeight - 32);
        }

        if (dono.ActualWidth > 0 && !double.IsNaN(janela.Width))
        {
            janela.Width = Math.Min(janela.Width, dono.ActualWidth - 32);
        }

        overlay.Visibility = Visibility.Visible;
        overlay.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        try
        {
            return janela.ShowDialog();
        }
        finally
        {
            overlay.Visibility = Visibility.Collapsed;
        }
    }
}
