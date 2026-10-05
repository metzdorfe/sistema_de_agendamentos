using System.Windows;
using System.Windows.Controls;
using EspacoFestas.Controls;
using EspacoFestas.Models;
using EspacoFestas.Services;

namespace EspacoFestas.Views
{
    /// <summary>Cadastro de cliente, pessoa física ou jurídica (RF01, RN01, RN02).</summary>
    public partial class ClienteFormView : UserControl
    {
        private static readonly string[] Ufs =
        [
            "", "AC", "AL", "AM", "AP", "BA", "CE", "DF", "ES", "GO", "MA", "MG", "MS", "MT", "PA",
            "PB", "PE", "PI", "PR", "RJ", "RN", "RO", "RR", "RS", "SC", "SE", "SP", "TO"
        ];

        private readonly int? _id;

        public ClienteFormView(int? id = null)
        {
            InitializeComponent();
            _id = id;
            CmbUf.ItemsSource = Ufs;
            CmbUf.SelectedItem = string.Empty;

            if (id is not null)
            {
                CarregarCliente(id.Value);
            }

            Loaded += (_, _) => TxtNome.Focus();
        }

        private void CarregarCliente(int id)
        {
            using var contexto = App.CriarContexto();
            var cliente = contexto.Clientes.Single(c => c.Id == id);

            TxtTitulo.Text = "Editar Cliente";
            ChkAtivo.Visibility = Visibility.Visible;
            ChkAtivo.IsChecked = cliente.Ativo;
            TxtNome.Text = cliente.Nome;
            TxtTelefone.Text = cliente.Telefone;
            TxtCpfCnpj.Text = cliente.CpfCnpj;
            TxtEmail.Text = cliente.Email;
            TxtLogradouro.Text = cliente.Logradouro;
            TxtNumero.Text = cliente.Numero;
            TxtComplemento.Text = cliente.Complemento;
            TxtBairro.Text = cliente.Bairro;
            TxtCidade.Text = cliente.Cidade;
            CmbUf.SelectedItem = cliente.Uf ?? string.Empty;
            TxtCep.Text = cliente.Cep;
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            var erro = Validar(out var cpfCnpj, out var cep);
            if (erro is not null)
            {
                TxtMensagem.Text = erro;
                return;
            }

            using var contexto = App.CriarContexto();
            if (cpfCnpj is not null && contexto.Clientes.Any(c => c.CpfCnpj == cpfCnpj && c.Id != _id))
            {
                TxtMensagem.Text = "Já existe outro cliente com esse CPF/CNPJ (RN01).";
                return;
            }

            Cliente cliente;
            if (_id is null)
            {
                cliente = new Cliente();
                contexto.Clientes.Add(cliente);
            }
            else
            {
                cliente = contexto.Clientes.Single(c => c.Id == _id);
                cliente.Ativo = ChkAtivo.IsChecked == true;
            }

            cliente.Nome = TxtNome.Text.Trim();
            cliente.Telefone = Documentos.SoDigitos(TxtTelefone.Text);
            cliente.CpfCnpj = cpfCnpj;
            cliente.Email = TextoOuNulo(TxtEmail.Text);
            cliente.Logradouro = TextoOuNulo(TxtLogradouro.Text);
            cliente.Numero = TextoOuNulo(TxtNumero.Text);
            cliente.Complemento = TextoOuNulo(TxtComplemento.Text);
            cliente.Bairro = TextoOuNulo(TxtBairro.Text);
            cliente.Cidade = TextoOuNulo(TxtCidade.Text);
            cliente.Uf = TextoOuNulo(CmbUf.SelectedItem as string);
            cliente.Cep = cep;

            contexto.SaveChanges();
            MainWindow.Atual.Navegar(new ClientesView());
        }

        private string? Validar(out string? cpfCnpj, out string? cep)
        {
            cpfCnpj = TextoOuNulo(Documentos.SoDigitos(TxtCpfCnpj.Text));
            cep = TextoOuNulo(Documentos.SoDigitos(TxtCep.Text));

            if (string.IsNullOrWhiteSpace(TxtNome.Text))
            {
                return "Informe o nome do cliente.";
            }

            if (Documentos.SoDigitos(TxtTelefone.Text).Length < 10)
            {
                return "Informe o telefone com DDD.";
            }

            if (cpfCnpj is not null && (cpfCnpj.Length is not (11 or 14) || !Documentos.CpfCnpjValido(cpfCnpj)))
            {
                return "CPF ou CNPJ inválido (RN01).";
            }

            var email = TxtEmail.Text.Trim();
            if (email.Length > 0 && !Documentos.EmailValido(email))
            {
                return "E-mail inválido.";
            }

            if (cep is not null && cep.Length != 8)
            {
                return "O CEP deve ter 8 dígitos.";
            }

            return null;
        }

        /// <summary>Confere os dígitos verificadores ao sair do campo (a máscara já formata durante a digitação).</summary>
        private void TxtCpfCnpj_LostFocus(object sender, RoutedEventArgs e)
        {
            var digitos = Documentos.SoDigitos(TxtCpfCnpj.Text);
            bool invalido = digitos.Length > 0 && !Documentos.CpfCnpjValido(digitos);
            Estilo.SetErro(TxtCpfCnpj, invalido);
            if (invalido)
            {
                TxtMensagem.Text = "CPF ou CNPJ inválido (RN01).";
            }
        }

        private void BtnVoltar_Click(object sender, RoutedEventArgs e) => MainWindow.Atual.Navegar(new ClientesView());

        private static string? TextoOuNulo(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }
}
