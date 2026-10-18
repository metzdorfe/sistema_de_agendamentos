using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EspacoFestas.Models;
using EspacoFestas.Services;
using EspacoFestas.Services.Whatsapp;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Views
{
    /// <summary>
    /// Envio pelo WhatsApp com prévia: orçamento/agendamento (RF29, texto com o resumo) ou catálogo (RF28, PDF anexo).
    /// Ela revisa o texto e confirma; sem conexão, "Copiar texto" serve de alternativa. Falha fica no histórico para reenviar.
    /// </summary>
    public partial class EnvioWhatsappWindow : Window
    {
        private readonly TipoMensagem _tipo;
        private readonly int? _agendamentoId;
        private readonly int? _servicoId;
        private Cliente? _cliente;
        private string? _motivo;
        private bool _conectado;
        private bool _enviando;
        /// <summary>Ela mexeu no texto: trocar de cliente não reescreve mais a mensagem.</summary>
        private bool _editado;
        private bool _preenchendo;

        /// <summary>Verdadeiro se a mensagem foi enviada.</summary>
        public bool Enviou { get; private set; }

        private EnvioWhatsappWindow(TipoMensagem tipo, int? agendamentoId, int? servicoId)
        {
            InitializeComponent();
            _tipo = tipo;
            _agendamentoId = agendamentoId;
            _servicoId = servicoId;
            Loaded += async (_, _) =>
            {
                TemaService.AjustarBarraTitulo(this);
                await AtualizarEstadoAsync();
            };
        }

        /// <summary>Resumo do orçamento/agendamento para o cliente dele (RF29).</summary>
        public static EnvioWhatsappWindow Orcamento(int agendamentoId)
        {
            var janela = new EnvioWhatsappWindow(TipoMensagem.Orcamento, agendamentoId, null);
            using var contexto = App.CriarContexto();
            var agendamento = contexto.Agendamentos.AsNoTracking().Include(a => a.Cliente).Single(a => a.Id == agendamentoId);
            janela.TxtSubtitulo.Text = $"{Formatos.NomeStatus(agendamento.Status)} · {agendamento.Titulo} · {Formatos.Data(agendamento.DataEvento)}";
            janela.CmbCliente.Visibility = Visibility.Collapsed;
            janela.TxtCliente.Visibility = Visibility.Visible;
            janela.TxtCliente.Text = agendamento.Cliente!.Nome;
            janela.DefinirCliente(agendamento.Cliente);
            janela.Preencher(WhatsappMensagens.Preencher(WhatsappMensagens.TextoModelo(TipoMensagem.Orcamento),
                WhatsappMensagens.VariaveisAgendamento(contexto, agendamentoId)));
            return janela;
        }

        /// <summary>Catálogo em PDF (RF28); sem cliente, ela escolhe na lista.</summary>
        /// <param name="servicoId">Um serviço só; nulo = todos os ativos.</param>
        public static EnvioWhatsappWindow Catalogo(int? clienteId = null, int? servicoId = null)
        {
            var janela = new EnvioWhatsappWindow(TipoMensagem.Catalogo, null, servicoId);
            janela.TxtSubtitulo.Text = "Catálogo de serviços";
            janela.TxtAnexo.Visibility = Visibility.Visible;
            using var contexto = App.CriarContexto();
            var clientes = contexto.Clientes.AsNoTracking().Where(c => c.Ativo || c.Id == clienteId).OrderBy(c => c.Nome).ToList();
            janela.CmbCliente.ItemsSource = clientes.Select(c => new ClienteItem(c)).ToList();
            janela.CmbCliente.SelectedItem = ((List<ClienteItem>)janela.CmbCliente.ItemsSource).FirstOrDefault(c => c.Cliente.Id == clienteId);
            if (janela._cliente is null)
            {
                janela.TxtTelefone.Text = "Escolha o cliente.";
                janela.Preencher(WhatsappMensagens.TextoModelo(TipoMensagem.Catalogo));
            }

            return janela;
        }

        private void DefinirCliente(Cliente cliente)
        {
            _cliente = cliente;
            _motivo = WhatsappMensagens.MotivoNaoEnvia(cliente);
            TxtTelefone.Text = $"WhatsApp: {Documentos.FormatarTelefone(cliente.Telefone)}";
            TxtMotivo.Text = _motivo;
            TxtMotivo.Visibility = _motivo is null ? Visibility.Collapsed : Visibility.Visible;
            AtualizarBotao();
        }

        private void Preencher(string texto)
        {
            _preenchendo = true;
            TxtMensagem.Text = texto;
            _preenchendo = false;
        }

        private void CmbCliente_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbCliente.SelectedItem is not ClienteItem item)
            {
                return;
            }

            DefinirCliente(item.Cliente);
            if (!_editado)
            {
                Preencher(WhatsappMensagens.Preencher(WhatsappMensagens.TextoModelo(TipoMensagem.Catalogo),
                    WhatsappMensagens.VariaveisCliente(item.Cliente)));
            }
        }

        private void TxtMensagem_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_preenchendo)
            {
                _editado = true;
                TxtErro.Visibility = Visibility.Collapsed;
            }

            AtualizarBotao();
        }

        private async Task AtualizarEstadoAsync()
        {
            if (!App.Sistema.WhatsappAtivo)
            {
                MostrarEstado(false, "O WhatsApp está desligado. Ligue em Configurações → WhatsApp, ou use \"Copiar texto\".");
                return;
            }

            var status = await WhatsappServidor.Atual.ObterStatusAsync();
            MostrarEstado(status.EstaConectado, status.EstaConectado
                ? $"{status.Descricao}. A mensagem sai deste número."
                : $"{status.Descricao}. Conecte em Configurações → WhatsApp, ou use \"Copiar texto\".");
        }

        private void MostrarEstado(bool conectado, string texto)
        {
            _conectado = conectado;
            TxtEstado.Text = texto;
            CaixaEstado.SetResourceReference(StyleProperty, conectado ? "SeloSucesso" : "Aviso");
            AtualizarBotao();
        }

        private void AtualizarBotao()
        {
            string? impedimento = _enviando ? "Enviando..."
                : !_conectado ? "WhatsApp não conectado"
                : _cliente is null ? "Escolha o cliente"
                : _motivo is not null ? _motivo
                : string.IsNullOrWhiteSpace(TxtMensagem.Text) ? "Escreva a mensagem"
                : null;
            BtnEnviar.IsEnabled = impedimento is null;
            BtnEnviar.ToolTip = impedimento;
        }

        private async void BtnEnviar_Click(object sender, RoutedEventArgs e)
        {
            if (_cliente is null || _motivo is not null)
            {
                return;
            }

            _enviando = true;
            TxtErro.Visibility = Visibility.Collapsed;
            TxtEnviando.Visibility = Visibility.Visible;
            BtnCancelar.IsEnabled = TxtMensagem.IsEnabled = CmbCliente.IsEnabled = false;
            AtualizarBotao();
            try
            {
                string? anexo = null;
                if (_tipo == TipoMensagem.Catalogo)
                {
                    anexo = await Task.Run(() => WhatsappMensagens.GerarCatalogo(_servicoId));
                }

                var resultado = await WhatsappMensagens.EnviarAsync(
                    new NovaMensagem(_tipo, _cliente.Id, _agendamentoId, _cliente.Telefone, TxtMensagem.Text, anexo), WhatsappServidor.Atual);
                if (resultado.Ok)
                {
                    Enviou = true;
                    DialogResult = true;
                    return;
                }

                MostrarErro($"{WhatsappMensagens.DescreverFalha(resultado)} A mensagem ficou em Configurações → WhatsApp → Envios para reenviar.");
            }
            catch (IOException ex)
            {
                MostrarErro($"Não foi possível gerar o catálogo: {ex.Message}");
            }
            finally
            {
                _enviando = false;
                TxtEnviando.Visibility = Visibility.Collapsed;
                if (!Enviou)
                {
                    BtnCancelar.IsEnabled = TxtMensagem.IsEnabled = CmbCliente.IsEnabled = true;
                    AtualizarBotao();
                }
            }
        }

        private void MostrarErro(string texto)
        {
            TxtErro.Text = texto;
            TxtErro.Visibility = Visibility.Visible;
        }

        private void BtnCopiar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TxtMensagem.Text);
                BtnCopiar.Content = "Copiado";
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                MostrarErro("Não foi possível copiar agora. Tente de novo.");
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            if (!_enviando)
            {
                DialogResult = false;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !_enviando && !CmbCliente.IsDropDownOpen)
            {
                e.Handled = true;
                DialogResult = false;
            }
        }

        private void Cabecalho_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not Button && e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        /// <summary>ToString: o ComboBox do sistema mostra o item selecionado pelo ToString.</summary>
        private sealed record ClienteItem(Cliente Cliente)
        {
            public override string ToString() => Cliente.Nome;
        }
    }
}
