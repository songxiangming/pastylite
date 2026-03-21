using Pasty.Interop;

namespace Pasty.Services;

public class HotkeyManager : IDisposable
{
    public const int HOTKEY_ID = 0x0001;
    private IntPtr _hwnd;
    private bool _registered;

    public event Action? HotkeyPressed;

    public bool Register(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _registered = NativeMethods.RegisterHotKey(
            hwnd,
            HOTKEY_ID,
            NativeConstants.MOD_CONTROL,
            NativeConstants.VK_OEM_3);
        return _registered;
    }

    public void HandleHotkey(int id)
    {
        if (id == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke();
        }
    }

    public bool Reregister(IntPtr hwnd, uint modifier, uint key)
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_ID);
            _registered = false;
        }
        _hwnd = hwnd;
        _registered = NativeMethods.RegisterHotKey(hwnd, HOTKEY_ID, modifier, key);
        return _registered;
    }

    public void Dispose()
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_ID);
            _registered = false;
        }
    }
}
