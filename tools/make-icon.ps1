# Genera ClaudeSessionManager\app.ico (e un'anteprima PNG) disegnando l'icona in vettoriale con WPF.
# Uso:  powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
#
# Disegno: quadrato arrotondato color terracotta con una "scintilla" bianca (Claude)
# e, in basso a destra, un badge bianco con due frecce su/giù (import/export).

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

function Get-Spark([double]$cx, [double]$cy, [double]$r) {
    # 12 raggi a petalo, lunghi e corti alternati.
    $g = [System.Windows.Media.StreamGeometry]::new()
    $g.FillRule = [System.Windows.Media.FillRule]::Nonzero
    $ctx = $g.Open()
    for ($i = 0; $i -lt 12; $i++) {
        $a   = $i * [Math]::PI / 6 - [Math]::PI / 2
        $len = if ($i % 2 -eq 0) { $r } else { $r * 0.74 }
        $w   = $r * 0.105
        $tip = [System.Windows.Point]::new($cx + [Math]::Cos($a) * $len, $cy + [Math]::Sin($a) * $len)
        $mid = $len * 0.55
        $mx  = $cx + [Math]::Cos($a) * $mid; $my = $cy + [Math]::Sin($a) * $mid
        $px  = -[Math]::Sin($a) * $w;        $py = [Math]::Cos($a) * $w
        $ctx.BeginFigure([System.Windows.Point]::new($cx, $cy), $true, $true)
        $ctx.LineTo([System.Windows.Point]::new($mx + $px, $my + $py), $true, $true)
        $ctx.LineTo($tip, $true, $true)
        $ctx.LineTo([System.Windows.Point]::new($mx - $px, $my - $py), $true, $true)
    }
    $ctx.Close(); $g.Freeze(); $g
}

function Get-Arrow([double]$x, [double]$yFrom, [double]$yTo, [double]$w) {
    # Freccia verticale piena da yFrom a yTo (punta su yTo).
    $dir   = [Math]::Sign($yTo - $yFrom)
    $headH = $w * 1.15
    $neck  = $yTo - $dir * $headH
    $g = [System.Windows.Media.StreamGeometry]::new()
    $ctx = $g.Open()
    $ctx.BeginFigure([System.Windows.Point]::new($x - $w * 0.28, $yFrom), $true, $true)
    $ctx.LineTo([System.Windows.Point]::new($x - $w * 0.28, $neck), $true, $true)
    $ctx.LineTo([System.Windows.Point]::new($x - $w * 0.75, $neck), $true, $true)
    $ctx.LineTo([System.Windows.Point]::new($x, $yTo), $true, $true)
    $ctx.LineTo([System.Windows.Point]::new($x + $w * 0.75, $neck), $true, $true)
    $ctx.LineTo([System.Windows.Point]::new($x + $w * 0.28, $neck), $true, $true)
    $ctx.LineTo([System.Windows.Point]::new($x + $w * 0.28, $yFrom), $true, $true)
    $ctx.Close(); $g.Freeze(); $g
}

function Render-Png([int]$size) {
    $s  = $size / 256.0
    $dv = [System.Windows.Media.DrawingVisual]::new()
    $dc = $dv.RenderOpen()
    $dc.PushTransform([System.Windows.Media.ScaleTransform]::new($s, $s))

    # Sfondo
    $dc.DrawRoundedRectangle($orange, $null, [System.Windows.Rect]::new(8, 8, 240, 240), 52, 52)

    # Scintilla
    $dc.DrawGeometry($white, $null, (Get-Spark 104 104 78))

    # Badge import/export
    $dc.DrawEllipse($dark,  $null, [System.Windows.Point]::new(184, 184), 66, 66)
    $dc.DrawEllipse($white, $null, [System.Windows.Point]::new(184, 184), 58, 58)
    $dc.DrawGeometry($orange, $null, (Get-Arrow 160 220 146 30))   # su  = export
    $dc.DrawGeometry($orange, $null, (Get-Arrow 208 148 222 30))   # giù = import

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
