using System.IO;
using System.Text.Json;

namespace Pasty.Models;

public class AppSettings
{
    public int MaxEntries { get; set; } = 1000;
    public uint HotkeyModifier { get; set; } = 0x0002; // MOD_CONTROL
    public uint HotkeyKey { get; set; } = 0xC0; // VK_OEM_3 (backtick)

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save(string path)
    {
        var json = JsonSerializer.Serialize(this, _jsonOptions);
        File.WriteAllText(path, json);
    }

    public string GetHotkeyDisplayText()
    {
        var parts = new List<string>();
        if ((HotkeyModifier & 0x0002) != 0) parts.Add("Ctrl");
        if ((HotkeyModifier & 0x0004) != 0) parts.Add("Shift");
        if ((HotkeyModifier & 0x0001) != 0) parts.Add("Alt");
        if ((HotkeyModifier & 0x0008) != 0) parts.Add("Win");
        parts.Add(KeyToString(HotkeyKey));
        return string.Join(" + ", parts);
    }

    public static string KeyToString(uint vk)
    {
        return vk switch
        {
            0xC0 => "`",
            >= 0x30 and <= 0x39 => ((char)vk).ToString(),        // 0-9
            >= 0x41 and <= 0x5A => ((char)vk).ToString(),        // A-Z
            >= 0x70 and <= 0x87 => $"F{vk - 0x70 + 1}",         // F1-F24
            0x20 => "Space",
            0x2D => "Insert",
            0x2E => "Delete",
            0x24 => "Home",
            0x23 => "End",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0xBD => "-",
            0xBB => "=",
            0xDB => "[",
            0xDD => "]",
            0xDC => "\\",
            0xBA => ";",
            0xDE => "'",
            0xBC => ",",
            0xBE => ".",
            0xBF => "/",
            _ => $"0x{vk:X2}"
        };
    }
}
