# Ajustes v2 – Fest Agenda

Continuação do `prompt-ajustes-fest-agenda.md`. Onde este arquivo contradiz o anterior, **vale este** (tema, splash, pasta `Log`, WhatsApp). Antes de começar, leia a solução atual, os temas XAML e a estrutura do `AppPaths`, e não quebre o que já funciona. Compile a cada bloco.

## Decisões fechadas

| # | Decisão |
|---|---|
| 1 | Volta o **roxo**, **sem glow e sem gradiente**. A navbar acompanha o tema: tema claro = navbar clara; tema escuro = navbar escura. |
| 2 | **WhatsApp entra na v1.** Limite de **10 mensagens por dia, fixo no código** (não configurável). |
| 3 | Pasta de log: **`C:\FestAgenda\Log\`**, na raiz da instalação (não dentro de `Arquivos\`). |
| 4 | Relatório de faturamento: definido na seção 6. |
| 5 | Filtros **"Hoje"** e **"Esta semana"** entram em **Financeiro → Visão geral**. |

---

## 1. Tema roxo, sem glow

- Reverter `Themes/Light.xaml` e `Themes/Dark.xaml` para a paleta roxa do `design-interface.md`, mantendo as mesmas chaves (`CorFundo`, `CorSuperficie`, `CorBotaoPrincipal`...). Remover qualquer `DropShadowEffect` colorido, gradiente e borda brilhante (a regra do prompt anterior continua).
- Valores:

| Chave | Claro | Escuro |
|---|---|---|
| CorFundo | `#F7F5FB` | `#000000` |
| CorSuperficie (cards, painéis, **navbar**) | `#FFFFFF` | `#10002B` |
| CorSuperficieElevada | `#F3EBFD` | `#240046` |
| CorBorda | `#E4DCEF` | `#3C096C` |
| CorBotaoSecundario | `#EDE3FA` | `#5A189A` |
| CorBotaoPrincipal | `#7B2CBF` | `#7B2CBF` |
| CorBotaoPrincipalHover / CorFoco | `#5A189A` | `#9D4EDD` |
| CorDestaque (links, ícones ativos) | `#5A189A` | `#C77DFF` |
| CorTexto | `#1A0B2E` | `#F2EEF7` |
| CorTextoSecundario | `#6B5B7E` | `#A99BB8` |

- Foco de campo e item ativo da navbar: **borda/barra sólida** na cor de destaque, sem brilho.
- Tema padrão continua o **claro** (`CONFIGURACAO.TEMA = 'C'`).
- Status (âmbar, verde, vermelho) continuam, em tons suaves.

## 2. Logo da UnC e ícone

- Gerar uma versão **branca** da logo da UnC (PNG transparente) em `Assets/Brand/`. Manter a original colorida.
- **Tema escuro** (navbar, janelas): logo branca. **Tema claro**: logo original colorida, porque a branca some em fundo claro. A troca acompanha o tema (usar chave de recurso, por exemplo `ImagemLogoUnc`, definida em cada tema).
- **Splash:** sempre escura, com a logo branca.
- **`app.ico`:** símbolo branco sobre quadrado arredondado roxo `#7B2CBF`, para aparecer em barra de tarefas clara ou escura. Tamanhos 16, 32, 48 e 256. Nos tamanhos pequenos, só o símbolo.

## 3. Agenda

- **Mês, Semana e Dia funcionando** nos botões do topo.
  - **Mês:** como já está.
  - **Semana:** 7 colunas (dom–sáb) com os agendamentos do dia como cards (hora início–fim, título, cliente), na cor do status. Setas `‹ ›` avançam uma semana.
  - **Dia:** lista vertical por horário, com os mesmos cards e o status real. Setas `‹ ›` avançam um dia. Evento que passa da meia-noite aparece no dia em que começa (RN28).
  - Clicar no card abre o agendamento (popup). O botão "Hoje" volta para a data atual na visão ativa.
  - Lembrar a visão escolhida enquanto o app estiver aberto.

## 4. Campos: limites, máscaras e validação

Definir `MaxLength` igual ao tamanho do VARCHAR no DDL. Esta tabela **fecha o ponto "tamanhos dos VARCHAR"** dos docs; atualize o DDL e as configurações do EF.

| Campo | Banco | Tela |
|---|---|---|
| CLIENTE.NOME | VARCHAR(100) | MaxLength 100 |
| CLIENTE.TELEFONE | VARCHAR(11), só dígitos | máscara `(99) 99999-9999` (10 dígitos: `(99) 9999-9999`); aceita só números |
| CLIENTE.CPF_CNPJ | VARCHAR(14), só dígitos | máscara dinâmica: `000.000.000-00` até 11 dígitos, `00.000.000/0000-00` com 12 a 14; valida dígitos verificadores ao sair do campo |
| CLIENTE.EMAIL | VARCHAR(100) | MaxLength 100; valida formato |
| LOGRADOURO / COMPLEMENTO / BAIRRO / CIDADE | VARCHAR(100) / (60) / (60) / (60) | MaxLength igual |
| NUMERO | VARCHAR(10) | MaxLength 10 |
| UF | CHAR(2) | ComboBox com as 27 UFs |
| CEP | CHAR(8) | máscara `00000-000` |
| AGENDAMENTO.TITULO | VARCHAR(80) | MaxLength 80 |
| AGENDAMENTO.QUANTIDADE_CONVIDADOS | SMALLINT | só dígitos, até 5 caracteres, limite 1–32767 |
| AGENDAMENTO.MOTIVO_CANCELAMENTO | VARCHAR(200) | MaxLength 200, contador de caracteres |
| AGENDAMENTO.OBSERVACAO | VARCHAR(500) | MaxLength 500 |
| Data | DATE | DatePicker + máscara `dd/MM/yyyy`; rejeita data inválida |
| Hora início / fim | TIME | máscara `HH:mm`, 00:00–23:59, rejeita `25:99` |
| Valores (R$) | NUMERIC(18,2) | só dígitos e vírgula, 2 casas, máximo 9.999.999,99, não aceita negativo |
| Percentuais | NUMERIC(5,2) | 0–100, 2 casas |
| SERVICO.NOME / FAIXA.NOME / ADICIONAL.NOME / ESPECIE.DESCRICAO | VARCHAR(80) | MaxLength 80 |
| DESCRICAO (serviço, adicional) | VARCHAR(255) | MaxLength 255 |

- Bloquear a digitação inválida (`PreviewTextInput` e colagem), em vez de só mostrar erro depois. Colar texto com máscara deve limpar para dígitos.
- Criar controles/behaviors reutilizáveis (`MascaraTelefone`, `MascaraCpfCnpj`, `MascaraHora`...), sem repetir código por tela.
- No banco continua **só dígitos**; a máscara é só da tela.
- Telefone formatado também em grids, painel do dia e no agendamento.

## 5. Clientes

- **Grid:** remover as colunas "Total de eventos" e "Último evento". Colunas: Nome, Telefone (formatado), CPF/CNPJ, ações.
- CPF/CNPJ vazio (`NULL`) mostra **"CPF/CNPJ não informado"** em cinza e itálico. É só exibição, **nunca gravar esse texto**. Preenchido, mostra formatado.
- Ações por linha e no cadastro: **"Enviar catálogo"** (seção 8). Desabilitado, com tooltip, se o cliente não tem telefone.
- A busca continua por nome, telefone e CPF/CNPJ (comparar só os dígitos).

## 6. Relatórios

A tela **Relatórios** lista três relatórios. Ao clicar em um:

1. Abre um **popup de filtros** (modal, mesmo visual do popup de agendamento).
2. O usuário preenche e escolhe o formato: botões **"Gerar PDF"** e **"Gerar Excel (XLSX)"**.
3. Abre o diálogo de salvar; o relatório é gerado e o app oferece abrir o arquivo.

| Relatório | Filtros | Conteúdo |
|---|---|---|
| **Agendamentos** | Período, Cliente (todos ou um), Status (todos ou um), Serviço (todos ou um) | Data, hora, título, cliente, serviço, status, total, pago, saldo; totais no fim |
| **Caixa por cliente** | Cliente (todos ou um), Período | Entradas agrupadas por cliente, com total recebido e pendente |
| **Faturamento por período** (novo) | Período | Resumo **por mês**: nº de agendamentos faturados, total faturado, recebido, em aberto; depois o detalhe por agendamento |

- Faturamento: considerar agendamentos com status **Faturado** pela `DATA_EVENTO` (não existe data de faturamento no modelo). Cancelados ficam fora.
- **PDF:** QuestPDF, com cabeçalho da empresa (nome, logo), filtros usados e data de geração. **XLSX:** pacote **ClosedXML** (MIT); cabeçalho em negrito, primeira linha congelada, colunas ajustadas, valores como **números** com formato `R$ #,##0.00` e datas como data (não texto), linha de totais.
- Criar uma interface única de relatório (por exemplo `IRelatorio` com `GerarPdf(filtros)` e `GerarXlsx(filtros)`), para o popup ser genérico e cada relatório só declarar seus filtros.
- Período: "de" e "até" obrigatórios; "de" não pode ser maior que "até".
- Sem dados no filtro: avisar "Nenhum registro para os filtros informados" e não gerar arquivo.
- **O acesso ao catálogo em PDF** passa a ser por "Enviar catálogo" no cliente (seção 8) e pode ganhar uma entrada em Relatórios (gera o PDF sem enviar). Resolve o `// TODO` do prompt anterior.

### RelatorioLog

- Arquivo `C:\FestAgenda\Log\RelatorioLog.log`, em `AppPaths.Log` (criar a pasta se não existir). Anexa uma linha por geração, UTF-8, com proteção de concorrência (`lock`):

```
2026-10-04 22:30:15 | Agendamentos | PDF  | periodo=01/10/2026-31/10/2026; cliente=Todos; status=Todos; servico=Salão Principal | OK    | C:\Users\...\agendamentos.pdf
2026-10-04 22:31:02 | Faturamento  | XLSX | periodo=01/01/2026-30/09/2026 | ERRO  | arquivo em uso
```

- Registrar **sucesso e falha**, com os filtros usados e o formato. Falha ao gravar o log nunca pode impedir o relatório.
- Instalador: adicionar `Log\` à estrutura, com permissão `users-modify`, e **não apagar** na desinstalação.

## 7. Financeiro → Visão geral

- Acrescentar **"Hoje"** e **"Esta semana"** à lista de períodos do filtro (junto de Este mês, Personalizado etc.). Semana de domingo a sábado, como no calendário.
- Os cards (faturado, recebido, em aberto), o gráfico e os últimos lançamentos respeitam o período escolhido.

## 8. WhatsApp

### Antes de codar
Leia o README e a documentação de **https://github.com/mrifqidaffaaditya/WA-AKG** e confirme: runtime exigido (se for Node, definir como embutir), como iniciar o serviço, autenticação (chave/token), endpoints de **QR code**, **status da sessão**, **envio de texto**, **envio de documento (PDF)** e **logout**, e a licença. Se algo não bater com o que está descrito aqui, **pare e me avise** antes de seguir. A API é **não oficial**: avisar na tela de configuração que existe risco de bloqueio do número e que o uso deve ser só com clientes que já têm contato com a empresa.

### Integração
- Criar `Services/WhatsApp/` com `IWhatsAppService` (`ObterStatus`, `ObterQrCode`, `EnviarDocumento(telefone, arquivo, legenda)`, `Desconectar`) e a implementação para o WA-AKG. Nenhuma tela chama HTTP direto.
- O serviço do WA-AKG roda **local**, escutando só em `127.0.0.1`, iniciado pelo Fest Agenda e encerrado junto com ele. Sessão persistida em `C:\FestAgenda\Arquivos\WhatsApp\sessao\`. Se for Node, incluir um runtime portátil em `C:\FestAgenda\WhatsApp\` e adicionar ao instalador.
- Chave/porta do serviço gerada na instalação e guardada no `config.ini` (seção `[WhatsApp]`), nunca no código.
- O número de envio é o telefone do cliente com DDI: `55` + dígitos gravados.
- Todas as chamadas com timeout e tratamento de erro; **o salvamento de agendamento e cliente nunca depende do WhatsApp**.

### Limite diário (RN29)
- **10 mensagens por dia, constante no código** (`LimiteMensagensDia = 10`), sem campo na tela e sem coluna no banco.
- Conta as mensagens com status "enviada" na data de hoje. Ao atingir o limite: bloquear o envio com a mensagem "Limite de 10 mensagens por dia atingido. Tente novamente amanhã." Mostrar na tela de Envios o contador "Hoje: N/10".

### Banco (nova tabela – 13ª)

```sql
CREATE DOMAIN D_TIPO_MENSAGEM   AS CHAR(1) NOT NULL CHECK (VALUE IN ('O','C'));  -- O orçamento, C catálogo
CREATE DOMAIN D_STATUS_MENSAGEM AS CHAR(1) NOT NULL CHECK (VALUE IN ('E','F'));  -- E enviada, F falha

CREATE TABLE MENSAGEM_WHATSAPP (
  ID               INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
  CLIENTE_ID       INTEGER NOT NULL REFERENCES CLIENTE(ID),
  AGENDAMENTO_ID   INTEGER REFERENCES AGENDAMENTO(ID),   -- preenchido só no orçamento
  TIPO             D_TIPO_MENSAGEM,
  DATA_HORA_ENVIO  TIMESTAMP NOT NULL,
  TELEFONE         VARCHAR(13) NOT NULL,                 -- número usado no envio, com DDI
  STATUS           D_STATUS_MENSAGEM,
  MENSAGEM_ERRO    VARCHAR(200)
);
CREATE INDEX IDX_MENSAGEM_WHATSAPP_DATA ON MENSAGEM_WHATSAPP(DATA_HORA_ENVIO);
```

- Adicionar `COMMENT ON`, entidade, mapeamento EF e o relacionamento no diagrama do `modelagem-banco.md`. Registrar também tentativas com falha (status F), que **não** contam para o limite.

### Telas
- **Configurações → nova aba "WhatsApp":**
  - **Desconectado:** QR code para escanear, com instruções curtas e atualização automática do QR quando expirar.
  - **Conectado:** o QR code aparece **borrado**, com uma caixa verde **"Logado ✔"** e, ao lado, o botão **"Envios"**. Também um botão discreto **"Desconectar"**.
  - Verificar o status ao abrir a aba e a cada poucos segundos enquanto ela estiver aberta. Nunca pedir novo QR se já está logado.
  - **"Envios"** abre um popup com a lista: data/hora, cliente, **o que foi enviado** ("Orçamento enviado" ou "Catálogo enviado"), status (Enviada / Falha) e o contador "Hoje: N/10". Ordem do mais recente.
- **Cliente → "Enviar catálogo":** gera o PDF do catálogo (serviços ativos com faixas ativas e adicionais, com preços atuais, no formato do `fluxo-sistema.md`), pede confirmação e envia.
- **Agendamento → "Enviar orçamento":** botão no rodapé do popup, habilitado depois de salvo. Gera um PDF do orçamento (QuestPDF: empresa, cliente, evento, data e horário, serviços, adicionais, desconto, total e sinal) e envia.
- **Ao clicar "Salvar orçamento":** salva primeiro; depois abre o popup **"Deseja enviar o orçamento para o cliente pelo WhatsApp?"** com **Sim** e **Agora não**. Se escolher Sim, segue o envio acima.
- **Falhas:** WhatsApp desconectado, cliente sem telefone, limite atingido ou erro de rede mostram aviso claro, sem desfazer o que já foi salvo. Se desconectado, o aviso oferece abrir Configurações → WhatsApp.
- Mensagem de legenda curta e com o nome da empresa. Ex.: "Olá, {cliente}! Segue o orçamento do seu evento. {empresa}".

## 9. Tela Sobre

- Remover a lista de tecnologias utilizadas.
- Conteúdo:
  - **Curso:** Engenharia de Software
  - **Grupo:** Bruno Balbinott, Eduardo Metzdorf e Marco Dall Bello
  - **Professores:** Eduardo Pilonetto e Moacir Kichel
  - **Versão** (vinda do projeto)
- Frase de agradecimento no lugar das tecnologias, por exemplo: *"Feito com dedicação para transformar cada festa em uma boa história. Obrigado por fazer parte do nosso projeto."* (ajuste ao gosto do grupo).

## 10. Instalador e estrutura

Estrutura final:

```
C:\FestAgenda\
├── FestAgenda.exe  e  *.dll
├── config.ini            [Banco] e [WhatsApp]
├── BD\FESTAGENDA.FDB
├── Arquivos\             Empresa\, WhatsApp\sessao\, ...
├── Log\                  RelatorioLog.log
├── WhatsApp\             serviço do WA-AKG (e runtime, se necessário)
└── Utilitarios\
```

- Adicionar `AppPaths.Log` e `AppPaths.WhatsApp`.
- `Log\` e `Arquivos\WhatsApp\` com `users-modify`; ambos preservados na desinstalação e na atualização.
- Regerar o instalador e repetir o teste da instalação limpa (instalar → abrir → cadastrar cliente → agendar → gerar relatório (confere o log) → atualizar por cima → dados preservados → desinstalar → `BD\`, `Arquivos\` e `Log\` preservados).

## 11. Atualização dos docs

**`modelagem-banco.md`:** 13 tabelas; novo grupo "Comunicação" com `MENSAGEM_WHATSAPP`; domínios novos; tamanhos dos VARCHAR (seção 4) e fechamento do ponto em aberto; prefixo `CAMINHO_`; `CAMINHO_LOGO_EMPRESA` no lugar do BLOB; tema padrão `C`.

**`fluxo-sistema.md`:** nova tela de relatórios com popup e formatos; Visão geral com Hoje/Esta semana; fluxo de envio de catálogo e orçamento; popup após "Salvar orçamento"; Semana e Dia entram na v1 (sai de "Pontos em aberto"); WhatsApp sai de "Fora do escopo". Regras e requisitos novos:

- **RN29** – No máximo 10 mensagens de WhatsApp enviadas por dia (valor fixo).
- **RN30** – Falha ou ausência do WhatsApp nunca impede salvar cliente ou agendamento.
- **RN31** – Toda geração de relatório (sucesso ou erro) é registrada no `RelatorioLog`.
- **RF17/RF18** – atualizar com os filtros e os formatos PDF e XLSX.
- **RF24** – Calendário nas visões Mês, Semana e Dia.
- **RF25** – Relatório de faturamento por período.
- **RF26** – Conectar o WhatsApp por QR code, ver o estado e desconectar.
- **RF27** – Histórico de envios (cliente, tipo, data/hora, status).
- **RF28** – Enviar catálogo ao cliente; **RF29** – enviar orçamento; **RF30** – oferecer o envio ao salvar o orçamento.
- **RNF14** – Campos com limite de tamanho, máscara e validação de entrada.
- **RNF15** – Exportação XLSX com ClosedXML.
- **RNF16** – Serviço de WhatsApp local, só em 127.0.0.1.

**`design-interface.md`:** paleta roxa sem glow, navbar acompanhando o tema, logo UnC branca/colorida, splash sempre escura, ícone roxo com símbolo branco, aba WhatsApp (QR borrado + "Logado ✔"), popup de filtros dos relatórios, tela Sobre.

**`stack-projeto.md`:** pacotes ClosedXML e (se couber) cliente HTTP do WhatsApp; pastas `Services/WhatsApp/` e `Reports/`; `Log\`; WA-AKG na stack.

## Entrega

- Compilando sem warnings novos.
- Prints de: Agenda (Mês, Semana, Dia) nos dois temas, Clientes, popup de relatório, aba WhatsApp (desconectado e logado), popup pós-orçamento, Sobre.
- `RelatorioLog.log` com exemplos de sucesso e erro.
- Lista de pendências e dúvidas (principalmente o que a leitura do WA-AKG revelar).