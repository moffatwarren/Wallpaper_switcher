# Creates a Desktop shortcut that runs the wallpaper switcher completely silently (no terminal popups)
$desktopPath = [System.IO.Path]::Combine($env:USERPROFILE, "Desktop", "Switch Wallpaper.lnk")
$vbsPath = Join-Path $PSScriptRoot "RunSilent.vbs"

$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut($desktopPath)
$Shortcut.TargetPath = "wscript.exe"
$Shortcut.Arguments = "`"$vbsPath`""
$Shortcut.WorkingDirectory = $PSScriptRoot
$Shortcut.Description = "Randomly change Desktop Wallpaper and Lock Screen"
$Shortcut.IconLocation = "shell32.dll,248" # Wallpaper icon
$Shortcut.Save()

Write-Host "[+] Silent Desktop shortcut 'Switch Wallpaper' updated!" -ForegroundColor Green
