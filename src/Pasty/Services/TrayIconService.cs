using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace Pasty.Services;

public class TrayIconService : IDisposable
{
    private NotifyIcon? _trayIcon;
    private readonly Action _showPopup;
    private readonly Func<Task> _clearHistory;
    private readonly Action _showOptions;

    public TrayIconService(Action showPopup, Func<Task> clearHistory, Action showOptions)
    {
        _showPopup = showPopup;
        _clearHistory = clearHistory;
        _showOptions = showOptions;
    }

    public void Initialize()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Text = "Pasty - Clipboard Manager (Ctrl+`)",
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };
        _trayIcon.DoubleClick += (_, _) => _showPopup();
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show (Ctrl+`)", null, (_, _) => _showPopup());
        menu.Items.Add("Clear History", null, async (_, _) =>
        {
            var result = System.Windows.MessageBox.Show(
                "Clear all clipboard history?", "Pasty",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
                await _clearHistory();
        });
        menu.Items.Add("Options...", null, (_, _) => _showOptions());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Application.Current.Shutdown());
        return menu;
    }

    private static Icon CreateDefaultIcon()
    {
        // Create a simple 16x16 icon with a clipboard symbol
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(137, 180, 250)); // Catppuccin blue
        g.FillRectangle(new SolidBrush(Color.FromArgb(30, 30, 46)), 3, 2, 10, 12);
        g.FillRectangle(new SolidBrush(Color.FromArgb(205, 214, 244)), 5, 5, 6, 1);
        g.FillRectangle(new SolidBrush(Color.FromArgb(205, 214, 244)), 5, 7, 6, 1);
        g.FillRectangle(new SolidBrush(Color.FromArgb(205, 214, 244)), 5, 9, 4, 1);
        var handle = bmp.GetHicon();
        return Icon.FromHandle(handle);
    }

    public void Dispose()
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }
}
