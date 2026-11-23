using System.Text.Json;
using Aurora.Application.Services;
using Aurora.Core.Contracts;
using Aurora.Core.Models;
using Aurora.Core.Primitives;
using Aurora.Core.Tokens;
using Microsoft.Data.Sqlite;

namespace Aurora.Infrastructure.Persistence;

public sealed class SqliteAuroraRepository : IAuroraRepository
{
    public const int TargetSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _dbFilePath;
    private readonly IAuroraLogger _logger;
    private readonly string _connectionString;
    public bool EnteredRecoveryOnInit { get; private set; }

    public SqliteAuroraRepository(IAuroraLogger logger, string? customDatabasePath = null)
    {
        _logger = logger;
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProjectAurora",
            "data");
        _dbFilePath = customDatabasePath ?? Path.Combine(baseDir, "aurora.db");
        Directory.CreateDirectory(Path.GetDirectoryName(_dbFilePath)!);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true
        }.ToString();
    }

    public string DatabaseFilePath => _dbFilePath;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            await MigrateDatabaseAsync(ct);
        }
        catch (Exception ex)
        {
            EnteredRecoveryOnInit = true;
            _logger.Error("Infrastructure", "DB_MIGRATION_RECOVERY", "SQLite initialization failed; preserving original DB and recreating clean store.", ex);

            if (File.Exists(_dbFilePath))
            {
                var preservePath = $"{_dbFilePath}.recovery.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.bak";
                try
                {
                    SqliteConnection.ClearAllPools();
                    File.Move(_dbFilePath, preservePath, overwrite: true);
                }
                catch
                {
                    // Keep going in recovery mode
                }
            }

            await MigrateDatabaseAsync(ct);
        }
    }

    private async Task MigrateDatabaseAsync(CancellationToken ct)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
            await pragma.ExecuteNonQueryAsync(ct);
        }

        await using (var metaCmd = conn.CreateCommand())
        {
            metaCmd.CommandText = """
                CREATE TABLE IF NOT EXISTS SchemaMigrations (
                    Version INTEGER PRIMARY KEY,
                    AppliedAtUtc TEXT NOT NULL
                );
                """;
            await metaCmd.ExecuteNonQueryAsync(ct);
        }

        int currentVersion = 0;
        await using (var verCmd = conn.CreateCommand())
        {
            verCmd.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM SchemaMigrations;";
            var scalar = await verCmd.ExecuteScalarAsync(ct);
            currentVersion = Convert.ToInt32(scalar);
        }

        if (currentVersion >= TargetSchemaVersion)
            return;

        if (currentVersion > 0 && File.Exists(_dbFilePath))
        {
            var backupPath = $"{_dbFilePath}.bak.v{currentVersion}";
            File.Copy(_dbFilePath, backupPath, overwrite: true);
        }

        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);
        await using (var ddl = conn.CreateCommand())
        {
            ddl.Transaction = tx;
            ddl.CommandText = """
                CREATE TABLE IF NOT EXISTS KeyValueState (
                    StateKey TEXT PRIMARY KEY,
                    JsonPayload TEXT NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS PinnedShortcuts (
                    Id TEXT PRIMARY KEY,
                    OrderIndex INTEGER NOT NULL,
                    JsonPayload TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS WorkspaceProfiles (
                    Id TEXT PRIMARY KEY,
                    OrderIndex INTEGER NOT NULL,
                    JsonPayload TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS NotificationHistory (
                    Id TEXT PRIMARY KEY,
                    CreatedAtUtc TEXT NOT NULL,
                    JsonPayload TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS CustomThemes (
                    Id TEXT PRIMARY KEY,
                    UpdatedAtUtc TEXT NOT NULL,
                    JsonPayload TEXT NOT NULL
                );

                INSERT INTO SchemaMigrations (Version, AppliedAtUtc) VALUES (1, $now);
                """;
            ddl.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            await ddl.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        _logger.Info("Infrastructure", "DB_SCHEMA_READY", $"SQLite schema v{TargetSchemaVersion} verified.");
    }

    public async Task<UserPreferences> LoadPreferencesAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT JsonPayload FROM KeyValueState WHERE StateKey = 'user_preferences';";
        var raw = await cmd.ExecuteScalarAsync(ct) as string;
        if (string.IsNullOrWhiteSpace(raw))
            return UserPreferences.Default;

        return JsonSerializer.Deserialize<UserPreferences>(raw, JsonOpts) ?? UserPreferences.Default;
    }

    public async Task SavePreferencesAsync(UserPreferences preferences, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO KeyValueState (StateKey, JsonPayload, UpdatedAtUtc)
            VALUES ('user_preferences', $json, $now)
            ON CONFLICT(StateKey) DO UPDATE SET JsonPayload = excluded.JsonPayload, UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(preferences, JsonOpts));
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<PinnedShortcut>> LoadPinnedShortcutsAsync(CancellationToken ct = default)
    {
        var list = new List<PinnedShortcut>();
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT JsonPayload FROM PinnedShortcuts ORDER BY OrderIndex ASC;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<PinnedShortcut>(reader.GetString(0), JsonOpts);
            if (item is not null)
                list.Add(item);
        }

        return list;
    }

    public async Task SavePinnedShortcutsAsync(IReadOnlyList<PinnedShortcut> shortcuts, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        await using (var clear = conn.CreateCommand())
        {
            clear.Transaction = tx;
            clear.CommandText = "DELETE FROM PinnedShortcuts;";
            await clear.ExecuteNonQueryAsync(ct);
        }

        for (int i = 0; i < shortcuts.Count; i++)
        {
            await using var ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = "INSERT INTO PinnedShortcuts (Id, OrderIndex, JsonPayload) VALUES ($id, $ord, $json);";
            ins.Parameters.AddWithValue("$id", shortcuts[i].Id);
            ins.Parameters.AddWithValue("$ord", i);
            ins.Parameters.AddWithValue("$json", JsonSerializer.Serialize(shortcuts[i], JsonOpts));
            await ins.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<WorkspaceProfile>> LoadProfilesAsync(CancellationToken ct = default)
    {
        var list = new List<WorkspaceProfile>();
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT JsonPayload FROM WorkspaceProfiles ORDER BY OrderIndex ASC;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<WorkspaceProfile>(reader.GetString(0), JsonOpts);
            if (item is not null)
                list.Add(item);
        }

        return list;
    }

    public async Task SaveProfilesAsync(IReadOnlyList<WorkspaceProfile> profiles, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        await using (var clear = conn.CreateCommand())
        {
            clear.Transaction = tx;
            clear.CommandText = "DELETE FROM WorkspaceProfiles;";
            await clear.ExecuteNonQueryAsync(ct);
        }

        for (int i = 0; i < profiles.Count; i++)
        {
            await using var ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = "INSERT INTO WorkspaceProfiles (Id, OrderIndex, JsonPayload) VALUES ($id, $ord, $json);";
            ins.Parameters.AddWithValue("$id", profiles[i].Id);
            ins.Parameters.AddWithValue("$ord", i);
            ins.Parameters.AddWithValue("$json", JsonSerializer.Serialize(profiles[i], JsonOpts));
            await ins.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<AuroraNotification>> LoadNotificationsAsync(CancellationToken ct = default)
    {
        var list = new List<AuroraNotification>();
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT JsonPayload FROM NotificationHistory ORDER BY CreatedAtUtc DESC LIMIT 200;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<AuroraNotification>(reader.GetString(0), JsonOpts);
            if (item is not null)
                list.Add(item);
        }

        return list;
    }

    public async Task SaveNotificationsAsync(IReadOnlyList<AuroraNotification> notifications, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        await using (var clear = conn.CreateCommand())
        {
            clear.Transaction = tx;
            clear.CommandText = "DELETE FROM NotificationHistory;";
            await clear.ExecuteNonQueryAsync(ct);
        }

        foreach (var n in notifications.Take(200))
        {
            await using var ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = "INSERT INTO NotificationHistory (Id, CreatedAtUtc, JsonPayload) VALUES ($id, $ts, $json);";
            ins.Parameters.AddWithValue("$id", n.Id);
            ins.Parameters.AddWithValue("$ts", n.CreatedAtUtc.ToString("O"));
            ins.Parameters.AddWithValue("$json", JsonSerializer.Serialize(n, JsonOpts));
            await ins.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<AuroraThemeTokens>> LoadCustomThemesAsync(CancellationToken ct = default)
    {
        var list = new List<AuroraThemeTokens>();
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT JsonPayload FROM CustomThemes ORDER BY UpdatedAtUtc DESC;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<AuroraThemeTokens>(reader.GetString(0), JsonOpts);
            if (item is not null)
                list.Add(item);
        }

        return list;
    }

    public async Task SaveCustomThemeAsync(AuroraThemeTokens theme, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO CustomThemes (Id, UpdatedAtUtc, JsonPayload)
            VALUES ($id, $now, $json)
            ON CONFLICT(Id) DO UPDATE SET UpdatedAtUtc = excluded.UpdatedAtUtc, JsonPayload = excluded.JsonPayload;
            """;
        cmd.Parameters.AddWithValue("$id", theme.Id);
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(theme, JsonOpts));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private sealed record FullBackupBundle(
        string SchemaVersion,
        DateTimeOffset ExportedAtUtc,
        UserPreferences Preferences,
        IReadOnlyList<PinnedShortcut> PinnedShortcuts,
        IReadOnlyList<WorkspaceProfile> Profiles,
        IReadOnlyList<AuroraThemeTokens> CustomThemes);

    public async Task<string> ExportAllSettingsJsonAsync(CancellationToken ct = default)
    {
        var prefs = await LoadPreferencesAsync(ct);
        var pins = await LoadPinnedShortcutsAsync(ct);
        var profiles = await LoadProfilesAsync(ct);
        var customThemes = await LoadCustomThemesAsync(ct);

        var bundle = new FullBackupBundle(
            "1.0",
            DateTimeOffset.UtcNow,
            prefs,
            pins.Count > 0 ? pins : DefaultCatalogFactory.CreateDefaultPinnedShortcuts(),
            profiles.Count > 0 ? profiles : DefaultCatalogFactory.CreateDefaultProfiles(),
            customThemes);

        return JsonSerializer.Serialize(bundle, JsonOpts);
    }

    public async Task<AuroraResult> ImportAllSettingsJsonAsync(string json, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 4 * 1024 * 1024)
            return AuroraResult.Fail("SETTINGS_INVALID_SIZE", "Ayar yedek dosyası boş veya izin verilen sınırı aşıyor.");

        try
        {
            var bundle = JsonSerializer.Deserialize<FullBackupBundle>(json, JsonOpts);
            if (bundle is null || bundle.SchemaVersion != "1.0" || bundle.Preferences is null)
                return AuroraResult.Fail("SETTINGS_INVALID_SCHEMA", "Geçersiz ayar yedeği şema sürümü.");

            await SavePreferencesAsync(bundle.Preferences, ct);
            if (bundle.PinnedShortcuts is { Count: > 0 })
                await SavePinnedShortcutsAsync(bundle.PinnedShortcuts, ct);
            if (bundle.Profiles is { Count: > 0 })
                await SaveProfilesAsync(bundle.Profiles, ct);
            if (bundle.CustomThemes is not null)
            {
                foreach (var t in bundle.CustomThemes)
                    await SaveCustomThemeAsync(t, ct);
            }

            _logger.Info("Infrastructure", "SETTINGS_IMPORTED", "Full settings backup imported successfully.");
            return AuroraResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.Error("Infrastructure", "SETTINGS_IMPORT_FAILED", "Failed to parse imported settings bundle.", ex);
            return AuroraResult.Fail("SETTINGS_PARSE_ERROR", "Ayar dosyası okunamadı veya bozuk.");
        }
    }
}
