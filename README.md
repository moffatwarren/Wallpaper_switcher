# Windows Wallpaper & Lockscreen Switcher

A lightweight tool to randomly change your **Windows Desktop Wallpaper** and **Lock Screen Background** from a folder of images.

## Features
- **100% Silent Execution**: Double-clicking the shortcut or batch file changes your wallpapers in the background with zero command prompt popups or flashing terminal windows.
- Updates both **Desktop Wallpaper** and **Lock Screen** simultaneously.
- Supports `.jpg`, `.jpeg`, `.png`, `.bmp`, and `.webp` images.
- Native Windows support (requires zero external software or Python dependencies).

---

## Setup (any PC, any user)
1. Clone or download this repo anywhere.
2. Create a folder named **`Wallpapers`** inside your **Pictures** folder and put your images in it.
   (OneDrive-redirected Pictures folders are detected automatically.)
3. Double-click **`Make-Shortcut.bat`** to add the **`Switch Wallpaper`** shortcut to your Desktop.

If the folder is missing or empty, a popup tells you instead of failing silently.

> If you move the repo folder, run `Make-Shortcut.bat` again so the shortcut points to the new location.

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
# Standard usage (uses <Pictures>\Wallpapers)
.\Set-RandomWallpaper.ps1

# Use a different folder
.\Set-RandomWallpaper.ps1 -FolderPath "D:\Images\Backgrounds"

# Set DIFFERENT random images for Desktop and Lock Screen
.\Set-RandomWallpaper.ps1 -SeparateLockscreen

# Only change Desktop wallpaper
.\Set-RandomWallpaper.ps1 -DesktopOnly

# Only change Lock Screen background
.\Set-RandomWallpaper.ps1 -LockscreenOnly
```

> Run it with Windows PowerShell (`powershell.exe`), not PowerShell 7 (`pwsh`). The Lock Screen API is only available in Windows PowerShell. The shortcut and batch launcher already do this.

---

## Files Included
- `Make-Shortcut.bat` - Double-clickable batch file to run `Create-DesktopShortcut.ps1`.
- `Create-DesktopShortcut.ps1` - Script to create/update the silent Desktop shortcut.
- `Set-RandomWallpaper.ps1` - Main wallpaper switching script using Win32 and WinRT APIs.
- `RunSilent.vbs` - Silent VBScript wrapper to suppress console window popups.
- `Switch-Wallpaper.bat` - Double-clickable silent batch launcher.
