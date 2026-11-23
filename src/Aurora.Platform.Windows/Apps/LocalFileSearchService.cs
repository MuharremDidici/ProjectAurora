using System.Text;
using Aurora.Platform.Windows.DisplayAndTaskbar;

namespace Aurora.Platform.Windows.Apps;

public sealed record LocalSearchHit(
    string Name,
    string FullPath,
    string Subtitle,
    string Badge,
    bool IsDirectory,
    byte[]? IconPngBytes,
    int Score);

public sealed class LocalFileSearchService
{
    private sealed record IndexedEntry(
        string Name,
        string NormalizedName,
        string FullPath,
        string DisplayParent,
        bool IsDirectory,
        DateTime LastWriteUtc);

    private static readonly HashSet<string> SkippedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", ".vs", ".idea", ".vscode", "bin", "obj",
        "__pycache__", ".venv", "venv", "packages", "$RECYCLE.BIN",
        "System Volume Information", "AppData", "dist", "build", ".nuget"
    };

    private const int MaxIndexedEntries = 6500;

    private List<IndexedEntry> _index = [];
    private DateTimeOffset _lastIndexedUtc = DateTimeOffset.MinValue;
    private readonly object _indexLock = new();
    private bool _indexingInFlight;

    public static string NormalizeTurkish(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var sb = new StringBuilder(input.Length);
        foreach (char ch in input)
        {
            char mapped = ch switch
            {
                'İ' or 'I' or 'ı' or 'i' => 'i',
                'Ş' or 'ş' => 's',
                'Ğ' or 'ğ' => 'g',
                'Ü' or 'ü' => 'u',
                'Ö' or 'ö' => 'o',
                'Ç' or 'ç' => 'c',
                _ => char.ToLowerInvariant(ch)
            };
            sb.Append(mapped);
        }
        return sb.ToString();
    }

    public void WarmIndexInBackground()
    {
        lock (_indexLock)
        {
            if (_indexingInFlight || (DateTimeOffset.UtcNow - _lastIndexedUtc).TotalMinutes < 3)
                return;
            _indexingInFlight = true;
        }

        _ = Task.Run(() =>
        {
            try
            {
                RebuildIndex();
            }
            finally
            {
                lock (_indexLock) { _indexingInFlight = false; }
            }
        });
    }

    public IReadOnlyList<LocalSearchHit> Search(string rawQuery, int maxResults = 8)
    {
        if (string.IsNullOrWhiteSpace(rawQuery))
            return [];

        string trimmed = rawQuery.Trim();

        // 1. Direct filesystem path or environment variable path (e.g., "C:\Users", "%LOCALAPPDATA%")
        var directHits = TrySearchDirectPath(trimmed, maxResults);
        if (directHits.Count > 0)
            return directHits;

        string normQuery = NormalizeTurkish(trimmed);
        if (normQuery.Length < 2)
            return [];

        // Ensure index exists synchronously once if cold, otherwise refresh lazily in background
        List<IndexedEntry> snapshot;
        lock (_indexLock)
        {
            snapshot = _index;
        }

        if (snapshot.Count == 0)
        {
            RebuildIndex();
            lock (_indexLock) { snapshot = _index; }
        }
        else if ((DateTimeOffset.UtcNow - _lastIndexedUtc).TotalMinutes >= 3)
        {
            WarmIndexInBackground();
        }

        var tokens = normQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matches = new List<(IndexedEntry Entry, int Score)>(32);

        foreach (var entry in snapshot)
        {
            int score = ScoreMatch(entry.NormalizedName, normQuery, tokens, entry.IsDirectory, entry.LastWriteUtc);
            if (score > 0)
                matches.Add((entry, score));
        }

        var top = matches
            .OrderByDescending(m => m.Score)
            .ThenByDescending(m => m.Entry.LastWriteUtc)
            .Take(maxResults)
            .ToList();

        var results = new List<LocalSearchHit>(top.Count);
        foreach (var (entry, score) in top)
        {
            byte[]? icon = entry.IsDirectory
                ? null
                : Win32IconExtractor.ResolveShortcutIconPng(entry.FullPath);

            results.Add(new LocalSearchHit(
                Name: entry.Name,
                FullPath: entry.FullPath,
                Subtitle: entry.DisplayParent,
                Badge: entry.IsDirectory ? "Klasör" : FormatExtBadge(entry.FullPath),
                IsDirectory: entry.IsDirectory,
                IconPngBytes: icon,
                Score: score));
        }

        return results;
    }

    internal static int ScoreMatch(string normalizedName, string normQuery, string[] tokens, bool isDirectory, DateTime lastWriteUtc)
    {
        if (string.IsNullOrEmpty(normalizedName))
            return 0;

        int score = 0;
        if (normalizedName == normQuery)
        {
            score = 1000;
        }
        else if (normalizedName.StartsWith(normQuery, StringComparison.Ordinal))
        {
            score = 760;
        }
        else if (normalizedName.Contains(normQuery, StringComparison.Ordinal))
        {
            int idx = normalizedName.IndexOf(normQuery, StringComparison.Ordinal);
            bool wordStart = idx == 0 || !char.IsLetterOrDigit(normalizedName[idx - 1]);
            score = wordStart ? 620 : 420;
        }
        else if (tokens.Length > 1 && tokens.All(t => normalizedName.Contains(t, StringComparison.Ordinal)))
        {
            score = 380;
        }
        else
        {
            return 0;
        }

        // Recency bonus (files touched in last 7 days get prioritized)
        var ageDays = (DateTime.UtcNow - lastWriteUtc).TotalDays;
        if (ageDays <= 1) score += 90;
        else if (ageDays <= 7) score += 50;
        else if (ageDays <= 30) score += 20;

        if (isDirectory) score += 15;
        return score;
    }

    private static List<LocalSearchHit> TrySearchDirectPath(string raw, int maxResults)
    {
        var results = new List<LocalSearchHit>();
        bool looksLikePath = raw.Contains(":\\", StringComparison.Ordinal) ||
                             raw.StartsWith("\\\\", StringComparison.Ordinal) ||
                             (raw.StartsWith('%') && raw.EndsWith('%')) ||
                             raw.StartsWith("~\\", StringComparison.Ordinal) ||
                             raw == "~";
        if (!looksLikePath)
            return results;

        string expanded = raw == "~"
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : (raw.StartsWith("~\\", StringComparison.Ordinal)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), raw[2..])
                : Environment.ExpandEnvironmentVariables(raw));

        if (Directory.Exists(expanded))
        {
            results.Add(new LocalSearchHit(
                Name: Path.GetFileName(expanded.TrimEnd('\\')) is { Length: > 0 } fn ? fn : expanded,
                FullPath: expanded,
                Subtitle: expanded,
                Badge: "Klasör Aç",
                IsDirectory: true,
                IconPngBytes: null,
                Score: 1200));

            try
            {
                foreach (var child in Directory.EnumerateFileSystemEntries(expanded).Take(maxResults - 1))
                {
                    bool isDir = Directory.Exists(child);
                    results.Add(new LocalSearchHit(
                        Name: Path.GetFileName(child),
                        FullPath: child,
                        Subtitle: expanded,
                        Badge: isDir ? "Klasör" : FormatExtBadge(child),
                        IsDirectory: isDir,
                        IconPngBytes: isDir ? null : Win32IconExtractor.ResolveShortcutIconPng(child),
                        Score: 950));
                }
            }
            catch { }
            return results;
        }

        if (File.Exists(expanded))
        {
            results.Add(new LocalSearchHit(
                Name: Path.GetFileName(expanded),
                FullPath: expanded,
                Subtitle: Path.GetDirectoryName(expanded) ?? expanded,
                Badge: FormatExtBadge(expanded),
                IsDirectory: false,
                IconPngBytes: Win32IconExtractor.ResolveShortcutIconPng(expanded),
                Score: 1200));
            return results;
        }

        // Partial path auto-complete (e.g. "C:\Users\Lenovo\Doc")
        string? parentDir = Path.GetDirectoryName(expanded);
        string prefix = Path.GetFileName(expanded);
        if (!string.IsNullOrWhiteSpace(parentDir) && Directory.Exists(parentDir) && !string.IsNullOrWhiteSpace(prefix))
        {
            string normPrefix = NormalizeTurkish(prefix);
            try
            {
                foreach (var child in Directory.EnumerateFileSystemEntries(parentDir))
                {
                    string name = Path.GetFileName(child);
                    if (!NormalizeTurkish(name).StartsWith(normPrefix, StringComparison.Ordinal))
                        continue;

                    bool isDir = Directory.Exists(child);
                    results.Add(new LocalSearchHit(
                        Name: name,
                        FullPath: child,
                        Subtitle: parentDir,
                        Badge: isDir ? "Klasör" : FormatExtBadge(child),
                        IsDirectory: isDir,
                        IconPngBytes: isDir ? null : Win32IconExtractor.ResolveShortcutIconPng(child),
                        Score: 1000));
                    if (results.Count >= maxResults)
                        break;
                }
            }
            catch { }
        }

        return results;
    }

    private void RebuildIndex()
    {
        var entries = new List<IndexedEntry>(1024);
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var roots = new (string Path, string Label, int MaxDepth)[]
        {
            (Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Masaüstü", 2),
            (Path.Combine(userProfile, "Downloads"), "İndirilenler", 2),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Belgeler", 3),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Resimler", 2),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Videolar", 1),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Müzik", 1),
            (Environment.GetFolderPath(Environment.SpecialFolder.Recent), "Son Kullanılanlar", 0)
        };

        foreach (var (rootPath, rootLabel, maxDepth) in roots)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                continue;

            CrawlDirectory(rootPath, rootPath, rootLabel, 0, maxDepth, entries, seenPaths);
            if (entries.Count >= MaxIndexedEntries)
                break;
        }

        lock (_indexLock)
        {
            _index = entries;
            _lastIndexedUtc = DateTimeOffset.UtcNow;
        }
    }

    private static void CrawlDirectory(
        string currentDir,
        string rootDir,
        string rootLabel,
        int depth,
        int maxDepth,
        List<IndexedEntry> entries,
        HashSet<string> seenPaths)
    {
        if (entries.Count >= MaxIndexedEntries)
            return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(currentDir))
            {
                if (entries.Count >= MaxIndexedEntries) return;

                string fileName = Path.GetFileName(file);
                if (string.IsNullOrWhiteSpace(fileName) || fileName.StartsWith('.') || fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                    continue;

                bool isRecentLnk = rootLabel == "Son Kullanılanlar" && fileName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);
                string displayName = isRecentLnk ? Path.GetFileNameWithoutExtension(fileName) : fileName;
                if (!seenPaths.Add(file))
                    continue;

                DateTime mtime;
                try { mtime = File.GetLastWriteTimeUtc(file); } catch { mtime = DateTime.MinValue; }

                string relParent = BuildDisplayParent(currentDir, rootDir, rootLabel);
                entries.Add(new IndexedEntry(
                    Name: displayName,
                    NormalizedName: NormalizeTurkish(displayName),
                    FullPath: file,
                    DisplayParent: relParent,
                    IsDirectory: false,
                    LastWriteUtc: mtime));
            }
        }
        catch { }

        if (depth >= maxDepth)
            return;

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(currentDir))
            {
                if (entries.Count >= MaxIndexedEntries) return;

                string dirName = Path.GetFileName(dir);
                if (string.IsNullOrWhiteSpace(dirName) || dirName.StartsWith('.') || SkippedDirectoryNames.Contains(dirName))
                    continue;

                if (seenPaths.Add(dir))
                {
                    DateTime mtime;
                    try { mtime = Directory.GetLastWriteTimeUtc(dir); } catch { mtime = DateTime.MinValue; }

                    entries.Add(new IndexedEntry(
                        Name: dirName,
                        NormalizedName: NormalizeTurkish(dirName),
                        FullPath: dir,
                        DisplayParent: BuildDisplayParent(currentDir, rootDir, rootLabel),
                        IsDirectory: true,
                        LastWriteUtc: mtime));
                }

                CrawlDirectory(dir, rootDir, rootLabel, depth + 1, maxDepth, entries, seenPaths);
            }
        }
        catch { }
    }

    private static string BuildDisplayParent(string currentDir, string rootDir, string rootLabel)
    {
        if (string.Equals(currentDir, rootDir, StringComparison.OrdinalIgnoreCase))
            return rootLabel;
        string rel = Path.GetRelativePath(rootDir, currentDir);
        return $"{rootLabel}\\{rel}";
    }

    private static string FormatExtBadge(string path)
    {
        string ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(ext) || ext == "LNK") return "Dosya";
        return ext.Length > 6 ? "Dosya" : ext;
    }
}
