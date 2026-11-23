using Aurora.Core.Contracts;
using Aurora.Platform.Windows.Apps;
using Aurora.Platform.Windows.Audio;
using Aurora.Platform.Windows.DisplayAndTaskbar;
using Aurora.Platform.Windows.Metrics;
using Aurora.Platform.Windows.ShellExtras;
using Aurora.Platform.Windows.SystemControls;
using Xunit;

namespace Aurora.Platform.Windows.Tests;

public sealed class WindowsPlatformAdapterTests
{
    private sealed class TestLogger : IAuroraLogger
    {
        public string LogDirectoryPath => string.Empty;
        public IReadOnlyList<DiagnosticLogEntry> GetRecentEntries(int maxCount = 100) => [];
        public void Error(string category, string eventCode, string sanitizedMessage, Exception? ex = null) { }
        public void Info(string category, string eventCode, string sanitizedMessage) { }
        public void Warn(string category, string eventCode, string sanitizedMessage) { }
    }

    [Fact]
    public void MonitorTopologyService_ReturnsValidPrimaryMonitorAndDpi()
    {
        var svc = new MonitorTopologyService();
        var monitors = svc.GetMonitors();

        Assert.NotEmpty(monitors);
        var primary = svc.GetPrimaryMonitor();
        Assert.True(primary.IsPrimary);
        Assert.True(primary.EffectiveDpi >= 96);
        Assert.True(primary.BoundsPx.Width >= 800);
        Assert.True(primary.WorkAreaDip.Width > 0);
    }

    [Fact]
    public void Win32AppBarService_QueriesNativeTaskbarStatus_ReadOnlySafely()
    {
        using var appBar = new Win32AppBarService(new TestLogger());
        var status = appBar.QueryNativeTaskbarStatus();
        Assert.NotNull(status.GuidanceMessage);
        Assert.NotEmpty(status.GuidanceMessage);
    }

    [Fact]
    public void Win32WindowCatalog_EnumeratesVisibleTopLevelWindowsWithoutFault()
    {
        var catalog = new Win32WindowCatalog();
        var res = catalog.GetOpenWindows();

        Assert.True(res.IsSuccess);
        Assert.NotNull(res.Value);
        Assert.All(res.Value!, w =>
        {
            Assert.NotEqual(0, w.Hwnd);
            Assert.NotEqual((uint)Environment.ProcessId, w.ProcessId);
            Assert.False(string.IsNullOrWhiteSpace(w.Title));
        });
    }

    [Fact]
    public void CoreAudioService_ReadsEndpointsAndMasterStateSafely()
    {
        var audio = new CoreAudioService(new TestLogger());
        var endpoints = audio.GetEndpoints(includeCapture: true);
        Assert.NotNull(endpoints);

        if (endpoints.IsSuccess && endpoints.Value is { Count: > 0 })
        {
            var defState = audio.GetDefaultOutputState();
            Assert.True(defState.IsSuccess);
            Assert.InRange(defState.Value.VolumeScalar, 0.0, 1.0);
        }
    }

    [Fact]
    public async Task SystemMetricsAndPowerAdapters_RespectOfflineDefaultAndReturnBoundedValues()
    {
        var power = new WindowsDisplayAndPowerService(new TestLogger());
        var bat = power.GetBatteryStatus();
        Assert.InRange(bat.ChargePercent, 0, 100);

        var bright = power.GetBrightness();
        Assert.InRange(bright.BrightnessPercent, 0, 100);

        var metrics = new WindowsSystemMetricsService();
        var snap = metrics.SampleMetrics();
        Assert.InRange(snap.CpuUsagePercent, 0.0, 100.0);
        Assert.True(snap.RamTotalGb > 1.0);

        var offlineWeather = await metrics.GetWeatherAsync("İstanbul", 41.0, 28.9, allowNetwork: false);
        Assert.False(offlineWeather.IsLiveNetworkData);
    }

    [Fact]
    public void InstalledAppCatalogService_IndexesStartMenuAndSystemAppsWithoutNoise()
    {
        var catalog = new InstalledAppCatalogService();
        var apps = catalog.GetInstalledApps(forceRefresh: true);

        Assert.NotEmpty(apps);
        Assert.Contains(apps, a => a.Name == "Dosya Gezgini");
        Assert.Contains(apps, a => a.Name == "Görev Yöneticisi");
        Assert.DoesNotContain(apps, a => a.Name.Contains("uninstall", StringComparison.OrdinalIgnoreCase));

        // Verify in-memory cache returns same snapshot quickly
        var cached = catalog.GetInstalledApps(forceRefresh: false);
        Assert.Same(apps, cached);
    }

    [Fact]
    public void ShellExtrasServices_ReadKeyboardLayoutAndTrayAppsSafely()
    {
        var kb = new KeyboardLayoutService();
        var active = kb.GetActiveLayout();
        Assert.False(string.IsNullOrWhiteSpace(active.ShortCode));
        Assert.InRange(active.ShortCode.Length, 2, 4);

        var tray = new BackgroundTrayAppsService();
        var trayApps = tray.GetBackgroundTrayApps(forceRefresh: true);
        Assert.NotNull(trayApps);
        Assert.All(trayApps, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.DisplayName));
            Assert.True(t.MemoryMb >= 0);
        });

        var thumb = new WindowThumbnailCaptureService();
        Assert.Null(thumb.CapturePreviewJpegBase64(0));
    }

    [Fact]
    public void FullscreenMonitorDetector_EvaluatesAllMonitorsSafely()
    {
        var topology = new MonitorTopologyService();
        var monitors = topology.GetMonitors();
        var detector = new FullscreenMonitorDetector();

        var fs = detector.GetFullscreenMonitorDeviceIds(monitors);
        Assert.NotNull(fs);
        Assert.True(fs.Count <= monitors.Count);
    }

    [Fact]
    public void Win32WindowEventHook_StartsAndDisposesCleanly()
    {
        using var hook = new Win32WindowEventHook();
        hook.Start();
        Assert.True(hook.IsActive);
        hook.Dispose();
        Assert.False(hook.IsActive);
    }

    [Fact]
    public void CoreAudioService_QueriesCaptureMuteSafely_AndRejectsEmptyOutputDeviceId()
    {
        var audio = new CoreAudioService(new TestLogger());
        var mic = audio.GetDefaultCaptureMuteState();
        Assert.NotNull(mic.DeviceName);

        var invalidSwitch = audio.SetDefaultOutputDevice("   ");
        Assert.False(invalidSwitch.IsSuccess);
        Assert.Equal("AUDIO_INVALID_ID", invalidSwitch.Error?.Code);
    }
}
