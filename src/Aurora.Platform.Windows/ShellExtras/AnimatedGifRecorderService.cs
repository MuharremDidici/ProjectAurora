using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Aurora.Platform.Windows.ShellExtras;

public sealed record GifRecordingOutcome(
    bool Success,
    string FilePath,
    int FrameCount,
    double DurationSeconds,
    long SizeBytes,
    string? ErrorMessage = null);

/// <summary>
/// Zero-dependency, lightweight screen region Animated GIF89a recorder and pure-C# LZW encoder.
/// Captures at 10 FPS (up to 30 seconds max) with a fast perceptual 256-color palette (216 RGB cube + 40 gray ramp)
/// so recording and final encoding complete rapidly with minimal RAM &amp; CPU overhead.
/// </summary>
public sealed class AnimatedGifRecorderService : IDisposable
{
    private const int TargetFps = 10;
    private const int FrameDelayCentisecs = 10; // 100ms = 10 FPS
    private const int MaxDurationSeconds = 30;
    private const int MaxWidthPx = 920;

    private readonly object _sync = new();
    private readonly List<byte[]> _frames = [];
    private CancellationTokenSource? _cts;
    private Task? _captureTask;
    private Rectangle _captureRect;
    private int _outW;
    private int _outH;
    private DateTimeOffset _startedAtUtc;
    private bool _isRecording;

    public bool IsRecording
    {
        get { lock (_sync) return _isRecording; }
    }

    public int ElapsedSeconds
    {
        get
        {
            lock (_sync)
                return _isRecording ? Math.Max(0, (int)(DateTimeOffset.UtcNow - _startedAtUtc).TotalSeconds) : 0;
        }
    }

    public event Action<int>? ElapsedChanged;
    public event Action<GifRecordingOutcome>? RecordingFinished;

    public bool StartRecording(Rectangle screenRegion)
    {
        if (screenRegion.Width < 16 || screenRegion.Height < 16)
            return false;

        lock (_sync)
        {
            if (_isRecording) return false;
            _frames.Clear();
            _captureRect = screenRegion;

            double scale = screenRegion.Width > MaxWidthPx ? (MaxWidthPx / (double)screenRegion.Width) : 1.0;
            _outW = Math.Max(16, (int)Math.Round(screenRegion.Width * scale));
            _outH = Math.Max(16, (int)Math.Round(screenRegion.Height * scale));

            _startedAtUtc = DateTimeOffset.UtcNow;
            _isRecording = true;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            _captureTask = Task.Run(() => CaptureLoopAsync(ct));
            return true;
        }
    }

    public void RequestStop()
    {
        CancellationTokenSource? cts;
        lock (_sync)
        {
            if (!_isRecording) return;
            cts = _cts;
        }
        try { cts?.Cancel(); } catch { }
    }

    private async Task CaptureLoopAsync(CancellationToken ct)
    {
        int lastSec = -1;
        int maxFrames = TargetFps * MaxDurationSeconds;

        try
        {
            using var rawBmp = new Bitmap(_captureRect.Width, _captureRect.Height, PixelFormat.Format32bppArgb);
            using var scaledBmp = (_outW != _captureRect.Width || _outH != _captureRect.Height)
                ? new Bitmap(_outW, _outH, PixelFormat.Format32bppArgb)
                : null;

            while (!ct.IsCancellationRequested)
            {
                var tickStart = DateTimeOffset.UtcNow;
                int elapsed = (int)(tickStart - _startedAtUtc).TotalSeconds;
                if (elapsed != lastSec)
                {
                    lastSec = elapsed;
                    ElapsedChanged?.Invoke(elapsed);
                }

                if (elapsed >= MaxDurationSeconds)
                    break;

                using (var g = Graphics.FromImage(rawBmp))
                {
                    g.CopyFromScreen(_captureRect.Left, _captureRect.Top, 0, 0, _captureRect.Size, CopyPixelOperation.SourceCopy);
                }

                Bitmap sourceForQuant = rawBmp;
                if (scaledBmp is not null)
                {
                    using var sg = Graphics.FromImage(scaledBmp);
                    sg.InterpolationMode = InterpolationMode.Bilinear;
                    sg.DrawImage(rawBmp, 0, 0, _outW, _outH);
                    sourceForQuant = scaledBmp;
                }

                byte[] indexed = QuantizeBitmapToPaletteIndices(sourceForQuant);
                int count;
                lock (_sync)
                {
                    _frames.Add(indexed);
                    count = _frames.Count;
                }
                if (count >= maxFrames)
                    break;

                int spentMs = (int)(DateTimeOffset.UtcNow - tickStart).TotalMilliseconds;
                int waitMs = Math.Max(15, (1000 / TargetFps) - spentMs);
                try { await Task.Delay(waitMs, ct); } catch (OperationCanceledException) { break; }
            }
        }
        catch
        {
            // Continue to encode whatever frames were captured cleanly
        }

        List<byte[]> capturedFrames;
        int width, height;
        double duration;
        lock (_sync)
        {
            _isRecording = false;
            capturedFrames = [.. _frames];
            _frames.Clear();
            width = _outW;
            height = _outH;
            duration = Math.Max(0.1, (DateTimeOffset.UtcNow - _startedAtUtc).TotalSeconds);
            _cts?.Dispose();
            _cts = null;
        }

        GifRecordingOutcome outcome;
        if (capturedFrames.Count == 0)
        {
            outcome = new GifRecordingOutcome(false, string.Empty, 0, 0, 0, "Hiç kare yakalanamadı.");
        }
        else
        {
            try
            {
                string picsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ProjectAurora");
                Directory.CreateDirectory(picsDir);
                string filePath = Path.Combine(picsDir, $"Aurora-Kayit-{DateTime.Now:yyyyMMdd-HHmmss}.gif");
                EncodeGif89a(filePath, width, height, capturedFrames, FrameDelayCentisecs);
                long bytes = new FileInfo(filePath).Length;
                outcome = new GifRecordingOutcome(true, filePath, capturedFrames.Count, Math.Round(duration, 1), bytes);
            }
            catch (Exception ex)
            {
                outcome = new GifRecordingOutcome(false, string.Empty, capturedFrames.Count, duration, 0, ex.Message);
            }
        }

        RecordingFinished?.Invoke(outcome);
    }

    /// <summary>
    /// Fast perceptual 256-color Global Color Table:
    /// Indices 0..215 = 6×6×6 uniform RGB color cube (step 51)
    /// Indices 216..255 = 40 fine grayscale ramp steps (keeps dark UI/text razor-clean!)
    /// </summary>
    public static byte[] BuildGlobalPaletteRgb()
    {
        byte[] pal = new byte[256 * 3];
        int p = 0;
        for (int r = 0; r < 6; r++)
        {
            for (int g = 0; g < 6; g++)
            {
                for (int b = 0; b < 6; b++)
                {
                    pal[p++] = (byte)(r * 51);
                    pal[p++] = (byte)(g * 51);
                    pal[p++] = (byte)(b * 51);
                }
            }
        }

        for (int i = 0; i < 40; i++)
        {
            byte gray = (byte)Math.Clamp((int)Math.Round((i + 1) * (255.0 / 41.0)), 0, 255);
            pal[p++] = gray;
            pal[p++] = gray;
            pal[p++] = gray;
        }

        return pal;
    }

    public static byte MapRgbToPaletteIndex(byte r, byte g, byte b)
    {
        // Use the 40-step fine grayscale ramp when pixel is near-neutral (crisp dark/light desktop text & glass!)
        int max = Math.Max(r, Math.Max(g, b));
        int min = Math.Min(r, Math.Min(g, b));
        if ((max - min) <= 10)
        {
            int lum = (r * 30 + g * 59 + b * 11) / 100;
            int gIdx = (lum * 39) / 255;
            return (byte)(216 + Math.Clamp(gIdx, 0, 39));
        }

        int ri = (r * 5 + 127) / 255;
        int gi = (g * 5 + 127) / 255;
        int bi = (b * 5 + 127) / 255;
        return (byte)((ri * 36) + (gi * 6) + bi);
    }

    public static byte[] QuantizeBitmapToPaletteIndices(Bitmap bmp)
    {
        int w = bmp.Width;
        int h = bmp.Height;
        byte[] indices = new byte[w * h];
        var bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = bd.Stride;
            byte[] raw = new byte[Math.Abs(stride) * h];
            Marshal.Copy(bd.Scan0, raw, 0, raw.Length);

            int dst = 0;
            for (int y = 0; y < h; y++)
            {
                int row = y * stride;
                for (int x = 0; x < w; x++)
                {
                    int px = row + (x * 4);
                    byte b = raw[px];
                    byte g = raw[px + 1];
                    byte r = raw[px + 2];
                    indices[dst++] = MapRgbToPaletteIndex(r, g, b);
                }
            }
        }
        finally
        {
            bmp.UnlockBits(bd);
        }

        return indices;
    }

    public static void EncodeGif89a(string outputPath, int width, int height, IReadOnlyList<byte[]> indexedFrames, int delayCentiseconds = 10)
    {
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
        EncodeGif89a(fs, width, height, indexedFrames, delayCentiseconds);
    }

    public static void EncodeGif89a(Stream stream, int width, int height, IReadOnlyList<byte[]> indexedFrames, int delayCentiseconds = 10)
    {
        using var bw = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);

        // 1. Header "GIF89a"
        bw.Write("GIF89a"u8);

        // 2. Logical Screen Descriptor (Global Color Table = 256 entries, 8-bit)
        bw.Write((ushort)width);
        bw.Write((ushort)height);
        bw.Write((byte)0xF7); // GCT flag=1, color res=7, sort=0, size=7 (2^(7+1)=256)
        bw.Write((byte)0);    // Background color index
        bw.Write((byte)0);    // Pixel aspect ratio

        // 3. Global Color Table (768 bytes)
        bw.Write(BuildGlobalPaletteRgb());

        // 4. NETSCAPE2.0 Application Extension (infinite loop)
        bw.Write((byte)0x21);
        bw.Write((byte)0xFF);
        bw.Write((byte)0x0B);
        bw.Write("NETSCAPE2.0"u8);
        bw.Write((byte)0x03);
        bw.Write((byte)0x01);
        bw.Write((ushort)0); // 0 = loop forever
        bw.Write((byte)0x00);

        ushort delay = (ushort)Math.Clamp(delayCentiseconds, 2, 600);

        // 5. Frames
        foreach (var indices in indexedFrames)
        {
            // Graphic Control Extension
            bw.Write((byte)0x21);
            bw.Write((byte)0xF9);
            bw.Write((byte)0x04);
            bw.Write((byte)0x04); // Disposal method: do not dispose (no transparency needed for opaque screen captures)
            bw.Write(delay);
            bw.Write((byte)0x00);
            bw.Write((byte)0x00);

            // Image Descriptor
            bw.Write((byte)0x2C);
            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write((ushort)width);
            bw.Write((ushort)height);
            bw.Write((byte)0x00); // No local color table

            // LZW Minimum Code Size = 8
            bw.Write((byte)8);
            WriteLzwSubBlocks(bw, indices);
            bw.Write((byte)0x00); // Block terminator
        }

        // 6. Trailer
        bw.Write((byte)0x3B);
        bw.Flush();
    }

    private static void WriteLzwSubBlocks(BinaryWriter bw, byte[] indices)
    {
        const int clearCode = 256;
        const int eoiCode = 257;
        const int maxTableSize = 4096;

        // Fast open-addressed hash table for (prefix << 8) | suffix -> code
        const int hashCap = 8192; // power of 2
        const int hashMask = hashCap - 1;
        int[] keys = new int[hashCap];
        short[] vals = new short[hashCap];
        Array.Fill(keys, -1);

        byte[] subBlock = new byte[255];
        int subLen = 0;
        int bitAccum = 0;
        int bitCount = 0;

        void EmitCode(int code, int codeSize)
        {
            bitAccum |= (code << bitCount);
            bitCount += codeSize;
            while (bitCount >= 8)
            {
                subBlock[subLen++] = (byte)(bitAccum & 0xFF);
                bitAccum >>= 8;
                bitCount -= 8;
                if (subLen == 255)
                {
                    bw.Write((byte)255);
                    bw.Write(subBlock, 0, 255);
                    subLen = 0;
                }
            }
        }

        int codeSize = 9;
        int nextCode = 258;
        EmitCode(clearCode, codeSize);

        if (indices.Length > 0)
        {
            int prefix = indices[0];
            for (int i = 1; i < indices.Length; i++)
            {
                int k = indices[i];
                int key = (prefix << 8) | k;
                int slot = ((key * 2654435761u) >> 19) is var h ? (int)(h & hashMask) : 0;

                int found = -1;
                while (keys[slot] != -1)
                {
                    if (keys[slot] == key)
                    {
                        found = vals[slot];
                        break;
                    }
                    slot = (slot + 1) & hashMask;
                }

                if (found >= 0)
                {
                    prefix = found;
                }
                else
                {
                    EmitCode(prefix, codeSize);
                    if (nextCode < maxTableSize)
                    {
                        keys[slot] = key;
                        vals[slot] = (short)nextCode;
                        if (nextCode == (1 << codeSize) && codeSize < 12)
                            codeSize++;
                        nextCode++;
                    }
                    else
                    {
                        EmitCode(clearCode, codeSize);
                        Array.Fill(keys, -1);
                        codeSize = 9;
                        nextCode = 258;
                    }
                    prefix = k;
                }
            }

            EmitCode(prefix, codeSize);
        }

        EmitCode(eoiCode, codeSize);
        if (bitCount > 0)
            subBlock[subLen++] = (byte)(bitAccum & 0xFF);

        if (subLen > 0)
        {
            bw.Write((byte)subLen);
            bw.Write(subBlock, 0, subLen);
        }
    }

    public void Dispose()
    {
        RequestStop();
    }
}
