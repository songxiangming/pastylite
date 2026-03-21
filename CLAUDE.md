# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet build src/Pasty/Pasty.csproj              # Debug build
dotnet build src/Pasty/Pasty.csproj -c Release    # Release build
dotnet publish src/Pasty/Pasty.csproj -c Release  # Single-file self-contained exe (win-x64)
```

No test project exists yet. No linter is configured.

## Architecture

**Pasty** is a lightweight Windows clipboard manager built with C#/.NET 8.0 (WPF + WinForms). It runs as a system tray app, monitors the clipboard, stores history in SQLite, and lets users recall/paste items via a popup triggered by **Ctrl+Backtick**.

### Core Flow

1. `App.xaml.cs` — Entry point. Enforces single-instance (Mutex), initializes SQLite DB (`pasty.db` next to exe), creates a hidden `MainWindow` for Win32 message handling, and wires up all services.
2. `ClipboardMonitor` — Subscribes to `WM_CLIPBOARDUPDATE` via the hidden window. Reads clipboard in multiple formats (text, HTML, RTF, images, file drops). Deduplicates via SHA256 content hash.
3. `HotkeyManager` — Registers/unregisters global hotkey via `RegisterHotKey` P/Invoke.
4. `PopupWindow`/`PopupViewModel` — MVVM popup UI. Loads 200 recent items, supports fuzzy text search, keyboard navigation. Catppuccin Mocha dark theme (`Resources/Styles.xaml`).
5. `PasteService` — Restores selected item to clipboard (preserving rich formats), then simulates Ctrl+V via `SendInput` into the previously-focused window.
6. `ClipboardStore` — All SQLite access. Semaphore-protected async operations, WAL mode, auto-prunes to 1000 items.

### Key Layers

- **Interop/** — All P/Invoke declarations (`NativeMethods`, `NativeConstants`, `NativeStructs`). Windows clipboard, hotkey, input simulation, and memory management APIs.
- **Data/** — SQLite schema init (`DatabaseInitializer`) and CRUD (`ClipboardStore`).
- **Services/** — Clipboard monitoring, hotkey registration, paste automation, search, tray icon.
- **ViewModels/** — `PopupViewModel` (main list + search logic), `ClipboardItemViewModel` (per-item display).
- **Models/** — `ClipboardItem` (data record), `ClipboardFormat` enum (Text=0, RichText=1, Html=2, Image=3, FileDrop=4).

### Important Details

- Only external NuGet dependency: `Microsoft.Data.Sqlite 8.0.11`
- Images are stored as PNG blobs (10MB limit) with 64x64 thumbnails
- File drops stored as JSON arrays of paths
- Database uses content_hash index for duplicate detection
- Popup is positioned near the cursor on the active monitor
- The app requires Windows (P/Invoke heavy) — not cross-platform
