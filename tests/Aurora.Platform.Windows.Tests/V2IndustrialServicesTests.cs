using System.Drawing;
using System.Drawing.Imaging;
using Aurora.Core.Contracts;
using Aurora.Platform.Windows.Audio;
using Aurora.Platform.Windows.DisplayAndTaskbar;
using Aurora.Platform.Windows.ShellExtras;
using Aurora.Platform.Windows.SystemControls;
using Xunit;

namespace Aurora.Platform.Windows.Tests;

public sealed class V2IndustrialServicesTests
{
    private sealed class QuietLogger : IAuroraLogger
    {
        public string LogDirectoryPath => string.Empty;
        public IReadOnlyList<DiagnosticLogEntry> GetRecentEntries(int maxCount = 100) => [];
        public void Error(string category, string eventCode, string sanitizedMessage, Exception? ex = null) { }
        public void Info(string category, string eventCode, string sanitizedMessage) { }
        public void Warn(string category, string eventCode, string sanitizedMessage) { }
    }

    [Fact]
    public void AwakeKeepAliveService_TransitionsModes_AndExpiresCleanly()
    {
        using var svc = new AwakeKeepAliveService();
        var t0 = new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);

        Assert.False(svc.GetSnapshot(t0).IsActive);

        var snap30 = svc.SetMode(AwakeMode.Minutes30, t0);
        Assert.True(snap30.IsActive);
        Assert.Equal("30m", snap30.ModeKey);
        Assert.Equal(30, snap30.RemainingMinutes);

        Assert.False(svc.CheckExpiration(t0.AddMinutes(15)));
        Assert.Equal(15, svc.GetSnapshot(t0.AddMinutes(15)).RemainingMinutes);

        // Expires cleanly at +30m
        Assert.True(svc.CheckExpiration(t0.AddMinutes(30)));
        Assert.False(svc.GetSnapshot(t0.AddMinutes(30)).IsActive);

        // Indefinite mode never expires
        var indef = svc.SetModeByKey("indefinite", t0);
        Assert.True(indef.IsActive);
        Assert.False(svc.CheckExpiration(t0.AddHours(24)));
    }

    [Fact]
    public void SnapZonesLayoutCatalog_ComputesAccurateZones_ForAllPresets()
    {
        var workArea = new Rectangle(0, 0, 1920, 1040);

        var codeZones = SnapZonesLayoutCatalog.ComputeZones(workArea, "code6535", gapPx: 10);
        Assert.Equal(2, codeZones.Count);
        Assert.True(codeZones[0].BoundsPx.Width > codeZones[1].BoundsPx.Width);
        Assert.False(codeZones[0].BoundsPx.IntersectsWith(codeZones[1].BoundsPx));

        var studio3 = SnapZonesLayoutCatalog.ComputeZones(workArea, "studio3", gapPx: 10);
        Assert.Equal(3, studio3.Count);

        var cols3 = SnapZonesLayoutCatalog.ComputeZones(workArea, "cols3", gapPx: 10);
        Assert.Equal(3, cols3.Count);

        var grid = SnapZonesLayoutCatalog.ComputeZones(workArea, "grid2x2", gapPx: 10);
        Assert.Equal(4, grid.Count);

        var hit = SnapZonesLayoutCatalog.HitTestZone(grid, new Point(1500, 800));
        Assert.NotNull(hit);
        Assert.Equal(4, hit!.Index);
    }

    [Fact]
    public async Task ScreenOcrService_DescribesColors_AndExtractsTextFromRenderedBitmap()
    {
        var info = ScreenOcrService.DescribeColor(Color.FromArgb(56, 189, 248));
        Assert.Equal("#38BDF8", info.Hex);
        Assert.Equal("rgb(56, 189, 248)", info.Rgb);
        Assert.StartsWith("hsl(19", info.Hsl);

        // Render high-contrast synthetic text image and verify live offline Windows WinRT OCR reads it
        using var bmp = new Bitmap(560, 140, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            using var font = new Font("Segoe UI", 36f, FontStyle.Bold);
            g.DrawString("AURORA 2026", font, Brushes.Black, 30, 35);
        }

        string? thumb = ScreenOcrService.CreatePngThumbnailBase64(bmp);
        Assert.False(string.IsNullOrWhiteSpace(thumb));

        var ocr = await ScreenOcrService.ExtractTextFromBitmapAsync(bmp);
        Assert.True(ocr.Success, ocr.ErrorMessage);
        Assert.Contains("AURORA", ocr.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026", ocr.Text);
    }

    [Fact]
    public void PerAppAudioRoutingService_FormatsSwdPaths_AndQueriesLivePolicyFactorySafely()
    {
        const string sampleMmId = "{0.0.0.00000000}.{11111111-2222-3333-4444-555555555555}";
        string? swd = PerAppAudioRoutingService.FormatRenderDeviceInterfaceId(sampleMmId);
        Assert.Equal(@"\\?\SWD#MMDEVAPI#{0.0.0.00000000}.{11111111-2222-3333-4444-555555555555}#{e6327cad-dcec-4949-ae8a-991e976a79d2}", swd);
        Assert.Equal(sampleMmId, PerAppAudioRoutingService.ExtractMmDeviceId(swd));
        Assert.Null(PerAppAudioRoutingService.FormatRenderDeviceInterfaceId(""));

        var svc = new PerAppAudioRoutingService(new QuietLogger());
        // Live Windows 11 probe: must never crash even across Insider/24H2/25H2 builds
        _ = svc.IsSupported();
        _ = svc.GetAppOutputDeviceId((uint)Environment.ProcessId);

        var invalid = svc.SetAppOutputDevice(0, sampleMmId);
        Assert.False(invalid.IsSuccess);
        Assert.Equal("AUDIO_APP_PID_INVALID", invalid.Error?.Code);
    }

    [Fact]
    public void Win32AppBarService_ComputesExactRestoredWorkArea_AcrossMultiMonitorEdges()
    {
        // Primary monitor (2560x1440) with 48px bottom taskbar
        var monPrimary = new Aurora.Core.Primitives.PhysicalPixelRect(0, 0, 2560, 1440);
        var trayBottom = new Aurora.Core.Primitives.PhysicalPixelRect(0, 1392, 2560, 48);
        var waPrimary = Win32AppBarService.ComputeRestoredWorkArea(monPrimary, trayBottom);
        Assert.Equal(0, waPrimary.X);
        Assert.Equal(0, waPrimary.Y);
        Assert.Equal(2560, waPrimary.Width);
        Assert.Equal(1392, waPrimary.Height);

        // Left negative-origin monitor (-1280, 200, 1280x1024) with 40px bottom secondary taskbar
        var monLeft = new Aurora.Core.Primitives.PhysicalPixelRect(-1280, 200, 1280, 1024);
        var trayLeftSec = new Aurora.Core.Primitives.PhysicalPixelRect(-1280, 1184, 1280, 40);
        var waLeft = Win32AppBarService.ComputeRestoredWorkArea(monLeft, trayLeftSec);
        Assert.Equal(-1280, waLeft.X);
        Assert.Equal(200, waLeft.Y);
        Assert.Equal(1280, waLeft.Width);
        Assert.Equal(984, waLeft.Height);

        // Top-docked taskbar case
        var trayTop = new Aurora.Core.Primitives.PhysicalPixelRect(0, 0, 2560, 48);
        var waTop = Win32AppBarService.ComputeRestoredWorkArea(monPrimary, trayTop);
        Assert.Equal(48, waTop.Y);
        Assert.Equal(1392, waTop.Height);
    }

    [Fact]
    public void HardwareOsdMonitorService_SeedsSilently_AndEmitsAccurateHardwareEvents()
    {
        using var osd = new HardwareOsdMonitorService();

        // First sample seeds baseline silently (no startup spam)
        Assert.Null(osd.ObserveState(50, false, true, false, true, 70, false, true));

        // Volume step up -> volume HUD
        var volEvt = osd.ObserveState(64, false, true, false, true, 70, false, true);
        Assert.NotNull(volEvt);
        Assert.Equal("volume", volEvt!.Kind);
        Assert.Equal(64, volEvt.Percent);
        Assert.True(volEvt.Active);

        // Mute -> mute HUD
        var muteEvt = osd.ObserveState(64, true, true, false, true, 70, false, true);
        Assert.NotNull(muteEvt);
        Assert.Equal("mute", muteEvt!.Kind);
        Assert.False(muteEvt.Active);

        // Brightness change -> brightness HUD
        var brEvt = osd.ObserveState(64, true, true, false, true, 85, false, true);
        Assert.NotNull(brEvt);
        Assert.Equal("brightness", brEvt!.Kind);
        Assert.Equal(85, brEvt.Percent);

        // Mic muted -> mic HUD
        var micEvt = osd.ObserveState(64, true, true, true, true, 85, false, true);
        Assert.NotNull(micEvt);
        Assert.Equal("mic", micEvt!.Kind);
        Assert.Equal("KAPALI", micEvt.ValueText);
        Assert.False(micEvt.Active);

        // CapsLock toggled on -> capslock HUD
        var capsEvt = osd.ObserveState(64, true, true, true, true, 85, true, true);
        Assert.NotNull(capsEvt);
        Assert.Equal("capslock", capsEvt!.Kind);
        Assert.Equal("AÇIK", capsEvt.ValueText);
        Assert.True(capsEvt.Active);
    }

    [Fact]
    public void AltDragWindowService_Resolves3x3Quadrants_AndPreventsWindowInversion()
    {
        var win = new Rectangle(100, 100, 900, 600);

        Assert.Equal(ResizeEdges.Left | ResizeEdges.Top, AltDragWindowService.ResolveResizeEdges(win, new Point(150, 150)));
        Assert.Equal(ResizeEdges.Right | ResizeEdges.Bottom, AltDragWindowService.ResolveResizeEdges(win, new Point(950, 650)));
        Assert.Equal(ResizeEdges.Top, AltDragWindowService.ResolveResizeEdges(win, new Point(550, 140)));
        // Center defaults smoothly to Bottom-Right
        Assert.Equal(ResizeEdges.Right | ResizeEdges.Bottom, AltDragWindowService.ResolveResizeEdges(win, new Point(550, 400)));

        // Shrinking aggressively past minimum clamps cleanly without inverting bounds
        var clamped = AltDragWindowService.ComputeResizedRect(win, ResizeEdges.Left | ResizeEdges.Top, dx: 950, dy: 900);
        Assert.True(clamped.Width >= 260);
        Assert.True(clamped.Height >= 150);
        Assert.Equal(win.Right, clamped.Right);
        Assert.Equal(win.Bottom, clamped.Bottom);
    }

    [Fact]
    public void HardwareOsdMonitorService_VolumeStep_SyncBaseline_AndNativeOsdGeometryPrecision()
    {
        Assert.Equal(90, HardwareOsdMonitorService.ComputeNextVolumeStep(88, stepUp: true));
        Assert.Equal(90, HardwareOsdMonitorService.ComputeNextVolumeStep(89, stepUp: true));
        Assert.Equal(100, HardwareOsdMonitorService.ComputeNextVolumeStep(99, stepUp: true));
        Assert.Equal(88, HardwareOsdMonitorService.ComputeNextVolumeStep(90, stepUp: false));
        Assert.Equal(90, HardwareOsdMonitorService.ComputeNextVolumeStep(91, stepUp: false));
        Assert.Equal(0, HardwareOsdMonitorService.ComputeNextVolumeStep(1, stepUp: false));

        using var osd = new HardwareOsdMonitorService();
        osd.ObserveState(50, false, true, false, true, 70, false, false);
        osd.SyncVolumeBaseline(52, false);
        // After syncing baseline directly, delta poller must NOT double-fire
        Assert.Null(osd.ObserveState(52, false, true, false, true, 70, false, false));

        var monitors = new[] { new Rectangle(0, 0, 1920, 1080) };
        // Windows 11 bottom-center hardware slider pill (~230x56 near bottom center) -> matched
        Assert.True(HardwareOsdMonitorService.IsCompactBottomCenterOsdGeometry(new Rectangle(845, 960, 230, 56), monitors));
        // Alt+Tab / Start Menu / Quick Settings -> NEVER matched!
        Assert.False(HardwareOsdMonitorService.IsCompactBottomCenterOsdGeometry(new Rectangle(460, 240, 1000, 600), monitors));
        Assert.False(HardwareOsdMonitorService.IsCompactBottomCenterOsdGeometry(new Rectangle(1520, 580, 380, 440), monitors));
    }

    [Fact]
    public void AltDragWindowService_ResolvesDockEdgeMagneticOffset_AndDockLiveScale()
    {
        var mon = new Rectangle(0, 0, 1920, 1080);

        // Left edge near vertical center snaps magnetically to 0
        var (eLeft, oLeft) = AltDragWindowService.ResolveTargetDockEdgeAndOffset(mon, new Point(120, 550));
        Assert.Equal("Left", eLeft);
        Assert.Equal(0, oLeft);

        // Right edge near top computes negative offset
        var (eRight, oRight) = AltDragWindowService.ResolveTargetDockEdgeAndOffset(mon, new Point(1850, 200));
        Assert.Equal("Right", eRight);
        Assert.True(oRight < -40);

        // Bottom center snaps magnetically to 0; bottom right shifts positively
        var (eBotMid, oBotMid) = AltDragWindowService.ResolveTargetDockEdgeAndOffset(mon, new Point(950, 1020));
        Assert.Equal("Bottom", eBotMid);
        Assert.Equal(0, oBotMid);

        var (eBotRight, oBotRight) = AltDragWindowService.ResolveTargetDockEdgeAndOffset(mon, new Point(1400, 1020));
        Assert.Equal("Bottom", eBotRight);
        Assert.True(oBotRight > 35);

        // Dock scale resizing: outward grows, inward shrinks, strictly clamped to 75..135
        Assert.True(AltDragWindowService.ComputeResizedDockScale(100, "Bottom", dx: 0, dy: -80) > 110);
        Assert.True(AltDragWindowService.ComputeResizedDockScale(100, "Left", dx: 80, dy: 0) > 110);
        Assert.True(AltDragWindowService.ComputeResizedDockScale(100, "Right", dx: -80, dy: 0) > 110);
        Assert.Equal(135, AltDragWindowService.ComputeResizedDockScale(100, "Bottom", dx: 0, dy: -500));
        Assert.Equal(75, AltDragWindowService.ComputeResizedDockScale(100, "Bottom", dx: 0, dy: 500));
    }
}
