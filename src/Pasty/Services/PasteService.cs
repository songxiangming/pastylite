using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Pasty.Data;
using Pasty.Interop;
using Pasty.Models;

using Clipboard = System.Windows.Clipboard;
using DataFormats = System.Windows.DataFormats;
using DataObject = System.Windows.DataObject;
using TextDataFormat = System.Windows.TextDataFormat;

namespace Pasty.Services;

public class PasteService
{
    private readonly ClipboardMonitor _monitor;
    private readonly ClipboardStore _store;
    private IntPtr _targetWindow;

    public PasteService(ClipboardMonitor monitor, ClipboardStore store)
    {
        _monitor = monitor;
        _store = store;
    }

    public void CaptureTargetWindow()
    {
        _targetWindow = NativeMethods.GetForegroundWindow();
    }

    public async Task PasteItemAsync(long itemId, bool plainTextOnly)
    {
        var item = await _store.GetByIdAsync(itemId);
        if (item == null) return;

        _monitor.SetSelfSettingFlag(true);
        try
        {
            // Set clipboard data
            var dataObj = new DataObject();

            if (plainTextOnly)
            {
                if (item.PlainText != null)
                    dataObj.SetText(item.PlainText, TextDataFormat.UnicodeText);
                else
                    return; // Nothing to paste as plain text
            }
            else
            {
                // Restore all available formats
                if (item.PlainText != null)
                    dataObj.SetText(item.PlainText, TextDataFormat.UnicodeText);

                if (item.Html != null)
                    dataObj.SetText(item.Html, TextDataFormat.Html);

                if (item.RichText != null)
                {
                    using var ms = new MemoryStream(item.RichText);
                    dataObj.SetData(DataFormats.Rtf, ms);
                }

                if (item.ImagePng != null)
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = new MemoryStream(item.ImagePng);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    dataObj.SetImage(bmp);
                }

                if (item.FilePaths != null)
                {
                    var files = System.Text.Json.JsonSerializer.Deserialize<string[]>(item.FilePaths);
                    if (files != null)
                    {
                        var collection = new System.Collections.Specialized.StringCollection();
                        collection.AddRange(files);
                        dataObj.SetFileDropList(collection);
                    }
                }
            }

            Clipboard.SetDataObject(dataObj, true);

            // Restore focus to target window and paste
            await Task.Delay(50);

            if (_targetWindow != IntPtr.Zero)
                NativeMethods.SetForegroundWindow(_targetWindow);

            await Task.Delay(50);

            SimulateCtrlV();

            // Update last pasted time
            await _store.UpdateLastPastedAsync(itemId);
        }
        finally
        {
            // Delay a bit before clearing the flag to ensure clipboard notification is processed
            await Task.Delay(100);
            _monitor.SetSelfSettingFlag(false);
        }
    }

    private static void SimulateCtrlV()
    {
        var inputs = new INPUT[4];

        // Ctrl down
        inputs[0].Type = NativeConstants.INPUT_KEYBOARD;
        inputs[0].Union.Keyboard.VirtualKey = (ushort)NativeConstants.VK_CONTROL;

        // V down
        inputs[1].Type = NativeConstants.INPUT_KEYBOARD;
        inputs[1].Union.Keyboard.VirtualKey = (ushort)NativeConstants.VK_V;

        // V up
        inputs[2].Type = NativeConstants.INPUT_KEYBOARD;
        inputs[2].Union.Keyboard.VirtualKey = (ushort)NativeConstants.VK_V;
        inputs[2].Union.Keyboard.Flags = NativeConstants.KEYEVENTF_KEYUP;

        // Ctrl up
        inputs[3].Type = NativeConstants.INPUT_KEYBOARD;
        inputs[3].Union.Keyboard.VirtualKey = (ushort)NativeConstants.VK_CONTROL;
        inputs[3].Union.Keyboard.Flags = NativeConstants.KEYEVENTF_KEYUP;

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }
}
