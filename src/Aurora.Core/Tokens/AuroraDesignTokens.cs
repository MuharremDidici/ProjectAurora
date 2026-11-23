using System.Globalization;

namespace Aurora.Core.Tokens;

public enum AuroraThemeFamily
{
    AuroraGlass,
    ObsidianPro,
    AuroraFlow,
    Nebula
}

public enum AuroraColorMode
{
    Dark,
    Light
}

public enum AuroraBlurLevel
{
    None,
    Subtle,
    Standard,
    Strong
}

public enum AuroraElevationLevel
{
    Flat = 0,
    SubtleSeparation = 1,
    FloatingCard = 2,
    TransientPanel = 3
}

public sealed record ColorTokens(
    string Canvas,
    string Surface1,
    string Surface2,
    string Surface3,
    string SurfaceElevated,
    string TextPrimary,
    string TextSecondary,
    string TextDisabled,
    string BorderSubtle,
    string BorderStrong,
    string AccentPrimary,
    string AccentSecondary,
    string StateSuccess,
    string StateWarning,
    string StateDanger,
    string StateInfo,
    string FocusRing);

public sealed record TypographyTokens(
    string FontFamily = "Segoe UI Variable Display, Segoe UI",
    double ScaleFactor = 1.0,
    double DisplaySize = 28.0,
    double TitleSize = 20.0,
    double SectionSize = 15.0,
    double BodySize = 13.5,
    double CaptionSize = 11.5)
{
    public double ScaledDisplay => Math.Round(DisplaySize * ScaleFactor, 1);
    public double ScaledTitle => Math.Round(TitleSize * ScaleFactor, 1);
    public double ScaledSection => Math.Round(SectionSize * ScaleFactor, 1);
    public double ScaledBody => Math.Round(BodySize * ScaleFactor, 1);
    public double ScaledCaption => Math.Round(CaptionSize * ScaleFactor, 1);
}

public sealed record SpacingTokens(
    double ScaleFactor = 1.0)
{
    public double Space1 => Math.Round(4.0 * ScaleFactor, 1);
    public double Space2 => Math.Round(8.0 * ScaleFactor, 1);
    public double Space3 => Math.Round(12.0 * ScaleFactor, 1);
    public double Space4 => Math.Round(16.0 * ScaleFactor, 1);
    public double Space5 => Math.Round(20.0 * ScaleFactor, 1);
    public double Space6 => Math.Round(24.0 * ScaleFactor, 1);
    public double Space8 => Math.Round(32.0 * ScaleFactor, 1);
    public double Space10 => Math.Round(40.0 * ScaleFactor, 1);
}

public sealed record RadiusTokens(
    double ScaleFactor = 1.0)
{
    public double Xs => Math.Round(6.0 * ScaleFactor, 1);
    public double Sm => Math.Round(10.0 * ScaleFactor, 1);
    public double Md => Math.Round(14.0 * ScaleFactor, 1);
    public double Lg => Math.Round(20.0 * ScaleFactor, 1);
    public double Xl => Math.Round(26.0 * ScaleFactor, 1);
    public double Pill => 999.0;
}

public sealed record MaterialTokens(
    AuroraBlurLevel Blur = AuroraBlurLevel.Standard,
    double SurfaceOpacity = 0.86,
    double ShadowIntensity = 0.45,
    bool AmbientGradientEnabled = false);

public sealed record MotionTokens(
    int FastMs = 120,
    int StandardMs = 190,
    int EmphasisMs = 280,
    bool ReducedMotion = false,
    bool PauseWhenHidden = true)
{
    public int EffectiveFastMs => ReducedMotion ? 0 : FastMs;
    public int EffectiveStandardMs => ReducedMotion ? 0 : StandardMs;
    public int EffectiveEmphasisMs => ReducedMotion ? 0 : EmphasisMs;
}

public sealed record AuroraThemeTokens(
    string Id,
    string DisplayName,
    string Description,
    AuroraThemeFamily Family,
    AuroraColorMode ColorMode,
    ColorTokens Colors,
    TypographyTokens Typography,
    SpacingTokens Spacing,
    RadiusTokens Radius,
    MaterialTokens Material,
    MotionTokens Motion,
    double IconSizeDip = 22.0);

public static class ContrastCalculator
{
    public static (byte A, byte R, byte G, byte B) ParseHexColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return (255, 15, 23, 42);

        var s = hex.Trim().TrimStart('#');
        if (s.Length == 6 && uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return (255, (byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
        }

        if (s.Length == 8 && uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
        {
            return ((byte)((argb >> 24) & 0xFF), (byte)((argb >> 16) & 0xFF), (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF));
        }

        return (255, 15, 23, 42);
    }

    public static bool IsValidHexColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || !hex.StartsWith('#'))
            return false;
        var s = hex[1..];
        return (s.Length is 6 or 8) && uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
    }

    public static double RelativeLuminance(string hex)
    {
        var (_, r, g, b) = ParseHexColor(hex);
        static double Channel(byte c)
        {
            var v = c / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);
    }

    public static double CalculateContrastRatio(string foregroundHex, string backgroundHex)
    {
        var l1 = RelativeLuminance(foregroundHex);
        var l2 = RelativeLuminance(backgroundHex);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return Math.Round((lighter + 0.05) / (darker + 0.05), 2);
    }

    public static bool MeetsWcagAA(string foregroundHex, string backgroundHex, bool largeText = false)
    {
        var ratio = CalculateContrastRatio(foregroundHex, backgroundHex);
        return ratio >= (largeText ? 3.0 : 4.5);
    }
}

public static class BuiltInThemes
{
    public static AuroraThemeTokens AuroraGlassDark { get; } = new(
        Id: "aurora-glass-dark",
        DisplayName: "Aurora Glass",
        Description: "Buzlu yarı saydam yüzeyler, ince ışık kenarlıkları ve dengeli derinlik gölgeleri.",
        Family: AuroraThemeFamily.AuroraGlass,
        ColorMode: AuroraColorMode.Dark,
        Colors: new ColorTokens(
            Canvas: "#0B101E",
            Surface1: "#121A2F",
            Surface2: "#18233D",
            Surface3: "#202E4F",
            SurfaceElevated: "#1C2947",
            TextPrimary: "#F8FAFC",
            TextSecondary: "#CBD5E1",
            TextDisabled: "#64748B",
            BorderSubtle: "#2D3E63",
            BorderStrong: "#4A6396",
            AccentPrimary: "#38BDF8",
            AccentSecondary: "#818CF8",
            StateSuccess: "#34D399",
            StateWarning: "#FBBF24",
            StateDanger: "#FB7185",
            StateInfo: "#60A5FA",
            FocusRing: "#7DD3FC"),
        Typography: new TypographyTokens(),
        Spacing: new SpacingTokens(1.0),
        Radius: new RadiusTokens(1.0),
        Material: new MaterialTokens(AuroraBlurLevel.Standard, SurfaceOpacity: 0.86, ShadowIntensity: 0.45, AmbientGradientEnabled: false),
        Motion: new MotionTokens(120, 190, 280, ReducedMotion: false, PauseWhenHidden: true),
        IconSizeDip: 22.0);

    public static AuroraThemeTokens AuroraGlassLight { get; } = AuroraGlassDark with
    {
        Id = "aurora-glass-light",
        DisplayName = "Aurora Glass (Açık)",
        ColorMode = AuroraColorMode.Light,
        Colors = new ColorTokens(
            Canvas: "#EEF2F9",
            Surface1: "#FFFFFF",
            Surface2: "#F1F5FB",
            Surface3: "#E2E8F4",
            SurfaceElevated: "#FFFFFF",
            TextPrimary: "#0F172A",
            TextSecondary: "#334155",
            TextDisabled: "#94A3B8",
            BorderSubtle: "#CBD5E1",
            BorderStrong: "#94A3B8",
            AccentPrimary: "#0284C7",
            AccentSecondary: "#4F46E5",
            StateSuccess: "#059669",
            StateWarning: "#D97706",
            StateDanger: "#E11D48",
            StateInfo: "#2563EB",
            FocusRing: "#0284C7"),
        Material = new MaterialTokens(AuroraBlurLevel.Standard, SurfaceOpacity: 0.90, ShadowIntensity: 0.25, AmbientGradientEnabled: false)
    };

    public static AuroraThemeTokens ObsidianProDark { get; } = new(
        Id: "obsidian-pro-dark",
        DisplayName: "Obsidian Pro",
        Description: "Yoğun bilgi hiyerarşisi, yüksek kontrastlı profesyonel yüzeyler ve düşük hareket modu.",
        Family: AuroraThemeFamily.ObsidianPro,
        ColorMode: AuroraColorMode.Dark,
        Colors: new ColorTokens(
            Canvas: "#090A0D",
            Surface1: "#111318",
            Surface2: "#181B22",
            Surface3: "#212630",
            SurfaceElevated: "#1C2029",
            TextPrimary: "#F4F6F9",
            TextSecondary: "#B8C1CF",
            TextDisabled: "#636D7E",
            BorderSubtle: "#2A303D",
            BorderStrong: "#454F63",
            AccentPrimary: "#10B981",
            AccentSecondary: "#14B8A6",
            StateSuccess: "#22C55E",
            StateWarning: "#F59E0B",
            StateDanger: "#EF4444",
            StateInfo: "#3B82F6",
            FocusRing: "#34D399"),
        Typography: new TypographyTokens(ScaleFactor: 0.96),
        Spacing: new SpacingTokens(0.92),
        Radius: new RadiusTokens(0.75),
        Material: new MaterialTokens(AuroraBlurLevel.Subtle, SurfaceOpacity: 0.96, ShadowIntensity: 0.30, AmbientGradientEnabled: false),
        Motion: new MotionTokens(100, 140, 200, ReducedMotion: false, PauseWhenHidden: true),
        IconSizeDip: 20.0);

    public static AuroraThemeTokens AuroraFlowDark { get; } = new(
        Id: "aurora-flow-dark",
        DisplayName: "Aurora Flow",
        Description: "Dinamik kuzey ışığı gradyan vurguları, katmanlı renk alanları ve yüksek okunabilirlik.",
        Family: AuroraThemeFamily.AuroraFlow,
        ColorMode: AuroraColorMode.Dark,
        Colors: new ColorTokens(
            Canvas: "#0C0A1D",
            Surface1: "#151230",
            Surface2: "#1E1A42",
            Surface3: "#282356",
            SurfaceElevated: "#221D4A",
            TextPrimary: "#FAF8FF",
            TextSecondary: "#D6D1F5",
            TextDisabled: "#7870A8",
            BorderSubtle: "#37306B",
            BorderStrong: "#5B4FA8",
            AccentPrimary: "#A78BFA",
            AccentSecondary: "#2DD4BF",
            StateSuccess: "#34D399",
            StateWarning: "#FBBF24",
            StateDanger: "#FB7185",
            StateInfo: "#60A5FA",
            FocusRing: "#C4B5FD"),
        Typography: new TypographyTokens(),
        Spacing: new SpacingTokens(1.02),
        Radius: new RadiusTokens(1.12),
        Material: new MaterialTokens(AuroraBlurLevel.Strong, SurfaceOpacity: 0.84, ShadowIntensity: 0.50, AmbientGradientEnabled: true),
        Motion: new MotionTokens(130, 210, 300, ReducedMotion: false, PauseWhenHidden: true),
        IconSizeDip: 23.0);

    public static AuroraThemeTokens NebulaDark { get; } = new(
        Id: "nebula-dark",
        DisplayName: "Nebula",
        Description: "Derin uzay yüzeyleri, yalnızca odak ve aktif durumlar için ince neon siber konturlar.",
        Family: AuroraThemeFamily.Nebula,
        ColorMode: AuroraColorMode.Dark,
        Colors: new ColorTokens(
            Canvas: "#05070F",
            Surface1: "#0B1021",
            Surface2: "#111933",
            Surface3: "#182347",
            SurfaceElevated: "#141E3D",
            TextPrimary: "#F0F9FF",
            TextSecondary: "#BAE6FD",
            TextDisabled: "#526D8A",
            BorderSubtle: "#1D3259",
            BorderStrong: "#06B6D4",
            AccentPrimary: "#22D3EE",
            AccentSecondary: "#F472B6",
            StateSuccess: "#4ADE80",
            StateWarning: "#FACC15",
            StateDanger: "#F43F5E",
            StateInfo: "#38BDF8",
            FocusRing: "#67E8F9"),
        Typography: new TypographyTokens(),
        Spacing: new SpacingTokens(1.0),
        Radius: new RadiusTokens(0.90),
        Material: new MaterialTokens(AuroraBlurLevel.Standard, SurfaceOpacity: 0.90, ShadowIntensity: 0.55, AmbientGradientEnabled: true),
        Motion: new MotionTokens(115, 180, 260, ReducedMotion: false, PauseWhenHidden: true),
        IconSizeDip: 22.0);

    public static AuroraThemeTokens HighContrastSafe { get; } = new(
        Id: "aurora-high-contrast",
        DisplayName: "Yüksek Kontrast (Erişilebilirlik)",
        Description: "Dekoratif bulanıklık kapatılmış, maksimum WCAG AAA metin ve odak kontrastı.",
        Family: AuroraThemeFamily.ObsidianPro,
        ColorMode: AuroraColorMode.Dark,
        Colors: new ColorTokens(
            Canvas: "#000000",
            Surface1: "#0A0A0A",
            Surface2: "#141414",
            Surface3: "#1F1F1F",
            SurfaceElevated: "#141414",
            TextPrimary: "#FFFFFF",
            TextSecondary: "#EFEFEF",
            TextDisabled: "#8C8C8C",
            BorderSubtle: "#A0A0A0",
            BorderStrong: "#FFFFFF",
            AccentPrimary: "#00E5FF",
            AccentSecondary: "#FFFF00",
            StateSuccess: "#00FF7F",
            StateWarning: "#FFD700",
            StateDanger: "#FF5252",
            StateInfo: "#40C4FF",
            FocusRing: "#FFFF00"),
        Typography: new TypographyTokens(ScaleFactor: 1.05),
        Spacing: new SpacingTokens(1.05),
        Radius: new RadiusTokens(0.6),
        Material: new MaterialTokens(AuroraBlurLevel.None, SurfaceOpacity: 1.0, ShadowIntensity: 0.0, AmbientGradientEnabled: false),
        Motion: new MotionTokens(0, 0, 0, ReducedMotion: true, PauseWhenHidden: true),
        IconSizeDip: 24.0);

    public static IReadOnlyList<AuroraThemeTokens> All { get; } =
    [
        AuroraGlassDark,
        AuroraGlassLight,
        ObsidianProDark,
        AuroraFlowDark,
        NebulaDark,
        HighContrastSafe
    ];

    public static AuroraThemeTokens GetByIdOrDefault(string? id) =>
        All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)) ?? AuroraGlassDark;

    public static AuroraThemeTokens GetByFamily(AuroraThemeFamily family, AuroraColorMode mode = AuroraColorMode.Dark) =>
        family switch
        {
            AuroraThemeFamily.AuroraGlass => mode == AuroraColorMode.Light ? AuroraGlassLight : AuroraGlassDark,
            AuroraThemeFamily.ObsidianPro => ObsidianProDark,
            AuroraThemeFamily.AuroraFlow => AuroraFlowDark,
            AuroraThemeFamily.Nebula => NebulaDark,
            _ => AuroraGlassDark
        };
}
