using System.IO;
using System.Windows.Media.Imaging;
using Pasty.Models;

namespace Pasty.ViewModels;

public class ClipboardItemViewModel
{
    private readonly ClipboardItem _item;
    private BitmapImage? _thumbnail;
    private BitmapImage? _fullImage;

    public ClipboardItemViewModel(ClipboardItem item)
    {
        _item = item;
    }

    public long Id => _item.Id;
    public ClipboardFormat Format => _item.Format;
    public bool IsFavorite => _item.IsFavorite;
    public DateTime CreatedAt => _item.CreatedAt;

    public bool IsTextItem => Format is ClipboardFormat.Text or ClipboardFormat.RichText
                              or ClipboardFormat.Html or ClipboardFormat.FileDrop;
    public bool IsImageItem => Format == ClipboardFormat.Image;

    public string FormatIcon => Format switch
    {
        ClipboardFormat.Text => "\U0001F4CB",      // clipboard
        ClipboardFormat.RichText => "\U0001F4DD",   // memo
        ClipboardFormat.Html => "\U0001F310",       // globe
        ClipboardFormat.Image => "\U0001F5BC",      // framed picture
        ClipboardFormat.FileDrop => "\U0001F4C1",   // file folder
        _ => "\U0001F4CB"
    };

    public string DisplayText
    {
        get
        {
            if (Format == ClipboardFormat.Image)
            {
                if (!string.IsNullOrEmpty(_item.OcrText))
                {
                    var preview = _item.OcrText.Length > 80
                        ? _item.OcrText[..80] + "..."
                        : _item.OcrText;
                    return $"(Image) {preview}";
                }
                return "(Image)";
            }

            if (Format == ClipboardFormat.FileDrop && _item.FilePaths != null)
            {
                try
                {
                    var files = System.Text.Json.JsonSerializer.Deserialize<string[]>(_item.FilePaths);
                    if (files != null && files.Length > 0)
                        return files.Length == 1 ? files[0] : $"{files[0]} (+{files.Length - 1} more)";
                }
                catch { }
            }

            return _item.PreviewText ?? "(empty)";
        }
    }

    public string SearchText => _item.PreviewText ?? _item.OcrText ?? _item.FilePaths ?? "";

    public void SetOcrText(string ocrText)
    {
        _item.OcrText = ocrText;
    }

    public string RelativeTime
    {
        get
        {
            var elapsed = DateTime.UtcNow - _item.CreatedAt;
            return elapsed.TotalSeconds < 60 ? "just now"
                : elapsed.TotalMinutes < 60 ? $"{(int)elapsed.TotalMinutes}m ago"
                : elapsed.TotalHours < 24 ? $"{(int)elapsed.TotalHours}h ago"
                : elapsed.TotalDays < 7 ? $"{(int)elapsed.TotalDays}d ago"
                : _item.CreatedAt.ToLocalTime().ToString("MMM d");
        }
    }

    public BitmapImage? Thumbnail
    {
        get
        {
            if (_thumbnail != null || _item.ImageThumbnail == null) return _thumbnail;
            try
            {
                _thumbnail = new BitmapImage();
                _thumbnail.BeginInit();
                _thumbnail.StreamSource = new MemoryStream(_item.ImageThumbnail);
                _thumbnail.CacheOption = BitmapCacheOption.OnLoad;
                _thumbnail.EndInit();
                _thumbnail.Freeze();
            }
            catch
            {
                _thumbnail = null;
            }
            return _thumbnail;
        }
    }

    public BitmapImage? FullImage
    {
        get
        {
            if (_fullImage != null || _item.ImagePng == null) return _fullImage;
            try
            {
                _fullImage = new BitmapImage();
                _fullImage.BeginInit();
                _fullImage.StreamSource = new MemoryStream(_item.ImagePng);
                _fullImage.CacheOption = BitmapCacheOption.OnLoad;
                _fullImage.EndInit();
                _fullImage.Freeze();
            }
            catch
            {
                _fullImage = null;
            }
            return _fullImage;
        }
    }
}
