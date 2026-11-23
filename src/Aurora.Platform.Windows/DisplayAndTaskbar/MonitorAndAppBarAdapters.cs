using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Aurora.Core.Contracts;
using Aurora.Core.Models;
using Aurora.Core.Primitives;
using Aurora.Platform.Windows.Win32;

namespace Aurora.Platform.Windows.DisplayAndTaskbar;

public sealed class MonitorTopologyService : IMonitorTopologyService
{
    public IReadOnlyList<MonitorDescriptor> GetMonitors()
    {
        var monitors = new List<MonitorDescriptor>();
        int index = 1;

        NativeMethods.EnumDisplayMonitors(0, 0, (nint hMonitor, nint _, ref NativeMethods.RECT _, nint _) =>
        {
            var mi = new NativeMethods.MONITORINFOEXW
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEXW>()
            };

            if (NativeMethods.GetMonitorInfoW(hMonitor, ref mi))
            {
                uint dpiX = 96;
                if (NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dx, out _) == 0 && dx > 0)
                    dpiX = dx;

                bool isPrimary = (mi.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;
                var deviceId = string.IsNullOrWhiteSpace(mi.szDevice) ? $"DISPLAY_{index}" : mi.szDevice.Trim();
                var label = isPrimary ? $"Birincil Ekran ({mi.rcMonitor.Width}×{mi.rcMonitor.Height})" : $"Ekran {index} ({mi.rcMonitor.Width}×{mi.rcMonitor.Height})";

                monitors.Add(new MonitorDescriptor(
                    DeviceId: deviceId,
                    FriendlyName: label,
                    IsPrimary: isPrimary,
                    EffectiveDpi: dpiX,
                    BoundsPx: new PhysicalPixelRect(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Width, mi.rcMonitor.Height),
                    WorkAreaPx: new PhysicalPixelRect(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Width, mi.rcWork.Height)));
                index++;
            }

            return true;
        }, 0);

        if (monitors.Count == 0)
        {
            monitors.Add(new MonitorDescriptor(
                "PRIMARY_FALLBACK",
                "Birincil Ekran",
                true,
                96,
                new PhysicalPixelRect(0, 0, 1920, 1080),
                new PhysicalPixelRect(0, 0, 1920, 1032)));
        }

        return monitors.OrderByDescending(m => m.IsPrimary).ToList();
    }

    public MonitorDescriptor GetPrimaryMonitor() =>
        GetMonitors().First(m => m.IsPrimary);
}

public sealed class Win32AppBarService(IAuroraLogger logger) : IAppBarService
{
    private const uint ABM_SETSTATE = 0x0000000A;
    private const uint SPI_SETWORKAREA = 0x002F;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_HIDEWINDOW = 0x0080;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    private const uint WM_SETTINGCHANGE = 0x001A;
    private static readonly nint HWND_BROADCAST = new(0xFFFF);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(nint hWnd);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindowW(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindowExW(nint hWndParent, nint hWndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(nint hWnd, [MarshalAs(UnmanagedType.Bool)] bool bEnable);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out NativeMethods.RECT lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(nint hWnd, char[] lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoW(uint uiAction, uint uiParam, ref NativeMethods.RECT pvParam, uint fWinIni);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SendNotifyMessageW(nint hWnd, uint msg, nuint wParam, nint lParam);

    private readonly ConcurrentDictionary<nint, uint> _registeredBars = new();
    private readonly Dictionary<string, NativeMethods.RECT> _savedMonitorWorkAreas = new(StringComparer.OrdinalIgnoreCase);
    private int? _originalNativeTaskbarState;
    private bool _nativeTaskbarSuppressed;
    private bool _disposed;

    public int? OriginalNativeTaskbarState => _originalNativeTaskbarState;
    public bool IsNativeTaskbarSuppressed => _nativeTaskbarSuppressed;

    public void SetNativeTaskbarHiddenWhileActive(bool hide)
    {
        if (hide)
        {
            HideNativeWindowsTaskbar();
        }
        else
        {
            RestoreNativeWindowsTaskbar();
        }
    }

    /// <summary>
    /// Completely hides and disables the native Windows taskbar across ALL monitors
    /// WITHOUT enabling Windows Auto-Hide (so moving the cursor to the bottom edge
    /// NEVER slides or peeks the Windows taskbar back up), while reclaiming full
    /// monitor work area for maximized applications. Automatically restored on exit.
    /// </summary>
    public void HideNativeWindowsTaskbar()
    {
        try
        {
            var abd = new NativeMethods.APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>()
            };

            nint primaryTray = FindWindowW("Shell_TrayWnd", null);
            // Always restore the native Windows taskbar visibly (not Auto-Hide) when Aurora closes
            _originalNativeTaskbarState ??= 0;

            // Snapshot original non-full monitor work areas before expanding them
            CaptureOriginalMonitorWorkAreasIfEmpty();

            // Release the OS 48px AppBar WorkArea reservation cleanly across all monitors via ABM_SETSTATE,
            // then immediately hide & disable Shell_TrayWnd + all Shell_SecondaryTrayWnd so Windows Auto-Hide
            // NEVER peeks or slides up from the bottom edge!
            if (primaryTray != 0)
            {
                EnableWindow(primaryTray, true);
                abd.hWnd = primaryTray;
                abd.lParam = NativeMethods.ABS_AUTOHIDE;
                NativeMethods.SHAppBarMessage(ABM_SETSTATE, ref abd);
            }

            // Completely hide and disable primary + all secondary monitor taskbars (Explorer stays intact!)
            SuppressAllTrayWindows();

            _nativeTaskbarSuppressed = true;
            logger.Info("AppBar", "NATIVE_TB_LOCKED_OFF", "Completely hidden & disabled native Windows taskbar across all monitors with full work area and zero peek.");
        }
        catch (Exception ex)
        {
            logger.Warn("AppBar", "NATIVE_TB_HIDE_WARN", $"Could not hide native taskbar ({ex.GetType().Name}).");
        }
    }

    /// <summary>
    /// Real-time lockdown guard invoked on Win32 EVENT_OBJECT_SHOW and periodic heartbeat.
    /// Prevents Windows 11 (hover, alerts, Win key, display wake) from sliding the native taskbar back up.
    /// </summary>
    public void EnforceNativeTaskbarSuppressed(nint? changedHwnd = null)
    {
        if (!_nativeTaskbarSuppressed || _disposed)
            return;

        try
        {
            if (changedHwnd is { } hwnd && hwnd != 0)
            {
                if (IsNativeTaskbarWindow(hwnd))
                {
                    HideAndDisableTrayWindow(hwnd);
                }
                return;
            }

            foreach (var trayHwnd in EnumerateAllTrayWindows())
            {
                if (NativeMethods.IsWindowVisible(trayHwnd) || IsWindowEnabled(trayHwnd))
                {
                    HideAndDisableTrayWindow(trayHwnd);
                }
            }
        }
        catch { }
    }

    public void RestoreNativeWindowsTaskbar(int? explicitState = null)
    {
        try
        {
            // Disarm real-time lockdown BEFORE restoring visibility!
            _nativeTaskbarSuppressed = false;
            int stateToRestore = explicitState ?? _originalNativeTaskbarState ?? 0;

            var trayWindows = EnumerateAllTrayWindows();
            foreach (var hwnd in trayWindows)
            {
                EnableWindow(hwnd, true);
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOW);
                NativeMethods.SetWindowPos(
                    hwnd,
                    0,
                    0, 0, 0, 0,
                    SWP_SHOWWINDOW | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
            }

            nint primaryTray = FindWindowW("Shell_TrayWnd", null);
            var abd = new NativeMethods.APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(),
                hWnd = primaryTray,
                lParam = stateToRestore
            };
            NativeMethods.SHAppBarMessage(ABM_SETSTATE, ref abd);

            // Non-blocking WorkArea refresh (fWinIni = 0 avoids 5-second synchronous HWND_BROADCAST stalls)
            if ((stateToRestore & NativeMethods.ABS_AUTOHIDE) == 0)
            {
                RestoreAllMonitorWorkAreas(trayWindows);
            }

            SendNotifyMessageW(HWND_BROADCAST, WM_SETTINGCHANGE, 0, 0);
            logger.Info("AppBar", "NATIVE_TB_RESTORED", "Restored native Windows taskbar visibility, input, and work area across all monitors.");
        }
        catch (Exception ex)
        {
            logger.Warn("AppBar", "NATIVE_TB_RESTORE_WARN", $"Restore native taskbar warning ({ex.GetType().Name}).");
        }
    }

    public static PhysicalPixelRect ComputeRestoredWorkArea(PhysicalPixelRect monitorBounds, PhysicalPixelRect trayRect)
    {
        if (trayRect.Width <= 0 || trayRect.Height <= 0)
            return monitorBounds;

        int left = monitorBounds.X;
        int top = monitorBounds.Y;
        int right = monitorBounds.Right;
        int bottom = monitorBounds.Bottom;

        bool horizontal = trayRect.Width >= trayRect.Height;
        if (horizontal)
        {
            int distBottom = Math.Abs(monitorBounds.Bottom - trayRect.Bottom);
            int distTop = Math.Abs(trayRect.Y - monitorBounds.Y);
            if (distBottom <= distTop)
                bottom = Math.Min(bottom, trayRect.Y);
            else
                top = Math.Max(top, trayRect.Bottom);
        }
        else
        {
            int distLeft = Math.Abs(trayRect.X - monitorBounds.X);
            int distRight = Math.Abs(monitorBounds.Right - trayRect.Right);
            if (distLeft <= distRight)
                left = Math.Max(left, trayRect.Right);
            else
                right = Math.Min(right, trayRect.X);
        }

        if (right <= left || bottom <= top)
            return monitorBounds;

        return new PhysicalPixelRect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Computes the exact per-monitor Windows WorkArea rectangle that leaves a clean breathing channel
    /// for Aurora's Bottom / Left / Right Dock so maximized apps (Antigravity, WhatsApp, Discord, VS Code)
    /// NEVER have their typing boxes or sidebars covered by the Dock.
    /// </summary>
    public static PhysicalPixelRect ComputeDockReservedWorkArea(
        PhysicalPixelRect monitorBounds,
        double scaleFactor,
        string dockEdge,
        int dockScalePercent,
        bool reserve,
        string dockDensity = "Standard")
    {
        if (!reserve || monitorBounds.Width <= 0 || monitorBounds.Height <= 0)
            return monitorBounds;

        double dpi = Math.Max(1.0, scaleFactor);
        double dockMul = Math.Clamp(dockScalePercent <= 0 ? 100 : dockScalePercent, 75, 135) / 100.0;
        bool compact = string.Equals(dockDensity, "Compact", StringComparison.OrdinalIgnoreCase);
        bool cozy = string.Equals(dockDensity, "Cozy", StringComparison.OrdinalIgnoreCase);

        int left = monitorBounds.X;
        int top = monitorBounds.Y;
        int right = monitorBounds.Right;
        int bottom = monitorBounds.Bottom;

        if (string.Equals(dockEdge, "Left", StringComparison.OrdinalIgnoreCase))
        {
            double baseSide = compact ? 48.0 : (cozy ? 66.0 : 56.0);
            int inset = (int)Math.Round(Math.Clamp(baseSide * dockMul + 14.0, 52.0, 122.0) * dpi);
            left = Math.Min(right - 320, left + inset);
        }
        else if (string.Equals(dockEdge, "Right", StringComparison.OrdinalIgnoreCase))
        {
            double baseSide = compact ? 48.0 : (cozy ? 66.0 : 56.0);
            int inset = (int)Math.Round(Math.Clamp(baseSide * dockMul + 14.0, 52.0, 122.0) * dpi);
            right = Math.Max(left + 320, right - inset);
        }
        else
        {
            double baseBottom = compact ? 50.0 : (cozy ? 68.0 : 58.0);
            int inset = (int)Math.Round(Math.Clamp(baseBottom * dockMul + 14.0, 54.0, 126.0) * dpi);
            bottom = Math.Max(top + 240, bottom - inset);
        }

        return new PhysicalPixelRect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Applies (or releases) Aurora's Dock WorkArea reservation across all monitors via SPI_SETWORKAREA
    /// (fWinIni = 0, NEVER broadcasting WM_SETTINGCHANGE(SPI_SETWORKAREA) to HWND_BROADCAST because
    /// Shell_TrayWnd responds to that broadcast in ABS_AUTOHIDE mode by immediately wiping rcWork back
    /// to 100%!), and directly notifies & refits already-maximized user windows cleanly above/beside the Dock.
    /// </summary>
    public void ApplyDockWorkAreaReservation(string dockEdge, int dockScalePercent, bool reserve, string dockDensity = "Standard")
    {
        if (!_nativeTaskbarSuppressed || _disposed)
            return;

        try
        {
            CaptureOriginalMonitorWorkAreasIfEmpty();
            bool anyChanged = false;

            NativeMethods.EnumDisplayMonitors(0, 0, (nint hMon, nint _, ref NativeMethods.RECT _, nint _) =>
            {
                var info = new NativeMethods.MONITORINFOEXW { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEXW>() };
                if (!NativeMethods.GetMonitorInfoW(hMon, ref info))
                    return true;

                uint dpiX = 96;
                if (NativeMethods.GetDpiForMonitor(hMon, NativeMethods.MDT_EFFECTIVE_DPI, out var dx, out _) == 0 && dx > 0)
                    dpiX = dx;

                var monPx = new PhysicalPixelRect(info.rcMonitor.Left, info.rcMonitor.Top, info.rcMonitor.Width, info.rcMonitor.Height);
                var desired = ComputeDockReservedWorkArea(monPx, dpiX / 96.0, dockEdge, dockScalePercent, reserve, dockDensity);

                if (info.rcWork.Left != desired.X || info.rcWork.Top != desired.Y ||
                    info.rcWork.Right != desired.Right || info.rcWork.Bottom != desired.Bottom)
                {
                    var rc = new NativeMethods.RECT
                    {
                        Left = desired.X,
                        Top = desired.Y,
                        Right = desired.Right,
                        Bottom = desired.Bottom
                    };
                    SystemParametersInfoW(SPI_SETWORKAREA, 0, ref rc, 0);
                    anyChanged = true;
                }
                return true;
            }, 0);

            if (anyChanged)
            {
                RefitAlreadyMaximizedWindowsToWorkArea();
            }
        }
        catch (Exception ex)
        {
            logger.Warn("AppBar", "DOCK_WORKAREA_WARN", $"Dock work-area reservation warning ({ex.GetType().Name}).");
        }
    }

    private static void RefitAlreadyMaximizedWindowsToWorkArea()
    {
        try
        {
            uint ownPid = (uint)Environment.ProcessId;
            int framePad = Math.Clamp(GetSystemMetrics(32 /* SM_CXSIZEFRAME */) + GetSystemMetrics(92 /* SM_CXPADDEDBORDER */), 6, 12);

            NativeMethods.EnumWindows((hwnd, _) =>
            {
                if (hwnd == 0 || !NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsIconic(hwnd) || !IsZoomed(hwnd))
                    return true;

                NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0 || pid == ownPid)
                    return true;

                if (IsNativeTaskbarWindow(hwnd))
                    return true;

                long ex = NativeMethods.GetWindowLongPtr64(hwnd, NativeMethods.GWL_EXSTYLE);
                if ((ex & NativeMethods.WS_EX_TOOLWINDOW) != 0)
                    return true;

                if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                    return true;

                nint hMon = NativeMethods.MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                var info = new NativeMethods.MONITORINFOEXW { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEXW>() };
                if (hMon == 0 || !NativeMethods.GetMonitorInfoW(hMon, ref info))
                    return true;

                var wa = info.rcWork;
                if (!GetWindowRect(hwnd, out var cur))
                    return true;

                // Notify ONLY this user application window (never Shell_TrayWnd) so Chromium/Electron updates WM_GETMINMAXINFO
                SendNotifyMessageW(hwnd, WM_SETTINGCHANGE, SPI_SETWORKAREA, 0);

                bool needsRefit =
                    Math.Abs(cur.Bottom - (wa.Bottom + framePad)) > 6 ||
                    Math.Abs(cur.Left - (wa.Left - framePad)) > 6 ||
                    Math.Abs(cur.Right - (wa.Right + framePad)) > 6;

                if (needsRefit)
                {
                    NativeMethods.SetWindowPos(
                        hwnd,
                        0,
                        wa.Left - framePad,
                        wa.Top - framePad,
                        (wa.Right - wa.Left) + framePad * 2,
                        (wa.Bottom - wa.Top) + framePad * 2,
                        SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
                }

                return true;
            }, 0);
        }
        catch { }
    }

    private static List<nint> EnumerateAllTrayWindows()
    {
        var list = new List<nint>(4);
        nint primary = FindWindowW("Shell_TrayWnd", null);
        if (primary != 0)
            list.Add(primary);

        nint sec = 0;
        while ((sec = FindWindowExW(0, sec, "Shell_SecondaryTrayWnd", null)) != 0)
            list.Add(sec);

        return list;
    }

    private static bool IsNativeTaskbarWindow(nint hwnd)
    {
        var buf = new char[64];
        int len = GetClassNameW(hwnd, buf, buf.Length);
        if (len <= 0) return false;
        var span = buf.AsSpan(0, len);
        return span.Equals("Shell_TrayWnd", StringComparison.Ordinal) ||
               span.Equals("Shell_SecondaryTrayWnd", StringComparison.Ordinal);
    }

    private static void HideAndDisableTrayWindow(nint hwnd)
    {
        if (hwnd == 0) return;
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
        EnableWindow(hwnd, false);
        NativeMethods.SetWindowPos(
            hwnd,
            0,
            0, 0, 0, 0,
            SWP_HIDEWINDOW | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    private static void SuppressAllTrayWindows()
    {
        foreach (var hwnd in EnumerateAllTrayWindows())
            HideAndDisableTrayWindow(hwnd);
    }

    private void CaptureOriginalMonitorWorkAreasIfEmpty()
    {
        if (_savedMonitorWorkAreas.Count > 0)
            return;

        NativeMethods.EnumDisplayMonitors(0, 0, (nint hMon, nint _, ref NativeMethods.RECT _, nint _) =>
        {
            var info = new NativeMethods.MONITORINFOEXW { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEXW>() };
            if (NativeMethods.GetMonitorInfoW(hMon, ref info))
            {
                bool hasReservation =
                    info.rcWork.Left != info.rcMonitor.Left ||
                    info.rcWork.Top != info.rcMonitor.Top ||
                    info.rcWork.Right != info.rcMonitor.Right ||
                    info.rcWork.Bottom != info.rcMonitor.Bottom;

                if (hasReservation && !string.IsNullOrWhiteSpace(info.szDevice))
                {
                    _savedMonitorWorkAreas[info.szDevice] = info.rcWork;
                }
            }
            return true;
        }, 0);
    }

    private void RestoreAllMonitorWorkAreas(IReadOnlyList<nint> trayWindows)
    {
        if (_savedMonitorWorkAreas.Count > 0)
        {
            foreach (var saved in _savedMonitorWorkAreas.Values)
            {
                var rc = saved;
                SystemParametersInfoW(SPI_SETWORKAREA, 0, ref rc, 0);
            }
            _savedMonitorWorkAreas.Clear();
            return;
        }

        // Detached watchdog / --restore-taskbar path: compute exact WorkArea directly from each monitor's tray rect!
        foreach (var trayHwnd in trayWindows)
        {
            if (!GetWindowRect(trayHwnd, out var trayRc))
                continue;

            nint hMon = NativeMethods.MonitorFromWindow(trayHwnd, MONITOR_DEFAULTTONEAREST);
            var info = new NativeMethods.MONITORINFOEXW { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEXW>() };
            if (hMon == 0 || !NativeMethods.GetMonitorInfoW(hMon, ref info))
                continue;

            var monPx = new PhysicalPixelRect(info.rcMonitor.Left, info.rcMonitor.Top, info.rcMonitor.Width, info.rcMonitor.Height);
            var trayPx = new PhysicalPixelRect(trayRc.Left, trayRc.Top, trayRc.Width, trayRc.Height);
            var restored = ComputeRestoredWorkArea(monPx, trayPx);

            var targetRc = new NativeMethods.RECT
            {
                Left = restored.X,
                Top = restored.Y,
                Right = restored.Right,
                Bottom = restored.Bottom
            };
            SystemParametersInfoW(SPI_SETWORKAREA, 0, ref targetRc, 0);
        }
    }

    public NativeTaskbarStatus QueryNativeTaskbarStatus()
    {
        try
        {
            var stateData = new NativeMethods.APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>()
            };
            var stateBits = (int)NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETSTATE, ref stateData);
            bool isAutoHide = (stateBits & NativeMethods.ABS_AUTOHIDE) != 0;

            var posData = new NativeMethods.APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>()
            };
            var ok = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETTASKBARPOS, ref posData) != 0;

            var edge = ok && Enum.IsDefined(typeof(TaskbarEdge), (int)posData.uEdge)
                ? (TaskbarEdge)posData.uEdge
                : TaskbarEdge.Bottom;

            var rect = ok
                ? new PhysicalPixelRect(posData.rc.Left, posData.rc.Top, posData.rc.Width, posData.rc.Height)
                : new PhysicalPixelRect(0, 0, 0, 0);

            var visibility = isAutoHide
                ? NativeTaskbarVisibilityState.AutoHideEnabled
                : NativeTaskbarVisibilityState.AlwaysVisible;

            bool coexistence = visibility == NativeTaskbarVisibilityState.AlwaysVisible;
            var msg = coexistence
                ? "Birlikte Çalışma Modu Aktif: Windows görev çubuğu görünür durumda; Aurora çakışmadan üst çalışma alanına yerleşti."
                : "Birincil Görev Çubuğu Modu: Windows görev çubuğu kullanıcı tarafından otomatik gizlemeye ayarlanmış.";

            return new NativeTaskbarStatus(visibility, edge, rect, coexistence, msg);
        }
        catch (Exception ex)
        {
            logger.Warn("AppBar", "NATIVE_TB_QUERY_WARN", $"Could not query native taskbar state ({ex.GetType().Name}).");
            return new NativeTaskbarStatus(
                NativeTaskbarVisibilityState.Unknown,
                TaskbarEdge.Bottom,
                new PhysicalPixelRect(0, 0, 0, 0),
                true,
                "Birlikte Çalışma Modu (Güvenli Varsayılan)");
        }
    }

    public AuroraResult<PhysicalPixelRect> RegisterOrUpdateBar(
        nint hwnd,
        TaskbarEdge edge,
        int thicknessPx,
        MonitorDescriptor monitor,
        bool reserveWorkArea)
    {
        if (hwnd == 0)
            return AuroraResult<PhysicalPixelRect>.Fail("APPBAR_INVALID_HWND", "Geçersiz pencere tutamacı.");

        try
        {
            if (!reserveWorkArea)
            {
                if (_registeredBars.ContainsKey(hwnd))
                    UnregisterBar(hwnd);

                var area = monitor.WorkAreaPx;
                var rect = edge switch
                {
                    TaskbarEdge.Top => new PhysicalPixelRect(area.X, area.Y, area.Width, thicknessPx),
                    TaskbarEdge.Left => new PhysicalPixelRect(area.X, area.Y, thicknessPx, area.Height),
                    TaskbarEdge.Right => new PhysicalPixelRect(area.Right - thicknessPx, area.Y, thicknessPx, area.Height),
                    _ => new PhysicalPixelRect(area.X, area.Bottom - thicknessPx, area.Width, thicknessPx)
                };
                return AuroraResult<PhysicalPixelRect>.Ok(rect);
            }

            if (!_registeredBars.TryGetValue(hwnd, out var callbackMsg))
            {
                callbackMsg = NativeMethods.RegisterWindowMessageW($"ProjectAurora_AppBar_{hwnd}");
                var newData = new NativeMethods.APPBARDATA
                {
                    cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(),
                    hWnd = hwnd,
                    uCallbackMessage = callbackMsg
                };

                var regRes = NativeMethods.SHAppBarMessage(NativeMethods.ABM_NEW, ref newData);
                if (regRes == 0)
                {
                    return AuroraResult<PhysicalPixelRect>.Fail("APPBAR_REGISTER_FAILED", "SHAppBarMessage(ABM_NEW) kaydı reddedildi.");
                }

                _registeredBars[hwnd] = callbackMsg;
                logger.Info("AppBar", "APPBAR_REGISTERED", $"Registered AppBar edge={edge} thicknessPx={thicknessPx}.");
            }

            var b = monitor.BoundsPx;
            var abd = new NativeMethods.APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(),
                hWnd = hwnd,
                uEdge = (uint)edge,
                rc = new NativeMethods.RECT
                {
                    Left = b.X,
                    Top = b.Y,
                    Right = b.Right,
                    Bottom = b.Bottom
                }
            };

            NativeMethods.SHAppBarMessage(NativeMethods.ABM_QUERYPOS, ref abd);

            switch (edge)
            {
                case TaskbarEdge.Top:
                    abd.rc.Bottom = abd.rc.Top + thicknessPx;
                    break;
                case TaskbarEdge.Left:
                    abd.rc.Right = abd.rc.Left + thicknessPx;
                    break;
                case TaskbarEdge.Right:
                    abd.rc.Left = abd.rc.Right - thicknessPx;
                    break;
                default:
                    abd.rc.Top = abd.rc.Bottom - thicknessPx;
                    break;
            }

            NativeMethods.SHAppBarMessage(NativeMethods.ABM_SETPOS, ref abd);
            var granted = new PhysicalPixelRect(abd.rc.Left, abd.rc.Top, abd.rc.Width, abd.rc.Height);
            return AuroraResult<PhysicalPixelRect>.Ok(granted);
        }
        catch (Exception ex)
        {
            logger.Error("AppBar", "APPBAR_UPDATE_ERR", "AppBar edge negotiation failed; entering safe fallback.", ex);
            return AuroraResult<PhysicalPixelRect>.Fail("APPBAR_EXCEPTION", "Görev çubuğu kenar kaydı sırasında hata oluştu.");
        }
    }

    public AuroraResult UnregisterBar(nint hwnd)
    {
        if (hwnd == 0 || !_registeredBars.TryRemove(hwnd, out _))
            return AuroraResult.Ok();

        try
        {
            var abd = new NativeMethods.APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(),
                hWnd = hwnd
            };
            NativeMethods.SHAppBarMessage(NativeMethods.ABM_REMOVE, ref abd);
            logger.Info("AppBar", "APPBAR_REMOVED", "Unregistered AppBar reservation cleanly.");
            return AuroraResult.Ok();
        }
        catch (Exception ex)
        {
            logger.Error("AppBar", "APPBAR_REMOVE_ERR", "Failed to remove AppBar cleanly.", ex);
            return AuroraResult.Fail("APPBAR_REMOVE_ERR", "AppBar kaydı kaldırılırken hata oluştu.");
        }
    }

    public void UnregisterAll()
    {
        foreach (var hwnd in _registeredBars.Keys.ToArray())
        {
            UnregisterBar(hwnd);
        }

        if (_nativeTaskbarSuppressed)
        {
            RestoreNativeWindowsTaskbar();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        UnregisterAll();
    }
}

public sealed class Win32WindowCatalog : IWindowCatalog
{
    private readonly uint _currentPid = (uint)Environment.ProcessId;

    public nint GetForegroundWindowHandle() => NativeMethods.GetForegroundWindow();

    public byte[]? ResolveShortcutIconPng(string targetPath) =>
        Win32IconExtractor.ResolveShortcutIconPng(targetPath);

    public AuroraResult<IReadOnlyList<AppWindowEntry>> GetOpenWindows(IReadOnlySet<nint>? excludedHwnds = null)
    {
        var results = new List<AppWindowEntry>();
        var fg = NativeMethods.GetForegroundWindow();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (hWnd == 0 || (excludedHwnds is not null && excludedHwnds.Contains(hWnd)))
                return true;

            if (!NativeMethods.IsWindowVisible(hWnd))
                return true;

            if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;

            long exStyle = NativeMethods.GetWindowLongPtr64(hWnd, NativeMethods.GWL_EXSTYLE);
            bool isToolWindow = (exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0;
            bool isAppWindow = (exStyle & NativeMethods.WS_EX_APPWINDOW) != 0;
            nint owner = NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER);

            if ((isToolWindow || owner != 0) && !isAppWindow)
                return true;

            int len = NativeMethods.GetWindowTextLengthW(hWnd);
            if (len <= 0)
                return true;

            NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0 || pid == _currentPid)
                return true;

            var buf = new char[Math.Min(len + 2, 260)];
            int copied = NativeMethods.GetWindowTextW(hWnd, buf, buf.Length);
            var title = copied > 0 ? new string(buf, 0, copied).Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(title) || title is "Program Manager" or "Windows Input Experience")
                return true;

            string procName = "app";
            string exePath = string.Empty;
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                procName = proc.ProcessName;
                try { exePath = proc.MainModule?.FileName ?? procName; } catch { exePath = procName; }
            }
            catch
            {
                // Protected or elevated system process
            }

            nint hMon = NativeMethods.MonitorFromWindow(hWnd, 2 /* MONITOR_DEFAULTTONEAREST */);
            string monId = "PRIMARY";
            if (hMon != 0)
            {
                var mi = new NativeMethods.MONITORINFOEXW { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEXW>() };
                if (NativeMethods.GetMonitorInfoW(hMon, ref mi) && !string.IsNullOrWhiteSpace(mi.szDevice))
                    monId = mi.szDevice.Trim();
            }

            bool isTopMost = (exStyle & 0x00000008L /* WS_EX_TOPMOST */) != 0;
            var iconBytes = Win32IconExtractor.GetWindowOrProcessIconPng(hWnd, exePath, procName);

            results.Add(new AppWindowEntry(
                Hwnd: hWnd,
                ProcessId: pid,
                ProcessName: procName,
                ExecutablePath: exePath,
                Title: title,
                IsActive: hWnd == fg,
                IsMinimized: NativeMethods.IsIconic(hWnd),
                IsResponding: !NativeMethods.IsHungAppWindow(hWnd),
                MonitorDeviceId: monId,
                IconPngBytes: iconBytes,
                IsTopMost: isTopMost));

            return true;
        }, 0);

        return AuroraResult<IReadOnlyList<AppWindowEntry>>.Ok(results);
    }
}

public sealed class Win32WindowActivationService(IAuroraLogger logger) : IWindowActivationService
{
    public AuroraResult ActivateOrToggleWindow(nint hwnd)
    {
        if (hwnd == 0)
            return AuroraResult.Fail("WIN_INVALID_HWND", "Pencere bulunamadı.");

        try
        {
            if (NativeMethods.IsIconic(hwnd))
            {
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
                NativeMethods.SetForegroundWindow(hwnd);
                return AuroraResult.Ok();
            }

            if (NativeMethods.GetForegroundWindow() == hwnd)
            {
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MINIMIZE);
                return AuroraResult.Ok();
            }

            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOW);
            NativeMethods.BringWindowToTop(hwnd);
            NativeMethods.SetForegroundWindow(hwnd);
            return AuroraResult.Ok();
        }
        catch (Exception ex)
        {
            logger.Warn("WindowActivation", "WIN_ACTIVATE_WARN", $"Window activation failed ({ex.GetType().Name}).");
            return AuroraResult.Fail("WIN_ACTIVATE_FAILED", "Pencere öne getirilemedi (izin sınırı olabilir).");
        }
    }

    public AuroraResult FocusWindow(nint hwnd)
    {
        if (hwnd == 0)
            return AuroraResult.Fail("WIN_INVALID_HWND", "Pencere bulunamadı.");

        try
        {
            if (NativeMethods.IsIconic(hwnd))
            {
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
            }
            else
            {
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOW);
            }

            NativeMethods.BringWindowToTop(hwnd);
            NativeMethods.SetForegroundWindow(hwnd);
            return AuroraResult.Ok();
        }
        catch (Exception ex)
        {
            logger.Warn("WindowActivation", "WIN_FOCUS_WARN", $"Window focus failed ({ex.GetType().Name}).");
            return AuroraResult.Fail("WIN_FOCUS_FAILED", "Pencere öne getirilemedi.");
        }
    }

    public AuroraResult MinimizeWindow(nint hwnd)
    {
        if (hwnd == 0)
            return AuroraResult.Fail("WIN_INVALID_HWND", "Geçersiz pencere.");
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MINIMIZE);
        return AuroraResult.Ok();
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PostMessageW(nint hWnd, uint msg, nint wParam, nint lParam);

    public AuroraResult CloseWindow(nint hwnd)
    {
        if (hwnd == 0)
            return AuroraResult.Fail("WIN_INVALID_HWND", "Geçersiz pencere.");
        PostMessageW(hwnd, 0x0010 /* WM_CLOSE */, 0, 0);
        return AuroraResult.Ok();
    }

    public AuroraResult RestoreWindow(nint hwnd)
    {
        if (hwnd == 0)
            return AuroraResult.Fail("WIN_INVALID_HWND", "Geçersiz pencere.");
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        NativeMethods.SetForegroundWindow(hwnd);
        return AuroraResult.Ok();
    }

    public AuroraResult ArrangeWindow(nint hwnd, string mode)
    {
        if (hwnd == 0)
            return AuroraResult.Fail("WIN_INVALID_HWND", "Geçersiz pencere.");

        try
        {
            nint hMon = NativeMethods.MonitorFromWindow(hwnd, 2);
            var mi = new NativeMethods.MONITORINFOEXW { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEXW>() };
            if (!NativeMethods.GetMonitorInfoW(hMon, ref mi))
                return AuroraResult.Fail("WIN_MON_ERR", "Ekran çalışma alanı okunamadı.");

            var w = mi.rcWork;
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);

            switch (mode.ToLowerInvariant())
            {
                case "left":
                    NativeMethods.SetWindowPos(hwnd, 0, w.Left, w.Top, w.Width / 2, w.Height, 0);
                    break;
                case "right":
                    NativeMethods.SetWindowPos(hwnd, 0, w.Left + (w.Width / 2), w.Top, w.Width / 2, w.Height, 0);
                    break;
                case "center":
                    int cw = (int)(w.Width * 0.74);
                    int ch = (int)(w.Height * 0.80);
                    NativeMethods.SetWindowPos(hwnd, 0, w.Left + ((w.Width - cw) / 2), w.Top + ((w.Height - ch) / 2), cw, ch, 0);
                    break;
                case "maximize":
                    NativeMethods.ShowWindow(hwnd, 3 /* SW_MAXIMIZE */);
                    break;
            }

            NativeMethods.SetForegroundWindow(hwnd);
            return AuroraResult.Ok();
        }
        catch (Exception ex)
        {
            logger.Warn("WindowActivation", "WIN_ARRANGE_WARN", $"ArrangeWindow failed ({ex.GetType().Name}).");
            return AuroraResult.Fail("WIN_ARRANGE_ERR", "Pencere hizalanamadı.");
        }
    }

    public AuroraResult ToggleAlwaysOnTop(nint hwnd)
    {
        if (hwnd == 0)
            return AuroraResult.Fail("WIN_INVALID_HWND", "Geçersiz pencere.");

        long exStyle = NativeMethods.GetWindowLongPtr64(hwnd, NativeMethods.GWL_EXSTYLE);
        bool currentlyTop = (exStyle & 0x00000008L) != 0;
        nint insertAfter = currentlyTop ? NativeMethods.HWND_NOTOPMOST : NativeMethods.HWND_TOPMOST;
        NativeMethods.SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE);
        NativeMethods.SetForegroundWindow(hwnd);
        return AuroraResult.Ok();
    }

    public AuroraResult LaunchShortcut(PinnedShortcut shortcut)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(shortcut.TargetPath) &&
                (shortcut.TargetPath.StartsWith("appsfolder:", StringComparison.OrdinalIgnoreCase) ||
                 shortcut.TargetPath.Contains('|')))
            {
                if (Aurora.Platform.Windows.Apps.InstalledAppCatalogService.LaunchTarget(shortcut.TargetPath, out var err))
                {
                    logger.Info("WindowActivation", "SHORTCUT_LAUNCHED", $"Launched shell shortcut '{shortcut.Id}'.");
                    return AuroraResult.Ok();
                }
                return AuroraResult.Fail("SHORTCUT_LAUNCH_FAILED", err ?? $"'{shortcut.DisplayName}' başlatılamadı.");
            }

            var psi = new ProcessStartInfo
            {
                FileName = shortcut.TargetPath,
                Arguments = shortcut.Arguments ?? string.Empty,
                UseShellExecute = true
            };
            Process.Start(psi);
            logger.Info("WindowActivation", "SHORTCUT_LAUNCHED", $"Launched shortcut '{shortcut.Id}'.");
            return AuroraResult.Ok();
        }
        catch (Exception ex)
        {
            logger.Warn("WindowActivation", "SHORTCUT_LAUNCH_ERR", $"Shortcut launch failed for '{shortcut.Id}' ({ex.GetType().Name}).");
            return AuroraResult.Fail("SHORTCUT_LAUNCH_FAILED", $"'{shortcut.DisplayName}' başlatılamadı.");
        }
    }
}
