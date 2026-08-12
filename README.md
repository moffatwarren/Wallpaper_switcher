# Windows Wallpaper & Lockscreen Switcher

A lightweight tool to randomly change your **Windows Desktop Wallpaper** and **Lock Screen Background** from a folder of images.

## Features
- **100% Silent Execution**: Double-clicking the shortcut or batch file changes your wallpapers in the background with zero command prompt popups or flashing terminal windows.
- Updates both **Desktop Wallpaper** and **Lock Screen** simultaneously.
- Supports `.jpg`, `.jpeg`, `.png`, `.bmp`, and `.webp` images.
- Native Windows support (requires zero external software or Python dependencies).

---

## How to Use

### 1. Double-Click Desktop Shortcut (Easiest & Silent)
Double-click the **`Switch Wallpaper`** shortcut on your Desktop. Your Desktop wallpaper and Lock Screen background will instantly change in the background silently.

---

### 2. Creating / Refreshing the Desktop Shortcut
Double-click **`Make-Shortcut.bat`** to create or refresh the **`Switch Wallpaper`** shortcut on your Desktop.

---

### 3. Manual PowerShell Command Line Options

If you want to run the script manually in PowerShell with visual output or options:

```powershell
# Standard usage
.\Set-RandomWallpaper.ps1 -FolderPath "C:\Users\moffa\Pictures\Wallpapers"

# Set DIFFERENT random images for Desktop and Lock Screen
.\Set-RandomWallpaper.ps1 -FolderPath "C:\Users\moffa\Pictures\Wallpapers" -SeparateLockscreen

# Only change Desktop wallpaper
.\Set-RandomWallpaper.ps1 -FolderPath "C:\Users\moffa\Pictures\Wallpapers" -DesktopOnly

# Only change Lock Screen background
.\Set-RandomWallpaper.ps1 -FolderPath "C:\Users\moffa\Pictures\Wallpapers" -LockscreenOnly
```

---

## Files Included
- `Make-Shortcut.bat` - Double-clickable batch file to run `Create-DesktopShortcut.ps1`.
- `Create-DesktopShortcut.ps1` - Script to create/update the silent Desktop shortcut.
- `Set-RandomWallpaper.ps1` - Main wallpaper switching script using Win32 and WinRT APIs.
- `RunSilent.vbs` - Silent VBScript wrapper to suppress console window popups.
- `Switch-Wallpaper.bat` - Double-clickable silent batch launcher.
