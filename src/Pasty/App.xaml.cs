using System.IO;
using System.Windows;
using System.Windows.Interop;
using Pasty.Data;
using Pasty.Models;
using Pasty.Services;
using Pasty.ViewModels;
using Pasty.Views;

using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Pasty;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private ClipboardMonitor? _clipboardMonitor;
    private ClipboardStore? _clipboardStore;
    private HotkeyManager? _hotkeyManager;
    private PasteService? _pasteService;
    private TrayIconService? _trayService;
    private PopupWindow? _popupWindow;
    private PopupViewModel? _popupViewModel;
    private AppSettings? _settings;
    private string? _settingsPath;
    private IntPtr _mainHwnd;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Single-instance enforcement
        _singleInstanceMutex = new Mutex(true, "PastyClipboardManager_v1", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("Pasty is already running.", "Pasty",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Determine data directory: use LocalAppData for dotnet-run, exe directory for published
        var processName = Path.GetFileName(Environment.ProcessPath) ?? "";
        string dataDir;
        if (processName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase) ||
            processName.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pasty");
            Directory.CreateDirectory(dataDir);
        }
        else
        {
            dataDir = AppContext.BaseDirectory;
        }

        // Load settings
        _settingsPath = Path.Combine(dataDir, "settings.json");
        _settings = AppSettings.Load(_settingsPath);

        // Database path
        var dbPath = Path.Combine(dataDir, "pasty.db");

        // Initialize database
        var dbInit = new DatabaseInitializer(dbPath);
        await dbInit.InitializeAsync();

        _clipboardStore = new ClipboardStore(dbPath);
        await _clipboardStore.PruneAsync(_settings.MaxEntries);

        // Initialize services
        _clipboardMonitor = new ClipboardMonitor();
        _hotkeyManager = new HotkeyManager();
        _pasteService = new PasteService(_clipboardMonitor, _clipboardStore);

        // Create popup
        _popupViewModel = new PopupViewModel(_clipboardStore, _pasteService);
        _popupWindow = new PopupWindow();
        _popupWindow.SetViewModel(_popupViewModel);

        // Wire up clipboard capture
        _clipboardMonitor.ClipboardChanged += async item =>
        {
            var id = await _clipboardStore.InsertOrBumpAsync(item);
            // Update in-memory list in popup
            item.Id = id;
            Dispatcher.Invoke(() =>
            {
                _popupViewModel?.AddNewItem(item);
            });
        };

        // Wire up hotkey
        _hotkeyManager.HotkeyPressed += TogglePopup;

        // Create hidden main window (needed for HWND to receive messages)
        var mainWindow = new MainWindow(_clipboardMonitor, _hotkeyManager);
        mainWindow.Show();
        _mainHwnd = new WindowInteropHelper(mainWindow).Handle;

        // Start tray icon
        _trayService = new TrayIconService(
            showPopup: () => ShowPopup(),
            clearHistory: async () =>
            {
                await _clipboardStore.ClearAllAsync();
                Dispatcher.Invoke(() => _popupViewModel?.LoadItemsAsync());
            },
            showOptions: () => ShowOptions());
        _trayService.Initialize();
    }

    private void TogglePopup()
    {
        if (_popupWindow == null) return;

        if (_popupWindow.IsVisible)
        {
            _popupWindow.Hide();
        }
        else
        {
            ShowPopup();
        }
    }

    private void ShowPopup()
    {
        if (_popupWindow == null || _pasteService == null) return;

        _pasteService.CaptureTargetWindow();
        _ = _popupWindow.ShowAndLoadAsync();
    }

    private async void ShowOptions()
    {
        if (_settings == null || _settingsPath == null) return;

        var optionsWindow = new OptionsWindow(_settings);
        optionsWindow.ShowDialog();

        if (!optionsWindow.Saved) return;

        _settings.MaxEntries = optionsWindow.ResultMaxEntries;
        _settings.HotkeyModifier = optionsWindow.ResultHotkeyModifier;
        _settings.HotkeyKey = optionsWindow.ResultHotkeyKey;
        _settings.Save(_settingsPath);

        // Apply new hotkey
        if (_hotkeyManager != null && _mainHwnd != IntPtr.Zero)
        {
            _hotkeyManager.Reregister(_mainHwnd, _settings.HotkeyModifier, _settings.HotkeyKey);
        }

        // Apply new max entries
        if (_clipboardStore != null)
        {
            await _clipboardStore.PruneAsync(_settings.MaxEntries);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyManager?.Dispose();
        _clipboardMonitor?.Dispose();
        _trayService?.Dispose();
        _clipboardStore?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
