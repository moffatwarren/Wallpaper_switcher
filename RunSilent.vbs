Dim WshShell, fso, scriptPath, exitCode
Set WshShell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

' Resolve the script relative to this file so the repo works from any location
scriptPath = fso.BuildPath(fso.GetParentFolderName(WScript.ScriptFullName), "Set-RandomWallpaper.ps1")

' Run PowerShell completely hidden (0 window flag) and wait for its exit code.
' The script picks the default folder: <Pictures>\Wallpapers
exitCode = WshShell.Run("powershell.exe -ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File """ & scriptPath & """", 0, True)

' Errors are invisible in a hidden window, so surface the common ones
Select Case exitCode
    Case 1
        MsgBox "Wallpaper folder not found." & vbCrLf & vbCrLf & _
            "Create a folder named 'Wallpapers' inside your Pictures folder and add some images.", _
            vbExclamation, "Wallpaper Switcher"
    Case 2
        MsgBox "No images found in your Pictures\Wallpapers folder." & vbCrLf & vbCrLf & _
            "Supported types: .jpg, .jpeg, .png, .bmp, .webp", _
            vbExclamation, "Wallpaper Switcher"
End Select
