using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using Aurora.Platform.Windows.DisplayAndTaskbar;
using Aurora.Platform.Windows.Win32;
using Microsoft.Win32;

namespace Aurora.Platform.Windows.ShellExtras;

public sealed record KeyboardLayoutInfo(
    string ShortCode,
    string DisplayName,
    long Handle);

public sealed class KeyboardLayoutService
{
    private const uint WmInputLangChangeRequest = 0x0050;
    private const uint KlfActivate = 0x00000001;

    [DllImport("user32.dll")]
    private static extern nint GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    private static extern int GetKeyboardLayoutList(int nBuff, [Out] nint[]? lpList);

    [DllImport("user32.dll")]
    private static extern nint ActivateKeyboardLayout(nint hkl, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(nint hWnd, uint msg, nint wParam, nint lParam);

    public KeyboardLayoutInfo GetActiveLayout(nint foregroundHwnd = 0)
    {
        try
        {
            nint hwnd = foregroundHwnd != 0 ? foregroundHwnd : NativeMethods.GetForegroundWindow();
            uint tid = 0;
            if (hwnd != 0)
                tid = NativeMethods.GetWindowThreadProcessId(hwnd, out _);

            nint hkl = GetKeyboardLayout(tid);
            return DescribeHkl(hkl);
        }
        catch
        {
            return new KeyboardLayoutInfo("TR", "Türkçe Q", 0x041F041F);
        }
    }

    public KeyboardLayoutInfo CycleKeyboardLayout(nint targetExternalHwnd)
    {
        try
        {
            int count = GetKeyboardLayoutList(0, null);
            if (count <= 1)
                return GetActiveLayout(targetExternalHwnd);

            var list = new nint[count];
            int filled = GetKeyboardLayoutList(count, list);
            if (filled <= 1)
                return GetActiveLayout(targetExternalHwnd);

            var current = GetActiveLayout(targetExternalHwnd);
            int curIdx = Array.FindIndex(list, 0, filled, h => (long)h == current.Handle);
            if (curIdx < 0)
            {
                ushort curLang = (ushort)(current.Handle & 0xFFFF);
                curIdx = Array.FindIndex(list, 0, filled, h => ((ushort)((long)h & 0xFFFF)) == curLang);
            }

            nint nextHkl = list[(Math.Max(0, curIdx) + 1) % filled];
            ActivateKeyboardLayout(nextHkl, KlfActivate);

            if (targetExternalHwnd != 0)
            {
                PostMessageW(targetExternalHwnd, WmInputLangChangeRequest, 0, nextHkl);
            }

            return DescribeHkl(nextHkl);
        }
        catch
        {
            return GetActiveLayout(targetExternalHwnd);
        }
    }

    private static KeyboardLayoutInfo DescribeHkl(nint hkl)
    {
        ushort langId = (ushort)((long)hkl & 0xFFFF);
        ushort layoutId = (ushort)(((ulong)(long)hkl >> 16) & 0xFFFF);

        return langId switch
        {
            0x041F => new KeyboardLayoutInfo("TR", layoutId == 0x041F ? "Türkçe Q" : "Türkçe F/Özel", (long)hkl),
            0x0409 => new KeyboardLayoutInfo("EN", "English (US)", (long)hkl),
            0x0809 => new KeyboardLayoutInfo("EN", "English (UK)", (long)hkl),
            0x0407 => new KeyboardLayoutInfo("DE", "Deutsch", (long)hkl),
            0x040C => new KeyboardLayoutInfo("FR", "Français", (long)hkl),
            0x0419 => new KeyboardLayoutInfo("RU", "Русский", (long)hkl),
            0x0401 => new KeyboardLayoutInfo("AR", "العربية", (long)hkl),
            _ => BuildFallbackCulture(langId, hkl)
        };
    }

    private static KeyboardLayoutInfo BuildFallbackCulture(ushort langId, nint hkl)
    {
        try
        {
            if (langId > 0)
            {
                var ci = CultureInfo.GetCultureInfo(langId);
                return new KeyboardLayoutInfo(
                    ci.TwoLetterISOLanguageName.ToUpperInvariant(),
                    ci.NativeName,
                    (long)hkl);
            }
        }
        catch { }

        return new KeyboardLayoutInfo("TR", "Klavye", (long)hkl);
    }
}

public sealed record BackgroundTrayAppEntry(
    string Key,
    string DisplayName,
    string Subtitle,
    string ProcessName,
    string? ExecutablePath,
    string? LaunchUriOverride,
    int RepresentativePid,
    int ProcessCount,
    double MemoryMb,
    byte[]? IconPngBytes,
    bool CanQuitCleanly);

public sealed class BackgroundTrayAppsService
{
    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(ref Guid rfid, uint dwFlags, nint hToken, out nint ppszPath);

    [DllImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSuspendState(
        [MarshalAs(UnmanagedType.Bool)] bool hibernate,
        [MarshalAs(UnmanagedType.Bool)] bool forceCritical,
        [MarshalAs(UnmanagedType.Bool)] bool disableWakeEvent);

    private readonly int _currentSessionId = Process.GetCurrentProcess().SessionId;
    private readonly int _currentPid = Environment.ProcessId;

    private static readonly HashSet<string> ExcludedShellProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "monotificationux", "msedgewebview2", "aurora.app", "runtimebroker",
        "sihost", "shellexperiencehost", "searchhost", "startmenuexperiencehost",
        "textinputhost", "dwm", "ctfmon", "conhost", "svchost", "dllhost", "taskhostw",
        "smartscreen", "lockapp", "applicationframehost", "shellhost", "widgetservice", "widgetboard"
    };

    private static readonly HashSet<string> BrowserProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "msedge", "chrome", "brave", "opera", "firefox", "vivaldi", "arc"
    };

    private static readonly Dictionary<string, (string FriendlyName, string? UriOverride, bool AllowQuit)> KnownTrayFamilies =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["steam"] = ("Steam", "steam://open/main", true),
            ["steamwebhelper"] = ("Steam", "steam://open/main", true),
            ["discord"] = ("Discord", null, true),
            ["spotify"] = ("Spotify", null, true),
            ["spotifylauncher"] = ("Spotify", null, true),
            ["telegram"] = ("Telegram", null, true),
            ["whatsapp"] = ("WhatsApp", null, true),
            ["ms-teams"] = ("Microsoft Teams", null, true),
            ["teams"] = ("Microsoft Teams", null, true),
            ["onedrive"] = ("Microsoft OneDrive", null, true),
            ["onedrive.sync.service"] = ("Microsoft OneDrive", null, true),
            ["securityhealthsystray"] = ("Windows Güvenliği", "windowsdefender:", false),
            ["nvcontainer"] = ("NVIDIA Denetim", null, false),
            ["nvdisplay.container"] = ("NVIDIA Denetim", null, false),
            ["nvidia share"] = ("NVIDIA Overlay", null, true),
            ["nvidia overlay"] = ("NVIDIA Overlay", null, true),
            ["nvidia app"] = ("NVIDIA App", null, true),
            ["pangpa"] = ("GlobalProtect VPN", null, true),
            ["rustwtm"] = ("RustWTM", null, true),
            ["mumunxmain"] = ("MuMuPlayer", null, true),
            ["rdpwin"] = ("Secret Server RDP", null, true),
            ["batterywidgethost"] = ("Lenovo Pil Widget", null, false),
            ["phoneexperiencehost"] = ("Telefon Bağlantısı", "ms-phone:", true),
            ["nahimicnotiftsys"] = ("Nahimic Audio", null, false),
            ["nahimic3"] = ("Nahimic Audio", null, false),
            ["rtkauduservice64"] = ("Realtek Audio", "ms-settings:sound", false),
            ["steelseriesgg"] = ("SteelSeries GG", null, true),
            ["steelseriessonar"] = ("SteelSeries Sonar", null, true),
            ["lghub"] = ("Logitech G HUB", null, true),
            ["lghub_agent"] = ("Logitech G HUB", null, true),
            ["vgtray"] = ("Riot Vanguard", null, true),
            ["epicgameslauncher"] = ("Epic Games", null, true),
            ["battle.net"] = ("Battle.net", null, true),
            ["eadesktop"] = ("EA App", null, true),
            ["powertoys"] = ("Microsoft PowerToys", null, true),
            ["powertoys.awake"] = ("Microsoft PowerToys", null, true),
            ["sharex"] = ("ShareX", null, true),
            ["everything"] = ("Everything", null, true),
            ["obs64"] = ("OBS Studio", null, true),
            ["qbittorrent"] = ("qBittorrent", null, true),
            ["docker desktop"] = ("Docker Desktop", null, true),
            ["com.docker.backend"] = ("Docker Desktop", null, true),
            ["slack"] = ("Slack", null, true),
            ["zoom"] = ("Zoom", null, true),
            ["anydesk"] = ("AnyDesk", null, true),
            ["teamviewer"] = ("TeamViewer", null, true),
            ["tailscale-ipn"] = ("Tailscale VPN", null, true),
            ["warp-taskbar"] = ("Cloudflare WARP", null, true),
            ["lenovovantage"] = ("Lenovo Vantage", null, true),
            ["ollama app"] = ("Ollama", null, true),
            ["blitz"] = ("Blitz", null, true)
        };

    private sealed record NotifyRegInfo(string? ResolvedExePath, string? Tooltip, bool IsPromoted);

    private Dictionary<string, NotifyRegInfo>? _notifyRegistryCache;
    private DateTimeOffset _registryLoadedUtc = DateTimeOffset.MinValue;
    private List<BackgroundTrayAppEntry>? _cache;
    private DateTimeOffset _lastSampleUtc = DateTimeOffset.MinValue;

    public static void TriggerSystemSleep()
    {
        try { SetSuspendState(hibernate: false, forceCritical: false, disableWakeEvent: false); } catch { }
    }

    public static void OpenRecycleBin()
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", "shell:RecycleBinFolder") { UseShellExecute = true }); } catch { }
    }

    public IReadOnlyList<BackgroundTrayAppEntry> GetBackgroundTrayApps(
        IReadOnlySet<string>? openWindowProcessNames = null,
        bool forceRefresh = false)
    {
        if (!forceRefresh && _cache is not null && (DateTimeOffset.UtcNow - _lastSampleUtc).TotalSeconds < 4)
            return _cache;

        var notifyMap = GetWindows11NotifyIconMap(forceRefresh);

        var buckets = new Dictionary<string, (
            string FriendlyName,
            string Subtitle,
            string PrimaryProcName,
            string? BestExePath,
            string? UriOverride,
            int RepPid,
            int Count,
            long WorkingSetBytes,
            bool AllowQuit,
            bool IsPromoted)>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                using (proc)
                {
                    if (proc.Id == _currentPid)
                        continue;

                    int sess;
                    try { sess = proc.SessionId; } catch { continue; }
                    if (sess != _currentSessionId)
                        continue;

                    string pName = proc.ProcessName;
                    if (string.IsNullOrWhiteSpace(pName) || ExcludedShellProcesses.Contains(pName))
                        continue;

                    bool inKnown = KnownTrayFamilies.TryGetValue(pName, out var knownMeta);
                    bool inWindowsTrayReg = notifyMap.TryGetValue(pName, out var regMeta);

                    if (!inKnown && !inWindowsTrayReg)
                        continue;

                    // If a web browser already has an open window visible on the Dock, don't duplicate it as a tray-only item
                    if (BrowserProcessNames.Contains(pName) &&
                        openWindowProcessNames is not null &&
                        openWindowProcessNames.Contains(pName))
                    {
                        continue;
                    }

                    string? exePath = null;
                    long ws = 0;
                    try { ws = proc.WorkingSet64; } catch { }
                    try { exePath = proc.MainModule?.FileName; } catch { }
                    if (string.IsNullOrWhiteSpace(exePath))
                        exePath = regMeta?.ResolvedExePath;

                    string friendlyName = inKnown
                        ? knownMeta.FriendlyName
                        : ResolveFriendlyProductName(exePath, regMeta?.Tooltip, pName);

                    string subtitle = SanitizeTooltip(regMeta?.Tooltip, friendlyName);
                    string? uriOverride = inKnown ? knownMeta.UriOverride : null;
                    bool allowQuit = inKnown ? knownMeta.AllowQuit : !IsCoreSystemBinary(exePath);
                    bool promoted = regMeta?.IsPromoted ?? false;

                    if (!buckets.TryGetValue(friendlyName, out var existing))
                    {
                        buckets[friendlyName] = (
                            friendlyName,
                            subtitle,
                            pName,
                            exePath,
                            uriOverride,
                            proc.Id,
                            1,
                            ws,
                            allowQuit,
                            promoted);
                    }
                    else
                    {
                        string? chosenPath = existing.BestExePath;
                        string chosenProc = existing.PrimaryProcName;
                        int chosenPid = existing.RepPid;
                        string chosenSub = !string.IsNullOrWhiteSpace(existing.Subtitle) ? existing.Subtitle : subtitle;

                        if (!string.IsNullOrWhiteSpace(exePath) &&
                            (string.IsNullOrWhiteSpace(chosenPath) || !pName.Contains("helper", StringComparison.OrdinalIgnoreCase)))
                        {
                            chosenPath = exePath;
                            chosenProc = pName;
                            chosenPid = proc.Id;
                        }

                        buckets[friendlyName] = (
                            friendlyName,
                            chosenSub,
                            chosenProc,
                            chosenPath,
                            existing.UriOverride ?? uriOverride,
                            chosenPid,
                            existing.Count + 1,
                            existing.WorkingSetBytes + ws,
                            existing.AllowQuit && allowQuit,
                            existing.IsPromoted || promoted);
                    }
                }
            }
        }
        catch
        {
            // Ignore transient process enumeration races
        }

        var list = new List<BackgroundTrayAppEntry>(buckets.Count);
        foreach (var b in buckets.Values
            .OrderByDescending(x => x.IsPromoted)
            .ThenByDescending(x => x.AllowQuit)
            .ThenBy(x => x.FriendlyName, StringComparer.CurrentCultureIgnoreCase))
        {
            byte[]? icon = !string.IsNullOrWhiteSpace(b.BestExePath)
                ? Win32IconExtractor.ResolveShortcutIconPng(b.BestExePath)
                : null;

            list.Add(new BackgroundTrayAppEntry(
                Key: b.PrimaryProcName.ToLowerInvariant(),
                DisplayName: b.FriendlyName,
                Subtitle: b.Subtitle,
                ProcessName: b.PrimaryProcName,
                ExecutablePath: b.BestExePath,
                LaunchUriOverride: b.UriOverride,
                RepresentativePid: b.RepPid,
                ProcessCount: b.Count,
                MemoryMb: Math.Round(b.WorkingSetBytes / (1024.0 * 1024.0), 1),
                IconPngBytes: icon,
                CanQuitCleanly: b.AllowQuit));
        }

        _cache = list;
        _lastSampleUtc = DateTimeOffset.UtcNow;
        return _cache;
    }

    private Dictionary<string, NotifyRegInfo> GetWindows11NotifyIconMap(bool force)
    {
        if (!force && _notifyRegistryCache is not null && (DateTimeOffset.UtcNow - _registryLoadedUtc).TotalSeconds < 25)
            return _notifyRegistryCache;

        var map = new Dictionary<string, NotifyRegInfo>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings");
            if (root is not null)
            {
                foreach (var subName in root.GetSubKeyNames())
                {
                    try
                    {
                        using var sub = root.OpenSubKey(subName);
                        if (sub is null) continue;

                        var rawExe = sub.GetValue("ExecutablePath") as string;
                        if (string.IsNullOrWhiteSpace(rawExe)) continue;

                        var resolved = ExpandKnownFolderPrefix(rawExe);
                        var procName = Path.GetFileNameWithoutExtension(resolved);
                        if (string.IsNullOrWhiteSpace(procName)) continue;

                        var tip = sub.GetValue("InitialTooltip") as string;
                        bool isPromoted = Convert.ToInt32(sub.GetValue("IsPromoted") ?? 0) != 0;
                        bool exists = File.Exists(resolved);

                        if (!map.TryGetValue(procName, out var prev))
                        {
                            map[procName] = new NotifyRegInfo(resolved, tip, isPromoted);
                        }
                        else
                        {
                            bool prevExists = !string.IsNullOrWhiteSpace(prev.ResolvedExePath) && File.Exists(prev.ResolvedExePath);
                            string? bestPath = (exists && !prevExists) ? resolved : (prev.ResolvedExePath ?? resolved);
                            string? bestTip = !string.IsNullOrWhiteSpace(tip) && !tip.StartsWith("notifyIcon", StringComparison.OrdinalIgnoreCase)
                                ? tip
                                : prev.Tooltip;
                            map[procName] = new NotifyRegInfo(bestPath, bestTip, prev.IsPromoted || isPromoted);
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        _notifyRegistryCache = map;
        _registryLoadedUtc = DateTimeOffset.UtcNow;
        return map;
    }

    private static string ExpandKnownFolderPrefix(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath) || !rawPath.StartsWith('{'))
            return rawPath;

        int closeIdx = rawPath.IndexOf('}');
        if (closeIdx <= 1)
            return rawPath;

        string guidStr = rawPath[1..closeIdx];
        string remainder = rawPath[(closeIdx + 1)..].TrimStart('\\');

        string? prefix = guidStr.ToUpperInvariant() switch
        {
            "6D809377-6AF0-444B-8957-A3773F02200E" => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E" => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "1AC14E77-02E7-4E5D-B744-2EB1AE5198B7" => Environment.SystemDirectory,
            "F38BF404-1D43-42F2-9305-67DE0B28FC23" => Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            _ => TryResolveShKnownFolder(guidStr)
        };

        return string.IsNullOrWhiteSpace(prefix) ? rawPath : Path.Combine(prefix, remainder);
    }

    private static string? TryResolveShKnownFolder(string guidText)
    {
        if (!Guid.TryParse(guidText, out var g))
            return null;

        nint ptr = 0;
        try
        {
            if (SHGetKnownFolderPath(ref g, 0, 0, out ptr) == 0 && ptr != 0)
                return Marshal.PtrToStringUni(ptr);
        }
        catch { }
        finally
        {
            if (ptr != 0)
                Marshal.FreeCoTaskMem(ptr);
        }
        return null;
    }

    private static string ResolveFriendlyProductName(string? exePath, string? tooltip, string procName)
    {
        if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
        {
            try
            {
                var vi = FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(vi.ProductName) && vi.ProductName.Length <= 34 &&
                    !vi.ProductName.Contains("Operating System", StringComparison.OrdinalIgnoreCase))
                {
                    return vi.ProductName.Trim();
                }
                if (!string.IsNullOrWhiteSpace(vi.FileDescription) && vi.FileDescription.Length <= 34)
                {
                    return vi.FileDescription.Trim();
                }
            }
            catch { }
        }

        if (!string.IsNullOrWhiteSpace(tooltip) &&
            !tooltip.StartsWith("notifyIcon", StringComparison.OrdinalIgnoreCase))
        {
            var firstLine = tooltip.Split(['\r', '\n', '·', '-'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(firstLine) && firstLine.Length is >= 2 and <= 30)
                return firstLine;
        }

        return char.ToUpperInvariant(procName[0]) + procName[1..];
    }

    private static string SanitizeTooltip(string? tooltip, string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(tooltip))
            return string.Empty;

        var clean = tooltip.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (clean.StartsWith("notifyIcon", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(clean, friendlyName, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return clean.Length > 42 ? clean[..41] + "…" : clean;
    }

    private static bool IsCoreSystemBinary(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
            return false;
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return exePath.StartsWith(winDir, StringComparison.OrdinalIgnoreCase);
    }

    public bool OpenOrRestoreTrayApp(string? exePath, string? uriOverride, int pid)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(uriOverride))
            {
                Process.Start(new ProcessStartInfo(uriOverride) { UseShellExecute = true });
                return true;
            }

            if (pid > 0)
            {
                try
                {
                    using var p = Process.GetProcessById(pid);
                    if (p.MainWindowHandle != 0)
                    {
                        NativeMethods.ShowWindow(p.MainWindowHandle, NativeMethods.SW_RESTORE);
                        NativeMethods.SetForegroundWindow(p.MainWindowHandle);
                        return true;
                    }
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo { FileName = exePath, UseShellExecute = true });
                return true;
            }
        }
        catch { }

        return false;
    }

    public int QuitTrayAppByProcessName(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return 0;

        int closed = 0;
        try
        {
            foreach (var p in Process.GetProcessesByName(processName))
            {
                using (p)
                {
                    if (p.Id == _currentPid || p.SessionId != _currentSessionId)
                        continue;

                    try
                    {
                        if (!p.CloseMainWindow())
                            p.Kill();
                        closed++;
                    }
                    catch { }
                }
            }
        }
        catch { }

        _cache = null;
        return closed;
    }
}

public sealed class WindowThumbnailCaptureService
{
    private const uint PwRenderFullContent = 0x00000002;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out NativeMethods.RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(nint hWnd, nint hdcBlt, uint nFlags);

    public string? CapturePreviewJpegBase64(nint hwnd, int maxWidth = 360, int maxHeight = 202)
    {
        if (hwnd == 0 || NativeMethods.IsIconic(hwnd) || !NativeMethods.IsWindowVisible(hwnd))
            return null;

        try
        {
            if (!GetWindowRect(hwnd, out var rc))
                return null;

            int srcW = rc.Width;
            int srcH = rc.Height;
            if (srcW < 40 || srcH < 40 || srcW > 7680 || srcH > 4320)
                return null;

            using var fullBmp = new Bitmap(srcW, srcH, PixelFormat.Format32bppRgb);
            bool captured;
            using (var gFull = Graphics.FromImage(fullBmp))
            {
                nint hdc = gFull.GetHdc();
                try
                {
                    captured = PrintWindow(hwnd, hdc, PwRenderFullContent);
                    if (!captured)
                        captured = PrintWindow(hwnd, hdc, 0);
                }
                finally
                {
                    gFull.ReleaseHdc(hdc);
                }
            }

            if (!captured)
                return null;

            double ratio = Math.Min((double)maxWidth / srcW, (double)maxHeight / srcH);
            int dstW = Math.Max(48, (int)Math.Round(srcW * ratio));
            int dstH = Math.Max(36, (int)Math.Round(srcH * ratio));

            using var thumb = new Bitmap(dstW, dstH, PixelFormat.Format24bppRgb);
            using (var gThumb = Graphics.FromImage(thumb))
            {
                gThumb.InterpolationMode = InterpolationMode.HighQualityBilinear;
                gThumb.CompositingQuality = CompositingQuality.HighSpeed;
                gThumb.DrawImage(fullBmp, new Rectangle(0, 0, dstW, dstH));
            }

            using var ms = new MemoryStream();
            var jpegEncoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
            if (jpegEncoder is not null)
            {
                using var encParams = new EncoderParameters(1);
                encParams.Param[0] = new EncoderParameter(Encoder.Quality, 84L);
                thumb.Save(ms, jpegEncoder, encParams);
            }
            else
            {
                thumb.Save(ms, ImageFormat.Png);
            }

            return Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return null;
        }
    }
}
