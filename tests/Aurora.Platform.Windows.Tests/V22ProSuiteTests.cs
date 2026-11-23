using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Net.Http;
using Aurora.Platform.Windows.DisplayAndTaskbar;
using Aurora.Platform.Windows.Metrics;
using Aurora.Platform.Windows.ShellExtras;
using Aurora.Platform.Windows.SystemControls;
using Xunit;

namespace Aurora.Platform.Windows.Tests;

public sealed class V22ProSuiteTests
{
    [Fact]
    public void BluetoothBattery_NormalizesNames_ClassifiesCategories_AndEvaluatesAlertsWithHysteresis()
    {
        Assert.Equal("Sony WH-1000XM5", BluetoothBatteryService.NormalizeBluetoothDeviceName("Sony WH-1000XM5 Hands-Free AG Audio"));
        Assert.Equal("AirPods Pro", BluetoothBatteryService.NormalizeBluetoothDeviceName("AirPods Pro Avrcp Transport"));
        Assert.Equal("earbuds", BluetoothBatteryService.ClassifyCategory("AirPods Pro"));
        Assert.Equal("headset", BluetoothBatteryService.ClassifyCategory("Logitech Headset"));
        Assert.Equal("headset", BluetoothBatteryService.ClassifyCategory("G435 Bluetooth Gaming Headset"));
        Assert.Equal("mouse", BluetoothBatteryService.ClassifyCategory("MX Master 3S"));
        Assert.Equal("mouse", BluetoothBatteryService.ClassifyCategory("Logitech Pebble"));
        Assert.Equal("keyboard", BluetoothBatteryService.ClassifyCategory("Keychron K2"));
        Assert.Equal("gamepad", BluetoothBatteryService.ClassifyCategory("Xbox Wireless Controller"));

        var svc = new BluetoothBatteryService();
        var d1 = new BluetoothBatteryDevice("dev-1", "Logitech Headset", 65, "headset", "PnP");

        // 1st poll seeds silently (no boot spam)
        Assert.Empty(svc.EvaluateAlerts([d1]));

        // New device connects -> connected alert
        var d2 = new BluetoothBatteryDevice("dev-2", "MX Master 3S", 40, "mouse", "BLE");
        var connectedAlerts = svc.EvaluateAlerts([d1, d2]);
        Assert.Single(connectedAlerts);
        Assert.Equal("connected", connectedAlerts[0].Kind);
        Assert.Equal("MX Master 3S", connectedAlerts[0].Device.Name);

        // Battery drops to 12% -> lowBattery alert once
        var d2Low = d2 with { BatteryPercent = 12 };
        var lowAlerts = svc.EvaluateAlerts([d1, d2Low]);
        Assert.Single(lowAlerts);
        Assert.Equal("lowBattery", lowAlerts[0].Kind);

        // Repeated poll at 11% -> suppressed by hysteresis
        Assert.Empty(svc.EvaluateAlerts([d1, d2 with { BatteryPercent = 11 }]));

        // Recharged to 30% then drops again to 14% -> fires again
        Assert.Empty(svc.EvaluateAlerts([d1, d2 with { BatteryPercent = 30 }]));
        var reLow = svc.EvaluateAlerts([d1, d2 with { BatteryPercent = 14 }]);
        Assert.Single(reLow);
        Assert.Equal("lowBattery", reLow[0].Kind);
    }

    [Fact]
    public void PrivacyIndicator_FormatsWin32AndPackagedNames_AndReadsRegistrySafely()
    {
        Assert.Equal("Discord", PrivacyIndicatorService.FormatConsentAppName(@"C:#Users#Test#AppData#Local#Discord#Discord.exe", isNonPackaged: true));
        Assert.Equal("OBS Studio", PrivacyIndicatorService.FormatConsentAppName(@"C:#Program Files#obs-studio#bin#64bit#obs64.exe", isNonPackaged: true));
        Assert.Equal("WhatsApp", PrivacyIndicatorService.FormatConsentAppName("5319275A.WhatsAppDesktop_cv1g1gvanyjgm", isNonPackaged: false));

        var svc = new PrivacyIndicatorService();
        var snap = svc.GetCurrentSnapshot();
        Assert.NotNull(snap.CameraApps);
        Assert.NotNull(snap.MicApps);
    }

    [Fact]
    public void QrCodeSvgGenerator_ProducesValidFinderPatternsAndSvg()
    {
        string url = "http://192.168.1.42:54321/s/a1b2c3d4e5";
        var matrix = QrCodeSvgGenerator.EncodeMatrix(url);
        int size = matrix.GetLength(0);

        Assert.Equal(size, matrix.GetLength(1));
        Assert.True(size >= 25 && (size - 17) % 4 == 0);

        // Verify all 3 7x7 finder corners have dark outer border & dark 3x3 center
        Assert.True(matrix[0, 0] && matrix[3, 3] && !matrix[1, 1]);
        Assert.True(matrix[0, size - 1] && matrix[3, size - 4]);
        Assert.True(matrix[size - 1, 0] && matrix[size - 4, 3]);

        string svg = QrCodeSvgGenerator.GenerateSvg(url);
        Assert.StartsWith("<svg ", svg);
        Assert.Contains("<path ", svg);
        Assert.EndsWith("</svg>", svg);
    }

    [Fact]
    public async Task LanQrFileShare_ServesLandingPageAndExactByteDownload_ZeroUac()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "AuroraQrTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string sampleFile = Path.Combine(tempDir, "aurora-test.txt");
        byte[] expectedBytes = "Merhaba Project Aurora v2.2 LAN AirDrop!"u8.ToArray();
        await File.WriteAllBytesAsync(sampleFile, expectedBytes);

        using var share = new LanQrFileShareService();
        try
        {
            var session = share.StartSharing([sampleFile]);
            Assert.NotNull(session);
            Assert.Contains("/s/", session.Url);
            Assert.StartsWith("<svg ", session.QrSvg);

            // Replace LAN IP with loopback for deterministic local HTTP verification
            var uri = new Uri(session.Url);
            string baseLocal = $"http://127.0.0.1:{uri.Port}";

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            string landingHtml = await http.GetStringAsync($"{baseLocal}/s/{session.Token}");
            Assert.Contains("aurora-test.txt", landingHtml);

            byte[] downloaded = await http.GetByteArrayAsync($"{baseLocal}/d/{session.Token}");
            Assert.Equal(expectedBytes, downloaded);

            // Server increments DownloadCount immediately after flushing response bytes on its worker task
            LanQrShareSession? updated = share.GetActiveSession();
            for (int i = 0; i < 40 && (updated is null || updated.DownloadCount < 1); i++)
            {
                await Task.Delay(25);
                updated = share.GetActiveSession();
            }
            Assert.NotNull(updated);
            Assert.Equal(1, updated!.DownloadCount);
        }
        finally
        {
            share.StopSharing();
            Assert.Null(share.GetActiveSession());
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void QuickFileActions_ConvertsPngToJpg_AndZipsMultipleFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "AuroraQuickAct_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string pngPath = Path.Combine(tempDir, "demo.png");
            using (var bmp = new Bitmap(32, 32))
            {
                using var g = Graphics.FromImage(bmp);
                g.Clear(Color.DeepSkyBlue);
                bmp.Save(pngPath, ImageFormat.Png);
            }

            var conv = LanQrFileShareService.ExecuteQuickConvertOrZip([pngPath]);
            Assert.True(conv.Success);
            Assert.EndsWith(".jpg", conv.OutputPath, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(conv.OutputPath));

            string txtPath = Path.Combine(tempDir, "not.txt");
            File.WriteAllText(txtPath, "hello");
            var zipRes = LanQrFileShareService.ExecuteQuickConvertOrZip([pngPath, txtPath]);
            Assert.True(zipRes.Success);
            Assert.EndsWith(".zip", zipRes.OutputPath, StringComparison.OrdinalIgnoreCase);
            using var archive = ZipFile.OpenRead(zipRes.OutputPath);
            Assert.Equal(2, archive.Entries.Count);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void AnimatedGifEncoder_ProducesValidMultiFrameGif89aDecodableByGdiPlus()
    {
        const int w = 64, h = 48;
        var frames = new List<byte[]>();
        Color[] colors = [Color.FromArgb(14, 21, 38), Color.FromArgb(56, 189, 248), Color.FromArgb(52, 211, 153)];

        foreach (var c in colors)
        {
            using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(c);
                using var br = new SolidBrush(Color.White);
                g.FillRectangle(br, 10, 10, 20, 20);
            }
            frames.Add(AnimatedGifRecorderService.QuantizeBitmapToPaletteIndices(bmp));
        }

        using var ms = new MemoryStream();
        AnimatedGifRecorderService.EncodeGif89a(ms, w, h, frames, delayCentiseconds: 10);
        byte[] gifBytes = ms.ToArray();

        Assert.True(gifBytes.Length > 800);
        Assert.Equal("GIF89a", System.Text.Encoding.ASCII.GetString(gifBytes, 0, 6));
        Assert.Equal(0x3B, gifBytes[^1]);

        ms.Position = 0;
        using var decoded = Image.FromStream(ms);
        Assert.Equal(w, decoded.Width);
        Assert.Equal(h, decoded.Height);
        int frameCount = decoded.GetFrameCount(FrameDimension.Time);
        Assert.Equal(3, frameCount);
    }

    [Fact]
    public void TopProcesses_AndDdcCiService_BehaveSafely()
    {
        var top = WindowsSystemMetricsService.GetTopMemoryProcesses(3);
        Assert.True(top.Length <= 3);
        Assert.DoesNotContain(top, p => string.Equals(p.Name, "explorer", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(top, p => string.Equals(p.Name, "Aurora.App", StringComparison.OrdinalIgnoreCase));
        Assert.False(WindowsSystemMetricsService.TryTerminateUserProcessSafe("explorer", 4));
        Assert.False(WindowsSystemMetricsService.TryTerminateUserProcessSafe("Aurora.App", Environment.ProcessId));

        var ddc = new DdcCiMonitorBrightnessService();
        Assert.Equal("VSC803C", DdcCiMonitorBrightnessService.ExtractMonitorHardwareCode(@"MONITOR\VSC803C\{4d36e96e-e325-11ce-bfc1-08002be10318}\0002"));
        Assert.Equal("AIC9450", DdcCiMonitorBrightnessService.ExtractMonitorHardwareCode(@"DISPLAY\AIC9450\5&298d202f&0&UID4354_0"));
        Assert.Equal("DISPLAY1", DdcCiMonitorBrightnessService.SanitizeMonitorIdSegment(@"\\.\DISPLAY1"));
        var mons = ddc.DiscoverMonitors();
        Assert.NotNull(mons);
        Assert.All(mons, m => Assert.DoesNotContain("\\", m.Id));
    }

    [Fact]
    public void NativeShellSuppression_MatchesWin11OsdAndToasts_WhileProtectingSystemPanels()
    {
        // Volume key interception covers physical AND driver-injected volume keys (Lenovo Fn / Logitech G HUB)
        Assert.True(HardwareOsdMonitorService.ShouldInterceptVolumeKey(true, HardwareOsdMonitorService.VkVolumeUp));
        Assert.True(HardwareOsdMonitorService.ShouldInterceptVolumeKey(true, HardwareOsdMonitorService.VkVolumeDown));
        Assert.True(HardwareOsdMonitorService.ShouldInterceptVolumeKey(true, HardwareOsdMonitorService.VkVolumeMute));
        Assert.False(HardwareOsdMonitorService.ShouldInterceptVolumeKey(false, HardwareOsdMonitorService.VkVolumeUp));
        Assert.False(HardwareOsdMonitorService.ShouldInterceptVolumeKey(true, HardwareOsdMonitorService.VkCapital));

        Rectangle[] mons = [new Rectangle(0, 0, 2560, 1440)];

        // Bottom-center high-DPI Win11 volume flyout (with DWM shadow padding)
        Assert.True(HardwareOsdMonitorService.IsCompactBottomCenterOsdGeometry(new Rectangle(1060, 1270, 440, 110), mons));
        // Top-center Win11 on-screen indicator
        Assert.True(HardwareOsdMonitorService.IsCompactBottomCenterOsdGeometry(new Rectangle(1080, 42, 400, 90), mons));
        // Start Menu & Quick Settings must NEVER be matched
        Assert.False(HardwareOsdMonitorService.IsCompactBottomCenterOsdGeometry(new Rectangle(930, 680, 700, 720), mons));
        Assert.False(HardwareOsdMonitorService.IsCompactBottomCenterOsdGeometry(new Rectangle(2160, 960, 380, 440), mons));

        // Turkish & English native toast banners -> MATCH
        Assert.True(HardwareOsdMonitorService.IsNativeToastBannerMatch("Yeni bildirim", new Rectangle(2140, 1240, 400, 140), mons));
        Assert.True(HardwareOsdMonitorService.IsNativeToastBannerMatch("New notification", new Rectangle(2140, 1240, 400, 140), mons));
        // Untitled bottom-right toast banner -> MATCH
        Assert.True(HardwareOsdMonitorService.IsNativeToastBannerMatch("", new Rectangle(2150, 1260, 390, 130), mons));

        // Action Center / Notification Center / Quick Settings / Start -> STRICTLY PROTECTED (false)
        Assert.False(HardwareOsdMonitorService.IsNativeToastBannerMatch("Bildirim Merkezi", new Rectangle(2140, 1240, 400, 140), mons));
        Assert.False(HardwareOsdMonitorService.IsNativeToastBannerMatch("Notification Center", new Rectangle(2140, 600, 400, 800), mons));
        Assert.False(HardwareOsdMonitorService.IsNativeToastBannerMatch("Hızlı ayarlar", new Rectangle(2140, 1020, 400, 380), mons));
        Assert.False(HardwareOsdMonitorService.IsNativeToastBannerMatch("Quick settings", new Rectangle(2140, 1020, 400, 380), mons));
        Assert.False(HardwareOsdMonitorService.IsNativeToastBannerMatch("", new Rectangle(2140, 200, 400, 900), mons));
    }

    [Fact]
    public void NotificationActivation_AndDockWorkAreaReservation_ComputeAccurateResults()
    {
        // Protocol URI deep-links (Antigravity, Snipping Tool, WhatsApp, Teams)
        Assert.True(NotificationActivationService.TryExtractLaunchUri("antigravity-ide://google.google-antigravity/open-conversation?cascadeId=abc", "protocol", out var agUri));
        Assert.StartsWith("antigravity-ide://", agUri);
        Assert.True(NotificationActivationService.TryExtractLaunchUri("ms-screensketch:edit?&filePath=test.png", "protocol", out var ssUri));
        Assert.StartsWith("ms-screensketch:", ssUri);
        Assert.True(NotificationActivationService.TryExtractLaunchUri("whatsapp://send?phone=123", "", out var waUri));
        Assert.StartsWith("whatsapp://", waUri);
        Assert.False(NotificationActivationService.TryExtractLaunchUri("chatId=905551112233@s.whatsapp.net", "foreground", out _));

        // Per-monitor Pinned Fixed Bar WorkArea reservation (slim 44/52/60px bottom, 58px vertical at 100% scale)
        var mon = new Aurora.Core.Primitives.PhysicalPixelRect(0, 0, 2560, 1440);
        var waBottom = Win32AppBarService.ComputeDockReservedWorkArea(mon, 1.0, "Bottom", 100, reserve: true);
        Assert.Equal(0, waBottom.X);
        Assert.Equal(2560, waBottom.Width);
        Assert.Equal(52, mon.Bottom - waBottom.Bottom);

        // Density-aware Fixed Bar heights (Compact 44px < Standard 52px < Cozy 60px)
        var waCompact = Win32AppBarService.ComputeDockReservedWorkArea(mon, 1.0, "Bottom", 100, reserve: true, dockDensity: "Compact");
        var waCozy = Win32AppBarService.ComputeDockReservedWorkArea(mon, 1.0, "Bottom", 100, reserve: true, dockDensity: "Cozy");
        Assert.Equal(44, mon.Bottom - waCompact.Bottom);
        Assert.Equal(60, mon.Bottom - waCozy.Bottom);

        var waLeft = Win32AppBarService.ComputeDockReservedWorkArea(mon, 1.0, "Left", 100, reserve: true);
        Assert.Equal(58, waLeft.X);
        Assert.Equal(1440, waLeft.Height);

        var waRight = Win32AppBarService.ComputeDockReservedWorkArea(mon, 1.0, "Right", 100, reserve: true);
        Assert.Equal(58, mon.Right - waRight.Right);

        var waFull = Win32AppBarService.ComputeDockReservedWorkArea(mon, 1.0, "Bottom", 100, reserve: false);
        Assert.Equal(mon, waFull);
    }
}
