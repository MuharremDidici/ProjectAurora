using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace Aurora.Platform.Windows.DisplayAndTaskbar;

[Flags]
public enum ResizeEdges
{
    None = 0,
    Left = 1,
    Right = 2,
    Top = 4,
    Bottom = 8
}

/// <summary>
/// AltSnap-grade Alt+Left-Drag (Move from anywhere) and Alt+Right-Drag (3x3 Smart Resize
/// from anywhere) manipulator for both normal Windows applications AND Aurora's own Dock
/// (Alt+Left-Drag repositions/snaps the Dock across Bottom/Left/Right edges & offsets;
/// Alt+Right-Drag smoothly resizes the Dock live from 75% to 135%).
/// </summary>
public sealed class AltDragWindowService : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmMouseMove = 0x0200;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int WmRButtonDown = 0x0204;
    private const int WmRButtonUp = 0x0205;

    private const int VkMenu = 0x12; // Alt
    private const ushort VkUnassignedMenuSuppress = 0xE8;
    private const uint InputKeyboard = 1;
    private const uint KeyeventfKeyUp = 0x0002;

    private const uint GaRoot = 2;
    private const int GwlStyle = -16;
    private const long WsThickFrame = 0x00040000L;
    private const long WsCaption = 0x00C00000L;
    private const int SwRestore = 9;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpAsyncWindowPos = 0x4000;

    private const int MinWindowWidthPx = 260;
    private const int MinWindowHeightPx = 150;

    private static readonly HashSet<string> ProtectedShellClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "Windows.UI.Core.CoreWindow",
        "MultitaskingViewFrame",
        "Xaml_WindowedPopupClass",
        "NotifyIconOverflowWindow"
    };

    private enum DragOperation { None, Move, Resize, DockMove, DockResize }

    private delegate nint LowLevelMouseProc(int nCode, nint wParam, nint lParam);

    private readonly LowLevelMouseProc _proc;
    private readonly uint _ownPid = (uint)Environment.ProcessId;
    private readonly Func<nint, bool>? _isExcludedHwnd;
    private readonly Func<Point, nint>? _dockHitResolver;
    private nint _hookHandle;
    private bool _disposed;

    private DragOperation _op = DragOperation.None;
    private nint _targetHwnd;
    private Point _startCursor;
    private Rectangle _startWinRect;
    private ResizeEdges _resizeEdges;
    private long _lastMoveTickMs;

    public event Action<nint>? AltMoveStarted;
    public event Action<nint>? AltMoveEnded;

    /// <summary>(dockHwnd, isResize, startCursorPt)</summary>
    public event Action<nint, bool, Point>? DockAltDragStarted;

    /// <summary>(dockHwnd, isResize, startCursorPt, currentCursorPt)</summary>
    public event Action<nint, bool, Point, Point>? DockAltDragMoved;

    /// <summary>(dockHwnd, isResize, startCursorPt, endCursorPt)</summary>
    public event Action<nint, bool, Point, Point>? DockAltDragEnded;

    public bool IsActive => _hookHandle != 0;

    public AltDragWindowService(
        Func<nint, bool>? isExcludedHwnd = null,
        Func<Point, nint>? dockHitResolver = null)
    {
        _isExcludedHwnd = isExcludedHwnd;
        _dockHitResolver = dockHitResolver;
        _proc = HookCallback;
    }

    public void SetEnabled(bool enabled)
    {
        if (_disposed) return;
        if (enabled && _hookHandle == 0)
        {
            using var curProc = Process.GetCurrentProcess();
            using var curMod = curProc.MainModule;
            nint hMod = curMod is not null ? GetModuleHandleW(curMod.ModuleName) : 0;
            _hookHandle = SetWindowsHookExW(WhMouseLl, _proc, hMod, 0);
        }
        else if (!enabled && _hookHandle != 0)
        {
            CancelActiveDrag();
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = 0;
        }
    }

    /// <summary>
    /// Divides a window into a 3x3 grid to determine intuitive resize edges from any grab point.
    /// Center 1/9th defaults smoothly to Bottom-Right corner resize.
    /// </summary>
    public static ResizeEdges ResolveResizeEdges(Rectangle winRect, Point grabPoint)
    {
        if (winRect.Width <= 0 || winRect.Height <= 0)
            return ResizeEdges.Right | ResizeEdges.Bottom;

        double relX = Math.Clamp((grabPoint.X - winRect.Left) / (double)winRect.Width, 0.0, 1.0);
        double relY = Math.Clamp((grabPoint.Y - winRect.Top) / (double)winRect.Height, 0.0, 1.0);

        var edges = ResizeEdges.None;
        if (relX < 0.33) edges |= ResizeEdges.Left;
        else if (relX > 0.67) edges |= ResizeEdges.Right;

        if (relY < 0.33) edges |= ResizeEdges.Top;
        else if (relY > 0.67) edges |= ResizeEdges.Bottom;

        return edges == ResizeEdges.None ? (ResizeEdges.Right | ResizeEdges.Bottom) : edges;
    }

    /// <summary>
    /// Computes clamped new window bounds during Alt+Right-Drag resize, preventing window inversion.
    /// </summary>
    public static Rectangle ComputeResizedRect(Rectangle startRect, ResizeEdges edges, int dx, int dy)
    {
        int left = startRect.Left;
        int top = startRect.Top;
        int right = startRect.Right;
        int bottom = startRect.Bottom;

        if ((edges & ResizeEdges.Left) != 0)
            left = Math.Min(startRect.Left + dx, right - MinWindowWidthPx);
        if ((edges & ResizeEdges.Right) != 0)
            right = Math.Max(startRect.Right + dx, left + MinWindowWidthPx);
        if ((edges & ResizeEdges.Top) != 0)
            top = Math.Min(startRect.Top + dy, bottom - MinWindowHeightPx);
        if ((edges & ResizeEdges.Bottom) != 0)
            bottom = Math.Max(startRect.Bottom + dy, top + MinWindowHeightPx);

        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    /// <summary>
    /// Resolves which monitor edge (Left / Right / Bottom) and magnetic edge offset (-100..+100,
    /// snapping cleanly to 0 center within ±10%) the Dock should dock to during Alt+Left-Drag.
    /// </summary>
    public static (string Edge, int OffsetPercent) ResolveTargetDockEdgeAndOffset(Rectangle monitorBounds, Point cursorPx)
    {
        if (monitorBounds.Width <= 0 || monitorBounds.Height <= 0)
            return ("Bottom", 0);

        int sideBand = Math.Clamp((int)Math.Round(monitorBounds.Width * 0.20), 140, 320);
        int bottomCornerBand = Math.Clamp((int)Math.Round(monitorBounds.Height * 0.10), 80, 120);
        bool nearBottomEdge = cursorPx.Y >= monitorBounds.Bottom - bottomCornerBand;
        int effectiveSideBand = nearBottomEdge ? 90 : sideBand;

        if (cursorPx.X <= monitorBounds.Left + effectiveSideBand)
        {
            double relY = Math.Clamp((cursorPx.Y - monitorBounds.Top) / (double)monitorBounds.Height, 0.0, 1.0);
            int offset = (int)Math.Round((relY - 0.5) * 200.0);
            if (Math.Abs(offset) <= 10) offset = 0;
            return ("Left", Math.Clamp(offset, -100, 100));
        }

        if (cursorPx.X >= monitorBounds.Right - effectiveSideBand)
        {
            double relY = Math.Clamp((cursorPx.Y - monitorBounds.Top) / (double)monitorBounds.Height, 0.0, 1.0);
            int offset = (int)Math.Round((relY - 0.5) * 200.0);
            if (Math.Abs(offset) <= 10) offset = 0;
            return ("Right", Math.Clamp(offset, -100, 100));
        }

        double relX = Math.Clamp((cursorPx.X - monitorBounds.Left) / (double)monitorBounds.Width, 0.0, 1.0);
        int hOffset = (int)Math.Round((relX - 0.5) * 200.0);
        if (Math.Abs(hOffset) <= 10) hOffset = 0;
        return ("Bottom", Math.Clamp(hOffset, -100, 100));
    }

    /// <summary>
    /// Computes the live Dock scale percentage (75%..135%) during Alt+Right-Drag on the Dock.
    /// Pulling away from the docked edge grows the dock; pushing toward the edge shrinks it.
    /// </summary>
    public static int ComputeResizedDockScale(int startScalePercent, string dockEdge, int dx, int dy)
    {
        double effectivePx = dockEdge switch
        {
            "Left" => Math.Abs(dx) >= Math.Abs(dy) ? dx : -dy,
            "Right" => Math.Abs(dx) >= Math.Abs(dy) ? -dx : -dy,
            _ => Math.Abs(dy) >= Math.Abs(dx) ? -dy : dx
        };

        int deltaPct = (int)Math.Round(effectivePx / 4.0);
        return Math.Clamp(startScalePercent + deltaPct, 75, 135);
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode < 0)
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        int msg = (int)wParam;

        // Ultra-fast path (<200ns): when no drag is active and Alt is not held, pass straight through
        if (_op == DragOperation.None)
        {
            if (msg != WmLButtonDown && msg != WmRButtonDown)
                return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

            bool altDown = (GetAsyncKeyState(VkMenu) & 0x8000) != 0;
            if (!altDown)
                return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

            var hs = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var pt = new Point(hs.pt.X, hs.pt.Y);

            if (TryBeginAltDrag(pt, isResize: msg == WmRButtonDown))
                return new nint(1); // Swallow click cleanly so app/dock doesn't click or open context menu

            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        // Active drag in progress
        if (msg == WmMouseMove)
        {
            var hs = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            ApplyActiveDragMove(hs.pt.X, hs.pt.Y);
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        if (((_op is DragOperation.Move or DragOperation.DockMove) && msg == WmLButtonUp) ||
            ((_op is DragOperation.Resize or DragOperation.DockResize) && msg == WmRButtonUp))
        {
            var hs = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            FinishActiveDrag(new Point(hs.pt.X, hs.pt.Y));
            return new nint(1); // Swallow matching release so app/dock doesn't pop a right-click context menu!
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private bool TryBeginAltDrag(Point cursorPt, bool isResize)
    {
        // 1. Check Aurora's own Dock interactive hit area FIRST so Alt+Left-Drag moves/docks
        //    and Alt+Right-Drag resizes the Aurora Dock itself just like normal windows!
        if (_dockHitResolver is not null)
        {
            nint dockHwnd = 0;
            try { dockHwnd = _dockHitResolver(cursorPt); } catch { }
            if (dockHwnd != 0)
            {
                _targetHwnd = dockHwnd;
                _startCursor = cursorPt;
                _startWinRect = Rectangle.Empty;
                _resizeEdges = ResizeEdges.None;
                _op = isResize ? DragOperation.DockResize : DragOperation.DockMove;
                _lastMoveTickMs = 0;
                try { DockAltDragStarted?.Invoke(dockHwnd, isResize, cursorPt); } catch { }
                return true;
            }
        }

        // 2. Standard Windows application Alt+Drag
        nint childHwnd = WindowFromPoint(new POINT { X = cursorPt.X, Y = cursorPt.Y });
        if (childHwnd == 0) return false;

        nint rootHwnd = GetAncestor(childHwnd, GaRoot);
        if (rootHwnd == 0 || !IsWindow(rootHwnd)) return false;

        GetWindowThreadProcessId(rootHwnd, out uint pid);
        if (pid == 0 || pid == _ownPid) return false;
        if (_isExcludedHwnd is not null && _isExcludedHwnd(rootHwnd)) return false;

        var clsBuf = new StringBuilder(96);
        if (GetClassNameW(rootHwnd, clsBuf, clsBuf.Capacity) > 0 &&
            ProtectedShellClasses.Contains(clsBuf.ToString()))
            return false;

        long style = GetWindowLongPtrW(rootHwnd, GwlStyle);
        // Never manipulate borderless fullscreen games (no caption and no thick frame)
        if ((style & WsCaption) == 0 && (style & WsThickFrame) == 0)
            return false;

        if (isResize && (style & WsThickFrame) == 0)
            return false;

        if (!GetWindowRect(rootHwnd, out var rc))
            return false;

        if (IsZoomed(rootHwnd))
        {
            // Restore maximized window cleanly and keep cursor proportionally anchored on move
            // (supports multi-monitor negative X/Y coordinates!)
            double ratioX = rc.Width > 0 ? Math.Clamp((cursorPt.X - rc.Left) / (double)rc.Width, 0.1, 0.9) : 0.5;
            ShowWindow(rootHwnd, SwRestore);
            if (!GetWindowRect(rootHwnd, out rc))
                return false;

            if (!isResize)
            {
                int newLeft = cursorPt.X - (int)Math.Round(rc.Width * ratioX);
                int newTop = cursorPt.Y - 22;
                SetWindowPos(rootHwnd, 0, newLeft, newTop, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpAsyncWindowPos);
                rc = new RECT { Left = newLeft, Top = newTop, Right = newLeft + rc.Width, Bottom = newTop + rc.Height };
            }
        }

        _targetHwnd = rootHwnd;
        _startCursor = cursorPt;
        _startWinRect = Rectangle.FromLTRB(rc.Left, rc.Top, rc.Right, rc.Bottom);
        _op = isResize ? DragOperation.Resize : DragOperation.Move;
        _resizeEdges = isResize ? ResolveResizeEdges(_startWinRect, cursorPt) : ResizeEdges.None;
        _lastMoveTickMs = 0;

        SetForegroundWindow(rootHwnd);

        if (_op == DragOperation.Move)
        {
            try { AltMoveStarted?.Invoke(rootHwnd); } catch { }
        }
        return true;
    }

    private void ApplyActiveDragMove(int cursorX, int cursorY)
    {
        // Coalesce high-DPI gaming mice (1000Hz) to ~120 FPS (8ms) so UI & SetWindowPos stay silky-smooth
        long nowMs = Environment.TickCount64;
        if (nowMs - _lastMoveTickMs < 8)
            return;
        _lastMoveTickMs = nowMs;

        if (_op is DragOperation.DockMove or DragOperation.DockResize)
        {
            try { DockAltDragMoved?.Invoke(_targetHwnd, _op == DragOperation.DockResize, _startCursor, new Point(cursorX, cursorY)); } catch { }
            return;
        }

        if (_targetHwnd == 0 || !IsWindow(_targetHwnd))
        {
            CancelActiveDrag();
            return;
        }

        int dx = cursorX - _startCursor.X;
        int dy = cursorY - _startCursor.Y;

        if (_op == DragOperation.Move)
        {
            SetWindowPos(
                _targetHwnd,
                0,
                _startWinRect.Left + dx,
                _startWinRect.Top + dy,
                0,
                0,
                SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpAsyncWindowPos);
        }
        else if (_op == DragOperation.Resize)
        {
            var next = ComputeResizedRect(_startWinRect, _resizeEdges, dx, dy);
            SetWindowPos(
                _targetHwnd,
                0,
                next.Left,
                next.Top,
                next.Width,
                next.Height,
                SwpNoZOrder | SwpNoActivate | SwpAsyncWindowPos);
        }
    }

    private void FinishActiveDrag(Point endCursorPt)
    {
        var endedOp = _op;
        var hwnd = _targetHwnd;
        var startPt = _startCursor;
        _op = DragOperation.None;
        _targetHwnd = 0;
        _resizeEdges = ResizeEdges.None;

        // Prevent releasing Alt from accidentally activating the foreground app's File/Edit menu bar
        SuppressLoneAltMenuActivation();

        if (endedOp is DragOperation.DockMove or DragOperation.DockResize)
        {
            try { DockAltDragEnded?.Invoke(hwnd, endedOp == DragOperation.DockResize, startPt, endCursorPt); } catch { }
        }
        else if (endedOp == DragOperation.Move && hwnd != 0)
        {
            try { AltMoveEnded?.Invoke(hwnd); } catch { }
        }
    }

    private static void SuppressLoneAltMenuActivation()
    {
        try
        {
            if ((GetAsyncKeyState(VkMenu) & 0x8000) == 0)
                return;

            var input = new INPUT
            {
                type = InputKeyboard,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = VkUnassignedMenuSuppress,
                        dwFlags = KeyeventfKeyUp
                    }
                }
            };
            SendInput(1, [input], Marshal.SizeOf<INPUT>());
        }
        catch { }
    }

    private void CancelActiveDrag()
    {
        _op = DragOperation.None;
        _targetHwnd = 0;
        _resizeEdges = ResizeEdges.None;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelActiveDrag();
        if (_hookHandle != 0)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookExW(int idHook, LowLevelMouseProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint hwnd, uint gaFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(nint hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtrW(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? lpModuleName);
}
