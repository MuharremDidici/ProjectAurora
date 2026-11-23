using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Aurora.App.Host;

/// <summary>
/// Click-through, non-activating, zero-interference outer border frame drawn around the active
/// GIF capture region (3 px OUTSIDE the captured area so the border itself never appears inside the GIF!).
/// </summary>
public sealed class GifRecordingBorderForm : Form
{
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int Pad = 4;

    private static readonly Color ChromaKey = Color.FromArgb(1, 2, 3);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(nint hWnd, uint dwAffinity);

    private const uint WdaExcludeFromCapture = 0x00000011;

    public GifRecordingBorderForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = ChromaKey;
        TransparencyKey = ChromaKey;
        DoubleBuffered = true;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExLayered | WsExTransparent | WsExToolWindow | WsExNoActivate;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    public void ShowAroundRegion(Rectangle captureRect)
    {
        Bounds = new Rectangle(
            captureRect.Left - Pad,
            captureRect.Top - Pad,
            captureRect.Width + (Pad * 2),
            captureRect.Height + (Pad * 2));

        if (!Visible) Show();
        try { SetWindowDisplayAffinity(Handle, WdaExcludeFromCapture); } catch { }
        Invalidate();
    }

    public void HideBorder()
    {
        if (Visible) Hide();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(ChromaKey);
        using var outerPen = new Pen(Color.FromArgb(235, 251, 113, 133), 2.5f)
        {
            DashStyle = DashStyle.Dash,
            DashPattern = [6f, 4f]
        };
        g.DrawRectangle(outerPen, 1, 1, Width - 3, Height - 3);
    }
}
