using Aurora.Core.Primitives;
using Aurora.Core.Tokens;

namespace Aurora.Core.Models;

public enum TaskbarEdge
{
    Bottom = 3,
    Top = 1,
    Left = 0,
    Right = 2
}

public enum TaskbarAlignment
{
    Center,
    Left
}

public enum TaskbarDensity
{
    Compact,
    Standard,
    Spacious
}

public enum TaskbarLayoutMode
{
    DynamicIslands = 0,
    CompactCenteredDock = 1,
    FullWidthStrip = 2
}

public enum NativeTaskbarVisibilityState
{
    Unknown,
    AlwaysVisible,
    AutoHideEnabled
}

public sealed record NativeTaskbarStatus(
    NativeTaskbarVisibilityState Visibility,
    TaskbarEdge Edge,
    PhysicalPixelRect BoundsPx,
    bool CoexistenceActive,
    string GuidanceMessage);

public sealed record PinnedShortcut(
    string Id,
    string DisplayName,
    string TargetPath,
    string? Arguments,
    string IconGlyph,
    int OrderIndex);

public sealed record AppWindowEntry(
    nint Hwnd,
    uint ProcessId,
    string ProcessName,
    string ExecutablePath,
    string Title,
    bool IsActive,
    bool IsMinimized,
    bool IsResponding,
    string MonitorDeviceId,
    byte[]? IconPngBytes = null,
    bool IsTopMost = false);

public sealed record AppWindowGroup(
    string GroupKey,
    string DisplayName,
    string ExecutablePath,
    string IconGlyph,
    bool IsPinned,
    int OrderIndex,
    IReadOnlyList<AppWindowEntry> Windows,
    byte[]? IconPngBytes = null)
{
    public bool IsRunning => Windows.Count > 0;
    public bool HasActiveWindow => Windows.Any(w => w.IsActive);
    public int WindowCount => Windows.Count;
}

public sealed record TaskbarSettings(
    TaskbarEdge Edge = TaskbarEdge.Bottom,
    TaskbarAlignment Alignment = TaskbarAlignment.Center,
    TaskbarDensity Density = TaskbarDensity.Standard,
    bool AutoHide = false,
    int AutoHideRevealZoneDip = 6,
    bool GroupWindowsByApp = true,
    bool ShowWindowPreviews = true,
    bool ShowOnAllMonitors = true,
    bool FloatingDockStyle = true,
    TaskbarLayoutMode LayoutMode = TaskbarLayoutMode.DynamicIslands,
    bool ShowActiveAppLabel = true)
{
    public double BarThicknessDip => Density switch
    {
        TaskbarDensity.Compact => 48.0,
        TaskbarDensity.Spacious => 64.0,
        _ => 56.0
    };
}

public enum ControlCenterDensity
{
    Compact,
    Standard,
    Expanded
}

public enum ControlCardKind
{
    Connectivity,
    MasterAudio,
    AppAudioMixer,
    Brightness,
    BatteryPower,
    MediaSummary,
    WorkspaceQuickSwitch
}

public sealed record ControlCardConfig(
    ControlCardKind Kind,
    string Title,
    bool IsVisible,
    int OrderIndex);

public sealed record ControlCenterSettings(
    ControlCenterDensity Density,
    IReadOnlyList<ControlCardConfig> Cards)
{
    public static ControlCenterSettings Default { get; } = new(
        ControlCenterDensity.Standard,
        [
            new(ControlCardKind.Connectivity, "Bağlantı ve Radyolar", true, 0),
            new(ControlCardKind.MasterAudio, "Ana Ses ve Çıkış Aygıtları", true, 1),
            new(ControlCardKind.Brightness, "Ekran Parlaklığı", true, 2),
            new(ControlCardKind.AppAudioMixer, "Uygulama Bazlı Ses Karıştırıcı", true, 3),
            new(ControlCardKind.MediaSummary, "Medya Oturumu", true, 4),
            new(ControlCardKind.BatteryPower, "Pil ve Güç Durumu", true, 5),
            new(ControlCardKind.WorkspaceQuickSwitch, "Çalışma Alanı Profili", true, 6)
        ]);
}

public sealed record AudioEndpointInfo(
    string Id,
    string FriendlyName,
    bool IsCapture,
    bool IsDefault,
    double VolumeScalar,
    bool IsMuted);

public sealed record AudioSessionInfo(
    string SessionIdentifier,
    uint ProcessId,
    string DisplayName,
    double VolumeScalar,
    bool IsMuted,
    bool IsActive);

public sealed record RadioStatusSnapshot(
    string Name,
    ControlOperationalState OperationalState,
    CapabilityState Capability,
    string StatusText,
    string SettingsDeepLinkUri);

public sealed record BatterySnapshot(
    bool HasBattery,
    int ChargePercent,
    bool IsCharging,
    bool IsBatterySaverActive,
    CapabilityState Capability,
    string StatusText);

public sealed record BrightnessSnapshot(
    bool IsSupported,
    int BrightnessPercent,
    CapabilityState Capability,
    string DisplayTypeLabel,
    string FallbackSettingsUri = "ms-settings:display");

public sealed record MediaSessionSnapshot(
    string SessionId,
    string SourceAppId,
    string Title,
    string Artist,
    string AlbumTitle,
    bool IsPlaying,
    bool CanPlay,
    bool CanPause,
    bool CanGoNext,
    bool CanGoPrevious,
    byte[]? ArtworkBytes);

public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Error
}

public enum NotificationCategory
{
    System,
    Audio,
    Workspace,
    Reminder,
    Security,
    Recovery
}

public sealed record AuroraNotification(
    string Id,
    string SourceModule,
    NotificationCategory Category,
    NotificationSeverity Severity,
    string Title,
    string Body,
    DateTimeOffset CreatedAtUtc,
    bool IsRead = false,
    bool IsDismissed = false,
    string? ActionLabel = null,
    string? ActionDeepLink = null);

public sealed record NotificationSettings(
    bool Enabled = true,
    bool QuietHoursEnabled = false,
    int QuietHoursStartHour = 23,
    int QuietHoursEndHour = 7,
    int RetentionDays = 14,
    int MaxStoredCount = 200);

public enum WidgetKind
{
    ClockDate,
    Calendar,
    CpuRamMetrics,
    GpuMetrics,
    NetworkThroughput,
    Weather
}

public enum WidgetSizeClass
{
    Small,
    Medium,
    Large
}

public sealed record WidgetInstanceConfig(
    string Id,
    WidgetKind Kind,
    string Title,
    WidgetSizeClass Size,
    bool IsVisible,
    int OrderIndex,
    string MonitorDeviceId = "PRIMARY");

public sealed record SystemMetricsSnapshot(
    double CpuUsagePercent,
    double RamUsagePercent,
    double RamUsedGb,
    double RamTotalGb,
    double GpuUsagePercent,
    bool IsGpuAvailable,
    string GpuAdapterName,
    double NetworkDownloadKbps,
    double NetworkUploadKbps,
    DateTimeOffset SampledAtUtc);

public sealed record WeatherSnapshot(
    bool IsLiveNetworkData,
    string LocationName,
    double TemperatureCelsius,
    string ConditionSummary,
    int HumidityPercent,
    double WindKmh,
    string DataSourceDisclosure,
    DateTimeOffset UpdatedAtUtc);

public sealed record WorkspaceProfile(
    string Id,
    string Name,
    string IconGlyph,
    string Description,
    string ThemeId,
    TaskbarSettings Taskbar,
    ControlCenterSettings ControlCenter,
    IReadOnlyList<WidgetInstanceConfig> Widgets,
    bool EnableQuietHours);

public sealed record ThemePackageManifest(
    string SchemaVersion,
    string PackageId,
    string Name,
    string Author,
    string Version,
    AuroraThemeTokens Theme,
    IReadOnlyList<string> ApprovedAssets);

public sealed record GlobalShortcutBinding(
    string ActionId,
    string DisplayName,
    string Modifiers,
    string Key,
    bool Enabled = true)
{
    public string DisplayGesture => $"{Modifiers}+{Key}";
}

public sealed record UserPreferences(
    bool OnboardingCompleted,
    string ActiveProfileId,
    string ActiveThemeId,
    bool StartWithWindows,
    bool RespectSystemReducedMotion,
    bool LowPowerEffectsOnBattery,
    bool WeatherNetworkOptIn,
    string WeatherCityName,
    double WeatherLatitude,
    double WeatherLongitude,
    NotificationSettings Notifications,
    IReadOnlyList<GlobalShortcutBinding> Shortcuts)
{
    public static UserPreferences Default { get; } = new(
        OnboardingCompleted: false,
        ActiveProfileId: "profile-work",
        ActiveThemeId: BuiltInThemes.AuroraGlassDark.Id,
        StartWithWindows: false,
        RespectSystemReducedMotion: true,
        LowPowerEffectsOnBattery: true,
        WeatherNetworkOptIn: false,
        WeatherCityName: "İstanbul",
        WeatherLatitude: 41.0082,
        WeatherLongitude: 28.9784,
        Notifications: new NotificationSettings(),
        Shortcuts:
        [
            new("toggle-control-center", "Kontrol Merkezini Aç / Kapat", "Ctrl+Alt", "A"),
            new("toggle-notifications", "Bildirim Merkezini Aç / Kapat", "Ctrl+Alt", "N"),
            new("toggle-widgets", "Widget Panelini Aç / Kapat", "Ctrl+Alt", "W"),
            new("open-studio", "Aurora Stüdyo ve Ayarları Aç", "Ctrl+Alt", "S")
        ]);
}
