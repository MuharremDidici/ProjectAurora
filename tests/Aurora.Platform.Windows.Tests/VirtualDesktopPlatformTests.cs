using Aurora.Platform.Windows.DisplayAndTaskbar;
using Xunit;

namespace Aurora.Platform.Windows.Tests;

public sealed class VirtualDesktopPlatformTests
{
    [Fact]
    public void VirtualDesktopService_ReadsWindows11RegistrySnapshot_Safely()
    {
        var svc = new VirtualDesktopService();
        var snap = svc.GetSnapshot();

        Assert.True(snap.TotalCount >= 1);
        Assert.InRange(snap.CurrentIndex, 0, snap.TotalCount - 1);
        Assert.Equal(snap.TotalCount, snap.Desktops.Count);
        Assert.False(string.IsNullOrWhiteSpace(snap.ActiveName));
        Assert.Single(snap.Desktops, d => d.IsCurrent);
    }
}
