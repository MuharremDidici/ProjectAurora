using System.Text;
using Aurora.Core.Models;
using Aurora.Infrastructure.Notifications;
using Aurora.Infrastructure.Persistence;
using Aurora.Infrastructure.Updates;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aurora.Integration.Tests;

public sealed class V2PillarsTests
{
    [Fact]
    public void WindowsNotificationBridge_ParsesRealToastXml_AndFriendlyAppNames()
    {
        string xml = """
            <toast>
              <visual>
                <binding template="ToastGeneric">
                  <text>Ahmet Yılmaz</text>
                  <text>Toplantı 14:00'e alındı</text>
                  <text>Teams Kanalı</text>
                </binding>
              </visual>
            </toast>
            """;

        var (title, body) = WindowsNotificationBridgeService.ParseToastXmlPayload(Encoding.UTF8.GetBytes(xml));
        Assert.Equal("Ahmet Yılmaz", title);
        Assert.Contains("Toplantı 14:00'e alındı", body);
        Assert.Equal("WhatsApp", WindowsNotificationBridgeService.FormatFriendlyAppName("5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App"));
        Assert.Equal("Microsoft Teams", WindowsNotificationBridgeService.FormatFriendlyAppName("MSTeams_8wekyb3d8bbwe!MSTeams"));
        Assert.Equal("Windows Güvenliği", WindowsNotificationBridgeService.FormatFriendlyAppName("Windows.Defender.SecHealthUI"));
    }

    [Fact]
    public void WindowsNotificationBridge_ReadsToastsFromWpnSchemaSqlite_Safely()
    {
        string dir = Path.Combine(Path.GetTempPath(), "aurora_wpn_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        string dbPath = Path.Combine(dir, "wpndatabase.db");
        try
        {
            using (var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE NotificationHandler (RecordId INTEGER PRIMARY KEY, PrimaryId TEXT);
                    CREATE TABLE Notification (Id INTEGER PRIMARY KEY, HandlerId INTEGER, Type TEXT, Payload BLOB, ArrivalTime INTEGER);
                    INSERT INTO NotificationHandler (RecordId, PrimaryId) VALUES (1, '5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App');
                    """;
                cmd.ExecuteNonQuery();

                using var ins = conn.CreateCommand();
                ins.CommandText = "INSERT INTO Notification (Id, HandlerId, Type, Payload, ArrivalTime) VALUES (101, 1, 'toast', $p, $t);";
                ins.Parameters.AddWithValue("$p", Encoding.UTF8.GetBytes("<toast><visual><binding><text>Mesaj</text><text>Selam, proje harika olmuş!</text></binding></visual></toast>"));
                ins.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow.ToFileTime());
                ins.ExecuteNonQuery();
            }

            using var bridge = new WindowsNotificationBridgeService(dbPath);
            bridge.Start();
            var toasts = bridge.GetRecentSystemToasts();

            Assert.Single(toasts);
            Assert.Equal("WhatsApp", toasts[0].AppName);
            Assert.Equal("Mesaj", toasts[0].Title);
            Assert.Equal("Selam, proje harika olmuş!", toasts[0].Body);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void AuroraBackupService_RoundTripsFullBundle_AndRejectsCorruptFiles()
    {
        string dir = Path.Combine(Path.GetTempPath(), "aurora_bkp_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "test.aurora-backup");
        try
        {
            var prefs = new ShellRuntimePreferences(
                SmartTidyEnabled: true,
                DockAutoHide: true,
                PerMonitorWindowFilter: true,
                DockMagnificationPercent: 60,
                CrownPinned: true,
                HideNativeTaskbarWhileActive: true,
                DwmLivePreviewEnabled: true,
                SystemNotificationBridgeEnabled: true,
                WinKeyOpensOmniSearch: true,
                OmniHotkeyPreset: "Ctrl+Space");

            var bundle = new AuroraBackupBundle(
                FormatVersion: "1.1",
                ExportedAtUtc: DateTimeOffset.UtcNow,
                ShellPreferences: prefs,
                ActiveThemeId: "obsidian-pro-dark",
                ActiveProfileId: "profile-work",
                PinnedShortcuts: [new PinnedShortcut("pin-1", "Opera", "opera.exe", null, "\uE737", 0)],
                AgendaNotes: new Dictionary<string, string> { ["2026-11-19"] = "Release kontrol" },
                ScratchpadText: "Hızlı not testi",
                DropShelfPaths: [],
                SearchFrecency: new Dictionary<string, int> { ["opera.exe"] = 7 });

            AuroraBackupService.ExportToFile(path, bundle);
            var restored = AuroraBackupService.ImportFromFile(path);

            Assert.NotNull(restored);
            Assert.Equal("Ctrl+Space", restored!.ShellPreferences.OmniHotkeyPreset);
            Assert.True(restored.ShellPreferences.WinKeyOpensOmniSearch);
            Assert.True(restored.ShellPreferences.DwmLivePreviewEnabled);
            Assert.Equal("obsidian-pro-dark", restored.ActiveThemeId);
            Assert.Single(restored.PinnedShortcuts);
            Assert.Equal(7, restored.SearchFrecency["opera.exe"]);

            string corruptPath = Path.Combine(dir, "corrupt.aurora-backup");
            File.WriteAllText(corruptPath, "{not-valid-json");
            Assert.Null(AuroraBackupService.ImportFromFile(corruptPath));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void ShellRuntimePreferences_ResolvesEffectiveDockMode_WithLegacyFallback()
    {
        var legacyAutoHide = new ShellRuntimePreferences(DockAutoHide: true, DockVisibilityMode: null);
        Assert.Equal("AutoHide", legacyAutoHide.EffectiveDockMode);

        var smartDodge = new ShellRuntimePreferences(DockAutoHide: false, DockVisibilityMode: "SmartDodge");
        Assert.Equal("SmartDodge", smartDodge.EffectiveDockMode);

        var always = new ShellRuntimePreferences(DockAutoHide: false, DockVisibilityMode: "AlwaysVisible");
        Assert.Equal("AlwaysVisible", always.EffectiveDockMode);
    }

    [Fact]
    public void ShellPreferencesStore_PersistsOnboardingCompleted_AndDockMode()
    {
        string dir = Path.Combine(Path.GetTempPath(), "aurora_prefs_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var store = new ShellPreferencesStore(Path.Combine(dir, "shell_settings.json"));
            var initial = store.Load();
            Assert.False(initial.OnboardingCompleted);

            var updated = initial with
            {
                OnboardingCompleted = true,
                DockVisibilityMode = "SmartDodge",
                OmniHotkeyPreset = "Ctrl+Space"
            };
            store.Save(updated);

            var reloaded = store.Load();
            Assert.True(reloaded.OnboardingCompleted);
            Assert.Equal("SmartDodge", reloaded.EffectiveDockMode);
            Assert.Equal("Ctrl+Space", reloaded.OmniHotkeyPreset);

            var v2 = reloaded with
            {
                DockDensity = "Compact",
                DockEdge = "Left",
                DockScalePercent = 118,
                DockOffsetPercent = -32,
                HardwareOsdEnabled = true,
                AltDragEnabled = true,
                AutoCheckUpdatesEnabled = false,
                ShowClockSeconds = true,
                SnapZonesEnabled = true,
                SnapZonePreset = "studio3"
            };
            store.Save(v2);

            var reloadedV2 = store.Load();
            Assert.Equal("Compact", reloadedV2.EffectiveDockDensity);
            Assert.Equal("Left", reloadedV2.EffectiveDockEdge);
            Assert.Equal(118, reloadedV2.EffectiveDockScalePercent);
            Assert.Equal(-32, reloadedV2.EffectiveDockOffsetPercent);
            Assert.True(reloadedV2.HardwareOsdEnabled);
            Assert.True(reloadedV2.AltDragEnabled);
            Assert.False(reloadedV2.AutoCheckUpdatesEnabled);
            Assert.True(reloadedV2.ShowClockSeconds);
            Assert.True(reloadedV2.SnapZonesEnabled);
            Assert.Equal("studio3", reloadedV2.EffectiveSnapZonePreset);

            // Extreme out-of-range DockScalePercent / DockOffsetPercent clamp safely on Save/Load
            store.Save(reloadedV2 with { DockScalePercent = 999, DockOffsetPercent = -999 });
            var clampedPrefs = store.Load();
            Assert.Equal(135, clampedPrefs.EffectiveDockScalePercent);
            Assert.Equal(-100, clampedPrefs.EffectiveDockOffsetPercent);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void ClipboardVaultService_PersistsTextAndImages_Deduplicates_Pins_Searches_AndCleansOrphans()
    {
        string dir = Path.Combine(Path.GetTempPath(), "aurora_clipvault_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var vault = new ClipboardVaultService(dir);
            var first = vault.RecordText("https://aurora.local/docs", "Opera");
            Assert.NotNull(first);
            var second = vault.RecordText("Toplantı notları: v2.0 yayına hazır", "Teams");
            Assert.NotNull(second);

            // Re-copying identical text deduplicates instead of growing count
            vault.RecordText("https://aurora.local/docs", "Opera");
            Assert.Equal(2, vault.GetTotalCount());

            // Pin first item and record an image entry
            Assert.True(vault.TogglePin(first!.Id));
            byte[] fakePng = [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3, 4];
            var imgEntry = vault.RecordImage(fakePng, "AAAA", 640, 360, "Ekran Alıntısı");
            Assert.NotNull(imgEntry);
            Assert.Equal("image", imgEntry!.Kind);
            Assert.True(File.Exists(imgEntry.ImagePath!));

            // Search & filter
            var pinnedOnly = vault.GetItems(filter: "pinned");
            Assert.Single(pinnedOnly);
            Assert.Equal(first.Id, pinnedOnly[0].Id);

            var searchHits = vault.GetItems(query: "yayına");
            Assert.Single(searchHits);
            Assert.Equal(second!.Id, searchHits[0].Id);

            var imagesOnly = vault.GetItems(filter: "image");
            Assert.Single(imagesOnly);

            // Clear unpinned preserves pinned text and removes orphan image file on disk
            int cleared = vault.ClearUnpinned();
            Assert.Equal(2, cleared);
            Assert.Equal(1, vault.GetTotalCount());
            Assert.False(File.Exists(imgEntry.ImagePath!));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void AuroraUpdateService_ComparesSemVer_ParsesManifests_AndVerifiesSha256()
    {
        Assert.True(AuroraUpdateService.IsNewerVersion("2.1.0", "2.0.0"));
        Assert.True(AuroraUpdateService.IsNewerVersion("v2.0.1", "2.0.0"));
        Assert.False(AuroraUpdateService.IsNewerVersion("2.0.0", "2.0.0"));
        Assert.False(AuroraUpdateService.IsNewerVersion("1.9.9", "2.0.0"));

        Assert.Equal(AuroraUpdateService.DefaultManifestUrl, AuroraUpdateService.ResolveEffectiveManifestUrl(""));
        Assert.Equal("https://custom.local/m.json", AuroraUpdateService.ResolveEffectiveManifestUrl("https://custom.local/m.json"));

        var relativeManifest = AuroraUpdateService.ParseManifestJson("""
            {
              "version": "2.1.0",
              "releaseNotes": "Performans iyileştirmeleri",
              "downloadUrl": "Aurora-Kurulum-v2.1-x64.exe",
              "sha256": "ABCDEF"
            }
            """, new Uri("https://github.com/MuharremDidici/ProjectAurora/releases/latest/download/release-manifest.json"));
        Assert.NotNull(relativeManifest);
        Assert.Equal("2.1.0", relativeManifest!.Version);
        Assert.Equal("https://github.com/MuharremDidici/ProjectAurora/releases/latest/download/Aurora-Kurulum-v2.1-x64.exe", relativeManifest.DownloadUrl);
        Assert.True(AuroraUpdateService.IsDirectInstallerDownloadUrl(relativeManifest.DownloadUrl));
        Assert.Equal("ABCDEF", relativeManifest.Sha256);

        var ghManifest = AuroraUpdateService.ParseManifestJson("""
            {
              "tag_name": "v2.2.0",
              "body": "GitHub Release Notu",
              "html_url": "https://github.com/MuharremDidici/ProjectAurora/releases/tag/v2.2.0",
              "assets": [
                { "name": "release-manifest.json", "browser_download_url": "https://github.com/MuharremDidici/ProjectAurora/releases/download/v2.2.0/release-manifest.json" },
                { "name": "Aurora-Kurulum-v2.2-x64.exe", "browser_download_url": "https://github.com/MuharremDidici/ProjectAurora/releases/download/v2.2.0/Aurora-Kurulum-v2.2-x64.exe" }
              ]
            }
            """);
        Assert.NotNull(ghManifest);
        Assert.Equal("2.2.0", ghManifest!.Version);
        Assert.Equal("https://github.com/MuharremDidici/ProjectAurora/releases/download/v2.2.0/Aurora-Kurulum-v2.2-x64.exe", ghManifest.DownloadUrl);
        Assert.True(AuroraUpdateService.IsDirectInstallerDownloadUrl(ghManifest.DownloadUrl));

        string tmpFile = Path.Combine(Path.GetTempPath(), "aurora_sha_" + Guid.NewGuid().ToString("N")[..8] + ".bin");
        try
        {
            File.WriteAllBytes(tmpFile, Encoding.UTF8.GetBytes("aurora-v2"));
            string expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("aurora-v2")));
            Assert.True(AuroraUpdateService.VerifyFileSha256(tmpFile, expectedHash));
            Assert.False(AuroraUpdateService.VerifyFileSha256(tmpFile, "0000"));
        }
        finally
        {
            try { File.Delete(tmpFile); } catch { }
        }
    }
}
