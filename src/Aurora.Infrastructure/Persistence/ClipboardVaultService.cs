using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Aurora.Infrastructure.Persistence;

public sealed record ClipboardVaultEntry(
    long Id,
    string Kind,            // "text" | "image"
    string TextContent,
    string PreviewText,
    string? ImagePath,
    string? ThumbBase64,
    int ImageWidth,
    int ImageHeight,
    string SourceApp,
    bool IsPinned,
    DateTimeOffset LastCopiedUtc);

/// <summary>
/// Persistent, searchable, deduplicated SQLite Clipboard Vault supporting both
/// text snippets and full-resolution images with compact thumbnails, pinning,
/// and automatic pruning with orphan image cleanup.
/// </summary>
public sealed class ClipboardVaultService
{
    private const int MaxUnpinnedEntries = 200;
    private readonly string _dbPath;
    private readonly string _imagesDir;
    private readonly string _connectionString;
    private readonly object _gate = new();

    public ClipboardVaultService(string? customDataDirectory = null)
    {
        string baseDir = !string.IsNullOrWhiteSpace(customDataDirectory)
            ? customDataDirectory
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProjectAurora",
                "data");

        Directory.CreateDirectory(baseDir);
        _dbPath = Path.Combine(baseDir, "clipboard_vault.db");
        _imagesDir = Path.Combine(baseDir, "clip_images");
        Directory.CreateDirectory(_imagesDir);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();

        EnsureSchema();
    }

    public string DatabasePath => _dbPath;

    private void EnsureSchema()
    {
        lock (_gate)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                PRAGMA journal_mode = WAL;
                CREATE TABLE IF NOT EXISTS ClipboardItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ContentHash TEXT NOT NULL UNIQUE,
                    Kind TEXT NOT NULL,
                    TextContent TEXT NOT NULL DEFAULT '',
                    PreviewText TEXT NOT NULL DEFAULT '',
                    ImagePath TEXT,
                    ThumbBase64 TEXT,
                    ImageWidth INTEGER NOT NULL DEFAULT 0,
                    ImageHeight INTEGER NOT NULL DEFAULT 0,
                    SourceApp TEXT NOT NULL DEFAULT '',
                    IsPinned INTEGER NOT NULL DEFAULT 0,
                    LastCopiedUtc TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_ClipboardItems_Order
                    ON ClipboardItems (IsPinned DESC, LastCopiedUtc DESC);
                """;
            cmd.ExecuteNonQuery();
        }
    }

    public ClipboardVaultEntry? RecordText(string rawText, string? sourceApp = null)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return null;

        string normalized = rawText.Length > 64_000 ? rawText[..64_000] : rawText;
        string trimmed = normalized.Trim();
        if (trimmed.Length == 0)
            return null;

        string preview = BuildPreview(trimmed);
        string hash = ComputeSha256Hex("T:" + normalized);
        string nowIso = DateTimeOffset.UtcNow.ToString("O");
        string app = string.IsNullOrWhiteSpace(sourceApp) ? "Pano" : sourceApp.Trim();

        lock (_gate)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO ClipboardItems (ContentHash, Kind, TextContent, PreviewText, SourceApp, IsPinned, LastCopiedUtc)
                VALUES ($h, 'text', $txt, $prev, $app, 0, $ts)
                ON CONFLICT(ContentHash) DO UPDATE SET
                    LastCopiedUtc = excluded.LastCopiedUtc,
                    SourceApp = CASE WHEN excluded.SourceApp != '' THEN excluded.SourceApp ELSE ClipboardItems.SourceApp END;
                """;
            cmd.Parameters.AddWithValue("$h", hash);
            cmd.Parameters.AddWithValue("$txt", normalized);
            cmd.Parameters.AddWithValue("$prev", preview);
            cmd.Parameters.AddWithValue("$app", app);
            cmd.Parameters.AddWithValue("$ts", nowIso);
            cmd.ExecuteNonQuery();

            PruneLocked(conn);
            return FindByHashLocked(conn, hash);
        }
    }

    public ClipboardVaultEntry? RecordImage(byte[] pngBytes, string? thumbBase64, int width, int height, string? sourceApp = null)
    {
        if (pngBytes is null || pngBytes.Length == 0 || width <= 0 || height <= 0)
            return null;

        string hash = ComputeSha256Hex(pngBytes);
        string imgFile = Path.Combine(_imagesDir, hash[..24] + ".png");
        string nowIso = DateTimeOffset.UtcNow.ToString("O");
        string app = string.IsNullOrWhiteSpace(sourceApp) ? "Ekran Görüntüsü" : sourceApp.Trim();
        string preview = $"Görsel ({width}×{height} px)";

        lock (_gate)
        {
            if (!File.Exists(imgFile))
            {
                try { File.WriteAllBytes(imgFile, pngBytes); }
                catch { return null; }
            }

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO ClipboardItems (ContentHash, Kind, TextContent, PreviewText, ImagePath, ThumbBase64, ImageWidth, ImageHeight, SourceApp, IsPinned, LastCopiedUtc)
                VALUES ($h, 'image', '', $prev, $path, $thumb, $w, $ht, $app, 0, $ts)
                ON CONFLICT(ContentHash) DO UPDATE SET
                    LastCopiedUtc = excluded.LastCopiedUtc,
                    ImagePath = excluded.ImagePath,
                    ThumbBase64 = COALESCE(excluded.ThumbBase64, ClipboardItems.ThumbBase64);
                """;
            cmd.Parameters.AddWithValue("$h", hash);
            cmd.Parameters.AddWithValue("$prev", preview);
            cmd.Parameters.AddWithValue("$path", imgFile);
            cmd.Parameters.AddWithValue("$thumb", (object?)thumbBase64 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$w", width);
            cmd.Parameters.AddWithValue("$ht", height);
            cmd.Parameters.AddWithValue("$app", app);
            cmd.Parameters.AddWithValue("$ts", nowIso);
            cmd.ExecuteNonQuery();

            PruneLocked(conn);
            return FindByHashLocked(conn, hash);
        }
    }

    public IReadOnlyList<ClipboardVaultEntry> GetItems(string? query = null, string filter = "all", int limit = 60)
    {
        limit = Math.Clamp(limit, 1, 200);
        string q = (query ?? string.Empty).Trim();
        string f = (filter ?? "all").Trim().ToLowerInvariant();

        lock (_gate)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();

            var where = new List<string>();
            if (f == "pinned")
                where.Add("IsPinned = 1");
            else if (f == "image")
                where.Add("Kind = 'image'");
            else if (f == "text")
                where.Add("Kind = 'text'");

            if (q.Length > 0)
            {
                where.Add("(TextContent LIKE $q OR PreviewText LIKE $q OR SourceApp LIKE $q)");
                cmd.Parameters.AddWithValue("$q", "%" + q + "%");
            }

            string whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;
            cmd.CommandText = $"""
                SELECT Id, Kind, TextContent, PreviewText, ImagePath, ThumbBase64, ImageWidth, ImageHeight, SourceApp, IsPinned, LastCopiedUtc
                FROM ClipboardItems
                {whereSql}
                ORDER BY IsPinned DESC, LastCopiedUtc DESC
                LIMIT $lim;
                """;
            cmd.Parameters.AddWithValue("$lim", limit);

            var list = new List<ClipboardVaultEntry>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadEntry(reader));
            return list;
        }
    }

    public ClipboardVaultEntry? GetById(long id)
    {
        lock (_gate)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT Id, Kind, TextContent, PreviewText, ImagePath, ThumbBase64, ImageWidth, ImageHeight, SourceApp, IsPinned, LastCopiedUtc
                FROM ClipboardItems WHERE Id = $id LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadEntry(reader) : null;
        }
    }

    public bool TogglePin(long id)
    {
        lock (_gate)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE ClipboardItems SET IsPinned = CASE WHEN IsPinned = 1 THEN 0 ELSE 1 END WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    public bool Delete(long id)
    {
        string? imgPath = null;
        lock (_gate)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using (var sel = conn.CreateCommand())
            {
                sel.CommandText = "SELECT ImagePath FROM ClipboardItems WHERE Id = $id;";
                sel.Parameters.AddWithValue("$id", id);
                imgPath = sel.ExecuteScalar() as string;
            }
            using var del = conn.CreateCommand();
            del.CommandText = "DELETE FROM ClipboardItems WHERE Id = $id;";
            del.Parameters.AddWithValue("$id", id);
            int affected = del.ExecuteNonQuery();
            TryDeleteImageFile(imgPath);
            return affected > 0;
        }
    }

    public int ClearUnpinned()
    {
        lock (_gate)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var toDelete = new List<string>();
            using (var sel = conn.CreateCommand())
            {
                sel.CommandText = "SELECT ImagePath FROM ClipboardItems WHERE IsPinned = 0 AND ImagePath IS NOT NULL;";
                using var r = sel.ExecuteReader();
                while (r.Read())
                {
                    if (!r.IsDBNull(0)) toDelete.Add(r.GetString(0));
                }
            }
            using var del = conn.CreateCommand();
            del.CommandText = "DELETE FROM ClipboardItems WHERE IsPinned = 0;";
            int count = del.ExecuteNonQuery();
            foreach (var p in toDelete) TryDeleteImageFile(p);
            return count;
        }
    }

    public int GetTotalCount()
    {
        lock (_gate)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM ClipboardItems;";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    private void PruneLocked(SqliteConnection conn)
    {
        var stalePaths = new List<string>();
        var staleIds = new List<long>();

        using (var sel = conn.CreateCommand())
        {
            sel.CommandText = """
                SELECT Id, ImagePath FROM ClipboardItems
                WHERE IsPinned = 0
                ORDER BY LastCopiedUtc DESC
                LIMIT -1 OFFSET $max;
                """;
            sel.Parameters.AddWithValue("$max", MaxUnpinnedEntries);
            using var r = sel.ExecuteReader();
            while (r.Read())
            {
                staleIds.Add(r.GetInt64(0));
                if (!r.IsDBNull(1)) stalePaths.Add(r.GetString(1));
            }
        }

        if (staleIds.Count == 0)
            return;

        using var del = conn.CreateCommand();
        del.CommandText = $"DELETE FROM ClipboardItems WHERE Id IN ({string.Join(',', staleIds)});";
        del.ExecuteNonQuery();

        foreach (var p in stalePaths)
            TryDeleteImageFile(p);
    }

    private static ClipboardVaultEntry? FindByHashLocked(SqliteConnection conn, string hash)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Kind, TextContent, PreviewText, ImagePath, ThumbBase64, ImageWidth, ImageHeight, SourceApp, IsPinned, LastCopiedUtc
            FROM ClipboardItems WHERE ContentHash = $h LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$h", hash);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadEntry(reader) : null;
    }

    private static ClipboardVaultEntry ReadEntry(SqliteDataReader r) => new(
        Id: r.GetInt64(0),
        Kind: r.GetString(1),
        TextContent: r.GetString(2),
        PreviewText: r.GetString(3),
        ImagePath: r.IsDBNull(4) ? null : r.GetString(4),
        ThumbBase64: r.IsDBNull(5) ? null : r.GetString(5),
        ImageWidth: r.GetInt32(6),
        ImageHeight: r.GetInt32(7),
        SourceApp: r.GetString(8),
        IsPinned: r.GetInt32(9) == 1,
        LastCopiedUtc: DateTimeOffset.TryParse(r.GetString(10), out var dt) ? dt : DateTimeOffset.UtcNow);

    private static string BuildPreview(string text)
    {
        var collapsed = string.Join(' ', text.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return collapsed.Length > 140 ? collapsed[..139] + "…" : collapsed;
    }

    private static string ComputeSha256Hex(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

    private static string ComputeSha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private static void TryDeleteImageFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
