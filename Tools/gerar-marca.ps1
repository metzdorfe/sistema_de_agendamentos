# ============================================================================
# Fest Agenda — gera os recursos visuais da marca a partir da logo da UnC.
#
#   Assets/Brand/unc_logo_azul.png   (original, PNG sem fundo — entrada)
#   Assets/Brand/unc_logo.png        logo horizontal recortada (sem margem vazia)
#   Assets/Brand/unc_marca.png       símbolo + "UNC" (quadrado), navbar e Sobre
#   Assets/Brand/unc_simbolo.png     só o símbolo, para tamanhos pequenos
#   Assets/Brand/*_branca.png        as mesmas três em branco (tema escuro e splash)
#   Assets/Icons/app.ico             16, 32, 48 e 256 px: marca branca sobre quadrado arredondado roxo #6D3FC0
#   Assets/Splash/splash.png         imagem estática da splash (mesmo visual da SplashWindow: escura, logo branca)
#   Installer/Imagens/*.bmp          imagens do assistente do instalador (lateral e topo), 100%/150%/200%
#
# Uso (Windows PowerShell 5.1, que já roda em STA):
#   powershell -ExecutionPolicy Bypass -File Tools\gerar-marca.ps1
# ============================================================================

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$raiz = Split-Path -Parent $PSScriptRoot
$brand = Join-Path $raiz 'Assets\Brand'
$original = Join-Path $brand 'unc_logo_azul.png'

function Ler-Imagem([string]$caminho) {
    $img = New-Object System.Windows.Media.Imaging.BitmapImage
    $img.BeginInit()
    $img.CacheOption = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
    $img.UriSource = New-Object System.Uri($caminho)
    $img.EndInit()
    $img.Freeze()
    # Normaliza para BGRA32 a 96 DPI para ler os pixels
    $conv = New-Object System.Windows.Media.Imaging.FormatConvertedBitmap($img, [System.Windows.Media.PixelFormats]::Bgra32, $null, 0)
    $stride = $conv.PixelWidth * 4
    $pixels = New-Object byte[] ($stride * $conv.PixelHeight)
    $conv.CopyPixels($pixels, $stride, 0)
    return [System.Windows.Media.Imaging.BitmapSource]::Create($conv.PixelWidth, $conv.PixelHeight, 96, 96,
        [System.Windows.Media.PixelFormats]::Bgra32, $null, $pixels, $stride)
}

# Caixa dos pixels visíveis dentro de uma região (x0..x1, y0..y1)
function Caixa($bmp, [int]$x0, [int]$y0, [int]$x1, [int]$y1) {
    $stride = $bmp.PixelWidth * 4
    $pixels = New-Object byte[] ($stride * $bmp.PixelHeight)
    $bmp.CopyPixels($pixels, $stride, 0)
    $minX = [int]::MaxValue; $minY = [int]::MaxValue; $maxX = -1; $maxY = -1
    for ($y = $y0; $y -le $y1; $y++) {
        for ($x = $x0; $x -le $x1; $x++) {
            if ($pixels[$y * $stride + $x * 4 + 3] -gt 16) {
                if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    return New-Object System.Windows.Int32Rect($minX, $minY, ($maxX - $minX + 1), ($maxY - $minY + 1))
}

function Salvar-Png($fonte, [string]$caminho) {
    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($fonte))
    $fs = [System.IO.File]::Create($caminho)
    try { $enc.Save($fs) } finally { $fs.Dispose() }
}

function Png-Bytes($fonte) {
    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($fonte))
    $ms = New-Object System.IO.MemoryStream
    $enc.Save($ms)
    return $ms.ToArray()
}

# Desenha a imagem centralizada num quadrado de $tamanho px, com $margem px de respiro, sem distorcer
function Quadrado($fonte, [int]$tamanho, [double]$margem) {
    $area = $tamanho - 2 * $margem
    $escala = [Math]::Min($area / $fonte.PixelWidth, $area / $fonte.PixelHeight)
    $w = $fonte.PixelWidth * $escala; $h = $fonte.PixelHeight * $escala
    $visual = New-Object System.Windows.Media.DrawingVisual
    [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($visual, [System.Windows.Media.BitmapScalingMode]::HighQuality)
    $dc = $visual.RenderOpen()
    $dc.DrawImage($fonte, (New-Object System.Windows.Rect((($tamanho - $w) / 2), (($tamanho - $h) / 2), $w, $h)))
    $dc.Close()
    $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($tamanho, $tamanho, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($visual)
    return $rtb
}

$logo = Ler-Imagem $original
Write-Host "Logo original: $($logo.PixelWidth)x$($logo.PixelHeight)"

# Logo inteira, sem margem vazia
$caixaLogo = Caixa $logo 0 0 ($logo.PixelWidth - 1) ($logo.PixelHeight - 1)
$logoRecortada = New-Object System.Windows.Media.Imaging.CroppedBitmap($logo, $caixaLogo)
Salvar-Png $logoRecortada (Join-Path $brand 'unc_logo.png')

# Símbolo + "UNC": a coluna da esquerda, antes do texto "UNIVERSIDADE DO CONTESTADO"
$caixaMarca = Caixa $logo 0 0 560 ($logo.PixelHeight - 1)
$marca = New-Object System.Windows.Media.Imaging.CroppedBitmap($logo, $caixaMarca)
Salvar-Png $marca (Join-Path $brand 'unc_marca.png')

# Só o símbolo: acima da linha vazia que separa o símbolo das letras "UNC"
$stride = $logo.PixelWidth * 4
$px = New-Object byte[] ($stride * $logo.PixelHeight)
$logo.CopyPixels($px, $stride, 0)
$corte = -1
for ($y = $caixaMarca.Y + 40; $y -lt $caixaMarca.Y + $caixaMarca.Height; $y++) {
    $vazia = $true
    for ($x = $caixaMarca.X; $x -lt $caixaMarca.X + $caixaMarca.Width; $x++) {
        if ($px[$y * $stride + $x * 4 + 3] -gt 16) { $vazia = $false; break }
    }
    if ($vazia) { $corte = $y; break }
}
$caixaSimbolo = Caixa $logo $caixaMarca.X $caixaMarca.Y ($caixaMarca.X + $caixaMarca.Width - 1) ($corte - 1)
$simbolo = New-Object System.Windows.Media.Imaging.CroppedBitmap($logo, $caixaSimbolo)
Salvar-Png $simbolo (Join-Path $brand 'unc_simbolo.png')
Write-Host "Marca: $caixaMarca  Simbolo: $caixaSimbolo"

# Versões brancas: mesmo canal alfa, cor trocada por branco (mantém o antisserrilhado das bordas)
function Branca($fonte) {
    $conv = New-Object System.Windows.Media.Imaging.FormatConvertedBitmap($fonte, [System.Windows.Media.PixelFormats]::Bgra32, $null, 0)
    $stride = $conv.PixelWidth * 4
    $px = New-Object byte[] ($stride * $conv.PixelHeight)
    $conv.CopyPixels($px, $stride, 0)
    for ($i = 0; $i -lt $px.Length; $i += 4) { $px[$i] = 255; $px[$i + 1] = 255; $px[$i + 2] = 255 }
    return [System.Windows.Media.Imaging.BitmapSource]::Create($conv.PixelWidth, $conv.PixelHeight, 96, 96,
        [System.Windows.Media.PixelFormats]::Bgra32, $null, $px, $stride)
}
$logoBranca = Branca $logoRecortada
$marcaBranca = Branca $marca
$simboloBranco = Branca $simbolo
Salvar-Png $logoBranca (Join-Path $brand 'unc_logo_branca.png')
Salvar-Png $marcaBranca (Join-Path $brand 'unc_marca_branca.png')
Salvar-Png $simboloBranco (Join-Path $brand 'unc_simbolo_branco.png')

# ---------------------------------------------------------------------------
# Ícone: quadrado arredondado roxo com a marca branca, legível em barra de tarefas clara ou escura.
# 256 e 48 com símbolo + "UNC"; 32 e 16 só com o símbolo (as letras ficam ilegíveis)
# ---------------------------------------------------------------------------
$frames = @(
    @{ Tamanho = 16;  Fonte = $simboloBranco; Margem = 2.5 },
    @{ Tamanho = 32;  Fonte = $simboloBranco; Margem = 5 },
    @{ Tamanho = 48;  Fonte = $marcaBranca;   Margem = 7 },
    @{ Tamanho = 256; Fonte = $marcaBranca;   Margem = 40 }
)
$roxo = New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(0x6D, 0x3F, 0xC0))
$roxo.Freeze()

# Fundo roxo de cantos arredondados (raio ~22% do lado) + imagem centralizada por cima
function QuadradoIcone($fonte, [int]$tamanho, [double]$margem) {
    $area = $tamanho - 2 * $margem
    $escala = [Math]::Min($area / $fonte.PixelWidth, $area / $fonte.PixelHeight)
    $w = $fonte.PixelWidth * $escala; $h = $fonte.PixelHeight * $escala
    $raio = $tamanho * 0.22
    $visual = New-Object System.Windows.Media.DrawingVisual
    [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($visual, [System.Windows.Media.BitmapScalingMode]::HighQuality)
    $dc = $visual.RenderOpen()
    $dc.DrawRoundedRectangle($roxo, $null, (New-Object System.Windows.Rect(0, 0, $tamanho, $tamanho)), $raio, $raio)
    $dc.DrawImage($fonte, (New-Object System.Windows.Rect((($tamanho - $w) / 2), (($tamanho - $h) / 2), $w, $h)))
    $dc.Close()
    $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($tamanho, $tamanho, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($visual)
    return $rtb
}
# Tamanhos pequenos em DIB 32 bits (BMP), o formato que todo leitor de .ico entende; 256 em PNG.
function Dib-Bytes($fonte) {
    $t = $fonte.PixelWidth
    $conv = New-Object System.Windows.Media.Imaging.FormatConvertedBitmap($fonte, [System.Windows.Media.PixelFormats]::Bgra32, $null, 0)
    $stride = $t * 4
    $px = New-Object byte[] ($stride * $t)
    $conv.CopyPixels($px, $stride, 0)
    $ms = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter($ms)
    # BITMAPINFOHEADER: altura dobrada (imagem XOR + máscara AND)
    $w.Write([UInt32]40); $w.Write([Int32]$t); $w.Write([Int32]($t * 2)); $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]0); $w.Write([UInt32]0); $w.Write([Int32]0); $w.Write([Int32]0); $w.Write([UInt32]0); $w.Write([UInt32]0)
    for ($y = $t - 1; $y -ge 0; $y--) { $w.Write($px, $y * $stride, $stride) }
    # Máscara AND toda zerada (a transparência vem do canal alfa); linhas alinhadas em 32 bits
    $linhaMascara = [Math]::Ceiling($t / 32) * 4
    $w.Write((New-Object byte[] ($linhaMascara * $t)))
    $w.Flush()
    return $ms.ToArray()
}

$imagens = foreach ($f in $frames) {
    $quadro = QuadradoIcone $f.Fonte $f.Tamanho $f.Margem
    if ($f.Tamanho -ge 256) { , (Png-Bytes $quadro) } else { , (Dib-Bytes $quadro) }
}

$ico = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ico)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$frames.Count)
$deslocamento = 6 + 16 * $frames.Count
for ($i = 0; $i -lt $frames.Count; $i++) {
    $t = $frames[$i].Tamanho
    $lado = if ($t -ge 256) { 0 } else { $t }
    $bw.Write([byte]$lado); $bw.Write([byte]$lado); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$imagens[$i].Length); $bw.Write([UInt32]$deslocamento)
    $deslocamento += $imagens[$i].Length
}
foreach ($bytes in $imagens) { $bw.Write([byte[]]$bytes) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $raiz 'Assets\Icons\app.ico'), $ico.ToArray())

# ---------------------------------------------------------------------------
# Splash estática: mesmo layout da Views/SplashWindow.xaml (560x340, sempre escura, confete, nome em Pacifico,
# UnC branca no rodapé), sem o texto do passo e a versão, que só a janela mostra. Ao mudar a janela, mudar aqui.
# ---------------------------------------------------------------------------
$pastaFontes = 'file:///' + ((Join-Path $raiz 'Assets\Fonts\') -replace '\\', '/')
$fonte = $pastaFontes + '#Inter'
$fonteMarca = $pastaFontes + '#Pacifico'
$uriLogo = (Join-Path $brand 'unc_logo_branca.png') -replace '\\', '/'
$xaml = @"
<Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Width="560" Height="340">
  <Border CornerRadius="16" Background="#1B1B1F" BorderBrush="#2E2E35" BorderThickness="1" ClipToBounds="True">
    <Grid>
      <Canvas>
        <Ellipse Canvas.Left="474" Canvas.Top="30" Width="14" Height="14" Fill="#7C5AD9"/>
        <Ellipse Canvas.Left="508" Canvas.Top="68" Width="8" Height="8" Fill="#E0A94F"/>
        <Ellipse Canvas.Left="440" Canvas.Top="70" Width="6" Height="6" Fill="#B5A0F0"/>
        <Rectangle Canvas.Left="500" Canvas.Top="22" Width="18" Height="6" RadiusX="3" RadiusY="3" Fill="#B5A0F0"><Rectangle.RenderTransform><RotateTransform Angle="35"/></Rectangle.RenderTransform></Rectangle>
        <Rectangle Canvas.Left="452" Canvas.Top="104" Width="14" Height="5" RadiusX="2.5" RadiusY="2.5" Fill="#5CC98A"><Rectangle.RenderTransform><RotateTransform Angle="-25"/></Rectangle.RenderTransform></Rectangle>
        <Rectangle Canvas.Left="520" Canvas.Top="112" Width="12" Height="5" RadiusX="2.5" RadiusY="2.5" Fill="#7C5AD9"><Rectangle.RenderTransform><RotateTransform Angle="60"/></Rectangle.RenderTransform></Rectangle>
        <Ellipse Canvas.Left="40" Canvas.Top="232" Width="12" Height="12" Fill="#E0A94F"/>
        <Ellipse Canvas.Left="78" Canvas.Top="268" Width="7" Height="7" Fill="#7C5AD9"/>
        <Ellipse Canvas.Left="26" Canvas.Top="282" Width="6" Height="6" Fill="#5CC98A"/>
        <Rectangle Canvas.Left="62" Canvas.Top="222" Width="16" Height="6" RadiusX="3" RadiusY="3" Fill="#B5A0F0"><Rectangle.RenderTransform><RotateTransform Angle="-40"/></Rectangle.RenderTransform></Rectangle>
        <Rectangle Canvas.Left="100" Canvas.Top="246" Width="12" Height="5" RadiusX="2.5" RadiusY="2.5" Fill="#7C5AD9"><Rectangle.RenderTransform><RotateTransform Angle="20"/></Rectangle.RenderTransform></Rectangle>
      </Canvas>
      <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center" Margin="0,0,0,24">
        <TextBlock Text="Fest Agenda" FontFamily="$fonteMarca" FontSize="52" Foreground="#ECECEF" HorizontalAlignment="Center"/>
        <TextBlock Text="AGENDA DE FESTAS E EVENTOS" FontFamily="$fonte" FontSize="11" FontWeight="SemiBold" Foreground="#B5A0F0" HorizontalAlignment="Center" Margin="0,2,0,0"/>
        <StackPanel Margin="0,28,0,0" Width="240">
          <Border Height="4" CornerRadius="2" Background="#2E2E35"/>
          <TextBlock Text=" " FontFamily="$fonte" FontSize="12" Margin="0,10,0,0"/>
        </StackPanel>
      </StackPanel>
      <Image Source="file:///$uriLogo" Height="20" Stretch="Uniform" Opacity="0.75" RenderOptions.BitmapScalingMode="HighQuality"
             HorizontalAlignment="Left" VerticalAlignment="Bottom" Margin="20,0,0,16"/>
    </Grid>
  </Border>
</Grid>
"@
$elemento = [System.Windows.Markup.XamlReader]::Parse($xaml)
$elemento.Measure((New-Object System.Windows.Size(560, 340)))
$elemento.Arrange((New-Object System.Windows.Rect(0, 0, 560, 340)))
$elemento.UpdateLayout()
# A imagem é carregada de forma assíncrona: deixa o dispatcher processar antes de renderizar
[System.Windows.Threading.Dispatcher]::CurrentDispatcher.Invoke([Action] {}, [System.Windows.Threading.DispatcherPriority]::ApplicationIdle)
$rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(560, 340, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
$rtb.Render($elemento)
Salvar-Png $rtb (Join-Path $raiz 'Assets\Splash\splash.png')

# ---------------------------------------------------------------------------
# Instalador (Inno Setup): imagem lateral (boas-vindas/conclusão) e do topo das páginas, em BMP 24 bits
# (o Inno escolhe o tamanho conforme o zoom do Windows). Mesmo visual da splash: grafite, confete, nome em Pacifico.
# ---------------------------------------------------------------------------
$pastaInstalador = Join-Path $raiz 'Installer\Imagens'
New-Item -ItemType Directory -Force -Path $pastaInstalador | Out-Null

function Salvar-Bmp($visual, [int]$largura, [int]$altura, [string]$caminho) {
    $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($largura, $altura, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($visual)
    $bgr = New-Object System.Windows.Media.Imaging.FormatConvertedBitmap($rtb, [System.Windows.Media.PixelFormats]::Bgr24, $null, 0)
    $enc = New-Object System.Windows.Media.Imaging.BmpBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bgr))
    $fs = [System.IO.File]::Create($caminho)
    try { $enc.Save($fs) } finally { $fs.Dispose() }
}

# Lateral: desenhada em 164x314 e ampliada por escala (vetorial, sem perder nitidez)
$xamlLateral = @"
<Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Width="164" Height="314" Background="#1B1B1F">
  <Canvas>
    <Ellipse Canvas.Left="118" Canvas.Top="22" Width="10" Height="10" Fill="#7C5AD9"/>
    <Ellipse Canvas.Left="142" Canvas.Top="50" Width="6" Height="6" Fill="#E0A94F"/>
    <Ellipse Canvas.Left="100" Canvas.Top="52" Width="5" Height="5" Fill="#B5A0F0"/>
    <Rectangle Canvas.Left="136" Canvas.Top="16" Width="14" Height="5" RadiusX="2.5" RadiusY="2.5" Fill="#B5A0F0"><Rectangle.RenderTransform><RotateTransform Angle="35"/></Rectangle.RenderTransform></Rectangle>
    <Rectangle Canvas.Left="112" Canvas.Top="78" Width="11" Height="4" RadiusX="2" RadiusY="2" Fill="#5CC98A"><Rectangle.RenderTransform><RotateTransform Angle="-25"/></Rectangle.RenderTransform></Rectangle>
    <Ellipse Canvas.Left="18" Canvas.Top="240" Width="9" Height="9" Fill="#E0A94F"/>
    <Ellipse Canvas.Left="44" Canvas.Top="262" Width="5" Height="5" Fill="#7C5AD9"/>
    <Ellipse Canvas.Left="12" Canvas.Top="268" Width="5" Height="5" Fill="#5CC98A"/>
    <Rectangle Canvas.Left="30" Canvas.Top="236" Width="12" Height="5" RadiusX="2.5" RadiusY="2.5" Fill="#B5A0F0"><Rectangle.RenderTransform><RotateTransform Angle="-40"/></Rectangle.RenderTransform></Rectangle>
    <Rectangle Canvas.Left="60" Canvas.Top="250" Width="10" Height="4" RadiusX="2" RadiusY="2" Fill="#7C5AD9"><Rectangle.RenderTransform><RotateTransform Angle="20"/></Rectangle.RenderTransform></Rectangle>
  </Canvas>
  <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center" Margin="0,0,0,20">
    <TextBlock Text="Fest" FontFamily="$fonteMarca" FontSize="40" Foreground="#ECECEF" HorizontalAlignment="Center"/>
    <TextBlock Text="Agenda" FontFamily="$fonteMarca" FontSize="40" Foreground="#ECECEF" HorizontalAlignment="Center" Margin="0,-22,0,0"/>
    <TextBlock Text="FESTAS E EVENTOS" FontFamily="$fonte" FontSize="8" FontWeight="SemiBold" Foreground="#B5A0F0" HorizontalAlignment="Center" Margin="0,4,0,0"/>
  </StackPanel>
  <Image Source="file:///$uriLogo" Height="16" Stretch="Uniform" Opacity="0.75" RenderOptions.BitmapScalingMode="HighQuality"
         HorizontalAlignment="Center" VerticalAlignment="Bottom" Margin="0,0,0,14"/>
</Grid>
"@
foreach ($escala in 1, 1.5, 2) {
    $elemento = [System.Windows.Markup.XamlReader]::Parse($xamlLateral)
    $l = [int](164 * $escala); $a = [int](314 * $escala)
    $elemento.LayoutTransform = New-Object System.Windows.Media.ScaleTransform($escala, $escala)
    $elemento.Measure((New-Object System.Windows.Size($l, $a)))
    $elemento.Arrange((New-Object System.Windows.Rect(0, 0, $l, $a)))
    $elemento.UpdateLayout()
    [System.Windows.Threading.Dispatcher]::CurrentDispatcher.Invoke([Action] {}, [System.Windows.Threading.DispatcherPriority]::ApplicationIdle)
    Salvar-Bmp $elemento $l $a (Join-Path $pastaInstalador "lateral-$l.bmp")
}

# Topo: o ícone do app (quadrado roxo com o símbolo branco) sobre fundo branco, que é o do topo das páginas
foreach ($lado in 55, 83, 110) {
    $quadro = QuadradoIcone $simboloBranco ($lado - 6) ([Math]::Round(($lado - 6) * 0.16))
    $visual = New-Object System.Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()
    $dc.DrawRectangle([System.Windows.Media.Brushes]::White, $null, (New-Object System.Windows.Rect(0, 0, $lado, $lado)))
    $dc.DrawImage($quadro, (New-Object System.Windows.Rect(3, 3, ($lado - 6), ($lado - 6))))
    $dc.Close()
    Salvar-Bmp $visual $lado $lado (Join-Path $pastaInstalador "topo-$lado.bmp")
}

Write-Host 'Recursos da marca gerados.'

