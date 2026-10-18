; ============================================================================
; Fest Agenda — instalador (Inno Setup 6)
; Gerado pelo build-instalador.ps1, que publica o sistema, monta o Firebird
; embarcado e o banco vazio e passa os caminhos e a versão abaixo (/D...).
;
; Estrutura instalada (padrão C:\FestAgenda):
;   FestAgenda.exe, *.dll, Firebird embarcado (fbclient.dll, plugins\, intl\...)
;   config.ini     conexão com o banco      (só na 1ª instalação; nunca desinstalado)
;   BD\            basefestagenda.fdb           (só na 1ª instalação; nunca desinstalado)
;   Arquivos\      imagens do sistema       (nunca desinstalado)
;   Log\           RelatorioLog.log, Erros.log, whatsapp.log (nunca desinstalado)
;   Utilitarios\   backup, restauração, verificação, isql, scripts SQL
;   Whatsapp\      node\ (Node portátil), server\ (Baileys + node_modules), sessao\ (login; nunca sobrescrito nem desinstalado)
;   Licencas\      Firebird (IDPL/IPL), fontes Inter e Pacifico (SIL OFL), Node e Baileys (MIT)
;
; Atualização por cima: depois de copiar os arquivos, roda "FestAgenda.exe --atualizar-banco",
; que faz backup do banco (Utilitarios\Backups) e aplica os alteracoes-*.sql que faltam.
; ============================================================================

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif
#ifndef BancoVazio
  #define BancoVazio "..\artifacts\banco\basefestagenda.fdb"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\instalador"
#endif

#define MyAppName "Fest Agenda"
#define MyAppExe "FestAgenda.exe"
#define MyAppPublisher "Universidade do Contestado (UnC)"

[Setup]
; AppId fixo: é ele que faz a nova versão atualizar a instalação existente. Não mudar.
AppId={{6E3C6B0B-6F1A-4C55-9A47-3E8D3B2F7A21}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}
DefaultDirName=C:\FestAgenda
UsePreviousAppDir=yes
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=FestAgenda-Setup-{#MyAppVersion}
SetupIconFile=..\Assets\Icons\app.ico
UninstallDisplayIcon={app}\{#MyAppExe}
UninstallDisplayName={#MyAppName}
WizardStyle=modern
; Imagens da marca (geradas pelo Tools\gerar-marca.ps1); o Inno escolhe a do tamanho certo para o zoom do Windows
WizardImageFile=Imagens\lateral-164.bmp,Imagens\lateral-246.bmp,Imagens\lateral-328.bmp
WizardSmallImageFile=Imagens\topo-55.bmp,Imagens\topo-83.bmp,Imagens\topo-110.bmp
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
; Instala em C:\ e ajusta permissões das pastas de dados: precisa de administrador
PrivilegesRequired=admin
; Na atualização, fecha o Fest Agenda se estiver aberto (o banco embarcado fica travado com ele)
CloseApplications=force
RestartApplications=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"

[Dirs]
; Pastas de dados: usuários comuns podem gravar (senão o sistema só funcionaria como administrador)
; e a desinstalação nunca apaga o que estiver nelas.
Name: "{app}\BD"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{app}\Arquivos"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{app}\Arquivos\Empresa"; Permissions: users-modify; Flags: uninsneveruninstall
; Log dos relatórios (RN31): o sistema anexa uma linha a cada geração
Name: "{app}\Log"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{app}\Utilitarios\Backups"; Permissions: users-modify; Flags: uninsneveruninstall
; PDFs enviados pelo WhatsApp (catálogo) e sessão do WhatsApp (login por QR): o sistema grava; atualizar e desinstalar não apagam
Name: "{app}\Arquivos\WhatsApp"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{app}\Whatsapp\sessao"; Permissions: users-modify; Flags: uninsneveruninstall

[Files]
; Sistema, DLLs, Firebird embarcado e utilitários: sempre atualizados
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "config.ini,\Whatsapp\sessao\*"; Flags: ignoreversion recursesubdirs createallsubdirs
; config.ini e banco: só na primeira instalação; atualizar e desinstalar não mexem neles
Source: "{#PublishDir}\config.exemplo.ini"; DestDir: "{app}"; DestName: "config.ini"; Permissions: users-modify; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#BancoVazio}"; DestDir: "{app}\BD"; Permissions: users-modify; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; WorkingDir: "{app}"
Name: "{autoprograms}\{#MyAppName} - Utilitários"; Filename: "{app}\Utilitarios"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Code]
// Depois de copiar os arquivos: atualiza a estrutura do banco (com backup antes). Na primeira instalação
// o banco já vem em dia e nada é alterado. Falha não desfaz a instalação: avisa e aponta o log.
procedure CurStepChanged(CurStep: TSetupStep);
var
  Codigo: Integer;
begin
  if CurStep <> ssPostInstall then
    Exit;

  WizardForm.StatusLabel.Caption := 'Atualizando o banco de dados...';
  if not Exec(ExpandConstant('{app}\{#MyAppExe}'), '--atualizar-banco', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, Codigo) then
    Codigo := -1;

  if Codigo <> 0 then
    SuppressibleMsgBox('Não foi possível atualizar o banco de dados automaticamente.' + #13#10#13#10 +
      'Os seus dados não foram apagados. Veja o motivo em:' + #13#10 + ExpandConstant('{app}\Log\AtualizacaoBanco.log') + #13#10#13#10 +
      'Ao abrir, o sistema tenta atualizar de novo. Se continuar com erro, um backup do banco fica em Utilitarios\Backups.',
      mbError, MB_OK, IDOK);
end;

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "Abrir o {#MyAppName}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Messages]
brazilianportuguese.WelcomeLabel1=Bem-vindo ao Fest Agenda
brazilianportuguese.WelcomeLabel2=Este assistente vai instalar o [name/ver] neste computador.%n%nO sistema já leva tudo o que precisa (incluindo o banco de dados): não é preciso instalar mais nada.%n%nSe o Fest Agenda já estiver instalado, ele será atualizado e os seus dados serão mantidos. Antes de atualizar, feche o sistema.
brazilianportuguese.FinishedHeadingLabel=Fest Agenda instalado
brazilianportuguese.FinishedLabelNoIcons=O [name] foi instalado neste computador.
brazilianportuguese.FinishedLabel=O [name] foi instalado neste computador e pode ser aberto pelos atalhos criados.
brazilianportuguese.UninstalledMost=O %1 foi removido.%n%nOs dados foram mantidos: as pastas BD (banco de dados), Arquivos, Log, Whatsapp\sessao (login do WhatsApp) e o config.ini continuam na pasta do sistema. Apague-as manualmente se não precisar mais delas.
