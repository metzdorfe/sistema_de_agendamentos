# Divergências entre documentos e código – Fest Agenda

Contradições encontradas e como foram resolvidas. Ordem de prioridade dos documentos: ver `CLAUDE.md`.

| # | Onde | O que diz | O que vale | Motivo |
|---|---|---|---|---|
| 1 | `ajustes.md` × `prompt-whatsapp-finalizacao.md` | WA-AKG × Baileys | Baileys | WA-AKG exige Next.js e MySQL/PostgreSQL. Decisão de 18/10/2026 (`docs/decisoes.md`). |
| 2 | `ajustes.md` (RN29) × `prompt-whatsapp-finalizacao.md` | Limite de 10 mensagens/dia fixo × configurável com padrão 100 | Configurável, padrão 10 | Decisão de 18/10/2026. `fluxo-sistema.md` atualizado. |
| 3 | `ajustes.md` × `prompt-whatsapp-finalizacao.md` | Tabela `MENSAGEM_WHATSAPP` × `WHATSAPP_MENSAGEM` | `WHATSAPP_MENSAGEM` | Especificação mais detalhada. Registrado no `modelagem-banco.md`. |
| 4 | `ajustes.md` × `CLAUDE.md` | Sessão em `Arquivos\WhatsApp\sessao` × `Whatsapp\sessao` | `Whatsapp\sessao` | Fica junto do servidor; `Arquivos\WhatsApp` guarda só os PDFs enviados. |
| 5 | `ajustes.md` × `prompt-whatsapp-finalizacao.md` | Chave e porta do WhatsApp no `config.ini` × token novo a cada início e porta automática | Token novo e porta automática | Mais seguro: nada fixo em arquivo. O `config.ini` continua só com o banco. |
| 6 | `prompt-whatsapp-finalizacao.md` × `ajustes.md` | Logs em `Logs\` × `Log\` | `Log\` | É a pasta que o instalador já cria e preserva. |
| 7 | `prompt-whatsapp-finalizacao.md` | `CONFIGURACAO.WHATSAPP_PORTA`, `..._LEMBRETE_EVENTO_DIAS`, `..._AVISO_VENCIMENTO_DIAS` | Não criados | Porta é automática; lembretes e cobrança ficaram fora desta entrega. |
| 8 | `design-interface.md` (roxo/neon, depois roxo nos fundos) × visual atual | — | Paleta neutra com roxo suave (18/10/2026) | Pedido da cliente/grupo. `design-interface.md` e `CLAUDE.md` atualizados. |
| 9 | `fluxo-sistema.md` (RN26) | Estorno de "pagamento à vista ou sinal" | À vista = parcela recebida no faturamento; estorna como baixa (RN21) | No código, o à vista sempre vira parcela paga no RECEBER. |
| 10 | `prompt-revisao-qualidade.md` | Faturado imutável "em todas as camadas (inclusive update direto)" | Garantido na aplicação (service + tela), sem trigger | Regra 4 do `CLAUDE.md`: regras na aplicação, não em trigger. Risco aceito. |
| 11 | `prompt-revisao-qualidade.md` | Cita `prompt-ajustes-fest-agenda.md` | `ajustes.md` | O arquivo anterior não existe mais; o prompt foi corrigido. |
| 12 | Tema de fontes no código × documentos | Fontes por `pack://application:,,,/Assets/Fonts/#...` | Caminho relativo `../Assets/Fonts/#...` | A forma antiga não carregava as fontes embutidas. `stack-projeto.md` atualizado. |
