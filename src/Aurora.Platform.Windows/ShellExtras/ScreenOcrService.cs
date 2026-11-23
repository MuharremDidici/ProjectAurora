using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Aurora.Platform.Windows.ShellExtras;

public sealed record OcrExtractionResult(
    bool Success,
    string Text,
    string LanguageTag,
    int LineCount,
    string? ErrorMessage = null);

public sealed record PickedColorInfo(
    byte R,
    byte G,
    byte B,
    string Hex,
    string Rgb,
    string Hsl);

/// <summary>
/// Offline Windows 11 WinRT OCR engine wrapper + pixel color formatter +
/// clipboard privacy format guard (skips password managers like 1Password, Bitwarden, KeePass).
/// </summary>
public static class ScreenOcrService
{
    private static readonly uint CfExcludeFromMonitor = RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
    private static readonly uint CfCanIncludeInHistory = RegisterClipboardFormat("CanIncludeInClipboardHistory");

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormat(string lpszFormat);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(nint hWndNewOwner);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern nint GetClipboardData(uint uFormat);

    [DllImport("kernel32.dll")]
    private static extern nint GlobalLock(nint hMem);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(nint hMem);

    /// <summary>
    /// Respects standard Windows Clipboard Privacy markers used by password managers
    /// (Bitwarden, 1Password, KeePass, Edge Password Manager) so secrets are never stored.
    /// </summary>
    public static bool ShouldIgnoreCurrentClipboardForPrivacy()
    {
        try
        {
            if (CfExcludeFromMonitor != 0 && IsClipboardFormatAvailable(CfExcludeFromMonitor))
                return true;

            if (CfCanIncludeInHistory != 0 && IsClipboardFormatAvailable(CfCanIncludeInHistory))
            {
                if (OpenClipboard(0))
                {
                    try
                    {
                        nint h = GetClipboardData(CfCanIncludeInHistory);
                        if (h != 0)
                        {
                            nint ptr = GlobalLock(h);
                            if (ptr != 0)
                            {
                                try
                                {
                                    int allowed = Marshal.ReadInt32(ptr);
                                    if (allowed == 0)
                                        return true;
                                }
                                finally { GlobalUnlock(h); }
                            }
                        }
                    }
                    finally { CloseClipboard(); }
                }
            }
        }
        catch { }

        return false;
    }

    public static async Task<OcrExtractionResult> ExtractTextFromBitmapAsync(Bitmap bitmap)
    {
        if (bitmap is null || bitmap.Width < 4 || bitmap.Height < 4)
            return new OcrExtractionResult(false, string.Empty, string.Empty, 0, "Seçili alan çok küçük.");

        try
        {
            using var ms = new MemoryStream();
            // Upscale small captures 2x for significantly higher WinRT OCR accuracy on 9–11pt UI fonts
            if (bitmap.Width < 900 && bitmap.Height < 400)
            {
                using var scaled = new Bitmap(bitmap.Width * 2, bitmap.Height * 2, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(bitmap, 0, 0, scaled.Width, scaled.Height);
                }
                scaled.Save(ms, ImageFormat.Png);
            }
            else
            {
                bitmap.Save(ms, ImageFormat.Png);
            }

            return await ExtractTextFromPngBytesAsync(ms.ToArray());
        }
        catch (Exception ex)
        {
            return new OcrExtractionResult(false, string.Empty, string.Empty, 0, $"OCR hatası: {ex.Message}");
        }
    }

    public static async Task<OcrExtractionResult> ExtractTextFromPngBytesAsync(byte[] pngBytes)
    {
        if (pngBytes is null || pngBytes.Length == 0)
            return new OcrExtractionResult(false, string.Empty, string.Empty, 0, "Görsel verisi boş.");

        try
        {
            OcrEngine? engine = CreateBestOcrEngine();
            if (engine is null)
                return new OcrExtractionResult(false, string.Empty, string.Empty, 0, "Windows OCR dil paketi yüklü değil.");

            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(pngBytes.AsBuffer());
            stream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(stream);
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);

            var ocrResult = await engine.RecognizeAsync(softwareBitmap);
            var sb = new StringBuilder();
            foreach (var line in ocrResult.Lines)
            {
                string lineText = line.Text?.Trim() ?? string.Empty;
                if (lineText.Length > 0)
                    sb.AppendLine(lineText);
            }

            string final = sb.ToString().Trim();
            if (final.Length == 0)
                return new OcrExtractionResult(false, string.Empty, engine.RecognizerLanguage.LanguageTag, 0, "Seçilen bölgede okunabilir metin bulunamadı.");

            return new OcrExtractionResult(true, final, engine.RecognizerLanguage.LanguageTag, ocrResult.Lines.Count);
        }
        catch (Exception ex)
        {
            return new OcrExtractionResult(false, string.Empty, string.Empty, 0, $"OCR işlenemedi: {ex.Message}");
        }
    }

    private static OcrEngine? CreateBestOcrEngine()
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is not null)
            return engine;

        foreach (string tag in new[] { "tr-TR", "en-US", "en-GB" })
        {
            try
            {
                var lang = new Language(tag);
                if (OcrEngine.IsLanguageSupported(lang))
                {
                    var candidate = OcrEngine.TryCreateFromLanguage(lang);
                    if (candidate is not null)
                        return candidate;
                }
            }
            catch { }
        }

        var available = OcrEngine.AvailableRecognizerLanguages;
        if (available.Count > 0)
            return OcrEngine.TryCreateFromLanguage(available[0]);

        return null;
    }

    public static PickedColorInfo DescribeColor(Color c)
    {
        string hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        string rgb = $"rgb({c.R}, {c.G}, {c.B})";

        double r = c.R / 255.0;
        double g = c.G / 255.0;
        double b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2.0;
        double h = 0;
        double s = 0;

        if (Math.Abs(max - min) > 1e-6)
        {
            double d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            if (Math.Abs(max - r) < 1e-6) h = ((g - b) / d) + (g < b ? 6 : 0);
            else if (Math.Abs(max - g) < 1e-6) h = ((b - r) / d) + 2;
            else h = ((r - g) / d) + 4;
            h *= 60.0;
        }

        string hsl = $"hsl({Math.Round(h)}°, {Math.Round(s * 100)}%, {Math.Round(l * 100)}%)";
        return new PickedColorInfo(c.R, c.G, c.B, hex, rgb, hsl);
    }

    public static string? CreatePngThumbnailBase64(Bitmap source, int maxWidth = 160, int maxHeight = 96)
    {
        try
        {
            double scale = Math.Min(1.0, Math.Min((double)maxWidth / source.Width, (double)maxHeight / source.Height));
            int tw = Math.Max(1, (int)Math.Round(source.Width * scale));
            int th = Math.Max(1, (int)Math.Round(source.Height * scale));

            using var thumb = new Bitmap(tw, th, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(thumb))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(source, 0, 0, tw, th);
            }
            using var ms = new MemoryStream();
            thumb.Save(ms, ImageFormat.Png);
            return Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return null;
        }
    }
}
