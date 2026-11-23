using System.Text;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;

namespace Aurora.Infrastructure.Notifications;

public sealed record SystemToastNotification(
    long NotificationId,
    string AppName,
    string Title,
    string Body,
    DateTimeOffset ArrivedAtUtc);

/// <summary>
/// Non-intrusive, read-only Windows 11 Action Center / WPN toast listener.
/// Watches %LOCALAPPDATA%\Microsoft\Windows\Notifications\wpndatabase.db via FileSystemWatcher
/// (0% idle CPU) and snapshots safely without locking WpnUserService.
/// </summary>
public sealed class WindowsNotificationBridgeService : IDisposable
{
    private readonly string _wpnDir;
    private readonly string _wpnDbPath;
    private readonly string _snapshotDir;
    private readonly object _syncLock = new();
    private FileSystemWatcher? _watcher;
    private System.Threading.Timer? _debounceTimer;
    private long _highWaterArrivalTime;
    private readonly HashSet<long> _seenIds = [];
    private List<SystemToastNotification> _recent = [];
    private bool _initialized;
    private bool _disposed;

    public event EventHandler<SystemToastNotification>? NewToastArrived;

    public WindowsNotificationBridgeService(string? customWpnDbPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customWpnDbPath))
        {
            _wpnDbPath = customWpnDbPath;
            _wpnDir = Path.GetDirectoryName(customWpnDbPath) ?? string.Empty;
        }
        else
        {
            _wpnDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Windows", "Notifications");
            _wpnDbPath = Path.Combine(_wpnDir, "wpndatabase.db");
        }

        _snapshotDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProjectAurora", "cache", "wpn_snap");
    }

    public bool IsEnabled { get; set; } = true;

    public IReadOnlyList<SystemToastNotification> GetRecentSystemToasts()
    {
        lock (_syncLock)
        {
            return [.. _recent];
        }
    }

    public void Start()
    {
        if (_disposed || !File.Exists(_wpnDbPath))
            return;

        // Baseline read so existing historical notifications populate history without re-toasting
        PollSnapshot(raiseEvents: false);

        try
        {
            if (Directory.Exists(_wpnDir))
            {
                _watcher = new FileSystemWatcher(_wpnDir, "wpndatabase.db*")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };
                _watcher.Changed += OnWpnFileTouched;
                _watcher.Created += OnWpnFileTouched;
            }
        }
        catch
        {
            // Graceful fallback if directory watch is unavailable
        }
    }

    public void PollNow(bool raiseEvents = true) => PollSnapshot(raiseEvents);

    private void OnWpnFileTouched(object sender, FileSystemEventArgs e)
    {
        if (_disposed || !IsEnabled)
            return;

        lock (_syncLock)
        {
            _debounceTimer ??= new System.Threading.Timer(_ => PollSnapshot(raiseEvents: true), null, Timeout.Infinite, Timeout.Infinite);
            _debounceTimer.Change(420, Timeout.Infinite);
        }
    }

    private void PollSnapshot(bool raiseEvents)
    {
        if (_disposed || !IsEnabled || !File.Exists(_wpnDbPath))
            return;

        string snapPrefix = Path.Combine(_snapshotDir, $"wpn_{Environment.ProcessId}");
        string snapDb = snapPrefix + ".db";
        string snapWal = snapDb + "-wal";
        string snapShm = snapDb + "-shm";

        List<SystemToastNotification> newlyArrived = [];

        lock (_syncLock)
        {
            try
            {
                Directory.CreateDirectory(_snapshotDir);
                CopyFileSharedRead(_wpnDbPath, snapDb);
                CopyFileSharedReadIfExists(_wpnDbPath + "-wal", snapWal);
                CopyFileSharedReadIfExists(_wpnDbPath + "-shm", snapShm);

                var parsed = ReadToastsFromSqlite(snapDb);
                _recent = parsed;

                if (!_initialized)
                {
                    _initialized = true;
                    foreach (var t in parsed)
                    {
                        _seenIds.Add(t.NotificationId);
                        long ft = t.ArrivedAtUtc.ToFileTime();
                        if (ft > _highWaterArrivalTime) _highWaterArrivalTime = ft;
                    }
                }
                else
                {
                    foreach (var t in parsed.OrderBy(x => x.ArrivedAtUtc))
                    {
                        long ft = t.ArrivedAtUtc.ToFileTime();
                        if (_seenIds.Add(t.NotificationId) && ft >= _highWaterArrivalTime - TimeSpan.FromSeconds(5).Ticks)
                        {
                            if (ft > _highWaterArrivalTime) _highWaterArrivalTime = ft;
                            if (raiseEvents)
                                newlyArrived.Add(t);
                        }
                    }

                    if (_seenIds.Count > 200)
                    {
                        var keep = parsed.Select(x => x.NotificationId).ToHashSet();
                        _seenIds.IntersectWith(keep);
                    }
                }
            }
            catch
            {
                // Transient SQLite WAL checkpoint contention; ignore safely
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                TryDeleteQuietly(snapDb);
                TryDeleteQuietly(snapWal);
                TryDeleteQuietly(snapShm);
            }
        }

        foreach (var toast in newlyArrived)
        {
            try { NewToastArrived?.Invoke(this, toast); } catch { }
        }
    }

    internal static List<SystemToastNotification> ReadToastsFromSqlite(string dbPath)
    {
        var list = new List<SystemToastNotification>(12);
        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };

        using var conn = new SqliteConnection(csb.ToString());
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT n.Id, n.ArrivalTime, COALESCE(nh.PrimaryId, ''), n.Payload
            FROM Notification n
            LEFT JOIN NotificationHandler nh ON n.HandlerId = nh.RecordId
            WHERE n.Type IN ('toast', 'toastCondensed')
              AND n.Payload IS NOT NULL
            ORDER BY n.ArrivalTime DESC
            LIMIT 12;
            """;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            long id = reader.GetInt64(0);
            long arrivalFileTime = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
            string primaryId = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            byte[]? payloadBytes = reader.IsDBNull(3) ? null : (byte[])reader[3];

            if (payloadBytes is not { Length: > 0 })
                continue;

            var (title, body) = ParseToastXmlPayload(payloadBytes);
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
                continue;

            string appName = FormatFriendlyAppName(primaryId);
            if (string.IsNullOrWhiteSpace(title)) title = appName;

            DateTimeOffset arrivedUtc = arrivalFileTime > 0
                ? SafeFromFileTimeUtc(arrivalFileTime)
                : DateTimeOffset.UtcNow;

            list.Add(new SystemToastNotification(id, appName, title, body, arrivedUtc));
        }

        return list;
    }

    public static (string Title, string Body) ParseToastXmlPayload(byte[] payloadBytes)
    {
        try
        {
            string xml = Encoding.UTF8.GetString(payloadBytes).Trim('\0', ' ', '\r', '\n');
            int lt = xml.IndexOf('<');
            if (lt > 0) xml = xml[lt..];

            var doc = XDocument.Parse(xml, LoadOptions.None);
            var lines = doc.Descendants()
                .Where(e => string.Equals(e.Name.LocalName, "text", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Value?.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (lines.Count == 0)
                return (string.Empty, string.Empty);

            string title = Truncate(lines[0]!, 90);
            string body = lines.Count > 1
                ? Truncate(string.Join(" — ", lines.Skip(1).Take(2)), 180)
                : string.Empty;
            return (title, body);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    public static string FormatFriendlyAppName(string primaryId)
    {
        if (string.IsNullOrWhiteSpace(primaryId))
            return "Windows";

        string lower = primaryId.ToLowerInvariant();
        if (lower.Contains("whatsapp")) return "WhatsApp";
        if (lower.Contains("teams")) return "Microsoft Teams";
        if (lower.Contains("outlook") || lower.Contains("windowscommunicationsapps")) return "Outlook";
        if (lower.Contains("discord")) return "Discord";
        if (lower.Contains("telegram")) return "Telegram";
        if (lower.Contains("slack")) return "Slack";
        if (lower.Contains("spotify")) return "Spotify";
        if (lower.Contains("opera")) return "Opera";
        if (lower.Contains("brave")) return "Brave";
        if (lower.Contains("chrome")) return "Chrome";
        if (lower.Contains("msedge") || lower.Contains("edge")) return "Edge";
        if (lower.Contains("sechealthui") || lower.Contains("defender") || lower.Contains("security")) return "Windows Güvenliği";
        if (lower.Contains("yourphone") || lower.Contains("phonelink")) return "Telefon Bağlantısı";
        if (lower.Contains("steam")) return "Steam";
        if (lower.Contains("xbox")) return "Xbox";
        if (lower.Contains("windowsupdate")) return "Windows Update";
        if (lower.Contains("explorer")) return "Dosya Gezgini";

        // Clean fallback from AUMID or path
        string cleaned = primaryId;
        int bang = cleaned.IndexOf('!');
        if (bang > 0) cleaned = cleaned[..bang];
        int under = cleaned.IndexOf('_');
        if (under > 0) cleaned = cleaned[..under];
        int slash = cleaned.LastIndexOf('\\');
        if (slash >= 0 && slash + 1 < cleaned.Length) cleaned = Path.GetFileNameWithoutExtension(cleaned[(slash + 1)..]);
        if (cleaned.Contains('.'))
        {
            var segs = cleaned.Split('.', StringSplitOptions.RemoveEmptyEntries);
            cleaned = segs.LastOrDefault(s => !s.Equals("App", StringComparison.OrdinalIgnoreCase) && !s.Equals("Desktop", StringComparison.OrdinalIgnoreCase)) ?? cleaned;
        }
        return string.IsNullOrWhiteSpace(cleaned) ? "Windows" : cleaned;
    }

    private static DateTimeOffset SafeFromFileTimeUtc(long fileTime)
    {
        try { return DateTimeOffset.FromFileTime(fileTime).ToUniversalTime(); }
        catch { return DateTimeOffset.UtcNow; }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";

    private static void CopyFileSharedRead(string src, string dst)
    {
        using var inStream = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var outStream = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.Read);
        inStream.CopyTo(outStream);
    }

    private static void CopyFileSharedReadIfExists(string src, string dst)
    {
        if (File.Exists(src))
        {
            try { CopyFileSharedRead(src, dst); } catch { }
        }
    }

    private static void TryDeleteQuietly(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _watcher?.Dispose(); } catch { }
        try { _debounceTimer?.Dispose(); } catch { }
    }
}
