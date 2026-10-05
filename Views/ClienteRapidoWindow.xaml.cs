using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EspacoFestas.Controls;
using EspacoFestas.Models;
using EspacoFestas.Services;

namespace EspacoFestas.Views
{
    /// <summary>Cadastro rápido de cliente a partir do agendamento (RF01, RN01). Ao salvar, o agendamento já seleciona o cliente.</summary>
    public partial class ClienteRapidoWindow : Window
    {
        public ClienteRapidoWindow(string nomeSugerido)
        {
            InitializeComponent();

            // Se o que foi digitado na busca parece um nome, já vem preenchido
            if (Documentos.SoDigitos(nomeSugerido).Length == 0)
            {
                TxtNome.Text = nomeSugerido;
            }

            AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                new TextChangedEventHandler((s, e) => LimparErro(e.OriginalSource as TextBox)));
            Loaded += (_, _) =>
            {
                TxtNome.Focus();
                TxtNome.CaretIndex = TxtNome.Text.Length;
            };
        }

        /// <summary>Cliente gravado (nulo se cancelou).</summary>
        public ClienteOpcao? Cliente { get; private set; }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            bool ok = true;
            void Erro(TextBox campo, TextBlock mensagem, string texto)
            {
                Estilo.SetErro(campo, true);
                mensagem.Text = texto;
                mensagem.Visibility = Visibility.Visible;
                if (ok)
                {
                    campo.Focus();
                }

                ok = false;
            }

            var cpfCnpj = Documentos.SoDigitos(TxtCpfCnpj.Text);
            var email = TxtEmail.Text.Trim();

            if (string.IsNullOrWhiteSpace(TxtNome.Text))
            {
                Erro(TxtNome, ErroNome, "Informe o nome.");
            }

            if (Documentos.SoDigitos(TxtTelefone.Text).Length < 10)
            {
                Erro(TxtTelefone, ErroTelefone, "Informe o telefone com DDD.");
            }

            if (cpfCnpj.Length > 0 && (cpfCnpj.Length is not (11 or 14) || !Documentos.CpfCnpjValido(cpfCnpj)))
            {
                Erro(TxtCpfCnpj, ErroCpfCnpj, "CPF ou CNPJ inválido.");
            }

            if (email.Length > 0 && !Documentos.EmailValido(email))
            {
                Erro(TxtEmail, ErroEmail, "E-mail inválido.");
            }

            if (!ok)
            {
                return;
            }

            using var contexto = App.CriarContexto();
            if (cpfCnpj.Length > 0 && contexto.Clientes.Any(c => c.CpfCnpj == cpfCnpj))
            {
                Erro(TxtCpfCnpj, ErroCpfCnpj, "Já existe um cliente com esse CPF/CNPJ.");
                return;
            }

            var cliente = new Cliente
            {
                Nome = TxtNome.Text.Trim(),
                Telefone = Documentos.SoDigitos(TxtTelefone.Text),
                CpfCnpj = cpfCnpj.Length > 0 ? cpfCnpj : null,
                Email = email.Length > 0 ? email : null
            };
            contexto.Clientes.Add(cliente);
            contexto.SaveChanges();

            Cliente = new ClienteOpcao(cliente.Id, cliente.Nome, cliente.Telefone, cliente.CpfCnpj);
            DialogResult = true;
        }

        private void LimparErro(TextBox? campo)
        {
            var mensagem = campo == TxtNome ? ErroNome
                : campo == TxtTelefone ? ErroTelefone
                : campo == TxtCpfCnpj ? ErroCpfCnpj
                : campo == TxtEmail ? ErroEmail
                : null;
            if (campo is null || mensagem is null)
            {
                return;
            }

            Estilo.SetErro(campo, false);
            mensagem.Visibility = Visibility.Collapsed;
        }

        /// <summary>Confere os dígitos verificadores ao sair do campo (a máscara já formata durante a digitação).</summary>
        private void TxtCpfCnpj_LostFocus(object sender, RoutedEventArgs e)
        {
            var digitos = Documentos.SoDigitos(TxtCpfCnpj.Text);
            if (digitos.Length > 0 && !Documentos.CpfCnpjValido(digitos))
            {
                Estilo.SetErro(TxtCpfCnpj, true);
                ErroCpfCnpj.Text = "CPF ou CNPJ inválido.";
                ErroCpfCnpj.Visibility = Visibility.Visible;
            }
            else
            {
                LimparErro(TxtCpfCnpj);
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void Cabecalho_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not Button && e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
