using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EspacoFestas.Models;
using EspacoFestas.Services;
using EspacoFestas.Services.Whatsapp;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Views
{
    /// <summary>RF27: histórico dos envios pelo WhatsApp, com filtro, texto da mensagem e reenvio das falhas.</summary>
    public partial class EnviosWhatsappWindow : Window
    {
        public EnviosWhatsappWindow()
        {
            InitializeComponent();
            Carregar();
            Loaded += (_, _) => TemaService.AjustarBarraTitulo(this);
        }

        /// <summary>Últimas 200 mensagens, mais recentes primeiro, com o filtro de situação.</summary>
        private void Carregar()
        {
            var selecionada = (Grade.SelectedItem as MensagemLinha)?.Id;
            using var contexto = App.CriarContexto();
            var consulta = contexto.WhatsappMensagens.AsNoTracking();
            if (FiltroEnviadas.IsChecked == true)
            {
                consulta = consulta.Where(m => m.Status == StatusMensagem.Enviada);
            }
            else if (FiltroFalhas.IsChecked == true)
            {
                consulta = consulta.Where(m => m.Status == StatusMensagem.Falha);
            }

            var linhas = consulta
                .OrderByDescending(m => m.DataCriacao)
                .Take(200)
                .Select(m => new { m.Id, m.DataCriacao, m.DataEnvio, Cliente = m.Cliente != null ? m.Cliente.Nome : null, m.Telefone, m.Tipo, m.Status, m.Texto, m.MensagemErro })
                .AsEnumerable()
                .Select(m => new MensagemLinha(
                    m.Id,
                    (m.DataEnvio ?? m.DataCriacao).ToString("dd/MM/yyyy HH:mm"),
                    m.Cliente ?? Documentos.FormatarTelefone(m.Telefone[2..]),
                    m.Tipo switch
                    {
                        TipoMensagem.Catalogo => "Catálogo",
                        TipoMensagem.Orcamento => "Orçamento",
                        TipoMensagem.LembreteEvento => "Lembrete",
                        TipoMensagem.CobrancaParcela => "Cobrança",
                        _ => "Avulsa"
                    },
                    m.Status switch
                    {
                        StatusMensagem.Enviada => "Enviada",
                        StatusMensagem.Falha => "Falha",
                        StatusMensagem.Cancelada => "Cancelada",
                        _ => "Pendente"
                    },
                    m.Texto,
                    m.MensagemErro))
                .ToList();

            Grade.ItemsSource = linhas;
            Grade.SelectedItem = linhas.FirstOrDefault(l => l.Id == selecionada);
            Vazio.Visibility = linhas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            Grade.Visibility = linhas.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            TxtResumo.Text = $"Hoje: {WhatsappMensagens.EnviadasHoje(contexto)} de {App.Sistema.WhatsappLimiteDiario} mensagens enviadas";
            AtualizarSelecao();
        }

        private void AtualizarSelecao()
        {
            var linha = Grade.SelectedItem as MensagemLinha;
            TxtTexto.Text = linha?.Texto ?? "Selecione uma mensagem para ver o texto.";
            TxtTexto.SetResourceReference(TextBlock.ForegroundProperty, linha is null ? "CorTextoSecundario" : "CorTexto");
            bool falha = linha is { Status: "Falha" };
            BtnReenviar.IsEnabled = falha;
            BtnReenviar.ToolTip = falha ? "Tentar enviar de novo" : "Selecione uma mensagem com falha";
        }

        private void Grade_SelectionChanged(object sender, SelectionChangedEventArgs e) => AtualizarSelecao();

        private void Filtro_Click(object sender, RoutedEventArgs e) => Carregar();

        private async void BtnReenviar_Click(object sender, RoutedEventArgs e)
        {
            if (Grade.SelectedItem is not MensagemLinha linha)
            {
                return;
            }

            BtnReenviar.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
            WhatsappResultado resultado;
            try
            {
                resultado = await WhatsappMensagens.ReenviarAsync(linha.Id, WhatsappServidor.Atual);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            Carregar();
            if (!resultado.Ok)
            {
                Dialogo.Avisar("Não foi possível reenviar", WhatsappMensagens.DescreverFalha(resultado));
            }
        }

        private void BtnFechar_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
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

        public sealed record MensagemLinha(int Id, string Data, string Cliente, string Tipo, string Status, string Texto, string? Erro);
    }
}
