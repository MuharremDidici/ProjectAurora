using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aurora.Core.Contracts;

namespace Aurora.Infrastructure.Logging;

public sealed partial class PrivacySafeFileLogger : IAuroraLogger
{
    private readonly ConcurrentQueue<DiagnosticLogEntry> _ringBuffer = new();
    private readonly Lock _fileLock = new();
    private readonly string _logDir;
    private const int MaxRingSize = 250;

    public PrivacySafeFileLogger(string? customLogDirectory = null)
    {
        _logDir = customLogDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProjectAurora",
            "logs");
        Directory.CreateDirectory(_logDir);
    }

    public string LogDirectoryPath => _logDir;

    public void Info(string category, string eventCode, string sanitizedMessage) =>
        Write("INFO", category, eventCode, sanitizedMessage, null);

    public void Warn(string category, string eventCode, string sanitizedMessage) =>
        Write("WARN", category, eventCode, sanitizedMessage, null);

    public void Error(string category, string eventCode, string sanitizedMessage, Exception? ex = null) =>
        Write("ERROR", category, eventCode, sanitizedMessage, ex);

    public IReadOnlyList<DiagnosticLogEntry> GetRecentEntries(int maxCount = 100) =>
        _ringBuffer.Reverse().Take(Math.Max(1, maxCount)).ToList();

    private void Write(string level, string category, string eventCode, string message, Exception? ex)
    {
        var safeMsg = RedactSensitivePatterns(message);
        if (ex is not null)
        {
            safeMsg = $"{safeMsg} [{ex.GetType().Name}]";
        }

        var entry = new DiagnosticLogEntry(
            DateTimeOffset.UtcNow,
            level,
            category,
            eventCode,
            safeMsg);

        _ringBuffer.Enqueue(entry);
        while (_ringBuffer.Count > MaxRingSize && _ringBuffer.TryDequeue(out _)) { }

        try
        {
            var filePath = Path.Combine(_logDir, $"aurora-{DateTimeOffset.UtcNow:yyyyMMdd}.jsonl");
            var line = JsonSerializer.Serialize(entry) + Environment.NewLine;
            lock (_fileLock)
            {
                File.AppendAllText(filePath, line);
            }
        }
        catch
        {
            // Logging must never crash the host process.
        }
    }

    public static string RedactSensitivePatterns(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var redacted = EmailRegex().Replace(raw, "[REDACTED_EMAIL]");
        redacted = UserProfilePathRegex().Replace(redacted, @"C:\Users\[USER]\");
        return redacted.Length > 400 ? redacted[..400] : redacted;
    }

    [GeneratedRegex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"[A-Za-z]:\\Users\\[^\\]+\\", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UserProfilePathRegex();
}
