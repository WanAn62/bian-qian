param([string]$Title = "")

Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class Native {
  public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
  public struct RECT { public int Left, Top, Right, Bottom; }
  public static List<Tuple<IntPtr, string>> WindowsOf(uint pid) {
    var list = new List<Tuple<IntPtr, string>>();
    EnumWindows((h, l) => {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) {
        var sb = new StringBuilder(256); GetWindowText(h, sb, 256);
        list.Add(Tuple.Create(h, sb.ToString()));
      }
      return true;
    }, IntPtr.Zero);
    return list;
  }
}
"@

[Native]::SetProcessDPIAware() | Out-Null

$outFull = "D:\github\Notelet\docs\screenshot.png"
New-Item -ItemType Directory -Force -Path (Split-Path $outFull) | Out-Null

$p = Get-Process Notelet -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { Write-Output "NO-PROCESS"; exit 1 }

$wins = [Native]::WindowsOf($p.Id)
$target = [IntPtr]::Zero
foreach ($w in $wins) {
  if ($Title -ne "" -and $w.Item2 -like "*$Title*") { $target = $w.Item1; break }
}
if ($target -eq [IntPtr]::Zero -and $Title -eq "") { $target = $p.MainWindowHandle }
if ($target -eq [IntPtr]::Zero) { Write-Output "NO-WINDOW"; exit 1 }

$r = New-Object Native+RECT
[Native]::GetWindowRect($target, [ref]$r) | Out-Null
$w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
if ($w -lt 100 -or $h -lt 100) { Write-Output "BAD-RECT $w x $h"; exit 1 }

$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[Native]::PrintWindow($target, $hdc, 2) | Out-Null
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
Write-Output ("ok '{0}' {1}x{2} -> {3}" -f $Title, $w, $h, $prevPath)
