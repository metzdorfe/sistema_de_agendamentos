# CLAUDE.md – Fest Agenda

Contexto do projeto para o Claude Code. Leia este arquivo inteiro antes de mexer em qualquer coisa.

## O que é

**Fest Agenda** (antigo "Espaço de Festas"): sistema desktop para gestão de um espaço de festas — clientes, serviços (espaços) com faixas de preço, adicionais, agenda/calendário, orçamentos, agendamentos, faturamento, contas a receber, caixa, relatórios e catálogo em PDF.

- Projeto acadêmico da **UnC**, mas entregue como produto de verdade a uma cliente (dona do espaço). Qualidade de produto comercial.
- **Monousuário, sem login na v1** (RNF09). Interface em português, formatos brasileiros (dd/mm/aaaa, R$).

## Stack

| Item | Decisão |
|---|---|
| Plataforma | .NET 10, desktop (`EspacoFestas.csproj`, solução `EspacoFestas.slnx`, namespace `EspacoFestas`) |
| Interface | WPF, temas claro (**padrão**) e escuro, trocados em tempo de execução |
| Banco | Firebird 5 (embarcado por padrão; servidor opcional via `config.ini`) |
| Acesso a dados | Entity Framework Core 10 (`FirebirdSql.EntityFrameworkCore.Firebird`), contexto `Data/EspacoFestasContext.cs` |
| PDF | QuestPDF, licença Community (definida em `App.xaml.cs`; confirmar se o uso é permitido) |
| Planilha | Relatórios também saem em XLSX (`Reports/Relatorios/XlsxRelatorio.cs`) |
| Config técnica | `config.ini` (só conexão com o banco), lido com `Microsoft.Extensions.Configuration.Ini` |
| Fonte | Inter, embutida |
| Instalador | Inno Setup (`Installer/FestAgenda.iss`) |
| WhatsApp | Servidor local Node.js + **Baileys** 7.0.0-rc14 (não oficial) em `Whatsapp/server`; Node 24 LTS portátil no instalador; QR code com `QRCoder`. Decisões e riscos em `docs/decisoes.md` |
| Versionamento | Git |

## Documentação (em `.claude/`) – o que vale quando há conflito

1. `ajustes.md` (Ajustes v2, mais recente) – **vence** sobre os demais: tema, splash, pasta `Log`, WhatsApp. Ele cita um `prompt-ajustes-fest-agenda.md` anterior, que não está mais no repositório; o que importava dele está resumido neste arquivo (nome Fest Agenda, visual clean/azul, tema claro padrão, agendamento em popup, Firebird embarcado, `CAMINHO_LOGO_EMPRESA`).
2. `fluxo-sistema.md` – regras de negócio (RN), requisitos funcionais (RF) e não funcionais (RNF). **Fonte da verdade das regras.**
3. `modelagem-banco.md` – modelo, dicionário, CHECKs, domínios, índices.
4. `stack-projeto.md` – pacotes, pastas, convenções.
5. `design-interface.md` – estrutura das telas. **Atenção:** as paletas descritas ali (roxo/neon com glow, depois roxo nos fundos) estão **superadas** pela de 18/10/2026: neutra com roxo suave só no destaque (ver "Convenções"). A estrutura das telas continua valendo.

Os documentos podem estar atrás do código. Confirme no código (`Database/schema.sql`, Services) antes de assumir algo; se encontrar contradição, avise e siga a ordem acima.

## Estrutura instalada (`C:\FestAgenda\`, mas pode ser outra pasta)

```
FestAgenda.exe + DLLs soltas (sem PublishSingleFile, self-contained win-x64)
config.ini
BD\basefestagenda.fdb       (caminho definido no config.ini)
Arquivos\Empresa\           (logo) e outras subpastas por tipo
Log\                        (AtualizacaoBanco.log, RelatorioLog.log, Erros.log, whatsapp.log)
Utilitarios\                (Scripts, isql, gbak, .bat de backup/restauração/verificação, Backups\)
Whatsapp\                   node\node.exe, server\ (src + node_modules), sessao\ (login do número)
Arquivos\WhatsApp\          PDFs enviados (catálogo)
```

- Nunca usar caminho fixo `C:\FestAgenda`: tudo a partir de `AppContext.BaseDirectory` via `Services/AppPaths.cs`.
- No banco, caminhos de arquivo são **relativos a `Arquivos\`**.
- Atualização do instalador não pode sobrescrever `config.ini`, `.fdb`, `Arquivos\` nem a sessão do WhatsApp. Desinstalar não apaga `BD\`, `Arquivos\` nem a sessão.

## Organização do código

```
Models/ Data/ Services/(Whatsapp/) Views/ Controls/ Themes/ Assets/(Brand, Fonts, Icons, Splash) Reports/(Relatorios/) Database/ Installer/ Tools/ FestAgenda.Tests/ Whatsapp/server/ docs/
```

- **WhatsApp:** telas usam só `IWhatsappService` (`Services/Whatsapp`). `WhatsappServidor` sobe o Node (sem janela, Job Object, token novo, porta livre lida da saída `PORTA=`), reinicia até 3 vezes e para junto com o app. `WhatsappMensagens` cuida de modelos, variáveis, quem pode receber, limite diário e histórico. Em desenvolvimento usa o `node` do PATH e a pasta `Whatsapp/server` do repositório (rode `npm ci` nela). `WHATSAPP_MOCK=1` no ambiente = modo simulado, sem conectar.

- **Sem MVVM**: telas em code-behind (`*.xaml.cs`), com regras nos Services estáticos (`AgendamentoService`, `FinanceiroService`). Linhas editáveis de listas usam `Observavel` (`Views/AgendamentoItens.cs`). Manter esse padrão.
- `Controls/`: máscaras, seletor de data, modal, ícones e estilos anexados.
- Navegação: `MainWindow.Atual.Navegar(new XView())`; popups via `Modal.Mostrar(janela, dono, overlay)`.
- Mensagens e confirmações: `Dialogo` (`Views/DialogoWindow.xaml.cs`), nunca `MessageBox`.
- `Tools/gerar-marca.ps1`: gera os assets da marca.

## Convenções

| Onde | Padrão |
|---|---|
| Banco | MAIÚSCULO_COM_UNDERSCORE, por extenso, sem aspas. PK `ID`, FK `<TABELA>_ID`. Prefixos `DATA_`, `HORA_`, `VALOR_`, `PERCENTUAL_`, `QUANTIDADE_`, `CAMINHO_` |
| C# classes/propriedades | PascalCase |
| C# variáveis/parâmetros | camelCase |
| Recursos XAML | PascalCase pela função (`CorBotaoPrincipal`), nunca pela cor |

- A conversão PascalCase → MAIÚSCULO_COM_UNDERSCORE é feita no `OnModelCreating` (`Data/NomeBanco.cs`).
- Telas **nunca** usam cor fixa: sempre `{DynamicResource ...}`. Cores em `Themes/Light.xaml` e `Themes/Dark.xaml` (mesmas chaves); estilos em `Themes/Estilos.xaml`; ícones em `Themes/Icones.xaml`.
- Visual (18/10/2026): base **neutra** (cinzas frios no claro, grafite no escuro) e o **roxo da marca só em ação e destaque** (botão principal, item ativo, foco, seleção). Sem fundos lilás, sem glow, sem gradiente. Controles com 36 px, cantos 8 px (12 px em cartões), foco com anel translúcido (`CorFocoAnel`). A splash, o ícone e as imagens do instalador saem do `Tools/gerar-marca.ps1` com a mesma paleta.
- Todo script de banco com `COMMENT ON` em tabelas e campos.

## Banco e atualização de versão

- `Database/schema.sql`: banco do zero. `Database/dados-iniciais.sql`: carga inicial.
- Mudança de estrutura = novo `Database/alteracoes-AAAA-MM-DD.sql` (recurso embutido) **e** registro em `Services/AtualizacaoBanco.cs` com a consulta que detecta se já foi aplicada. A atualização roda na splash e no instalador (`FestAgenda.exe --atualizar-banco`), com backup gbak antes.
- Mudou o banco? Atualize `schema.sql`, o script de alteração, `COMMENT ON`, a entidade/mapeamento EF **e** `modelagem-banco.md`.

## Modelo de dados (14 tabelas)

- **Cadastros:** CLIENTE, SERVICO, SERVICO_FAIXA, ADICIONAL, SERVICO_ADICIONAL (N:N), ESPECIE
- **Agendamento:** AGENDAMENTO, SERVICO_AGENDAMENTO, ADICIONAL_AGENDAMENTO
- **Financeiro:** RECEBER (só dívida futura), FINANCEIRO (caixa: o que entrou/saiu de fato)
- **Configuração:** CONFIGURACAO (uma linha, `ID = 1`, CHECK no banco)
- **Comunicação:** WHATSAPP_MENSAGEM (fila e histórico, status P/E/F/C), MODELO_MENSAGEM (texto por tipo, com variáveis)

Pontos-chave:
- `AGENDAMENTO.STATUS`: O orçamento, A agendado, F faturado, C cancelado. `HORA_FIM < HORA_INICIO` = termina no dia seguinte; o evento pertence à `DATA_EVENTO`.
- `RECEBER.STATUS`: A aberto, P pago, C cancelado. `PARCELA_ORIGEM_ID` liga a parcela de diferença da baixa parcial.
- `FINANCEIRO`: `TIPO` E/S; no máximo uma FK preenchida entre `RECEBER_ID` e `AGENDAMENTO_ID` (CHECK). `ESPECIE_ID` é a espécie **real**. `LANCAMENTO_ESTORNADO_ID` só na saída de estorno ou devolução (CHECK + índice único). `MOVIMENTACAO_CAIXA` = suprimento/sangria (sempre avulso).
- `CONFIGURACAO`: `BLOQUEIO_UM_POR_DIA`, `PERCENTUAL_JUROS_MES`, `PERCENTUAL_MULTA`, `PRAZO_CANCELAMENTO_DIAS`, `TEMA` (C claro **padrão**, E escuro, S Windows), dados da empresa, `CAMINHO_LOGO_EMPRESA`, `WHATSAPP_ATIVO`, `WHATSAPP_LIMITE_DIARIO`.
- `CLIENTE.ACEITA_WHATSAPP`: consentimento (LGPD); desmarcado, nada é enviado.
- Valores em NUMERIC(18,2); percentuais NUMERIC(5,2); flags BOOLEAN nativo; CPF/CNPJ/CEP/telefone gravados só com dígitos. CPF/CNPJ único (índice).
- Nada usado é excluído: inativa-se (`ATIVO`). Histórico financeiro nunca é apagado.

## Regras de negócio (resumo – texto completo em `fluxo-sistema.md`)

Ao criar ou mudar uma regra, atualize `fluxo-sistema.md` (numere como RNxx), ajuste este resumo e cite o número no código.

- **RN01 CPF/CNPJ:** opcional; se informado, válido (dígitos verificadores, sem dígitos todos iguais) e único.
- **RN05:** agendamento precisa de pelo menos um serviço.
- **Valores congelados (RN08):** venda e custo copiados no lançamento; mudar o cadastro depois não altera agendamentos.
- **Total (RN09):** serviços + adicionais − desconto. Linha de adicional = quantidade × valor (se `COBRA_POR_QUANTIDADE`). Desconto não pode passar do subtotal.
- **Disponibilidade (RN10/11):** por **serviço**; com `BLOQUEIO_UM_POR_DIA`, bloqueiam Agendado, Faturado e Orçamento **com sinal**. Orçamento sem sinal não bloqueia; ao gravar/converter, revalidar. O próprio agendamento não se bloqueia.
- **Sinal (RN12):** vai direto ao FINANCEIRO ligado ao agendamento; não gera RECEBER.
- **RN32:** o cliente nunca paga mais que o total: sinal ≤ total − já pago, e o total não pode ficar menor que o já pago.
- **Faturamento (RN13–17):** soma das parcelas = total − já pago; espécie sem parcelamento aceita 1 parcela; recebido hoje → FINANCEIRO, vencimento futuro → RECEBER; parcelas iguais com a diferença de centavos na última; **faturado é imutável**, só cancela.
- **Juros/multa (RN18):** `juros_dia = parcela × taxa_mês ÷ 100 ÷ 30`; `total = parcela + multa + juros_dia × dias_atraso`. Valores ajustáveis na tela de baixa.
- **Baixa parcial (RN19):** pergunta se cria parcela da diferença (ou perdoa); original fica P em qualquer caso.
- **RN20:** a baixa pode usar espécie diferente da combinada.
- **Estorno (RN21/26):** baixa: reabre parcela (limpa juros, multa, valor, data), saída no caixa, cancela parcela de diferença. Sinal: saída ligada ao agendamento; orçamento deixa de bloquear a data. Entrada avulsa também se estorna; saída não. A saída aponta para a entrada estornada (`FINANCEIRO.LANCAMENTO_ESTORNADO_ID`), uma vez só. Bloqueado em agendamento cancelado e, para sinal, em faturado.
- **Caixa (RN33, RF12/RF15):** lançamento à mão: avulso (entrada ou saída) ou despesa do evento (saída ligada ao agendamento). Despesa não conta como pago nem é devolvida.
- **RN34:** suprimento e sangria só movimentam o caixa (fora de receita/despesa). Não se estornam: corrige-se com o lançamento contrário (o mesmo vale para saída avulsa errada: lançar entrada).
- **Lucro (RF16):** receita (total) − custo dos adicionais − despesas do evento; no popup do faturado e no relatório de faturamento.
- **WhatsApp (RN29, RN30, RN35):** limite diário configurável (padrão 10); falha nunca impede salvar nada; só envia a quem aceita e tem telefone válido; toda mensagem é revisada e confirmada por ela.
- **Cancelamento (RN22–24, RN27):** só até a data do evento inclusive; motivo e data obrigatórios; parcelas abertas viram C; dentro do prazo (`PRAZO_CANCELAMENTO_DIAS` antes do evento) tudo que foi pago é devolvido: uma saída por entrada, apontando para ela (`LANCAMENTO_ESTORNADO_ID`); fora do prazo, não há devolução.
- **RN28:** evento pode passar da meia-noite (ex.: 23:00–02:00); fim igual ao início é inválido.

## Telas

Navbar fixa à esquerda (Agenda, Clientes, Serviços, Financeiro [Visão geral, Contas a receber, Caixa], Relatórios, perfil da empresa → Configurações). Splash → Agenda (mês) → painel do dia (desliza da direita) → agendamento (**popup modal**) → faturamento (popup). Baixa de parcela em popup a partir de Contas a receber. Estorno: botão "Estornar baixa" em Contas a receber e "Estornar" no Caixa. Caixa: "Novo lançamento" abre o popup de despesa do evento, lançamento avulso, suprimento e sangria. Popup do agendamento: "Enviar por WhatsApp" (e pergunta ao salvar orçamento); faturado mostra o lucro do evento. Catálogo pelo WhatsApp: Relatórios → Catálogo ou menu do cliente. Configurações: Parâmetros, Espécies de pagamento, Empresa, Aparência, WhatsApp (conexão por QR, limite, mensagens padrão, histórico), Sobre.

Relatórios (PDF e XLSX): Agendamentos, Caixa por cliente, Faturamento, Catálogo.

## Estado atual e pendências conhecidas

- Verificar no código o que de fato está implementado; **não assumir**.
- Não implementado: WhatsApp para cobrança de parcela e lembretes; finalização da `prompt-whatsapp-finalizacao.md` parte B (versão 1.0.0 única, backup pelo sistema, manuais, README, CHANGELOG, licenças, tag).
- Erros não tratados vão para `Log\Erros.log` (`Services/ErroLog.cs`, rotação a cada 1 MB, guarda 3), sem dados digitados.
- Em aberto: licença QuestPDF, fluxo de branches, semana/dia no calendário.

## Testes

`FestAgenda.Tests/` (xUnit v3): `dotnet test FestAgenda.Tests`. Regras puras (CPF/CNPJ, total, parcelamento, telefone, variáveis) e regras com banco (agendamento, faturamento, juros/multa, baixa, estorno, cancelamento, caixa, lucro, WhatsApp, CHECKs). Os de banco criam um Firebird novo no servidor local (`localhost:3050`, SYSDBA/masterkey) a partir de `Database/schema.sql` e o apagam no fim; sem servidor, são pulados. Um teste sobe o servidor Node de verdade em modo simulado (precisa de `node` no PATH e `npm ci` em `Whatsapp/server`). Regra nova ou corrigida ganha teste.

Servidor do WhatsApp: `npm test` em `Whatsapp/server` (telefone, caminho, fila/limite, token, rotas e log).
- Fora do escopo, salvo decisão do grupo: recibo em PDF e backup automático.

## Como trabalhar neste repositório

1. Leia a estrutura da solução e os temas antes de alterar.
2. Mudou o banco? Ver "Banco e atualização de versão".
3. Mudou regra de negócio? Atualize `fluxo-sistema.md` (numere como RNxx) e o resumo deste arquivo.
4. Regras parametrizáveis ficam na aplicação (Services), não em trigger.
5. Dinheiro sempre `decimal`; arredondar com `MidpointRounding.AwayFromZero` em 2 casas.
6. Operações financeiras multi-passo dentro de **transação** única.
7. Sem `MessageBox` para erro de campo: validação inline (borda + mensagem junto do campo).
8. Compile sem warnings novos (`dotnet build -c Release` hoje sai com 0 avisos). Commits pequenos, mensagens em português, uma mudança lógica por commit.
9. Nunca versionar `.fdb`, `config.ini` real, sessão do WhatsApp ou tokens. Versionar `config.exemplo.ini`.
10. Os instaladores de terceiros em `Installer/Utilitarios/` (SDK do .NET, Firebird, RDBExpert) ficam só na máquina local e **não vão para o GitHub**.
11. Ao terminar, liste o que ficou pendente ou precisa de decisão do grupo.

## Arquivos de prompt deste projeto (em `.claude/`)

- `prompt-revisao-qualidade.md` – revisão geral (QA) do sistema.
- `prompt-whatsapp-finalizacao.md` – integração WhatsApp e fechamento do projeto.

Ordem recomendada: revisão de qualidade → WhatsApp e finalização.
