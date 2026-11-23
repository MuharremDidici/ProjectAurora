using System.Text.Json;

namespace Aurora.Infrastructure.Recovery;

public sealed class StartupCrashSentinel
{
    private readonly string _sentinelFilePath;

    private sealed record SentinelPayload(int ConsecutiveIncompleteBoots, DateTimeOffset LastBootUtc);

    public StartupCrashSentinel(string? customDirectory = null)
    {
        var dir = customDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProjectAurora",
            "data");
        Directory.CreateDirectory(dir);
        _sentinelFilePath = Path.Combine(dir, "boot_sentinel.json");
    }

    public bool RecordBootAndCheckSafeModeRecommendation()
    {
        try
        {
            int count = 0;
            if (File.Exists(_sentinelFilePath))
            {
                var existing = JsonSerializer.Deserialize<SentinelPayload>(File.ReadAllText(_sentinelFilePath));
                if (existing is not null)
                    count = existing.ConsecutiveIncompleteBoots;
            }

            count++;
            File.WriteAllText(_sentinelFilePath, JsonSerializer.Serialize(new SentinelPayload(count, DateTimeOffset.UtcNow)));
            return count >= 3;
        }
        catch
        {
            return false;
        }
    }

    public void MarkHealthyOrGracefulShutdown()
    {
        try
        {
            File.WriteAllText(_sentinelFilePath, JsonSerializer.Serialize(new SentinelPayload(0, DateTimeOffset.UtcNow)));
        }
        catch
        {
            // Ignore IO errors on shutdown
        }
    }
}
