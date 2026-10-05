# ============================================================================
# Fest Agenda — gera o instalador FestAgenda-Setup-x.y.z.exe
#
#   1. dotnet publish (Release, win-x64, self-contained, DLLs soltas — sem PublishSingleFile)
#   2. Firebird 5 Embedded na raiz (fbclient.dll, plugins\engine13.dll, intl\, firebird.msg, ICU...)
#   3. Utilitarios\ (isql, gbak, gfix, .bat, LEIAME.txt, Scripts\*.sql)
#   4. Banco vazio BD\basefestagenda.fdb com a estrutura e os dados iniciais (criado pelo próprio embarcado)
#   5. Compila Installer\FestAgenda.iss com o Inno Setup 6 (versão vem do <Version> do .csproj)
#
# Uso:  powershell -ExecutionPolicy Bypass -File build-instalador.ps1
#       [-FirebirdDir "C:\Program Files\Firebird\Firebird_5_0"]   de onde copiar o Firebird 5 (x64)
#       [-Iscc "C:\...\ISCC.exe"]                                 se o Inno Setup não for achado sozinho
#       [-SemInstalador]                                          só monta artifacts\publish e o banco
#
# O Firebird 5 x64 precisa estar instalado (ou extraído do zip oficial) em -FirebirdDir só nesta máquina
# de build; quem instala o Fest Agenda não precisa de servidor Firebird.
# ============================================================================
param(
    [string]$FirebirdDir = "C:\Program Files\Firebird\Firebird_5_0",
    [string]$Iscc,
    [switch]$SemInstalador
)

$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
$artefatos = Join-Path $raiz 'artifacts'
$publicacao = Join-Path $artefatos 'publish'
$pastaBanco = Join-Path $artefatos 'banco'
$banco = Join-Path $pastaBanco 'basefestagenda.fdb'

function Passo([string]$texto) { Write-Host "`n==> $texto" -ForegroundColor Cyan }

function Copiar([string]$origem, [string]$destino) {
    New-Item -ItemType Directory -Force -Path (Split-Path $destino) | Out-Null
    Copy-Item -Path $origem -Destination $destino -Force
}

# ---------------------------------------------------------------------------
Passo 'Versão'
[xml]$projeto = Get-Content (Join-Path $raiz 'EspacoFestas.csproj')
$versao = @($projeto.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
if (-not $versao) { throw 'Não achei <Version> no EspacoFestas.csproj.' }
Write-Host "Fest Agenda $versao"

if (-not (Test-Path (Join-Path $FirebirdDir 'fbclient.dll'))) {
    throw "Firebird 5 não encontrado em '$FirebirdDir'. Informe -FirebirdDir (pasta com fbclient.dll, plugins\, intl\)."
}

# ---------------------------------------------------------------------------
Passo 'Publicando (self-contained, win-x64)'
if (Test-Path $publicacao) { Remove-Item $publicacao -Recurse -Force }
if (Test-Path $pastaBanco) { Remove-Item $pastaBanco -Recurse -Force }
dotnet publish (Join-Path $raiz 'EspacoFestas.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -o $publicacao -nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish falhou.' }

# ---------------------------------------------------------------------------
Passo 'Firebird 5 Embedded'
$arquivosFirebird = @('fbclient.dll', 'firebird.msg', 'ib_util.dll', 'zlib1.dll',
    'msvcp140.dll', 'msvcp140_1.dll', 'vcruntime140.dll', 'vcruntime140_1.dll')
foreach ($arquivo in $arquivosFirebird) {
    $origem = Join-Path $FirebirdDir $arquivo
    if (Test-Path $origem) { Copiar $origem (Join-Path $publicacao $arquivo) }
    elseif ($arquivo -in 'fbclient.dll', 'firebird.msg') { throw "Falta $arquivo em $FirebirdDir" }
}
# ICU (o número da versão muda entre builds do Firebird: icudt63.dll, icuuc63.dll, icuin63.dll, icudt63l.dat)
Get-ChildItem $FirebirdDir -File | Where-Object { $_.Name -match '^icu.*\.(dll|dat)$' } |
    ForEach-Object { Copiar $_.FullName (Join-Path $publicacao $_.Name) }
Copiar (Join-Path $FirebirdDir 'plugins\engine13.dll') (Join-Path $publicacao 'plugins\engine13.dll')
Copiar (Join-Path $FirebirdDir 'intl\fbintl.dll') (Join-Path $publicacao 'intl\fbintl.dll')
Copiar (Join-Path $FirebirdDir 'intl\fbintl.conf') (Join-Path $publicacao 'intl\fbintl.conf')
if (Test-Path (Join-Path $FirebirdDir 'tzdata')) {
    Copy-Item (Join-Path $FirebirdDir 'tzdata') (Join-Path $publicacao 'tzdata') -Recurse -Force
}
Copiar (Join-Path $raiz 'Installer\Firebird\firebird.conf') (Join-Path $publicacao 'firebird.conf')
foreach ($licenca in 'IDPLicense.txt', 'IPLicense.txt') {
    $origem = Join-Path $FirebirdDir $licenca
    if (Test-Path $origem) { Copiar $origem (Join-Path $publicacao "Licencas\Firebird\$licenca") }
}
# Fontes embutidas no executável (SIL OFL 1.1 pede que a licença acompanhe a distribuição)
Copiar (Join-Path $raiz 'Assets\Fonts\OFL.txt') (Join-Path $publicacao 'Licencas\Fontes\Inter-OFL.txt')
Copiar (Join-Path $raiz 'Assets\Fonts\OFL-Pacifico.txt') (Join-Path $publicacao 'Licencas\Fontes\Pacifico-OFL.txt')

# ---------------------------------------------------------------------------
Passo 'Utilitarios'
$util = Join-Path $publicacao 'Utilitarios'
foreach ($ferramenta in 'isql.exe', 'gbak.exe', 'gfix.exe') {
    Copiar (Join-Path $FirebirdDir $ferramenta) (Join-Path $util $ferramenta)
}
Get-ChildItem (Join-Path $raiz 'Installer\Utilitarios') -File | ForEach-Object { Copiar $_.FullName (Join-Path $util $_.Name) }
foreach ($script in 'schema.sql', 'dados-iniciais.sql') {
    Copiar (Join-Path $raiz "Database\$script") (Join-Path $util "Scripts\$script")
}
Get-ChildItem (Join-Path $raiz 'Database') -Filter 'alteracoes-*.sql' | ForEach-Object { Copiar $_.FullName (Join-Path $util "Scripts\$($_.Name)") }

# ---------------------------------------------------------------------------
Passo 'Banco vazio (estrutura + dados iniciais), criado pelo Firebird embarcado publicado'
New-Item -ItemType Directory -Force -Path $pastaBanco | Out-Null
$scripts = Join-Path $util 'Scripts'
$criar = Join-Path $artefatos 'criar-banco.sql'
@"
SET SQL DIALECT 3;
SET NAMES UTF8;
CREATE DATABASE '$banco' USER 'SYSDBA' PAGE_SIZE 16384 DEFAULT CHARACTER SET UTF8;
INPUT '$(Join-Path $scripts 'schema.sql')';
INPUT '$(Join-Path $scripts 'dados-iniciais.sql')';
COMMIT;
"@ | Set-Content -Path $criar -Encoding Ascii

# Usa o isql e o fbclient da própria publicação: valida que o embarcado montado funciona sozinho
$pathAnterior = $env:Path
$env:Path = "$publicacao;$env:Path"
$env:FIREBIRD = $publicacao
try {
    & (Join-Path $util 'isql.exe') -q -b -ch UTF8 -i $criar
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar o banco vazio (isql).' }
}
finally {
    $env:Path = $pathAnterior
    Remove-Item Env:FIREBIRD -ErrorAction SilentlyContinue
    Remove-Item $criar -ErrorAction SilentlyContinue
}
Write-Host "Banco criado: $banco ($([math]::Round((Get-Item $banco).Length / 1KB)) KB)"

if ($SemInstalador) {
    Write-Host "`nPublicação em $publicacao (instalador não gerado: -SemInstalador)." -ForegroundColor Green
    return
}

# ---------------------------------------------------------------------------
Passo 'Instalador (Inno Setup 6)'
if (-not $Iscc) {
    $candidatos = @(
        (Get-Command 'iscc.exe' -ErrorAction SilentlyContinue).Source,
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path $_) }
    $Iscc = @($candidatos)[0]
}
if (-not $Iscc) { throw 'ISCC.exe (Inno Setup 6) não encontrado. Instale: winget install JRSoftware.InnoSetup, ou informe -Iscc.' }

$saida = Join-Path $artefatos 'instalador'
& $Iscc "/DMyAppVersion=$versao" "/DPublishDir=$publicacao" "/DBancoVazio=$banco" "/DOutputDir=$saida" `
    (Join-Path $raiz 'Installer\FestAgenda.iss')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o instalador.' }

$setup = Join-Path $saida "FestAgenda-Setup-$versao.exe"
Write-Host "`nInstalador gerado: $setup ($([math]::Round((Get-Item $setup).Length / 1MB, 1)) MB)" -ForegroundColor Green
