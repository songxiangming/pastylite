namespace Pasty.Models;

public class ClipboardItem
{
    public long Id { get; set; }
    public ClipboardFormat Format { get; set; }
    public string? PlainText { get; set; }
    public byte[]? RichText { get; set; }
    public string? Html { get; set; }
    public byte[]? ImagePng { get; set; }
    public byte[]? ImageThumbnail { get; set; }
    public string? FilePaths { get; set; }
    public string ContentHash { get; set; } = "";
    public string? PreviewText { get; set; }
    public long ByteSize { get; set; }
    public bool IsFavorite { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastPastedAt { get; set; }
    public string? OcrText { get; set; }
}
