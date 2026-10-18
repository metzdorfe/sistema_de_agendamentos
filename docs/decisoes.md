# Decisões e riscos aceitos – Fest Agenda

Registro das decisões tomadas fora dos documentos originais, com o motivo. Mais recentes primeiro.

## 18/10/2026 – WhatsApp

| Decisão | Motivo |
|---|---|
| **Baileys** (`@whiskeysockets/baileys` 7.0.0-rc14) num servidor Node.js local, no lugar do WA-AKG previsto no `ajustes.md` | O WA-AKG exige Next.js e um servidor MySQL/PostgreSQL só para guardar a sessão, pesado demais para a máquina da cliente. O Baileys conecta direto por WebSocket, sem navegador, e a sessão fica em arquivo (`Whatsapp\sessao`). A versão 7 é a linha mantida (a 6.x virou `legacy` no npm). |
| Node **24.21.0 LTS** portátil dentro do instalador (`Whatsapp\node\node.exe`), conferido por SHA-256 no `build-instalador.ps1` | A cliente não instala Node nem npm, e a máquina dela não baixa nada além do próprio WhatsApp. A 24.22.0 tinha só 4 dias; ficou a anterior. |
| Limite diário **configurável, padrão 10** (o `ajustes.md` previa 10 fixo) | Mantém o valor seguro como padrão e permite ajustar sem nova versão. O servidor também aplica o teto e espaça os envios (6 a 15 s, um por vez). |
| Escopo: conexão por QR, catálogo, orçamento/agendamento e histórico com reenvio | Cobrança de parcela e lembretes ficaram para depois. O banco já aceita os tipos **L** e **P** e o `RECEBER_ID`. |
| Tabela `WHATSAPP_MENSAGEM` (nome do `prompt-whatsapp-finalizacao.md`), não `MENSAGEM_WHATSAPP` (`ajustes.md`) | O prompt do WhatsApp é a especificação mais detalhada e recente dessa parte. |
| O app fala com o servidor só pela interface `IWhatsappService` | Permite trocar pela API oficial (Cloud API da Meta) no futuro sem mexer nas telas. |

### Risco aceito: biblioteca não oficial

O Baileys não é oficial e o uso viola os termos do WhatsApp: **o número pode ser bloqueado**. Mitigações no sistema e na operação:

- número **dedicado ao negócio**, não o pessoal da cliente;
- só para quem já é cliente (consentimento `CLIENTE.ACEITA_WHATSAPP`, desmarcável);
- envio **sempre revisado e confirmado** por ela, nunca automático ou em massa;
- limite diário baixo (padrão 10), fila com um envio por vez e intervalo aleatório, "digitando..." antes de cada mensagem;
- aviso do risco na própria tela de Configurações → WhatsApp.

A cliente e o grupo precisam estar cientes disso **por escrito** antes da entrega.

### Segurança local

Servidor só em `127.0.0.1`, token aleatório novo a cada início (passado por variável de ambiente), corpo limitado a 64 KB, anexos só de dentro de `Arquivos\` (sem `..` nem caminho absoluto). O processo fica preso a um Job Object do Windows: morre junto com o sistema, mesmo se ele travar. Log em `Log\whatsapp.log` sem texto das mensagens e com telefones mascarados.

## 18/10/2026 – Financeiro

| Decisão | Motivo |
|---|---|
| Sinal nunca passa do que falta pagar, e o total não pode ficar abaixo do já pago (RN32) | Evitar cliente pagando a mais sem registro de crédito. A alternativa (crédito do cliente) exigiria tabela nova. |
| Estorno e devolução gravam `FINANCEIRO.LANCAMENTO_ESTORNADO_ID` apontando para a entrada | Separar estorno/devolução de despesa do evento (todas são saídas ligadas ao agendamento) e impedir desfazer a mesma entrada duas vezes. |
| Saída e suprimento não se estornam; corrige-se com o lançamento contrário (entrada avulsa ou sangria) | Mantém o CHECK "estorno é sempre saída" e o histórico intacto. |
| Suprimento e sangria marcados em `FINANCEIRO.MOVIMENTACAO_CAIXA` | Movimentam o caixa sem contar como receita ou despesa. |
