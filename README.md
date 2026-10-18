# Fest Agenda

Sistema desktop para gestão de um espaço de festas: clientes, serviços com faixas de preço e adicionais, agenda, orçamentos, agendamentos, faturamento, contas a receber, caixa, relatórios (PDF e Excel), catálogo em PDF e envio pelo WhatsApp.

Projeto acadêmico da Universidade do Contestado (UnC), entregue como produto a uma cliente. Contexto completo para quem for mexer no código: [`CLAUDE.md`](CLAUDE.md). Regras de negócio: `.claude/fluxo-sistema.md`. Banco: `.claude/modelagem-banco.md`.

## Tecnologias

.NET 10 + WPF · Firebird 5 (embarcado) com Entity Framework Core 10 · QuestPDF e ClosedXML · servidor local Node.js + Baileys para o WhatsApp · Inno Setup 6 para o instalador.

## Requisitos para desenvolver

- Windows 10/11 x64, SDK do .NET 10.
- Firebird 5 Server instalado (desenvolvimento usa o servidor em `localhost:3050`; a cliente usa o embarcado).
- Node.js 20 ou mais novo e npm (só para o WhatsApp).
- Inno Setup 6 (só para gerar o instalador).

## Rodar

1. Copie `config.exemplo.ini` para `config.ini` e ajuste a seção `[Banco]` (em desenvolvimento, `Tipo=Servidor` apontando para um `.fdb` local).
2. Banco novo: crie com `Database/schema.sql` + `Database/dados-iniciais.sql` (isql). Banco antigo é atualizado sozinho ao abrir (`Database/alteracoes-*.sql`).
3. WhatsApp: `cd Whatsapp/server && npm ci`. Para testar sem conectar num número real, defina `WHATSAPP_MOCK=1` no ambiente.
4. `dotnet run --project EspacoFestas.csproj`

## Testes

```
dotnet test FestAgenda.Tests      # regras de negócio; os de banco criam um Firebird novo em localhost:3050
cd Whatsapp/server && npm test    # servidor do WhatsApp
```

Roteiro manual para validar uma instalação: [`docs/plano-testes-manuais.md`](docs/plano-testes-manuais.md).

## Gerar o instalador

```
powershell -ExecutionPolicy Bypass -File build-instalador.ps1
```

Publica o sistema (self-contained, win-x64), monta o Firebird embarcado e o banco vazio, baixa o Node portátil (versão e SHA-256 fixos no script), instala as dependências do servidor do WhatsApp e compila `Installer/FestAgenda.iss`. Saída em `artifacts/instalador`.

## Documentos

- [`docs/decisoes.md`](docs/decisoes.md) – decisões e riscos aceitos (inclusive o do WhatsApp não oficial).
- [`docs/relatorio-revisao.md`](docs/relatorio-revisao.md) – revisão de qualidade.
- [`docs/divergencias.md`](docs/divergencias.md) – contradições entre documentos e como foram resolvidas.
