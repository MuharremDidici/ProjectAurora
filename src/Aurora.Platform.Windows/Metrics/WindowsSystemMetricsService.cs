using System.Management;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using Aurora.Core.Contracts;
using Aurora.Core.Models;
using Aurora.Platform.Windows.Win32;

namespace Aurora.Platform.Windows.Metrics;

public sealed class WindowsSystemMetricsService : ISystemMetricsService
{
    private static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromSeconds(5) };

    private ulong _prevIdle;
    private ulong _prevKernel;
    private ulong _prevUser;
    private long _prevRxBytes;
    private long _prevTxBytes;
    private DateTimeOffset _prevNetTimestamp = DateTimeOffset.MinValue;
    private readonly string _gpuAdapterName;
    private readonly bool _hasDedicatedGpu;

    public WindowsSystemMetricsService()
    {
        (_gpuAdapterName, _hasDedicatedGpu) = DetectPrimaryGpuAdapter();
        PrimeCounters();
    }

    private void PrimeCounters()
    {
        if (NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            _prevIdle = idle.ToUInt64();
            _prevKernel = kernel.ToUInt64();
            _prevUser = user.ToUInt64();
        }

        (_prevRxBytes, _prevTxBytes) = ReadTotalNetworkBytes();
        _prevNetTimestamp = DateTimeOffset.UtcNow;
    }

    public SystemMetricsSnapshot SampleMetrics()
    {
        double cpuPct = SampleCpuPercent();
        var (ramPct, usedGb, totalGb) = SampleMemory();
        var (rxKbps, txKbps) = SampleNetworkThroughput();

        return new SystemMetricsSnapshot(
            CpuUsagePercent: cpuPct,
            RamUsagePercent: ramPct,
            RamUsedGb: usedGb,
            RamTotalGb: totalGb,
            GpuUsagePercent: 0.0,
            IsGpuAvailable: _hasDedicatedGpu,
            GpuAdapterName: _gpuAdapterName,
            NetworkDownloadKbps: rxKbps,
            NetworkUploadKbps: txKbps,
            SampledAtUtc: DateTimeOffset.UtcNow);
    }

    private double SampleCpuPercent()
    {
        if (!NativeMethods.GetSystemTimes(out var idleFt, out var kernelFt, out var userFt))
            return 0.0;

        ulong idle = idleFt.ToUInt64();
        ulong kernel = kernelFt.ToUInt64();
        ulong user = userFt.ToUInt64();

        ulong idleDiff = idle - _prevIdle;
        ulong totalDiff = (kernel - _prevKernel) + (user - _prevUser);

        _prevIdle = idle;
        _prevKernel = kernel;
        _prevUser = user;

        if (totalDiff == 0)
            return 0.0;

        double busy = 100.0 * (1.0 - ((double)idleDiff / totalDiff));
        return Math.Round(Math.Clamp(busy, 0.0, 100.0), 1);
    }

    private static (double Percent, double UsedGb, double TotalGb) SampleMemory()
    {
        var mem = new NativeMethods.MEMORYSTATUSEX
        {
            dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>()
        };

        if (!NativeMethods.GlobalMemoryStatusEx(ref mem) || mem.ullTotalPhys == 0)
            return (0, 0, 0);

        double totalGb = mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
        double availGb = mem.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
        double usedGb = Math.Max(0, totalGb - availGb);
        return (mem.dwMemoryLoad, Math.Round(usedGb, 1), Math.Round(totalGb, 1));
    }

    private (double RxKbps, double TxKbps) SampleNetworkThroughput()
    {
        var now = DateTimeOffset.UtcNow;
        var (rx, tx) = ReadTotalNetworkBytes();
        double elapsedSec = (now - _prevNetTimestamp).TotalSeconds;

        if (elapsedSec <= 0.05 || _prevNetTimestamp == DateTimeOffset.MinValue)
        {
            _prevRxBytes = rx;
            _prevTxBytes = tx;
            _prevNetTimestamp = now;
            return (0, 0);
        }

        double rxKbps = Math.Max(0, (rx - _prevRxBytes) / 1024.0 / elapsedSec);
        double txKbps = Math.Max(0, (tx - _prevTxBytes) / 1024.0 / elapsedSec);

        _prevRxBytes = rx;
        _prevTxBytes = tx;
        _prevNetTimestamp = now;

        return (Math.Round(rxKbps, 1), Math.Round(txKbps, 1));
    }

    private static (long Rx, long Tx) ReadTotalNetworkBytes()
    {
        long rx = 0;
        long tx = 0;
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up ||
                    nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    continue;

                var stats = nic.GetIPv4Statistics();
                rx += stats.BytesReceived;
                tx += stats.BytesSent;
            }
        }
        catch
        {
            // Ignore transient NIC enumeration error
        }

        return (rx, tx);
    }

    private static (string Name, bool Available) DetectPrimaryGpuAdapter()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            using var list = searcher.Get();
            var names = new List<string>();
            foreach (ManagementObject mo in list)
            {
                using (mo)
                {
                    var n = mo["Name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(n) && !n.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                        names.Add(n.Trim());
                }
            }

            var preferred = names.FirstOrDefault(n =>
                n.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Arc", StringComparison.OrdinalIgnoreCase)) ?? names.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(preferred))
                return (preferred, true);
        }
        catch
        {
            // Fallback
        }

        return ("Grafik Bağdaştırıcısı Yok", false);
    }

    public async Task<WeatherSnapshot> GetWeatherAsync(
        string cityName,
        double latitude,
        double longitude,
        bool allowNetwork,
        CancellationToken ct = default)
    {
        if (!allowNetwork)
        {
            return new WeatherSnapshot(
                IsLiveNetworkData: false,
                LocationName: cityName,
                TemperatureCelsius: 19.0,
                ConditionSummary: "Çevrimdışı Mod (Ağ İsteği Kapalı)",
                HumidityPercent: 58,
                WindKmh: 12.0,
                DataSourceDisclosure: "Gizlilik Korumalı Yerel Mod — Canlı hava durumu için Ayarlar > Widget Ağ İznini açın.",
                UpdatedAtUtc: DateTimeOffset.UtcNow);
        }

        try
        {
            var url = FormattableString.Invariant(
                $"https://api.open-meteo.com/v1/forecast?latitude={latitude:F4}&longitude={longitude:F4}&current=temperature_2m,relative_humidity_2m,wind_speed_10m,weather_code");
            using var doc = await SharedHttp.GetFromJsonAsync<JsonDocument>(url, ct);
            if (doc is not null && doc.RootElement.TryGetProperty("current", out var cur))
            {
                double temp = cur.GetProperty("temperature_2m").GetDouble();
                int hum = cur.GetProperty("relative_humidity_2m").GetInt32();
                double wind = cur.GetProperty("wind_speed_10m").GetDouble();
                int code = cur.GetProperty("weather_code").GetInt32();

                return new WeatherSnapshot(
                    IsLiveNetworkData: true,
                    LocationName: cityName,
                    TemperatureCelsius: Math.Round(temp, 1),
                    ConditionSummary: MapWmoWeatherCode(code),
                    HumidityPercent: hum,
                    WindKmh: Math.Round(wind, 1),
                    DataSourceDisclosure: "Kaynak: Open-Meteo API (Kullanıcı İzinli HTTPS)",
                    UpdatedAtUtc: DateTimeOffset.UtcNow);
            }
        }
        catch
        {
            // Graceful offline fallback per DEC-007
        }

        return new WeatherSnapshot(
            IsLiveNetworkData: false,
            LocationName: cityName,
            TemperatureCelsius: 19.0,
            ConditionSummary: "Çevrimdışı Yedek Görünüm",
            HumidityPercent: 58,
            WindKmh: 12.0,
            DataSourceDisclosure: "Ağ erişilemedi — çevrimdışı yerel görünüm.",
            UpdatedAtUtc: DateTimeOffset.UtcNow);
    }

    private static string MapWmoWeatherCode(int code) => code switch
    {
        0 => "Açık ve Güneşli",
        1 or 2 => "Parçalı Bulutlu",
        3 => "Kapalı Bulutlu",
        45 or 48 => "Sisli",
        51 or 53 or 55 or 61 or 63 or 65 => "Yağmurlu",
        71 or 73 or 75 => "Kar Yağışlı",
        95 or 96 or 99 => "Gök Gürültülü Fırtına",
        _ => "Çok Bulutlu"
    };

    private static readonly HashSet<string> ProtectedSystemProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "idle", "system", "registry", "smss", "csrss", "wininit", "services", "lsass", "svchost",
        "dwm", "explorer", "fontdrvhost", "winlogon", "sihost", "taskhostw", "ctfmon", "audiodg",
        "memory compression", "aurora.app", "msedgewebview2", "securityhealthservice", "msmpeng"
    };

    public static TopProcessSnapshot[] GetTopMemoryProcesses(int count = 3)
    {
        var grouped = new Dictionary<string, (int LeadPid, long TotalBytes, int InstanceCount)>(StringComparer.OrdinalIgnoreCase);
        int selfPid = Environment.ProcessId;

        try
        {
            foreach (var p in System.Diagnostics.Process.GetProcesses())
            {
                using (p)
                {
                    try
                    {
                        if (p.Id <= 4 || p.Id == selfPid) continue;
                        string name = p.ProcessName;
                        if (string.IsNullOrWhiteSpace(name) || ProtectedSystemProcesses.Contains(name))
                            continue;

                        long ws = p.WorkingSet64;
                        if (ws < 20L * 1024 * 1024) continue;

                        if (grouped.TryGetValue(name, out var existing))
                        {
                            int lead = ws > existing.TotalBytes ? p.Id : existing.LeadPid;
                            grouped[name] = (lead, existing.TotalBytes + ws, existing.InstanceCount + 1);
                        }
                        else
                        {
                            grouped[name] = (p.Id, ws, 1);
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        return grouped
            .OrderByDescending(kv => kv.Value.TotalBytes)
            .Take(Math.Clamp(count, 1, 6))
            .Select(kv => new TopProcessSnapshot(
                Pid: kv.Value.LeadPid,
                Name: kv.Key,
                DisplayName: FormatProcessLabel(kv.Key),
                MemoryMb: Math.Round(kv.Value.TotalBytes / (1024.0 * 1024.0), 0),
                Instances: kv.Value.InstanceCount))
            .ToArray();
    }

    public static bool TryTerminateUserProcessSafe(string processName, int fallbackPid)
    {
        if (string.IsNullOrWhiteSpace(processName) || ProtectedSystemProcesses.Contains(processName))
            return false;

        int selfPid = Environment.ProcessId;
        bool killedAny = false;
        try
        {
            var targets = System.Diagnostics.Process.GetProcessesByName(processName);
            if (targets.Length > 0)
            {
                foreach (var proc in targets)
                {
                    using (proc)
                    {
                        if (proc.Id == selfPid || proc.Id <= 4) continue;
                        try
                        {
                            if (!proc.CloseMainWindow())
                                proc.Kill(entireProcessTree: false);
                            killedAny = true;
                        }
                        catch
                        {
                            try { proc.Kill(entireProcessTree: false); killedAny = true; } catch { }
                        }
                    }
                }
            }
            else if (fallbackPid > 4 && fallbackPid != selfPid)
            {
                using var p = System.Diagnostics.Process.GetProcessById(fallbackPid);
                if (!ProtectedSystemProcesses.Contains(p.ProcessName))
                {
                    p.Kill(entireProcessTree: false);
                    killedAny = true;
                }
            }
        }
        catch { }

        return killedAny;
    }

    private static string FormatProcessLabel(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Uygulama";
        return raw.ToLowerInvariant() switch
        {
            "chrome" => "Google Chrome",
            "msedge" => "Microsoft Edge",
            "opera" => "Opera",
            "brave" => "Brave",
            "firefox" => "Firefox",
            "code" => "VS Code",
            "devenv" => "Visual Studio",
            "spotify" => "Spotify",
            "discord" => "Discord",
            "ms-teams" => "Microsoft Teams",
            "whatsapp" => "WhatsApp",
            "obs64" => "OBS Studio",
            "windowsterminal" => "Terminal",
            _ => char.ToUpperInvariant(raw[0]) + raw[1..]
        };
    }
}

public sealed record TopProcessSnapshot(
    int Pid,
    string Name,
    string DisplayName,
    double MemoryMb,
    int Instances);
