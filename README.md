# Wallpaper Picker

A lightweight Windows tray app for browsing your wallpapers as a scrollable row of slanted cards and setting the **Desktop Wallpaper** and **Lock Screen** with one keypress.

## Features
- Lives in the system tray and opens instantly with **`Ctrl+Alt+W`**.
- Opens on your current wallpaper, on the monitor your mouse is on.
- Sets the Desktop wallpaper and Lock Screen together, or the Desktop only.
- Picks up new images in the folder automatically.
- Supports `.jpg`, `.jpeg`, `.png`, `.bmp`, and `.webp` images.
- Native Windows, with zero external software, SDKs or Python needed.

---

## Install
1. Clone or download this repo.
2. Double-click **`install.bat`**. It:
   - builds the app into `%LOCALAPPDATA%\WallpaperPicker` (using the C# compiler built into Windows)
   - adds a **Wallpaper Picker** shortcut to the Start Menu
   - creates **`Pictures\Wallpapers`** if it doesn't exist, adding `Lowpoly_Street.png` as a starter image when it has no images (OneDrive-redirected Pictures folders are detected automatically)
   - starts the app in the tray
3. Put your images in **`Pictures\Wallpapers`**.
4. Right-click the tray icon and tick **Start with Windows** so it is always available.

Once installed, the repo folder can be moved or deleted. To update, pull the latest changes and run `install.bat` again; your settings are kept.

> Windows 11 hides new tray icons in the `^` overflow. Drag the icon onto the taskbar to keep it visible.

To remove it, double-click **`uninstall.bat`**. This removes the app, the Start Menu shortcut and the startup entry. Your wallpapers are not touched.

---

## How to Use
Press **`Ctrl+Alt+W`** or click the tray icon to open the picker.

| Key / Mouse | Action |
|---|---|
| `←` `→` / `A` `D` / `H` `L` / mouse wheel | Browse |
| `Home` `End` / `PgUp` `PgDn` | Jump |
| `R` | Random |
| `Enter` or double-click | Set Desktop wallpaper + Lock Screen, then close |
| `Shift+Enter` | Set Desktop wallpaper only, then close |
| `Esc` / `Ctrl+Alt+W` / click elsewhere | Hide |

The tray icon's right-click menu also has **Random wallpaper**, **Open wallpapers folder**, **Start with Windows** and **Exit**.

---

## Files Included
- `install.bat` / `uninstall.bat` - Double-click to install or remove.
- `Install.ps1` - The installer: builds the exe (including its icon), copies files, creates the shortcut and wallpapers folder. Run with `-Uninstall` to remove.
- `WallpaperPicker.cs` - Source for the tray app and picker.
- `Set-LockScreen.ps1` - Sets the Lock Screen image (WinRT API). Installed next to the exe and called by the picker.
- `Lowpoly_Street.png` - Starter wallpaper, copied in when the wallpapers folder has no images.
