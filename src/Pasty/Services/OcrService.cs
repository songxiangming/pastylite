using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Local;

namespace Pasty.Services;

public class OcrService : IDisposable
{
    private readonly PaddleOcrAll? _engine;
    private readonly object _lock = new();

    public OcrService()
    {
        try
        {
            FullOcrModel model = LocalFullModels.ChineseV3;
            _engine = new PaddleOcrAll(model, PaddleDevice.Mkldnn())
            {
                AllowRotateDetection = true,
                Enable180Classification = false
            };
        }
        catch
        {
            _engine = null;
        }
    }

    public bool IsAvailable => _engine != null;

    /// <summary>
    /// Extract text from PNG image bytes using PaddleOCR.
    /// Returns null if OCR is unavailable, image is invalid, or no text found.
    /// </summary>
    public string? ExtractText(byte[] pngBytes)
    {
        if (_engine == null) return null;

        try
        {
            using var mat = Cv2.ImDecode(pngBytes, ImreadModes.Color);
            if (mat.Empty()) return null;

            PaddleOcrResult result;
            lock (_lock)
            {
                result = _engine.Run(mat);
            }

            var text = result.Text?.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _engine?.Dispose();
    }
}
