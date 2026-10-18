# Relatório da revisão de qualidade – Fest Agenda (18/10/2026)

Roteiro: `.claude/prompt-revisao-qualidade.md`. Automatizados: 66 testes C# (xUnit, Firebird real) e 23 do servidor do WhatsApp (Node), todos passando. Build Release com 0 avisos.

| ID | Severidade | Área | Descrição | Arquivo | Status |
|---|---|---|---|---|---|
| Q01 | Alto | Financeiro | Estorno de baixa e de sinal (RN21/RN26) não existia | Services/FinanceiroService.cs | Corrigido |
| Q02 | Alto | Financeiro | Sinal podia passar do total (cliente pagando a mais sem registro) | Services/AgendamentoService.cs | Corrigido (RN32) |
| Q03 | Alto | Banco | 57 campos sem `COMMENT ON` | Database/schema.sql, alteracoes-2026-10-18.sql | Corrigido |
| Q04 | Alto | Robustez | Sistema podia ser aberto duas vezes; a 2ª janela não abria o banco embarcado e mostrava erro técnico | App.xaml.cs | Corrigido (traz a janela aberta para a frente) |
| Q05 | Alto | Interface | Fontes embutidas (Inter e Pacifico) não carregavam; o Windows usava fonte do sistema | Themes/Estilos.xaml | Corrigido |
| Q06 | Alto | Instalador | Instalador levaria o SDK do .NET (205 MB) e o instalador do Firebird | build-instalador.ps1 | Corrigido |
| Q07 | Médio | Financeiro | Multa/juros arredondavam o 5 para o par | Services/FinanceiroService.cs | Corrigido |
| Q08 | Médio | Relatórios | Relatório contava como pago o valor devolvido no cancelamento | Services/AgendamentoService.cs | Corrigido |
| Q09 | Médio | Privacidade | CPF/telefone podiam ir inteiros para o Log\Erros.log (mensagens do banco trazem a chave duplicada) | Services/ErroLog.cs | Corrigido (mascarado) |
| Q10 | Médio | Robustez | Erros não tratados não eram gravados em arquivo | Services/ErroLog.cs, App.xaml.cs | Corrigido |
| Q11 | Médio | Desempenho | Consultas no banco rodam na thread da tela (síncronas) | Views/* | Pendente (com 5.000 clientes e 10.000 agendamentos as consultas levam < 25 ms) |
| Q12 | Baixo | Faturamento | Parcela "recebida hoje" podia gravar outro vencimento se editado | Views/FaturamentoWindow.xaml.cs | Corrigido |
| Q13 | Baixo | Código | `EmConstrucaoView` sem uso | Views/ | Corrigido (removida) |
| Q14 | Baixo | Agendamento | Erro de regra ao cancelar caía na janela genérica | App.xaml.cs | Corrigido (vira aviso) |
| Q15 | Alto | Segurança | Senha padrão do Firebird (`masterkey`) no config.ini | config.exemplo.ini | Decisão do grupo |
| Q16 | Alto | Dados | Sem backup automático nem lembrete | — | Decisão do grupo |
| Q17 | Médio | Licença | QuestPDF Community: permitida para empresa com receita anual < US$ 1 mi; confirmar com a cliente | App.xaml.cs | Decisão do grupo |
| Q18 | Médio | Regra | "Faturado imutável" é garantido na aplicação, não por trigger (regra do projeto) | — | Aceito |

Verificado sem defeito: CHECKs do banco recusam configuração com ID 2, CPF/CNPJ com 12 dígitos, cancelamento sem motivo, status inválido, limite do WhatsApp zero e lançamento com duas origens; SQL só por LINQ/parâmetros; dinheiro sempre `decimal`; operações financeiras em transação; config.ini ausente/inválido e banco inacessível mostram mensagem clara na splash; logo apagada vira placeholder.

## Não executado (precisa de máquina/pessoa)

- Instalação em máquina limpa (Windows sem .NET e sem Firebird), atualização por cima e desinstalação.
- Abrir o sistema duas vezes de verdade (Q04 compilado e revisado, não executado).
- Passada visual em todas as telas nos dois temas, monitor 125%/150% e janela mínima.
- WhatsApp: estados "QR nítido" e "conectado com check" na tela.

## Documentos desta revisão

- `docs/plano-testes-manuais.md` – roteiro para a cliente/grupo validarem numa instalação limpa (cobre os itens "não executado" acima).
- `docs/divergencias.md` – contradições entre documentos e código.
- `README.md` – como compilar, rodar, testar e gerar o instalador.
