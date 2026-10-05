using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using EspacoFestas.Services;

namespace EspacoFestas.Controls;

/// <summary>
/// Calendário em popup para campos de data com máscara dd/MM/yyyy. Desenhado com as cores do tema
/// (o Calendar padrão do WPF tem gradiente e não acompanha o tema escuro).
/// </summary>
public static class SeletorData
{
    private static readonly string[] DiasSemana = ["D", "S", "T", "Q", "Q", "S", "S"];

    /// <summary>Abre o calendário abaixo de <paramref name="alvo"/>; ao escolher, preenche o campo e devolve o foco a ele.</summary>
    /// <param name="aoEscolher">Chamado depois de preencher o campo (ex.: conferir a disponibilidade da data).</param>
    public static void Abrir(TextBox campo, FrameworkElement alvo, Action? aoEscolher = null)
    {
        var inicial = Mascara.LerData(campo.Text) ?? DateTime.Today;
        var mes = new DateTime(inicial.Year, inicial.Month, 1);

        var popup = new Popup
        {
            PlacementTarget = alvo,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            VerticalOffset = 4
        };

        var titulo = new TextBlock { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        titulo.SetResourceReference(TextBlock.ForegroundProperty, "CorTexto");
        var grade = new UniformGrid { Columns = 7, Rows = 7 };

        var anterior = BotaoSeta("IconeAnterior", "Mês anterior");
        var proximo = BotaoSeta("IconeProximo", "Próximo mês");
        anterior.Click += (_, _) => { mes = mes.AddMonths(-1); Montar(); };
        proximo.Click += (_, _) => { mes = mes.AddMonths(1); Montar(); };

        var cabecalho = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(anterior, Dock.Left);
        DockPanel.SetDock(proximo, Dock.Right);
        cabecalho.Children.Add(anterior);
        cabecalho.Children.Add(proximo);
        cabecalho.Children.Add(titulo);

        var conteudo = new StackPanel { Width = 7 * 36 };
        conteudo.Children.Add(cabecalho);
        conteudo.Children.Add(grade);

        var moldura = new Border
        {
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            Child = conteudo
        };
        moldura.SetResourceReference(Border.BackgroundProperty, "CorSuperficie");
        moldura.SetResourceReference(Border.BorderBrushProperty, "CorBorda");
        popup.Child = moldura;
        popup.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                popup.IsOpen = false;
                campo.Focus();
            }
        };

        Montar();
        popup.IsOpen = true;

        void Montar()
        {
            titulo.Text = Formatos.Capitalizar(mes.ToString("MMMM yyyy", Formatos.Brasil));
            grade.Children.Clear();
            foreach (var dia in DiasSemana)
            {
                var texto = new TextBlock { Text = dia, FontSize = 11, FontWeight = FontWeights.Medium, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                texto.SetResourceReference(TextBlock.ForegroundProperty, "CorTextoSecundario");
                grade.Children.Add(texto);
            }

            var primeiro = mes.AddDays(-(int)mes.DayOfWeek);
            for (int i = 0; i < 42; i++)
            {
                var data = primeiro.AddDays(i);
                if (i >= 35 && data.Month != mes.Month)
                {
                    break;
                }

                grade.Children.Add(BotaoDia(data, data.Month == mes.Month, data == inicial));
            }

            grade.Rows = (grade.Children.Count + 6) / 7;
        }

        Button BotaoDia(DateTime data, bool doMes, bool escolhido)
        {
            var botao = new Button
            {
                Content = data.Day.ToString(),
                Width = 36,
                Height = 32,
                MinHeight = 32,
                Padding = new Thickness(0),
                FontSize = 13,
                FontWeight = data == DateTime.Today ? FontWeights.Bold : FontWeights.Normal,
                Opacity = doMes ? 1 : 0.4
            };
            botao.SetResourceReference(FrameworkElement.StyleProperty, escolhido ? "BotaoPrincipal" : "BotaoFantasma");
            if (!escolhido)
            {
                botao.SetResourceReference(Control.ForegroundProperty, data == DateTime.Today ? "CorDestaque" : "CorTexto");
            }

            botao.Click += (_, _) =>
            {
                campo.Text = Formatos.Data(data);
                popup.IsOpen = false;
                campo.Focus();
                campo.CaretIndex = campo.Text.Length;
                aoEscolher?.Invoke();
            };
            return botao;
        }
    }

    private static Button BotaoSeta(string icone, string dica)
    {
        var botao = new Button { ToolTip = dica };
        botao.SetResourceReference(FrameworkElement.StyleProperty, "BotaoIcone");
        botao.SetResourceReference(Estilo.IconeProperty, icone);
        return botao;
    }
}
