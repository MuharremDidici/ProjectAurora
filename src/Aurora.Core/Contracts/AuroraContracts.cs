using Aurora.Core.Models;
using Aurora.Core.Primitives;
using Aurora.Core.Tokens;

namespace Aurora.Core.Contracts;

public interface IWindowCatalog
{
    AuroraResult<IReadOnlyList<AppWindowEntry>> GetOpenWindows(IReadOnlySet<nint>? excludedHwnds = null);
    nint GetForegroundWindowHandle();
    byte[]? ResolveShortcutIconPng(string targetPath) => null;
}

public interface IWindowActivationService
{
    AuroraResult ActivateOrToggleWindow(nint hwnd);
    AuroraResult FocusWindow(nint hwnd) => ActivateOrToggleWindow(hwnd);
    AuroraResult MinimizeWindow(nint hwnd);
    AuroraResult RestoreWindow(nint hwnd);
    AuroraResult CloseWindow(nint hwnd) => AuroraResult.Ok();
    AuroraResult ArrangeWindow(nint hwnd, string mode) => AuroraResult.Ok();
    AuroraResult ToggleAlwaysOnTop(nint hwnd) => AuroraResult.Ok();
    AuroraResult LaunchShortcut(PinnedShortcut shortcut);
}

public interface IAppBarService : IDisposable
{
    NativeTaskbarStatus QueryNativeTaskbarStatus();
    AuroraResult<PhysicalPixelRect> RegisterOrUpdateBar(nint hwnd, TaskbarEdge edge, int thicknessPx, MonitorDescriptor monitor, bool reserveWorkArea);
    AuroraResult UnregisterBar(nint hwnd);
    void UnregisterAll();
}

public interface IAudioEndpointService
{
    AuroraResult<IReadOnlyList<AudioEndpointInfo>> GetEndpoints(bool includeCapture = true);
    AuroraResult<(double VolumeScalar, bool IsMuted, string DeviceName)> GetDefaultOutputState();
    AuroraResult SetMasterVolume(double volumeScalar);
    AuroraResult SetMasterMute(bool isMuted);
}

public interface IAudioSessionService
{
    AuroraResult<IReadOnlyList<AudioSessionInfo>> GetActiveSessions();
    AuroraResult SetSessionVolume(uint processId, double volumeScalar);
    AuroraResult SetSessionMute(uint processId, bool isMuted);
}

public interface INetworkStatusService
{
    Task<RadioStatusSnapshot> GetWifiAndNetworkStatusAsync(CancellationToken ct = default);
}

public interface IRadioService
{
    Task<RadioStatusSnapshot> GetBluetoothStatusAsync(CancellationToken ct = default);
    Task<AuroraResult> TrySetRadioStateAsync(string radioKind, bool enabled, CancellationToken ct = default);
    RadioStatusSnapshot GetAirplaneModeShortcutStatus();
}

public interface IDisplayService
{
    BrightnessSnapshot GetBrightness();
    AuroraResult SetBrightness(int brightnessPercent);
}

public interface IPowerService
{
    BatterySnapshot GetBatteryStatus();
}

public interface IMediaSessionService
{
    Task<AuroraResult<IReadOnlyList<MediaSessionSnapshot>>> GetActiveMediaSessionsAsync(CancellationToken ct = default);
    Task<AuroraResult> ExecuteTransportAsync(string sessionId, string command, CancellationToken ct = default);
}

public interface IMonitorTopologyService
{
    IReadOnlyList<MonitorDescriptor> GetMonitors();
    MonitorDescriptor GetPrimaryMonitor();
}

public interface ISystemMetricsService
{
    SystemMetricsSnapshot SampleMetrics();
    Task<WeatherSnapshot> GetWeatherAsync(string cityName, double latitude, double longitude, bool allowNetwork, CancellationToken ct = default);
}

public interface IStartupRegistrationService
{
    bool IsStartupEnabled();
    AuroraResult SetStartupEnabled(bool enabled, string executablePath);
}

public interface IAuroraRepository
{
    Task InitializeAsync(CancellationToken ct = default);
    Task<UserPreferences> LoadPreferencesAsync(CancellationToken ct = default);
    Task SavePreferencesAsync(UserPreferences preferences, CancellationToken ct = default);
    Task<IReadOnlyList<PinnedShortcut>> LoadPinnedShortcutsAsync(CancellationToken ct = default);
    Task SavePinnedShortcutsAsync(IReadOnlyList<PinnedShortcut> shortcuts, CancellationToken ct = default);
    Task<IReadOnlyList<WorkspaceProfile>> LoadProfilesAsync(CancellationToken ct = default);
    Task SaveProfilesAsync(IReadOnlyList<WorkspaceProfile> profiles, CancellationToken ct = default);
    Task<IReadOnlyList<AuroraNotification>> LoadNotificationsAsync(CancellationToken ct = default);
    Task SaveNotificationsAsync(IReadOnlyList<AuroraNotification> notifications, CancellationToken ct = default);
    Task<IReadOnlyList<AuroraThemeTokens>> LoadCustomThemesAsync(CancellationToken ct = default);
    Task SaveCustomThemeAsync(AuroraThemeTokens theme, CancellationToken ct = default);
    Task<string> ExportAllSettingsJsonAsync(CancellationToken ct = default);
    Task<AuroraResult> ImportAllSettingsJsonAsync(string json, CancellationToken ct = default);
}

public interface IThemePackageStore
{
    Task<AuroraResult<ThemePackageManifest>> ImportPackageAsync(string filePath, CancellationToken ct = default);
    Task<AuroraResult<string>> ExportPackageAsync(AuroraThemeTokens theme, string destinationFilePath, string author = "Aurora User", CancellationToken ct = default);
}

public sealed record DiagnosticLogEntry(
    DateTimeOffset TimestampUtc,
    string Level,
    string Category,
    string EventCode,
    string SanitizedMessage);

public interface IAuroraLogger
{
    void Info(string category, string eventCode, string sanitizedMessage);
    void Warn(string category, string eventCode, string sanitizedMessage);
    void Error(string category, string eventCode, string sanitizedMessage, Exception? ex = null);
    IReadOnlyList<DiagnosticLogEntry> GetRecentEntries(int maxCount = 100);
    string LogDirectoryPath { get; }
}
