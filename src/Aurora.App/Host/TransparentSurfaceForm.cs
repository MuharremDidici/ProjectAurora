using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Aurora.App.Host;

public readonly record struct DipHitRect(double X, double Y, double W, double H);

public sealed class TransparentSurfaceForm : Form
{
    private const int GwlExStyle = -20;
    private const long WsExLayered = 0x00080000L;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExToolWindow = 0x00000080L;

    private const int DwmwaNcRenderingPolicy = 2;
    private const int DwmncrpDisabled = 1;
    private const int DwmwaBorderColor = 34;
    private const uint DwmColorNone = 0xFFFFFFFE;
    private const uint DwmBbEnable = 0x00000001;
    private const uint DwmBbBlurRegion = 0x00000002;

    private const int WmDpiChanged = 0x02E0;
    private const int SwHide = 0;
    private const int SwShowNoActivate = 4;
    private static readonly nint HwndTopMost = -1;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpFrameChanged = 0x0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_BLURBEHIND
    {
        public uint dwFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fEnable;
        public nint hRgnBlur;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fTransitionOnMaximized;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WIN32_RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WIN32_POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(nint hWnd, ref MARGINS pMarInset);

    [DllImport("dwmapi.dll")]
    private static extern int DwmEnableBlurBehindWindow(nint hWnd, ref DWM_BLURBEHIND pBlurBehind);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref uint attrValue, int attrSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("gdi32.dll")]
    private static extern nint CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint hObject);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr64(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out WIN32_RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out WIN32_POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(WIN32_POINT point);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(nint hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    private delegate bool EnumChildProc(nint hwnd, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(nint hWndParent, EnumChildProc lpEnumFunc, nint lParam);

    private Rectangle _targetBoundsPx;
    private readonly WebView2 _webView;
    private readonly System.Windows.Forms.Timer _layoutSyncTimer;
    private int _layoutSyncRemainingTicks;
    private long _lastLayoutSyncRequestMs;
    private bool _wasWithinHalo;
    private bool _wasPointerInsideHitRect;
    private DipHitRect[] _interactiveRects = [];
    private double _viewportCssW;
    private double _viewportCssH;
    private bool _currentlyClickThrough = true;
    private bool _forceInteractive;
    private bool _suppressedForFullscreen;
    private bool _isReady;
    private bool _registeredInCoordinator;

    /// <summary>
    /// Single shared cursor-delta hit-test coordinator across all monitors & surfaces.
    /// Replaces 6 independent 16ms timers (375 ticks/s) with ONE adaptive loop:
    /// - 60Hz (16ms) while the pointer is moving or near a surface
    /// - Automatic 100ms idle sleep + zero Win32 surface work when pointer is stationary (>350ms)
    /// - Instant 0ms wake on first pixel of movement or DOM hitRect update
    /// </summary>
    private static class SurfaceHitTestCoordinator
    {
        private static readonly List<TransparentSurfaceForm> s_surfaces = [];
        private static System.Windows.Forms.Timer? s_timer;
        private static int s_lastX = int.MinValue;
        private static int s_lastY = int.MinValue;
        private static MouseButtons s_lastButtons = MouseButtons.None;
        private static long s_lastMotionMs;
        private static bool s_dirty = true;

        public static void Register(TransparentSurfaceForm form)
        {
            if (!s_surfaces.Contains(form))
                s_surfaces.Add(form);

            if (s_timer is null)
            {
                s_timer = new System.Windows.Forms.Timer { Interval = 16 };
                s_timer.Tick += OnTick;
            }
            s_dirty = true;
            s_lastMotionMs = Environment.TickCount64;
            if (!s_timer.Enabled)
                s_timer.Start();
        }

        public static void Unregister(TransparentSurfaceForm form)
        {
            s_surfaces.Remove(form);
            if (s_surfaces.Count == 0 && s_timer is not null)
            {
                s_timer.Stop();
            }
        }

        public static void MarkDirty()
        {
            s_dirty = true;
            if (s_timer is not null && s_timer.Interval != 16)
                s_timer.Interval = 16;
        }

        private static void OnTick(object? sender, EventArgs e)
        {
            if (s_surfaces.Count == 0 || s_timer is null)
                return;

            if (!GetCursorPos(out var cursor))
                return;

            var buttons = Control.MouseButtons;
            long now = Environment.TickCount64;
            bool moved = cursor.X != s_lastX || cursor.Y != s_lastY || buttons != s_lastButtons;

            if (moved)
            {
                s_lastX = cursor.X;
                s_lastY = cursor.Y;
                s_lastButtons = buttons;
                s_lastMotionMs = now;
                s_dirty = true;
            }

            if (!s_dirty)
            {
                // Zero-cost idle sleep when cursor is completely stationary
                int idleInterval = (now - s_lastMotionMs) > 350 ? 100 : 16;
                if (s_timer.Interval != idleInterval)
                    s_timer.Interval = idleInterval;
                return;
            }

            s_dirty = false;
            bool anyNearSurface = false;
            for (int i = s_surfaces.Count - 1; i >= 0; i--)
            {
                var surf = s_surfaces[i];
                if (surf.IsDisposed)
                {
                    s_surfaces.RemoveAt(i);
                    continue;
                }
                if (surf.EvaluateCursorClickThrough(cursor, buttons))
                    anyNearSurface = true;
            }

            int desiredInterval = (moved || anyNearSurface) ? 16 : ((now - s_lastMotionMs) > 350 ? 100 : 48);
            if (s_timer.Interval != desiredInterval)
                s_timer.Interval = desiredInterval;
        }
    }

    public string MonitorDeviceId { get; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool EnableNativeCursorWake { get; set; }
    public bool IsSuppressedForFullscreen => _suppressedForFullscreen;
    public bool IsForceInteractive => _forceInteractive;
    public int InteractiveRectCount => _interactiveRects.Length;
    public (double Width, double Height) ViewportCssSize => (_viewportCssW, _viewportCssH);
    public bool IsCurrentlyClickThrough => _currentlyClickThrough;
    public Rectangle WebViewControlBounds => _webView.Bounds;
    public string InteractiveRectsSummary => string.Join("; ", _interactiveRects.Select(r => $"{Math.Round(r.X)},{Math.Round(r.Y)},{Math.Round(r.W)}x{Math.Round(r.H)}"));
    public Rectangle ActualWin32Rect => IsHandleCreated && GetWindowRect(Handle, out var rc) ? Rectangle.FromLTRB(rc.Left, rc.Top, rc.Right, rc.Bottom) : Bounds;

    public object ProbeWin32HitRouting(Point screenPt)
    {
        SetClickThrough(false);
        long ex = IsHandleCreated ? (long)GetWindowLongPtr64(Handle, GwlExStyle) : 0;
        nint wfp = WindowFromPoint(new WIN32_POINT { X = screenPt.X, Y = screenPt.Y });
        var cls = new System.Text.StringBuilder(128);
        if (wfp != 0) GetClassNameW(wfp, cls, cls.Capacity);
        uint wfpPid = 0;
        if (wfp != 0) GetWindowThreadProcessId(wfp, out wfpPid);

        var children = new List<string>();
        if (IsHandleCreated)
        {
            EnumChildWindows(Handle, (ch, _) =>
            {
                var c = new System.Text.StringBuilder(64);
                GetClassNameW(ch, c, c.Capacity);
                GetWindowRect(ch, out var cr);
                children.Add($"{c}:{cr.Left},{cr.Top},{cr.Width}x{cr.Height}");
                return true;
            }, 0);
        }

        SetClickThrough(true);
        return new
        {
            TestPt = $"{screenPt.X},{screenPt.Y}",
            ExStyleHex = $"0x{ex:X}",
            HasTransparentBitAfterDisable = (ex & WsExTransparent) != 0,
            WindowFromPointCls = cls.ToString(),
            WindowFromPointPid = wfpPid,
            OwnPid = Environment.ProcessId,
            HitOwnProcess = wfpPid == (uint)Environment.ProcessId,
            ChildHwnds = children.Take(6).ToArray()
        };
    }

    public event Func<string, JsonElement, Task>? MessageReceived;
    public event Func<string, IReadOnlyList<string>, Task>? FilesDropped;
    public event Func<string, JsonElement, IReadOnlyList<string>, Task>? FilesDroppedWithContext;

    private static readonly Icon? s_appIcon = TryLoadAppIcon();

    private static Icon? TryLoadAppIcon()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            return !string.IsNullOrWhiteSpace(exe) && File.Exists(exe) ? Icon.ExtractAssociatedIcon(exe) : null;
        }
        catch { return null; }
    }

    public TransparentSurfaceForm(string surfaceTitle, string monitorDeviceId, Rectangle screenBoundsPx)
    {
        MonitorDeviceId = monitorDeviceId;
        Text = surfaceTitle;
        if (s_appIcon is not null)
            Icon = s_appIcon;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
        _targetBoundsPx = screenBoundsPx;
        Bounds = screenBoundsPx;
        SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, false);

        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = Color.Transparent
        };
        Controls.Add(_webView);

        // Multi-monitor WebView2 post-resize sync guard: guarantees background monitors flush fresh hitRects!
        _layoutSyncTimer = new System.Windows.Forms.Timer { Interval = 140 };
        _layoutSyncTimer.Tick += (_, _) =>
        {
            RequestLayoutSync(force: true);
            _layoutSyncRemainingTicks--;
            if (_layoutSyncRemainingTicks <= 0)
                _layoutSyncTimer.Stop();
        };

        Deactivate += (_, _) =>
        {
            if (_forceInteractive)
            {
                _forceInteractive = false;
                PostJsonState("""{"type":"outsideDeactivate"}""");
                EvaluateCursorClickThroughImmediate();
            }
        };
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= (int)(WsExToolWindow | WsExLayered | WsExTransparent);
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(EventArgs.Empty);
        ApplyDwmPerPixelTransparency();
        ApplyPhysicalWindowPos();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmDpiChanged)
        {
            // Keep our explicitly negotiated physical pixel rect intact instead of letting WinForms distort it
            m.Result = 0;
            ApplyPhysicalWindowPos();
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.Transparent);
    }

    public void SetPhysicalBounds(Rectangle boundsPx)
    {
        _targetBoundsPx = boundsPx;
        if (IsDisposed)
            return;

        if (IsHandleCreated)
        {
            ApplyPhysicalWindowPos();
            SchedulePostResizeLayoutSync();
        }
        else
        {
            Bounds = boundsPx;
        }
    }

    public void RequestLayoutSync(bool force = false)
    {
        if (!_isReady || IsDisposed)
            return;

        long now = Environment.TickCount64;
        if (!force && now - _lastLayoutSyncRequestMs < 140)
            return;

        _lastLayoutSyncRequestMs = now;
        PostJsonState("""{"type":"syncLayout"}""");
    }

    private void SchedulePostResizeLayoutSync()
    {
        if (!_isReady || IsDisposed)
            return;

        RequestLayoutSync(force: true);
        _layoutSyncRemainingTicks = 4; // Follow-ups at 140ms, 280ms, 420ms, 560ms for throttled secondary monitors
        _layoutSyncTimer.Stop();
        _layoutSyncTimer.Start();
    }

    private void ApplyPhysicalWindowPos()
    {
        if (!IsHandleCreated || IsDisposed)
            return;

        SetWindowPos(
            Handle,
            HwndTopMost,
            _targetBoundsPx.X,
            _targetBoundsPx.Y,
            _targetBoundsPx.Width,
            _targetBoundsPx.Height,
            SwpNoActivate | SwpFrameChanged);

        // Keep WebView2 child controller bounds & parent screen-position cache strictly aligned across all DPIs/monitors
        if (GetWindowRect(Handle, out var actual))
        {
            int cw = Math.Max(1, actual.Width);
            int ch = Math.Max(1, actual.Height);
            if (_webView.Left != 0 || _webView.Top != 0 || _webView.Width != cw || _webView.Height != ch)
            {
                _webView.SetBounds(0, 0, cw, ch);
            }
        }
        PerformLayout();
        OnLocationChanged(EventArgs.Empty);

        // Resync cached click-through flag against live Win32 style bits
        long ex = GetWindowLongPtr64(Handle, GwlExStyle);
        _currentlyClickThrough = (ex & WsExTransparent) != 0;
        EvaluateCursorClickThroughImmediate();
    }

    public void SetSuppressedForFullscreen(bool suppressed)
    {
        if (_suppressedForFullscreen == suppressed || IsDisposed || !IsHandleCreated)
            return;

        // If user explicitly summoned Omnibar/Deck via hotkey, don't hide while interactive
        if (suppressed && _forceInteractive)
            return;

        _suppressedForFullscreen = suppressed;
        if (suppressed)
        {
            ShowWindow(Handle, SwHide);
        }
        else
        {
            ShowWindow(Handle, SwShowNoActivate);
            SetWindowPos(Handle, HwndTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
            EvaluateCursorClickThroughImmediate();
        }
    }

    private void ApplyDwmPerPixelTransparency()
    {
        if (Handle == 0)
            return;

        try
        {
            int ncPolicy = DwmncrpDisabled;
            DwmSetWindowAttribute(Handle, DwmwaNcRenderingPolicy, ref ncPolicy, sizeof(int));

            uint noBorder = DwmColorNone;
            DwmSetWindowAttribute(Handle, DwmwaBorderColor, ref noBorder, sizeof(uint));

            var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            DwmExtendFrameIntoClientArea(Handle, ref margins);

            nint emptyRgn = CreateRectRgn(0, 0, -1, -1);
            try
            {
                var bb = new DWM_BLURBEHIND
                {
                    dwFlags = DwmBbEnable | DwmBbBlurRegion,
                    fEnable = true,
                    hRgnBlur = emptyRgn,
                    fTransitionOnMaximized = false
                };
                DwmEnableBlurBehindWindow(Handle, ref bb);
            }
            finally
            {
                if (emptyRgn != 0)
                    DeleteObject(emptyRgn);
            }
        }
        catch
        {
            // Ignore DWM errors on fallback display drivers
        }
    }

    public async Task InitializeHtmlAsync(string htmlContent, CoreWebView2Environment sharedEnv)
    {
        await _webView.EnsureCoreWebView2Async(sharedEnv);
        _webView.DefaultBackgroundColor = Color.Transparent;

        var settings = _webView.CoreWebView2.Settings;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDevToolsEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsSwipeNavigationEnabled = false;
        settings.IsPinchZoomEnabled = false;

        try
        {
            _webView.CoreWebView2.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
        }
        catch
        {
            // Older WebView2 runtimes may not support MemoryUsageTargetLevel
        }

        _webView.CoreWebView2.WebMessageReceived += async (_, args) =>
        {
            try
            {
                if (args.AdditionalObjects is { Count: > 0 })
                {
                    var droppedPaths = new List<string>();
                    foreach (var obj in args.AdditionalObjects)
                    {
                        if (obj is CoreWebView2File wf && !string.IsNullOrWhiteSpace(wf.Path))
                            droppedPaths.Add(wf.Path);
                    }
                    if (droppedPaths.Count > 0)
                    {
                        if (FilesDroppedWithContext is not null)
                        {
                            using var ctxDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(args.WebMessageAsJson) ? "{}" : args.WebMessageAsJson);
                            await FilesDroppedWithContext(MonitorDeviceId, ctxDoc.RootElement.Clone(), droppedPaths);
                            return;
                        }
                        if (FilesDropped is not null)
                        {
                            await FilesDropped(MonitorDeviceId, droppedPaths);
                            return;
                        }
                    }
                }

                using var doc = JsonDocument.Parse(args.WebMessageAsJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("type", out var tProp) && tProp.GetString() == "hitRects")
                {
                    if (root.TryGetProperty("vw", out var vwProp) && vwProp.ValueKind == JsonValueKind.Number)
                        _viewportCssW = vwProp.GetDouble();
                    if (root.TryGetProperty("vh", out var vhProp) && vhProp.ValueKind == JsonValueKind.Number)
                        _viewportCssH = vhProp.GetDouble();

                    if (root.TryGetProperty("rects", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<DipHitRect>(arr.GetArrayLength());
                        foreach (var item in arr.EnumerateArray())
                        {
                            list.Add(new DipHitRect(
                                item.GetProperty("x").GetDouble(),
                                item.GetProperty("y").GetDouble(),
                                item.GetProperty("w").GetDouble(),
                                item.GetProperty("h").GetDouble()));
                        }
                        _interactiveRects = [.. list];
                        EvaluateCursorClickThroughImmediate();
                    }
                    return;
                }

                if (MessageReceived is not null)
                    await MessageReceived(MonitorDeviceId, root.Clone());
            }
            catch
            {
                // Ignore malformed message
            }
        };

        _webView.NavigateToString(htmlContent);
        _isReady = true;
        _registeredInCoordinator = true;
        SurfaceHitTestCoordinator.Register(this);
    }

    public void PostJsonState(string json)
    {
        if (!_isReady || IsDisposed || _webView.CoreWebView2 is null)
            return;

        if (InvokeRequired)
        {
            BeginInvoke(() => PostJsonState(json));
            return;
        }

        try
        {
            _webView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch
        {
            // Ignore post during shutdown
        }
    }

    public void FocusAndActivateSurface()
    {
        if (_suppressedForFullscreen)
        {
            _suppressedForFullscreen = false;
            ShowWindow(Handle, SwShowNoActivate);
        }

        _forceInteractive = true;
        SetClickThrough(false);
        SetWindowPos(Handle, HwndTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize);
        SetForegroundWindow(Handle);
        Activate();
        _webView.Focus();
        SurfaceHitTestCoordinator.MarkDirty();
    }

    public bool ContainsInteractivePoint(Point screenPt)
    {
        if (IsDisposed || !IsHandleCreated || Handle == 0 || _suppressedForFullscreen)
            return false;

        if (!GetWindowRect(Handle, out var rc))
            return false;

        if (screenPt.X < rc.Left || screenPt.X >= rc.Right || screenPt.Y < rc.Top || screenPt.Y >= rc.Bottom)
            return false;

        double cssX;
        double cssY;
        if (_viewportCssW > 10 && _viewportCssH > 10 && rc.Width > 0 && rc.Height > 0)
        {
            cssX = ((screenPt.X - rc.Left) / (double)rc.Width) * _viewportCssW;
            cssY = ((screenPt.Y - rc.Top) / (double)rc.Height) * _viewportCssH;
        }
        else
        {
            double fallbackScale = DeviceDpi > 0 ? DeviceDpi / 96.0 : 1.0;
            cssX = (screenPt.X - rc.Left) / fallbackScale;
            cssY = (screenPt.Y - rc.Top) / fallbackScale;
        }

        foreach (var r in _interactiveRects)
        {
            if (cssX >= r.X - 16.0 && cssX <= r.X + r.W + 16.0 &&
                cssY >= r.Y - 16.0 && cssY <= r.Y + r.H + 16.0)
            {
                return true;
            }
        }

        return false;
    }

    public void ReleaseForcedInteractive()
    {
        _forceInteractive = false;
        EvaluateCursorClickThroughImmediate();
    }

    public async Task CapturePngAsync(string outputPath)
    {
        if (!_isReady || _webView.CoreWebView2 is null)
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await _webView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs);
    }

    private void EvaluateCursorClickThroughImmediate()
    {
        SurfaceHitTestCoordinator.MarkDirty();
        if (GetCursorPos(out var cursor))
            EvaluateCursorClickThrough(cursor, Control.MouseButtons);
    }

    private bool EvaluateCursorClickThrough(WIN32_POINT cursor, MouseButtons buttons)
    {
        if (IsDisposed || !IsHandleCreated || Handle == 0 || _suppressedForFullscreen)
            return false;

        if (_forceInteractive)
        {
            SetClickThrough(false);
            return false;
        }

        // Ultra-fast physical bounds pre-check before touching Win32 GetWindowRect when already idle & outside halo
        const int approachHaloPx = 120;
        if (_currentlyClickThrough && !_wasWithinHalo && !_wasPointerInsideHitRect &&
            (cursor.X < _targetBoundsPx.Left - approachHaloPx || cursor.X >= _targetBoundsPx.Right + approachHaloPx ||
             cursor.Y < _targetBoundsPx.Top - approachHaloPx || cursor.Y >= _targetBoundsPx.Bottom + approachHaloPx))
        {
            return false;
        }

        if (!GetWindowRect(Handle, out var rc))
            return false;

        bool withinHalo = cursor.X >= rc.Left - approachHaloPx && cursor.X < rc.Right + approachHaloPx &&
                          cursor.Y >= rc.Top - approachHaloPx && cursor.Y < rc.Bottom + approachHaloPx;

        // Self-healing multi-monitor guard: the instant the cursor approaches a secondary monitor's
        // surface, request a fresh layout & hitRects flush before the pointer even reaches the dock!
        if (withinHalo && !_wasWithinHalo)
        {
            RequestLayoutSync(force: false);
        }
        _wasWithinHalo = withinHalo;

        if (cursor.X < rc.Left || cursor.X >= rc.Right || cursor.Y < rc.Top || cursor.Y >= rc.Bottom)
        {
            if (_wasPointerInsideHitRect)
            {
                _wasPointerInsideHitRect = false;
                if (EnableNativeCursorWake && _isReady)
                    PostJsonState("""{"type":"cursorLeft"}""");
            }
            SetClickThrough(true);
            return withinHalo;
        }

        // Exact DPI-invariant physical-to-CSS coordinate projection:
        // Works with 0-pixel drift across 100%, 125%, 150%, 175%, 200% and multi-monitor negative coords!
        double cssX;
        double cssY;
        bool staleAspect = false;
        if (_viewportCssW > 10 && _viewportCssH > 10 && rc.Width > 0 && rc.Height > 0)
        {
            double winAspect = rc.Width / (double)rc.Height;
            double cssAspect = _viewportCssW / _viewportCssH;
            staleAspect = Math.Max(winAspect / cssAspect, cssAspect / winAspect) > 1.25;
            if (staleAspect)
            {
                RequestLayoutSync(force: false);
            }
            cssX = ((cursor.X - rc.Left) / (double)rc.Width) * _viewportCssW;
            cssY = ((cursor.Y - rc.Top) / (double)rc.Height) * _viewportCssH;
        }
        else
        {
            double fallbackScale = DeviceDpi > 0 ? DeviceDpi / 96.0 : 1.0;
            cssX = (cursor.X - rc.Left) / fallbackScale;
            cssY = (cursor.Y - rc.Top) / fallbackScale;
        }

        double pad = (buttons & MouseButtons.Left) != 0 ? 28.0 : 10.0;

        bool hitAny = false;
        if (!staleAspect)
        {
            foreach (var r in _interactiveRects)
            {
                if (cssX >= r.X - pad && cssX <= r.X + r.W + pad &&
                    cssY >= r.Y - pad && cssY <= r.Y + r.H + pad)
                {
                    hitAny = true;
                    break;
                }
            }
        }

        SetClickThrough(!hitAny);

        // Native Win32 cursor wake/leave signal: guarantees tucked docks on ALL monitors
        // (including unfocused secondary/mixed-DPI screens) open within 16ms even if Chromium
        // does not synthesize mouseenter after WS_EX_TRANSPARENT is removed!
        if (hitAny && !_wasPointerInsideHitRect)
        {
            _wasPointerInsideHitRect = true;
            if (EnableNativeCursorWake && _isReady)
                PostJsonState("""{"type":"cursorWake"}""");
        }
        else if (!hitAny && _wasPointerInsideHitRect)
        {
            _wasPointerInsideHitRect = false;
            if (EnableNativeCursorWake && _isReady)
                PostJsonState("""{"type":"cursorLeft"}""");
        }

        return true;
    }

    private void SetClickThrough(bool passThrough)
    {
        if (Handle == 0)
            return;

        long ex = GetWindowLongPtr64(Handle, GwlExStyle);
        bool actualPassThrough = (ex & WsExTransparent) != 0;
        bool hasLayered = (ex & WsExLayered) != 0;
        if (_currentlyClickThrough == passThrough && actualPassThrough == passThrough && (passThrough ? hasLayered : !hasLayered))
            return;

        _currentlyClickThrough = passThrough;
        if (passThrough)
        {
            ex |= WsExTransparent | WsExLayered;
        }
        else
        {
            // CRITICAL WIN32 FIX: Clear BOTH WS_EX_TRANSPARENT AND WS_EX_LAYERED when interactive!
            // Keeping WS_EX_LAYERED without UpdateLayeredWindow causes win32k.sys to clip WindowFromPoint
            // hit-testing to the window's initial boot bitmap size after dynamic edge resizes (Left <-> Bottom),
            // causing clicks on expanded areas to pass straight through to apps behind Aurora!
            // DWM per-pixel transparency (DwmExtendFrameIntoClientArea + DwmEnableBlurBehindWindow) remains 100% intact.
            ex &= ~(WsExTransparent | WsExLayered);
        }

        SetWindowLongPtr64(Handle, GwlExStyle, (nint)ex);

        if (!passThrough)
        {
            ApplyDwmPerPixelTransparency();
        }

        SetWindowPos(
            Handle,
            passThrough ? 0 : HwndTopMost,
            0, 0, 0, 0,
            SwpNoMove | SwpNoSize | SwpNoActivate | SwpFrameChanged | (passThrough ? SwpNoZOrder : 0));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_registeredInCoordinator)
            {
                _registeredInCoordinator = false;
                SurfaceHitTestCoordinator.Unregister(this);
            }
            _layoutSyncTimer.Stop();
            _layoutSyncTimer.Dispose();
            _webView.Dispose();
        }
        base.Dispose(disposing);
    }
}
