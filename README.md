# Pasty

Lightweight Windows clipboard manager built with C#/.NET 8.0 (WPF + WinForms). Runs as a system tray app, monitors the clipboard, stores history in SQLite, and lets you recall/paste items via **Ctrl+Backtick**.

## Build & Run

```powershell
# Debug build
dotnet build src/Pasty/Pasty.csproj

# Release build
dotnet build src/Pasty/Pasty.csproj -c Release

# Run directly
dotnet run --project src/Pasty/Pasty.csproj

# Run in background (PowerShell)
Start-Process dotnet -ArgumentList "run","--project","src/Pasty/Pasty.csproj" -WindowStyle Hidden

# Publish single-file exe (win-x64)
dotnet publish src/Pasty/Pasty.csproj -c Release
```

## Auto-start on Windows Login (no admin required)

1. Publish the app (if not already done):
   ```powershell
   dotnet publish src/Pasty/Pasty.csproj -c Release
   ```

2. Create a startup shortcut:
   ```powershell
   $WshShell = New-Object -ComObject WScript.Shell
   $Shortcut = $WshShell.CreateShortcut("$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup\Pasty.lnk")
   $Shortcut.TargetPath = "D:\code\wsl-mirror\pasty\src\Pasty\bin\Release\net8.0-windows\win-x64\publish\Pasty.exe"
   $Shortcut.WorkingDirectory = "D:\code\wsl-mirror\pasty\src\Pasty\bin\Release\net8.0-windows\win-x64\publish"
   $Shortcut.WindowStyle = 7
   $Shortcut.Save()
   ```

3. Verify the shortcut was created:
   ```powershell
   ls "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup\Pasty.lnk"
   ```

4. Log out and log back in (or restart). Pasty will start automatically.

To remove auto-start:
```powershell
Remove-Item "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup\Pasty.lnk"
```
