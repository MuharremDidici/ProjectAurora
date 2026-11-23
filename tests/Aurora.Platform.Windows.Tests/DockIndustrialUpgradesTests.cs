using Aurora.Platform.Windows.ShellExtras;
using Xunit;

namespace Aurora.Platform.Windows.Tests;

public sealed class DockIndustrialUpgradesTests
{
    [Fact]
    public void DockFolderStackService_ReadsRecentItems_AndSkipsHiddenFiles()
    {
        string dir = Path.Combine(Path.GetTempPath(), "aurora_fstack_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "desktop.ini"), "hidden");
            File.WriteAllText(Path.Combine(dir, ".gitignore"), "hidden");
            File.WriteAllText(Path.Combine(dir, "older.txt"), "hello");
            Thread.Sleep(25);
            File.WriteAllText(Path.Combine(dir, "newest.pdf"), "pdf-bytes");

            var items = DockFolderStackService.ReadFolderStackItems(dir, maxItems: 5);
            Assert.Equal(2, items.Count);
            Assert.Equal("newest.pdf", items[0].Name);
            Assert.Equal("older.txt", items[1].Name);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void WindowAttentionAndProgress_TracksNotifications_AndParsesRealProgress()
    {
        var svc = new WindowAttentionAndProgressService();
        svc.MarkAttention("Microsoft Teams");
        Assert.True(svc.HasAttention("Teams", "ms-teams"));

        svc.ClearAttention("msteams");
        Assert.False(svc.HasAttention("Teams", "ms-teams"));

        Assert.Equal(42, WindowAttentionAndProgressService.ExtractProgressPercent(["%42 tamamlandı - Kopyalanıyor"]));
        Assert.Equal(68, WindowAttentionAndProgressService.ExtractProgressPercent(["Copying 12 items - 68% complete"]));
        Assert.Equal(85, WindowAttentionAndProgressService.ExtractProgressPercent(["Downloading update 85%"]));
        Assert.Null(WindowAttentionAndProgressService.ExtractProgressPercent(["50% Off Summer Sale - Opera"]));
    }
}
