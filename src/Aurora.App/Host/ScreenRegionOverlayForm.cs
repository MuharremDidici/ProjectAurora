using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Aurora.Platform.Windows.ShellExtras;

namespace Aurora.App.Host;

public sealed record ColorPickOutcome(PickedColorInfo ColorInfo, bool CopyRgb);

/// <summary>
/// Flicker-free, double-buffered virtual-desktop overlay spanning ALL connected monitors.
/// Supports two executive tool modes:
///   1) OCR Region Snip (freezes screen, marquee selection, returns cropped Bitmap)
///   2) Live Color Eyedropper (11x11 pixel magnifier loupe + HEX/RGB/HSL live readout)
/// </summary>
public sealed class ScreenRegionOverlayForm : Form
{
    private enum ToolMode { OcrRegion, ColorPicker }

    private readonly ToolMode _mode;
    private readonly Rectangle _virtualBounds;
    private readonly Bitmap _frozenDesktop;

    private bool _isDragging;
    private Point _dragStartLocal;
    private Point _cursorLocal;
    private Rectangle _selectionLocal;

    public Bitmap? CapturedCrop { get; private set; }
    public ColorPickOutcome? PickedColor { get; private set; }

    private ScreenRegionOverlayForm(ToolMode mode, Rectangle virtualBounds, Bitmap frozenDesktop)
    {
        _mode = mode;
        _virtualBounds = virtualBounds;
        _frozenDesktop = frozenDesktop;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = virtualBounds;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        KeyPreview = true;
        Cursor = Cursors.Cross;

        var cur = Cursor.Position;
        _cursorLocal = new Point(cur.X - virtualBounds.X, cur.Y - virtualBounds.Y);
    }

    public static Bitmap? CaptureOcrRegion()
    {
        var bounds = SystemInformation.VirtualScreen;
        using var snap = CaptureVirtualDesktop(bounds);
        if (snap is null) return null;

        using var overlay = new ScreenRegionOverlayForm(ToolMode.OcrRegion, bounds, snap);
        return overlay.ShowDialog() == DialogResult.OK ? overlay.CapturedCrop : null;
    }

    public static ColorPickOutcome? PickColorFromScreen()
    {
        var bounds = SystemInformation.VirtualScreen;
        using var snap = CaptureVirtualDesktop(bounds);
        if (snap is null) return null;

        using var overlay = new ScreenRegionOverlayForm(ToolMode.ColorPicker, bounds, snap);
        return overlay.ShowDialog() == DialogResult.OK ? overlay.PickedColor : null;
    }

    private static Bitmap? CaptureVirtualDesktop(Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return null;
        try
        {
            var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            if (_mode == ToolMode.ColorPicker)
            {
                var px = SamplePixelSafe(e.Location);
                bool wantRgb = (ModifierKeys & Keys.Shift) == Keys.Shift;
                PickedColor = new ColorPickOutcome(ScreenOcrService.DescribeColor(px), wantRgb);
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            _isDragging = true;
            _dragStartLocal = e.Location;
            _cursorLocal = e.Location;
            _selectionLocal = new Rectangle(e.X, e.Y, 0, 0);
            Invalidate();
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        _cursorLocal = e.Location;
        if (_mode == ToolMode.OcrRegion && _isDragging)
        {
            int x = Math.Min(_dragStartLocal.X, e.X);
            int y = Math.Min(_dragStartLocal.Y, e.Y);
            int w = Math.Abs(e.X - _dragStartLocal.X);
            int h = Math.Abs(e.Y - _dragStartLocal.Y);
            _selectionLocal = new Rectangle(x, y, w, h);
        }
        Invalidate();
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_mode == ToolMode.OcrRegion && _isDragging && e.Button == MouseButtons.Left)
        {
            _isDragging = false;
            var rect = Rectangle.Intersect(_selectionLocal, new Rectangle(0, 0, _frozenDesktop.Width, _frozenDesktop.Height));
            if (rect.Width >= 8 && rect.Height >= 8)
            {
                var crop = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(crop))
                {
                    g.DrawImage(_frozenDesktop, new Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);
                }
                CapturedCrop = crop;
                DialogResult = DialogResult.OK;
            }
            else
            {
                DialogResult = DialogResult.Cancel;
            }
            Close();
        }
        base.OnMouseUp(e);
    }

    private Color SamplePixelSafe(Point localPt)
    {
        int x = Math.Clamp(localPt.X, 0, _frozenDesktop.Width - 1);
        int y = Math.Clamp(localPt.Y, 0, _frozenDesktop.Height - 1);
        return _frozenDesktop.GetPixel(x, y);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // 1. Paint frozen desktop 1:1
        g.DrawImageUnscaled(_frozenDesktop, 0, 0);

        if (_mode == ToolMode.OcrRegion)
        {
            PaintOcrHud(g);
        }
        else
        {
            PaintColorLoupeHud(g);
        }
    }

    private void PaintOcrHud(Graphics g)
    {
        using var dimBrush = new SolidBrush(Color.FromArgb(118, 6, 10, 20));
        if (_selectionLocal.Width > 2 && _selectionLocal.Height > 2)
        {
            // Dim everything OUTSIDE the selection rectangle so selected text stays razor-sharp
            using var region = new Region(new Rectangle(0, 0, Width, Height));
            region.Exclude(_selectionLocal);
            g.FillRegion(dimBrush, region);

            using var glowPen = new Pen(Color.FromArgb(95, 56, 189, 248), 5f);
            using var borderPen = new Pen(Color.FromArgb(255, 56, 189, 248), 2f);
            g.DrawRectangle(glowPen, _selectionLocal);
            g.DrawRectangle(borderPen, _selectionLocal);

            string dimText = $"{_selectionLocal.Width} × {_selectionLocal.Height} px  •  Bırakınca OCR okur";
            DrawPillBadge(g, dimText, _selectionLocal.Left, Math.Max(14, _selectionLocal.Top - 34));
        }
        else
        {
            g.FillRectangle(dimBrush, 0, 0, Width, Height);
        }

        // Top instruction banner centered on active monitor under cursor
        var activeScreen = Screen.FromPoint(new Point(_cursorLocal.X + _virtualBounds.X, _cursorLocal.Y + _virtualBounds.Y));
        int bannerCenterX = activeScreen.Bounds.Left - _virtualBounds.X + (activeScreen.Bounds.Width / 2);
        int bannerY = activeScreen.Bounds.Top - _virtualBounds.Y + 26;
        DrawCenteredBanner(g, "✦ Aurora OCR Metin Yakalayıcı — Okumak istediğiniz alanı sürükleyip seçin  •  ESC iptal", bannerCenterX, bannerY);
    }

    private void PaintColorLoupeHud(Graphics g)
    {
        var activeScreen = Screen.FromPoint(new Point(_cursorLocal.X + _virtualBounds.X, _cursorLocal.Y + _virtualBounds.Y));
        int bannerCenterX = activeScreen.Bounds.Left - _virtualBounds.X + (activeScreen.Bounds.Width / 2);
        int bannerY = activeScreen.Bounds.Top - _virtualBounds.Y + 26;
        DrawCenteredBanner(g, "✦ Aurora Renk Damlalığı — Sol Tık: HEX Kopyala  •  Shift+Tık: RGB  •  Sağ Tık / ESC: İptal", bannerCenterX, bannerY);

        var pxColor = SamplePixelSafe(_cursorLocal);
        var info = ScreenOcrService.DescribeColor(pxColor);

        const int gridCells = 11; // 11x11 odd grid -> exact center pixel
        const int cellPx = 12;
        const int loupeSize = gridCells * cellPx; // 132px
        const int cardW = 198;
        const int cardH = loupeSize + 64;

        // Place loupe cleanly beside cursor, flipping automatically near edges
        int screenRightLocal = activeScreen.Bounds.Right - _virtualBounds.X;
        int screenBottomLocal = activeScreen.Bounds.Bottom - _virtualBounds.Y;
        int lx = _cursorLocal.X + 26;
        int ly = _cursorLocal.Y + 26;
        if (lx + cardW + 14 > screenRightLocal) lx = _cursorLocal.X - cardW - 26;
        if (ly + cardH + 14 > screenBottomLocal) ly = _cursorLocal.Y - cardH - 26;

        var cardRect = new Rectangle(lx, ly, cardW, cardH);
        using (var path = CreateRoundRect(cardRect, 14))
        using (var bg = new SolidBrush(Color.FromArgb(242, 11, 17, 32)))
        using (var border = new Pen(Color.FromArgb(170, 56, 189, 248), 1.5f))
        {
            g.FillPath(bg, path);
            g.DrawPath(border, path);
        }

        // Magnified 11x11 pixel grid
        int gx = lx + (cardW - loupeSize) / 2;
        int gy = ly + 10;
        var gridRect = new Rectangle(gx, gy, loupeSize, loupeSize);
        int half = gridCells / 2;

        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        for (int dy = -half; dy <= half; dy++)
        {
            for (int dx = -half; dx <= half; dx++)
            {
                var c = SamplePixelSafe(new Point(_cursorLocal.X + dx, _cursorLocal.Y + dy));
                using var b = new SolidBrush(c);
                g.FillRectangle(b, gx + ((dx + half) * cellPx), gy + ((dy + half) * cellPx), cellPx, cellPx);
            }
        }

        // Subtle pixel gridlines + center pixel highlight box
        using (var gridPen = new Pen(Color.FromArgb(45, 255, 255, 255), 1f))
        {
            for (int i = 1; i < gridCells; i++)
            {
                g.DrawLine(gridPen, gx + (i * cellPx), gy, gx + (i * cellPx), gy + loupeSize);
                g.DrawLine(gridPen, gx, gy + (i * cellPx), gx + loupeSize, gy + (i * cellPx));
            }
            g.DrawRectangle(new Pen(Color.FromArgb(140, 255, 255, 255), 1f), gridRect);
        }

        var centerBox = new Rectangle(gx + (half * cellPx), gy + (half * cellPx), cellPx, cellPx);
        using (var outerPen = new Pen(Color.Black, 2f))
        using (var innerPen = new Pen(Color.White, 1.2f))
        {
            g.DrawRectangle(outerPen, centerBox);
            g.DrawRectangle(innerPen, Rectangle.Inflate(centerBox, -1, -1));
        }

        // Swatch + HEX + RGB + HSL readout
        int textTop = gy + loupeSize + 10;
        var swatchRect = new Rectangle(lx + 12, textTop + 2, 34, 34);
        using (var swPath = CreateRoundRect(swatchRect, 8))
        using (var swBrush = new SolidBrush(pxColor))
        using (var swPen = new Pen(Color.FromArgb(190, 255, 255, 255), 1.5f))
        {
            g.FillPath(swBrush, swPath);
            g.DrawPath(swPen, swPath);
        }

        using var hexFont = new Font("Segoe UI Semibold", 11.5f, FontStyle.Bold);
        using var subFont = new Font("Segoe UI", 8.2f, FontStyle.Regular);
        using var whiteBrush = new SolidBrush(Color.FromArgb(245, 248, 252));
        using var mutedBrush = new SolidBrush(Color.FromArgb(165, 184, 210));

        g.DrawString(info.Hex, hexFont, whiteBrush, lx + 54, textTop - 1);
        g.DrawString(info.Rgb, subFont, mutedBrush, lx + 54, textTop + 18);
        g.DrawString(info.Hsl, subFont, mutedBrush, lx + 54, textTop + 31);
    }

    private static void DrawCenteredBanner(Graphics g, string text, int centerX, int y)
    {
        using var font = new Font("Segoe UI Semibold", 10f);
        var sz = g.MeasureString(text, font);
        int w = (int)Math.Ceiling(sz.Width) + 30;
        int h = 34;
        var rect = new Rectangle(centerX - (w / 2), y, w, h);

        using var path = CreateRoundRect(rect, 17);
        using var bg = new SolidBrush(Color.FromArgb(232, 10, 16, 30));
        using var border = new Pen(Color.FromArgb(165, 56, 189, 248), 1.2f);
        using var txtBrush = new SolidBrush(Color.FromArgb(244, 248, 255));
        g.FillPath(bg, path);
        g.DrawPath(border, path);
        g.DrawString(text, font, txtBrush, rect.X + 15, rect.Y + 8);
    }

    private static void DrawPillBadge(Graphics g, string text, int x, int y)
    {
        using var font = new Font("Segoe UI Semibold", 9f);
        var sz = g.MeasureString(text, font);
        var rect = new Rectangle(x, y, (int)Math.Ceiling(sz.Width) + 18, 26);
        using var path = CreateRoundRect(rect, 13);
        using var bg = new SolidBrush(Color.FromArgb(235, 12, 18, 34));
        using var border = new Pen(Color.FromArgb(190, 56, 189, 248), 1f);
        using var txtBrush = new SolidBrush(Color.FromArgb(240, 247, 255));
        g.FillPath(bg, path);
        g.DrawPath(border, path);
        g.DrawString(text, font, txtBrush, rect.X + 9, rect.Y + 5);
    }

    private static GraphicsPath CreateRoundRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var gp = new GraphicsPath();
        gp.AddArc(r.X, r.Y, d, d, 180, 90);
        gp.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        gp.CloseFigure();
        return gp;
    }
}
