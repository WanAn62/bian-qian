Add-Type -AssemblyName System.Drawing

$size = 64
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

function New-RoundRect([System.Drawing.Graphics]$gg, [System.Drawing.Brush]$b, [float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddArc($x, $y, $r, $r, 180, 90)
  $p.AddArc($x + $w - $r, $y, $r, $r, 270, 90)
  $p.AddArc($x + $w - $r, $y + $h - $r, $r, $r, 0, 90)
  $p.AddArc($x, $y + $h - $r, $r, $r, 90, 90)
  $p.CloseFigure()
  $gg.FillPath($b, $p)
}

$accent = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 217, 126, 95))
$line   = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 206, 197, 185))

New-RoundRect $g $accent 2 2 60 60 14
New-RoundRect $g ([System.Drawing.Brushes]::White) 14 12 36 40 8
$g.FillRectangle($line,   20, 23, 24, 4)
$g.FillRectangle($line,   20, 31, 24, 4)
$g.FillRectangle($accent, 20, 39, 15, 4)

$ico = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
$dir = Join-Path $PSScriptRoot "..\src\Notelet\Assets"
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$fs = [System.IO.File]::Create((Join-Path $dir "app.ico"))
$ico.Save($fs)
$fs.Close()
$g.Dispose(); $bmp.Dispose()
Write-Output "icon written"
