@echo off
title Setup Desktop Shortcut
echo ========================================================
echo   Setting up Desktop Shortcut for Wallpaper Switcher
echo ========================================================
echo.

powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0Create-DesktopShortcut.ps1"

echo.
pause
