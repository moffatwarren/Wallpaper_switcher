Dim WshShell, scriptPath, wallpaperDir
Set WshShell = CreateObject("WScript.Shell")

scriptPath = "C:\Users\moffa\Documents\GitHub\Wallpaper_switcher\Set-RandomWallpaper.ps1"
wallpaperDir = "C:\Users\moffa\Pictures\Wallpapers"

' Run PowerShell completely hidden (0 window flag)
WshShell.Run "powershell.exe -ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File """ & scriptPath & """ -FolderPath """ & wallpaperDir & """", 0, False
