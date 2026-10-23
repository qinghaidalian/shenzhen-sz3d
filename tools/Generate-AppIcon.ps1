# 生成 Sz3D 应用图标：用 WPF 渲染多分辨率 PNG 并打包为 .ico
# 产物：src/Sz3D.Presentation.WPF/Resources/AppIcon.png 与 AppIcon.ico
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$ErrorActionPreference = 'Stop'
$outDir = Join-Path $PSScriptRoot '..\src\Sz3D.Presentation.WPF\Resources'
$outDir = [System.IO.Path]::GetFullPath($outDir)

function New-LogoBitmap([int]$size) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()

    $rect = New-Object System.Windows.Rect(0, 0, $size, $size)
    [double]$c = [double]$size

    # 背景径向渐变（深蓝 -> 近黑）
    $bg = New-Object System.Windows.Media.RadialGradientBrush
    $bg.GradientOrigin = New-Object System.Windows.Point(0.5, 0.38)
    $bg.Center = New-Object System.Windows.Point(0.5, 0.38)
    $bg.RadiusX = 0.85; $bg.RadiusY = 0.85
    $bg.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.Color]::FromRgb(0x1b,0x4f,0xd6), 0.0)))
    $bg.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.Color]::FromRgb(0x0b,0x2a,0x8f), 0.45)))
    $bg.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.Color]::FromRgb(0x05,0x0b,0x28), 1.0)))

    $radius = [double]($size * 0.16)
    $dc.DrawRoundedRectangle($bg, $null, $rect, $radius, $radius)

    # 底部光带
    $sweep = New-Object System.Windows.Media.LinearGradientBrush
    $sweep.StartPoint = New-Object System.Windows.Point(0,0)
    $sweep.EndPoint   = New-Object System.Windows.Point(1,0)
    $sweep.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.Color]::FromArgb(0,0x2f,0x7b,0xff), 0.0)))
    $sweep.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.Color]::FromArgb(230,0x7c,0xc0,0xff), 0.5)))
    $sweep.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.Color]::FromArgb(0,0x2f,0x7b,0xff), 1.0)))
    $sg = New-Object System.Windows.Media.StreamGeometry
    $ctx = $sg.Open()
    $p0 = New-Object System.Windows.Point(([double](0.03*$c)), ([double](0.80*$c)))
    $p1 = New-Object System.Windows.Point(([double](0.50*$c)), ([double](0.58*$c)))
    $p2 = New-Object System.Windows.Point(([double](0.97*$c)), ([double](0.80*$c)))
    $p3 = New-Object System.Windows.Point(([double](0.97*$c)), ([double](0.875*$c)))
    $p4 = New-Object System.Windows.Point(([double](0.50*$c)), ([double](0.69*$c)))
    $p5 = New-Object System.Windows.Point(([double](0.03*$c)), ([double](0.875*$c)))
    $ctx.BeginFigure($p0, $true, $true)
    $ctx.QuadraticBezierTo($p1, $p2, $true, $false)
    $ctx.LineTo($p3, $true, $false)
    $ctx.QuadraticBezierTo($p4, $p5, $true, $false)
    $ctx.Close()
    $dc.DrawGeometry($sweep, $null, $sg)

    # 文字 Sz3D 渐变
    $word = New-Object System.Windows.Media.LinearGradientBrush
    $word.StartPoint = New-Object System.Windows.Point(0,0)
    $word.EndPoint   = New-Object System.Windows.Point(0,1)
    $word.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.Color]::FromRgb(0xea,0xf4,0xff), 0.0)))
    $word.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.Color]::FromRgb(0x9e,0xc9,0xff), 0.45)))
    $word.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.Color]::FromRgb(0x2f,0x7b,0xff), 1.0)))

    $typeface = New-Object System.Windows.Media.Typeface(
        (New-Object System.Windows.Media.FontFamily("Segoe UI, Arial")),
        [System.Windows.FontStyles]::Normal,
        [System.Windows.FontWeights]::ExtraBold,
        [System.Windows.FontStretches]::Normal)
    $dpi = 96.0
    $ft = New-Object System.Windows.Media.FormattedText(
        "Sz3D",
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Windows.FlowDirection]::LeftToRight,
        $typeface, ($size * 0.34), $word, $dpi)
    $origin = New-Object System.Windows.Point((($size - $ft.Width) / 2.0), (($size - $ft.Height) / 2.0))
    $dc.DrawText($ft, $origin)

    $dc.Close()

    $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($visual)
    return $rtb
}

function Save-Png($bitmap, [string]$path) {
    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $fs = [System.IO.File]::Create($path)
    try { $enc.Save($fs) } finally { $fs.Dispose() }
}

# 单张展示用 PNG（256）
$png256 = New-LogoBitmap 256
Save-Png $png256 (Join-Path $outDir 'AppIcon.png')

# 多分辨率 PNG 内存流，手工组装 ICO
$sizes = 16,24,32,48,64,128,256
$pngBytes = @{}
foreach ($s in $sizes) {
    $bmp = New-LogoBitmap $s
    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bmp))
    $ms = New-Object System.IO.MemoryStream
    $enc.Save($ms)
    $pngBytes[$s] = $ms.ToArray()
    $ms.Dispose()
}

$icoPath = Join-Path $outDir 'AppIcon.ico'
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
try {
    # ICONDIR
    $bw.Write([UInt16]0)      # reserved
    $bw.Write([UInt16]1)      # type = icon
    $bw.Write([UInt16]$sizes.Count)

    $offset = 6 + (16 * $sizes.Count)
    foreach ($s in $sizes) {
        $data = $pngBytes[$s]
        $dim = if ($s -ge 256) { 0 } else { $s }   # 256 用 0 表示
        $bw.Write([Byte]$dim)                       # width
        $bw.Write([Byte]$dim)                       # height
        $bw.Write([Byte]0)                          # color count
        $bw.Write([Byte]0)                          # reserved
        $bw.Write([UInt16]1)                        # planes
        $bw.Write([UInt16]32)                       # bpp
        $bw.Write([UInt32]$data.Length)             # bytes in resource
        $bw.Write([UInt32]$offset)                  # offset
        $offset += $data.Length
    }
    foreach ($s in $sizes) { $bw.Write($pngBytes[$s]) }
}
finally { $bw.Dispose(); $fs.Dispose() }

Write-Output "PNG : $(Join-Path $outDir 'AppIcon.png')"
Write-Output "ICO : $icoPath"
