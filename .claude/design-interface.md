# Design da Interface – Fest Agenda

> **Criado em 29/09/2026.** Direção visual e estrutura das telas, para guiar o desenho no Figma e depois o WPF. Regras de negócio no `fluxo-sistema.md`; banco no `modelagem-banco.md`; stack no `stack-projeto.md`.
> **Atualizado em 29/09/2026:** revisão do primeiro protótipo no Figma, opção de **tema claro ou escuro** e mudanças no banco aprovadas pelo grupo.
> **Atualizado em 30/09/2026:** sistema passa a se chamar **Fest Agenda**; logo da **UnC** no lugar dos balões (navbar, splash, ícone); visual **clean** sem neon (base neutra + azul da UnC como única cor de destaque); **tema claro é o padrão**; splash clara, sem o crédito da UnC; "Gerar catálogo" saiu da tela do serviço; **agendamento em popup**.
> **Atualizado em 04/10/2026 (ajustes v2, vale sobre o que está acima):** volta a **paleta roxa**, sem glow e sem gradiente; a navbar acompanha o tema; logo da UnC **branca no escuro** e colorida no claro; **splash sempre escura** com a logo branca; ícone com a marca branca sobre quadrado roxo; Agenda com **Semana e Dia**; popup de filtros dos relatórios; nova tela Sobre. A aba WhatsApp entrou em 18/10/2026 (ver o fim deste documento).
> **Atualizado em 18/10/2026 (vale sobre tudo acima):** paleta **neutra com roxo suave**. Fundos e bordas em cinzas frios (claro: fundo #F7F7F8, cartão #FFFFFF, borda #E4E4E7, texto #1C1B22) ou grafite (escuro: fundo #121214, cartão #1B1B1F, borda #2E2E35, texto #ECECEF); roxo só no botão principal, item ativo da navbar, foco e seleção (#6D3FC0 no claro, #7C5AD9 no escuro). Componentes: altura 36 px, campos brancos com anel de foco roxo translúcido, abas segmentadas com a ativa em "pílula" branca sobre trilho cinza, navbar com o item ativo em fundo roxo bem claro, selos em pílula, tooltip invertido, linhas de tabela com 44 px. Splash, ícone e imagens do instalador regenerados em grafite (`Tools/gerar-marca.ps1`).

> **Atualizado em 04/10/2026 (noite):** o nome **Fest Agenda** passa a usar a fonte **Pacifico** (só o nome: navbar, splash e Sobre; o resto continua em Inter); splash redesenhada (560×340, confete em formas sólidas, nome grande, frase de apoio, UnC discreta no rodapé); popups de **faturamento** e de **quitar parcela**.

## Conceito

- **Clean, simples e profissional.** Paleta roxa com **uma cor de destaque** por tema (botão principal `#7B2CBF`; destaque `#5A189A` no claro e `#C77DFF` no escuro), usada no botão principal, item ativo, links e foco.
- **Sem cara de template:** nada de glow, gradientes nem bordas brilhantes. Foco de campo e item ativo da navbar: **borda/barra sólida**.
- **Dois temas:** claro (padrão) e escuro. Ela escolhe nas Configurações.
- **Marca do sistema:** logo da UnC + o nome **Fest Agenda**. O nome da empresa cliente vem de `CONFIGURACAO.NOME_EMPRESA` (rodapé da navbar, catálogo).
- **Navbar fixa à esquerda**, igual em todas as telas; só o conteúdo à direita muda.
- A primeira tela depois da splash é o **calendário de agendamentos**.

## Temas

### Escolha do tema

- Em **Configurações → Aparência**, três opções: **Claro** (padrão), **Escuro** e **Seguir o Windows**.
- A troca é aplicada **na hora**, sem reiniciar o sistema.
- **A splash é sempre escura**, nos dois temas, com a logo branca da UnC: a imagem estática inicial é compilada no executável, antes de o sistema saber qual tema está escolhido.

### Implementação no WPF

- Dois arquivos de recursos com **as mesmas chaves** e cores diferentes: `Themes/Dark.xaml` e `Themes/Light.xaml`.
- As telas **nunca usam cor fixa**: sempre `{DynamicResource ...}` com o nome da função (ex.: `CorFundo`, `CorSuperficie`, `CorBotaoPrincipal`, `CorTexto`).
- Trocar de tema = substituir o dicionário em `Application.Current.Resources.MergedDictionaries`.
- "Seguir o Windows": ler a preferência do sistema (chave `AppsUseLightTheme` no registro) ao abrir.
- A preferência fica gravada em `CONFIGURACAO.TEMA`.

### Tabela de cores por função

Paleta roxa (ajustes v2, 04/10/2026). A navbar usa `CorSuperficie`, então é clara no tema claro e escura no escuro.

| Função (chave do recurso) | Claro (padrão) | Escuro |
|---|---|---|
| Fundo da janela (`CorFundo`) | `#F7F5FB` | `#000000` |
| Superfície: painéis, cards, navbar (`CorSuperficie`) | `#FFFFFF` | `#10002B` |
| Superfície elevada: inputs, item ativo, hover (`CorSuperficieElevada`) | `#F3EBFD` | `#240046` |
| Bordas e divisórias (`CorBorda`) | `#E4DCEF` | `#3C096C` |
| Hover de borda / polegar da rolagem (`CorBotaoSecundario`) | `#EDE3FA` | `#5A189A` |
| Botão principal (`CorBotaoPrincipal`) | `#7B2CBF` | `#7B2CBF` |
| Hover do botão principal (`CorBotaoPrincipalHover`) e foco (`CorFoco`) | `#5A189A` | `#9D4EDD` |
| Destaque: links, ícone ativo (`CorDestaque`) | `#5A189A` | `#C77DFF` |
| Ênfase pontual: dia de hoje, total (`CorEnfase`) | `#5A189A` | `#C77DFF` |
| Texto principal (`CorTexto`) | `#1A0B2E` | `#F2EEF7` |
| Texto secundário: rótulos, ajuda (`CorTextoSecundario`) | `#6B5B7E` | `#A99BB8` |
| Dia selecionado / hoje na semana (`CorSelecao`) | `#F3EBFD` | `#1C0038` |
| Logo da UnC (`ImagemLogoUnc`, imagem) | original colorida | branca |
| Fundo escurecido atrás de popup (`CorOverlay`) | `#1A0B2E` 45% | `#000000` 70% |

**Regras de contraste:**
- Texto **branco** sobre o botão principal nos dois temas.
- A logo da UnC vem da chave `ImagemLogoUnc`, definida em cada tema: **colorida no claro** (a branca some em fundo claro) e **branca no escuro** (`Assets/Brand/*_branca.png`, gerada pelo `Tools/gerar-marca.ps1`). Nunca distorcida (`Stretch=Uniform`, `BitmapScalingMode=HighQuality`) e com respiro de pelo menos 16 px quando aparece sozinha.
- Sombra: no máximo uma sombra cinza muito suave (só nos diálogos flutuantes). No escuro, a separação é por tom e borda fina.

### Cores de apoio (status)

Usadas **só onde carregam significado**, em tons menos saturados.

| Uso | Claro | Escuro |
|---|---|---|
| Orçamento (âmbar) | `#B7791F` | `#D6A24A` |
| Pago, sucesso (verde) | `#2F855A` | `#5FB98A` |
| Vencido, erro, cancelar (vermelho) | `#C53030` | `#E27272` |

### Status no calendário

| Situação | Visual |
|---|---|
| Livre | Cor da superfície, sem destaque |
| Agendado / Faturado | Cor do botão principal (roxo) preenchida |
| Orçamento com sinal | Âmbar preenchido (bloqueia a data) |
| Orçamento sem sinal | Âmbar só na borda, tracejada (não bloqueia) |

Status nunca depende só da cor: usar legenda fixa, borda tracejada e texto, por causa de daltonismo.

## Estilo

### Tipografia

- Fonte **Inter**, gratuita e embutida no projeto WPF, para funcionar mesmo sem estar instalada na máquina.
- Números **tabulares** em valores e grades, para alinhar as casas decimais.

| Uso | Tamanho | Peso |
|---|---|---|
| Título da tela | 24 | SemiBold |
| Título de seção | 16 | SemiBold |
| Texto padrão | 14 | Regular |
| Rótulos e textos auxiliares | 12 | Regular |

### Espaçamento e forma

- Todo espaçamento em **múltiplos de 8** (8, 16, 24, 32).
- Cantos: **8px** em botões e inputs, **12px** em cards e painéis.
- **Escuro:** profundidade por tom, não por sombra (`#0F172A` → `#1E293B` → `#273449`), com borda fina quando precisar separar.
- **Claro:** cards brancos sobre fundo cinza bem claro (`#F8FAFC`), com borda fina.

### Ícones

- Um único conjunto de traço fino: **Lucide** ou **Fluent System Icons**. Não misturar estilos.

### Detalhes

- **Sem glow.** Foco dos campos = borda na cor de destaque.
- **Item ativo da navbar:** barrinha vertical à esquerda na cor de destaque e fundo de superfície elevada.
- **Validação inline:** campo com borda vermelha + mensagem curta logo abaixo; `MessageBox` nunca é usado para erro de campo.
- **Animações curtas (150–250ms):** painel do dia deslizando, hover suave, fade na troca de mês e na troca de tema.
- **Respiro:** poucas linhas de grade; nas tabelas, só divisórias horizontais bem apagadas.
- **Estados vazios** com ícone, mensagem e botão de ação (ex.: "Nenhum cliente ainda" + "Cadastrar cliente").

## Estrutura geral

```
┌──────────────┬──────────────────────────────────────────────────┐
│ [UnC] Fest   │                                                  │
│       Agenda │                                                  │
│  navbar      │           conteúdo da tela atual                 │
│  (fixa)      │                                                  │
│              │                                                  │
│ ──────────── │                                                  │
│ (logo) Nome  │                                                  │
│  da Empresa  │                                                  │
└──────────────┴──────────────────────────────────────────────────┘
```

### Navbar

```
▣ Agenda
◯ Clientes
◯ Serviços        (com adicionais e catálogo dentro)
◯ Financeiro
   · Visão geral
   · Contas a receber
   · Caixa
◯ Relatórios
◯ Configurações   (opcional: o perfil já leva para lá)
──────────
(logo) Nome da empresa  → Configurações
```

- **Rodapé da navbar: logo e nome da empresa**, não nome de usuário. A v1 é monousuário e sem login (RNF09), então não existe "Eduardo – Administrador". Clicando, abre as Configurações.
- O item Configurações na navbar é redundante com o perfil; pode ficar, é escolha do grupo.
- No Figma, desenhar a navbar como **componente com variantes** "expandida" e "recolhida" (e, se for o caso, "tema claro" e "tema escuro").

### Responsividade

Como é WPF, responsivo significa se adaptar ao tamanho da janela:

| Largura da janela | Navbar |
|---|---|
| Larga | Expandida, ícone + texto (220–240px) |
| Abaixo de ~1100px | Recolhida, só ícones (~64px) com tooltip; perfil vira só a logo |

- Painel do dia em janela estreita: ocupa quase toda a largura.
- **Tamanho mínimo da janela:** cerca de 1024×640.

## Telas

### Agenda (tela principal)

```
┌──────────────┬──────────────────────────────────────────────────┐
│  [logo]      │  Agendamentos             [+ Novo Agendamento]   │
│              │                                                  │
│ ▣ Agenda     │   ‹  Outubro 2026  ›        [Mês][Semana][Dia]   │
│ ◯ Clientes   │                                                  │
│ ◯ Serviços   │   DOM   SEG   TER   QUA   QUI   SEX   SÁB         │
│ ◯ Financeiro │  ┌────┬────┬────┬────┬────┬────┬────┐            │
│ ◯ Relatórios │  │ 28 │ 29 │ 30 │  1 │  2 │  3 │  4 │            │
│              │  │    │    │    │ ▮  │    │ ▮▮ │ ┆  │            │
│              │  ├────┼────┼────┼────┼────┼────┼────┤            │
│              │  │  5 │ ...                                      │
│              │  └────┴──────────────────────────────            │
│              │                                                  │
│              │  ○ Livre ▮ Agendado/Faturado ▮ Orç. c/ sinal     │
│ ──────────── │  ┆ Orç. s/ sinal                                 │
│ (logo) Nome  │                                                  │
│  da Empresa  │                                                  │
└──────────────┴──────────────────────────────────────────────────┘
```

- Cada dia mostra **pílulas** com hora e título/cliente, na cor do status; se não couber, mostra a primeira e "+2".
- **Dia de hoje** com borda de ênfase; **dia selecionado** preenchido; dias passados com opacidade reduzida.
- **Legenda** fixa embaixo, com os quatro status.
- **"+ Novo Agendamento"** no topo, para criar sem precisar clicar no dia.
- **Visões Mês, Semana e Dia** (v1): Semana em 7 colunas (dom–sáb) com cards (hora, título, cliente) na cor do status, coluna de hoje em `CorSelecao`; Dia com a hora à esquerda e o mesmo card do painel do dia (status real). Setas avançam mês/semana/dia conforme a visão; a visão escolhida fica lembrada enquanto o app estiver aberto.
- No calendário, "Agendado / Faturado" pode ser um selo só, porque os dois bloqueiam a data da mesma forma.

### Agenda do dia

**Combinado:** painel lateral que desliza da direita, por cima do calendário. Assim dá para fechar e clicar em outro dia sem perder o mês de vista. (No protótipo ela virou tela inteira; funciona, mas perde essa agilidade. Decidir no Figma.)

```
                              ┌──────────────────────────────┐
                              │ Sexta, 16 de outubro 2026  ✕ │
                              │                              │
                              │ ┃ 10:00 – 14:00              │
                              │ ┃ Festa Infantil             │
                              │ ┃ Cliente: Família Silva     │
                              │ ┃ Salão Principal · 30 pess. │
                              │ ┃ [Agendado]               › │
                              │                              │
                              │ ┃ 19:00 – 23:00              │
                              │ ┃ Casamento                  │
                              │ ┃ Cliente: Fernanda e Lucas  │
                              │ ┃ Salão Principal · 120 pess.│
                              │ ┃ [Orçamento com sinal]    › │
                              │                              │
                              │ [ + Novo agendamento ]       │
                              └──────────────────────────────┘
```

- Cada card tem uma **faixa lateral** na cor do status.
- **Na lista do dia, mostrar o status real** (Orçamento, Agendado, Faturado), não "Agendado / Faturado" juntos: ela precisa saber o que ainda falta faturar.
- Hora de início **e fim** e **título do evento** vêm de `HORA_INICIO`, `HORA_FIM` e `TITULO` (ver "Mudanças no banco").
- Clicar no card abre o agendamento. O botão **Cancelar fica dentro do agendamento**, não na lista, para evitar clique acidental.
- Dia vazio: "Nenhum agendamento" e o botão de novo em destaque.

### Agendamento (popup)

Abre como **janela modal por cima da tela atual**, a partir de "+ Novo Agendamento" (Agenda, sem data), "Novo agendamento" (painel do dia, já com a data) ou ao abrir um agendamento existente.

```
┌───────────────────────────────────────────────────────────────┐
│ Novo agendamento  [Agendado]                                ✕ │
├───────────────────────────────────────────────────────────────┤
│ Cliente   [🔍 nome, telefone ou CPF/CNPJ          ] [+]       │
│ Evento    Título*  Data*  Início*  Fim*  Convidados           │
│           ⓘ termina no dia seguinte (fim < início)            │
│ Serviço   [Escolha o serviço ▾] [+ Adicionar]                 │
│   ┌ Salão Principal ............. Valor [1.500,00] 🗑 ┐        │
│   │ (•) Fim de semana R$1.500  ( ) Seg a Qua R$800   │        │
│   └──────────────────────────────────────────────────┘        │
│ Adicionais  ☑ Garçom   qtd [4]  valor [150,00]  R$ 600,00     │
│             custo R$ 100,00 / un (cinza)                      │
├───────────────────────────────────────────────────────────────┤ ← rodapé fixo
│ Serviços  Adicionais  Desconto [   ]  Sinal [   ][Espécie▾]   │
│                                                Total R$ 2.100 │
│ [Cancelar agendamento]   Cancelar · Salvar orçamento ·        │
│                          Agendar · Agendar e faturar          │
└───────────────────────────────────────────────────────────────┘
```

- Janela sem borda do Windows, cantos de 12 px, centralizada, ~900×640 (nunca maior que a janela de trás), rolagem interna; o fundo da tela atrás escurece (`CorOverlay`).
- Fecha com **Esc**, **✕** ou **Cancelar**; se houver alteração não salva, pergunta antes.
- **Cliente:** busca por nome, telefone ou CPF/CNPJ, com sugestões; **+** abre o cadastro rápido (outro popup, com o fundo do agendamento escurecido); ao salvar, o cliente já fica selecionado.
- **Serviço:** só ativos, com faixa ativa (RN03) e, com "um agendamento por dia", livres na data (RN10). Faixas em radio button; a marcada preenche o valor, que pode ser editado (RN06). Aceita mais de um serviço.
- **Adicionais:** só os vinculados aos serviços escolhidos (RN07); quantidade só se `COBRA_POR_QUANTIDADE`; valor unitário editável; total da linha; custo em cinza.
- **Resumo** no rodapé fixo, recalculado na hora (RN09); sinal opcional (valor + espécie, RN12).
- **Botões:** novo/orçamento: Cancelar · Salvar orçamento · Agendar · Agendar e faturar. Agendado: Cancelar · Salvar · Faturar (não volta a orçamento). **Faturado:** só leitura, com apenas **Cancelar agendamento** (RN17). Cancelado: só leitura, mostra data e motivo.
- **Cancelar agendamento** pede o motivo (obrigatório) e mostra antes o que acontece: parcelas canceladas, devolução se dentro do prazo (RN22–RN27).

### Clientes

**Lista:** busca por nome, telefone ou CPF/CNPJ (só os dígitos); colunas Nome, Telefone (formatado), CPF/CNPJ e ações. CPF/CNPJ vazio mostra **"CPF/CNPJ não informado"** em cinza e itálico (só exibição, nunca gravado). A ação "Enviar catálogo pelo WhatsApp" fica no menu (⋮) de cada cliente.

**Campos:** máscaras reutilizáveis (`Controls/Mascara.cs`: telefone, CPF/CNPJ dinâmico, CEP, hora, data, inteiro, moeda, percentual) que bloqueiam a digitação inválida e limpam o texto colado; CPF/CNPJ conferido ao sair do campo; data com calendário próprio no tema (`Controls/SeletorData.cs`).

**Cadastro (protótipo):**

```
┌───────────────────────────────┬───────────────────────────────┐
│ DADOS PESSOAIS                │ ENDEREÇO (opcional)           │
│ Nome completo *               │ Rua                           │
│ Telefone *                    │ Número        Complemento     │
│ CPF/CNPJ                      │ Bairro                        │
│ E-mail                        │ Cidade        UF      CEP     │
└───────────────────────────────┴───────────────────────────────┘
                                          [Cancelar] [Salvar Cliente]
```

- **UF não pode faltar** (no protótipo não aparece).
- CPF/CNPJ, e-mail e endereço detalhado já estão no banco (ver "Mudanças no banco"). Aceitar CNPJ faz sentido: a Atlética UnC do exemplo é pessoa jurídica.

### Serviços

**A lista mostra só os espaços** (Salão Principal, Espaço Gourmet). DJ, Buffet e Decoração são **adicionais**, não serviços, e o preço de um serviço vem das **faixas**, não de um valor único.

**Lista:**

| Nome | A partir de | Faixas | Adicionais | Status |
|---|---|---|---|---|
| Salão Principal | R$ 800,00 | 3 | 5 | Ativo |
| Espaço Gourmet | R$ 600,00 | 2 | 3 | Ativo |

- Sem colunas **Categoria** e **Duração** (não existem no modelo).

**Tela de um serviço:** os adicionais ficam **dentro do serviço**, para ela preencher menos e ver na hora o que cada serviço oferece. No banco continua o N:N (SERVICO_ADICIONAL); a tela só esconde a tabela de ligação.

```
┌──────────────┬──────────────────────────────────────────────────┐
│              │  ‹ Serviços / Salão Principal                    │
│              │                                                  │
│              │  Nome [Salão Principal        ]  Ativo [✓]       │
│              │  Descrição [                  ]                  │
│              │                                                  │
│              │  FAIXAS DE PREÇO                    [+ Faixa]    │
│              │  ≡ Seg a Qua ............. R$   800,00   ✓       │
│              │  ≡ Qui e Sex ............. R$ 1.000,00   ✓       │
│              │  ≡ Fim de semana/Feriado . R$ 1.500,00   ✓       │
│              │                                                  │
│              │  ADICIONAIS       [+ Novo] [+ Usar existente]    │
│              │  Garçom ........ R$ 150,00 / un   custo R$ 100   │
│              │  Buffet ........ R$  45,00 / pessoa  custo R$ 25 │
│              │  Decoração ..... R$ 400,00        custo R$ 200   │
│              │  DJ ............ R$ 900,00        custo R$ 600   │
└──────────────┴──────────────────────────────────────────────────┘
```

- **≡** nas faixas: arrastar para reordenar (grava `ORDEM_EXIBICAO`).
- **"+ Novo"**: cria o adicional já ligado a este serviço (caso comum).
- **"+ Usar existente"**: busca adicionais de outros serviços. Evita recadastrar o garçom quando houver um segundo espaço.
- **Adicional compartilhado:** ao editar, avisar "este adicional também é usado em: Espaço Gourmet", porque a mudança de preço vale para todos.
- **Remover da lista** só desvincula do serviço; não apaga o adicional.
- **Sem "Gerar catálogo"** nesta tela (decisão de 30/09/2026). O código do PDF (QuestPDF) continua; se o catálogo ficar no escopo, o acesso vai para **Relatórios** (pendente). A lista de Serviços ainda tem o botão do catálogo de todos os serviços: decidir se fica.

### Financeiro

Três abas (ou subitens na navbar):

| Aba | Conteúdo |
|---|---|
| **Visão geral** (protótipo pronto) | Filtro de período; cards Total faturado, Recebido, Em aberto (com variação); gráfico de faturamento por mês; últimos lançamentos |
| **Contas a receber** (falta desenhar) | Parcelas em aberto e vencidas; baixa com juros e multa preenchidos; baixa parcial com pergunta sobre a diferença; estorno |
| **Caixa** (falta desenhar) | Entradas e saídas por período; lançamento avulso; despesas do evento (ex.: pagamento do garçom); estorno de à vista/sinal |

- Na Visão geral, parcelas **vencidas** merecem um card ou destaque próprio em vermelho.

### Configurações (pelo perfil)

Abas internas (no protótipo aparecem Notificações e Assinatura, que **não existem no escopo** e saem):

| Aba | Conteúdo |
|---|---|
| Parâmetros | Um agendamento por dia, juros ao mês, multa, prazo de cancelamento |
| Espécies de pagamento | Cadastro de espécies (mexe pouco, por isso fica aqui e não na navbar) |
| Empresa | Nome, CNPJ, telefone, e-mail, endereço e logo |
| Aparência | Tema: Escuro, Claro ou Seguir o Windows |
| Sobre | Curso (Engenharia de Software), grupo (Bruno Balbinott, Eduardo Metzdorf e Marco Dall Bello), professores (Eduardo Pilonetto e Moacir Kichel), versão do projeto e uma frase de agradecimento (sem lista de tecnologias) |

### Relatórios

Cartões (Agendamentos, Caixa por cliente, Faturamento por período, Catálogo de serviços). Clicar abre um **popup de filtros** no mesmo visual do popup de agendamento: título e descrição no cabeçalho, só os filtros do relatório (período com calendário, cliente, status, serviço) e, no rodapé, **Cancelar**, **Gerar Excel (XLSX)** e **Gerar PDF** (principal).

## Splash

Sempre **escura**, nos dois temas. Duas camadas, com **o mesmo visual** para não "piscar" na troca (ao mudar a janela, mudar também o `Tools/gerar-marca.ps1`):

1. **Imagem estática nativa** (`SplashScreen` do WPF, `Assets/Splash/splash.png`): aparece antes do .NET carregar. Gerada pelo `Tools/gerar-marca.ps1` a partir do mesmo layout da janela.
2. **Janela de splash** (`Views/SplashWindow`), enquanto o sistema lê o `config.ini`, conecta no Firebird e carrega a configuração.

```
┌──────────────────────────────────────────────┐
│                                   ●  ▬  ·    │
│                                     ·  ▬     │
│                Fest Agenda   (Pacifico, 52)  │
│           AGENDA DE FESTAS E EVENTOS         │
│             ━━━━━━━━━━░░░░░░░░               │
│            Conectando ao banco...            │
│   ▬  ●                                       │
│ ·    ▬                                       │
│ [UnC branca]                          v1.0.0 │
└──────────────────────────────────────────────┘
```

- Conteúdo centralizado: **"Fest Agenda" em Pacifico → frase "AGENDA DE FESTAS E EVENTOS" (`#C77DFF`) → barra de progresso fina → passo atual**. Confete (círculos e tirinhas sólidas em roxo, lilás, âmbar e verde) nos cantos superior direito e inferior esquerdo. UnC branca pequena no canto inferior esquerdo; versão no direito.
- **Sem** o texto "Desenvolvido por acadêmicos da UnC" e sem a logo pequena do rodapé (o crédito fica na aba Sobre).
- **Fundo** `#10002B`, sem gradiente; borda fina `#3C096C`; texto `#F2EEF7`; janela sem borda do Windows, cantos de 16 px, 560×340.
- **Barra de progresso:** cor sólida `#C77DFF` sobre trilho `#2A0A4F`, sem gradiente nem brilho.
- **Duração real:** só o tempo de carregar, com mínimo de ~1 segundo.
- **Erro na abertura** (banco não encontrado, `config.ini` faltando ou inválido): a própria splash mostra a mensagem, com botões para configurar a conexão, tentar de novo ou sair.
- A imagem estática do WPF é mostrada em pixels reais (não escala com o zoom do Windows): em telas com 125%/150% ela aparece um pouco menor que a janela por um instante.

### Ícone do aplicativo

- `Assets/Icons/app.ico` gerado pelo `Tools/gerar-marca.ps1`: marca **branca sobre quadrado arredondado roxo `#7B2CBF`**, para aparecer em barra de tarefas clara ou escura. **256 e 48 px** com o símbolo + "UNC"; **32 e 16 px** só com o símbolo (as letras ficam ilegíveis).
- Aparece no executável, no instalador, na área de trabalho e na barra de tarefas. Sem balões em lugar nenhum.

## Mudanças no banco (aprovadas em 29/09/2026)

Saíram do protótipo e já estão no `modelagem-banco.md`, `fluxo-sistema.md` e `stack-projeto.md`.

| Tabela | Mudança | Motivo |
|---|---|---|
| AGENDAMENTO | + `TITULO` (ex.: "Festa Infantil", "Casamento") | Identificar o evento no calendário e na agenda do dia |
| AGENDAMENTO | `HORA_EVENTO` → `HORA_INICIO`; + `HORA_FIM` | Card do dia mostra início e fim. Fim menor que início = termina no dia seguinte (RN28) |
| CLIENTE | `CPF` → `CPF_CNPJ` (só dígitos, 11 ou 14, único e opcional) | Clientes pessoa jurídica (ex.: Atlética UnC) |
| CLIENTE | + `EMAIL` | Campo no cadastro |
| CLIENTE | `ENDERECO` → `LOGRADOURO`, `NUMERO`, `COMPLEMENTO`, `BAIRRO`, `CEP` (mantendo `CIDADE` e `UF`) | Endereço completo no cadastro |
| CONFIGURACAO | + `NOME_EMPRESA`, `CNPJ_EMPRESA`, `TELEFONE_EMPRESA`, `EMAIL_EMPRESA`, `ENDERECO_EMPRESA`, `LOGO_EMPRESA` (BLOB) | Editáveis pela tela; saem do `config.ini` |
| CONFIGURACAO | + `TEMA`: `E` escuro, `C` claro, `S` seguir o Windows | Preferência de tema |
| CONFIGURACAO | `LOGO_EMPRESA` (BLOB) → `CAMINHO_LOGO_EMPRESA` (VARCHAR 260, relativo a `Arquivos\`) — **30/09/2026** | Imagens ficam em arquivos na pasta do sistema |
| CONFIGURACAO | `TEMA` padrão passa a `C` (claro) — **30/09/2026** | Novo visual clean |

O `config.ini` passa a ter só a conexão do banco.

## Revisão do protótipo (29/09/2026)

Resumo do que ajustar no Figma:

- [ ] **Serviços:** listar só espaços; tirar DJ, Buffet e Decoração (são adicionais), Categoria, Valor único e Duração.
- [ ] **Agenda do dia:** mostrar o status real em vez de "Agendado / Faturado"; decidir entre painel lateral e tela inteira.
- [ ] **Clientes:** incluir UF no formulário.
- [ ] **Navbar:** trocar "Eduardo – Administrador" por logo e nome da empresa.
- [ ] **Configurações:** tirar Notificações e Assinatura; abas Parâmetros, Espécies, Empresa, Aparência e Sobre.
- [ ] **Financeiro:** desenhar Contas a receber e Caixa.
- [ ] **Splash:** rodapé com a UnC e versão; nome/logo da empresa vindos do banco.
- [ ] **Tema claro:** desenhar ao menos Agenda e um formulário para validar a paleta.
- [ ] Calendário: decidir se Semana e Dia entram na v1.

## Pontos em aberto

- [x] Mudanças no banco aprovadas (29/09/2026).
- [x] Navbar no tema claro: clara (superfície branca), sem âncora escura.
- [ ] Escolher o conjunto de ícones (Lucide ou Fluent System Icons).
- [ ] Biblioteca de controles do WPF (WPF-UI, MaterialDesignInXamlToolkit ou tema Fluent nativo) — precisa suportar troca de tema em tempo de execução; DataGrid, ComboBox e DatePicker padrão ficam ruins no escuro.
- [x] Logo da UnC aplicada (PNG azul sem fundo, `Assets/Brand/`). Ainda vale confirmar se há manual de marca da UnC para projetos acadêmicos.
- [x] Catálogo em PDF: cartão em Relatórios (04/10/2026); o botão da lista de Serviços continua.
- [x] Aba **WhatsApp** em Configurações (18/10/2026): aviso do risco da biblioteca não oficial, interruptor "Usar o WhatsApp", limite por dia, estado em texto (selo verde quando conectado), QR code com a instrução do celular enquanto aguarda, "Conectar" / "Desconectar" (pede confirmação), mensagens padrão com prévia e "Mensagens enviadas" (filtro e "Reenviar" para falhas). O QR some ao conectar, em vez do "QR borrado" do protótipo.
- [x] Popup **Enviar pelo WhatsApp** (orçamento e catálogo): estado da conexão, cliente (escolhido na lista, no catálogo), motivo quando o cliente não pode receber, texto editável, "Copiar texto" e "Enviar". Depois de "Salvar orçamento", pergunta se quer enviar (RF30).
- [x] Popup de agendamento faturado: cartão **Lucro do evento** (receita, custo dos adicionais, despesas e lucro, com a lista das despesas).
- [x] Caixa: **Novo lançamento** (saída, entrada, suprimento, sangria; despesa do evento) e **Estornar**; Contas a receber: **Estornar baixa**.
- [ ] Telas ainda não desenhadas: faturamento, contas a receber, caixa, relatórios. (Agendamento: feito como popup em 30/09/2026.)