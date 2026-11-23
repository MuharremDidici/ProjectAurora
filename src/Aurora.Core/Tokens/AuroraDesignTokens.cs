using System.Globalization;

namespace Aurora.Core.Tokens;

public enum AuroraThemeFamily
{
    AuroraGlass,
    ObsidianPro,
    AuroraFlow,
    Nebula,
    MonoMatrix
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
    double IconSizeDip = 22.0,
    string VisualStyle = "liquid-glass");

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
        DisplayName: "Liquid Glass 26",
        Description: "Optik katmanlı buzlu cam, üst kırılma kenarlığı ve akıcı kapsül formu.",
        Family: AuroraThemeFamily.AuroraGlass,
        ColorMode: AuroraColorMode.Dark,
        Colors: new ColorTokens(
            Canvas: "#0A0F1D",
            Surface1: "#0F172A",
            Surface2: "#162038",
            Surface3: "#1E2C4A",
            SurfaceElevated: "#1A2744",
            TextPrimary: "#F8FAFC",
            TextSecondary: "#B6C4D8",
            TextDisabled: "#64748B",
            BorderSubtle: "#2A3A5C",
            BorderStrong: "#47608E",
            AccentPrimary: "#38BDF8",
            AccentSecondary: "#818CF8",
            StateSuccess: "#34D399",
            StateWarning: "#FBBF24",
            StateDanger: "#FB7185",
            StateInfo: "#60A5FA",
            FocusRing: "#7DD3FC"),
        Typography: new TypographyTokens(),
        Spacing: new SpacingTokens(1.0),
        Radius: new RadiusTokens(1.05),
        Material: new MaterialTokens(AuroraBlurLevel.Standard, SurfaceOpacity: 0.90, ShadowIntensity: 0.45, AmbientGradientEnabled: false),
        Motion: new MotionTokens(120, 190, 260, ReducedMotion: false, PauseWhenHidden: true),
        IconSizeDip: 22.0,
        VisualStyle: "liquid-glass");

    public static AuroraThemeTokens AuroraGlassLight { get; } = AuroraGlassDark with
    {
        Id = "aurora-glass-light",
        DisplayName = "Bento Silk (Açık)",
        Description = "Sıcak porselen açık yüzeyler, yüzen üst ada ve İskandinav yumuşak derinlik.",
        ColorMode = AuroraColorMode.Light,
        Colors = new ColorTokens(
            Canvas: "#F2F0EB",
            Surface1: "#FAF9F6",
            Surface2: "#F0EEE9",
            Surface3: "#E5E2DB",
            SurfaceElevated: "#FFFFFF",
            TextPrimary: "#14161A",
            TextSecondary: "#4A515E",
            TextDisabled: "#9098A6",
            BorderSubtle: "#D6D2C8",
            BorderStrong: "#B5AFA3",
            AccentPrimary: "#2563EB",
            AccentSecondary: "#0EA5E9",
            StateSuccess: "#059669",
            StateWarning: "#D97706",
            StateDanger: "#E11D48",
            StateInfo: "#2563EB",
            FocusRing: "#2563EB"),
        Material = new MaterialTokens(AuroraBlurLevel.Standard, SurfaceOpacity: 0.94, ShadowIntensity: 0.22, AmbientGradientEnabled: false),
        VisualStyle = "bento-silk"
    };

    public static AuroraThemeTokens ObsidianProDark { get; } = new(
        Id: "obsidian-pro-dark",
        DisplayName: "Linear Precision",
        Description: "Mat karbon yüzeyler, jilet keskinliğinde 1px çizgiler ve sıfır gürültülü profesyonel düzen.",
        Family: AuroraThemeFamily.ObsidianPro,
        ColorMode: AuroraColorMode.Dark,
        Colors: new ColorTokens(
            Canvas: "#090A0D",
            Surface1: "#101217",
            Surface2: "#171A21",
            Surface3: "#202530",
            SurfaceElevated: "#1A1E27",
            TextPrimary: "#F4F6F9",
            TextSecondary: "#A3AEBE",
            TextDisabled: "#636D7E",
            BorderSubtle: "#262C38",
            BorderStrong: "#3F495C",
            AccentPrimary: "#10B981",
            AccentSecondary: "#34D399",
            StateSuccess: "#22C55E",
            StateWarning: "#F59E0B",
            StateDanger: "#EF4444",
            StateInfo: "#3B82F6",
            FocusRing: "#34D399"),
        Typography: new TypographyTokens(ScaleFactor: 0.96),
        Spacing: new SpacingTokens(0.92),
        Radius: new RadiusTokens(0.72),
        Material: new MaterialTokens(AuroraBlurLevel.Subtle, SurfaceOpacity: 0.96, ShadowIntensity: 0.28, AmbientGradientEnabled: false),
        Motion: new MotionTokens(100, 150, 200, ReducedMotion: false, PauseWhenHidden: true),
        IconSizeDip: 20.0,
        VisualStyle: "linear-precision");

    public static AuroraThemeTokens MonoMatrixDark { get; } = new(
        Id: "mono-matrix-dark",
        DisplayName: "Mono Matrix",
        Description: "Endüstriyel dot-matrix doku, monospace telemetri ve dairesel sinyal LED göstergeleri.",
        Family: AuroraThemeFamily.MonoMatrix,
        ColorMode: AuroraColorMode.Dark,
        Colors: new ColorTokens(
            Canvas: "#08080A",
            Surface1: "#0E0E11",
            Surface2: "#16161A",
            Surface3: "#202026",
            SurfaceElevated: "#1A1A20",
            TextPrimary: "#F5F5F7",
            TextSecondary: "#A8A8B3",
            TextDisabled: "#63636E",
            BorderSubtle: "#282830",
            BorderStrong: "#484854",
            AccentPrimary: "#FF453A",
            AccentSecondary: "#FF9F0A",
            StateSuccess: "#30D158",
            StateWarning: "#FF9F0A",
            StateDanger: "#FF453A",
            StateInfo: "#64D2FF",
            FocusRing: "#FF6961"),
        Typography: new TypographyTokens(ScaleFactor: 0.98),
        Spacing: new SpacingTokens(0.96),
        Radius: new RadiusTokens(0.78),
        Material: new MaterialTokens(AuroraBlurLevel.Subtle, SurfaceOpacity: 0.97, ShadowIntensity: 0.35, AmbientGradientEnabled: false),
        Motion: new MotionTokens(95, 150, 210, ReducedMotion: false, PauseWhenHidden: true),
        IconSizeDip: 21.0,
        VisualStyle: "mono-matrix");

    public static AuroraThemeTokens AuroraFlowDark { get; } = new(
        Id: "aurora-flow-dark",
        DisplayName: "Spatial Vision",
        Description: "Tam kapsül uzamsal cam, dairesel ikon haleleri ve havada süzülen dinamik üst ada.",
        Family: AuroraThemeFamily.AuroraFlow,
        ColorMode: AuroraColorMode.Dark,
        Colors: new ColorTokens(
            Canvas: "#0D0E1B",
            Surface1: "#14162A",
            Surface2: "#1D203C",
            Surface3: "#272B4E",
            SurfaceElevated: "#212545",
            TextPrimary: "#FAF9FF",
            TextSecondary: "#C5C2E2",
            TextDisabled: "#76729E",
            BorderSubtle: "#343866",
            BorderStrong: "#565C9E",
            AccentPrimary: "#8B5CF6",
            AccentSecondary: "#2DD4BF",
            StateSuccess: "#34D399",
            StateWarning: "#FBBF24",
            StateDanger: "#FB7185",
            StateInfo: "#60A5FA",
            FocusRing: "#A78BFA"),
        Typography: new TypographyTokens(),
        Spacing: new SpacingTokens(1.02),
        Radius: new RadiusTokens(1.25),
        Material: new MaterialTokens(AuroraBlurLevel.Strong, SurfaceOpacity: 0.88, ShadowIntensity: 0.48, AmbientGradientEnabled: true),
        Motion: new MotionTokens(130, 200, 280, ReducedMotion: false, PauseWhenHidden: true),
        IconSizeDip: 22.0,
        VisualStyle: "spatial-vision");

    public static AuroraThemeTokens NebulaDark { get; } = new(
        Id: "nebula-dark",
        DisplayName: "Cyber Prism",
        Description: "Derin OLED siyah yüzey, kenar aydınlatmalı ince ışık şeridi ve keskin teknik HUD hatları.",
        Family: AuroraThemeFamily.Nebula,
        ColorMode: AuroraColorMode.Dark,
        Colors: new ColorTokens(
            Canvas: "#05070E",
            Surface1: "#090E1C",
            Surface2: "#10182E",
            Surface3: "#182444",
            SurfaceElevated: "#131D38",
            TextPrimary: "#F0F9FF",
            TextSecondary: "#A5CBE2",
            TextDisabled: "#526D8A",
            BorderSubtle: "#1D3259",
            BorderStrong: "#06B6D4",
            AccentPrimary: "#06B6D4",
            AccentSecondary: "#F43F5E",
            StateSuccess: "#4ADE80",
            StateWarning: "#FACC15",
            StateDanger: "#F43F5E",
            StateInfo: "#38BDF8",
            FocusRing: "#22D3EE"),
        Typography: new TypographyTokens(),
        Spacing: new SpacingTokens(1.0),
        Radius: new RadiusTokens(0.70),
        Material: new MaterialTokens(AuroraBlurLevel.Standard, SurfaceOpacity: 0.93, ShadowIntensity: 0.52, AmbientGradientEnabled: true),
        Motion: new MotionTokens(110, 175, 240, ReducedMotion: false, PauseWhenHidden: true),
        IconSizeDip: 22.0,
        VisualStyle: "cyber-prism");

    public static AuroraThemeTokens HighContrastSafe { get; } = new(
        Id: "aurora-high-contrast",
        DisplayName: "Yüksek Kontrast",
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
        IconSizeDip: 24.0,
        VisualStyle: "high-contrast");

    public static IReadOnlyList<AuroraThemeTokens> All { get; } =
    [
        AuroraGlassDark,
        AuroraGlassLight,
        ObsidianProDark,
        MonoMatrixDark,
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
            AuroraThemeFamily.MonoMatrix => MonoMatrixDark,
            AuroraThemeFamily.AuroraFlow => AuroraFlowDark,
            AuroraThemeFamily.Nebula => NebulaDark,
            _ => AuroraGlassDark
        };
}

public static class ThemeCustomizationHelper
{
    public static AuroraThemeTokens ApplyOverrides(
        AuroraThemeTokens baseTheme,
        string? accentPrimary = null,
        string? accentSecondary = null,
        string? surface1 = null,
        string? surface2 = null,
        string? textPrimary = null,
        double? surfaceOpacity = null)
    {
        var c = baseTheme.Colors;
        string effAccent1 = ContrastCalculator.IsValidHexColor(accentPrimary) ? accentPrimary!.Trim().ToUpperInvariant() : c.AccentPrimary;
        string effAccent2 = ContrastCalculator.IsValidHexColor(accentSecondary) ? accentSecondary!.Trim().ToUpperInvariant() : c.AccentSecondary;
        string effSurf1 = ContrastCalculator.IsValidHexColor(surface1) ? surface1!.Trim().ToUpperInvariant() : c.Surface1;
        string effSurf2 = ContrastCalculator.IsValidHexColor(surface2) ? surface2!.Trim().ToUpperInvariant() : c.Surface2;
        string effText1 = ContrastCalculator.IsValidHexColor(textPrimary) ? textPrimary!.Trim().ToUpperInvariant() : c.TextPrimary;

        // Auto-detect effective light/dark color mode from customized Surface1 luminance
        double surfLum = ContrastCalculator.RelativeLuminance(effSurf1);
        var effMode = surfLum >= 0.42 ? AuroraColorMode.Light : AuroraColorMode.Dark;

        // Derive harmonious secondary text if Surface1 changed dramatically or textPrimary changed
        string effText2 = c.TextSecondary;
        if (effMode != baseTheme.ColorMode || !string.Equals(effText1, c.TextPrimary, StringComparison.OrdinalIgnoreCase))
        {
            effText2 = BlendHex(effText1, effSurf1, 0.34);
        }

        double effOpacity = surfaceOpacity.HasValue
            ? Math.Round(Math.Clamp(surfaceOpacity.Value, 0.55, 1.0), 2)
            : baseTheme.Material.SurfaceOpacity;

        return baseTheme with
        {
            ColorMode = effMode,
            Colors = c with
            {
                Canvas = effSurf1,
                Surface1 = effSurf1,
                Surface2 = effSurf2,
                SurfaceElevated = effSurf2,
                TextPrimary = effText1,
                TextSecondary = effText2,
                AccentPrimary = effAccent1,
                AccentSecondary = effAccent2,
                FocusRing = effAccent1
            },
            Material = baseTheme.Material with { SurfaceOpacity = effOpacity }
        };
    }

    private static string BlendHex(string fgHex, string bgHex, double bgWeight)
    {
        var (_, fr, fg, fb) = ContrastCalculator.ParseHexColor(fgHex);
        var (_, br, bg, bb) = ContrastCalculator.ParseHexColor(bgHex);
        double w = Math.Clamp(bgWeight, 0.0, 1.0);
        byte r = (byte)Math.Round(fr * (1 - w) + br * w);
        byte g = (byte)Math.Round(fg * (1 - w) + bg * w);
        byte b = (byte)Math.Round(fb * (1 - w) + bb * w);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
