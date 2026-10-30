# Genera le icone in ClaudeSessionManager\Assets disegnandole in vettoriale con WPF (solo Windows):
#   icon.ico      exe Windows (a tutta superficie)
#   AppIcon.icns  app macOS (griglia Apple: corpo 824/1024 centrato, il resto trasparente)
#   icon.png      icona della finestra
# e l'anteprima tools\icon-preview.png per il README.
# Uso:  powershell -STA -ExecutionPolicy Bypass -File tools\make-icon.ps1
#
# Disegno: quadrato arrotondato color terracotta con una scintilla bianca a raggi arrotondati
# e, in basso a destra, un badge bianco con due frecce su/giù (export/import).

Add-Type -AssemblyName PresentationCore, WindowsBase

$root   = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'ClaudeSessionManager\Assets'
$icoOut = Join-Path $assets 'icon.ico'
$icnsOut = Join-Path $assets 'AppIcon.icns'
$winPng = Join-Path $assets 'icon.png'
$pngOut = Join-Path $root 'tools\icon-preview.png'

function New-Brush([string]$hex) {
    $b = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString($hex))
    $b.Freeze(); $b
}

$orange = New-Brush '#D97757'
$dark   = New-Brush '#B85C3D'
$white  = New-Brush '#FFFFFF'

# Raggi della scintilla: angolo (gradi), lunghezza (0..1), spessore (0..1), relativi al raggio.
$rays = @(
    @(-90, 1.00, 0.17), @(-61, 0.76, 0.13), @(-31, 0.93, 0.15), @( -1, 0.72, 0.12),
    @( 29, 0.96, 0.16), @( 58, 0.79, 0.13), @( 89, 0.99, 0.17), @(119, 0.74, 0.12),
    @(150, 0.94, 0.15), @(181, 0.77, 0.13), @(211, 0.90, 0.15), @(240, 0.71, 0.12)
)

function Draw-Spark($dc, [double]$cx, [double]$cy, [double]$r) {
    foreach ($ray in $rays) {
        $a   = $ray[0] * [Math]::PI / 180
        $pen = [System.Windows.Media.Pen]::new($white, $ray[2] * $r * 1.5)
        $pen.StartLineCap = [System.Windows.Media.PenLineCap]::Round
        $pen.EndLineCap   = [System.Windows.Media.PenLineCap]::Round
        $from = [System.Windows.Point]::new($cx, $cy)
        $to   = [System.Windows.Point]::new($cx + [Math]::Cos($a) * $r * $ray[1], $cy + [Math]::Sin($a) * $r * $ray[1])
        $dc.DrawLine($pen, $from, $to)
    }
}

# Frecce su/giù disegnate in un riquadro 100x100, poi spostate e scalate sul badge.
$arrows = [System.Windows.Media.Geometry]::Parse('M34 86V38H20L38 14L56 38H42V86Z M58 14V62H44L62 86L80 62H66V14Z')

function Render-Png([int]$size, [bool]$macGrid = $false) {
    $s  = $size / 256.0
    $dv = [System.Windows.Media.DrawingVisual]::new()
    $dc = $dv.RenderOpen()
    $dc.PushTransform([System.Windows.Media.ScaleTransform]::new($s, $s))
    if ($macGrid) {
        # Il corpo (8..248 su 256) va in 100..924 su 1024, cioè 25..231 su 256.
        $k = 206.0 / 240.0
        $g = [System.Windows.Media.TransformGroup]::new()
        $g.Children.Add([System.Windows.Media.ScaleTransform]::new($k, $k))
        $g.Children.Add([System.Windows.Media.TranslateTransform]::new(25 - 8 * $k, 25 - 8 * $k))
        $dc.PushTransform($g)
    }

    # Sfondo
    $dc.DrawRoundedRectangle($orange, $null, [System.Windows.Rect]::new(8, 8, 240, 240), 52, 52)

    # Scintilla
    Draw-Spark $dc 96 96 70

    # Badge import/export
    $dc.DrawEllipse($dark,  $null, [System.Windows.Point]::new(184, 184), 62, 62)
    $dc.DrawEllipse($white, $null, [System.Windows.Point]::new(184, 184), 54, 54)
    $t = [System.Windows.Media.TransformGroup]::new()
    $t.Children.Add([System.Windows.Media.ScaleTransform]::new(1.15, 1.15))
    $t.Children.Add([System.Windows.Media.TranslateTransform]::new(126.5, 126.5))
    $dc.PushTransform($t)
    $dc.DrawGeometry($orange, $null, $arrows)
    $dc.Pop()

    if ($macGrid) { $dc.Pop() }
    $dc.Pop(); $dc.Close()

    $bmp = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bmp.Render($dv)
    $enc = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bmp))
    $ms = [System.IO.MemoryStream]::new()
    $enc.Save($ms)
    , $ms.ToArray()
}

$sizes  = 16, 24, 32, 48, 64, 128, 256
$images = @{}
foreach ($sz in $sizes) { $images[$sz] = Render-Png $sz }
[System.IO.File]::WriteAllBytes($pngOut, $images[256])
[System.IO.File]::WriteAllBytes($winPng, $images[256])

# File .ico con immagini PNG incorporate (supportato da Windows Vista in poi).
$ms = [System.IO.MemoryStream]::new()
$bw = [System.IO.BinaryWriter]::new($ms)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
foreach ($sz in $sizes) {
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$images[$sz].Length); $bw.Write([UInt32]$offset)
    $offset += $images[$sz].Length
}
foreach ($sz in $sizes) { $bw.Write($images[$sz]) }
$bw.Flush()
[System.IO.File]::WriteAllBytes($icoOut, $ms.ToArray())

# File .icns con immagini PNG incorporate (big-endian).
$icns = @(@('icp4',16),@('icp5',32),@('icp6',64),@('ic07',128),@('ic08',256),@('ic09',512),@('ic10',1024),@('ic11',32),@('ic12',64),@('ic13',256),@('ic14',512))
$macImages = @{}
foreach ($e in $icns) { if (-not $macImages.ContainsKey($e[1])) { $macImages[$e[1]] = Render-Png $e[1] $true } }
function BE([System.IO.Stream]$st, [int]$v) { $st.Write([byte[]]((($v -shr 24) -band 255), (($v -shr 16) -band 255), (($v -shr 8) -band 255), ($v -band 255)), 0, 4) }
$total = 8; foreach ($e in $icns) { $total += 8 + $macImages[$e[1]].Length }
$fs = [System.IO.MemoryStream]::new()
$fs.Write([Text.Encoding]::ASCII.GetBytes('icns'), 0, 4); BE $fs $total
foreach ($e in $icns) {
    $data = $macImages[$e[1]]
    $fs.Write([Text.Encoding]::ASCII.GetBytes($e[0]), 0, 4); BE $fs (8 + $data.Length)
    $fs.Write($data, 0, $data.Length)
}
[System.IO.File]::WriteAllBytes($icnsOut, $fs.ToArray())

"Creato $icoOut"
"Creato $icnsOut"
"Creato $winPng"
"Anteprima $pngOut"
