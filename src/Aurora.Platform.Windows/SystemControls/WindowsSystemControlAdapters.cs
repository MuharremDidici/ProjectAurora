using System.Management;
using Aurora.Core.Contracts;
using Aurora.Core.Models;
using Aurora.Core.Primitives;
using Aurora.Platform.Windows.Win32;
using Microsoft.Win32;
using Windows.Devices.Radios;
using Windows.Networking.Connectivity;

namespace Aurora.Platform.Windows.SystemControls;

public sealed class WindowsRadioAndNetworkService(IAuroraLogger logger) : INetworkStatusService, IRadioService
{
    public async Task<RadioStatusSnapshot> GetWifiAndNetworkStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            bool hasInternet = profile?.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess;

            var radios = await Radio.GetRadiosAsync();
            var wifiRadio = radios.FirstOrDefault(r => r.Kind == RadioKind.WiFi);

            if (wifiRadio is not null)
            {
                bool isOn = wifiRadio.State == RadioState.On;
                byte? bars = profile?.GetSignalBars();
                string statusText = !isOn
                    ? "Kapalı"
                    : hasInternet
                        ? (bars.HasValue ? $"Bağlı ({bars.Value}/5 sinyal)" : "İnternet Bağlı")
                        : "Açık (Ağ bekleniyor)";

                return new RadioStatusSnapshot(
                    Name: "Wi-Fi",
                    OperationalState: isOn ? ControlOperationalState.On : ControlOperationalState.Off,
                    Capability: CapabilityState.SupportedWithConsent,
                    StatusText: statusText,
                    SettingsDeepLinkUri: "ms-settings:network-wifi");
            }

            if (profile is not null)
            {
                return new RadioStatusSnapshot(
                    Name: "Ağ / Ethernet",
                    OperationalState: hasInternet ? ControlOperationalState.On : ControlOperationalState.Off,
                    Capability: CapabilityState.DeepLinkFallback,
                    StatusText: hasInternet ? "Ethernet Bağlı" : "Bağlantı Sınırlı",
                    SettingsDeepLinkUri: "ms-settings:network-status");
            }

            return new RadioStatusSnapshot(
                Name: "Wi-Fi",
                OperationalState: ControlOperationalState.Off,
                Capability: CapabilityState.DeepLinkFallback,
                StatusText: "Bağlı Değil",
                SettingsDeepLinkUri: "ms-settings:network-wifi");
        }
        catch (Exception ex)
        {
            logger.Warn("Network", "WIFI_STATUS_WARN", $"Wi-Fi status check degraded ({ex.GetType().Name}).");
            return new RadioStatusSnapshot(
                Name: "Wi-Fi",
                OperationalState: ControlOperationalState.Unavailable,
                Capability: CapabilityState.DeepLinkFallback,
                StatusText: "Windows Ayarları ile Yönet",
                SettingsDeepLinkUri: "ms-settings:network-wifi");
        }
    }

    public async Task<RadioStatusSnapshot> GetBluetoothStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var radios = await Radio.GetRadiosAsync();
            var btRadio = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
            if (btRadio is null)
            {
                return new RadioStatusSnapshot(
                    Name: "Bluetooth",
                    OperationalState: ControlOperationalState.Unavailable,
                    Capability: CapabilityState.Unavailable,
                    StatusText: "Adaptör Bulunamadı",
                    SettingsDeepLinkUri: "ms-settings:bluetooth");
            }

            bool isOn = btRadio.State == RadioState.On;
            return new RadioStatusSnapshot(
                Name: "Bluetooth",
                OperationalState: isOn ? ControlOperationalState.On : ControlOperationalState.Off,
                Capability: CapabilityState.SupportedWithConsent,
                StatusText: isOn ? "Açık" : "Kapalı",
                SettingsDeepLinkUri: "ms-settings:bluetooth");
        }
        catch (Exception ex)
        {
            logger.Warn("Radio", "BT_STATUS_WARN", $"Bluetooth query fallback ({ex.GetType().Name}).");
            return new RadioStatusSnapshot(
                Name: "Bluetooth",
                OperationalState: ControlOperationalState.Unavailable,
                Capability: CapabilityState.DeepLinkFallback,
                StatusText: "Windows Ayarları ile Yönet",
                SettingsDeepLinkUri: "ms-settings:bluetooth");
        }
    }

    public async Task<AuroraResult> TrySetRadioStateAsync(string radioKind, bool enabled, CancellationToken ct = default)
    {
        string deepLink = string.Equals(radioKind, "Bluetooth", StringComparison.OrdinalIgnoreCase)
            ? "ms-settings:bluetooth"
            : "ms-settings:network-wifi";

        try
        {
            var access = await Radio.RequestAccessAsync();
            if (access != RadioAccessStatus.Allowed)
            {
                return AuroraResult.Fail(
                    "RADIO_PERMISSION_DENIED",
                    "Windows radyo denetim izni kısıtlı; hızlı ayar bağlantısını kullanın.",
                    CapabilityState.PermissionRequired,
                    deepLink);
            }

            var radios = await Radio.GetRadiosAsync();
            var targetKind = string.Equals(radioKind, "Bluetooth", StringComparison.OrdinalIgnoreCase)
                ? RadioKind.Bluetooth
                : RadioKind.WiFi;

            var radio = radios.FirstOrDefault(r => r.Kind == targetKind);
            if (radio is null)
            {
                return AuroraResult.Fail("RADIO_NOT_FOUND", "İlgili kablosuz adaptör bulunamadı.", CapabilityState.Unavailable, deepLink);
            }

            var status = await radio.SetStateAsync(enabled ? RadioState.On : RadioState.Off);
            if (status == RadioAccessStatus.Allowed)
            {
                logger.Info("Radio", "RADIO_TOGGLED", $"Radio {targetKind} state changed to {enabled}.");
                return AuroraResult.Ok(CapabilityState.SupportedWithConsent);
            }

            return AuroraResult.Fail(
                "RADIO_SET_RESTRICTED",
                "İşlem Windows tarafından kısıtlandı; Windows Ayarları üzerinden değiştirilebilir.",
                CapabilityState.DeepLinkFallback,
                deepLink);
        }
        catch (Exception ex)
        {
            logger.Warn("Radio", "RADIO_TOGGLE_ERR", $"Radio toggle fallback ({ex.GetType().Name}).");
            return AuroraResult.Fail("RADIO_EX", "Kablosuz durum doğrudan değiştirilemedi.", CapabilityState.DeepLinkFallback, deepLink);
        }
    }

    public RadioStatusSnapshot GetAirplaneModeShortcutStatus() =>
        new(
            Name: "Uçak Modu",
            OperationalState: ControlOperationalState.Off,
            Capability: CapabilityState.DeepLinkFallback,
            StatusText: "Windows Ayarları Kısayolu",
            SettingsDeepLinkUri: "ms-settings:network-airplanemode");
}

public sealed class WindowsDisplayAndPowerService(IAuroraLogger logger) : IDisplayService, IPowerService
{
    public BrightnessSnapshot GetBrightness()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            using var collection = searcher.Get();
            foreach (ManagementObject obj in collection)
            {
                using (obj)
                {
                    int val = Convert.ToInt32(obj["CurrentBrightness"]);
                    return new BrightnessSnapshot(
                        IsSupported: true,
                        BrightnessPercent: Math.Clamp(val, 0, 100),
                        Capability: CapabilityState.DirectlySupported,
                        DisplayTypeLabel: "Dahili Ekran Paneli (WMI)");
                }
            }
        }
        catch
        {
            // External monitors or desktop GPUs without WMI brightness
        }

        return new BrightnessSnapshot(
            IsSupported: false,
            BrightnessPercent: 100,
            Capability: CapabilityState.Unavailable,
            DisplayTypeLabel: "Harici Ekran (Doğrudan WMI Parlaklığı Yok)");
    }

    public AuroraResult SetBrightness(int brightnessPercent)
    {
        int clamped = Math.Clamp(brightnessPercent, 5, 100);
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            using var collection = searcher.Get();
            bool any = false;
            foreach (ManagementObject obj in collection)
            {
                using (obj)
                {
                    obj.InvokeMethod("WmiSetBrightness", [1u, (byte)clamped]);
                    any = true;
                }
            }

            return any
                ? AuroraResult.Ok()
                : AuroraResult.Fail("BRIGHTNESS_UNSUPPORTED", "Bu ekranda yazılım üzerinden parlaklık desteklenmiyor.", CapabilityState.Unavailable, "ms-settings:display");
        }
        catch (Exception ex)
        {
            logger.Warn("Display", "BRIGHTNESS_SET_WARN", $"Brightness set unavailable ({ex.GetType().Name}).");
            return AuroraResult.Fail("BRIGHTNESS_ERR", "Parlaklık ayarlanamadı; Windows Ekran Ayarlarını kullanın.", CapabilityState.DeepLinkFallback, "ms-settings:display");
        }
    }

    public BatterySnapshot GetBatteryStatus()
    {
        if (!NativeMethods.GetSystemPowerStatus(out var sps))
        {
            return new BatterySnapshot(false, 100, false, false, CapabilityState.Unavailable, "Güç Durumu Okunamadı");
        }

        // BatteryFlag 128 = No system battery
        if ((sps.BatteryFlag & 128) != 0 || sps.BatteryLifePercent == 255)
        {
            return new BatterySnapshot(
                HasBattery: false,
                ChargePercent: 100,
                IsCharging: true,
                IsBatterySaverActive: false,
                Capability: CapabilityState.Unavailable,
                StatusText: "Masaüstü / AC Güç Bağlı");
        }

        int pct = Math.Clamp((int)sps.BatteryLifePercent, 0, 100);
        bool acOnline = sps.ACLineStatus == 1;
        bool saver = sps.SystemStatusFlag == 1;
        string status = acOnline
            ? (pct >= 99 ? $"Tam Dolu (%{pct} • Prize Takılı)" : $"Şarj Oluyor (%{pct})")
            : (saver ? $"Pil Tasarrufu Aktif (%{pct})" : $"Pilde Çalışıyor (%{pct})");

        return new BatterySnapshot(
            HasBattery: true,
            ChargePercent: pct,
            IsCharging: acOnline,
            IsBatterySaverActive: saver,
            Capability: CapabilityState.DirectlySupported,
            StatusText: status);
    }
}

public sealed class HkcuStartupRegistrationService(IAuroraLogger logger) : IStartupRegistrationService
{
    private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ProjectAurora";

    public bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: false);
            var val = key?.GetValue(ValueName) as string;
            return !string.IsNullOrWhiteSpace(val);
        }
        catch
        {
            return false;
        }
    }

    public AuroraResult SetStartupEnabled(bool enabled, string executablePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: true);
            if (key is null)
                return AuroraResult.Fail("STARTUP_REG_UNAVAILABLE", "Kullanıcı başlangıç anahtarı açılamadı.");

            if (enabled)
            {
                key.SetValue(ValueName, $"\"{executablePath}\"");
                logger.Info("Startup", "STARTUP_ENABLED", "Enabled standard-user HKCU Run startup entry.");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                logger.Info("Startup", "STARTUP_DISABLED", "Removed standard-user HKCU Run startup entry.");
            }

            return AuroraResult.Ok();
        }
        catch (Exception ex)
        {
            logger.Error("Startup", "STARTUP_ERR", "Failed to update HKCU startup registration.", ex);
            return AuroraResult.Fail("STARTUP_UPDATE_ERR", "Başlangıç ayarı güncellenemedi.");
        }
    }
}
