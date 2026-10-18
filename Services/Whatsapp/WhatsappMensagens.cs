using System.IO;
using System.Text.RegularExpressions;
using EspacoFestas.Data;
using EspacoFestas.Models;
using EspacoFestas.Reports;
using Microsoft.EntityFrameworkCore;

namespace EspacoFestas.Services.Whatsapp;

/// <summary>O que a tela de envio manda enviar (texto já revisado por ela).</summary>
/// <param name="CaminhoAnexo">PDF relativo à pasta Arquivos\ (catálogo); nulo = só texto.</param>
public sealed record NovaMensagem(TipoMensagem Tipo, int? ClienteId, int? AgendamentoId, string Telefone, string Texto, string? CaminhoAnexo = null);

/// <summary>
/// Mensagens de WhatsApp (RF26–RF29): modelos com variáveis, quem pode receber, limite diário (RN29) e o registro de cada envio
/// em WHATSAPP_MENSAGEM (nada é apagado; falha pode ser reenviada). Falha aqui nunca desfaz o que foi salvo (RN30).
/// </summary>
public static partial class WhatsappMensagens
{
    /// <summary>Variáveis que os modelos aceitam, na ordem em que a tela mostra.</summary>
    public static readonly IReadOnlyList<string> Variaveis =
        ["cliente", "titulo", "status", "data", "hora", "servicos", "valor", "pago", "saldo", "empresa", "telefone_empresa"];

    /// <summary>Nome de cada variável na tela (o editor mostra a etiqueta "Cliente", não "{cliente}").</summary>
    public static readonly IReadOnlyDictionary<string, string> Rotulos = new Dictionary<string, string>
    {
        ["cliente"] = "Cliente",
        ["titulo"] = "Título do evento",
        ["status"] = "Orçamento/agendamento",
        ["data"] = "Data do evento",
        ["hora"] = "Horário",
        ["servicos"] = "Serviços",
        ["valor"] = "Valor total",
        ["pago"] = "Valor pago",
        ["saldo"] = "Saldo",
        ["empresa"] = "Nome da empresa",
        ["telefone_empresa"] = "Telefone da empresa"
    };

    /// <summary>Variáveis que fazem sentido em cada tipo (o catálogo não tem evento nem valores).</summary>
    public static IReadOnlyList<string> VariaveisDoTipo(TipoMensagem tipo) => tipo == TipoMensagem.Catalogo
        ? ["cliente", "empresa", "telefone_empresa"]
        : Variaveis;

    /// <summary>Texto padrão de fábrica do tipo (botão "Restaurar padrão").</summary>
    public static string TextoPadrao(TipoMensagem tipo) => Padroes.GetValueOrDefault(tipo, string.Empty);

    /// <summary>Textos padrão, se o modelo do banco estiver vazio ou inativo.</summary>
    private static readonly Dictionary<TipoMensagem, string> Padroes = new()
    {
        [TipoMensagem.Catalogo] = "Olá, {cliente}! Segue o catálogo de serviços da {empresa}. Qualquer dúvida, é só chamar.",
        [TipoMensagem.Orcamento] = "Olá, {cliente}! Segue o resumo do seu {status}:\n\n*{titulo}*\nData: {data}, das {hora}\n" +
                                   "Serviços: {servicos}\nTotal: {valor}\nJá pago: {pago}\nSaldo: {saldo}\n\n{empresa} - {telefone_empresa}"
    };

    /// <summary>DDDs existentes no Brasil (a mesma lista de Whatsapp\server\src\telefone.js).</summary>
    private static readonly HashSet<int> Ddds =
    [
        11, 12, 13, 14, 15, 16, 17, 18, 19, 21, 22, 24, 27, 28, 31, 32, 33, 34, 35, 37, 38,
        41, 42, 43, 44, 45, 46, 47, 48, 49, 51, 53, 54, 55, 61, 62, 63, 64, 65, 66, 67, 68, 69,
        71, 73, 74, 75, 77, 79, 81, 82, 83, 84, 85, 86, 87, 88, 89, 91, 92, 93, 94, 95, 96, 97, 98, 99
    ];

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Variavel();

    /// <summary>Troca cada {variavel} pelo valor; variável desconhecida fica como está.</summary>
    public static string Preencher(string modelo, IReadOnlyDictionary<string, string> valores) =>
        Variavel().Replace(modelo, m => valores.TryGetValue(m.Groups[1].Value, out var valor) ? valor : m.Value);

    /// <summary>Mesmas regras do servidor: 55 + DDD + 8 ou 9 dígitos (celular de 9 começa com 9). Nulo = inválido.</summary>
    public static string? NormalizarTelefone(string? telefone)
    {
        var digitos = Documentos.SoDigitos(telefone);
        if (digitos.Length is 12 or 13 && digitos.StartsWith("55", StringComparison.Ordinal))
        {
            digitos = digitos[2..];
        }

        if (digitos.Length is not (10 or 11) || !Ddds.Contains(int.Parse(digitos[..2])))
        {
            return null;
        }

        return digitos.Length == 11 && digitos[2] != '9' ? null : "55" + digitos;
    }

    /// <summary>Por que o cliente não pode receber (null = pode). A tela explica o motivo em vez de esconder o botão.</summary>
    public static string? MotivoNaoEnvia(Cliente cliente)
    {
        if (!cliente.AceitaWhatsapp)
        {
            return "O cliente não aceita receber mensagens pelo WhatsApp (marcado no cadastro).";
        }

        return NormalizarTelefone(cliente.Telefone) is null
            ? "O telefone do cliente não é válido para o WhatsApp. Corrija no cadastro."
            : null;
    }

    public static string TextoModelo(TipoMensagem tipo)
    {
        using var contexto = App.CriarContexto();
        var texto = contexto.ModelosMensagem.AsNoTracking().Where(m => m.Tipo == tipo && m.Ativo).Select(m => m.Texto).FirstOrDefault();
        return string.IsNullOrWhiteSpace(texto) ? Padroes.GetValueOrDefault(tipo, string.Empty) : texto;
    }

    /// <summary>Salva o texto do modelo (cria se não existir).</summary>
    public static void SalvarModelo(TipoMensagem tipo, string titulo, string texto)
    {
        using var contexto = App.CriarContexto();
        var modelo = contexto.ModelosMensagem.SingleOrDefault(m => m.Tipo == tipo);
        if (modelo is null)
        {
            modelo = new ModeloMensagem { Tipo = tipo, Titulo = titulo };
            contexto.ModelosMensagem.Add(modelo);
        }

        modelo.Texto = texto.Trim();
        modelo.Ativo = true;
        contexto.SaveChanges();
    }

    public static Dictionary<string, string> VariaveisCliente(Cliente cliente) => new()
    {
        ["cliente"] = PrimeiroNome(cliente.Nome),
        ["empresa"] = App.NomeEmpresa,
        ["telefone_empresa"] = Documentos.FormatarTelefone(App.Sistema.TelefoneEmpresa)
    };

    /// <summary>Variáveis do orçamento/agendamento: título, status, data, hora, serviços, total, pago e saldo.</summary>
    public static Dictionary<string, string> VariaveisAgendamento(EspacoFestasContext contexto, int agendamentoId)
    {
        var a = contexto.Agendamentos.AsNoTracking()
            .Where(x => x.Id == agendamentoId)
            .Select(x => new
            {
                x.Titulo, x.Status, x.DataEvento, x.HoraInicio, x.HoraFim, x.ValorTotal, Cliente = x.Cliente!,
                Servicos = x.Servicos.Select(s => s.Servico!.Nome).ToList()
            })
            .Single();
        decimal pago = AgendamentoService.ValorPago(contexto, agendamentoId);

        var valores = VariaveisCliente(a.Cliente);
        valores["titulo"] = a.Titulo;
        valores["status"] = a.Status == StatusAgendamento.Orcamento ? "orçamento" : "agendamento";
        valores["data"] = $"{Formatos.Data(a.DataEvento)} ({a.DataEvento.ToString("dddd", Formatos.Brasil)})";
        valores["hora"] = $"{Formatos.Hora(a.HoraInicio)} às {Formatos.Hora(a.HoraFim)}";
        valores["servicos"] = string.Join(", ", a.Servicos);
        valores["valor"] = Formatos.Moeda(a.ValorTotal);
        valores["pago"] = Formatos.Moeda(pago);
        valores["saldo"] = Formatos.Moeda(Math.Max(a.ValorTotal - pago, 0));
        return valores;
    }

    /// <summary>Gera o catálogo em Arquivos\WhatsApp com nome único e devolve o caminho relativo (como fica no banco).</summary>
    public static string GerarCatalogo(int? servicoId)
    {
        var completo = Path.Combine(AppPaths.PastaArquivos("WhatsApp"), $"catalogo_{DateTime.Now:yyyyMMddHHmmssfff}.pdf");
        CatalogoPdf.Gerar(completo, servicoId);
        return AppPaths.ArquivoRelativo(completo);
    }

    /// <summary>RN29: enviadas hoje (contam para o limite diário).</summary>
    public static int EnviadasHoje(EspacoFestasContext contexto) =>
        contexto.WhatsappMensagens.Count(m => m.Status == StatusMensagem.Enviada && m.DataEnvio >= DateTime.Today);

    /// <summary>Registra a mensagem e tenta enviar. Falha fica gravada (F) com o motivo, para reenviar depois.</summary>
    public static async Task<WhatsappResultado> EnviarAsync(NovaMensagem nova, IWhatsappService servico)
    {
        if (!App.Sistema.WhatsappAtivo)
        {
            return WhatsappResultado.Falha("DESLIGADO", "O WhatsApp está desligado em Configurações.");
        }

        var telefone = NormalizarTelefone(nova.Telefone);
        if (telefone is null)
        {
            return WhatsappResultado.Falha("TELEFONE_INVALIDO", "Telefone inválido para o WhatsApp.");
        }

        if (string.IsNullOrWhiteSpace(nova.Texto) && nova.CaminhoAnexo is null)
        {
            return WhatsappResultado.Falha("PEDIDO_INVALIDO", "Escreva a mensagem.");
        }

        int id;
        using (var contexto = App.CriarContexto())
        {
            var registro = new WhatsappMensagem
            {
                Tipo = nova.Tipo,
                ClienteId = nova.ClienteId,
                AgendamentoId = nova.AgendamentoId,
                Telefone = telefone,
                Texto = Limitar(nova.Texto.Trim(), 2000),
                CaminhoAnexo = nova.CaminhoAnexo,
                Status = StatusMensagem.Pendente,
                DataCriacao = DateTime.Now
            };
            contexto.WhatsappMensagens.Add(registro);
            contexto.SaveChanges();
            id = registro.Id;
        }

        return await TentarAsync(id, servico);
    }

    /// <summary>Reenvio de uma mensagem que falhou.</summary>
    public static async Task<WhatsappResultado> ReenviarAsync(int mensagemId, IWhatsappService servico)
    {
        if (!App.Sistema.WhatsappAtivo)
        {
            return WhatsappResultado.Falha("DESLIGADO", "O WhatsApp está desligado em Configurações.");
        }

        using (var contexto = App.CriarContexto())
        {
            if (contexto.WhatsappMensagens.Where(m => m.Id == mensagemId).Select(m => m.Status).Single() != StatusMensagem.Falha)
            {
                return WhatsappResultado.Falha("PEDIDO_INVALIDO", "Só mensagens com falha podem ser reenviadas.");
            }
        }

        return await TentarAsync(mensagemId, servico);
    }

    private static async Task<WhatsappResultado> TentarAsync(int id, IWhatsappService servico)
    {
        WhatsappMensagem mensagem;
        int enviadasHoje;
        using (var contexto = App.CriarContexto())
        {
            mensagem = contexto.WhatsappMensagens.AsNoTracking().Single(m => m.Id == id);
            enviadasHoje = EnviadasHoje(contexto);
        }

        WhatsappResultado resultado;
        if (enviadasHoje >= App.Sistema.WhatsappLimiteDiario)
        {
            resultado = WhatsappResultado.Falha("LIMITE",
                $"Limite de {App.Sistema.WhatsappLimiteDiario} mensagens por dia atingido. Reenvie amanhã ou aumente o limite em Configurações → WhatsApp.");
        }
        else if (mensagem.CaminhoAnexo is { } anexo)
        {
            var nome = mensagem.Tipo == TipoMensagem.Catalogo ? $"Catálogo {App.NomeEmpresa}.pdf" : Path.GetFileName(anexo);
            resultado = await servico.EnviarDocumentoAsync(mensagem.Telefone, anexo, nome, mensagem.Texto);
        }
        else
        {
            resultado = await servico.EnviarTextoAsync(mensagem.Telefone, mensagem.Texto);
        }

        using (var contexto = App.CriarContexto())
        {
            var registro = contexto.WhatsappMensagens.Single(m => m.Id == id);
            registro.Tentativas++;
            registro.Status = resultado.Ok ? StatusMensagem.Enviada : StatusMensagem.Falha;
            registro.DataEnvio = resultado.Ok ? DateTime.Now : null;
            registro.MensagemErro = resultado.Ok ? null : Limitar(DescreverFalha(resultado), 500);
            contexto.SaveChanges();
        }

        return resultado;
    }

    /// <summary>Motivo da falha em português, para a tela e para o histórico.</summary>
    public static string DescreverFalha(WhatsappResultado resultado) => resultado.Codigo switch
    {
        "NAO_CONECTADO" => resultado.Erro ?? "O WhatsApp não está conectado.",
        "NUMERO_SEM_WHATSAPP" => "Este número não tem WhatsApp.",
        "TELEFONE_INVALIDO" => "Telefone inválido para o WhatsApp.",
        "ARQUIVO_INVALIDO" => "O PDF não foi encontrado na pasta Arquivos.",
        _ => resultado.Erro ?? "Não foi possível enviar."
    };

    private static string PrimeiroNome(string nome) => nome.Trim().Split(' ', 2)[0];

    private static string Limitar(string texto, int tamanho) => texto.Length <= tamanho ? texto : texto[..tamanho];
}
