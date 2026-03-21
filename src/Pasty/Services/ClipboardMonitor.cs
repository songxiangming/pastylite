using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pasty.Interop;
using Pasty.Models;

namespace Pasty.Services;

public class ClipboardMonitor : IDisposable
{
    private IntPtr _hwnd;
    private bool _isListening;
    private volatile bool _isSelfSetting;

    private static uint _cfHtml;
    private static uint _cfRtf;

    public event Action<ClipboardItem>? ClipboardChanged;

    public void Start(IntPtr hwnd)
    {
        _hwnd = hwnd;

        // Register custom clipboard format IDs
        _cfHtml = NativeMethods.RegisterClipboardFormat("HTML Format");
        _cfRtf = NativeMethods.RegisterClipboardFormat("Rich Text Format");

        if (NativeMethods.AddClipboardFormatListener(hwnd))
        {
            _isListening = true;
        }
    }

    public void Stop()
    {
        if (_isListening && _hwnd != IntPtr.Zero)
        {
            NativeMethods.RemoveClipboardFormatListener(_hwnd);
            _isListening = false;
        }
    }

    public void SetSelfSettingFlag(bool value) => _isSelfSetting = value;

    public void HandleClipboardUpdate()
    {
        if (_isSelfSetting) return;

        try
        {
            var item = ReadClipboard();
            if (item != null)
            {
                ClipboardChanged?.Invoke(item);
            }
        }
        catch
        {
            // Clipboard may be locked by another app; silently ignore
        }
    }

    private ClipboardItem? ReadClipboard()
    {
        // Retry up to 3 times if clipboard is locked
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (NativeMethods.OpenClipboard(_hwnd))
            {
                try
                {
                    return ReadClipboardFormats();
                }
                finally
                {
                    NativeMethods.CloseClipboard();
                }
            }
            Thread.Sleep(50);
        }
        return null;
    }

    private ClipboardItem? ReadClipboardFormats()
    {
        string? plainText = null;
        string? html = null;
        byte[]? rtfBytes = null;
        byte[]? imagePng = null;
        byte[]? imageThumbnail = null;
        string? filePaths = null;
        ClipboardFormat format = ClipboardFormat.Text;

        // Read plain text
        if (NativeMethods.IsClipboardFormatAvailable(NativeConstants.CF_UNICODETEXT))
        {
            var hData = NativeMethods.GetClipboardData(NativeConstants.CF_UNICODETEXT);
            if (hData != IntPtr.Zero)
            {
                var ptr = NativeMethods.GlobalLock(hData);
                if (ptr != IntPtr.Zero)
                {
                    try
                    {
                        plainText = Marshal.PtrToStringUni(ptr);
                    }
                    finally
                    {
                        NativeMethods.GlobalUnlock(hData);
                    }
                }
            }
        }

        // Read HTML
        if (_cfHtml != 0 && NativeMethods.IsClipboardFormatAvailable(_cfHtml))
        {
            var hData = NativeMethods.GetClipboardData(_cfHtml);
            if (hData != IntPtr.Zero)
            {
                var ptr = NativeMethods.GlobalLock(hData);
                if (ptr != IntPtr.Zero)
                {
                    try
                    {
                        var size = (int)NativeMethods.GlobalSize(hData);
                        var bytes = new byte[size];
                        Marshal.Copy(ptr, bytes, 0, size);
                        html = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                        format = ClipboardFormat.Html;
                    }
                    finally
                    {
                        NativeMethods.GlobalUnlock(hData);
                    }
                }
            }
        }

        // Read RTF
        if (_cfRtf != 0 && NativeMethods.IsClipboardFormatAvailable(_cfRtf))
        {
            var hData = NativeMethods.GetClipboardData(_cfRtf);
            if (hData != IntPtr.Zero)
            {
                var ptr = NativeMethods.GlobalLock(hData);
                if (ptr != IntPtr.Zero)
                {
                    try
                    {
                        var size = (int)NativeMethods.GlobalSize(hData);
                        rtfBytes = new byte[size];
                        Marshal.Copy(ptr, rtfBytes, 0, size);
                        if (format == ClipboardFormat.Text)
                            format = ClipboardFormat.RichText;
                    }
                    finally
                    {
                        NativeMethods.GlobalUnlock(hData);
                    }
                }
            }
        }

        // Read image (CF_DIB / CF_DIBV5) using native API
        uint dibFormat = 0;
        if (NativeMethods.IsClipboardFormatAvailable(NativeConstants.CF_DIBV5))
            dibFormat = NativeConstants.CF_DIBV5;
        else if (NativeMethods.IsClipboardFormatAvailable(NativeConstants.CF_DIB))
            dibFormat = NativeConstants.CF_DIB;

        if (dibFormat != 0)
        {
            // Only treat as image if there's no text content
            if (string.IsNullOrEmpty(plainText))
            {
                try
                {
                    var bitmapSource = ReadDibFromClipboard(dibFormat);
                    if (bitmapSource != null)
                    {
                        imagePng = EncodeToPng(bitmapSource);
                        if (imagePng != null && imagePng.Length <= 10 * 1024 * 1024) // 10MB limit
                        {
                            imageThumbnail = CreateThumbnail(bitmapSource, 192);
                            format = ClipboardFormat.Image;
                        }
                        else
                        {
                            imagePng = null; // Too large, skip
                        }
                    }
                }
                catch
                {
                    // Image reading failed
                }
            }
        }

        // Read file drop
        if (NativeMethods.IsClipboardFormatAvailable(NativeConstants.CF_HDROP))
        {
            var hData = NativeMethods.GetClipboardData(NativeConstants.CF_HDROP);
            if (hData != IntPtr.Zero)
            {
                var fileCount = NativeMethods.DragQueryFile(hData, 0xFFFFFFFF, null, 0);
                var files = new List<string>();
                for (uint i = 0; i < fileCount; i++)
                {
                    var sb = new StringBuilder(260);
                    NativeMethods.DragQueryFile(hData, i, sb, 260);
                    files.Add(sb.ToString());
                }
                if (files.Count > 0)
                {
                    filePaths = System.Text.Json.JsonSerializer.Serialize(files);
                    plainText ??= string.Join("\n", files);
                    format = ClipboardFormat.FileDrop;
                }
            }
        }

        // Nothing captured
        if (plainText == null && imagePng == null && filePaths == null)
            return null;

        // Compute content hash
        var hashSource = format == ClipboardFormat.Image && imagePng != null
            ? imagePng
            : Encoding.UTF8.GetBytes(plainText ?? "");
        var contentHash = Convert.ToHexString(SHA256.HashData(hashSource)).ToLowerInvariant();

        // Build preview text
        string? previewText = null;
        if (plainText != null)
        {
            var normalized = System.Text.RegularExpressions.Regex.Replace(plainText, @"\s+", " ").Trim();
            previewText = normalized.Length > 200 ? normalized[..200] : normalized;
        }

        long byteSize = imagePng?.Length ?? Encoding.UTF8.GetByteCount(plainText ?? "");

        return new ClipboardItem
        {
            Format = format,
            PlainText = plainText,
            RichText = rtfBytes,
            Html = html,
            ImagePng = imagePng,
            ImageThumbnail = imageThumbnail,
            FilePaths = filePaths,
            ContentHash = contentHash,
            PreviewText = previewText,
            ByteSize = byteSize,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static byte[]? EncodeToPng(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    private static byte[]? CreateThumbnail(BitmapSource source, int maxDim)
    {
        try
        {
            double scale = Math.Min((double)maxDim / source.PixelWidth, (double)maxDim / source.PixelHeight);
            if (scale >= 1.0) scale = 1.0;

            var transformed = new TransformedBitmap(source,
                new System.Windows.Media.ScaleTransform(scale, scale));

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(transformed));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource? ReadDibFromClipboard(uint dibFormat)
    {
        var hData = NativeMethods.GetClipboardData(dibFormat);
        if (hData == IntPtr.Zero) return null;

        var ptr = NativeMethods.GlobalLock(hData);
        if (ptr == IntPtr.Zero) return null;

        try
        {
            var size = (int)NativeMethods.GlobalSize(hData);
            if (size < 40) return null; // BITMAPINFOHEADER is 40 bytes minimum

            var dibBytes = new byte[size];
            Marshal.Copy(ptr, dibBytes, 0, size);

            // Parse BITMAPINFOHEADER
            int headerSize = BitConverter.ToInt32(dibBytes, 0);
            int width = BitConverter.ToInt32(dibBytes, 4);
            int height = BitConverter.ToInt32(dibBytes, 8);
            short bitCount = BitConverter.ToInt16(dibBytes, 14);
            int compression = BitConverter.ToInt32(dibBytes, 16);
            int colorsUsed = BitConverter.ToInt32(dibBytes, 32);

            // Determine if image is bottom-up (positive height = bottom-up)
            bool bottomUp = height > 0;
            int absHeight = Math.Abs(height);

            // Determine pixel format
            PixelFormat pixelFormat;
            switch (bitCount)
            {
                case 32:
                    pixelFormat = PixelFormats.Bgra32;
                    break;
                case 24:
                    pixelFormat = PixelFormats.Bgr24;
                    break;
                case 16:
                    pixelFormat = PixelFormats.Bgr555;
                    break;
                default:
                    return null; // Unsupported bit depth
            }

            // Calculate color table size (only for <= 8bpp, but we handle 16/24/32)
            int colorTableSize = 0;
            if (bitCount <= 8)
            {
                int colors = colorsUsed > 0 ? colorsUsed : (1 << bitCount);
                colorTableSize = colors * 4; // RGBQUAD is 4 bytes
            }
            // For BI_BITFIELDS compression (value 3), there are 3 DWORD color masks
            if (compression == 3)
            {
                colorTableSize = 12; // 3 x DWORD
            }

            int pixelDataOffset = headerSize + colorTableSize;
            int stride = ((width * bitCount + 31) / 32) * 4;
            int pixelDataSize = stride * absHeight;

            if (pixelDataOffset + pixelDataSize > size)
                return null; // Data too small

            var pixelData = new byte[pixelDataSize];
            Array.Copy(dibBytes, pixelDataOffset, pixelData, 0, pixelDataSize);

            // If bottom-up, flip the rows
            if (bottomUp)
            {
                var flipped = new byte[pixelDataSize];
                for (int y = 0; y < absHeight; y++)
                {
                    Array.Copy(pixelData, (absHeight - 1 - y) * stride, flipped, y * stride, stride);
                }
                pixelData = flipped;
            }

            var bitmap = BitmapSource.Create(width, absHeight, 96, 96, pixelFormat, null, pixelData, stride);
            bitmap.Freeze();
            return bitmap;
        }
        finally
        {
            NativeMethods.GlobalUnlock(hData);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
