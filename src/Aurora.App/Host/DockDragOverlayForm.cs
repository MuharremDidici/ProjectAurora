using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Aurora.App.Host;

/// <summary>
/// Click-through, non-activating Aurora glass preview overlay shown while the user
/// drags the Dock with Alt+Left-Click (magnetic Left/Right/Bottom edge + offset preview)
/// or resizes the Dock with Alt+Right-Click (live scale HUD badge).
/// </summary>
public sealed class DockDragOverlayForm : Form
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
    private bool _isResizeMode;
    private string _targetEdge = "Bottom";
    private int _offsetPercent;
    private int _scalePercent = 100;
    private Point _cursorPx;
    private bool _isShowing;

    public DockDragOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(1, 2, 3);
        TransparencyKey = Color.FromArgb(1, 2, 3);
        Opacity = 0.88;
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

    public void UpdateMovePreview(Rectangle monitorBoundsPx, string targetEdge, int offsetPercent, int scalePercent)
    {
        bool changed = _monitorBounds != monitorBoundsPx ||
                       _isResizeMode ||
                       !string.Equals(_targetEdge, targetEdge, StringComparison.Ordinal) ||
                       _offsetPercent != offsetPercent ||
                       _scalePercent != scalePercent;

        _monitorBounds = monitorBoundsPx;
        _isResizeMode = false;
        _targetEdge = targetEdge;
        _offsetPercent = Math.Clamp(offsetPercent, -100, 100);
        _scalePercent = Math.Clamp(scalePercent, 75, 135);

        ShowAndRepaint(monitorBoundsPx, changed);
    }

    public void UpdateResizeHud(Rectangle monitorBoundsPx, string currentEdge, int scalePercent, Point cursorPx)
    {
        bool changed = _monitorBounds != monitorBoundsPx ||
                       !_isResizeMode ||
                       !string.Equals(_targetEdge, currentEdge, StringComparison.Ordinal) ||
                       _scalePercent != scalePercent ||
                       Math.Abs(_cursorPx.X - cursorPx.X) > 4 ||
                       Math.Abs(_cursorPx.Y - cursorPx.Y) > 4;

        _monitorBounds = monitorBoundsPx;
        _isResizeMode = true;
        _targetEdge = currentEdge;
        _scalePercent = Math.Clamp(scalePercent, 75, 135);
        _cursorPx = cursorPx;

        ShowAndRepaint(monitorBoundsPx, changed);
    }

    public void HideOverlay()
    {
        if (!_isShowing) return;
        _isShowing = false;
        if (IsHandleCreated)
            ShowWindow(Handle, SwHide);
    }

    private void ShowAndRepaint(Rectangle monitorBoundsPx, bool changed)
    {
        if (!_isShowing || Bounds != monitorBoundsPx)
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
        else if (changed)
        {
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(BackColor);

        if (Width <= 0 || Height <= 0)
            return;

        if (_isResizeMode)
        {
            PaintResizeBadge(g);
            return;
        }

        PaintMovePreview(g);
    }

    private void PaintMovePreview(Graphics g)
    {
        double scale = _scalePercent / 100.0;
        int pillThickness = (int)Math.Round(60 * scale);
        int hLength = Math.Min(Width - 96, (int)Math.Round(680 * scale));
        int vLength = Math.Min(Height - 120, (int)Math.Round(560 * scale));

        // 1. Subtle edge snap rails (Left / Right / Bottom) so drop zones are immediately legible
        DrawEdgeRail(g, new Rectangle(8, (Height - 240) / 2, 5, 240), _targetEdge == "Left");
        DrawEdgeRail(g, new Rectangle(Width - 13, (Height - 240) / 2, 5, 240), _targetEdge == "Right");
        DrawEdgeRail(g, new Rectangle((Width - 280) / 2, Height - 13, 280, 5), _targetEdge == "Bottom");

        // 2. Glowing glass Dock Ghost at target edge & offset
        Rectangle ghostRect;
        if (_targetEdge == "Left")
        {
            int maxTravel = Math.Max(0, (Height - vLength) / 2 - 24);
            int y = ((Height - vLength) / 2) + (int)Math.Round((_offsetPercent / 100.0) * maxTravel);
            ghostRect = new Rectangle(10, Math.Clamp(y, 18, Math.Max(18, Height - vLength - 18)), pillThickness, vLength);
        }
        else if (_targetEdge == "Right")
        {
            int maxTravel = Math.Max(0, (Height - vLength) / 2 - 24);
            int y = ((Height - vLength) / 2) + (int)Math.Round((_offsetPercent / 100.0) * maxTravel);
            ghostRect = new Rectangle(Width - pillThickness - 10, Math.Clamp(y, 18, Math.Max(18, Height - vLength - 18)), pillThickness, vLength);
        }
        else
        {
            int maxTravel = Math.Max(0, (Width - hLength) / 2 - 24);
            int x = ((Width - hLength) / 2) + (int)Math.Round((_offsetPercent / 100.0) * maxTravel);
            ghostRect = new Rectangle(Math.Clamp(x, 18, Math.Max(18, Width - hLength - 18)), Height - pillThickness - 10, hLength, pillThickness);
        }

        using (var path = CreateRoundRect(ghostRect, 22))
        using (var fill = new SolidBrush(Color.FromArgb(20, 44, 58)))
        using (var border = new Pen(Color.FromArgb(52, 211, 153), 2.8f))
        {
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }

        // Center magnet alignment tick when offset == 0
        if (_offsetPercent == 0)
        {
            using var magPen = new Pen(Color.FromArgb(180, 56, 189, 248), 2f) { DashStyle = DashStyle.Dash };
            if (_targetEdge is "Left" or "Right")
            {
                int cy = Height / 2;
                int x1 = _targetEdge == "Left" ? ghostRect.Right + 8 : ghostRect.Left - 38;
                g.DrawLine(magPen, x1, cy, x1 + 30, cy);
            }
            else
            {
                int cx = Width / 2;
                g.DrawLine(magPen, cx, ghostRect.Top - 32, cx, ghostRect.Top - 8);
            }
        }

        // 3. Crisp floating HUD pill describing edge + position
        string edgeTitle = _targetEdge == "Left" ? "Sol Dikey" : (_targetEdge == "Right" ? "Sağ Dikey" : "Alt Yatay");
        string posTitle = _offsetPercent == 0
            ? "Orta (Mıknatıs)"
            : (_offsetPercent > 0 ? $"+%{_offsetPercent}" : $"%{_offsetPercent}");
        string label = $"✦ Dock Taşı: {edgeTitle} • {posTitle}  (Bırakınca yerleşir)";

        int badgeCx = _targetEdge == "Left"
            ? Math.Min(Width - 220, ghostRect.Right + 200)
            : (_targetEdge == "Right"
                ? Math.Max(220, ghostRect.Left - 200)
                : ghostRect.Left + (ghostRect.Width / 2));
        int badgeCy = _targetEdge is "Left" or "Right"
            ? ghostRect.Top + (ghostRect.Height / 2)
            : Math.Max(40, ghostRect.Top - 34);

        DrawHudPill(g, label, badgeCx, badgeCy);
    }

    private void PaintResizeBadge(Graphics g)
    {
        string edgeTitle = _targetEdge == "Left" ? "Sol" : (_targetEdge == "Right" ? "Sağ" : "Alt");
        string label = $"⤢ Dock Boyutu ({edgeTitle}): %{_scalePercent}  •  Alt+Sağ Sürükle";

        int localX = Math.Clamp(_cursorPx.X - _monitorBounds.X, 180, Math.Max(180, Width - 180));
        int localY = Math.Clamp(_cursorPx.Y - _monitorBounds.Y - 46, 36, Math.Max(36, Height - 44));
        DrawHudPill(g, label, localX, localY);
    }

    private static void DrawEdgeRail(Graphics g, Rectangle rect, bool active)
    {
        using var path = CreateRoundRect(rect, 3);
        using var brush = new SolidBrush(active
            ? Color.FromArgb(52, 211, 153)
            : Color.FromArgb(55, 75, 105));
        g.FillPath(brush, path);
    }

    private void DrawHudPill(Graphics g, string text, int centerX, int centerY)
    {
        using var font = new Font("Segoe UI Semibold", 10f);
        var sz = g.MeasureString(text, font);
        int pw = (int)Math.Ceiling(sz.Width) + 30;
        int ph = 34;
        int px = Math.Clamp(centerX - (pw / 2), 14, Math.Max(14, Width - pw - 14));
        int py = Math.Clamp(centerY - (ph / 2), 14, Math.Max(14, Height - ph - 14));

        var rect = new Rectangle(px, py, pw, ph);
        using var path = CreateRoundRect(rect, 17);
        using var fill = new SolidBrush(Color.FromArgb(11, 18, 32));
        using var border = new Pen(Color.FromArgb(52, 211, 153), 1.6f);
        using var txt = new SolidBrush(Color.FromArgb(245, 250, 255));
        g.FillPath(fill, path);
        g.DrawPath(border, path);
        g.DrawString(text, font, txt, px + 15, py + 7);
    }

    private static GraphicsPath CreateRoundRect(Rectangle r, int radius)
    {
        int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) d = 2;
        var gp = new GraphicsPath();
        gp.AddArc(r.X, r.Y, d, d, 180, 90);
        gp.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        gp.CloseFigure();
        return gp;
    }
}
