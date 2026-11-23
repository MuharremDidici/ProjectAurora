using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Aurora.Platform.Windows.DisplayAndTaskbar;

namespace Aurora.App.Host;

/// <summary>
/// Click-through, non-activating translucent Shift+Drag Snap Zones overlay.
/// Renders glowing Aurora glass snap regions across the active monitor while
/// the user drags any window with Shift held, highlighting the target zone in real time.
/// </summary>
public sealed class SnapZonesOverlayForm : Form
{
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExTopMost = 0x00000008;
    private const int SwShowNoActivate = 4;
    private const int SwHide = 0;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    private Rectangle _monitorBounds;
    private IReadOnlyList<SnapZoneSpec> _zones = [];
    private int _hoveredZoneIndex = -1;
    private string _presetTitle = "Kodlama 65/35";
    private bool _isShowing;

    public SnapZonesOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(1, 2, 3);
        TransparencyKey = Color.FromArgb(1, 2, 3);
        Opacity = 0.80;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExLayered | WsExTransparent | WsExToolWindow | WsExNoActivate | WsExTopMost;
            return cp;
        }
    }

    public bool IsOverlayVisible => _isShowing;

    public SnapZoneSpec? ActiveHoveredZone =>
        _zones.FirstOrDefault(z => z.Index == _hoveredZoneIndex);

    public void UpdateZones(Rectangle monitorBoundsPx, IReadOnlyList<SnapZoneSpec> zones, string presetTitle, Point cursorPx)
    {
        bool boundsChanged = _monitorBounds != monitorBoundsPx;
        _monitorBounds = monitorBoundsPx;
        _zones = zones;
        _presetTitle = presetTitle;

        var hit = SnapZonesLayoutCatalog.HitTestZone(zones, cursorPx);
        int newHover = hit?.Index ?? -1;
        bool hoverChanged = newHover != _hoveredZoneIndex;
        _hoveredZoneIndex = newHover;

        if (boundsChanged || !_isShowing)
        {
            Bounds = monitorBoundsPx;
        }

        if (!_isShowing)
        {
            _isShowing = true;
            if (!IsHandleCreated) CreateHandle();
            ShowWindow(Handle, SwShowNoActivate);
            Invalidate();
        }
        else if (boundsChanged || hoverChanged)
        {
            Invalidate();
        }
    }

    public void HideOverlay()
    {
        if (!_isShowing)
            return;
        _isShowing = false;
        _hoveredZoneIndex = -1;
        if (IsHandleCreated)
            ShowWindow(Handle, SwHide);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(BackColor);

        if (_zones.Count == 0)
            return;

        // Paint each snap zone card relative to this monitor's top-left
        foreach (var z in _zones)
        {
            bool hovered = z.Index == _hoveredZoneIndex;
            var localRect = new Rectangle(
                z.BoundsPx.X - _monitorBounds.X,
                z.BoundsPx.Y - _monitorBounds.Y,
                z.BoundsPx.Width,
                z.BoundsPx.Height);

            using var path = CreateRoundRect(localRect, 18);
            using var fill = new SolidBrush(hovered
                ? Color.FromArgb(14, 116, 178)
                : Color.FromArgb(18, 28, 48));
            using var border = new Pen(hovered
                ? Color.FromArgb(125, 211, 252)
                : Color.FromArgb(90, 118, 158), hovered ? 3.2f : 1.6f);

            g.FillPath(fill, path);
            g.DrawPath(border, path);

            // Center number badge + zone label
            int cx = localRect.X + (localRect.Width / 2);
            int cy = localRect.Y + (localRect.Height / 2);

            var badgeRect = new Rectangle(cx - 26, cy - 36, 52, 52);
            using var badgeFill = new SolidBrush(hovered
                ? Color.FromArgb(240, 255, 255, 255)
                : Color.FromArgb(180, 15, 23, 42));
            using var badgeBorder = new Pen(Color.FromArgb(220, 56, 189, 248), 2f);
            g.FillEllipse(badgeFill, badgeRect);
            g.DrawEllipse(badgeBorder, badgeRect);

            using var numFont = new Font("Segoe UI Semibold", 16f, FontStyle.Bold);
            using var numBrush = new SolidBrush(hovered ? Color.FromArgb(12, 22, 42) : Color.White);
            var numStr = z.Index.ToString();
            var numSz = g.MeasureString(numStr, numFont);
            g.DrawString(numStr, numFont, numBrush, cx - (numSz.Width / 2f), cy - 10 - (numSz.Height / 2f));

            using var lblFont = new Font("Segoe UI Semibold", 11.5f);
            using var lblBrush = new SolidBrush(Color.FromArgb(245, 250, 255));
            var lblSz = g.MeasureString(z.Label, lblFont);
            g.DrawString(z.Label, lblFont, lblBrush, cx - (lblSz.Width / 2f), cy + 26);
        }

        // Top subtle banner indicating active preset
        string banner = $"✦ Shift + Bırak: {_presetTitle}";
        using var bFont = new Font("Segoe UI Semibold", 9.5f);
        var bSz = g.MeasureString(banner, bFont);
        int bw = (int)Math.Ceiling(bSz.Width) + 28;
        var bRect = new Rectangle((Width - bw) / 2, 16, bw, 30);
        using var bPath = CreateRoundRect(bRect, 15);
        using var bFill = new SolidBrush(Color.FromArgb(230, 11, 18, 34));
        using var bPen = new Pen(Color.FromArgb(190, 56, 189, 248), 1.2f);
        using var bTxt = new SolidBrush(Color.White);
        g.FillPath(bFill, bPath);
        g.DrawPath(bPen, bPath);
        g.DrawString(banner, bFont, bTxt, bRect.X + 14, bRect.Y + 6);
    }

    private static GraphicsPath CreateRoundRect(Rectangle r, int radius)
    {
        int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var gp = new GraphicsPath();
        gp.AddArc(r.X, r.Y, d, d, 180, 90);
        gp.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        gp.CloseFigure();
        return gp;
    }
}
