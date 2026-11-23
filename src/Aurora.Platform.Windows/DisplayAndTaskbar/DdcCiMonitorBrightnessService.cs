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
/// Runs completely zero-UAC (asInvoker) and releases every physical monitor handle immediately.
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

    public IReadOnlyList<MonitorBrightnessEntry> GetCachedMonitors()
    {
        lock (_sync)
            return [.. _cached];
    }

    public IReadOnlyList<MonitorBrightnessEntry> DiscoverMonitors()
    {
        var discovered = new List<MonitorBrightnessEntry>();

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
                        if (pm.hPhysicalMonitor == 0)
                            continue;

                        if (GetVCPFeatureAndVCPFeatureReply(pm.hPhysicalMonitor, VcpCodeBrightness, out _, out uint curVal, out uint maxVal)
                            && maxVal > 0 && maxVal <= 1000)
                        {
                            int pct = Math.Clamp((int)Math.Round((curVal / (double)maxVal) * 100.0), 0, 100);
                            string friendly = ResolveFriendlyMonitorName(devName, pm.szPhysicalMonitorDescription, extIndex);
                            string id = $"ddc:{devName}:{i}";

                            discovered.Add(new MonitorBrightnessEntry(
                                Id: id,
                                Name: friendly,
                                BrightnessPercent: pct,
                                IsInternal: false,
                                IsPrimary: isPrimary,
                                DeviceName: devName,
                                MinValue: 0,
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
                var parts = entry.Id.Split(':');
                if (parts.Length >= 3 && int.TryParse(parts[^1], out int parsed))
                    physIdx = Math.Clamp(parsed, 0, (int)count - 1);

                uint maxVal = entry.MaxValue > 0 ? entry.MaxValue : 100u;
                uint vcpValue = (uint)Math.Clamp(Math.Round((clamped / 100.0) * maxVal), 0, maxVal);
                bool ok = SetVCPFeature(physicals[physIdx].hPhysicalMonitor, VcpCodeBrightness, vcpValue);
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

    private static string ResolveFriendlyMonitorName(string gdiDeviceName, string physicalDesc, int fallbackIndex)
    {
        try
        {
            var dd = new DISPLAY_DEVICEW { cb = (uint)Marshal.SizeOf<DISPLAY_DEVICEW>() };
            if (EnumDisplayDevicesW(gdiDeviceName, 0, ref dd, EddGetDeviceInterfaceName))
            {
                string ds = (dd.DeviceString ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(ds) &&
                    !ds.Equals("Generic PnP Monitor", StringComparison.OrdinalIgnoreCase) &&
                    !ds.Equals("Genel PnP Monitör", StringComparison.OrdinalIgnoreCase))
                {
                    return TrimDisplayName(ds);
                }

                // Extract hardware PnP model code from DeviceID e.g. MONITOR\VSC1527\...
                if (!string.IsNullOrWhiteSpace(dd.DeviceID))
                {
                    var segs = dd.DeviceID.Split('\\', '#');
                    var code = segs.Skip(1).FirstOrDefault(s => s.Length >= 4 && s.Length <= 12 && char.IsLetter(s[0]));
                    if (!string.IsNullOrWhiteSpace(code))
                        return $"Monitör {fallbackIndex} ({code})";
                }
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
