<#
.SYNOPSIS
    Installs (or uninstalls) Wallpaper Picker for the current user.
.DESCRIPTION
    Install:
      - Compiles WallpaperPicker.cs with the C# compiler that ships with Windows (.NET Framework 4.x)
        into %LOCALAPPDATA%\WallpaperPicker, along with Set-LockScreen.ps1
      - Adds a "Wallpaper Picker" Start Menu shortcut
      - Creates <Pictures>\Wallpapers if needed, with a starter image if it has no images
      - Keeps an existing "Start with Windows" setting pointing at the installed exe
      - Starts the app
    Uninstall removes the app folder, the shortcut and the startup entry. Your wallpapers are left alone.
.PARAMETER Uninstall
    Remove Wallpaper Picker instead of installing it.
#>

[CmdletBinding()]
param([switch]$Uninstall)

$ErrorActionPreference = 'Stop'

$appName     = 'Wallpaper Picker'
$installDir  = Join-Path $env:LOCALAPPDATA 'WallpaperPicker'
$exePath     = Join-Path $installDir 'WallpaperPicker.exe'
$shortcut    = Join-Path ([Environment]::GetFolderPath('Programs')) "$appName.lnk"
$runKey      = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runValue    = 'WallpaperPicker'   # must match TrayApp.RunValue in WallpaperPicker.cs
$wallpapers  = Join-Path ([Environment]::GetFolderPath('MyPictures')) 'Wallpapers'

# The exe can't be overwritten or deleted while it's running
function Stop-Picker {
    Get-Process WallpaperPicker -ErrorAction SilentlyContinue | ForEach-Object {
        $_ | Stop-Process -Force
        $_.WaitForExit(5000) | Out-Null
    }
}

if ($Uninstall) {
    Stop-Picker
    Remove-ItemProperty -Path $runKey -Name $runValue -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $shortcut -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $installDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "[+] $appName uninstalled. Your wallpapers in $wallpapers were not touched." -ForegroundColor Green
    return
}

# ---------- Icon: three slanted cards, the middle one in the accent color ----------

function New-IconFile([string]$path) {
    Add-Type -AssemblyName System.Drawing
    $sizes = 16, 20, 24, 32, 40, 48, 64, 256

    $images = foreach ($size in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = 'AntiAlias'
        $g.PixelOffsetMode = 'HighQuality'
        $g.ScaleTransform($size / 32.0, $size / 32.0)   # artwork is designed on a 32x32 grid
        $side = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235, 255, 255, 255))
        $accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0xF0, 0x87, 0x6A))
        foreach ($card in @(@(0, $side), @(10.5, $accent), @(21, $side))) {
            $x = [single]$card[0]
            $g.FillPolygon($card[1], [System.Drawing.PointF[]]@(
                (New-Object System.Drawing.PointF ($x + 5), 3),
                (New-Object System.Drawing.PointF ($x + 11), 3),
                (New-Object System.Drawing.PointF ($x + 6), 29),
                (New-Object System.Drawing.PointF $x, 29)))
        }
        $g.Dispose()

        $ms = New-Object System.IO.MemoryStream
        if ($size -ge 256) {
            $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        } else {
            # Classic 32bpp DIB entry (best compatibility for small sizes)
            $w = New-Object System.IO.BinaryWriter $ms
            $w.Write([int]40); $w.Write([int]$size); $w.Write([int]($size * 2)); $w.Write([int16]1); $w.Write([int16]32)
            $w.Write((New-Object byte[] 24))
            for ($y = $size - 1; $y -ge 0; $y--) {          # bottom-up BGRA
                for ($x = 0; $x -lt $size; $x++) {
                    $c = $bmp.GetPixel($x, $y)
                    $w.Write([byte]$c.B); $w.Write([byte]$c.G); $w.Write([byte]$c.R); $w.Write([byte]$c.A)
                }
            }
            $w.Write((New-Object byte[] ([math]::Ceiling($size / 32.0) * 4 * $size)))  # AND mask, unused
            $w.Flush()
        }
        $bmp.Dispose()
        , $ms.ToArray()
    }

    $w = New-Object System.IO.BinaryWriter ([System.IO.File]::Create($path))
    $w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
        $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([int16]0)
        $w.Write([int16]1); $w.Write([int16]32)
        $w.Write([int]$images[$i].Length); $w.Write([int]$offset)
        $offset += $images[$i].Length
    }
    foreach ($img in $images) { $w.Write([byte[]]$img) }
    $w.Close()
}

# ---------- Build ----------

$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fw 'csc.exe'
if (-not (Test-Path $csc)) { throw ".NET Framework 4 compiler not found at $csc" }

Stop-Picker
New-Item -ItemType Directory -Force -Path $installDir | Out-Null

# Also kept next to the exe so the Start Menu shortcut can point at it directly
$icon = Join-Path $installDir 'WallpaperPicker.ico'
New-IconFile $icon

$wpf = Join-Path $fw 'WPF'
& $csc /nologo /target:winexe /optimize+ "/out:$exePath" `
    "/win32icon:$icon" "/resource:$icon,WallpaperPicker.ico" `
    "/r:$wpf\PresentationCore.dll" "/r:$wpf\PresentationFramework.dll" "/r:$wpf\WindowsBase.dll" `
    /r:System.Xaml.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    (Join-Path $PSScriptRoot 'WallpaperPicker.cs')
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

# The picker calls this to set the Lock Screen; it must sit next to the exe
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Set-LockScreen.ps1') -Destination $installDir -Force
Write-Host "[+] Installed to $installDir" -ForegroundColor Green

# ---------- Start Menu shortcut ----------

$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut($shortcut)
$lnk.TargetPath = $exePath
$lnk.WorkingDirectory = $installDir
$lnk.Description = 'Browse and set wallpapers (Ctrl+Alt+W)'
$lnk.IconLocation = "$icon,0"
$lnk.Save()
# Start Menu search caches icons; ask Windows to refresh so it doesn't keep a generic one
& (Join-Path $env:WINDIR 'System32\ie4uinit.exe') -show
Write-Host "[+] Start Menu shortcut: $appName" -ForegroundColor Green

# ---------- Keep "Start with Windows" pointing at the installed copy ----------

if (Get-ItemProperty -Path $runKey -Name $runValue -ErrorAction SilentlyContinue) {
    Set-ItemProperty -Path $runKey -Name $runValue -Value "`"$exePath`" --tray"
    Write-Host '[+] Start with Windows updated' -ForegroundColor Green
}

# ---------- Wallpapers folder ----------

New-Item -ItemType Directory -Force -Path $wallpapers | Out-Null
$existing = @(Get-ChildItem -LiteralPath $wallpapers -File |
    Where-Object { $_.Extension -match '^\.(jpg|jpeg|png|bmp|webp)$' })
if ($existing.Count -eq 0) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Lowpoly_Street.png') -Destination $wallpapers
    Write-Host "[+] Added a starter wallpaper to $wallpapers" -ForegroundColor Green
} else {
    Write-Host "[+] Wallpaper folder: $wallpapers ($($existing.Count) images)" -ForegroundColor Green
}

# ---------- Launch ----------

# Start in the tray: the installer's console holds the focus, so a picker shown now would hide right away
Start-Process -FilePath $exePath -ArgumentList '--tray'
Write-Host "[+] $appName is running in the tray. Press Ctrl+Alt+W to open it." -ForegroundColor Green
