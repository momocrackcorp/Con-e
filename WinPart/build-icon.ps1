# Original WinPart icon; GPL-2.0-or-later. Build-time only, no runtime dependency.
Add-Type -AssemblyName System.Drawing
$bitmap = New-Object System.Drawing.Bitmap 64,64
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(35,86,151)), 4
$blue = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(50,112,196))
$green = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(50,178,130))
$graphics.FillRectangle([System.Drawing.Brushes]::White,6,9,52,46)
$graphics.DrawRectangle($pen,6,9,52,46)
$graphics.DrawLine($pen,6,22,58,22)
$graphics.FillRectangle($blue,12,29,18,19)
$graphics.FillRectangle($green,34,29,18,19)
$icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
$path = Join-Path $PSScriptRoot 'src/app.ico'
$stream = [System.IO.File]::Create($path)
try { $icon.Save($stream) } finally { $stream.Dispose(); $icon.Dispose(); $graphics.Dispose(); $bitmap.Dispose(); $pen.Dispose(); $blue.Dispose(); $green.Dispose() }
