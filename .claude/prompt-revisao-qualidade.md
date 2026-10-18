# Prompt – Revisão geral de qualidade (QA) do Fest Agenda

Você é o responsável de qualidade do **Fest Agenda**. Leia o `CLAUDE.md` e os documentos (`fluxo-sistema.md`, `modelagem-banco.md`, `stack-projeto.md`, `design-interface.md`, `ajustes.md`) antes de começar. Audite o sistema **como está no código**, compare com a especificação e corrija o que for defeito claro. O que for decisão de negócio, não decida: registre.

## Regras da revisão

- Não confie no documento: confirme no código e **execute** (compile, rode, teste). Cite arquivo e linha em cada achado.
- Corrija direto: bugs óbvios, violações de convenção, regra de negócio implementada errada, warnings. Um commit por correção.
- Não corrija sem perguntar: mudança de regra de negócio, mudança de modelo de dados fora do previsto, remoção de funcionalidade.
- Primeiro produza o relatório; depois corrija por ordem de severidade.

## Entregáveis

1. `docs/relatorio-revisao.md` com tabela: ID · Severidade (Crítico/Alto/Médio/Baixo) · Área · Descrição · Arquivo:linha · Status (corrigido / pendente / decisão do grupo).
2. Correções commitadas.
3. Projeto de testes (`FestAgenda.Tests`, xUnit) cobrindo as regras da seção 2.
4. `docs/plano-testes-manuais.md`: roteiro passo a passo para a cliente/grupo validar numa instalação limpa.
5. `docs/divergencias.md`: contradições entre documentos e código.
6. Resumo final: o que foi corrigido, o que depende do grupo, riscos restantes.

## 1. Build e saúde do projeto

- `dotnet build -c Release` sem warnings; listar e tratar os existentes.
- Pacotes NuGet: versões compatíveis com .NET 10, sem pacote não usado, licenças (**QuestPDF Community: confirmar se o uso é permitido** para este caso e registrar).
- Segredos ou caminhos pessoais no repositório; `.gitignore` cobre `.fdb`, `config.ini`, `bin/`, `obj/`, `.vs/`.
- Código morto, TODOs, `MessageBox` de erro de campo, `Thread.Sleep`, `async void` fora de eventos, exceções engolidas (`catch {}`).
- Operações de banco na thread da UI (travamento): tudo que consulta ou grava deve ser `async`.

## 2. Regras de negócio (testar cada uma)

Criar testes automatizados nos Services para:

| Regra | Casos mínimos |
|---|---|
| RN01 CPF/CNPJ | válido, inválido, dígitos repetidos (`111.111.111-11`), com máscara, duplicado, vazio |
| RN03/05 | serviço sem faixa ativa não aparece; agendamento sem serviço é recusado |
| RN04 | adicional por quantidade × fixo |
| RN06/08 | valor da faixa editável; mudar o cadastro depois **não** altera agendamento existente |
| RN09 | total = serviços + adicionais − desconto; desconto maior que o subtotal; total nunca negativo |
| RN10/11 | bloqueio por **serviço**; orçamento com sinal bloqueia, sem sinal não; com bloqueio desligado nada bloqueia; conversão revalida; **editar o próprio agendamento não pode bloquear a si mesmo**; cancelado não bloqueia |
| RN12 | sinal vai ao FINANCEIRO e não cria RECEBER |
| RN13–17 | desconto só até faturar; soma das formas = total − sinais (centavos!); espécie sem parcelamento só 1 parcela; hoje → caixa, futuro → RECEBER; faturado imutável em **todas** as camadas (UI, service, e tentativa direta de update) |
| Parcelamento | R$ 100,00 em 3x = 33,33 + 33,33 + 33,34 (soma exata); vencimentos mensais em fim de mês (31/01 → 28/02) |
| RN18 | exemplo do documento (1.000 / 2% / 2% / 15 dias = 1.030,00); parcela no dia do vencimento sem atraso; 1 dia de atraso; arredondamento |
| RN19 | baixa parcial com e sem parcela de diferença; valor recebido zero ou maior que o total |
| RN20 | espécie da baixa diferente da combinada |
| RN21 | estorno reabre parcela, limpa campos, lança saída, cancela parcela de diferença; estornar duas vezes |
| RN22–27 | motivo obrigatório; cancelar depois da data do evento é bloqueado; dentro/fora do prazo (dia exato do limite); devolução de sinal + parcelas pagas; parcelas abertas viram C; histórico preservado; CHECKs do banco batem com a aplicação |
| RN26 | estorno de à vista/sinal |
| RN28 | 23:00–02:00; fim igual ao início; disponibilidade usa `DATA_EVENTO` |

Verificar também: todo fluxo multi-passo (faturar, baixar, estornar, cancelar) roda em **uma transação**; falha no meio não deixa estado parcial. Valores `decimal` em todo lugar (nenhum `double`/`float`).

## 3. Banco de dados

- Rodar o DDL do zero num banco novo: sem erro; domínios, CHECKs, índices e `COMMENT ON` presentes em **todas** as tabelas e campos.
- Entidades EF × DDL: nomes, tipos, nulabilidade, tamanhos, FKs. Convenção de nomes MAIÚSCULO_COM_UNDERSCORE sem aspas funcionando (rodar SQL manual no `isql`).
- `D_TEMA` default deve ser `'C'` (tema claro padrão); `LOGO_EMPRESA` BLOB substituído por `CAMINHO_LOGO_EMPRESA VARCHAR(260)`.
- Tentar violar CHECKs direto no banco (financeiro com duas origens, cancelamento sem motivo, `CPF_CNPJ` com 12 dígitos, `CONFIGURACAO.ID = 2`).
- Índices existentes e usados nas consultas pesadas (calendário, contas a receber, caixa).
- Verificar consultas N+1 e carregamento excessivo no EF (`Include` desnecessário, `ToList` antes de filtrar).
- Concorrência: duas janelas/instâncias abertas; abrir o app duas vezes (deve avisar ou impedir).
- Migração/atualização: banco de versão anterior abre após atualizar o sistema? Existe controle de **versão do esquema** (tabela ou `PRAGMA`-equivalente) e script de upgrade? Se não, registrar como Alto.

## 4. Fluxos ponta a ponta

Executar na aplicação e anotar qualquer falha:

1. Primeira execução (banco vazio): splash → agenda vazia → configurar empresa → logo.
2. Cadastrar cliente (PF e PJ) → editar → inativar.
3. Serviço com 3 faixas + adicionais (novo e "usar existente"), reordenar faixas.
4. Orçamento sem sinal → orçamento com sinal → converter em agendado → faturar misto (pix hoje + cartão 3x) → baixar parcela em dia → baixar parcela vencida (juros e multa) → baixa parcial com diferença → estornar.
5. Cancelamento dentro e fora do prazo; conferir caixa e relatórios.
6. Tentar editar faturado; tentar agendar em dia bloqueado; evento 23:00–02:00.
7. Lucro do evento (RF16): receita − custo dos adicionais − despesas.
8. Relatório de agendamentos e de caixa por cliente: conferir totais com cálculo manual.
9. Catálogo PDF: preços atuais, logo, dados da empresa, acentos e quebra de página.
10. Trocar tema com o app aberto; "seguir o Windows".

## 5. Interface e usabilidade

- Todas as telas nos **dois temas**: nenhuma cor fixa, contraste adequado (≥ 4,5:1 no texto), nenhum controle padrão "quebrado" no escuro (DataGrid, ComboBox, DatePicker, calendário, scrollbar, tooltip, menu de contexto, popups).
- Remanescentes do visual antigo: roxo neon, glow, gradientes, balões, "Espaço de Festas" como nome do sistema, crédito da UnC na splash.
- Janela mínima 1024×640: nada cortado; navbar recolhe abaixo de ~1100 px; redimensionar e maximizar; monitor com escala 125%/150%.
- Popup de agendamento: foco inicial, Tab order, Enter/Esc, alterações não salvas, rolagem, validação inline, fechar por Esc pergunta se há alterações.
- Teclado: tudo operável sem mouse; atalhos coerentes; foco visível.
- Máscaras e formatos: CPF/CNPJ, telefone, CEP, moeda R$, datas dd/mm/aaaa; colar texto com máscara; vírgula e ponto decimais.
- Estados vazios, carregando e erro em cada lista; textos sem erro de português; terminologia consistente (Orçamento/Agendado/Faturado/Cancelado).
- Acessibilidade: status nunca só pela cor (borda tracejada, rótulo).

## 6. Robustez e erros

- Banco ausente, `config.ini` ausente ou inválido, senha errada, arquivo `.fdb` bloqueado/somente leitura, disco cheio: mensagem clara na splash, sem travar nem fechar sem aviso.
- Logo ausente (arquivo apagado): placeholder com o nome, sem exceção.
- Exceção não tratada: handler global (`DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`) que mostra mensagem amigável e grava **log em arquivo** (`Logs\` com rotação). Se não existir, implementar.
- Entradas extremas: nomes enormes, emojis/acentos, valores com 10+ dígitos, datas de 1900 e 2100, 0 convidados, quantidades negativas.
- Desempenho: base com 5.000 clientes e 10.000 agendamentos (gerar massa de teste); calendário, buscas e relatórios devem responder em < 1 s.

## 7. Instalação e distribuição

- Gerar o instalador e testar em máquina limpa (VM Windows 10/11 **sem** .NET e **sem** Firebird): instalar → abrir → usar → atualizar por cima → dados preservados → desinstalar → `BD\` preservado.
- Usuário comum (não administrador) consegue gravar em `BD\`, `Arquivos\`, `config.ini`?
- Firebird embarcado: DLLs e `plugins\`, `intl\`, `firebird.msg` presentes; **arquitetura x64 coerente** com o app; antivírus não bloqueia.
- Caminho com espaços e acentos; instalar em outro diretório que não `C:\FestAgenda`.
- `Utilitarios\*.bat`: backup, restauração, verificação funcionam e não sobrescrevem dados sem confirmar.
- Versão exibida em Sobre/splash = versão do instalador.

## 8. Segurança e privacidade (dados pessoais – LGPD)

- Senha do Firebird em texto no `config.ini`: avaliar risco e permissões do arquivo; senha SYSDBA padrão `masterkey` não pode ir para produção sem aviso ou troca.
- SQL: nada de concatenação de string em consulta; usar parâmetros/LINQ.
- Dados pessoais (CPF/CNPJ, telefone, e-mail, endereço) não aparecem em logs.
- Backup: ausência de rotina automática é risco; confirmar com o grupo se o lembrete de backup entra na v1 (registrar como decisão).

## 9. Documentação e repositório

- Documentos × código sincronizados (nomes de campos, RN, telas). Atualizar o que estiver velho (especialmente `design-interface.md`, que ainda descreve a paleta roxa).
- `README.md` na raiz: o que é, como compilar, como gerar o instalador, como rodar os testes.
- `config.exemplo.ini` atualizado; `Database/` com scripts versionados e ordem de execução.

## Formato da resposta final

Resumo curto: total de achados por severidade, o que foi corrigido, o que depende de decisão do grupo (lista objetiva, cada item com a pergunta e a recomendação), e o que impede a entrega ("bloqueadores de release").
