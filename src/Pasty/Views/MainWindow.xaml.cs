using System.Windows;
using System.Windows.Interop;
using Pasty.Interop;
using Pasty.Services;

namespace Pasty.Views;

public partial class MainWindow : Window
{
    private readonly ClipboardMonitor _clipboardMonitor;
    private readonly HotkeyManager _hotkeyManager;
    private HwndSource? _hwndSource;

    public MainWindow(ClipboardMonitor clipboardMonitor, HotkeyManager hotkeyManager)
    {
        InitializeComponent();
        _clipboardMonitor = clipboardMonitor;
        _hotkeyManager = hotkeyManager;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var helper = new WindowInteropHelper(this);
        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        _hwndSource?.AddHook(WndProc);

        _clipboardMonitor.Start(helper.Handle);
        _hotkeyManager.Register(helper.Handle);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case NativeConstants.WM_CLIPBOARDUPDATE:
                _clipboardMonitor.HandleClipboardUpdate();
                handled = true;
                break;

            case NativeConstants.WM_HOTKEY:
                _hotkeyManager.HandleHotkey(wParam.ToInt32());
                handled = true;
                break;
        }
        return IntPtr.Zero;
    }

    protected override void OnClosed(EventArgs e)
    {
        _hwndSource?.RemoveHook(WndProc);
        base.OnClosed(e);
    }
}
