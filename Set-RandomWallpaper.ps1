<#
.SYNOPSIS
    Randomly changes Windows Desktop Wallpaper and Lock Screen Background from a directory of images.
.DESCRIPTION
    This PowerShell script picks a random image (or separate random images) from a designated directory
    and sets it as your Windows Desktop Wallpaper and/or Lock Screen Background.
.PARAMETER FolderPath
    Path to the directory containing image files (.jpg, .jpeg, .png, .bmp, .webp).
.PARAMETER SeparateLockscreen
    If set, selects a different random image for the Lock Screen instead of using the Desktop image.
.PARAMETER DesktopOnly
    Only update the Desktop wallpaper.
.PARAMETER LockscreenOnly
    Only update the Lock Screen background.
#>

[CmdletBinding()]
param(
    [Parameter(Position=0, Mandatory=$false)]
    [string]$FolderPath = "$HOME\Pictures\Wallpapers",

    [switch]$SeparateLockscreen,
    [switch]$DesktopOnly,
    [switch]$LockscreenOnly
)

# 1. Resolve path
if (-not (Test-Path -Path $FolderPath)) {
    Write-Error "Directory '$FolderPath' does not exist. Please specify a valid folder path."
    exit 1
}

$resolvedPath = (Convert-Path -Path $FolderPath)

# 2. Supported extensions
$images = Get-ChildItem -Path $resolvedPath -File | Where-Object { $_.Extension -match '^\.(jpg|jpeg|png|bmp|webp)$' }

if ($images.Count -eq 0) {
    Write-Warning "No supported image files (.jpg, .jpeg, .png, .bmp, .webp) found in '$resolvedPath'."
    exit 0
}

# 3. Define Win32 API for Desktop Wallpaper
if (-not ([System.Management.Automation.PSTypeName]'WallpaperManager').Type) {
    Add-Type -TypeDefinition @"
    using System;
    using System.Runtime.InteropServices;

    public class WallpaperManager {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
    }
"@
}

function Set-DesktopWallpaper {
    param([string]$ImagePath)
    $SPI_SETDESKWALLPAPER = 0x0014
    $SPIF_UPDATEINIFILE = 0x01
    $SPIF_SENDCHANGE = 0x02
    [WallpaperManager]::SystemParametersInfo($SPI_SETDESKWALLPAPER, 0, $ImagePath, $SPIF_UPDATEINIFILE -bor $SPIF_SENDCHANGE) | Out-Null
    Write-Host "[+] Desktop Wallpaper updated to: $ImagePath" -ForegroundColor Green
}

function Set-LockscreenImage {
    param([string]$ImagePath)
    try {
        # Pre-load Windows Runtime (WinRT) projected types
        [Windows.System.UserProfile.LockScreen, Windows.System.UserProfile, ContentType = WindowsRuntime] | Out-Null
        [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime] | Out-Null
        Add-Type -AssemblyName System.Runtime.WindowsRuntime
        
        $asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { 
            $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' 
        })[0]
        
        $asTaskAction = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { 
            $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncAction' 
        })[0]

        # Get StorageFile asynchronously
        $getFileAsync = [Windows.Storage.StorageFile]::GetFileFromPathAsync($ImagePath)
        $getFileTask = $asTaskGeneric.MakeGenericMethod([Windows.Storage.StorageFile]).Invoke($null, @($getFileAsync))
        $getFileTask.Wait()
        $storageFile = $getFileTask.Result

        # Set LockScreen asynchronously
        $setLockAsync = [Windows.System.UserProfile.LockScreen]::SetImageFileAsync($storageFile)
        $setLockTask = $asTaskAction.Invoke($null, @($setLockAsync))
        $setLockTask.Wait()

        Write-Host "[+] Lock Screen background updated to: $ImagePath" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to set Lock Screen image: $_"
    }
}

# Pick random images
$desktopSelection = ($images | Get-Random).FullName
if ($SeparateLockscreen) {
    $lockscreenSelection = ($images | Get-Random).FullName
} else {
    $lockscreenSelection = $desktopSelection
}

# Execute
if (-not $LockscreenOnly) {
    Set-DesktopWallpaper -ImagePath $desktopSelection
}

if (-not $DesktopOnly) {
    Set-LockscreenImage -ImagePath $lockscreenSelection
}
