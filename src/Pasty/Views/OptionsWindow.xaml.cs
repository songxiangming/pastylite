using System.Windows;
using System.Windows.Input;
using Pasty.Models;

using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace Pasty.Views;

public partial class OptionsWindow : Window
{
    private uint _hotkeyModifier;
    private uint _hotkeyKey;
    private bool _recording;

    public int ResultMaxEntries { get; private set; }
    public uint ResultHotkeyModifier { get; private set; }
    public uint ResultHotkeyKey { get; private set; }
    public bool Saved { get; private set; }

    public OptionsWindow(AppSettings settings)
    {
        InitializeComponent();

        MaxEntriesBox.Text = settings.MaxEntries.ToString();
        _hotkeyModifier = settings.HotkeyModifier;
        _hotkeyKey = settings.HotkeyKey;
        HotkeyBox.Text = settings.GetHotkeyDisplayText();
    }

    private void OnHotkeyGotFocus(object sender, RoutedEventArgs e)
    {
        _recording = true;
        HotkeyBox.Text = "Press a key combo...";
    }

    private void OnHotkeyLostFocus(object sender, RoutedEventArgs e)
    {
        _recording = false;
        var temp = new AppSettings { HotkeyModifier = _hotkeyModifier, HotkeyKey = _hotkeyKey };
        HotkeyBox.Text = temp.GetHotkeyDisplayText();
    }

    private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recording) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore modifier-only presses
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
            return;

        uint mod = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mod |= 0x0002;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mod |= 0x0004;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mod |= 0x0001;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) mod |= 0x0008;

        // Require at least one modifier
        if (mod == 0)
        {
            HotkeyBox.Text = "Need at least one modifier (Ctrl/Alt/Shift)";
            return;
        }

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        _hotkeyModifier = mod;
        _hotkeyKey = vk;

        var temp = new AppSettings { HotkeyModifier = _hotkeyModifier, HotkeyKey = _hotkeyKey };
        HotkeyBox.Text = temp.GetHotkeyDisplayText();
        _recording = false;

        // Move focus away
        SaveButton.Focus();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(MaxEntriesBox.Text, out int maxEntries) || maxEntries < 100 || maxEntries > 1000000)
        {
            MessageBox.Show("Max entries must be between 100 and 1000000.", "Pasty",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ResultMaxEntries = maxEntries;
        ResultHotkeyModifier = _hotkeyModifier;
        ResultHotkeyKey = _hotkeyKey;
        Saved = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
