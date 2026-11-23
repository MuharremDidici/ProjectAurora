using Microsoft.Win32;

namespace Aurora.Platform.Windows.ShellExtras;

public sealed record PrivacyIndicatorSnapshot(
    bool CameraActive,
    bool MicActive,
    string[] CameraApps,
    string[] MicApps);

/// <summary>
/// Zero-UAC, sub-millisecond Windows 11 Camera &amp; Microphone active usage detector.
/// Reads Windows CapabilityAccessManager ConsentStore registry keys (LastUsedTimeStop == 0)
/// for both packaged UWP/MSIX apps and Win32 desktop executables (Discord, OBS, Teams, Chrome, etc.).
/// </summary>
public sealed class PrivacyIndicatorService
{
    private const string ConsentBase = @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    public PrivacyIndicatorSnapshot GetCurrentSnapshot()
    {
        var camApps = ReadActiveAppsForCapability("webcam");
        var micApps = ReadActiveAppsForCapability("microphone");
        return new PrivacyIndicatorSnapshot(
            CameraActive: camApps.Length > 0,
            MicActive: micApps.Length > 0,
            CameraApps: camApps,
            MicApps: micApps);
    }

    private static string[] ReadActiveAppsForCapability(string capabilityName)
    {
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var capKey = Registry.CurrentUser.OpenSubKey($@"{ConsentBase}\{capabilityName}");
            if (capKey is null)
                return [];

            foreach (var subName in capKey.GetSubKeyNames())
            {
                if (string.Equals(subName, "NonPackaged", StringComparison.OrdinalIgnoreCase))
                {
                    using var nonPkg = capKey.OpenSubKey("NonPackaged");
                    if (nonPkg is null) continue;
                    foreach (var exeKeyName in nonPkg.GetSubKeyNames())
                    {
                        using var appKey = nonPkg.OpenSubKey(exeKeyName);
                        if (appKey is not null && IsActiveConsentKey(appKey))
                        {
                            string friendly = FormatConsentAppName(exeKeyName, isNonPackaged: true);
                            if (!string.IsNullOrWhiteSpace(friendly) && !friendly.Contains("Aurora", StringComparison.OrdinalIgnoreCase))
                                active.Add(friendly);
                        }
                    }
                }
                else
                {
                    using var appKey = capKey.OpenSubKey(subName);
                    if (appKey is not null && IsActiveConsentKey(appKey))
                    {
                        string friendly = FormatConsentAppName(subName, isNonPackaged: false);
                        if (!string.IsNullOrWhiteSpace(friendly) && !friendly.Contains("Aurora", StringComparison.OrdinalIgnoreCase))
                            active.Add(friendly);
                    }
                }
            }
        }
        catch
        {
            // Registry temporarily inaccessible
        }

        return [.. active.Take(4)];
    }

    private static bool IsActiveConsentKey(RegistryKey key)
    {
        try
        {
            object? startObj = key.GetValue("LastUsedTimeStart");
            object? stopObj = key.GetValue("LastUsedTimeStop");
            if (startObj is null || stopObj is null)
                return false;

            long start = Convert.ToInt64(startObj);
            long stop = Convert.ToInt64(stopObj);
            return start > 0 && stop == 0;
        }
        catch
        {
            return false;
        }
    }

    public static string FormatConsentAppName(string rawKeyName, bool isNonPackaged)
    {
        if (string.IsNullOrWhiteSpace(rawKeyName))
            return string.Empty;

        string candidate;
        if (isNonPackaged)
        {
            string normalized = rawKeyName.Replace('#', '\\');
            candidate = Path.GetFileNameWithoutExtension(normalized);
        }
        else
        {
            // Packaged family name e.g. "Microsoft.WindowsCamera_8wekyb3d8bbwe" or "5319275A.WhatsAppDesktop_..."
            int underscore = rawKeyName.IndexOf('_');
            string baseName = underscore > 0 ? rawKeyName[..underscore] : rawKeyName;
            int lastDot = baseName.LastIndexOf('.');
            candidate = lastDot >= 0 && lastDot < baseName.Length - 1 ? baseName[(lastDot + 1)..] : baseName;
        }

        string lower = candidate.ToLowerInvariant();
        if (lower.Contains("discord")) return "Discord";
        if (lower.Contains("obs")) return "OBS Studio";
        if (lower.Contains("teams")) return "Teams";
        if (lower.Contains("zoom")) return "Zoom";
        if (lower.Contains("whatsapp")) return "WhatsApp";
        if (lower.Contains("telegram")) return "Telegram";
        if (lower.Contains("chrome")) return "Chrome";
        if (lower.Contains("msedge")) return "Edge";
        if (lower.Contains("opera")) return "Opera";
        if (lower.Contains("brave")) return "Brave";
        if (lower.Contains("firefox")) return "Firefox";
        if (lower.Contains("camera")) return "Kamera";
        if (lower.Contains("slack")) return "Slack";
        if (lower.Contains("nvidia") || lower.Contains("broadcast")) return "NVIDIA Broadcast";

        return candidate.Length > 0
            ? char.ToUpperInvariant(candidate[0]) + candidate[1..]
            : string.Empty;
    }
}
