using Aurora.Platform.Windows.Apps;
using Xunit;

namespace Aurora.Platform.Windows.Tests;

public sealed class OmniSearchPlatformTests
{
    [Fact]
    public void InstalledAppCatalog_EnumeratesShellAppsFolder_AndDeepWindowsSettings_Without90Cap()
    {
        var svc = new InstalledAppCatalogService();
        var apps = svc.GetInstalledApps(forceRefresh: true, includeIcons: false);

        Assert.NotEmpty(apps);
        Assert.Contains(apps, a => a.LaunchPath.StartsWith("appsfolder:", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(apps, a => a.LaunchPath.Equals("ms-settings:display", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(apps, a => a.LaunchPath.Equals("devmgmt.msc", StringComparison.OrdinalIgnoreCase));
        Assert.True(apps.Count > 100, $"Expected > 100 apps/settings/tools from shell:AppsFolder + settings, got {apps.Count}.");
    }

    [Fact]
    public void LocalFileSearch_NormalizesTurkishCharacters_Consistently()
    {
        Assert.Equal("istanbul", LocalFileSearchService.NormalizeTurkish("İstanbul"));
        Assert.Equal("isik", LocalFileSearchService.NormalizeTurkish("Işık"));
        Assert.Equal("ekran alintisi araci", LocalFileSearchService.NormalizeTurkish("Ekran Alıntısı Aracı"));
        Assert.Equal("guc ve uyku ozellikleri", LocalFileSearchService.NormalizeTurkish("Güç ve Uyku Özellikleri"));
    }

    [Fact]
    public void LocalFileSearch_ResolvesDirectFilesystemPaths()
    {
        var svc = new LocalFileSearchService();
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var hits = svc.Search(docs, maxResults: 5);

        Assert.NotEmpty(hits);
        Assert.Equal(docs, hits[0].FullPath);
        Assert.True(hits[0].IsDirectory);
    }

    [Fact]
    public void LaunchTarget_RejectsEmptyTargetSafely()
    {
        bool ok = InstalledAppCatalogService.LaunchTarget("   ", out var err);
        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(err));
    }
}
