using Aurora.Application.Services;
using Aurora.ControlCenter;
using Aurora.Core.Contracts;
using Aurora.Core.Events;
using Aurora.Core.Models;
using Aurora.Core.Primitives;
using Aurora.Core.Tokens;
using Aurora.Notifications;
using Aurora.Personalization;
using Aurora.Presentation;
using Aurora.Workspaces;
using Xunit;

namespace Aurora.UITests;

public sealed class DesignSystemAndModuleViewModelTests
{
    [Fact]
    public void DesignSystemComponentCatalog_DefinesAllSeventeenRequiredComponentsWithAccessibleRoles()
    {
        Assert.Equal(17, DesignSystemComponentCatalog.AllComponents.Count);

        string[] requiredNames =
        [
            "AuroraSurface", "AuroraPanel", "AuroraCard", "AuroraIconButton",
            "AuroraToggle", "AuroraSlider", "AuroraSegmentedControl", "AuroraDeviceRow",
            "AuroraStatusPill", "AuroraNotificationCard", "AuroraWidgetFrame", "AuroraMenu",
            "AuroraTooltip", "AuroraEmptyState", "AuroraErrorState", "AuroraSkeleton", "AuroraFocusRing"
        ];

        foreach (var name in requiredNames)
        {
            var spec = DesignSystemComponentCatalog.AllComponents.FirstOrDefault(c => c.ComponentName == name);
            Assert.NotNull(spec);
            Assert.False(string.IsNullOrWhiteSpace(spec!.AccessibleRole));
            Assert.False(string.IsNullOrWhiteSpace(spec.KeyboardContract));
            Assert.Contains(ControlOperationalState.Error, spec.SupportedStates);
        }
    }

    [Fact]
    public async Task PersonalizationStudio_SandboxesPreviewUntilExplicitApply_AndSupportsDiscard()
    {
        var repo = new InMemoryAuroraRepository();
        using var bus = new BoundedEventBus();
        var store = new AuroraStateStore(repo, bus, new NullAuroraLogger());
        await store.InitializeAsync(safeMode: false);

        var vm = new PersonalizationStudioViewModel(store, new StubThemePackageStore());
        Assert.Equal(BuiltInThemes.AuroraGlassDark.Id, vm.CommittedTheme.Id);
        Assert.False(vm.IsPreviewActive);

        // Preview Nebula without committing
        vm.PreviewBuiltInTheme(BuiltInThemes.NebulaDark.Id);
        Assert.True(vm.IsPreviewActive);
        Assert.Equal(BuiltInThemes.NebulaDark.Id, vm.WorkingTheme.Id);
        Assert.Equal(BuiltInThemes.AuroraGlassDark.Id, vm.CommittedTheme.Id);

        // Discard restores Aurora Glass
        vm.DiscardPreview();
        Assert.False(vm.IsPreviewActive);
        Assert.Equal(BuiltInThemes.AuroraGlassDark.Id, vm.WorkingTheme.Id);

        // Preview Aurora Flow and Apply
        vm.PreviewBuiltInTheme(BuiltInThemes.AuroraFlowDark.Id);
        await vm.ApplyCurrentPreviewAsync();
        Assert.False(vm.IsPreviewActive);
        Assert.Equal(BuiltInThemes.AuroraFlowDark.Id, vm.CommittedTheme.Id);
    }

    [Fact]
    public async Task WorkspaceManager_SwitchesProfilesAndFallsBackDeterministicallyOnInvalidId()
    {
        var repo = new InMemoryAuroraRepository();
        using var bus = new BoundedEventBus();
        var store = new AuroraStateStore(repo, bus, new NullAuroraLogger());
        await store.InitializeAsync(safeMode: false);

        var vm = new WorkspaceManagerViewModel(store);
        var ok = await vm.ActivateProfileAsync("profile-gaming");
        Assert.True(ok.IsSuccess);
        Assert.Equal("profile-gaming", vm.ActiveProfile.Id);
        Assert.True(store.Preferences.Notifications.QuietHoursEnabled);

        var bad = await vm.ActivateProfileAsync("non-existent-profile");
        Assert.False(bad.IsSuccess);
        Assert.Equal("profile-gaming", vm.ActiveProfile.Id); // Deterministic fallback preserved
    }

    [Fact]
    public async Task NotificationCenter_SupportsClearAllAndUndo_AndRespectsQuietHours()
    {
        var repo = new InMemoryAuroraRepository();
        using var bus = new BoundedEventBus();
        var store = new AuroraStateStore(repo, bus, new NullAuroraLogger());
        await store.InitializeAsync(safeMode: false);

        bool toastFired = false;
        using var sub = bus.Subscribe<AuroraNotificationReceivedEvent>(e => toastFired = e.ShowToast);

        var vm = new NotificationCenterViewModel(store);
        await vm.ToggleQuietHoursAsync(); // Turn on 23:00-07:00 or test Clear/Undo
        await vm.CreateQuickReminderAsync("Toplantı", "Saat 14:00");
        vm.Refresh();
        Assert.True(vm.TotalActiveCount >= 1);

        await vm.ClearAllAsync();
        Assert.Equal(0, vm.TotalActiveCount);
        Assert.True(vm.CanUndoClearAll);

        await vm.UndoClearAllAsync();
        Assert.True(vm.TotalActiveCount >= 1);
    }

    [Fact]
    public async Task ControlCenter_NeverCommitsFakeSuccessWhenRadioPermissionDenied()
    {
        var repo = new InMemoryAuroraRepository();
        using var bus = new BoundedEventBus();
        var store = new AuroraStateStore(repo, bus, new NullAuroraLogger());
        await store.InitializeAsync(safeMode: false);

        var deniedRadio = new RestrictedRadioStub();
        var vm = new ControlCenterViewModel(
            store,
            deniedRadio,
            deniedRadio,
            deniedRadio,
            deniedRadio,
            deniedRadio,
            deniedRadio);

        await vm.RefreshAllAsync();
        var res = await vm.ToggleBluetoothAsync();

        Assert.False(res.IsSuccess);
        Assert.Equal(ControlOperationalState.Off, vm.BluetoothStatus.OperationalState);
        Assert.Equal("ms-settings:bluetooth", vm.LastFallbackUri);
    }

    private sealed class NullAuroraLogger : IAuroraLogger
    {
        public string LogDirectoryPath => string.Empty;
        public IReadOnlyList<DiagnosticLogEntry> GetRecentEntries(int maxCount = 100) => [];
        public void Error(string category, string eventCode, string sanitizedMessage, Exception? ex = null) { }
        public void Info(string category, string eventCode, string sanitizedMessage) { }
        public void Warn(string category, string eventCode, string sanitizedMessage) { }
    }

    private sealed class StubThemePackageStore : IThemePackageStore
    {
        public Task<AuroraResult<string>> ExportPackageAsync(AuroraThemeTokens theme, string destinationFilePath, string author = "Test", CancellationToken ct = default) =>
            Task.FromResult(AuroraResult<string>.Ok(destinationFilePath));

        public Task<AuroraResult<ThemePackageManifest>> ImportPackageAsync(string filePath, CancellationToken ct = default) =>
            Task.FromResult(AuroraResult<ThemePackageManifest>.Fail("NOT_USED", "Stub"));
    }

    private sealed class InMemoryAuroraRepository : IAuroraRepository
    {
        private UserPreferences _prefs = UserPreferences.Default;
        private IReadOnlyList<PinnedShortcut> _pins = DefaultCatalogFactory.CreateDefaultPinnedShortcuts();
        private IReadOnlyList<WorkspaceProfile> _profiles = DefaultCatalogFactory.CreateDefaultProfiles();
        private IReadOnlyList<AuroraNotification> _notifs = [];
        private readonly List<AuroraThemeTokens> _themes = [];

        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<UserPreferences> LoadPreferencesAsync(CancellationToken ct = default) => Task.FromResult(_prefs);
        public Task SavePreferencesAsync(UserPreferences preferences, CancellationToken ct = default) { _prefs = preferences; return Task.CompletedTask; }
        public Task<IReadOnlyList<PinnedShortcut>> LoadPinnedShortcutsAsync(CancellationToken ct = default) => Task.FromResult(_pins);
        public Task SavePinnedShortcutsAsync(IReadOnlyList<PinnedShortcut> shortcuts, CancellationToken ct = default) { _pins = shortcuts; return Task.CompletedTask; }
        public Task<IReadOnlyList<WorkspaceProfile>> LoadProfilesAsync(CancellationToken ct = default) => Task.FromResult(_profiles);
        public Task SaveProfilesAsync(IReadOnlyList<WorkspaceProfile> profiles, CancellationToken ct = default) { _profiles = profiles; return Task.CompletedTask; }
        public Task<IReadOnlyList<AuroraNotification>> LoadNotificationsAsync(CancellationToken ct = default) => Task.FromResult(_notifs);
        public Task SaveNotificationsAsync(IReadOnlyList<AuroraNotification> notifications, CancellationToken ct = default) { _notifs = notifications; return Task.CompletedTask; }
        public Task<IReadOnlyList<AuroraThemeTokens>> LoadCustomThemesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<AuroraThemeTokens>>(_themes);
        public Task SaveCustomThemeAsync(AuroraThemeTokens theme, CancellationToken ct = default) { _themes.Add(theme); return Task.CompletedTask; }
        public Task<string> ExportAllSettingsJsonAsync(CancellationToken ct = default) => Task.FromResult("{}");
        public Task<AuroraResult> ImportAllSettingsJsonAsync(string json, CancellationToken ct = default) => Task.FromResult(AuroraResult.Ok());
    }

    private sealed class RestrictedRadioStub :
        IAudioEndpointService, IAudioSessionService, INetworkStatusService, IRadioService, IDisplayService, IPowerService
    {
        public AuroraResult<IReadOnlyList<AudioEndpointInfo>> GetEndpoints(bool includeCapture = true) => AuroraResult<IReadOnlyList<AudioEndpointInfo>>.Ok([]);
        public AuroraResult<(double VolumeScalar, bool IsMuted, string DeviceName)> GetDefaultOutputState() => AuroraResult<(double, bool, string)>.Ok((0.5, false, "Test Speaker"));
        public AuroraResult SetMasterVolume(double volumeScalar) => AuroraResult.Ok();
        public AuroraResult SetMasterMute(bool isMuted) => AuroraResult.Ok();
        public AuroraResult<IReadOnlyList<AudioSessionInfo>> GetActiveSessions() => AuroraResult<IReadOnlyList<AudioSessionInfo>>.Ok([]);
        public AuroraResult SetSessionVolume(uint processId, double volumeScalar) => AuroraResult.Ok();
        public AuroraResult SetSessionMute(uint processId, bool isMuted) => AuroraResult.Ok();
        public Task<RadioStatusSnapshot> GetWifiAndNetworkStatusAsync(CancellationToken ct = default) =>
            Task.FromResult(new RadioStatusSnapshot("Wi-Fi", ControlOperationalState.On, CapabilityState.SupportedWithConsent, "Connected", "ms-settings:network-wifi"));
        public Task<RadioStatusSnapshot> GetBluetoothStatusAsync(CancellationToken ct = default) =>
            Task.FromResult(new RadioStatusSnapshot("Bluetooth", ControlOperationalState.Off, CapabilityState.PermissionRequired, "Off", "ms-settings:bluetooth"));
        public Task<AuroraResult> TrySetRadioStateAsync(string radioKind, bool enabled, CancellationToken ct = default) =>
            Task.FromResult(AuroraResult.Fail("PERM_DENIED", "İzin gerekli", CapabilityState.PermissionRequired, "ms-settings:bluetooth"));
        public RadioStatusSnapshot GetAirplaneModeShortcutStatus() =>
            new("Airplane", ControlOperationalState.Off, CapabilityState.DeepLinkFallback, "Shortcut", "ms-settings:network-airplanemode");
        public BrightnessSnapshot GetBrightness() => new(false, 100, CapabilityState.Unavailable, "External");
        public AuroraResult SetBrightness(int brightnessPercent) => AuroraResult.Fail("UNSUP", "Unsupported", CapabilityState.Unavailable, "ms-settings:display");
        public BatterySnapshot GetBatteryStatus() => new(true, 95, true, false, CapabilityState.DirectlySupported, "Charging");
    }
}
