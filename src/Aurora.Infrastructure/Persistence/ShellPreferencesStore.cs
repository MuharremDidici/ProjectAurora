using System.Text.Json;

namespace Aurora.Infrastructure.Persistence;

public sealed record ThemeColorCustomization(
    string? AccentPrimary = null,
    string? AccentSecondary = null,
    string? Surface1 = null,
    string? Surface2 = null,
    string? TextPrimary = null,
    double? SurfaceOpacity = null)
{
    public bool HasAnyOverride =>
        !string.IsNullOrWhiteSpace(AccentPrimary) ||
        !string.IsNullOrWhiteSpace(AccentSecondary) ||
        !string.IsNullOrWhiteSpace(Surface1) ||
        !string.IsNullOrWhiteSpace(Surface2) ||
        !string.IsNullOrWhiteSpace(TextPrimary) ||
        SurfaceOpacity.HasValue;
}

public sealed record ShellRuntimePreferences(
    bool SmartTidyEnabled = true,
    bool DockAutoHide = false,
    bool PerMonitorWindowFilter = false,
    int DockMagnificationPercent = 48,
    bool CrownPinned = false,
    bool HideNativeTaskbarWhileActive = true,
    bool DwmLivePreviewEnabled = true,
    bool SystemNotificationBridgeEnabled = true,
    bool WinKeyOpensOmniSearch = false,
    string OmniHotkeyPreset = "Alt+Space",
    string? DockVisibilityMode = null,
    List<string>? DockFolderPaths = null,
    bool OnboardingCompleted = false,
    string DockDensity = "Standard",
    bool ShowClockSeconds = false,
    bool SnapZonesEnabled = true,
    string SnapZonePreset = "code6535",
    string UpdateManifestUrl = "",
    bool AutoCheckUpdatesEnabled = true,
    string DockEdge = "Bottom",
    bool HardwareOsdEnabled = true,
    bool AltDragEnabled = true,
    int DockScalePercent = 100,
    int DockOffsetPercent = 0,
    bool ClipboardAutoPasteEnabled = false,
    bool? ReserveDockWorkArea = null,
    string DockBarLayout = "Centered",
    Dictionary<string, ThemeColorCustomization>? ThemeCustomizations = null)
{
    public string EffectiveDockMode =>
        DockVisibilityMode is "AlwaysVisible" or "SmartDodge" or "AutoHide"
            ? DockVisibilityMode
            : (DockAutoHide ? "AutoHide" : "AlwaysVisible");

    public bool EffectiveReserveDockWorkAreaPreference => ReserveDockWorkArea ?? true;

    /// <summary>
    /// WorkArea is reserved ONLY when pinned as Fixed Bar ("AlwaysVisible").
    /// In Intellihide ("SmartDodge") and "AutoHide" modes, windows ALWAYS reclaim 100% screen height
    /// with zero wallpaper strip while the floating Crystal Pill glides away cleanly.
    /// </summary>
    public bool EffectiveReserveDockWorkArea =>
        EffectiveReserveDockWorkAreaPreference &&
        string.Equals(EffectiveDockMode, "AlwaysVisible", StringComparison.OrdinalIgnoreCase);

    public string EffectiveDockBarLayout =>
        DockBarLayout is "Centered" or "Split3Zone"
            ? DockBarLayout
            : "Centered";

    public string EffectiveDockDensity =>
        DockDensity is "Compact" or "Standard" or "Cozy"
            ? DockDensity
            : "Standard";

    public string EffectiveDockEdge =>
        DockEdge is "Bottom" or "Left" or "Right"
            ? DockEdge
            : "Bottom";

    public int EffectiveDockScalePercent => Math.Clamp(DockScalePercent <= 0 ? 100 : DockScalePercent, 75, 135);

    public int EffectiveDockOffsetPercent => Math.Clamp(DockOffsetPercent, -100, 100);

    public string EffectiveSnapZonePreset =>
        SnapZonePreset is "code6535" or "studio3" or "cols3" or "grid2x2"
            ? SnapZonePreset
            : "code6535";

    public ThemeColorCustomization? GetThemeCustomization(string? themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId) || ThemeCustomizations is null)
            return null;

        foreach (var kv in ThemeCustomizations)
        {
            if (string.Equals(kv.Key, themeId, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }
        return null;
    }
}

/// <summary>
/// Atomic JSON persistence for Aurora shell UI/ergonomics preferences
/// (dock auto-hide, multi-monitor window filter, droplet pin state, smart tidy,
/// magnification, and native Windows taskbar suppression).
/// </summary>
public sealed class ShellPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;

    public ShellPreferencesStore(string? customFilePath = null)
    {
        if (!string.IsNullOrWhiteSpace(customFilePath))
        {
            _filePath = customFilePath;
        }
        else
        {
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProjectAurora",
                "data");
            Directory.CreateDirectory(dataDir);
            _filePath = Path.Combine(dataDir, "shell_settings.json");
        }
    }

    public string FilePath => _filePath;

    public ShellRuntimePreferences Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new ShellRuntimePreferences();

            var json = File.ReadAllText(_filePath);
            var parsed = JsonSerializer.Deserialize<ShellRuntimePreferences>(json, JsonOptions);
            if (parsed is null)
                return new ShellRuntimePreferences();

            return parsed with
            {
                DockMagnificationPercent = Math.Clamp(parsed.DockMagnificationPercent, 0, 80),
                DockScalePercent = Math.Clamp(parsed.DockScalePercent <= 0 ? 100 : parsed.DockScalePercent, 75, 135),
                DockOffsetPercent = Math.Clamp(parsed.DockOffsetPercent, -100, 100)
            };
        }
        catch
        {
            return new ShellRuntimePreferences();
        }
    }

    public void Save(ShellRuntimePreferences prefs)
    {
        try
        {
            var normalized = prefs with
            {
                DockMagnificationPercent = Math.Clamp(prefs.DockMagnificationPercent, 0, 80),
                DockScalePercent = Math.Clamp(prefs.DockScalePercent <= 0 ? 100 : prefs.DockScalePercent, 75, 135),
                DockOffsetPercent = Math.Clamp(prefs.DockOffsetPercent, -100, 100)
            };

            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var tmpPath = _filePath + ".tmp";
            File.WriteAllText(tmpPath, JsonSerializer.Serialize(normalized, JsonOptions));
            File.Move(tmpPath, _filePath, overwrite: true);
        }
        catch
        {
            // Never crash UI loop on transient storage contention
        }
    }
}
