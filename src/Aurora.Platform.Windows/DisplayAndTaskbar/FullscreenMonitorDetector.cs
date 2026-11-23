using System.Runtime.InteropServices;
using System.Text;
using Aurora.Core.Models;
using Aurora.Core.Primitives;
using Aurora.Platform.Windows.Win32;

namespace Aurora.Platform.Windows.DisplayAndTaskbar;

public sealed class FullscreenMonitorDetector
{
    private const int GwlStyle = -16;
    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const int DwmwaExtendedFrameBounds = 9;
    private const uint MonitorDefaultToNearest = 2;

    // QUERY_USER_NOTIFICATION_STATE values
    private const int QunsBusy = 2;
    private const int QunsRunningD3dFullScreen = 3;
    private const int QunsPresentationMode = 4;

    private static readonly HashSet<string> IgnoredShellClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "Windows.UI.Core.CoreWindow",
        "EdgeUiInputTopWndClass",
        "MultitaskingViewFrame",
        "ForegroundStaging",
        "ApplicationManager_DesktopShellWindow",
        "Xaml_WindowedPopupClass",
        "NotifyIconOverflowWindow",
        "TopLevelWindowForOverflowXamlIsland"
    };

    private readonly uint _currentPid = (uint)Environment.ProcessId;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out NativeMethods.RECT lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(nint hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int dwAttribute, out NativeMethods.RECT pvAttribute, int cbAttribute);

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int pquns);

    /// <summary>
    /// Returns the set of MonitorDescriptor.DeviceId values that currently have an active
    /// fullscreen video, game, borderless fullscreen app, or presentation covering them.
    /// </summary>
    public HashSet<string> GetFullscreenMonitorDeviceIds(
        IReadOnlyList<MonitorDescriptor> monitors,
        IReadOnlySet<nint>? excludedHwnds = null) =>
        AnalyzeMonitors(monitors, excludedHwnds).FullscreenMonitors;

    /// <summary>
    /// Single-pass Z-order analyzer returning BOTH:
    /// 1) FullscreenMonitors: monitors running true fullscreen video/games/presentations (Aurora yields completely).
    /// 2) DockOverlappedMonitors: monitors where a maximized or overlapping normal window reaches the bottom Dock zone
    ///    (used by Smart Dodge mode so the Dock glides away only when it would cover app content).
    /// </summary>
    public (HashSet<string> FullscreenMonitors, HashSet<string> DockOverlappedMonitors) AnalyzeMonitors(
        IReadOnlyList<MonitorDescriptor> monitors,
        IReadOnlySet<nint>? excludedHwnds = null,
        string dockEdge = "Bottom")
    {
        var fullscreenMonitors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dockOverlappedMonitors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (monitors.Count == 0)
            return (fullscreenMonitors, dockOverlappedMonitors);

        var monitorMap = monitors.ToDictionary(m => m.DeviceId, StringComparer.OrdinalIgnoreCase);
        var decidedMonitors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool globalFullscreenState = false;
        try
        {
            if (SHQueryUserNotificationState(out int state) == 0)
            {
                globalFullscreenState = state is QunsRunningD3dFullScreen or QunsPresentationMode or QunsBusy;
            }
        }
        catch
        {
            // Ignore shell query errors
        }

        nint fgHwnd = NativeMethods.GetForegroundWindow();
        var classBuf = new StringBuilder(96);

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (decidedMonitors.Count >= monitorMap.Count && dockOverlappedMonitors.Count >= monitorMap.Count)
                return false;

            if (hWnd == 0 || (excludedHwnds is not null && excludedHwnds.Contains(hWnd)))
                return true;

            if (!NativeMethods.IsWindowVisible(hWnd) || NativeMethods.IsIconic(hWnd))
                return true;

            if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;

            NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0 || pid == _currentPid)
                return true;

            long exStyle = NativeMethods.GetWindowLongPtr64(hWnd, NativeMethods.GWL_EXSTYLE);
            if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0)
                return true;

            classBuf.Clear();
            int clsLen = GetClassNameW(hWnd, classBuf, classBuf.Capacity);
            if (clsLen > 0 && IgnoredShellClasses.Contains(classBuf.ToString()))
                return true;

            NativeMethods.RECT rc;
            if (DwmGetWindowAttribute(hWnd, DwmwaExtendedFrameBounds, out rc, Marshal.SizeOf<NativeMethods.RECT>()) != 0)
            {
                if (!GetWindowRect(hWnd, out rc))
                    return true;
            }

            if (rc.Width < 300 || rc.Height < 200)
                return true;

            string? monId = ResolveMonitorDeviceId(hWnd);
            if (monId is null || !monitorMap.TryGetValue(monId, out var mon))
                return true;

            var b = mon.BoundsPx;
            int dockZoneSize = (int)Math.Round(70 * Math.Max(1.0, mon.ScaleFactor));
            bool isMaximized = IsZoomed(hWnd);
            bool overlapsDockZone;

            if (string.Equals(dockEdge, "Left", StringComparison.OrdinalIgnoreCase))
            {
                int dockZoneRight = b.X + dockZoneSize;
                int dockZoneTop = b.Y + (b.Height / 6);
                int dockZoneBottom = b.Bottom - (b.Height / 6);
                overlapsDockZone = rc.Left < dockZoneRight - 4 && rc.Right > b.X &&
                                   rc.Bottom > dockZoneTop && rc.Top < dockZoneBottom;
            }
            else if (string.Equals(dockEdge, "Right", StringComparison.OrdinalIgnoreCase))
            {
                int dockZoneLeft = b.Right - dockZoneSize;
                int dockZoneTop = b.Y + (b.Height / 6);
                int dockZoneBottom = b.Bottom - (b.Height / 6);
                overlapsDockZone = rc.Right > dockZoneLeft + 4 && rc.Left < b.Right &&
                                   rc.Bottom > dockZoneTop && rc.Top < dockZoneBottom;
            }
            else
            {
                int dockZoneTop = b.Bottom - dockZoneSize;
                int dockZoneLeft = b.X + (b.Width / 6);
                int dockZoneRight = b.Right - (b.Width / 6);
                overlapsDockZone = rc.Bottom > dockZoneTop + 4 && rc.Top < b.Bottom &&
                                   rc.Right > dockZoneLeft && rc.Left < dockZoneRight;
            }
            if (overlapsDockZone)
            {
                dockOverlappedMonitors.Add(monId);
            }

            if (decidedMonitors.Contains(monId))
                return true;

            bool coversFullMonitor =
                rc.Left <= b.X + 4 &&
                rc.Top <= b.Y + 4 &&
                rc.Right >= b.Right - 4 &&
                rc.Bottom >= b.Bottom - 4;

            if (!coversFullMonitor)
            {
                if (hWnd == fgHwnd)
                    decidedMonitors.Add(monId);
                return true;
            }

            long style = NativeMethods.GetWindowLongPtr64(hWnd, GwlStyle);
            bool hasCaption = (style & WsCaption) == WsCaption;
            bool hasThickFrame = (style & WsThickFrame) != 0;
            bool isFg = hWnd == fgHwnd;

            bool isTrueFullscreen = !hasCaption || !hasThickFrame || !isMaximized || (isFg && globalFullscreenState);

            decidedMonitors.Add(monId);
            if (isTrueFullscreen)
            {
                fullscreenMonitors.Add(monId);
            }

            return true;
        }, 0);

        return (fullscreenMonitors, dockOverlappedMonitors);
    }

    private static string? ResolveMonitorDeviceId(nint hwnd)
    {
        nint hMon = NativeMethods.MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (hMon == 0)
            return null;

        var mi = new NativeMethods.MONITORINFOEXW
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEXW>()
        };
        if (NativeMethods.GetMonitorInfoW(hMon, ref mi) && !string.IsNullOrWhiteSpace(mi.szDevice))
            return mi.szDevice.Trim();

        return null;
    }
}
