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
    private const uint ABE_LEFT = 0;
    private const uint ABE_RIGHT = 2;
    private const uint ABE_BOTTOM = 3;
    private const uint SPI_SETWORKAREA = 0x002F;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_HIDEWINDOW = 0x0080;
    private const uint SWP_NOSENDCHANGING = 0x0400;
    private const uint SWP_ASYNCWINDOWPOS = 0x4000;
    private const int SW_SHOWNOACTIVATE = 4;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint WS_POPUP = 0x80000000u;
    private const uint WS_EX_TRANSPARENT = 0x00000020u;
    private const uint WS_EX_TOOLWINDOW_U = 0x00000080u;
    private const uint WS_EX_LAYERED = 0x00080000u;
    private const uint WS_EX_NOACTIVATE_U = 0x08000000u;
    private const uint LWA_ALPHA = 0x00000002u;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private static readonly nint HWND_BROADCAST = new(0xFFFF);
    private static readonly nint HWND_BOTTOM = new(1);

    private sealed record DockAppBarSlot(nint Hwnd, uint CallbackMsg, string MonitorDeviceId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        nint hWndParent,
        nint hMenu,
        nint hInstance,
        nint lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(nint hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int dwAttribute, out NativeMethods.RECT pvAttribute, int cbAttribute);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindowW(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindowExW(nint hWndParent, nint hWndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(nint hWnd, [MarshalAs(UnmanagedType.Bool)] bool bEnable);

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
    private readonly Dictionary<string, DockAppBarSlot> _dockAppBarSlots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NativeMethods.RECT> _savedMonitorWorkAreas = new(StringComparer.OrdinalIgnoreCase);
    private string? _lastDockReservationSignature;
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
    /// Completely hides the native Windows taskbar across ALL monitors while releasing its
    /// default reservation via ABM_SETSTATE(ABS_AUTOHIDE) so Aurora's own native AppBar slots
    /// (in Pinned Fixed Bar mode) or full 100% WorkArea (in Intellihide / AutoHide mode) govern
    /// window sizing cleanly without fighting Explorer. Automatically restored on exit.
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
            // then hide Shell_TrayWnd + all Shell_SecondaryTrayWnd (keeping Shell_TrayWnd enabled so
            // Explorer's AppBar manager cleanly handles Aurora's SHAppBarMessage ABM_NEW / ABM_SETPOS / ABM_REMOVE!).
            if (primaryTray != 0)
            {
                EnableWindow(primaryTray, true);
                abd.hWnd = primaryTray;
                abd.lParam = NativeMethods.ABS_AUTOHIDE;
                NativeMethods.SHAppBarMessage(ABM_SETSTATE, ref abd);
            }

            SuppressAllTrayWindows();

            _nativeTaskbarSuppressed = true;
            _lastDockReservationSignature = null;
            logger.Info("AppBar", "NATIVE_TB_LOCKED_OFF", "Hidden native Windows taskbar across all monitors with full work area and zero peek.");
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
                if (IsNativeTaskbarWindow(hwnd) && NativeMethods.IsWindowVisible(hwnd))
                {
                    HideTrayWindow(hwnd);
                }
                return;
            }

            foreach (var trayHwnd in EnumerateAllTrayWindows())
            {
                if (NativeMethods.IsWindowVisible(trayHwnd))
                {
                    HideTrayWindow(trayHwnd);
                }
            }
        }
        catch { }
    }

    public void RestoreNativeWindowsTaskbar(int? explicitState = null)
    {
        try
        {
            // Disarm real-time lockdown and remove Aurora's AppBar slots BEFORE restoring native taskbar!
            _nativeTaskbarSuppressed = false;
            _lastDockReservationSignature = null;
            ReleaseAllDockAppBarSlots();

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
    /// Computes the exact per-monitor Windows WorkArea rectangle for Aurora's Pinned Fixed Bar
    /// (Bottom 44/52/60px, Left/Right 50/58/66px at 100% scale) so maximized apps (Antigravity,
    /// WhatsApp, Discord, VS Code) sit pixel-flush right above/beside the edge-to-edge Fixed Bar
    /// with ZERO wallpaper bleed and ZERO covered typing boxes.
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
            double baseSide = compact ? 50.0 : (cozy ? 66.0 : 58.0);
            int inset = (int)Math.Round(Math.Clamp(baseSide * dockMul, 42.0, 92.0) * dpi);
            left = Math.Min(right - 320, left + inset);
        }
        else if (string.Equals(dockEdge, "Right", StringComparison.OrdinalIgnoreCase))
        {
            double baseSide = compact ? 50.0 : (cozy ? 66.0 : 58.0);
            int inset = (int)Math.Round(Math.Clamp(baseSide * dockMul, 42.0, 92.0) * dpi);
            right = Math.Max(left + 320, right - inset);
        }
        else
        {
            double baseBottom = compact ? 44.0 : (cozy ? 60.0 : 52.0);
            int inset = (int)Math.Round(Math.Clamp(baseBottom * dockMul, 38.0, 84.0) * dpi);
            bottom = Math.Max(top + 240, bottom - inset);
        }

        return new PhysicalPixelRect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Computes the exact edge strip rectangle to reserve via SHAppBarMessage(ABM_QUERYPOS / ABM_SETPOS)
    /// for Aurora's Pinned Fixed Bar on a given monitor.
    /// </summary>
    public static PhysicalPixelRect ComputeAppBarReservationRect(
        PhysicalPixelRect monitorBounds,
        double scaleFactor,
        string dockEdge,
        int dockScalePercent,
        string dockDensity = "Standard")
    {
        var workArea = ComputeDockReservedWorkArea(monitorBounds, scaleFactor, dockEdge, dockScalePercent, reserve: true, dockDensity);

        if (string.Equals(dockEdge, "Left", StringComparison.OrdinalIgnoreCase))
        {
            int w = Math.Max(1, workArea.X - monitorBounds.X);
            return new PhysicalPixelRect(monitorBounds.X, monitorBounds.Y, w, monitorBounds.Height);
        }

        if (string.Equals(dockEdge, "Right", StringComparison.OrdinalIgnoreCase))
        {
            int w = Math.Max(1, monitorBounds.Right - workArea.Right);
            return new PhysicalPixelRect(workArea.Right, monitorBounds.Y, w, monitorBounds.Height);
        }

        int h = Math.Max(1, monitorBounds.Bottom - workArea.Bottom);
        return new PhysicalPixelRect(monitorBounds.X, workArea.Bottom, monitorBounds.Width, h);
    }

    /// <summary>
    /// Applies (or releases) Aurora's Dock WorkArea reservation across all monitors using genuine
    /// Win32 AppBar registrations (SHAppBarMessage ABM_NEW / ABM_QUERYPOS / ABM_SETPOS / ABM_REMOVE)
    /// hosted on invisible, click-through per-monitor helper windows.
    /// Because Windows Explorer itself owns these AppBar entries, Explorer NEVER fights or resets
    /// rcWork back and forth — eliminating periodic window resize oscillation completely.
    /// Returns true when a real state transition occurred.
    /// </summary>
    public bool ApplyDockWorkAreaReservation(
        string dockEdge,
        int dockScalePercent,
        bool reserve,
        string dockDensity = "Standard",
        bool forceReapply = false)
    {
        if (!_nativeTaskbarSuppressed || _disposed)
            return false;

        try
        {
            CaptureOriginalMonitorWorkAreasIfEmpty();

            var monitors = EnumerateLiveMonitorSnapshots();
            if (monitors.Count == 0)
                return false;

            string signature = BuildDockReservationSignature(reserve, dockEdge, dockScalePercent, dockDensity, monitors);
            if (!forceReapply && string.Equals(_lastDockReservationSignature, signature, StringComparison.Ordinal))
            {
                bool slotsHealthy = !reserve
                    ? _dockAppBarSlots.Count == 0
                    : monitors.All(m => _dockAppBarSlots.TryGetValue(m.DeviceId, out var slot) && slot.Hwnd != 0 && IsWindow(slot.Hwnd));
                if (slotsHealthy)
                    return false;
            }

            _lastDockReservationSignature = signature;

            if (!reserve)
            {
                // Unpinned (SmartDodge Intellihide / AutoHide): cleanly unregister all AppBar slots so
                // Explorer natively restores 100% monitor WorkArea and expands maximized windows ONCE.
                ReleaseAllDockAppBarSlots();
                SuppressAllTrayWindows();

                // Verify every monitor's rcWork is cleanly back to 100% monitor bounds (handles secondary monitors / fallback)
                foreach (var mon in monitors)
                {
                    if (TryGetMonitorWorkArea(mon.HMonitor, out var liveWork) &&
                        (liveWork.Left != mon.Bounds.X || liveWork.Top != mon.Bounds.Y ||
                         liveWork.Right != mon.Bounds.Right || liveWork.Bottom != mon.Bounds.Bottom))
                    {
                        var fullRc = new NativeMethods.RECT
                        {
                            Left = mon.Bounds.X,
                            Top = mon.Bounds.Y,
                            Right = mon.Bounds.Right,
                            Bottom = mon.Bounds.Bottom
                        };
                        SystemParametersInfoW(SPI_SETWORKAREA, 0, ref fullRc, 0);
                    }
                }

                return true;
            }

            // Pinned Fixed Bar: maintain one invisible click-through AppBar slot per active monitor
            var activeIds = monitors.Select(m => m.DeviceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var staleId in _dockAppBarSlots.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            {
                ReleaseDockAppBarSlot(staleId);
            }

            uint uEdge = string.Equals(dockEdge, "Left", StringComparison.OrdinalIgnoreCase)
                ? ABE_LEFT
                : (string.Equals(dockEdge, "Right", StringComparison.OrdinalIgnoreCase) ? ABE_RIGHT : ABE_BOTTOM);

            foreach (var mon in monitors)
            {
                var strip = ComputeAppBarReservationRect(mon.Bounds, mon.ScaleFactor, dockEdge, dockScalePercent, dockDensity);
                var desiredWork = ComputeDockReservedWorkArea(mon.Bounds, mon.ScaleFactor, dockEdge, dockScalePercent, reserve: true, dockDensity);

                bool registeredViaAppBar = EnsureAndPositionMonitorAppBarSlot(mon, uEdge, strip);
                if (!registeredViaAppBar)
                {
                    // Fallback if Explorer shell is absent: apply SPI_SETWORKAREA once
                    var rc = new NativeMethods.RECT
                    {
                        Left = desiredWork.X,
                        Top = desiredWork.Y,
                        Right = desiredWork.Right,
                        Bottom = desiredWork.Bottom
                    };
                    SystemParametersInfoW(SPI_SETWORKAREA, 0, ref rc, 0);
                }
                else if (TryGetMonitorWorkArea(mon.HMonitor, out var liveWork))
                {
                    // Secondary monitor guard: if Windows didn't carve this monitor's rcWork via ABM_SETPOS, apply once
                    bool carved = uEdge switch
                    {
                        ABE_LEFT => liveWork.Left >= mon.Bounds.X + strip.Width - 2,
                        ABE_RIGHT => liveWork.Right <= mon.Bounds.Right - strip.Width + 2,
                        _ => liveWork.Bottom <= mon.Bounds.Bottom - strip.Height + 2
                    };
                    if (!carved)
                    {
                        var rc = new NativeMethods.RECT
                        {
                            Left = desiredWork.X,
                            Top = desiredWork.Y,
                            Right = desiredWork.Right,
                            Bottom = desiredWork.Bottom
                        };
                        SystemParametersInfoW(SPI_SETWORKAREA, 0, ref rc, 0);
                    }
                }
            }

            // Explorer occasionally tries to show Shell_SecondaryTrayWnd after AppBar position changes
            SuppressAllTrayWindows();
            return true;
        }
        catch (Exception ex)
        {
            logger.Warn("AppBar", "DOCK_WORKAREA_WARN", $"Dock work-area reservation warning ({ex.GetType().Name}).");
            return false;
        }
    }

    /// <summary>
    /// One-shot post-transition check (invoked ONCE ~150ms after an explicit Pin/Mode/Edge change,
    /// NEVER on a repeating timer). Inspects each maximized user window's actual visible bounds via
    /// DWMWA_EXTENDED_FRAME_BOUNDS; windows already resized by Windows Explorer are left completely
    /// untouched, and any stubborn Chromium/Electron/UWP window is snapped cleanly ONCE using its
    /// exact measured invisible shadow margins.
    /// </summary>
    public void RefitMismatchedMaximizedWindowsOnce()
    {
        if (!_nativeTaskbarSuppressed || _disposed)
            return;

        try
        {
            uint ownPid = (uint)Environment.ProcessId;

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
                if (!GetWindowRect(hwnd, out var winRc))
                    return true;

                NativeMethods.RECT visRc = winRc;
                bool hasDwmVis = DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var extRc, Marshal.SizeOf<NativeMethods.RECT>()) == 0
                                 && extRc.Width > 0 && extRc.Height > 0;
                if (hasDwmVis)
                    visRc = extRc;

                // If Explorer already aligned this maximized window cleanly to rcWork, DO NOT touch it!
                bool alreadyAligned =
                    Math.Abs(visRc.Left - wa.Left) <= 4 &&
                    Math.Abs(visRc.Top - wa.Top) <= 4 &&
                    Math.Abs(visRc.Right - wa.Right) <= 4 &&
                    Math.Abs(visRc.Bottom - wa.Bottom) <= 4;

                if (alreadyAligned)
                    return true;

                int padL = hasDwmVis ? Math.Clamp(visRc.Left - winRc.Left, 0, 20) : 0;
                int padT = hasDwmVis ? Math.Clamp(visRc.Top - winRc.Top, 0, 20) : 0;
                int padR = hasDwmVis ? Math.Clamp(winRc.Right - visRc.Right, 0, 20) : 0;
                int padB = hasDwmVis ? Math.Clamp(winRc.Bottom - visRc.Bottom, 0, 20) : 0;

                // Notify ONLY this lagging user window (never Shell_TrayWnd) and snap once cleanly
                SendNotifyMessageW(hwnd, WM_SETTINGCHANGE, SPI_SETWORKAREA, 0);
                NativeMethods.SetWindowPos(
                    hwnd,
                    0,
                    wa.Left - padL,
                    wa.Top - padT,
                    (wa.Right - wa.Left) + padL + padR,
                    (wa.Bottom - wa.Top) + padT + padB,
                    SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS);

                return true;
            }, 0);
        }
        catch { }
    }

    private sealed record LiveMonitorSnapshot(nint HMonitor, string DeviceId, PhysicalPixelRect Bounds, double ScaleFactor);

    private static List<LiveMonitorSnapshot> EnumerateLiveMonitorSnapshots()
    {
        var list = new List<LiveMonitorSnapshot>(4);
        int idx = 1;
        NativeMethods.EnumDisplayMonitors(0, 0, (nint hMon, nint _, ref NativeMethods.RECT _, nint _) =>
        {
            var info = new NativeMethods.MONITORINFOEXW { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEXW>() };
            if (!NativeMethods.GetMonitorInfoW(hMon, ref info))
                return true;

            uint dpiX = 96;
            if (NativeMethods.GetDpiForMonitor(hMon, NativeMethods.MDT_EFFECTIVE_DPI, out var dx, out _) == 0 && dx > 0)
                dpiX = dx;

            string devId = string.IsNullOrWhiteSpace(info.szDevice) ? $"DISPLAY_{idx}" : info.szDevice.Trim();
            var monPx = new PhysicalPixelRect(info.rcMonitor.Left, info.rcMonitor.Top, info.rcMonitor.Width, info.rcMonitor.Height);
            list.Add(new LiveMonitorSnapshot(hMon, devId, monPx, Math.Max(1.0, dpiX / 96.0)));
            idx++;
            return true;
        }, 0);
        return list;
    }

    private static string BuildDockReservationSignature(
        bool reserve,
        string dockEdge,
        int dockScalePercent,
        string dockDensity,
        IReadOnlyList<LiveMonitorSnapshot> monitors)
    {
        var monPart = string.Join(";", monitors.Select(m => $"{m.DeviceId}:{m.Bounds.X},{m.Bounds.Y},{m.Bounds.Width}x{m.Bounds.Height}@{m.ScaleFactor:F2}"));
        return $"{reserve}|{dockEdge}|{dockScalePercent}|{dockDensity}|{monPart}";
    }

    private static bool TryGetMonitorWorkArea(nint hMon, out NativeMethods.RECT workArea)
    {
        var info = new NativeMethods.MONITORINFOEXW { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEXW>() };
        if (NativeMethods.GetMonitorInfoW(hMon, ref info))
        {
            workArea = info.rcWork;
            return true;
        }
        workArea = default;
        return false;
    }

    private bool EnsureAndPositionMonitorAppBarSlot(LiveMonitorSnapshot mon, uint uEdge, PhysicalPixelRect strip)
    {
        if (!_dockAppBarSlots.TryGetValue(mon.DeviceId, out var slot) || slot.Hwnd == 0 || !IsWindow(slot.Hwnd))
        {
            if (slot is not null)
                ReleaseDockAppBarSlot(mon.DeviceId);

            nint hwnd = CreateWindowExW(
                WS_EX_TOOLWINDOW_U | WS_EX_NOACTIVATE_U | WS_EX_TRANSPARENT | WS_EX_LAYERED,
                "STATIC",
                $"ProjectAurora_DockSlot_{mon.DeviceId}",
                WS_POPUP,
                strip.X, strip.Y, strip.Width, strip.Height,
                0, 0, 0, 0);

            if (hwnd == 0)
                return false;

            SetLayeredWindowAttributes(hwnd, 0, 0, LWA_ALPHA);
            NativeMethods.ShowWindow(hwnd, SW_SHOWNOACTIVATE);

            uint cbMsg = NativeMethods.RegisterWindowMessageW($"ProjectAurora_DockAppBar_{hwnd}");
            var newAbd = new NativeMethods.APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(),
                hWnd = hwnd,
                uCallbackMessage = cbMsg
            };

            if (NativeMethods.SHAppBarMessage(NativeMethods.ABM_NEW, ref newAbd) == 0)
            {
                DestroyWindow(hwnd);
                return false;
            }

            slot = new DockAppBarSlot(hwnd, cbMsg, mon.DeviceId);
            _dockAppBarSlots[mon.DeviceId] = slot;
        }

        var abd = new NativeMethods.APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(),
            hWnd = slot.Hwnd,
            uEdge = uEdge,
            rc = new NativeMethods.RECT
            {
                Left = mon.Bounds.X,
                Top = mon.Bounds.Y,
                Right = mon.Bounds.Right,
                Bottom = mon.Bounds.Bottom
            }
        };

        NativeMethods.SHAppBarMessage(NativeMethods.ABM_QUERYPOS, ref abd);

        switch (uEdge)
        {
            case ABE_LEFT:
                abd.rc.Right = abd.rc.Left + strip.Width;
                break;
            case ABE_RIGHT:
                abd.rc.Left = abd.rc.Right - strip.Width;
                break;
            default:
                abd.rc.Top = abd.rc.Bottom - strip.Height;
                break;
        }

        NativeMethods.SHAppBarMessage(NativeMethods.ABM_SETPOS, ref abd);

        NativeMethods.SetWindowPos(
            slot.Hwnd,
            HWND_BOTTOM,
            abd.rc.Left,
            abd.rc.Top,
            Math.Max(1, abd.rc.Width),
            Math.Max(1, abd.rc.Height),
            NativeMethods.SWP_NOACTIVATE | SWP_NOSENDCHANGING);

        return true;
    }

    private void ReleaseDockAppBarSlot(string monitorDeviceId)
    {
        if (!_dockAppBarSlots.Remove(monitorDeviceId, out var slot))
            return;

        try
        {
            if (slot.Hwnd != 0)
            {
                var abd = new NativeMethods.APPBARDATA
                {
                    cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(),
                    hWnd = slot.Hwnd
                };
                NativeMethods.SHAppBarMessage(NativeMethods.ABM_REMOVE, ref abd);
                if (IsWindow(slot.Hwnd))
                    DestroyWindow(slot.Hwnd);
            }
        }
        catch { }
    }

    private void ReleaseAllDockAppBarSlots()
    {
        foreach (var id in _dockAppBarSlots.Keys.ToArray())
        {
            ReleaseDockAppBarSlot(id);
        }
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

    private static void HideTrayWindow(nint hwnd)
    {
        if (hwnd == 0) return;
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
        NativeMethods.SetWindowPos(
            hwnd,
            HWND_BOTTOM,
            0, 0, 0, 0,
            SWP_HIDEWINDOW | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    private static void SuppressAllTrayWindows()
    {
        foreach (var hwnd in EnumerateAllTrayWindows())
            HideTrayWindow(hwnd);
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

        ReleaseAllDockAppBarSlots();

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
