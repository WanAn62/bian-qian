Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Native {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

[Native]::SetProcessDPIAware() | Out-Null

$outFull = "D:\github\Notelet\docs\screenshot.png"
New-Item -ItemType Directory -Force -Path (Split-Path $outFull) | Out-Null

$p = Get-Process Notelet -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { Write-Output "NO-WINDOW"; exit 1 }

$r = New-Object Native+RECT
[Native]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
$w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
if ($w -lt 100 -or $h -lt 100) { Write-Output "BAD-RECT $w x $h"; exit 1 }

$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
# PW_RENDERFULLCONTENT (2): 对 DirectComposition/WPF 窗口也能成像，且不要求窗口在前台
[Native]::PrintWindow($p.MainWindowHandle, $hdc, 2) | Out-Null
$g.ReleaseHdc($hdc)
$bmp.Save($outFull, [System.Drawing.Imaging.ImageFormat]::Png)

$scale = [Math]::Min(1.0, 1280.0 / [Math]::Max($w, $h))
$pw2 = [int]($w * $scale); $ph2 = [int]($h * $scale)
$prev = New-Object System.Drawing.Bitmap($pw2, $ph2)
$g2 = [System.Drawing.Graphics]::FromImage($prev)
$g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g2.DrawImage($bmp, 0, 0, $pw2, $ph2)
$prevPath = Join-Path $env:TEMP "notelet-preview.png"
$prev.Save($prevPath, [System.Drawing.Imaging.ImageFormat]::Png)

$g.Dispose(); $bmp.Dispose(); $g2.Dispose(); $prev.Dispose()
Write-Output ("ok {0}x{1} -> {2}" -f $w, $h, $prevPath)
