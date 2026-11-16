# Original Con-E application icon, MIT. Generated locally at build time; no runtime dependency.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
function RoundedPath([float]$x,[float]$y,[float]$w,[float]$h,[float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x,$y,$d,$d,180,90); $p.AddArc($x+$w-$d,$y,$d,$d,270,90)
    $p.AddArc($x+$w-$d,$y+$h-$d,$d,$d,0,90); $p.AddArc($x,$y+$h-$d,$d,$d,90,90)
    $p.CloseFigure(); return $p
}
$frames = [System.Collections.Generic.List[object]]::new()
foreach ($size in @(16,20,24,32,40,48,64,96,128,256)) {
    $bitmap = New-Object System.Drawing.Bitmap $size,$size
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.ScaleTransform($size/256.0,$size/256.0)
    $bg = RoundedPath 8 8 240 240 50
    $gradient = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Rectangle]::new(8,8,240,240),[System.Drawing.Color]::FromArgb(42,104,208),[System.Drawing.Color]::FromArgb(29,66,145),[single]90)
    $screen = RoundedPath 42 53 172 123 13
    $white = New-Object System.Drawing.Pen ([System.Drawing.Color]::White),12
    $pulse = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(103,237,185)),12
    $pulse.StartCap = $pulse.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pulse.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.FillPath($gradient,$bg); $g.DrawPath($white,$screen)
    $g.DrawLine($white,128,177,128,207); $g.DrawLine($white,91,209,165,209)
    $points = [System.Drawing.PointF[]]@([System.Drawing.PointF]::new(58,117),[System.Drawing.PointF]::new(87,117),[System.Drawing.PointF]::new(108,92),[System.Drawing.PointF]::new(129,145),[System.Drawing.PointF]::new(150,111),[System.Drawing.PointF]::new(169,117),[System.Drawing.PointF]::new(198,117))
    $g.DrawLines($pulse,$points)
    $png = [System.IO.MemoryStream]::new()
    $bitmap.Save($png,[System.Drawing.Imaging.ImageFormat]::Png)
    $frames.Add(@{Size=$size;Bytes=$png.ToArray()})
    if ($size -eq 256) { $bitmap.Save((Join-Path $PSScriptRoot 'src/app-icon.png'),[System.Drawing.Imaging.ImageFormat]::Png) }
    $png.Dispose(); $pulse.Dispose(); $white.Dispose(); $screen.Dispose(); $gradient.Dispose(); $bg.Dispose(); $g.Dispose(); $bitmap.Dispose()
}
$path = Join-Path $PSScriptRoot 'src/app.ico'
$stream = [System.IO.File]::Create($path); $writer = [System.IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $side = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$side); $writer.Write([byte]$side); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
} finally { $writer.Dispose(); $stream.Dispose() }
