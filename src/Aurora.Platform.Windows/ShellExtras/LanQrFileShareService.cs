using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Aurora.Platform.Windows.ShellExtras;

public sealed record LanQrShareSession(
    string Token,
    string Url,
    string DisplayName,
    long SizeBytes,
    string SizeLabel,
    string QrSvg,
    int DownloadCount,
    DateTimeOffset ExpiresAtUtc);

public sealed record QuickFileActionOutcome(
    bool Success,
    string OutputPath,
    string SummaryTitle,
    string SummaryDetail);

/// <summary>
/// Zero-UAC (asInvoker) Local Wi-Fi / LAN AirDrop-style QR file share service + Quick Convert/ZIP utility.
/// Uses an ephemeral high-port TcpListener (no Windows HttpListener URL ACLs or admin rights required!)
/// and auto-shuts down cleanly on close or after 5 minutes.
/// </summary>
public sealed class LanQrFileShareService : IDisposable
{
    private readonly object _sync = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private string? _servedFilePath;
    private string? _servedDisplayName;
    private string? _tempBundlePath;
    private string? _token;
    private string? _url;
    private string? _qrSvg;
    private long _sizeBytes;
    private int _downloadCount;
    private DateTimeOffset _expiresAtUtc;

    public event Action? SessionChanged;

    public LanQrShareSession? GetActiveSession()
    {
        lock (_sync)
        {
            if (_listener is null || string.IsNullOrWhiteSpace(_token) || DateTimeOffset.UtcNow >= _expiresAtUtc)
                return null;

            return new LanQrShareSession(
                Token: _token,
                Url: _url ?? string.Empty,
                DisplayName: _servedDisplayName ?? "Dosya",
                SizeBytes: _sizeBytes,
                SizeLabel: FormatSize(_sizeBytes),
                QrSvg: _qrSvg ?? string.Empty,
                DownloadCount: _downloadCount,
                ExpiresAtUtc: _expiresAtUtc);
        }
    }

    public LanQrShareSession StartSharing(IReadOnlyList<string> paths)
    {
        StopSharing();

        var valid = paths
            .Where(p => !string.IsNullOrWhiteSpace(p) && (File.Exists(p) || Directory.Exists(p)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (valid.Count == 0)
            throw new FileNotFoundException("Paylaşılacak geçerli dosya bulunamadı.");

        string targetPath;
        string displayName;
        string? tempZip = null;

        if (valid.Count == 1 && File.Exists(valid[0]))
        {
            targetPath = valid[0];
            displayName = Path.GetFileName(targetPath);
        }
        else
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ProjectAurora", "LanShare");
            Directory.CreateDirectory(tempDir);
            string baseLabel = valid.Count == 1
                ? Path.GetFileName(valid[0].TrimEnd('\\', '/'))
                : $"Aurora-Paylasim-{valid.Count}-Oge";
            tempZip = Path.Combine(tempDir, $"{SanitizeFileName(baseLabel)}-{DateTime.Now:HHmmss}.zip");
            CreateZipFromPaths(valid, tempZip);
            targetPath = tempZip;
            displayName = Path.GetFileName(tempZip);
        }

        long sizeBytes = new FileInfo(targetPath).Length;
        string lanIp = ResolvePrimaryLanIpv4();
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant();

        var listener = new TcpListener(IPAddress.Any, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        string url = $"http://{lanIp}:{port}/s/{token}";
        string qrSvg = QrCodeSvgGenerator.GenerateSvg(url);

        var cts = new CancellationTokenSource();
        var expires = DateTimeOffset.UtcNow.AddMinutes(5);

        lock (_sync)
        {
            _listener = listener;
            _cts = cts;
            _servedFilePath = targetPath;
            _servedDisplayName = displayName;
            _tempBundlePath = tempZip;
            _token = token;
            _url = url;
            _qrSvg = qrSvg;
            _sizeBytes = sizeBytes;
            _downloadCount = 0;
            _expiresAtUtc = expires;
        }

        _ = Task.Run(() => AcceptLoopAsync(listener, cts.Token));
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), cts.Token);
                StopSharing();
            }
            catch { }
        });

        SessionChanged?.Invoke();
        return GetActiveSession()!;
    }

    public void StopSharing()
    {
        TcpListener? oldListener;
        CancellationTokenSource? oldCts;
        string? oldTemp;

        lock (_sync)
        {
            oldListener = _listener;
            oldCts = _cts;
            oldTemp = _tempBundlePath;

            _listener = null;
            _cts = null;
            _servedFilePath = null;
            _servedDisplayName = null;
            _tempBundlePath = null;
            _token = null;
            _url = null;
            _qrSvg = null;
            _sizeBytes = 0;
            _downloadCount = 0;
        }

        try { oldCts?.Cancel(); } catch { }
        try { oldListener?.Stop(); } catch { }
        try { oldCts?.Dispose(); } catch { }
        if (!string.IsNullOrWhiteSpace(oldTemp) && File.Exists(oldTemp))
        {
            try { File.Delete(oldTemp); } catch { }
        }

        if (oldListener is not null)
            SessionChanged?.Invoke();
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
            catch
            {
                break;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            client.ReceiveTimeout = 8000;
            client.SendTimeout = 30000;

            using var stream = client.GetStream();
            byte[] headerBuf = new byte[4096];
            int read = await stream.ReadAsync(headerBuf, ct);
            if (read <= 0) return;

            string reqText = Encoding.ASCII.GetString(headerBuf, 0, read);
            string firstLine = reqText.Split('\n')[0].Trim();
            var parts = firstLine.Split(' ');
            if (parts.Length < 2) return;
            string reqPath = parts[1].Split('?')[0];

            string? token;
            string? filePath;
            string? displayName;
            long sizeBytes;

            lock (_sync)
            {
                token = _token;
                filePath = _servedFilePath;
                displayName = _servedDisplayName;
                sizeBytes = _sizeBytes;
            }

            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                await WriteSimpleResponseAsync(stream, "404 Not Found", "text/plain; charset=utf-8", "Paylasim suresi doldu veya kapatildi.", ct);
                return;
            }

            if (string.Equals(reqPath, $"/s/{token}", StringComparison.Ordinal))
            {
                string safeName = WebUtility.HtmlEncode(displayName ?? "Dosya");
                string html = $$"""
                    <!DOCTYPE html>
                    <html lang="tr">
                    <head>
                    <meta charset="UTF-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1.0">
                    <title>Project Aurora — Hızlı Dosya Aktarımı</title>
                    <style>
                    body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;background:#090D18;color:#F8FAFC;font-family:system-ui,-apple-system,sans-serif;padding:20px;box-sizing:border-box;}
                    .card{width:100%;max-width:380px;background:linear-gradient(160deg,#11182B,#0B1120);border:1px solid rgba(56,189,248,.35);border-radius:24px;padding:26px 22px;text-align:center;box-shadow:0 24px 60px rgba(0,0,0,.75);}
                    .badge{display:inline-block;padding:4px 12px;border-radius:99px;background:rgba(56,189,248,.16);color:#38BDF8;font-size:12px;font-weight:700;margin-bottom:14px;}
                    h1{font-size:18px;margin:0 0 8px;word-break:break-word;}
                    .meta{color:#94A3B8;font-size:13px;margin-bottom:22px;}
                    .btn{display:block;width:100%;padding:14px 18px;border-radius:14px;background:linear-gradient(135deg,#38BDF8,#818CF8);color:#04101F;font-weight:800;font-size:15px;text-decoration:none;box-sizing:border-box;}
                    </style>
                    </head>
                    <body>
                    <div class="card">
                      <div class="badge">✦ Project Aurora Yerel Wi-Fi Aktarım</div>
                      <h1>{{safeName}}</h1>
                      <div class="meta">Boyut: {{FormatSize(sizeBytes)}} · Buluta yüklenmeden doğrudan aktarım</div>
                      <a class="btn" href="/d/{{token}}">Dosyayı İndir</a>
                    </div>
                    </body>
                    </html>
                    """;
                await WriteSimpleResponseAsync(stream, "200 OK", "text/html; charset=utf-8", html, ct);
                return;
            }

            if (string.Equals(reqPath, $"/d/{token}", StringComparison.Ordinal))
            {
                string encodedName = Uri.EscapeDataString(displayName ?? "aurora-dosya");
                string asciiName = SanitizeAsciiHeaderName(displayName ?? "aurora-dosya");
                string header =
                    $"HTTP/1.1 200 OK\r\n" +
                    $"Content-Type: application/octet-stream\r\n" +
                    $"Content-Length: {sizeBytes}\r\n" +
                    $"Content-Disposition: attachment; filename=\"{asciiName}\"; filename*=UTF-8''{encodedName}\r\n" +
                    $"Connection: close\r\n\r\n";

                byte[] hdrBytes = Encoding.ASCII.GetBytes(header);
                await stream.WriteAsync(hdrBytes, ct);

                await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
                await fs.CopyToAsync(stream, 65536, ct);
                await stream.FlushAsync(ct);

                Interlocked.Increment(ref _downloadCount);
                SessionChanged?.Invoke();
                return;
            }

            await WriteSimpleResponseAsync(stream, "404 Not Found", "text/plain; charset=utf-8", "Bulunamadi.", ct);
        }
    }

    private static async Task WriteSimpleResponseAsync(NetworkStream stream, string status, string contentType, string body, CancellationToken ct)
    {
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        string header =
            $"HTTP/1.1 {status}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {bodyBytes.Length}\r\n" +
            $"Connection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(header), ct);
        await stream.WriteAsync(bodyBytes, ct);
        await stream.FlushAsync(ct);
    }

    /// <summary>
    /// Smart quick file action for NotchNook-style Drop Zone #3 ("Hızlı Dönüştür / ZIP"):
    /// - Single PNG/BMP/TIFF image -> optimized high-quality JPEG beside original (instant size cut!)
    /// - Single JPG/WEBP -> lossless PNG beside original
    /// - Multiple files or folder -> timestamped ZIP archive beside first item
    /// </summary>
    public static QuickFileActionOutcome ExecuteQuickConvertOrZip(IReadOnlyList<string> paths)
    {
        var valid = paths
            .Where(p => !string.IsNullOrWhiteSpace(p) && (File.Exists(p) || Directory.Exists(p)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (valid.Count == 0)
            return new QuickFileActionOutcome(false, string.Empty, "Dönüştürme Başarısız", "Geçerli dosya bulunamadı.");

        try
        {
            if (valid.Count == 1 && File.Exists(valid[0]))
            {
                string src = valid[0];
                string ext = Path.GetExtension(src).ToLowerInvariant();
                string dir = ResolveWritableOutputDir(Path.GetDirectoryName(src));
                string stem = Path.GetFileNameWithoutExtension(src);

                if (ext is ".png" or ".bmp" or ".tif" or ".tiff")
                {
                    string outPath = EnsureUniquePath(Path.Combine(dir, $"{stem}_aurora.jpg"));
                    using var img = Image.FromFile(src);
                    var jpgCodec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    if (jpgCodec is not null)
                    {
                        using var encParams = new EncoderParameters(1);
                        encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 88L);
                        img.Save(outPath, jpgCodec, encParams);
                    }
                    else
                    {
                        img.Save(outPath, ImageFormat.Jpeg);
                    }
                    long before = new FileInfo(src).Length;
                    long after = new FileInfo(outPath).Length;
                    return new QuickFileActionOutcome(true, outPath, "Görsel JPG'ye Sıkıştırıldı", $"{Path.GetFileName(outPath)} ({FormatSize(before)} → {FormatSize(after)})");
                }

                if (ext is ".jpg" or ".jpeg")
                {
                    string outPath = EnsureUniquePath(Path.Combine(dir, $"{stem}_aurora.png"));
                    using var img = Image.FromFile(src);
                    img.Save(outPath, ImageFormat.Png);
                    return new QuickFileActionOutcome(true, outPath, "Görsel PNG'ye Dönüştürüldü", $"{Path.GetFileName(outPath)} ({FormatSize(new FileInfo(outPath).Length)})");
                }
            }

            string zipDir = ResolveWritableOutputDir(File.Exists(valid[0]) ? Path.GetDirectoryName(valid[0]) : Directory.GetParent(valid[0])?.FullName);
            string baseName = valid.Count == 1
                ? Path.GetFileNameWithoutExtension(valid[0].TrimEnd('\\', '/'))
                : $"Aurora-Arsiv-{DateTime.Now:yyyyMMdd-HHmmss}";
            string zipPath = EnsureUniquePath(Path.Combine(zipDir, $"{SanitizeFileName(baseName)}.zip"));
            CreateZipFromPaths(valid, zipPath);
            return new QuickFileActionOutcome(true, zipPath, "ZIP Arşivi Hazır", $"{Path.GetFileName(zipPath)} ({FormatSize(new FileInfo(zipPath).Length)})");
        }
        catch (Exception ex)
        {
            return new QuickFileActionOutcome(false, string.Empty, "Hızlı Dönüştürme Hatası", ex.Message);
        }
    }

    public static string ResolvePrimaryLanIpv4()
    {
        try
        {
            var candidates = new List<(IPAddress Addr, int Score)>();
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                string desc = $"{nic.Name} {nic.Description}".ToLowerInvariant();
                bool isVirtual = desc.Contains("vethernet") || desc.Contains("wsl") || desc.Contains("virtualbox") || desc.Contains("vmware") || desc.Contains("hyper-v") || desc.Contains("tailscale") || desc.Contains("vpn");

                var props = nic.GetIPProperties();
                bool hasGateway = props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.Any.Equals(g.Address));
                foreach (var ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(ua.Address)) continue;
                    var bytes = ua.Address.GetAddressBytes();
                    if (bytes[0] == 169 && bytes[1] == 254) continue; // APIPA

                    int score = 0;
                    if (!isVirtual) score += 50;
                    if (hasGateway) score += 40;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) score += 15;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet) score += 12;
                    if (bytes[0] == 192 && bytes[1] == 168) score += 10;
                    candidates.Add((ua.Address, score));
                }
            }

            if (candidates.Count > 0)
                return candidates.OrderByDescending(c => c.Score).First().Addr.ToString();
        }
        catch { }

        return "127.0.0.1";
    }

    private static void CreateZipFromPaths(List<string> paths, string zipPath)
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        var usedEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in paths)
        {
            if (File.Exists(p))
            {
                string name = MakeUniqueEntryName(Path.GetFileName(p), usedEntryNames);
                zip.CreateEntryFromFile(p, name, CompressionLevel.Optimal);
            }
            else if (Directory.Exists(p))
            {
                string rootFolder = Path.GetFileName(p.TrimEnd('\\', '/'));
                foreach (var file in Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(p, file).Replace('\\', '/');
                    string entryName = MakeUniqueEntryName($"{rootFolder}/{rel}", usedEntryNames);
                    zip.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                }
            }
        }
    }

    private static string MakeUniqueEntryName(string candidate, HashSet<string> used)
    {
        if (used.Add(candidate)) return candidate;
        string stem = Path.GetFileNameWithoutExtension(candidate);
        string ext = Path.GetExtension(candidate);
        string? dir = Path.GetDirectoryName(candidate)?.Replace('\\', '/');
        for (int i = 2; i < 1000; i++)
        {
            string alt = string.IsNullOrEmpty(dir) ? $"{stem}_{i}{ext}" : $"{dir}/{stem}_{i}{ext}";
            if (used.Add(alt)) return alt;
        }
        return candidate;
    }

    private static string ResolveWritableOutputDir(string? preferredDir)
    {
        if (!string.IsNullOrWhiteSpace(preferredDir) && Directory.Exists(preferredDir))
            return preferredDir;
        return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    }

    private static string EnsureUniquePath(string fullPath)
    {
        if (!File.Exists(fullPath)) return fullPath;
        string dir = Path.GetDirectoryName(fullPath) ?? string.Empty;
        string stem = Path.GetFileNameWithoutExtension(fullPath);
        string ext = Path.GetExtension(fullPath);
        for (int i = 2; i < 500; i++)
        {
            string candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
        return fullPath;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Aurora-Dosya" : cleaned;
    }

    private static string SanitizeAsciiHeaderName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            if (c >= 32 && c < 127 && c != '"' && c != '\\') sb.Append(c);
            else sb.Append('_');
        }
        return sb.Length > 0 ? sb.ToString() : "aurora-file";
    }

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes / (1024.0 * 1024.0):0.##} MB";
    }

    public void Dispose() => StopSharing();
}
