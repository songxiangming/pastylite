namespace Pasty.Interop;

internal static class NativeConstants
{
    // Window messages
    public const int WM_CLIPBOARDUPDATE = 0x031D;
    public const int WM_HOTKEY = 0x0312;

    // Clipboard formats
    public const uint CF_TEXT = 1;
    public const uint CF_UNICODETEXT = 13;
    public const uint CF_DIB = 8;
    public const uint CF_DIBV5 = 17;
    public const uint CF_HDROP = 15;

    // Hotkey modifiers
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_ALT = 0x0001;

    // Virtual key codes
    public const uint VK_OEM_3 = 0xC0; // backtick/tilde key
    public const uint VK_CONTROL = 0x11;
    public const uint VK_SHIFT = 0x10;
    public const uint VK_V = 0x56;

    // Global memory flags
    public const uint GMEM_MOVEABLE = 0x0002;
    public const uint GMEM_ZEROINIT = 0x0040;
    public const uint GHND = GMEM_MOVEABLE | GMEM_ZEROINIT;

    // SendInput
    public const int INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
}
