using System.Text.Json;
using Aurora.Core.Models;

namespace Aurora.Infrastructure.Persistence;

public sealed record AuroraBackupBundle(
    string FormatVersion,
    DateTimeOffset ExportedAtUtc,
    ShellRuntimePreferences ShellPreferences,
    string ActiveThemeId,
    string ActiveProfileId,
    IReadOnlyList<PinnedShortcut> PinnedShortcuts,
    Dictionary<string, string> AgendaNotes,
    string ScratchpadText,
    List<string> DropShelfPaths,
    Dictionary<string, int> SearchFrecency);

public static class AuroraBackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void ExportToFile(string filePath, AuroraBackupBundle bundle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(bundle);

        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        string tmpPath = filePath + ".tmp";
        File.WriteAllText(tmpPath, JsonSerializer.Serialize(bundle, JsonOptions));
        File.Move(tmpPath, filePath, overwrite: true);
    }

    public static AuroraBackupBundle? ImportFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            string json = File.ReadAllText(filePath);
            var parsed = JsonSerializer.Deserialize<AuroraBackupBundle>(json, JsonOptions);
            if (parsed is null || string.IsNullOrWhiteSpace(parsed.FormatVersion))
                return null;

            return parsed with
            {
                ShellPreferences = parsed.ShellPreferences ?? new ShellRuntimePreferences(),
                PinnedShortcuts = parsed.PinnedShortcuts ?? [],
                AgendaNotes = parsed.AgendaNotes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                ScratchpadText = parsed.ScratchpadText ?? string.Empty,
                DropShelfPaths = parsed.DropShelfPaths ?? [],
                SearchFrecency = parsed.SearchFrecency ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            };
        }
        catch
        {
            return null;
        }
    }
}
