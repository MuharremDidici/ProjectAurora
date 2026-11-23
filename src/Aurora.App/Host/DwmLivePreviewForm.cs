using System.Drawing;
using System.Runtime.InteropServices;
using Aurora.Core.Models;
using Aurora.Core.Primitives;

namespace Aurora.App.Host;

/// <summary>
/// Featherweight native Win32 DWM Live Thumbnail overlay (60 FPS GPU-composited, ~0% CPU, zero black boxes
/// on hardware-accelerated Chromium/Opera/Brave/video windows). Click-through and non-activating.
/// </summary>
public sealed class DwmLivePreviewForm : Form
{
    private const int WsExToolWindow = 0x00000080;
    private const int WsExTopMost = 0x00000008;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExTransparent = 0x00000020;

    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;
    private const int DwmwaBorderColor = 34;

    private const uint DwmTnpRectDestination = 0x00000001;
    private const uint DwmTnpOpacity = 0x00000004;
    private const uint DwmTnpVisible = 0x00000008;
    private const uint DwmTnpSourceClientAreaOnly = 0x00000010;

    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpHideWindow = 0x0080;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private static readonly nint HwndTopMost = new(-1);

    private nint _thumbHandle;
    private nint _currentSourceHwnd;
    private bool _isLightMode;

    public DwmLivePreviewForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(14, 21, 38);
        Size = new Size(1, 1);
        Location = new Point(-20000, -20000);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExToolWindow | WsExTopMost | WsExNoActivate | WsExTransparent;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyChromeAttributes();
    }

    public void SetLightMode(bool isLight)
    {
        if (_isLightMode == isLight && IsHandleCreated)
            return;
        _isLightMode = isLight;
        BackColor = isLight ? Color.FromArgb(241, 246, 253) : Color.FromArgb(12, 19, 36);
        if (IsHandleCreated)
            ApplyChromeAttributes();
    }

    public void ShowLivePreview(nint sourceHwnd, Rectangle anchorScreenRect, MonitorDescriptor monitor, bool isLightTheme)
    {
        if (sourceHwnd == 0 || !IsWindow(sourceHwnd) || IsIconic(sourceHwnd))
        {
            HidePreview();
            return;
        }

        if (!IsHandleCreated)
            CreateHandle();

        SetLightMode(isLightTheme);

        if (_currentSourceHwnd != sourceHwnd || _thumbHandle == 0)
        {
            UnregisterCurrentThumbnail();
            int hr = DwmRegisterThumbnail(Handle, sourceHwnd, out _thumbHandle);
            if (hr != 0 || _thumbHandle == 0)
            {
                HidePreview();
                return;
            }
            _currentSourceHwnd = sourceHwnd;
        }

        if (DwmQueryThumbnailSourceSize(_thumbHandle, out var srcSize) != 0 || srcSize.cx <= 0 || srcSize.cy <= 0)
        {
            HidePreview();
            return;
        }

        double scale = Math.Max(1.0, monitor.ScaleFactor);
        int pad = (int)Math.Round(7 * scale);
        int maxW = (int)Math.Round(304 * scale);
        int maxH = (int)Math.Round(182 * scale);

        double ratio = Math.Min((double)maxW / srcSize.cx, (double)maxH / srcSize.cy);
        int drawW = Math.Max((int)Math.Round(120 * scale), (int)Math.Round(srcSize.cx * ratio));
        int drawH = Math.Max((int)Math.Round(76 * scale), (int)Math.Round(srcSize.cy * ratio));

        int formW = drawW + (pad * 2);
        int formH = drawH + (pad * 2);

        // Position centered directly above the Radial Hub caption pill, clamped strictly inside the monitor
        var b = monitor.BoundsPx;
        int gap = (int)Math.Round(10 * scale);
        int targetX = anchorScreenRect.Left + ((anchorScreenRect.Width - formW) / 2);
        int targetY = anchorScreenRect.Top - formH - gap;

        if (targetY < b.Y + 12)
        {
            // Fallback beside the radial hub if near top edge
            targetY = Math.Max(b.Y + 12, anchorScreenRect.Top);
            int rightCand = anchorScreenRect.Right + gap;
            int leftCand = anchorScreenRect.Left - formW - gap;
            targetX = (rightCand + formW <= b.Right - 10) ? rightCand : Math.Max(b.X + 10, leftCand);
        }

        targetX = Math.Clamp(targetX, b.X + 10, Math.Max(b.X + 10, b.Right - formW - 10));
        targetY = Math.Clamp(targetY, b.Y + 10, Math.Max(b.Y + 10, b.Bottom - formH - 10));

        SetWindowPos(Handle, HwndTopMost, targetX, targetY, formW, formH, SwpNoActivate | SwpShowWindow);

        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DwmTnpRectDestination | DwmTnpVisible | DwmTnpOpacity | DwmTnpSourceClientAreaOnly,
            rcDestination = new RECT { Left = pad, Top = pad, Right = pad + drawW, Bottom = pad + drawH },
            opacity = 255,
            fVisible = true,
            fSourceClientAreaOnly = true
        };

        if (DwmUpdateThumbnailProperties(_thumbHandle, ref props) != 0)
        {
            HidePreview();
        }
    }

    public void HidePreview()
    {
        UnregisterCurrentThumbnail();
        if (IsHandleCreated)
        {
            SetWindowPos(Handle, 0, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpHideWindow);
        }
    }

    private void UnregisterCurrentThumbnail()
    {
        if (_thumbHandle != 0)
        {
            try { DwmUnregisterThumbnail(_thumbHandle); } catch { }
            _thumbHandle = 0;
        }
        _currentSourceHwnd = 0;
    }

    private void ApplyChromeAttributes()
    {
        try
        {
            int roundPref = DwmwcpRound;
            DwmSetWindowAttribute(Handle, DwmwaWindowCornerPreference, ref roundPref, sizeof(int));

            // COLORREF is 0x00BBGGRR (cyan accent border)
            uint borderColor = _isLightMode ? 0x00C78402u : 0x00F8BD38u;
            DwmSetWindowAttribute(Handle, DwmwaBorderColor, ref borderColor, sizeof(uint));
        }
        catch { }
    }

    protected override void Dispose(bool disposing)
    {
        UnregisterCurrentThumbnail();
        base.Dispose(disposing);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PSIZE
    {
        public int cx;
        public int cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_THUMBNAIL_PROPERTIES
    {
        public uint dwFlags;
        public RECT rcDestination;
        public RECT rcSource;
        public byte opacity;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fVisible;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fSourceClientAreaOnly;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmRegisterThumbnail(nint hwndDestination, nint hwndSource, out nint phThumbnailId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmUnregisterThumbnail(nint hThumbnailId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmQueryThumbnailSourceSize(nint hThumbnail, out PSIZE pSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmUpdateThumbnailProperties(nint hThumbnailId, ref DWM_THUMBNAIL_PROPERTIES ptnProperties);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref uint attrValue, int attrSize);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
