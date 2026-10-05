using System.Windows;
using System.Windows.Media;

namespace EspacoFestas.Controls;

/// <summary>Propriedades auxiliares dos estilos (Themes/Estilos.xaml).</summary>
public static class Estilo
{
    /// <summary>Cor que aparece por cima do controle no hover (com fade).</summary>
    public static readonly DependencyProperty CorHoverProperty =
        DependencyProperty.RegisterAttached("CorHover", typeof(Brush), typeof(Estilo), new PropertyMetadata(null));

    public static Brush? GetCorHover(DependencyObject elemento) => (Brush?)elemento.GetValue(CorHoverProperty);

    public static void SetCorHover(DependencyObject elemento, Brush? valor) => elemento.SetValue(CorHoverProperty, valor);

    /// <summary>Ícone opcional mostrado à esquerda do texto (botões, campos de busca, itens da navbar).</summary>
    public static readonly DependencyProperty IconeProperty =
        DependencyProperty.RegisterAttached("Icone", typeof(Geometry), typeof(Estilo), new PropertyMetadata(null));

    public static Geometry? GetIcone(DependencyObject elemento) => (Geometry?)elemento.GetValue(IconeProperty);

    public static void SetIcone(DependencyObject elemento, Geometry? valor) => elemento.SetValue(IconeProperty, valor);

    /// <summary>Campo com erro de validação: borda vermelha (a mensagem curta fica num TextBlock logo abaixo).</summary>
    public static readonly DependencyProperty ErroProperty =
        DependencyProperty.RegisterAttached("Erro", typeof(bool), typeof(Estilo), new PropertyMetadata(false));

    public static bool GetErro(DependencyObject elemento) => (bool)elemento.GetValue(ErroProperty);

    public static void SetErro(DependencyObject elemento, bool valor) => elemento.SetValue(ErroProperty, valor);
}
