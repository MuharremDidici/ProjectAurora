using Aurora.Application.Services;
using Aurora.Core.Contracts;
using Aurora.Core.Models;
using Aurora.Core.Primitives;
using Aurora.Presentation;

namespace Aurora.ControlCenter;

public sealed class ControlCenterViewModel(
    AuroraStateStore stateStore,
    IAudioEndpointService audioEndpointService,
    IAudioSessionService audioSessionService,
    INetworkStatusService networkService,
    IRadioService radioService,
    IDisplayService displayService,
    IPowerService powerService) : ObservableViewModel
{
    private double _masterVolumePercent = 65;
    private bool _isMasterMuted;
    private string _defaultAudioDeviceName = "Hoparlör";
    private IReadOnlyList<AudioEndpointInfo> _audioEndpoints = [];
    private IReadOnlyList<AudioSessionInfo> _audioSessions = [];
    private RadioStatusSnapshot _wifiStatus = new("Wi-Fi", ControlOperationalState.Off, CapabilityState.SupportedWithConsent, "Kontrol ediliyor...", "ms-settings:network-wifi");
    private RadioStatusSnapshot _bluetoothStatus = new("Bluetooth", ControlOperationalState.Off, CapabilityState.SupportedWithConsent, "Kontrol ediliyor...", "ms-settings:bluetooth");
    private RadioStatusSnapshot _airplaneStatus = new("Uçak Modu", ControlOperationalState.Off, CapabilityState.DeepLinkFallback, "Ayarlar Kısayolu", "ms-settings:network-airplanemode");
    private BrightnessSnapshot _brightness = new(false, 100, CapabilityState.Unavailable, "Denetleniyor");
    private BatterySnapshot _battery = new(false, 100, false, false, CapabilityState.Unavailable, "AC Güç");
    private string? _lastErrorBanner;
    private string? _lastFallbackUri;

    public ControlCenterDensity Density => stateStore.ActiveProfile.ControlCenter.Density;
    public IReadOnlyList<ControlCardConfig> OrderedVisibleCards =>
        stateStore.ActiveProfile.ControlCenter.Cards
            .Where(c => c.IsVisible)
            .OrderBy(c => c.OrderIndex)
            .ToList();

    public double MasterVolumePercent
    {
        get => _masterVolumePercent;
        private set => SetProperty(ref _masterVolumePercent, value);
    }

    public bool IsMasterMuted
    {
        get => _isMasterMuted;
        private set => SetProperty(ref _isMasterMuted, value);
    }

    public string DefaultAudioDeviceName
    {
        get => _defaultAudioDeviceName;
        private set => SetProperty(ref _defaultAudioDeviceName, value);
    }

    public IReadOnlyList<AudioEndpointInfo> AudioEndpoints
    {
        get => _audioEndpoints;
        private set => SetProperty(ref _audioEndpoints, value);
    }

    public IReadOnlyList<AudioSessionInfo> AudioSessions
    {
        get => _audioSessions;
        private set => SetProperty(ref _audioSessions, value);
    }

    public RadioStatusSnapshot WifiStatus
    {
        get => _wifiStatus;
        private set => SetProperty(ref _wifiStatus, value);
    }

    public RadioStatusSnapshot BluetoothStatus
    {
        get => _bluetoothStatus;
        private set => SetProperty(ref _bluetoothStatus, value);
    }

    public RadioStatusSnapshot AirplaneStatus
    {
        get => _airplaneStatus;
        private set => SetProperty(ref _airplaneStatus, value);
    }

    public BrightnessSnapshot Brightness
    {
        get => _brightness;
        private set => SetProperty(ref _brightness, value);
    }

    public BatterySnapshot Battery
    {
        get => _battery;
        private set => SetProperty(ref _battery, value);
    }

    public string? LastErrorBanner
    {
        get => _lastErrorBanner;
        private set => SetProperty(ref _lastErrorBanner, value);
    }

    public string? LastFallbackUri
    {
        get => _lastFallbackUri;
        private set => SetProperty(ref _lastFallbackUri, value);
    }

    public async Task RefreshAllAsync(CancellationToken ct = default)
    {
        var outRes = audioEndpointService.GetDefaultOutputState();
        if (outRes.IsSuccess)
        {
            MasterVolumePercent = Math.Round(outRes.Value.VolumeScalar * 100.0);
            IsMasterMuted = outRes.Value.IsMuted;
            DefaultAudioDeviceName = outRes.Value.DeviceName;
        }

        var eps = audioEndpointService.GetEndpoints(includeCapture: true);
        if (eps.IsSuccess && eps.Value is not null)
            AudioEndpoints = eps.Value;

        var sess = audioSessionService.GetActiveSessions();
        if (sess.IsSuccess && sess.Value is not null)
            AudioSessions = sess.Value;

        Brightness = displayService.GetBrightness();
        Battery = powerService.GetBatteryStatus();
        AirplaneStatus = radioService.GetAirplaneModeShortcutStatus();
        WifiStatus = await networkService.GetWifiAndNetworkStatusAsync(ct);
        BluetoothStatus = await radioService.GetBluetoothStatusAsync(ct);

        OnPropertyChanged(nameof(Density));
        OnPropertyChanged(nameof(OrderedVisibleCards));
    }

    public AuroraResult SetMasterVolumePercent(double percent)
    {
        var clamped = Math.Clamp(percent, 0.0, 100.0);
        var res = audioEndpointService.SetMasterVolume(clamped / 100.0);
        if (res.IsSuccess)
        {
            MasterVolumePercent = Math.Round(clamped);
            ClearError();
        }
        else
        {
            RecordFailure(res);
        }

        return res;
    }

    public AuroraResult ToggleMasterMute()
    {
        bool target = !IsMasterMuted;
        var res = audioEndpointService.SetMasterMute(target);
        if (res.IsSuccess)
        {
            IsMasterMuted = target;
            ClearError();
        }
        else
        {
            RecordFailure(res);
        }

        return res;
    }

    public AuroraResult SetAppSessionVolumePercent(uint processId, double percent)
    {
        var res = audioSessionService.SetSessionVolume(processId, Math.Clamp(percent, 0.0, 100.0) / 100.0);
        if (!res.IsSuccess)
            RecordFailure(res);
        return res;
    }

    public AuroraResult SetAppSessionMute(uint processId, bool muted)
    {
        var res = audioSessionService.SetSessionMute(processId, muted);
        if (res.IsSuccess)
        {
            var sess = audioSessionService.GetActiveSessions();
            if (sess.IsSuccess && sess.Value is not null)
                AudioSessions = sess.Value;
        }
        else
        {
            RecordFailure(res);
        }

        return res;
    }

    public async Task<AuroraResult> ToggleWifiAsync(CancellationToken ct = default)
    {
        bool desired = WifiStatus.OperationalState != ControlOperationalState.On;
        WifiStatus = WifiStatus with { OperationalState = ControlOperationalState.Pending, StatusText = "İşleniyor..." };

        var res = await radioService.TrySetRadioStateAsync("WiFi", desired, ct);
        WifiStatus = await networkService.GetWifiAndNetworkStatusAsync(ct);

        if (!res.IsSuccess)
            RecordFailure(res);
        else
            ClearError();

        return res;
    }

    public async Task<AuroraResult> ToggleBluetoothAsync(CancellationToken ct = default)
    {
        bool desired = BluetoothStatus.OperationalState != ControlOperationalState.On;
        BluetoothStatus = BluetoothStatus with { OperationalState = ControlOperationalState.Pending, StatusText = "İşleniyor..." };

        var res = await radioService.TrySetRadioStateAsync("Bluetooth", desired, ct);
        BluetoothStatus = await radioService.GetBluetoothStatusAsync(ct);

        if (!res.IsSuccess)
            RecordFailure(res);
        else
            ClearError();

        return res;
    }

    public AuroraResult SetBrightnessPercent(int percent)
    {
        var res = displayService.SetBrightness(percent);
        if (res.IsSuccess)
        {
            Brightness = Brightness with { BrightnessPercent = Math.Clamp(percent, 5, 100) };
            ClearError();
        }
        else
        {
            RecordFailure(res);
        }

        return res;
    }

    public async Task SetDensityAsync(ControlCenterDensity density, CancellationToken ct = default)
    {
        var updated = stateStore.ActiveProfile.ControlCenter with { Density = density };
        await stateStore.UpdateControlCenterSettingsAsync(updated, ct);
        OnPropertyChanged(nameof(Density));
    }

    public async Task MoveCardOrderAsync(ControlCardKind kind, int delta, CancellationToken ct = default)
    {
        var list = stateStore.ActiveProfile.ControlCenter.Cards.OrderBy(c => c.OrderIndex).ToList();
        var idx = list.FindIndex(c => c.Kind == kind);
        if (idx < 0)
            return;

        int newIdx = Math.Clamp(idx + delta, 0, list.Count - 1);
        var card = list[idx];
        list.RemoveAt(idx);
        list.Insert(newIdx, card);

        for (int i = 0; i < list.Count; i++)
            list[i] = list[i] with { OrderIndex = i };

        await stateStore.UpdateControlCenterSettingsAsync(stateStore.ActiveProfile.ControlCenter with { Cards = list }, ct);
        OnPropertyChanged(nameof(OrderedVisibleCards));
    }

    public async Task SetCardVisibilityAsync(ControlCardKind kind, bool visible, CancellationToken ct = default)
    {
        var list = stateStore.ActiveProfile.ControlCenter.Cards
            .Select(c => c.Kind == kind ? c with { IsVisible = visible } : c)
            .ToList();

        await stateStore.UpdateControlCenterSettingsAsync(stateStore.ActiveProfile.ControlCenter with { Cards = list }, ct);
        OnPropertyChanged(nameof(OrderedVisibleCards));
    }

    private void RecordFailure(AuroraResult res)
    {
        LastErrorBanner = res.Error?.Message ?? "İşlem desteklenmiyor.";
        LastFallbackUri = res.Error?.FallbackDeepLinkUri;
    }

    public void ClearError()
    {
        LastErrorBanner = null;
        LastFallbackUri = null;
    }
}
