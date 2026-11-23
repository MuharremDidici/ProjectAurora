using System.Diagnostics;
using System.Text.RegularExpressions;
using Aurora.Platform.Windows.Apps;
using Aurora.Platform.Windows.DisplayAndTaskbar;

namespace Aurora.Platform.Windows.ShellExtras;

public sealed record DockFolderItemEntry(
    string Name,
    string FullPath,
    bool IsDirectory,
    string SizeLabel,
    string ModifiedLabel,
    byte[]? IconPngBytes);

public sealed record DockFolderStackSummary(
    string Path,
    string DisplayName,
    int ItemCount);

public static partial class DockFolderStackService
{
    public static List<string> GetDefaultFolders()
    {
        var list = new List<string>();
        string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        if (Directory.Exists(downloads)) list.Add(downloads);
        if (Directory.Exists(documents)) list.Add(documents);
        return list;
    }

    public static string GetFriendlyFolderName(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "Klasör";
        string trimmed = path.TrimEnd('\\', '/');
        string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        if (string.Equals(trimmed, downloads, StringComparison.OrdinalIgnoreCase)) return "İndirilenler";
        if (string.Equals(trimmed, documents, StringComparison.OrdinalIgnoreCase)) return "Belgeler";
        if (string.Equals(trimmed, desktop, StringComparison.OrdinalIgnoreCase)) return "Masaüstü";
        if (string.Equals(trimmed, pictures, StringComparison.OrdinalIgnoreCase)) return "Resimler";

        string name = Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(name) ? trimmed : name;
    }

    public static List<DockFolderItemEntry> ReadFolderStackItems(string folderPath, int maxItems = 10)
    {
        var results = new List<DockFolderItemEntry>();
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return results;

        try
        {
            var entries = new List<(string Path, string Name, bool IsDir, DateTime Mtime, long Length)>();

            foreach (var dir in Directory.EnumerateDirectories(folderPath))
            {
                string name = Path.GetFileName(dir);
                if (string.IsNullOrWhiteSpace(name) || name.StartsWith('.')) continue;
                DateTime mt;
                try { mt = Directory.GetLastWriteTime(dir); } catch { mt = DateTime.MinValue; }
                entries.Add((dir, name, true, mt, 0));
            }

            foreach (var file in Directory.EnumerateFiles(folderPath))
            {
                string name = Path.GetFileName(file);
                if (string.IsNullOrWhiteSpace(name) || name.StartsWith('.') || name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                    continue;
                DateTime mt = DateTime.MinValue;
                long len = 0;
                try
                {
                    var fi = new FileInfo(file);
                    mt = fi.LastWriteTime;
                    len = fi.Length;
                }
                catch { }
                entries.Add((file, name, false, mt, len));
            }

            foreach (var e in entries.OrderByDescending(x => x.Mtime).Take(maxItems))
            {
                string sizeLbl = e.IsDir ? "Klasör" : FormatSize(e.Length);
                string modLbl = e.Mtime == DateTime.MinValue ? "" : FormatModified(e.Mtime);
                byte[]? icon = e.IsDir ? null : Win32IconExtractor.ResolveShortcutIconPng(e.Path);
                results.Add(new DockFolderItemEntry(e.Name, e.Path, e.IsDir, sizeLbl, modLbl, icon));
            }
        }
        catch { }

        return results;
    }

    public static bool OpenFilesWithApp(string? executablePath, IReadOnlyList<string> droppedPaths)
    {
        if (droppedPaths.Count == 0 || string.IsNullOrWhiteSpace(executablePath))
            return false;

        string target = executablePath.Trim();
        try
        {
            if (target.StartsWith("appsfolder:", StringComparison.OrdinalIgnoreCase))
            {
                // UWP/MSIX apps cannot accept raw CLI file paths reliably; bring app to foreground via AppsFolder
                return InstalledAppCatalogService.LaunchTarget(target, out _);
            }

            string quotedArgs = string.Join(' ', droppedPaths.Take(8).Select(p => $"\"{p}\""));
            if (target.EndsWith("explorer.exe", StringComparison.OrdinalIgnoreCase))
            {
                string first = droppedPaths[0];
                string arg = Directory.Exists(first) ? $"\"{first}\"" : $"/select,\"{first}\"";
                Process.Start(new ProcessStartInfo("explorer.exe", arg) { UseShellExecute = true });
                return true;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                Arguments = quotedArgs,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0 * 1024.0):0.1} GB";
        if (bytes >= 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):0.1} MB";
        return $"{Math.Max(1, bytes / 1024)} KB";
    }

    private static string FormatModified(DateTime dt)
    {
        var diff = DateTime.Now - dt;
        if (diff.TotalMinutes < 60) return $"{Math.Max(1, (int)diff.TotalMinutes)} dk";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} sa";
        return dt.ToString("dd.MM");
    }
}

/// <summary>
/// Tracks background app attention pulses (incoming Windows 11 notifications & EVENT_SYSTEM_ALERT)
/// and parses active file-transfer / download progress percentages for Dock icon rings.
/// </summary>
public sealed partial class WindowAttentionAndProgressService
{
    private readonly Dictionary<string, DateTimeOffset> _attentionByAppKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public void MarkAttention(string appOrProcessName)
    {
        string key = NormalizeAppKey(appOrProcessName);
        if (string.IsNullOrWhiteSpace(key)) return;
        lock (_lock)
        {
            _attentionByAppKey[key] = DateTimeOffset.UtcNow;
        }
    }

    public void ClearAttention(string appOrProcessName)
    {
        string key = NormalizeAppKey(appOrProcessName);
        if (string.IsNullOrWhiteSpace(key)) return;
        lock (_lock)
        {
            _attentionByAppKey.Remove(key);
        }
    }

    public bool HasAttention(string displayName, string? processName)
    {
        lock (_lock)
        {
            if (_attentionByAppKey.Count == 0) return false;
            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-15);
            string k1 = NormalizeAppKey(displayName);
            string k2 = NormalizeAppKey(processName ?? string.Empty);

            foreach (var kv in _attentionByAppKey.ToArray())
            {
                if (kv.Value < cutoff)
                {
                    _attentionByAppKey.Remove(kv.Key);
                    continue;
                }
                if ((!string.IsNullOrEmpty(k1) && (k1.Contains(kv.Key, StringComparison.Ordinal) || kv.Key.Contains(k1, StringComparison.Ordinal))) ||
                    (!string.IsNullOrEmpty(k2) && (k2.Contains(kv.Key, StringComparison.Ordinal) || kv.Key.Contains(k2, StringComparison.Ordinal))))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public static int? ExtractProgressPercent(IEnumerable<string> windowTitles)
    {
        foreach (var raw in windowTitles)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var m = PercentRegex().Match(raw);
            if (m.Success)
            {
                string digits = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                if (int.TryParse(digits, out int pct) && pct is >= 1 and <= 99)
                {
                    // Guard against ordinary non-progress titles (e.g., "50% Off Summer Sale - Opera")
                    string lower = raw.ToLowerInvariant();
                    if (lower.Contains("tamam") || lower.Contains("complete") || lower.Contains("kopya") ||
                        lower.Contains("copy") || lower.Contains("taşı") || lower.Contains("mov") ||
                        lower.Contains("indir") || lower.Contains("download") || lower.Contains("extract") ||
                        lower.Contains("ayıkla") || lower.Contains("yüklen") || lower.Contains("install") ||
                        lower.Contains("sync") || lower.Contains("render") || lower.Contains("export") ||
                        lower.Contains("progress") || lower.Contains("işlen") ||
                        raw.TrimStart().StartsWith('%'))
                    {
                        return pct;
                    }
                }
            }
        }
        return null;
    }

    private static string NormalizeAppKey(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        string s = raw.Trim().ToLowerInvariant()
            .Replace("microsoft ", "")
            .Replace("google ", "")
            .Replace(".exe", "")
            .Replace(" ", "");
        if (s.Contains("msteams")) return "teams";
        if (s.Contains("whatsapp")) return "whatsapp";
        if (s.Contains("discord")) return "discord";
        if (s.Contains("telegram")) return "telegram";
        if (s.Contains("slack")) return "slack";
        if (s.Contains("outlook")) return "outlook";
        return s;
    }

    [GeneratedRegex(@"(?:%\s*(\d{1,2})\b|\b(\d{1,2})\s*%)")]
    private static partial Regex PercentRegex();
}
