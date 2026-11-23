using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aurora.Application.Security;
using Aurora.Core.Contracts;
using Aurora.Core.Models;
using Aurora.Core.Primitives;
using Aurora.Core.Tokens;

namespace Aurora.Infrastructure.Themes;

public sealed class ThemePackageArchiveService(IAuroraLogger logger) : IThemePackageStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<AuroraResult<string>> ExportPackageAsync(
        AuroraThemeTokens theme,
        string destinationFilePath,
        string author = "Aurora Kullanıcısı",
        CancellationToken ct = default)
    {
        try
        {
            var manifest = new ThemePackageManifest(
                SchemaVersion: ThemePackageValidator.SupportedSchemaVersion,
                PackageId: theme.Id,
                Name: theme.DisplayName,
                Author: author,
                Version: "1.0.0",
                Theme: theme,
                ApprovedAssets: []);

            var validation = ThemePackageValidator.ValidateManifest(manifest);
            if (!validation.IsValid)
            {
                return AuroraResult<string>.Fail("THEME_EXPORT_INVALID", string.Join(" ", validation.Errors));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath)!);
            var manifestJson = JsonSerializer.Serialize(manifest, JsonOpts);
            var manifestBytes = Encoding.UTF8.GetBytes(manifestJson);

            if (destinationFilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                await File.WriteAllBytesAsync(destinationFilePath, manifestBytes, ct);
            }
            else
            {
                if (File.Exists(destinationFilePath))
                    File.Delete(destinationFilePath);

                await using var fs = new FileStream(destinationFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

                var entry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                await using (var es = entry.Open())
                {
                    await es.WriteAsync(manifestBytes, ct);
                }

                var shaHex = Convert.ToHexString(SHA256.HashData(manifestBytes));
                var sigEntry = archive.CreateEntry("manifest.sha256", CompressionLevel.Optimal);
                await using (var ss = sigEntry.Open())
                {
                    var sigBytes = Encoding.UTF8.GetBytes(shaHex);
                    await ss.WriteAsync(sigBytes, ct);
                }
            }

            logger.Info("Themes", "THEME_PACKAGE_EXPORTED", $"Exported theme package '{theme.Id}'.");
            return AuroraResult<string>.Ok(destinationFilePath);
        }
        catch (Exception ex)
        {
            logger.Error("Themes", "THEME_EXPORT_ERROR", "Failed to export theme package.", ex);
            return AuroraResult<string>.Fail("THEME_EXPORT_IO", "Tema paketi dışa aktarılamadı.");
        }
    }

    public async Task<AuroraResult<ThemePackageManifest>> ImportPackageAsync(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return AuroraResult<ThemePackageManifest>.Fail("THEME_FILE_NOT_FOUND", "Tema paketi dosyası bulunamadı.");

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > ThemePackageValidator.MaxTotalUncompressedBytes)
                return AuroraResult<ThemePackageManifest>.Fail("THEME_OVERSIZED", "Tema paketi izin verilen maksimum boyutu (8 MB) aşıyor.");

            string manifestJson;
            if (filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                if (fileInfo.Length > ThemePackageValidator.MaxManifestBytes)
                    return AuroraResult<ThemePackageManifest>.Fail("THEME_MANIFEST_OVERSIZED", "Manifest boyutu sınırı aşıldı.");

                manifestJson = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);
            }
            else
            {
                await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var archive = new ZipArchive(fs, ZipArchiveMode.Read);

                if (archive.Entries.Count == 0 || archive.Entries.Count > ThemePackageValidator.MaxArchiveEntries)
                    return AuroraResult<ThemePackageManifest>.Fail("THEME_ENTRY_LIMIT", "Tema paketindeki dosya sayısı güvenlik sınırını aşıyor.");

                long totalBytesRead = 0;
                byte[]? manifestBytes = null;
                string? expectedSha256 = null;

                foreach (var entry in archive.Entries)
                {
                    if (!string.Equals(entry.FullName, "manifest.sha256", StringComparison.OrdinalIgnoreCase))
                    {
                        var pathCheck = ThemePackageValidator.ValidateRelativeAssetPath(entry.FullName);
                        if (!pathCheck.IsSuccess)
                            return AuroraResult<ThemePackageManifest>.Fail(pathCheck.Error!.Code, pathCheck.Error.Message);
                    }

                    await using var stream = entry.Open();
                    using var ms = new MemoryStream();
                    var buffer = new byte[8192];
                    long entryRead = 0;
                    int read;
                    while ((read = await stream.ReadAsync(buffer, ct)) > 0)
                    {
                        entryRead += read;
                        totalBytesRead += read;

                        if (entryRead > ThemePackageValidator.MaxSingleAssetBytes ||
                            totalBytesRead > ThemePackageValidator.MaxTotalUncompressedBytes)
                        {
                            return AuroraResult<ThemePackageManifest>.Fail("THEME_ZIP_BOMB", "Sıkıştırılmış varlık boyutu güvenlik sınırını aştı.");
                        }

                        ms.Write(buffer, 0, read);
                    }

                    if (string.Equals(entry.FullName, "manifest.json", StringComparison.OrdinalIgnoreCase))
                        manifestBytes = ms.ToArray();
                    else if (string.Equals(entry.FullName, "manifest.sha256", StringComparison.OrdinalIgnoreCase))
                        expectedSha256 = Encoding.UTF8.GetString(ms.ToArray()).Trim();
                }

                if (manifestBytes is null)
                    return AuroraResult<ThemePackageManifest>.Fail("THEME_MISSING_MANIFEST", "Tema arşivinde manifest.json bulunamadı.");

                if (!string.IsNullOrWhiteSpace(expectedSha256))
                {
                    var actualSha = Convert.ToHexString(SHA256.HashData(manifestBytes));
                    if (!string.Equals(actualSha, expectedSha256, StringComparison.OrdinalIgnoreCase))
                        return AuroraResult<ThemePackageManifest>.Fail("THEME_INTEGRITY_MISMATCH", "Tema paketi bütünlük doğrulaması (SHA-256) başarısız oldu.");
                }

                manifestJson = Encoding.UTF8.GetString(manifestBytes);
            }

            var manifest = JsonSerializer.Deserialize<ThemePackageManifest>(manifestJson, JsonOpts);
            var report = ThemePackageValidator.ValidateManifest(manifest);
            if (!report.IsValid)
            {
                return AuroraResult<ThemePackageManifest>.Fail("THEME_SCHEMA_INVALID", string.Join(" ", report.Errors));
            }

            logger.Info("Themes", "THEME_PACKAGE_IMPORTED", $"Validated theme package '{manifest!.PackageId}' cleanly.");
            return AuroraResult<ThemePackageManifest>.Ok(manifest);
        }
        catch (InvalidDataException)
        {
            return AuroraResult<ThemePackageManifest>.Fail("THEME_CORRUPT_ARCHIVE", "Tema arşivi bozuk veya geçerli bir paket değil.");
        }
        catch (JsonException)
        {
            return AuroraResult<ThemePackageManifest>.Fail("THEME_CORRUPT_JSON", "Tema bildirimi geçerli bir JSON yapısında değil.");
        }
        catch (Exception ex)
        {
            logger.Error("Themes", "THEME_IMPORT_ERROR", "Unexpected error while validating theme package.", ex);
            return AuroraResult<ThemePackageManifest>.Fail("THEME_IMPORT_FAILED", "Tema paketi doğrulanamadı.");
        }
    }
}
