using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using EspacoFestas.Models;
using EspacoFestas.Services;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Views
{
    /// <summary>
    /// Tela de um serviço: dados, faixas de preço (ordem por arrastar) e adicionais (RF02, RF03).
    /// No banco os adicionais continuam N:N (SERVICO_ADICIONAL); a tela só esconde a tabela de ligação.
    /// </summary>
    public partial class ServicoView : UserControl
    {
        private int? _id;
        private int? _faixaEditando;
        private int? _adicionalEditando;
        private List<FaixaItem> _faixas = [];

        public ServicoView(int? id)
        {
            InitializeComponent();
            _id = id;
            Carregar();
        }

        private void Carregar()
        {
            bool salvo = _id is not null;
            AvisoNovo.Visibility = salvo ? Visibility.Collapsed : Visibility.Visible;
            CartaoFaixas.Visibility = CartaoAdicionais.Visibility = salvo ? Visibility.Visible : Visibility.Collapsed;
            if (!salvo)
            {
                return;
            }

            using var contexto = App.CriarContexto();
            var servico = contexto.Servicos.AsNoTracking().Single(s => s.Id == _id);
            TxtTitulo.Text = TxtTrilha.Text = servico.Nome;
            TxtNome.Text = servico.Nome;
            TxtDescricao.Text = servico.Descricao;
            ChkAtivo.IsChecked = servico.Ativo;

            CarregarFaixas(contexto);
            CarregarAdicionais(contexto);
        }

        // ------------------------------------------------------------------
        // Serviço
        // ------------------------------------------------------------------

        private void BtnSalvarServico_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNome.Text))
            {
                TxtMensagemServico.Text = "Informe o nome do serviço.";
                return;
            }

            using var contexto = App.CriarContexto();
            Servico servico;
            if (_id is null)
            {
                servico = new Servico();
                contexto.Servicos.Add(servico);
            }
            else
            {
                servico = contexto.Servicos.Single(s => s.Id == _id);
            }

            servico.Nome = TxtNome.Text.Trim();
            servico.Descricao = string.IsNullOrWhiteSpace(TxtDescricao.Text) ? null : TxtDescricao.Text.Trim();
            servico.Ativo = ChkAtivo.IsChecked == true;
            contexto.SaveChanges();

            bool novo = _id is null;
            _id = servico.Id;
            TxtMensagemServico.Text = string.Empty;
            Carregar();
            if (novo)
            {
                BtnNovaFaixa_Click(this, e);
            }
        }

        private void BtnVoltar_Click(object sender, RoutedEventArgs e) => MainWindow.Atual.Navegar(new ServicosView());

        // ------------------------------------------------------------------
        // Faixas de preço
        // ------------------------------------------------------------------

        private void CarregarFaixas(Data.EspacoFestasContext contexto)
        {
            _faixas = contexto.ServicoFaixas.AsNoTracking()
                .Where(f => f.ServicoId == _id)
                .OrderBy(f => f.OrdemExibicao).ThenBy(f => f.Id)
                .AsEnumerable()
                .Select(f => new FaixaItem(f.Id, f.Nome, f.Valor, Formatos.Moeda(f.Valor), f.Ativo))
                .ToList();
            ListaFaixas.ItemsSource = _faixas;
            VazioFaixas.Visibility = _faixas.Any(f => f.Ativo) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void RecarregarFaixas()
        {
            using var contexto = App.CriarContexto();
            CarregarFaixas(contexto);
        }

        private void BtnNovaFaixa_Click(object sender, RoutedEventArgs e) => AbrirEditorFaixa(null);

        private void BtnEditarFaixa_Click(object sender, RoutedEventArgs e) =>
            AbrirEditorFaixa((FaixaItem)((FrameworkElement)sender).DataContext);

        private void AbrirEditorFaixa(FaixaItem? faixa)
        {
            _faixaEditando = faixa?.Id;
            TxtFaixaNome.Text = faixa?.Nome ?? string.Empty;
            TxtFaixaValor.Text = faixa is null ? string.Empty : Formatos.Decimal(faixa.Valor);
            TxtMensagemFaixa.Visibility = Visibility.Collapsed;
            EditorFaixa.Visibility = Visibility.Visible;
            TxtFaixaNome.Focus();
        }

        private void BtnCancelarFaixa_Click(object sender, RoutedEventArgs e) => EditorFaixa.Visibility = Visibility.Collapsed;

        private void BtnSalvarFaixa_Click(object sender, RoutedEventArgs e)
        {
            string? erro = null;
            if (string.IsNullOrWhiteSpace(TxtFaixaNome.Text))
            {
                erro = "Informe o nome da faixa.";
            }
            else if (!Formatos.TentarLerDecimal(TxtFaixaValor.Text, out var valorLido) || valorLido < 0)
            {
                erro = "Valor inválido.";
            }

            if (erro is not null)
            {
                TxtMensagemFaixa.Text = erro;
                TxtMensagemFaixa.Visibility = Visibility.Visible;
                return;
            }

            Formatos.TentarLerDecimal(TxtFaixaValor.Text, out var valor);
            using var contexto = App.CriarContexto();
            ServicoFaixa faixa;
            if (_faixaEditando is null)
            {
                short proxima = (short)(contexto.ServicoFaixas.Where(f => f.ServicoId == _id).Max(f => (short?)f.OrdemExibicao) + 1 ?? 0);
                faixa = new ServicoFaixa { ServicoId = _id!.Value, OrdemExibicao = proxima };
                contexto.ServicoFaixas.Add(faixa);
            }
            else
            {
                faixa = contexto.ServicoFaixas.Single(f => f.Id == _faixaEditando);
            }

            faixa.Nome = TxtFaixaNome.Text.Trim();
            faixa.Valor = valor;
            contexto.SaveChanges();

            EditorFaixa.Visibility = Visibility.Collapsed;
            CarregarFaixas(contexto);
        }

        private void ChkFaixaAtiva_Click(object sender, RoutedEventArgs e)
        {
            var item = (FaixaItem)((FrameworkElement)sender).DataContext;
            using var contexto = App.CriarContexto();
            var faixa = contexto.ServicoFaixas.Single(f => f.Id == item.Id);
            faixa.Ativo = !faixa.Ativo;
            contexto.SaveChanges();
            CarregarFaixas(contexto);
        }

        private void AlcaFaixa_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = (FaixaItem)((FrameworkElement)sender).DataContext;
            DragDrop.DoDragDrop((DependencyObject)sender, item, DragDropEffects.Move);
        }

        private void ListaFaixas_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(typeof(FaixaItem)) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void ListaFaixas_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(FaixaItem)) is not FaixaItem arrastada
                || e.OriginalSource is not FrameworkElement { DataContext: FaixaItem destino }
                || arrastada.Id == destino.Id)
            {
                return;
            }

            // A faixa arrastada ocupa a posição da faixa onde foi solta
            var ordem = _faixas.ToList();
            int de = ordem.FindIndex(f => f.Id == arrastada.Id);
            int para = ordem.FindIndex(f => f.Id == destino.Id);
            ordem.RemoveAt(de);
            ordem.Insert(para, arrastada);

            using var contexto = App.CriarContexto();
            var faixas = contexto.ServicoFaixas.Where(f => f.ServicoId == _id).ToList();
            for (short i = 0; i < ordem.Count; i++)
            {
                faixas.Single(f => f.Id == ordem[i].Id).OrdemExibicao = i;
            }

            contexto.SaveChanges();
            CarregarFaixas(contexto);
        }

        // ------------------------------------------------------------------
        // Adicionais
        // ------------------------------------------------------------------

        private void CarregarAdicionais(Data.EspacoFestasContext contexto)
        {
            var itens = contexto.ServicoAdicionais.AsNoTracking()
                .Where(sa => sa.ServicoId == _id)
                .Select(sa => new
                {
                    sa.Adicional!.Id,
                    sa.Adicional.Nome,
                    sa.Adicional.ValorVenda,
                    sa.Adicional.ValorCusto,
                    sa.Adicional.CobraPorQuantidade,
                    sa.Adicional.Ativo,
                    Outros = sa.Adicional.ServicoAdicionais.Where(o => o.ServicoId != _id).Select(o => o.Servico!.Nome).ToList()
                })
                .OrderBy(a => a.Nome)
                .AsEnumerable()
                .Select(a => new AdicionalItem(a.Id, a.Nome,
                    Formatos.Moeda(a.ValorVenda) + (a.CobraPorQuantidade ? " / un" : string.Empty),
                    $"custo {Formatos.Moeda(a.ValorCusto)}", a.Ativo,
                    a.Outros.Count == 0 ? string.Empty : $"Também usado em: {string.Join(", ", a.Outros)}"))
                .ToList();

            ListaAdicionais.ItemsSource = itens;
            VazioAdicionais.Visibility = itens.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnNovoAdicional_Click(object sender, RoutedEventArgs e)
        {
            _adicionalEditando = null;
            TxtTituloAdicional.Text = "Novo adicional";
            TxtAdicionalNome.Text = TxtAdicionalDescricao.Text = TxtAdicionalVenda.Text = TxtAdicionalCusto.Text = string.Empty;
            ChkPorQuantidade.IsChecked = false;
            ChkAdicionalAtivo.IsChecked = true;
            AvisoCompartilhado.Visibility = Visibility.Collapsed;
            AbrirPainel();
        }

        private void BtnEditarAdicional_Click(object sender, RoutedEventArgs e)
        {
            var item = (AdicionalItem)((FrameworkElement)sender).DataContext;
            using var contexto = App.CriarContexto();
            var adicional = contexto.Adicionais.AsNoTracking().Single(a => a.Id == item.Id);

            _adicionalEditando = adicional.Id;
            TxtTituloAdicional.Text = "Editar adicional";
            TxtAdicionalNome.Text = adicional.Nome;
            TxtAdicionalDescricao.Text = adicional.Descricao;
            TxtAdicionalVenda.Text = Formatos.Decimal(adicional.ValorVenda);
            TxtAdicionalCusto.Text = Formatos.Decimal(adicional.ValorCusto);
            ChkPorQuantidade.IsChecked = adicional.CobraPorQuantidade;
            ChkAdicionalAtivo.IsChecked = adicional.Ativo;

            // Mudança de preço vale para todos os serviços que usam o adicional
            bool compartilhado = item.OutrosServicos.Length > 0;
            AvisoCompartilhado.Visibility = compartilhado ? Visibility.Visible : Visibility.Collapsed;
            TxtAvisoCompartilhado.Text = compartilhado
                ? $"{item.OutrosServicos.Replace("Também usado em", "Este adicional também é usado em")}. As alterações valem para todos."
                : string.Empty;
            AbrirPainel();
        }

        private void BtnSalvarAdicional_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtAdicionalNome.Text))
            {
                TxtMensagemAdicional.Text = "Informe o nome do adicional.";
                return;
            }

            if (!Formatos.TentarLerDecimal(TxtAdicionalVenda.Text, out var venda) || venda < 0)
            {
                TxtMensagemAdicional.Text = "Valor de venda inválido.";
                return;
            }

            decimal custo = 0;
            if (TxtAdicionalCusto.Text.Trim().Length > 0 && (!Formatos.TentarLerDecimal(TxtAdicionalCusto.Text, out custo) || custo < 0))
            {
                TxtMensagemAdicional.Text = "Valor de custo inválido.";
                return;
            }

            using var contexto = App.CriarContexto();
            Adicional adicional;
            if (_adicionalEditando is null)
            {
                adicional = new Adicional();
                adicional.ServicoAdicionais.Add(new ServicoAdicional { ServicoId = _id!.Value });
                contexto.Adicionais.Add(adicional);
            }
            else
            {
                adicional = contexto.Adicionais.Single(a => a.Id == _adicionalEditando);
            }

            adicional.Nome = TxtAdicionalNome.Text.Trim();
            adicional.Descricao = string.IsNullOrWhiteSpace(TxtAdicionalDescricao.Text) ? null : TxtAdicionalDescricao.Text.Trim();
            adicional.ValorVenda = venda;
            adicional.ValorCusto = custo;
            adicional.CobraPorQuantidade = ChkPorQuantidade.IsChecked == true;
            adicional.Ativo = ChkAdicionalAtivo.IsChecked == true;
            contexto.SaveChanges();

            FecharPainel();
            CarregarAdicionais(contexto);
        }

        private void BtnDesvincular_Click(object sender, RoutedEventArgs e)
        {
            var item = (AdicionalItem)((FrameworkElement)sender).DataContext;
            if (!Dialogo.Confirmar("Remover adicional do serviço",
                    $"{item.Nome} deixa de ser oferecido em {TxtNome.Text.Trim()}. O cadastro do adicional não é apagado.", "Remover"))
            {
                return;
            }

            using var contexto = App.CriarContexto();
            var vinculo = contexto.ServicoAdicionais.Single(sa => sa.ServicoId == _id && sa.AdicionalId == item.Id);
            contexto.ServicoAdicionais.Remove(vinculo);
            contexto.SaveChanges();
            CarregarAdicionais(contexto);
        }

        private void BtnUsarExistente_Click(object sender, RoutedEventArgs e)
        {
            using var contexto = App.CriarContexto();
            var existentes = contexto.Adicionais.AsNoTracking()
                .Where(a => a.Ativo && !a.ServicoAdicionais.Any(sa => sa.ServicoId == _id))
                .OrderBy(a => a.Nome)
                .AsEnumerable()
                .Select(a => new AdicionalItem(a.Id, a.Nome, Formatos.Moeda(a.ValorVenda), string.Empty, a.Ativo, string.Empty))
                .ToList();

            ListaExistentes.ItemsSource = existentes;
            VazioExistentes.Visibility = existentes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            PopupExistentes.IsOpen = true;
        }

        private void ListaExistentes_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not FrameworkElement { DataContext: AdicionalItem item })
            {
                return;
            }

            PopupExistentes.IsOpen = false;
            using var contexto = App.CriarContexto();
            contexto.ServicoAdicionais.Add(new ServicoAdicional { ServicoId = _id!.Value, AdicionalId = item.Id });
            contexto.SaveChanges();
            CarregarAdicionais(contexto);
        }

        private void AbrirPainel()
        {
            TxtMensagemAdicional.Text = string.Empty;
            PainelAdicional.Width = Math.Min(420, Math.Max(300, ActualWidth - 64));
            PainelAdicional.Visibility = Sombra.Visibility = Visibility.Visible;
            DeslocamentoPainel.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
                new DoubleAnimation(PainelAdicional.Width, 0, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            TxtAdicionalNome.Focus();
        }

        private void FecharPainel() => PainelAdicional.Visibility = Sombra.Visibility = Visibility.Collapsed;

        private void BtnCancelarAdicional_Click(object sender, RoutedEventArgs e) => FecharPainel();

        private void Sombra_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => FecharPainel();

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                FecharPainel();
                EditorFaixa.Visibility = Visibility.Collapsed;
            }
        }

        public sealed record FaixaItem(int Id, string Nome, decimal Valor, string ValorTexto, bool Ativo);

        public sealed record AdicionalItem(int Id, string Nome, string VendaTexto, string CustoTexto, bool Ativo, string OutrosServicos);
    }
}
