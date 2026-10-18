# Stack e Configuração do Projeto – Fest Agenda

> **Criado em 28/09/2026.** Referência técnica do projeto: tecnologias, pacotes, criação da solução, organização de pastas, convenções e configuração. Regras de negócio no `fluxo-sistema.md`; banco no `modelagem-banco.md`; visual no `design-interface.md`.
> **Atualizado em 29/09/2026:** `config.ini` só com a conexão; pastas de temas e recursos visuais (fonte, ícone, splash); biblioteca de controles precisa suportar troca de tema.
> **Atualizado em 30/09/2026:** nome **Fest Agenda** (`FestAgenda.exe`); marca da UnC em `Assets/Brand`; **instalador** (Inno Setup) em `C:\FestAgenda` com **Firebird 5 Embedded**; `AppPaths`; novo formato do `config.ini`; utilitários de manutenção.

## Stack

| Item | Decisão | Observação |
|---|---|---|
| Plataforma | **.NET 10** | Obrigatório. |
| Nome | **Fest Agenda** | `AssemblyName` = `FestAgenda` (executável `FestAgenda.exe`), `Product` = "Fest Agenda". Namespace e pasta continuam `EspacoFestas`. |
| Interface | **WPF** | Tema claro (padrão) e escuro; visual no `design-interface.md`. |
| Banco | **Firebird 5 Embedded** | Vai junto do sistema (`fbclient.dll` + `plugins\engine13.dll`); `config.ini` com `Tipo=Servidor` usa um Firebird Server na rede. |
| Acesso ao banco | **Entity Framework Core 10** | Provider oficial do Firebird para EF Core 10. |
| PDF | **QuestPDF** | Catálogo e relatórios. Conferir as condições da licença Community antes da entrega. |
| Excel | **ClosedXML** (MIT) | Relatórios em XLSX (RNF18). |
| WhatsApp | **Baileys** `@whiskeysockets/baileys` 7.0.0-rc14 (não oficial) em servidor **Node.js** local | Decidido em 18/10/2026 no lugar do WA-AKG (que exigia Next.js e MySQL/PostgreSQL). Node 24 LTS portátil em `Whatsapp\node`, código em `Whatsapp\server` (versões travadas no `package-lock.json`), sessão em `Whatsapp\sessao`. O app fala com ele por HTTP em 127.0.0.1 com token (`Services/Whatsapp/IWhatsappService`), o que permite trocar pela API oficial no futuro. Risco: biblioteca não oficial, o número pode ser bloqueado. |
| Configuração técnica | **config.ini** | Só a conexão do banco. Lido pelo `Microsoft.Extensions.Configuration.Ini`. |
| Fonte | **Inter** + **Pacifico** | Embutidas no projeto (SIL OFL 1.1). Pacifico só no nome "Fest Agenda" (`FonteMarca`). Em XAML, referenciar pelo caminho relativo ao dicionário (`../Assets/Fonts/#Inter`): a forma `pack://application:,,,/...` não carregava as fontes embutidas. |
| Versionamento | **Git** | |
| IDE | **Visual Studio** | |
| Instalador | **Inno Setup 6** | `Installer/FestAgenda.iss`, gerado pelo `build-instalador.ps1`. |

## Pacotes NuGet

| Pacote | Para quê |
|---|---|
| `FirebirdSql.EntityFrameworkCore.Firebird` | Provider do EF Core para Firebird (versão compatível com EF Core 10). Já traz o `FirebirdSql.Data.FirebirdClient`. |
| `Microsoft.EntityFrameworkCore.Design` | Ferramentas do EF (migrations/scaffold), se forem usadas. Só fora do Release, para não levar o Roslyn para o instalador. |
| `Microsoft.Extensions.Configuration.Ini` | Leitura do `config.ini`. |
| `QuestPDF` | Geração do catálogo e dos relatórios em PDF. |
| `ClosedXML` | Relatórios em XLSX (cabeçalho, primeira linha congelada, R$ como número, datas como data). |
| `QRCoder` (MIT) | QR code da conexão do WhatsApp em Configurações. |
| `xunit.v3` (só no `FestAgenda.Tests`) | Testes das regras de negócio, com banco Firebird criado do zero a cada execução. |
| Biblioteca de controles (a definir) | Estilo dos controles nos dois temas. Candidatas: WPF-UI, MaterialDesignInXamlToolkit ou o tema Fluent nativo do WPF. Requisito: **trocar de tema em tempo de execução**. |

Instalar sempre a versão estável mais recente compatível com .NET 10.

## Criação da solução no Visual Studio

1. Novo projeto → **Aplicativo WPF** (o que **não** tem "(.NET Framework)" no nome).
2. Informar o nome do projeto.
3. Em **Framework**, escolher **.NET 10.0**. Se não aparecer, instalar o SDK do .NET 10 ou atualizar o Visual Studio.

> Os templates com "(.NET Framework)" usam a plataforma antiga (4.x) e não rodam .NET 10 nem o EF Core atual.

## Organização de pastas

Projeto único, separado em pastas:

```
EspacoFestas/
├── Models/            entidades (Cliente, Agendamento, Receber...)
├── Data/              DbContext, mapeamentos do EF, convenção de nomes
├── Services/          regras de negócio (AgendamentoService: disponibilidade, total, cancelamento),
│                      AppPaths (pastas do sistema), Imagens, TemaService, RelatorioLog
├── Views/             telas WPF (XAML): splash, agenda, agendamento (popup), cadastros...
├── Controls/          controles e propriedades auxiliares (Icone, Estilo, Modal, Navbar),
│                      Mascara (máscaras de entrada reutilizáveis), SeletorData (calendário no tema)
├── Themes/            Light.xaml e Dark.xaml (mesmas chaves, cores diferentes) e estilos comuns
├── Assets/
│   ├── Fonts/         Inter
│   ├── Brand/         logo da UnC (original azul sem fundo + recortes gerados, coloridos e brancos)
│   ├── Icons/         ícone do app (.ico: 16, 32, 48, 256)
│   └── Splash/        imagem estática da splash
├── Reports/           CatalogoPdf; Relatorios/ (IRelatorio, os relatórios, PdfRelatorio e XlsxRelatorio genéricos)
├── Database/          scripts SQL: schema.sql, dados-iniciais.sql, alteracoes-*.sql
├── Installer/         FestAgenda.iss, firebird.conf do embarcado, Utilitarios (.bat, LEIAME.txt)
├── Tools/             gerar-marca.ps1 (recortes da logo, .ico e splash.png)
├── build-instalador.ps1  publish + Firebird embarcado + banco vazio + instalador
├── config.ini         conexão com o banco (não versionado)
├── config.exemplo.ini modelo versionado (é o config.ini da primeira instalação)
└── App.xaml
```

- Se o grupo preferir separar em camadas mais tarde, entidades e acesso ao banco podem ir para uma **Biblioteca de Classes** comum (não a WPF).
- Uso de MVVM (pasta `ViewModels/`): a definir.

### Temas

- `Themes/Dark.xaml` e `Themes/Light.xaml` definem as **mesmas chaves** (ex.: `CorFundo`, `CorSuperficie`, `CorBotaoPrincipal`, `CorTexto`); as cores estão no `design-interface.md`.
- As telas usam sempre `{DynamicResource ...}`, nunca cor fixa.
- Um serviço de tema troca o dicionário em `Application.Current.Resources.MergedDictionaries` ao abrir o sistema (conforme `CONFIGURACAO.TEMA`) e quando ela muda nas Configurações.
- "Seguir o Windows": ler `AppsUseLightTheme` em `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`.

### Splash

- Imagem estática: `Assets/Splash/splash.png` com Build Action **SplashScreen** (aparece antes do .NET carregar). Gerada pelo `Tools/gerar-marca.ps1` com o mesmo layout da janela, para não "piscar".
- Janela de splash (`Views/SplashWindow`): lê o `config.ini`, conecta no banco, carrega a CONFIGURACAO e abre a janela principal; trata erro de conexão.

### Marca e ícone

- Logo da UnC (PNG azul, sem fundo) em `Assets/Brand/unc_logo_azul.png`. O `Tools/gerar-marca.ps1` gera os recortes (`unc_logo.png`, `unc_marca.png`, `unc_simbolo.png`), as versões brancas (`*_branca.png`, `unc_simbolo_branco.png`), o `app.ico` (marca branca sobre quadrado roxo `#7B2CBF`; 16/32 só símbolo, 48/256 símbolo + "UNC") e a `splash.png` (escura). Rodar de novo se a logo mudar.
- Nas telas, a logo vem da chave `ImagemLogoUnc` de cada tema (`<Image Source="{DynamicResource ImagemLogoUnc}"/>`): colorida no claro, branca no escuro, sem distorcer.

## Convenções de nomes

| Onde | Padrão | Exemplo |
|---|---|---|
| Banco (tabelas e campos) | MAIÚSCULO_COM_UNDERSCORE, por extenso, sem aspas | `SERVICO_AGENDAMENTO`, `VALOR_PARCELA` |
| C# (classes e propriedades) | PascalCase | `ServicoAgendamento`, `ValorParcela` |
| C# (variáveis e parâmetros) | camelCase | `servicoAgendamento`, `valorParcela` |
| Recursos de tema (XAML) | PascalCase pela função, não pela cor | `CorBotaoPrincipal` (não `Roxo7B2CBF`) |

- **Mapeamento no EF:** por padrão o EF usaria o nome da propriedade como nome da coluna. É preciso configurar no `OnModelCreating` uma convenção que converta PascalCase para MAIÚSCULO_COM_UNDERSCORE em todas as entidades (ou `ToTable`/`HasColumnName` por entidade).
- Detalhes do banco (PK `ID`, FK `<TABELA>_ID`, prefixos `DATA_`, `HORA_`, `VALOR_`, `PERCENTUAL_`, `QUANTIDADE_`) no `modelagem-banco.md`.

## Distribuição e instalação

Estrutura depois de instalar (padrão `C:\FestAgenda`, mas o sistema funciona em qualquer pasta):

```
C:\FestAgenda\
├── FestAgenda.exe
├── *.dll                 ← .NET (self-contained), EF Core, Firebird client, QuestPDF...
├── fbclient.dll, firebird.conf, firebird.msg, icu*.dll, plugins\engine13.dll, intl\, tzdata\   ← Firebird 5 Embedded
├── config.ini            ← conexão com o banco
├── BD\basefestagenda.fdb     ← banco
├── Arquivos\             ← imagens (o banco guarda só o caminho relativo); Empresa\ = logo
├── Log\                  ← RelatorioLog.log (uma linha por relatório gerado ou com erro)
├── Utilitarios\          ← isql, gbak, gfix, backup.bat, restaurar.bat, verificar-banco.bat, abrir-isql.bat,
│                            LEIAME.txt, Scripts\*.sql, Backups\
└── Licencas\             ← Firebird\ (IDPL / IPL) e Fontes\ (Inter e Pacifico, SIL OFL)
```

- **Publicação:** `dotnet publish -c Release -r win-x64 --self-contained true`, **sem** `PublishSingleFile` (DLLs soltas ao lado do `.exe`). Quem instala não precisa do .NET.
- **Firebird embarcado:** o `build-instalador.ps1` copia do Firebird 5 x64 instalado na máquina de build (`-FirebirdDir`). `firebird.conf` próprio (`Installer/Firebird`): `ServerMode = Super` (o banco fica exclusivo do sistema enquanto aberto) e `Providers = Engine13, Remote` (caminho local abre no embarcado mesmo se houver servidor na máquina; `servidor:caminho` usa a rede).
- **Caminhos:** tudo a partir de `AppContext.BaseDirectory` pela classe `Services/AppPaths` (`Raiz`, `BD`, `Arquivos`, `Log`, `Utilitarios`, `ConfigIni`); nunca `C:\FestAgenda` fixo. No banco, caminhos relativos a `Arquivos\`.
- **Imagens:** ao salvar, copia para a subpasta de `Arquivos\` (criada sob demanda), nome único (`logo_aaaammddhhmmssfff.png`), máx. 512 px, PNG; grava o caminho relativo. Arquivo sumido → placeholder, sem erro.
- **Instalador (Inno Setup, `Installer/FestAgenda.iss`):** `DefaultDirName=C:\FestAgenda`, atalhos na área de trabalho e no menu Iniciar, ícone da UnC, versão do `<Version>` do `.csproj`. `Permissions: users-modify` em `BD\`, `Arquivos\`, `Log\`, `Utilitarios\Backups` e no `config.ini` (o sistema grava sem rodar como administrador). `config.ini` e `.FDB` com `onlyifdoesntexist` + `uninsneveruninstall`: atualizar troca só programa, DLLs e utilitários; desinstalar não apaga `BD\`, `Arquivos\` nem `Log\`.
- Na atualização, o instalador fecha o sistema se estiver aberto.
- **Atualização de banco (automática):** os `Database\alteracoes-*.sql` vão embutidos no executável. Depois de copiar os arquivos, o instalador roda `FestAgenda.exe --atualizar-banco` (sem janela): o `Services/AtualizacaoBanco` confere pela estrutura do banco o que falta, faz backup (gbak pelo serviço do Firebird) em `Utilitarios\Backups\antes-atualizacao-*.fbk` e aplica os scripts. Código de saída 0 = ok; falha → aviso no instalador, que não desfaz nada. A splash faz a mesma verificação ao abrir (cobre banco em servidor e falha no instalador). Registro em `Log\AtualizacaoBanco.log`. Bancos anteriores a 29/09/2026 continuam manuais. **Nova alteração de banco:** criar o `alteracoes-AAAA-MM-DD.sql` e incluir na lista `Alteracoes` do `AtualizacaoBanco`, com a consulta que diz se ela já foi aplicada.
- **Visual do assistente:** imagens lateral e do topo (`Installer/Imagens/*.bmp`, geradas pelo `Tools/gerar-marca.ps1`, em 100%/150%/200%) e textos de boas-vindas e conclusão em português.
- **Licenças:** `Licencas\Firebird\` (IDPL/IPL) e `Licencas\Fontes\` (Inter e Pacifico, SIL OFL), copiadas pelo `build-instalador.ps1`.
- **Primeira instalação:** leva um banco vazio criado no build (`schema.sql` + `dados-iniciais.sql`) pelo próprio Firebird embarcado publicado.
- **Gerar o instalador:** `powershell -ExecutionPolicy Bypass -File build-instalador.ps1` → `artifacts\instalador\FestAgenda-Setup-x.y.z.exe`. Precisa do Firebird 5 x64 e do Inno Setup 6 na máquina de build (`winget install JRSoftware.InnoSetup`).

## Configuração

**Técnica → `config.ini`** (fora do banco):
- Só a conexão com o banco de dados:

```ini
[Banco]
Tipo=Embarcado          ; Embarcado | Servidor
Servidor=localhost
Porta=3050
Caminho=BD\basefestagenda.fdb   ; relativo à pasta do app ou absoluto
Usuario=SYSDBA
Senha=masterkey
```

- Comentário no fim da linha (`; ...`) é ignorado pelo sistema. Em desenvolvimento, o `config.ini` local pode usar `Tipo=Servidor` apontando para o Firebird Server da máquina.

**No banco → tabela CONFIGURACAO** (alterada pela tela de Configurações):
- Parâmetros: um agendamento por dia, juros ao mês (%), multa (%, padrão sugerido 2%), prazo de cancelamento (dias).
- Dados da empresa: nome, CNPJ, telefone, e-mail, endereço e o caminho da logo (o arquivo fica em `Arquivos\Empresa`).
- Tema: claro (padrão), escuro ou seguir o Windows.

## Git

- `.gitignore` padrão do Visual Studio (ignora `bin/`, `obj/`, `.vs/`).
- **Não versionar** o arquivo do banco (`.fdb`) nem o `config.ini` real, que tem o caminho da máquina de cada um. Versionar um `config.exemplo.ini` com os campos de exemplo.
- Versionar os scripts SQL do banco (DDL, domínios, `COMMENT ON`) na pasta `Database/`.
- Não versionar `artifacts/` (saída do `build-instalador.ps1`). Versionar `Installer/`, `Tools/` e o `build-instalador.ps1`.
- Fluxo de branches e divisão de tarefas: a definir pelo grupo.

## Pontos em aberto

- [ ] Escolher a biblioteca de controles (precisa trocar de tema em tempo de execução).
- [ ] Uso de MVVM.
- [ ] Conferir a licença Community do QuestPDF.
- [ ] Fluxo de branches e divisão de tarefas no Git.
- [x] Instalador: Inno Setup em `C:\FestAgenda`, com Firebird embarcado (30/09/2026).
- [x] Backup: manual, pelos utilitários (`backup.bat` / `restaurar.bat`). Automático continua fora do escopo.
- [ ] Assinatura digital do instalador/executável (sem ela o Windows SmartScreen avisa "editor desconhecido").