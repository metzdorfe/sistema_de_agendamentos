using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace EspacoFestas.Controls;

/// <summary>
/// Editor de texto com campos (modelos do WhatsApp): no RichTextBox, cada {variavel} aparece como uma etiqueta colorida
/// com o nome amigável ("Cliente"), que se apaga inteira com um Backspace. Ao ler, as etiquetas voltam a ser {variavel},
/// que é o que fica gravado no banco.
/// </summary>
public static partial class EditorCampos
{
    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Variavel();

    /// <summary>Monta o documento a partir do texto gravado (uma linha por parágrafo).</summary>
    /// <param name="rotulos">Nome de cada variável; variável desconhecida fica como texto.</param>
    public static void Carregar(RichTextBox editor, string texto, IReadOnlyDictionary<string, string> rotulos)
    {
        var documento = new FlowDocument { PagePadding = new Thickness(0) };
        foreach (var linha in texto.Replace("\r\n", "\n").Split('\n'))
        {
            var paragrafo = new Paragraph { Margin = new Thickness(0), LineHeight = 22 };
            int inicio = 0;
            foreach (Match m in Variavel().Matches(linha))
            {
                if (!rotulos.TryGetValue(m.Groups[1].Value, out var rotulo))
                {
                    continue;
                }

                if (m.Index > inicio)
                {
                    paragrafo.Inlines.Add(new Run(linha[inicio..m.Index]));
                }

                paragrafo.Inlines.Add(new InlineUIContainer(Etiqueta(m.Groups[1].Value, rotulo)) { BaselineAlignment = BaselineAlignment.Center });
                inicio = m.Index + m.Length;
            }

            if (inicio < linha.Length)
            {
                paragrafo.Inlines.Add(new Run(linha[inicio..]));
            }

            documento.Blocks.Add(paragrafo);
        }

        editor.Document = documento;
    }

    /// <summary>Texto do editor com as etiquetas convertidas de volta para {variavel}.</summary>
    public static string Ler(RichTextBox editor)
    {
        var linhas = new List<string>();
        foreach (var bloco in editor.Document.Blocks)
        {
            var texto = new StringBuilder();
            if (bloco is Paragraph paragrafo)
            {
                Escrever(paragrafo.Inlines, texto);
            }
            else
            {
                texto.Append(new TextRange(bloco.ContentStart, bloco.ContentEnd).Text);
            }

            linhas.Add(texto.ToString());
        }

        return string.Join("\n", linhas).TrimEnd();
    }

    /// <summary>Insere a etiqueta na posição do cursor (substitui o que estiver selecionado).</summary>
    public static void Inserir(RichTextBox editor, string chave, string rotulo)
    {
        if (!editor.Selection.IsEmpty)
        {
            editor.Selection.Text = string.Empty;
        }

        var posicao = editor.CaretPosition.GetInsertionPosition(LogicalDirection.Forward);
        var etiqueta = new InlineUIContainer(Etiqueta(chave, rotulo), posicao) { BaselineAlignment = BaselineAlignment.Center };
        editor.CaretPosition = etiqueta.ElementEnd;
        editor.Focus();
    }

    /// <summary>A etiqueta colorida de um campo. Tag = nome da variável.</summary>
    public static Border Etiqueta(string chave, string rotulo)
    {
        var texto = new TextBlock { Text = rotulo, FontSize = 13, FontWeight = FontWeights.Medium };
        texto.SetResourceReference(TextBlock.ForegroundProperty, "CorDestaque");
        var etiqueta = new Border
        {
            Tag = chave,
            Child = texto,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(7, 1, 7, 2),
            Margin = new Thickness(1, 0, 1, 0),
            ToolTip = $"Campo preenchido automaticamente: {rotulo}"
        };
        etiqueta.SetResourceReference(Border.BackgroundProperty, "CorPrincipalFundo");
        return etiqueta;
    }

    private static void Escrever(InlineCollection inlines, StringBuilder texto)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    texto.Append(run.Text);
                    break;
                case LineBreak:
                    texto.Append('\n');
                    break;
                case InlineUIContainer { Child: FrameworkElement { Tag: string chave } }:
                    texto.Append('{').Append(chave).Append('}');
                    break;
                case Span span:
                    // Negrito, itálico ou texto colado com formatação: só o conteúdo importa
                    Escrever(span.Inlines, texto);
                    break;
            }
        }
    }
}
