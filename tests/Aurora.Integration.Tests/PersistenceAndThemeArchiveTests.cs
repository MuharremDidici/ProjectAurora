using System.IO.Compression;
using System.Text;
using Aurora.Application.Services;
using Aurora.Core.Events;
using Aurora.Core.Models;
using Aurora.Core.Tokens;
using Aurora.Infrastructure.Logging;
using Aurora.Infrastructure.Persistence;
using Aurora.Infrastructure.Themes;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aurora.Integration.Tests;

public sealed class PersistenceAndThemeArchiveTests
{
    [Fact]
    public async Task SqliteRepository_PersistsPreferencesProfilesNotifications_AndExportsBackupJson()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"aurora_int_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var logger = new PrivacySafeFileLogger(Path.Combine(tempDir, "logs"));
            var dbPath = Path.Combine(tempDir, "aurora.db");
            var repo = new SqliteAuroraRepository(logger, dbPath);

            using var bus = new BoundedEventBus();
            var store = new AuroraStateStore(repo, bus, logger);
            await store.InitializeAsync(safeMode: false);

            await store.SwitchProfileAsync("profile-gaming");
            await store.AddNotificationAsync("TestMod", NotificationCategory.Reminder, NotificationSeverity.Info, "Başlık", "İçerik", showToast: false);

            var jsonBackup = await repo.ExportAllSettingsJsonAsync();
            Assert.Contains("profile-gaming", jsonBackup);

            // Re-open in a fresh store instance and confirm persistence across restarts
            var store2 = new AuroraStateStore(repo, bus, logger);
            await store2.InitializeAsync(safeMode: false);
            Assert.Equal("profile-gaming", store2.ActiveProfile.Id);
            Assert.Equal(BuiltInThemes.NebulaDark.Id, store2.CommittedTheme.Id);
            Assert.NotEmpty(store2.Notifications);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task SqliteRepository_RecoversCleanlyAndPreservesCorruptDatabaseFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"aurora_corrupt_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var dbPath = Path.Combine(tempDir, "aurora.db");
            await File.WriteAllBytesAsync(dbPath, Encoding.UTF8.GetBytes("NOT_A_VALID_SQLITE_HEADER_DATA"));

            var logger = new PrivacySafeFileLogger(Path.Combine(tempDir, "logs"));
            var repo = new SqliteAuroraRepository(logger, dbPath);
            await repo.InitializeAsync();

            Assert.True(repo.EnteredRecoveryOnInit);
            var preservedBackups = Directory.GetFiles(tempDir, "*.recovery.*.bak");
            Assert.NotEmpty(preservedBackups);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ThemePackageArchive_ExportsValidSignedArchive_AndRejectsMaliciousZipEntries()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"aurora_pkg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var logger = new PrivacySafeFileLogger(Path.Combine(tempDir, "logs"));
            var svc = new ThemePackageArchiveService(logger);

            var validPkgPath = Path.Combine(tempDir, "obsidian.auroratheme");
            var exp = await svc.ExportPackageAsync(BuiltInThemes.ObsidianProDark, validPkgPath, "Aurora QA");
            Assert.True(exp.IsSuccess);

            var imp = await svc.ImportPackageAsync(validPkgPath);
            Assert.True(imp.IsSuccess);
            Assert.Equal(BuiltInThemes.ObsidianProDark.Id, imp.Value!.Theme.Id);

            // Craft malicious archive containing an executable file
            var evilZipPath = Path.Combine(tempDir, "evil.auroratheme");
            using (var fs = new FileStream(evilZipPath, FileMode.CreateNew))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("dropper.exe");
                await using var s = entry.Open();
                await s.WriteAsync(new byte[] { 0x4D, 0x5A });
            }

            var rejected = await svc.ImportPackageAsync(evilZipPath);
            Assert.False(rejected.IsSuccess);
            Assert.Equal("THEME_EXECUTABLE_CONTENT", rejected.Error!.Code);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void PrivacySafeFileLogger_RedactsUserPathsAndEmails()
    {
        var redacted = PrivacySafeFileLogger.RedactSensitivePatterns(
            @"Failed opening C:\Users\SecretPerson\Documents\file.txt for user@example.com");

        Assert.DoesNotContain("SecretPerson", redacted);
        Assert.DoesNotContain("user@example.com", redacted);
        Assert.Contains("[REDACTED_EMAIL]", redacted);
    }

    [Fact]
    public void ShellPreferencesStore_PersistsAllFlagsAtomicallyAndRecoversFromCorruption()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"aurora_shell_prefs_{Guid.NewGuid():N}.json");
        try
        {
            var store = new ShellPreferencesStore(tempFile);
            var saved = new ShellRuntimePreferences(
                SmartTidyEnabled: false,
                DockAutoHide: true,
                PerMonitorWindowFilter: true,
                DockMagnificationPercent: 64,
                CrownPinned: true,
                HideNativeTaskbarWhileActive: false);

            store.Save(saved);
            var reloaded = store.Load();

            Assert.Equal(saved, reloaded);

            // Corrupt the JSON file and confirm safe default fallback
            File.WriteAllText(tempFile, "{ corrupt json !!!");
            var recovered = store.Load();
            Assert.True(recovered.SmartTidyEnabled);
            Assert.False(recovered.DockAutoHide);
            Assert.Equal(48, recovered.DockMagnificationPercent);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
