using Aurora.Application.Security;
using Aurora.Application.Services;
using Aurora.Core.Models;
using Aurora.Core.Primitives;
using Aurora.Core.Tokens;
using Xunit;

namespace Aurora.Application.Tests;

public sealed class SecurityAndLayoutTests
{
    [Theory]
    [InlineData("../secret.png")]
    [InlineData(@"..\..\Windows\System32\cmd.exe")]
    [InlineData(@"C:\Users\Admin\bg.png")]
    [InlineData("/etc/passwd")]
    [InlineData("payload.exe")]
    [InlineData("script.ps1")]
    [InlineData("custom.xaml")]
    [InlineData("https://malicious.example/bg.png")]
    [InlineData(@"\\evilserver\share\bg.png")]
    public void ThemePackageValidator_RejectsUnsafeOrExecutablePaths(string unsafePath)
    {
        var result = ThemePackageValidator.ValidateRelativeAssetPath(unsafePath);
        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData("manifest.json")]
    [InlineData("assets/wallpaper.png")]
    [InlineData("preview/thumb.webp")]
    public void ThemePackageValidator_AllowsCleanRelativeApprovedAssets(string safePath)
    {
        var result = ThemePackageValidator.ValidateRelativeAssetPath(safePath);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ThemePackageValidator_ValidatesSchemaAndTokenBounds()
    {
        var validManifest = new ThemePackageManifest(
            SchemaVersion: "1.0",
            PackageId: "aurora-custom-1",
            Name: "Custom Glass",
            Author: "Aurora QA",
            Version: "1.0.0",
            Theme: BuiltInThemes.AuroraGlassDark,
            ApprovedAssets: ["assets/bg.png"]);

        var okReport = ThemePackageValidator.ValidateManifest(validManifest);
        Assert.True(okReport.IsValid);
        Assert.Empty(okReport.Errors);

        var badSchema = validManifest with { SchemaVersion = "9.9" };
        Assert.False(ThemePackageValidator.ValidateManifest(badSchema).IsValid);
    }

    [Fact]
    public void TaskbarLayoutCalculator_RespectsCoexistenceWorkArea_AndClampsFlyouts()
    {
        var monitor = new MonitorDescriptor(
            "DISPLAY1",
            "Primary 1440p",
            true,
            120, // 125% DPI
            new PhysicalPixelRect(0, 0, 2560, 1440),
            new PhysicalPixelRect(0, 0, 2560, 1380)); // 60px native taskbar at bottom

        var settings = new TaskbarSettings(TaskbarEdge.Bottom, TaskbarAlignment.Center, TaskbarDensity.Standard, FloatingDockStyle: true);
        var nativeCoex = new NativeTaskbarStatus(
            NativeTaskbarVisibilityState.AlwaysVisible,
            TaskbarEdge.Bottom,
            new PhysicalPixelRect(0, 1380, 2560, 60),
            CoexistenceActive: true,
            "Coexistence");

        var barDip = TaskbarLayoutCalculator.CalculateTaskbarBoundsDip(monitor, settings, nativeCoex);
        var barPx = barDip.ToPhysicalPixels(monitor.ScaleFactor);

        // Must never overlap native taskbar starting at Y=1380!
        Assert.True(barPx.Bottom <= 1380, $"Aurora bar bottom ({barPx.Bottom}) overlapped native taskbar (1380).");

        var flyoutDip = TaskbarLayoutCalculator.ClampFlyoutToWorkAreaDip(
            monitor,
            barDip,
            TaskbarEdge.Bottom,
            420,
            560,
            anchorCenterXDip: 99999); // Extreme right anchor must clamp safely inside work area

        Assert.True(flyoutDip.Right <= monitor.WorkAreaDip.Right);
        Assert.True(flyoutDip.Y >= monitor.WorkAreaDip.Y);
    }

    [Fact]
    public void BuildTaskbarGroups_MergesPinnedShortcutsAndRunningWindowsWithoutDuplicates()
    {
        var pins = new List<PinnedShortcut>
        {
            new("pin-notepad", "Not Defteri", "notepad.exe", null, "\uE70F", 0)
        };

        var windows = new List<AppWindowEntry>
        {
            new(101, 4200, "notepad", @"C:\Windows\notepad.exe", "notes.txt", true, false, true, "DISPLAY1"),
            new(102, 4200, "notepad", @"C:\Windows\notepad.exe", "todo.txt", false, false, true, "DISPLAY1"),
            new(205, 5100, "msedge", @"C:\Program Files\Edge\msedge.exe", "Browser", false, false, true, "DISPLAY1")
        };

        var groups = AuroraStateStore.BuildTaskbarGroups(pins, windows, groupByApplication: true);
        Assert.Equal(2, groups.Count);
        Assert.True(groups[0].IsPinned);
        Assert.Equal(2, groups[0].WindowCount);
        Assert.True(groups[0].HasActiveWindow);
        Assert.False(groups[1].IsPinned);
        Assert.Equal(1, groups[1].WindowCount);
    }
}
