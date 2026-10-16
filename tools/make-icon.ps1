# Genera ClaudeSessionManager\app.ico (e un'anteprima PNG) disegnando l'icona in vettoriale con WPF.
# Uso:  powershell -STA -ExecutionPolicy Bypass -File tools\make-icon.ps1
#
# Disegno: quadrato arrotondato color terracotta con una scintilla bianca a raggi arrotondati
# e, in basso a destra, un badge bianco con due frecce su/giù (export/import).

Add-Type -AssemblyName PresentationCore, WindowsBase

$root   = Split-Path -Parent $PSScriptRoot
$icoOut = Join-Path $root 'ClaudeSessionManager\app.ico'
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

function Render-Png([int]$size) {
    $s  = $size / 256.0
    $dv = [System.Windows.Media.DrawingVisual]::new()
    $dc = $dv.RenderOpen()
    $dc.PushTransform([System.Windows.Media.ScaleTransform]::new($s, $s))

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

"Creato $icoOut"
"Anteprima $pngOut"
