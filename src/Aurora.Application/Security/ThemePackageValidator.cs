using Aurora.Core.Models;
using Aurora.Core.Primitives;
using Aurora.Core.Tokens;

namespace Aurora.Application.Security;

public sealed record ThemeValidationReport(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> ContrastWarnings);

public static class ThemePackageValidator
{
    public const string SupportedSchemaVersion = "1.0";
    public const int MaxArchiveEntries = 16;
    public const long MaxSingleAssetBytes = 2L * 1024 * 1024;
    public const long MaxTotalUncompressedBytes = 8L * 1024 * 1024;
    public const long MaxManifestBytes = 512L * 1024;

    private static readonly HashSet<string> AllowedAssetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp"
    };

    private static readonly HashSet<string> ForbiddenExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".ps1", ".bat", ".cmd", ".vbs", ".js", ".jse", ".wsf",
        ".msi", ".scr", ".com", ".pif", ".hta", ".xaml", ".cs", ".py", ".sh", ".reg"
    };

    public static ThemeValidationReport ValidateManifest(ThemePackageManifest? manifest)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (manifest is null)
            return new ThemeValidationReport(false, ["Tema paketi bildirimi (manifest) boş olamaz."], []);

        if (!string.Equals(manifest.SchemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
            errors.Add($"Desteklenmeyen tema şema sürümü: '{manifest.SchemaVersion}'. Beklenen: '{SupportedSchemaVersion}'.");

        if (string.IsNullOrWhiteSpace(manifest.PackageId) || manifest.PackageId.Length > 80)
            errors.Add("Geçersiz PackageId (1-80 karakter olmalıdır).");

        if (string.IsNullOrWhiteSpace(manifest.Name) || manifest.Name.Length > 100)
            errors.Add("Geçersiz tema adı.");

        if (ContainsUnsafeExternalReference(manifest.PackageId) ||
            ContainsUnsafeExternalReference(manifest.Name) ||
            ContainsUnsafeExternalReference(manifest.Author))
        {
            errors.Add("Tema meta verilerinde harici ağ veya dosya sistemi referansı kullanılamaz.");
        }

        if (manifest.ApprovedAssets is not null)
        {
            if (manifest.ApprovedAssets.Count > MaxArchiveEntries)
                errors.Add($"Varlık sayısı sınırı aşıldı (en fazla {MaxArchiveEntries}).");

            foreach (var assetPath in manifest.ApprovedAssets)
            {
                var pathCheck = ValidateRelativeAssetPath(assetPath);
                if (!pathCheck.IsSuccess)
                    errors.Add(pathCheck.Error!.Message);
            }
        }

        if (manifest.Theme is null)
        {
            errors.Add("Tema jetonları (Theme) eksik.");
            return new ThemeValidationReport(false, errors, warnings);
        }

        ValidateThemeTokens(manifest.Theme, errors, warnings);
        return new ThemeValidationReport(errors.Count == 0, errors, warnings);
    }

    public static void ValidateThemeTokens(AuroraThemeTokens theme, List<string> errors, List<string> warnings)
    {
        if (ContainsUnsafeExternalReference(theme.Typography.FontFamily))
            errors.Add("Yazı tipi ailesinde harici URI kullanılamaz.");

        var c = theme.Colors;
        string[] allColors =
        [
            c.Canvas, c.Surface1, c.Surface2, c.Surface3, c.SurfaceElevated,
            c.TextPrimary, c.TextSecondary, c.TextDisabled,
            c.BorderSubtle, c.BorderStrong, c.AccentPrimary, c.AccentSecondary,
            c.StateSuccess, c.StateWarning, c.StateDanger, c.StateInfo, c.FocusRing
        ];

        if (allColors.Any(hex => !ContrastCalculator.IsValidHexColor(hex)))
            errors.Add("Tüm semantik renk jetonları geçerli #RRGGBB veya #AARRGGBB biçiminde olmalıdır.");

        if (theme.Material.SurfaceOpacity is < 0.45 or > 1.0)
            errors.Add("Yüzey opaklığı 0.45 ile 1.0 arasında olmalıdır.");

        if (theme.Typography.ScaleFactor is < 0.80 or > 1.40)
            errors.Add("Tipografi ölçeği 0.80 ile 1.40 arasında olmalıdır.");

        if (theme.Radius.ScaleFactor is < 0.30 or > 1.60)
            errors.Add("Köşe yarıçapı ölçeği 0.30 ile 1.60 arasında olmalıdır.");

        if (theme.IconSizeDip is < 16.0 or > 36.0)
            errors.Add("Simge boyutu 16 ile 36 DIP arasında olmalıdır.");

        if (errors.Count == 0)
        {
            if (!ContrastCalculator.MeetsWcagAA(c.TextPrimary, c.Surface1))
            {
                var ratio = ContrastCalculator.CalculateContrastRatio(c.TextPrimary, c.Surface1);
                warnings.Add($"Birincil metin kontrastı ({ratio}:1) WCAG 2.2 AA hedefinin (4.5:1) altında.");
            }

            if (!ContrastCalculator.MeetsWcagAA(c.FocusRing, c.Surface1, largeText: true))
                warnings.Add("Odak halkası kontrastı (FocusRing) düşük görünürlüğe sahip olabilir.");
        }
    }

    public static AuroraResult ValidateRelativeAssetPath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return AuroraResult.Fail("THEME_EMPTY_PATH", "Varlık dosya yolu boş olamaz.");

        if (ContainsUnsafeExternalReference(relativePath))
            return AuroraResult.Fail("THEME_EXTERNAL_REF", $"Harici URI reddedildi: '{relativePath}'.");

        if (relativePath.StartsWith('/') || relativePath.StartsWith('\\') || relativePath.Contains(':'))
            return AuroraResult.Fail("THEME_ABSOLUTE_PATH", $"Mutlak veya sürücü kök yolu reddedildi: '{relativePath}'.");

        var segments = relativePath.Split(['/', '\\'], StringSplitOptions.None);
        if (segments.Any(s => s is ".." or "." || string.IsNullOrWhiteSpace(s) || s.Any(char.IsControl)))
            return AuroraResult.Fail("THEME_PATH_TRAVERSAL", $"Yol geçişi (path traversal) girişimi engellendi: '{relativePath}'.");

        var ext = Path.GetExtension(relativePath);
        if (ForbiddenExtensions.Contains(ext))
            return AuroraResult.Fail("THEME_EXECUTABLE_CONTENT", $"Çalıştırılabilir veya betik içeriği reddedildi: '{ext}'.");

        if (!string.Equals(relativePath, "manifest.json", StringComparison.OrdinalIgnoreCase) &&
            !AllowedAssetExtensions.Contains(ext))
        {
            return AuroraResult.Fail("THEME_UNAPPROVED_EXT", $"Onaylanmayan dosya uzantısı: '{ext}'.");
        }

        return AuroraResult.Ok();
    }

    public static bool ContainsUnsafeExternalReference(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        return input.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
               input.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
               input.Contains("ftp://", StringComparison.OrdinalIgnoreCase) ||
               input.Contains("file://", StringComparison.OrdinalIgnoreCase) ||
               input.Contains("javascript:", StringComparison.OrdinalIgnoreCase) ||
               input.StartsWith(@"\\", StringComparison.Ordinal);
    }
}
