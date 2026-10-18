# Prompt – Integração com WhatsApp e finalização do Fest Agenda

Leia o `CLAUDE.md` e os documentos do projeto antes de começar. Pré-requisito: a revisão de qualidade (`prompt-revisao-qualidade.md`) concluída, sem **bloqueadores de release** em aberto. Se houver, trate-os primeiro.

Objetivo: (A) integrar o WhatsApp por um **servidor local em Node.js** que roda junto com o sistema e (B) fechar o projeto para entrega (versão 1.0.0, instalador, documentação, tag no Git).

## Regras gerais

- Trabalhe no repositório Git atual, em um branch `feature/whatsapp`. Commits pequenos, mensagens em português.
- Siga as convenções do `CLAUDE.md` (nomes, `AppPaths`, temas com `DynamicResource`, validação inline, transações).
- Mudança no banco: DDL em `Database/`, `COMMENT ON`, entidade EF, e atualização do `modelagem-banco.md`.
- O sistema **continua funcionando 100% sem WhatsApp e sem internet**. O WhatsApp é um complemento: se falhar, nada do resto pode travar. Registrar isso no `fluxo-sistema.md` (ajustar o RNF10, que hoje diz "sem depender de internet" apenas para o PDF).
- Antes de implementar, mostre um **plano curto** (arquitetura, tabelas novas, telas) e a lista de decisões que dependem do grupo. Siga com as recomendações abaixo se não houver resposta.

---

# PARTE A – WhatsApp

## A1. Decisão de tecnologia (registrar em `docs/decisoes.md`)

| Opção | Prós | Contras |
|---|---|---|
| **Baileys** (`@whiskeysockets/baileys`, conexão direta por WebSocket) – **recomendada** | Leve, sem Chromium, roda em Node portátil, sessão por QR code | Não oficial: o WhatsApp pode banir o número; a API muda com frequência |
| whatsapp-web.js | Popular | Exige Chromium/Puppeteer (pesado, centenas de MB), também não oficial |
| WhatsApp Cloud API (oficial, Meta) | Sem risco de banimento, estável | Exige conta Meta Business, número dedicado, modelos de mensagem aprovados, cobrança por conversa, e **webhook/servidor público** para receber; foge do "tudo local" |

- **Avisar o grupo e a cliente por escrito:** bibliotecas não oficiais violam os termos do WhatsApp. Mitigação: usar um número dedicado ao negócio, volume baixo, só para clientes que já têm relação com o espaço, intervalo entre mensagens e envio sempre iniciado ou confirmado por ela.
- Implementar atrás de uma interface (`IWhatsappService` no C#, e um contrato HTTP fixo no Node) para poder trocar pela Cloud API no futuro sem mexer nas telas.
- Fixar versões exatas (`package-lock.json` versionado). Testar a versão escolhida da biblioteca antes de congelar.

## A2. Arquitetura

```
FestAgenda.exe (WPF)
   │  HTTP em 127.0.0.1:<porta>  + token Bearer
   ▼
Whatsapp\server\  (Node.js)  ──WebSocket──►  WhatsApp
   └─ sessão salva em Whatsapp\sessao\
```

- **Node portátil** embarcado em `Whatsapp\node\node.exe` (versão LTS atual, x64). A cliente **não** instala Node nem npm.
- `node_modules` já instalados (`npm ci --omit=dev`) durante o build do instalador; **nada de rede na máquina dela** além do próprio WhatsApp.
- O WPF **inicia e supervisiona** o processo Node:
  - sobe em segundo plano, sem janela de console (`CreateNoWindow`), só se o WhatsApp estiver ativo na configuração;
  - espera `GET /health` ficar ok (timeout e mensagem amigável);
  - reinicia com backoff se cair (máx. 3 tentativas, depois avisa);
  - ao fechar o app, encerra o Node e filhos (usar **Job Object** do Windows para não deixar processo órfão, nem se o app travar);
  - impede segunda instância do servidor (porta ocupada → escolher outra livre e informar ao app).
- **Segurança local:** escutar **somente em `127.0.0.1`** (evita pedido de firewall e acesso pela rede); token aleatório gerado a cada inicialização e passado por variável de ambiente; rejeitar requisições sem o token; limite de tamanho de corpo.
- Logs do servidor em `Logs\whatsapp.log` com rotação; **sem** conteúdo de mensagens nem telefones completos (mascarar).

### Contrato HTTP (Node)

| Método | Rota | Função |
|---|---|---|
| GET | `/health` | Processo vivo |
| GET | `/status` | `estado`: `desconectado`, `aguardando_qr`, `conectando`, `conectado`, `erro`; `qr` (string) quando houver; número conectado e nome |
| POST | `/enviar/texto` | `{ telefone, texto }` |
| POST | `/enviar/documento` | `{ telefone, caminhoArquivo, nomeArquivo, legenda }` — o caminho precisa estar **dentro de `Arquivos\`** (validar para evitar path traversal) |
| POST | `/desconectar` | Faz logout e apaga a sessão |

- Resposta padrão: `{ ok, id, erro, codigo }`. Códigos: `NAO_CONECTADO`, `NUMERO_SEM_WHATSAPP`, `ARQUIVO_INVALIDO`, `LIMITE`, `FALHA`.
- **Normalizar telefone:** só dígitos; assumir Brasil (`55`) se faltar; validar DDD; resolver o JID real pela consulta de existência do número (`onWhatsApp`), por causa do nono dígito. Número sem WhatsApp → `NUMERO_SEM_WHATSAPP`, sem exceção.
- **Fila e limite no servidor:** uma mensagem por vez, intervalo aleatório de 6–15 s entre envios, teto diário configurável (padrão 100), "digitando…" antes de enviar. Nunca envio em massa de uma vez.
- Reconexão automática do WebSocket; tratar `loggedOut` (sessão invalidada → estado `desconectado`, avisar o app).

## A3. Banco de dados (propor; criar após o plano aprovado)

Tabelas novas (de 12 para 14) e campos:

**WHATSAPP_MENSAGEM** – fila e histórico (nada é apagado):
`ID`, `CLIENTE_ID` (FK, opcional), `AGENDAMENTO_ID` (FK, opcional), `RECEBER_ID` (FK, opcional), `TELEFONE`, `TIPO` (`C` catálogo, `O` orçamento/agendamento, `L` lembrete de evento, `P` cobrança de parcela, `A` avulsa), `TEXTO`, `CAMINHO_ANEXO` (relativo a `Arquivos\`), `STATUS` (`P` pendente, `E` enviada, `F` falha, `C` cancelada), `DATA_CRIACAO`, `DATA_ENVIO`, `TENTATIVAS`, `MENSAGEM_ERRO`.

**MODELO_MENSAGEM** – textos editáveis por ela:
`ID`, `TIPO`, `TITULO`, `TEXTO` (com variáveis `{cliente}`, `{titulo}`, `{data}`, `{hora}`, `{valor}`, `{vencimento}`, `{empresa}`, `{telefone_empresa}`), `ATIVO`. Já vir com modelos padrão em português, cordiais e curtos.

**CONFIGURACAO** (+ campos): `WHATSAPP_ATIVO` (boolean, padrão falso), `WHATSAPP_PORTA` (smallint, 0 = automática), `WHATSAPP_LIMITE_DIARIO` (smallint, padrão 100), `WHATSAPP_LEMBRETE_EVENTO_DIAS` (smallint, 0 = desligado), `WHATSAPP_AVISO_VENCIMENTO_DIAS` (smallint, 0 = desligado).

**CLIENTE** (+ campo): `ACEITA_WHATSAPP` (boolean, padrão verdadeiro; desmarcável) — respeito ao consentimento (LGPD). Cliente sem telefone válido ou sem aceite não recebe e a tela explica o motivo.

Domínio novo `D_STATUS_MENSAGEM` e `D_TIPO_MENSAGEM` (CHECK). Índices em `STATUS`+`DATA_CRIACAO`. Script de **upgrade** do banco (versão do esquema), sem perder dados existentes.

## A4. Funcionalidades (em ordem de prioridade)

1. **Conexão (Configurações → aba WhatsApp):** ligar/desligar, estado (indicador com texto, não só cor), botão "Conectar" que mostra o **QR code** (biblioteca `QRCoder` no WPF; atualização automática enquanto aguarda), nome/número conectado, "Desconectar", limite diário, dias de antecedência dos lembretes, edição dos modelos de mensagem com pré-visualização das variáveis. Texto explicativo curto sobre usar número dedicado.
2. **Enviar catálogo em PDF:** gerar com QuestPDF (código já existe) e enviar ao cliente escolhido. Acesso por **Relatórios → Catálogo** (resolver o TODO deixado quando o botão saiu da tela de Serviço), com opções "Salvar PDF" e "Enviar por WhatsApp".
3. **Orçamento/agendamento:** no popup de agendamento, botão "Enviar por WhatsApp" com resumo (título, data, hora, serviço, total, sinal pago, saldo). Só após salvar. Mostrar texto editável e prévia; **ela confirma antes de enviar**.
4. **Cobrança de parcela:** em Contas a receber, botão por linha e "cobrar vencidas" (seleciona várias, mostra lista e confirma). Texto com valor atualizado (juros/multa) e vencimento. Nunca insistir automaticamente.
5. **Lembretes** (opcionais, desligados por padrão): evento em D-N dias; parcela a vencer em D-N dias. Como é um app desktop, rodam **só enquanto o sistema está aberto**: ao abrir e a cada hora, listar pendências, mostrar uma tela "Mensagens pendentes de envio" para ela revisar e confirmar. Evitar duplicidade (não reenviar o mesmo lembrete).
6. **Histórico:** aba/lista "Mensagens enviadas" (filtro por cliente, tipo, status, período), com reenvio das que falharam.

Regras:
- Falha de envio nunca bloqueia salvar agendamento, faturar ou baixar. A mensagem fica `F` com o erro e pode ser reenviada.
- Sem conexão com a internet ou WhatsApp desconectado: botões continuam visíveis, mas explicam o estado e oferecem "copiar texto" como alternativa.
- Sem resposta/recebimento de mensagens na v1 (só envio). Se o cliente responder, ela vê no celular dela.
- Todos os textos em português; botão de envio no mesmo estilo dos demais; visual nos dois temas.

## A5. Instalador

- Incluir `Whatsapp\` (node portátil, `server`, `node_modules`) no `FestAgenda.iss`. Pasta `Whatsapp\sessao\` com `users-modify`, **fora da sobrescrita** nas atualizações e **preservada** ao desinstalar.
- `build-instalador.ps1`: acrescentar `npm ci --omit=dev` e a cópia do Node portátil (baixar a versão fixada e conferir o hash SHA-256 no script).
- Tamanho final do instalador: informar o novo tamanho.
- Testar em máquina limpa: instalar → ativar WhatsApp → QR → enviar texto → enviar PDF → fechar o app (conferir no Gerenciador de Tarefas que o Node encerrou) → reabrir (sessão persiste sem novo QR) → atualizar por cima (sessão preservada).

## A6. Testes (obrigatórios)

- Node: testes (Node test runner ou Jest) para normalização de telefone, validação de caminho, autenticação por token, limite diário e fila. Um **modo simulado** (`WHATSAPP_MOCK=1`) que responde sem conectar no WhatsApp, para testar o app e a CI.
- C#: testes do serviço (montagem de mensagem com variáveis, regras de elegibilidade, deduplicação de lembretes, tratamento de falha do servidor).
- Roteiro manual com um número real de teste: texto, PDF, número sem WhatsApp, número com nono dígito, WhatsApp desconectado no meio, internet caindo, desligar o Node à força.

---

# PARTE B – Finalização do projeto

## B1. Fechamento técnico

- Resolver todos os **bloqueadores de release** do `docs/relatorio-revisao.md`; os demais viram issues/lista de pendências.
- Decidir (ou registrar como decisão do grupo) os pontos em aberto: biblioteca de controles, MVVM, licença QuestPDF, visões Semana/Dia da agenda, backup.
- **Backup:** mesmo fora do escopo, implementar o mínimo seguro: lembrete ao fechar/abrir ("último backup há N dias") e botão **"Fazer backup agora"** em Configurações que usa o `gbak` de `Utilitarios\` e grava em `Backups\` com data no nome. Se o grupo não aprovar, deixar documentado como risco aceito.
- Troca da senha padrão do Firebird (`masterkey`) na instalação, ou aviso claro; senha no `config.ini` com permissão restrita.
- Versão **1.0.0** definida em um único lugar (`Directory.Build.props`) e refletida no executável, no instalador, na splash e em Sobre.
- Tela **Sobre:** versão, grupo, curso, orientador (UnC), contato de suporte.
- Verificar licenças de terceiros (QuestPDF, Baileys, Node, Firebird, Inter, ícones, QRCoder) e gerar `Licencas.txt` incluído no instalador.

## B2. Documentação

- `README.md`: visão geral, requisitos, como compilar, rodar, testar, gerar o instalador.
- `docs/manual-do-usuario.md` (e PDF): passo a passo para a cliente, com prints — primeiros passos, cadastros, agendar, faturar, receber, cancelar, relatórios, catálogo, WhatsApp, backup, tema.
- `docs/manual-tecnico.md`: arquitetura, estrutura de pastas, banco, Node, build, atualização do esquema, resolução de problemas (banco não abre, WhatsApp não conecta, porta ocupada).
- `docs/decisoes.md`: decisões e riscos aceitos (WhatsApp não oficial, sem login, sem backup automático etc.).
- Atualizar `modelagem-banco.md`, `fluxo-sistema.md`, `stack-projeto.md` e `design-interface.md` com tudo o que mudou (14 tabelas, WhatsApp, nova paleta, estrutura `C:\FestAgenda\`). Novos RN/RF numerados continuando a sequência (ex.: RF24 em diante).
- `CHANGELOG.md`.

## B3. Entrega

1. Merge de `feature/whatsapp` em `main` depois de todos os testes passarem.
2. Tag `v1.0.0` no Git.
3. `FestAgenda-Setup-1.0.0.exe` gerado pelo script e testado em instalação limpa **e** em atualização sobre versão anterior.
4. Pasta de entrega: instalador, manuais, `Licencas.txt`, hash SHA-256 do instalador.
5. Checklist final de aceite assinado pelo grupo: cada RF e RNF marcado como atendido, com onde foi testado.

## Resposta final esperada

- O que foi implementado e onde (arquivos principais).
- Resultado dos testes (automatizados e manuais) e prints das telas novas nos dois temas.
- Decisões que dependem do grupo/cliente, cada uma com a pergunta e a recomendação.
- Riscos conhecidos (em especial o do WhatsApp não oficial) e como foram mitigados.
- Lista do que ficou fora da v1.
