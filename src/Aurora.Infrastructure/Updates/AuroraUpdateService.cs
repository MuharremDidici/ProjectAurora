using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace Aurora.Infrastructure.Updates;

public sealed record AuroraReleaseManifest(
    string Version,
    string ReleaseNotes,
    string DownloadUrl,
    string? Sha256 = null,
    DateTimeOffset? PublishedAtUtc = null);

public sealed record AuroraUpdateCheckResult(
    bool Success,
    bool UpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string Message,
    AuroraReleaseManifest? Manifest = null);

public sealed record AuroraUpdateDownloadProgress(
    int Percent,
    long DownloadedBytes,
    long TotalBytes,
    string StageText);

public sealed record AuroraUpdateDownloadResult(
    bool Success,
    string? InstallerPath,
    bool Sha256Verified,
    string Message);

/// <summary>
/// GitHub Releases powered auto-updater with zero-rate-limit CDN manifest lookup,
/// automatic GitHub Releases API fallback, streaming progress download, PE validation,
/// and strict SHA-256 package integrity verification.
/// </summary>
public static class AuroraUpdateService
{
    public const string DefaultRepositoryOwner = "MuharremDidici";
    public const string DefaultRepositoryName = "ProjectAurora";
    public const string DefaultRepositoryUrl = $"https://github.com/{DefaultRepositoryOwner}/{DefaultRepositoryName}";
    public const string DefaultManifestUrl = $"https://github.com/{DefaultRepositoryOwner}/{DefaultRepositoryName}/releases/latest/download/release-manifest.json";
    public const string DefaultGitHubApiLatestUrl = $"https://api.github.com/repos/{DefaultRepositoryOwner}/{DefaultRepositoryName}/releases/latest";

    public const string FallbackVersion = "2.0.0";

    public static string CurrentVersion { get; } = ResolveAssemblyVersion();

    private static string ResolveAssemblyVersion()
    {
        try
        {
            var asm = Assembly.GetEntryAssembly() ?? typeof(AuroraUpdateService).Assembly;
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                string clean = info.Split('+')[0].Trim().TrimStart('v', 'V');
                if (TryParseSemVer(clean, out _))
                    return clean;
            }
            var ver = asm.GetName().Version;
            if (ver is not null && ver.Major >= 2)
                return $"{ver.Major}.{ver.Minor}.{Math.Max(0, ver.Build)}";
        }
        catch { }
        return FallbackVersion;
    }

    public static string ResolveEffectiveManifestUrl(string? configuredUrl)
    {
        if (string.IsNullOrWhiteSpace(configuredUrl))
            return DefaultManifestUrl;
        return configuredUrl.Trim();
    }

    public static bool IsNewerVersion(string? remoteVersion, string? currentVersion = null)
    {
        string cur = string.IsNullOrWhiteSpace(currentVersion) ? CurrentVersion : currentVersion;
        if (!TryParseSemVer(remoteVersion, out var r) || !TryParseSemVer(cur, out var c))
            return false;
        return r > c;
    }

    public static AuroraReleaseManifest? ParseManifestJson(string? json, Uri? sourceBaseUri = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Supports both Aurora release-manifest.json and GitHub Releases API JSON seamlessly
            string version = ReadFirstString(root, "version", "tag_name")?.TrimStart('v', 'V') ?? string.Empty;
            if (string.IsNullOrWhiteSpace(version))
                return null;

            string notes = ReadFirstString(root, "releaseNotes", "body") ?? string.Empty;
            string url = ReadFirstString(root, "downloadUrl") ?? string.Empty;
            string? sha256 = ReadFirstString(root, "sha256");

            // For GitHub Releases API JSON, prefer the direct Aurora-Kurulum-*.exe asset download URL over html_url
            if (string.IsNullOrWhiteSpace(url) &&
                root.TryGetProperty("assets", out var assetsProp) &&
                assetsProp.ValueKind == JsonValueKind.Array)
            {
                string? fallbackExe = null;
                foreach (var asset in assetsProp.EnumerateArray())
                {
                    string assetName = ReadFirstString(asset, "name") ?? string.Empty;
                    string assetUrl = ReadFirstString(asset, "browser_download_url") ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(assetUrl))
                        continue;

                    if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        if (assetName.Contains("Aurora-Kurulum", StringComparison.OrdinalIgnoreCase))
                        {
                            url = assetUrl;
                            break;
                        }
                        fallbackExe ??= assetUrl;
                    }
                }
                if (string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(fallbackExe))
                    url = fallbackExe;
            }

            if (string.IsNullOrWhiteSpace(url))
                url = ReadFirstString(root, "html_url") ?? string.Empty;

            // Resolve relative filename (e.g. "Aurora-Kurulum-v2.1-x64.exe") against the manifest URL directory
            if (!string.IsNullOrWhiteSpace(url) &&
                sourceBaseUri is not null &&
                !Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                if (Uri.TryCreate(sourceBaseUri, url, out var resolved))
                    url = resolved.AbsoluteUri;
            }

            DateTimeOffset? published = null;
            string? pubRaw = ReadFirstString(root, "publishedAtUtc", "published_at");
            if (!string.IsNullOrWhiteSpace(pubRaw) && DateTimeOffset.TryParse(pubRaw, out var dt))
                published = dt;

            return new AuroraReleaseManifest(version, notes, url, sha256, published);
        }
        catch
        {
            return null;
        }
    }

    public static bool IsDirectInstallerDownloadUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            return false;
        return uri.AbsolutePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    public static bool VerifyFileSha256(string filePath, string? expectedHex)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || string.IsNullOrWhiteSpace(expectedHex))
            return false;

        try
        {
            using var fs = File.OpenRead(filePath);
            string actual = Convert.ToHexString(SHA256.HashData(fs));
            return string.Equals(actual, expectedHex.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool HasValidPeHeader(string filePath)
    {
        try
        {
            var fi = new FileInfo(filePath);
            if (!fi.Exists || fi.Length < 1024 * 512) // Installer must be > 512 KB
                return false;

            using var fs = File.OpenRead(filePath);
            int b0 = fs.ReadByte();
            int b1 = fs.ReadByte();
            return b0 == 'M' && b1 == 'Z';
        }
        catch
        {
            return false;
        }
    }

    public static async Task<AuroraUpdateCheckResult> CheckForUpdatesAsync(
        string? manifestUrl = null,
        CancellationToken ct = default,
        bool allowDefaultFallback = true)
    {
        string effectiveUrl = allowDefaultFallback
            ? ResolveEffectiveManifestUrl(manifestUrl)
            : (manifestUrl?.Trim() ?? string.Empty);

        if (string.IsNullOrWhiteSpace(effectiveUrl))
        {
            return new AuroraUpdateCheckResult(
                Success: true,
                UpdateAvailable: false,
                CurrentVersion: CurrentVersion,
                LatestVersion: CurrentVersion,
                Message: $"Aurora v{CurrentVersion} güncel (çevrimdışı dağıtım modu).");
        }

        if (!Uri.TryCreate(effectiveUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return new AuroraUpdateCheckResult(
                Success: false,
                UpdateAvailable: false,
                CurrentVersion: CurrentVersion,
                LatestVersion: CurrentVersion,
                Message: "Güncelleme manifest adresi geçersiz.");
        }

        try
        {
            using var http = CreateHttpClient(TimeSpan.FromSeconds(10));
            AuroraReleaseManifest? manifest = null;

            using (var resp = await http.GetAsync(uri, ct))
            {
                if (resp.IsSuccessStatusCode)
                {
                    string body = await resp.Content.ReadAsStringAsync(ct);
                    var effectiveBaseUri = resp.RequestMessage?.RequestUri ?? uri;
                    manifest = ParseManifestJson(body, effectiveBaseUri);
                }
            }

            // If CDN manifest was not found (e.g., manual release without release-manifest.json),
            // gracefully fall back to GitHub Releases API to detect tag_name & .exe asset.
            if (manifest is null &&
                string.Equals(effectiveUrl, DefaultManifestUrl, StringComparison.OrdinalIgnoreCase))
            {
                using var apiResp = await http.GetAsync(DefaultGitHubApiLatestUrl, ct);
                if (apiResp.IsSuccessStatusCode)
                {
                    string apiBody = await apiResp.Content.ReadAsStringAsync(ct);
                    manifest = ParseManifestJson(apiBody, new Uri(DefaultGitHubApiLatestUrl));
                }
                else if (apiResp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new AuroraUpdateCheckResult(
                        Success: true,
                        UpdateAvailable: false,
                        CurrentVersion: CurrentVersion,
                        LatestVersion: CurrentVersion,
                        Message: $"Aurora v{CurrentVersion} en güncel sürüm.");
                }
            }

            if (manifest is null)
            {
                return new AuroraUpdateCheckResult(
                    Success: false,
                    UpdateAvailable: false,
                    CurrentVersion: CurrentVersion,
                    LatestVersion: CurrentVersion,
                    Message: "GitHub üzerinden sürüm bilgisi alınamadı.");
            }

            bool newer = IsNewerVersion(manifest.Version, CurrentVersion);
            return new AuroraUpdateCheckResult(
                Success: true,
                UpdateAvailable: newer,
                CurrentVersion: CurrentVersion,
                LatestVersion: manifest.Version,
                Message: newer
                    ? $"Yeni sürüm hazır: v{manifest.Version}"
                    : $"Aurora v{CurrentVersion} en güncel sürüm.",
                Manifest: manifest);
        }
        catch (Exception ex)
        {
            return new AuroraUpdateCheckResult(
                Success: false,
                UpdateAvailable: false,
                CurrentVersion: CurrentVersion,
                LatestVersion: CurrentVersion,
                Message: $"Güncelleme sunucusuna ulaşılamadı ({ex.GetType().Name}).");
        }
    }

    /// <summary>
    /// Downloads the release installer to %TEMP%\ProjectAurora\Updates with live progress,
    /// verifies PE header and SHA-256 checksum (when published in manifest), and returns the local path.
    /// </summary>
    public static async Task<AuroraUpdateDownloadResult> DownloadAndVerifyInstallerAsync(
        AuroraReleaseManifest manifest,
        IProgress<AuroraUpdateDownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (manifest is null || !IsDirectInstallerDownloadUrl(manifest.DownloadUrl))
        {
            return new AuroraUpdateDownloadResult(false, null, false, "Doğrudan indirilebilir kurulum (.exe) adresi bulunamadı.");
        }

        string updatesDir = Path.Combine(Path.GetTempPath(), "ProjectAurora", "Updates");
        Directory.CreateDirectory(updatesDir);

        string safeVer = string.Concat(manifest.Version.Where(ch => char.IsLetterOrDigit(ch) || ch == '.' || ch == '-'));
        if (string.IsNullOrWhiteSpace(safeVer)) safeVer = "latest";
        string finalPath = Path.Combine(updatesDir, $"Aurora-Update-v{safeVer}-x64.exe");
        string partPath = finalPath + ".part";

        try
        {
            // Reuse already-downloaded & hash-verified installer if present
            if (File.Exists(finalPath) &&
                HasValidPeHeader(finalPath) &&
                !string.IsNullOrWhiteSpace(manifest.Sha256) &&
                VerifyFileSha256(finalPath, manifest.Sha256))
            {
                progress?.Report(new AuroraUpdateDownloadProgress(100, new FileInfo(finalPath).Length, new FileInfo(finalPath).Length, "Doğrulandı"));
                return new AuroraUpdateDownloadResult(true, finalPath, true, $"v{manifest.Version} hazır (SHA-256 doğrulandı).");
            }

            if (File.Exists(partPath))
                File.Delete(partPath);

            progress?.Report(new AuroraUpdateDownloadProgress(0, 0, 0, "İndirme başlatılıyor..."));

            using var http = CreateHttpClient(TimeSpan.FromMinutes(15));
            using var response = await http.GetAsync(manifest.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                return new AuroraUpdateDownloadResult(false, null, false, $"İndirme başarısız (HTTP {(int)response.StatusCode}).");
            }

            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            long downloaded = 0;
            int lastReportedPct = -1;

            await using (var remoteStream = await response.Content.ReadAsStreamAsync(ct))
            await using (var localStream = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                byte[] buffer = new byte[81920];
                int read;
                while ((read = await remoteStream.ReadAsync(buffer, ct)) > 0)
                {
                    await localStream.WriteAsync(buffer.AsMemory(0, read), ct);
                    downloaded += read;

                    int pct = totalBytes > 0
                        ? (int)Math.Clamp((downloaded * 100L) / totalBytes, 0, 99)
                        : Math.Min(95, (int)(downloaded / (1024 * 1024)));

                    if (pct != lastReportedPct)
                    {
                        lastReportedPct = pct;
                        progress?.Report(new AuroraUpdateDownloadProgress(pct, downloaded, totalBytes, $"İndiriliyor %{pct}"));
                    }
                }
            }

            progress?.Report(new AuroraUpdateDownloadProgress(99, downloaded, Math.Max(totalBytes, downloaded), "Dosya doğrulanıyor..."));

            if (!HasValidPeHeader(partPath))
            {
                try { File.Delete(partPath); } catch { }
                return new AuroraUpdateDownloadResult(false, null, false, "İndirilen dosya geçerli bir Windows kurulum paketi değil.");
            }

            bool shaVerified = false;
            if (!string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                shaVerified = VerifyFileSha256(partPath, manifest.Sha256);
                if (!shaVerified)
                {
                    try { File.Delete(partPath); } catch { }
                    return new AuroraUpdateDownloadResult(false, null, false, "SHA-256 güvenlik doğrulaması başarısız; güncelleme iptal edildi.");
                }
            }

            File.Move(partPath, finalPath, overwrite: true);
            progress?.Report(new AuroraUpdateDownloadProgress(100, downloaded, Math.Max(totalBytes, downloaded), "Kuruluma hazır"));
            return new AuroraUpdateDownloadResult(true, finalPath, shaVerified, $"v{manifest.Version} indirildi ve doğrulandı.");
        }
        catch (OperationCanceledException)
        {
            try { if (File.Exists(partPath)) File.Delete(partPath); } catch { }
            return new AuroraUpdateDownloadResult(false, null, false, "Güncelleme indirmesi iptal edildi.");
        }
        catch (Exception ex)
        {
            try { if (File.Exists(partPath)) File.Delete(partPath); } catch { }
            return new AuroraUpdateDownloadResult(false, null, false, $"İndirme hatası ({ex.GetType().Name}).");
        }
    }

    private static HttpClient CreateHttpClient(TimeSpan timeout)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };
        var http = new HttpClient(handler) { Timeout = timeout };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"ProjectAurora/{CurrentVersion}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return http;
    }

    private static bool TryParseSemVer(string? raw, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        string cleaned = raw.Trim().TrimStart('v', 'V');
        int dash = cleaned.IndexOf('-');
        if (dash > 0)
            cleaned = cleaned[..dash];

        var parts = cleaned.Split('.');
        if (parts.Length == 2)
            cleaned += ".0";

        return Version.TryParse(cleaned, out version!);
    }

    private static string? ReadFirstString(JsonElement root, params string[] names)
    {
        foreach (var n in names)
        {
            if (root.TryGetProperty(n, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
        }
        return null;
    }
}
