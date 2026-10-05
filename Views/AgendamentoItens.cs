using System.ComponentModel;
using System.Runtime.CompilerServices;
using EspacoFestas.Services;

namespace EspacoFestas.Views
{
    /// <summary>Base mínima para as linhas da tela de agendamento avisarem a tela quando mudam.</summary>
    public abstract class Observavel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void Definir<T>(ref T campo, T valor, [CallerMemberName] string? nome = null)
        {
            if (EqualityComparer<T>.Default.Equals(campo, valor))
            {
                return;
            }

            campo = valor;
            Avisar(nome);
        }

        protected void Avisar([CallerMemberName] string? nome = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nome));
    }

    /// <summary>Faixa de preço oferecida como radio button (RN06).</summary>
    public sealed class FaixaOpcao(int id, string nome, decimal valor, bool ativa) : Observavel
    {
        private bool _selecionada;

        public int Id { get; } = id;
        public string Nome { get; } = ativa ? nome : $"{nome} (inativa)";
        public decimal Valor { get; } = valor;
        public string ValorTexto => Formatos.Moeda(Valor);

        public bool Selecionada
        {
            get => _selecionada;
            set => Definir(ref _selecionada, value);
        }
    }

    /// <summary>Um serviço na grade do agendamento: faixa escolhida + valor editável.</summary>
    public sealed class ServicoLinhaAgendamento : Observavel
    {
        private string _valorTexto = string.Empty;
        private string? _erro;

        public ServicoLinhaAgendamento(int servicoId, string nome, List<FaixaOpcao> faixas)
        {
            ServicoId = servicoId;
            Nome = nome;
            Faixas = faixas;
        }

        public int ServicoId { get; }
        public string Nome { get; }
        public List<FaixaOpcao> Faixas { get; }

        /// <summary>Nome do grupo dos radio buttons: um por serviço.</summary>
        public string Grupo => $"Faixas{ServicoId}";

        public FaixaOpcao? FaixaSelecionada => Faixas.FirstOrDefault(f => f.Selecionada);

        public string ValorTexto
        {
            get => _valorTexto;
            set => Definir(ref _valorTexto, value);
        }

        public string? Erro
        {
            get => _erro;
            set
            {
                Definir(ref _erro, value);
                Avisar(nameof(TemErro));
            }
        }

        public bool TemErro => !string.IsNullOrEmpty(_erro);

        public decimal? Valor => Formatos.TentarLerDecimal(ValorTexto, out var valor) && valor >= 0 ? valor : null;

        public void Escolher(FaixaOpcao faixa)
        {
            foreach (var f in Faixas)
            {
                f.Selecionada = f == faixa;
            }

            // RN06: o valor vem da faixa e pode ser editado
            ValorTexto = Formatos.Decimal(faixa.Valor);
            Erro = null;
            Avisar(nameof(FaixaSelecionada));
        }
    }

    /// <summary>Um adicional oferecido pelo(s) serviço(s) escolhido(s) (RN07).</summary>
    public sealed class AdicionalLinhaAgendamento : Observavel
    {
        private bool _selecionado;
        private string _quantidadeTexto = "1";
        private string _valorTexto;
        private bool _quantidadeErro;
        private bool _valorErro;

        public AdicionalLinhaAgendamento(int adicionalId, string nome, bool cobraPorQuantidade, decimal valor, decimal custo)
        {
            AdicionalId = adicionalId;
            Nome = nome;
            CobraPorQuantidade = cobraPorQuantidade;
            Custo = custo;
            _valorTexto = Formatos.Decimal(valor);
        }

        public int AdicionalId { get; }
        public string Nome { get; }
        public bool CobraPorQuantidade { get; }

        /// <summary>Custo unitário copiado do cadastro no momento do lançamento (RN08).</summary>
        public decimal Custo { get; }

        public string CustoTexto => CobraPorQuantidade ? $"custo {Formatos.Moeda(Custo)} / un" : $"custo {Formatos.Moeda(Custo)}";

        public bool Selecionado
        {
            get => _selecionado;
            set
            {
                Definir(ref _selecionado, value);
                Atualizar();
            }
        }

        public string QuantidadeTexto
        {
            get => _quantidadeTexto;
            set
            {
                Definir(ref _quantidadeTexto, value);
                Atualizar();
            }
        }

        public string ValorTexto
        {
            get => _valorTexto;
            set
            {
                Definir(ref _valorTexto, value);
                Atualizar();
            }
        }

        public bool QuantidadeErro
        {
            get => _quantidadeErro;
            private set => Definir(ref _quantidadeErro, value);
        }

        public bool ValorErro
        {
            get => _valorErro;
            private set => Definir(ref _valorErro, value);
        }

        /// <summary>RN04: só varia quando o adicional cobra por quantidade; senão fica 1.</summary>
        public short? Quantidade => !CobraPorQuantidade ? (short)1
            : short.TryParse(QuantidadeTexto.Trim(), out var qtd) && qtd > 0 ? qtd : null;

        public decimal? Valor => Formatos.TentarLerDecimal(ValorTexto, out var valor) && valor >= 0 ? valor : null;

        public decimal Total => Selecionado && Quantidade is { } q && Valor is { } v ? q * v : 0;

        public string TotalTexto => Selecionado ? Formatos.Moeda(Total) : "–";

        private void Atualizar()
        {
            QuantidadeErro = Selecionado && Quantidade is null;
            ValorErro = Selecionado && Valor is null;
            Avisar(nameof(Total));
            Avisar(nameof(TotalTexto));
        }
    }

    public sealed record ClienteOpcao(int Id, string Nome, string Telefone, string? CpfCnpj)
    {
        public string Detalhe => string.Join(" · ", new[] { Documentos.FormatarTelefone(Telefone), Documentos.FormatarCpfCnpj(CpfCnpj) }.Where(t => !string.IsNullOrEmpty(t)));
    }

    // ToString: o ComboBox mostra o item selecionado pelo ToString (o template do sistema não usa o DisplayMemberPath ali)
    public sealed record EspecieOpcao(int Id, string Descricao)
    {
        public override string ToString() => Descricao;
    }

    public sealed record ServicoOpcao(int Id, string Nome)
    {
        public override string ToString() => Nome;
    }
}
