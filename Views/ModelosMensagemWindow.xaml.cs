using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EspacoFestas.Controls;
using EspacoFestas.Models;
using EspacoFestas.Services;
using EspacoFestas.Services.Whatsapp;

namespace EspacoFestas.Views
{
    /// <summary>Edição das mensagens padrão do WhatsApp: lista à esquerda, editor com campos em etiqueta e prévia à direita.</summary>
    public partial class ModelosMensagemWindow : Window
    {
        private static readonly ModeloItem[] Modelos =
        [
            new(TipoMensagem.Catalogo, "Catálogo", "Vai junto com o PDF dos serviços"),
            new(TipoMensagem.Orcamento, "Orçamento / agendamento", "Resumo do evento para o cliente")
        ];

        private ModeloItem? _atual;
        /// <summary>Texto como está gravado, para saber se há alteração não salva.</summary>
        private string _gravado = string.Empty;
        private bool _trocando;

        public ModelosMensagemWindow()
        {
            InitializeComponent();
            ListaModelos.ItemsSource = Modelos;
            ListaModelos.SelectedIndex = 0;
            Loaded += (_, _) => TemaService.AjustarBarraTitulo(this);
        }

        private bool Alterado => _atual is not null && EditorCampos.Ler(Editor) != _gravado;

        private void ListaModelos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_trocando || ListaModelos.SelectedItem is not ModeloItem item || item == _atual)
            {
                return;
            }

            if (Alterado && !Dialogo.Confirmar("Descartar alterações?",
                    $"A mensagem \"{_atual!.Titulo}\" tem alterações não salvas.", "Descartar", perigo: true))
            {
                _trocando = true;
                ListaModelos.SelectedItem = _atual;
                _trocando = false;
                return;
            }

            _atual = item;
            _gravado = WhatsappMensagens.TextoModelo(item.Tipo).Replace("\r\n", "\n").TrimEnd();
            EditorCampos.Carregar(Editor, _gravado, WhatsappMensagens.Rotulos);

            PainelCampos.Children.Clear();
            foreach (var chave in WhatsappMensagens.VariaveisDoTipo(item.Tipo))
            {
                var rotulo = WhatsappMensagens.Rotulos[chave];
                var botao = new Button { Content = rotulo, ToolTip = $"Inserir \"{rotulo}\" no texto" };
                botao.SetResourceReference(StyleProperty, "BotaoCampo");
                botao.Click += (_, _) => EditorCampos.Inserir(Editor, chave, rotulo);
                PainelCampos.Children.Add(botao);
            }

            TxtStatus.Text = string.Empty;
            AtualizarPrevia();
        }

        private void Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded)
            {
                TxtStatus.Text = string.Empty;
                AtualizarPrevia();
            }
        }

        private void AtualizarPrevia()
        {
            var exemplo = new Dictionary<string, string>
            {
                ["cliente"] = "Maria",
                ["titulo"] = "Aniversário da Ana",
                ["status"] = "orçamento",
                ["data"] = "20/11/2026 (sexta-feira)",
                ["hora"] = "19:00 às 23:00",
                ["servicos"] = "Salão principal",
                ["valor"] = Formatos.Moeda(3500m),
                ["pago"] = Formatos.Moeda(500m),
                ["saldo"] = Formatos.Moeda(3000m),
                ["empresa"] = App.NomeEmpresa,
                ["telefone_empresa"] = Documentos.FormatarTelefone(App.Sistema.TelefoneEmpresa)
            };
            TxtPrevia.Text = WhatsappMensagens.Preencher(EditorCampos.Ler(Editor), exemplo);
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            if (_atual is null)
            {
                return;
            }

            var texto = EditorCampos.Ler(Editor);
            if (string.IsNullOrWhiteSpace(texto))
            {
                MostrarStatus("A mensagem precisa de texto.", erro: true);
                return;
            }

            WhatsappMensagens.SalvarModelo(_atual.Tipo, _atual.Titulo, texto);
            _gravado = texto;
            MostrarStatus("Mensagem salva.", erro: false);
        }

        private void BtnRestaurar_Click(object sender, RoutedEventArgs e)
        {
            if (_atual is null)
            {
                return;
            }

            EditorCampos.Carregar(Editor, WhatsappMensagens.TextoPadrao(_atual.Tipo), WhatsappMensagens.Rotulos);
            AtualizarPrevia();
            MostrarStatus("Texto padrão restaurado. Clique em Salvar mensagem para gravar.", erro: false);
        }

        private void MostrarStatus(string texto, bool erro)
        {
            TxtStatus.Text = texto;
            TxtStatus.SetResourceReference(TextBlock.ForegroundProperty, erro ? "CorErro" : "CorSucesso");
        }

        private void Fechar()
        {
            if (!Alterado || Dialogo.Confirmar("Descartar alterações?", "A mensagem tem alterações não salvas.", "Descartar", perigo: true))
            {
                DialogResult = false;
            }
        }

        private void BtnFechar_Click(object sender, RoutedEventArgs e) => Fechar();

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Fechar();
            }
        }

        private void Cabecalho_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not Button && e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        public sealed record ModeloItem(TipoMensagem Tipo, string Titulo, string Descricao);
    }
}
