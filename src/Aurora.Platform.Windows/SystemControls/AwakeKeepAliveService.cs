using System.Runtime.InteropServices;

namespace Aurora.Platform.Windows.SystemControls;

public enum AwakeMode
{
    Off = 0,
    Minutes30 = 30,
    Hour1 = 60,
    Hours2 = 120,
    Indefinite = -1
}

public sealed record AwakeStatusSnapshot(
    bool IsActive,
    string ModeKey,
    string ShortLabel,
    string FullLabel,
    int RemainingMinutes);

/// <summary>
/// Safe, zero-registry-modification Windows Keep-Awake service backed by
/// kernel32 SetThreadExecutionState. Automatically restores normal power
/// management on expiration or process exit without touching Power Plans.
/// </summary>
public sealed class AwakeKeepAliveService : IDisposable
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;
    private const uint EsDisplayRequired = 0x00000002;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);

    private AwakeMode _mode = AwakeMode.Off;
    private DateTimeOffset? _expiresAtUtc;
    private bool _disposed;

    public AwakeStatusSnapshot SetMode(AwakeMode mode, DateTimeOffset? nowUtc = null)
    {
        if (_disposed)
            return GetSnapshot(nowUtc);

        var now = nowUtc ?? DateTimeOffset.UtcNow;
        _mode = mode;

        if (mode == AwakeMode.Off)
        {
            _expiresAtUtc = null;
            ApplyExecutionState(active: false);
        }
        else if (mode == AwakeMode.Indefinite)
        {
            _expiresAtUtc = null;
            ApplyExecutionState(active: true);
        }
        else
        {
            _expiresAtUtc = now.AddMinutes((int)mode);
            ApplyExecutionState(active: true);
        }

        return GetSnapshot(now);
    }

    public AwakeStatusSnapshot SetModeByKey(string? modeKey, DateTimeOffset? nowUtc = null)
    {
        var mode = (modeKey ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "30m" or "minutes30" => AwakeMode.Minutes30,
            "1h" or "60m" or "hour1" => AwakeMode.Hour1,
            "2h" or "120m" or "hours2" => AwakeMode.Hours2,
            "indefinite" or "forever" or "on" => AwakeMode.Indefinite,
            _ => AwakeMode.Off
        };
        return SetMode(mode, nowUtc);
    }

    public AwakeStatusSnapshot CycleMode(DateTimeOffset? nowUtc = null)
    {
        var next = _mode switch
        {
            AwakeMode.Off => AwakeMode.Minutes30,
            AwakeMode.Minutes30 => AwakeMode.Hour1,
            AwakeMode.Hour1 => AwakeMode.Indefinite,
            _ => AwakeMode.Off
        };
        return SetMode(next, nowUtc);
    }

    /// <summary>
    /// Checks whether a timed Awake session has expired. Returns true if it just transitioned from active to off.
    /// </summary>
    public bool CheckExpiration(DateTimeOffset? nowUtc = null)
    {
        if (_mode == AwakeMode.Off || _mode == AwakeMode.Indefinite || _expiresAtUtc is null)
            return false;

        var now = nowUtc ?? DateTimeOffset.UtcNow;
        if (now >= _expiresAtUtc.Value)
        {
            SetMode(AwakeMode.Off, now);
            return true;
        }
        return false;
    }

    public AwakeStatusSnapshot GetSnapshot(DateTimeOffset? nowUtc = null)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        if (_mode == AwakeMode.Off)
            return new AwakeStatusSnapshot(false, "off", "Kapalı", "Normal güç yönetimi", 0);

        if (_mode == AwakeMode.Indefinite)
            return new AwakeStatusSnapshot(true, "indefinite", "Süresiz", "Ekran & sistem süresiz uyanık", -1);

        int remMin = _expiresAtUtc is null
            ? 0
            : Math.Max(0, (int)Math.Ceiling((_expiresAtUtc.Value - now).TotalMinutes));

        if (remMin <= 0)
        {
            SetMode(AwakeMode.Off, now);
            return new AwakeStatusSnapshot(false, "off", "Kapalı", "Normal güç yönetimi", 0);
        }

        string modeKey = _mode switch
        {
            AwakeMode.Minutes30 => "30m",
            AwakeMode.Hour1 => "1h",
            AwakeMode.Hours2 => "2h",
            _ => "1h"
        };

        string shortLabel = remMin >= 60
            ? $"{remMin / 60}s {remMin % 60:00}d"
            : $"{remMin} dk";

        return new AwakeStatusSnapshot(
            IsActive: true,
            ModeKey: modeKey,
            ShortLabel: shortLabel,
            FullLabel: $"Uyanık ({shortLabel} kaldı)",
            RemainingMinutes: remMin);
    }

    private static void ApplyExecutionState(bool active)
    {
        try
        {
            uint flags = active
                ? (EsContinuous | EsSystemRequired | EsDisplayRequired)
                : EsContinuous;
            SetThreadExecutionState(flags);
        }
        catch { }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_mode != AwakeMode.Off)
        {
            _mode = AwakeMode.Off;
            _expiresAtUtc = null;
            ApplyExecutionState(active: false);
        }
    }
}
