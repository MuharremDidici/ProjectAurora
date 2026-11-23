using System.Collections.Concurrent;
using System.Management;
using System.Runtime.InteropServices;

namespace Aurora.Platform.Windows.DisplayAndTaskbar;

public sealed record MonitorBrightnessEntry(
    string Id,
    string Name,
    int BrightnessPercent,
    bool IsInternal,
    bool IsPrimary,
    string DeviceName,
    uint MinValue,
    uint MaxValue);

/// <summary>
/// Unified real hardware monitor brightness controller supporting BOTH:
///   1) Internal laptop displays via root\WMI WmiMonitorBrightness / WmiSetBrightness
///   2) External desktop monitors (ViewSonic, Dell, LG, Samsung, ASUS, etc.) via native dxva2.dll DDC/CI VCP 0x10!
/// Includes real EDID friendly model resolution (WmiMonitorID -> "VA2715-QHD", "F-419") and per-monitor
/// latest-wins I2C write coalescing so rapid slider drags stay buttery-smooth with zero handle leaks.
/// </summary>
public sealed class DdcCiMonitorBrightnessService
{
    private const byte VcpCodeBrightness = 0x10;
    private const uint EddGetDeviceInterfaceName = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEXW
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICEW
    {
        public uint cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PHYSICAL_MONITOR
    {
        public nint hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    private delegate bool MonitorEnumProc(nint hMonitor, nint hdcMonitor, ref RECT lprcMonitor, nint dwData);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(nint hdc, nint lprcClip, MonitorEnumProc lpfnEnum, nint dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(nint hMonitor, ref MONITORINFOEXW lpmi);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevicesW(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICEW lpDisplayDevice, uint dwFlags);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(nint hMonitor, out uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(nint hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorBrightness(
        nint hMonitor,
        out uint pdwMinimumBrightness,
        out uint pdwCurrentBrightness,
        out uint pdwMaximumBrightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetMonitorBrightness(nint hMonitor, uint dwNewBrightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVCPFeatureAndVCPFeatureReply(
        nint hMonitor,
        byte bVCPCode,
        out int pvct,
        out uint pdwCurrentValue,
        out uint pdwMaximumValue);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetVCPFeature(nint hMonitor, byte bVCPCode, uint dwNewValue);

    private readonly object _sync = new();
    private List<MonitorBrightnessEntry> _cached = [];

    // Latest-wins per-monitor I2C write coalescer (prevents queuing 30 overlapping DDC transactions during slider drag)
    private readonly ConcurrentDictionary<string, int> _pendingTargets = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _activeWriters = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<MonitorBrightnessEntry> GetCachedMonitors()
    {
        lock (_sync)
            return [.. _cached];
    }

    public IReadOnlyList<MonitorBrightnessEntry> DiscoverMonitors()
    {
        var discovered = new List<MonitorBrightnessEntry>();
        var edidMap = QueryWmiEdidFriendlyNames();

        // 1. Check internal laptop panel via WMI
        int? wmiBrightness = TryReadWmiBrightness();
        if (wmiBrightness.HasValue)
        {
            discovered.Add(new MonitorBrightnessEntry(
                Id: "internal-wmi",
                Name: "Dahili Ekran",
                BrightnessPercent: wmiBrightness.Value,
                IsInternal: true,
                IsPrimary: true,
                DeviceName: "WMI_INTERNAL",
                MinValue: 0,
                MaxValue: 100));
        }

        // 2. Enumerate physical HMONITOR displays for DDC/CI external hardware brightness
        var hMonitors = new List<(nint Handle, string DeviceName, bool IsPrimary)>();
        EnumDisplayMonitors(0, 0, (nint hMon, nint _, ref RECT _, nint _) =>
        {
            var mi = new MONITORINFOEXW { cbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>() };
            if (GetMonitorInfoW(hMon, ref mi))
            {
                hMonitors.Add((hMon, mi.szDevice, (mi.dwFlags & 1u) != 0));
            }
            return true;
        }, 0);

        int extIndex = 1;
        foreach (var (hMon, devName, isPrimary) in hMonitors)
        {
            try
            {
                if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out uint count) || count == 0 || count > 4)
                    continue;

                var physicals = new PHYSICAL_MONITOR[count];
                if (!GetPhysicalMonitorsFromHMONITOR(hMon, count, physicals))
                    continue;

                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        var pm = physicals[i];
                        // NOTE: Windows dxva2.dll returns 0-based index handles (0, 1, 2...) where 0 IS A VALID physical monitor handle!
                        bool readOk = false;
                        uint minVal = 0;
                        uint curVal = 0;
                        uint maxVal = 100;

                        if (GetMonitorBrightness(pm.hPhysicalMonitor, out uint hlMin, out uint hlCur, out uint hlMax)
                            && hlMax > hlMin && hlMax <= 1000)
                        {
                            minVal = hlMin;
                            curVal = hlCur;
                            maxVal = hlMax;
                            readOk = true;
                        }
                        else if (GetVCPFeatureAndVCPFeatureReply(pm.hPhysicalMonitor, VcpCodeBrightness, out _, out uint vcpCur, out uint vcpMax)
                            && vcpMax > 0 && vcpMax <= 1000)
                        {
                            minVal = 0;
                            curVal = vcpCur;
                            maxVal = vcpMax;
                            readOk = true;
                        }

                        if (readOk)
                        {
                            double span = Math.Max(1.0, maxVal - minVal);
                            int pct = Math.Clamp((int)Math.Round(((curVal - minVal) / span) * 100.0), 0, 100);
                            string friendly = ResolveFriendlyMonitorName(devName, pm.szPhysicalMonitorDescription, edidMap, extIndex);
                            string cleanDev = SanitizeMonitorIdSegment(devName);
                            string id = $"ddc-{cleanDev}-{i}";

                            discovered.Add(new MonitorBrightnessEntry(
                                Id: id,
                                Name: friendly,
                                BrightnessPercent: pct,
                                IsInternal: false,
                                IsPrimary: isPrimary,
                                DeviceName: devName,
                                MinValue: minVal,
                                MaxValue: maxVal));
                            extIndex++;
                        }
                    }
                }
                finally
                {
                    DestroyPhysicalMonitors(count, physicals);
                }
            }
            catch
            {
                // Monitor does not support DDC/CI or driver rejected I2C transaction
            }
        }

        lock (_sync)
        {
            _cached = discovered;
            return [.. _cached];
        }
    }

    /// <summary>
    /// Non-blocking latest-wins hardware brightness update. Rapid slider events collapse cleanly into a single smooth I2C write loop.
    /// </summary>
    public void QueueSetMonitorBrightness(string monitorId, int brightnessPercent)
    {
        if (string.IsNullOrWhiteSpace(monitorId))
            return;

        int clamped = Math.Clamp(brightnessPercent, 0, 100);
        UpdateCachedPercent(monitorId, clamped);
        _pendingTargets[monitorId] = clamped;

        if (!_activeWriters.TryAdd(monitorId, 1))
            return;

        _ = Task.Run(() =>
        {
            try
            {
                while (_pendingTargets.TryRemove(monitorId, out int nextVal))
                {
                    TrySetMonitorBrightness(monitorId, nextVal);
                }
            }
            finally
            {
                _activeWriters.TryRemove(monitorId, out _);
                // Re-check in case a final value arrived right before TryRemove
                if (_pendingTargets.TryGetValue(monitorId, out int tailVal))
                {
                    QueueSetMonitorBrightness(monitorId, tailVal);
                }
            }
        });
    }

    public bool TrySetMonitorBrightness(string monitorId, int brightnessPercent)
    {
        int clamped = Math.Clamp(brightnessPercent, 0, 100);

        if (string.Equals(monitorId, "internal-wmi", StringComparison.OrdinalIgnoreCase))
        {
            int safeWmi = Math.Clamp(clamped, 5, 100);
            bool ok = TryWriteWmiBrightness(safeWmi);
            if (ok) UpdateCachedPercent(monitorId, safeWmi);
            return ok;
        }

        MonitorBrightnessEntry? entry;
        lock (_sync)
            entry = _cached.FirstOrDefault(m => string.Equals(m.Id, monitorId, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            return false;

        nint targetHMon = 0;
        EnumDisplayMonitors(0, 0, (nint hMon, nint _, ref RECT _, nint _) =>
        {
            var mi = new MONITORINFOEXW { cbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>() };
            if (GetMonitorInfoW(hMon, ref mi) && string.Equals(mi.szDevice, entry.DeviceName, StringComparison.OrdinalIgnoreCase))
            {
                targetHMon = hMon;
                return false;
            }
            return true;
        }, 0);

        if (targetHMon == 0)
            return false;

        try
        {
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(targetHMon, out uint count) || count == 0 || count > 4)
                return false;

            var physicals = new PHYSICAL_MONITOR[count];
            if (!GetPhysicalMonitorsFromHMONITOR(targetHMon, count, physicals))
                return false;

            try
            {
                int physIdx = 0;
                var parts = entry.Id.Split('-');
                if (parts.Length >= 3 && int.TryParse(parts[^1], out int parsed))
                    physIdx = Math.Clamp(parsed, 0, (int)count - 1);

                uint minVal = entry.MinValue;
                uint maxVal = entry.MaxValue > minVal ? entry.MaxValue : 100u;
                uint targetVal = (uint)Math.Clamp(Math.Round(minVal + (clamped / 100.0) * (maxVal - minVal)), minVal, maxVal);
                bool ok = SetMonitorBrightness(physicals[physIdx].hPhysicalMonitor, targetVal)
                    || SetVCPFeature(physicals[physIdx].hPhysicalMonitor, VcpCodeBrightness, targetVal);
                if (ok)
                    UpdateCachedPercent(monitorId, clamped);
                return ok;
            }
            finally
            {
                DestroyPhysicalMonitors(count, physicals);
            }
        }
        catch
        {
            return false;
        }
    }

    private void UpdateCachedPercent(string monitorId, int percent)
    {
        lock (_sync)
        {
            for (int i = 0; i < _cached.Count; i++)
            {
                if (string.Equals(_cached[i].Id, monitorId, StringComparison.OrdinalIgnoreCase))
                {
                    _cached[i] = _cached[i] with { BrightnessPercent = percent };
                    break;
                }
            }
        }
    }

    public static string SanitizeMonitorIdSegment(string rawDeviceName)
    {
        var chars = (rawDeviceName ?? string.Empty).Where(char.IsLetterOrDigit).ToArray();
        return chars.Length > 0 ? new string(chars) : "DISP";
    }

    private static Dictionary<string, string> QueryWmiEdidFriendlyNames()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT InstanceName, UserFriendlyName FROM WmiMonitorID");
            using var col = searcher.Get();
            foreach (ManagementObject obj in col)
            {
                using (obj)
                {
                    string inst = obj["InstanceName"]?.ToString() ?? string.Empty;
                    if (obj["UserFriendlyName"] is ushort[] rawChars && rawChars.Length > 0)
                    {
                        var friendly = new string(rawChars.TakeWhile(c => c != 0).Select(c => (char)c).ToArray()).Trim();
                        if (!string.IsNullOrWhiteSpace(friendly))
                        {
                            string? hwCode = ExtractMonitorHardwareCode(inst);
                            if (!string.IsNullOrWhiteSpace(hwCode))
                                map[hwCode] = friendly;
                        }
                    }
                }
            }
        }
        catch { }
        return map;
    }

    public static string? ExtractMonitorHardwareCode(string? deviceOrInstanceId)
    {
        if (string.IsNullOrWhiteSpace(deviceOrInstanceId))
            return null;

        var segs = deviceOrInstanceId.Split('\\', '#', '&');
        return segs.Skip(1).FirstOrDefault(s => s.Length >= 5 && s.Length <= 10 && char.IsLetter(s[0]) && s.Any(char.IsDigit));
    }

    private static string ResolveFriendlyMonitorName(
        string gdiDeviceName,
        string physicalDesc,
        Dictionary<string, string> edidMap,
        int fallbackIndex)
    {
        try
        {
            var dd = new DISPLAY_DEVICEW { cb = (uint)Marshal.SizeOf<DISPLAY_DEVICEW>() };
            if (EnumDisplayDevicesW(gdiDeviceName, 0, ref dd, EddGetDeviceInterfaceName))
            {
                // 1. Prefer real EDID model name (e.g. "VA2715-QHD", "F-419") matched by hardware PnP code!
                string? hwCode = ExtractMonitorHardwareCode(dd.DeviceID);
                if (!string.IsNullOrWhiteSpace(hwCode) && edidMap.TryGetValue(hwCode, out var edidFriendly))
                {
                    return TrimDisplayName(edidFriendly);
                }

                string ds = (dd.DeviceString ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(ds) &&
                    !ds.Equals("Generic PnP Monitor", StringComparison.OrdinalIgnoreCase) &&
                    !ds.Equals("Genel PnP Monitör", StringComparison.OrdinalIgnoreCase))
                {
                    return TrimDisplayName(ds);
                }

                if (!string.IsNullOrWhiteSpace(hwCode))
                    return $"Monitör {fallbackIndex} ({hwCode})";
            }
        }
        catch { }

        string p = (physicalDesc ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(p) &&
            !p.Equals("Generic PnP Monitor", StringComparison.OrdinalIgnoreCase) &&
            !p.Equals("Genel PnP Monitör", StringComparison.OrdinalIgnoreCase))
        {
            return TrimDisplayName(p);
        }

        return $"Harici Ekran {fallbackIndex}";
    }

    private static string TrimDisplayName(string raw) =>
        raw.Length > 20 ? raw[..19] + "…" : raw;

    private static int? TryReadWmiBrightness()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            using var col = searcher.Get();
            foreach (ManagementObject obj in col)
            {
                using (obj)
                {
                    return Math.Clamp(Convert.ToInt32(obj["CurrentBrightness"]), 0, 100);
                }
            }
        }
        catch { }
        return null;
    }

    private static bool TryWriteWmiBrightness(int percent)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            using var col = searcher.Get();
            bool any = false;
            foreach (ManagementObject obj in col)
            {
                using (obj)
                {
                    obj.InvokeMethod("WmiSetBrightness", [1u, (byte)percent]);
                    any = true;
                }
            }
            return any;
        }
        catch
        {
            return false;
        }
    }
}
