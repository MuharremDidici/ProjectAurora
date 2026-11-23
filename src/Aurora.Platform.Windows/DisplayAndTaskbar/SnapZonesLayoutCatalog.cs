using System.Drawing;
using System.Runtime.InteropServices;

namespace Aurora.Platform.Windows.DisplayAndTaskbar;

public sealed record SnapZoneSpec(int Index, string Label, Rectangle BoundsPx);

public sealed record SnapZonePresetInfo(string Id, string DisplayName, string Description);

/// <summary>
/// Pure geometry engine + DWM invisible-border-compensated Win32 window snapper
/// for interactive Shift+Drag Snap Zones across any monitor resolution and DPI.
/// </summary>
public static class SnapZonesLayoutCatalog
{
    private const int SwRestore = 9;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const int DwmwaExtendedFrameBounds = 9;

    public static readonly IReadOnlyList<SnapZonePresetInfo> AllPresets =
    [
        new("code6535", "Kodlama 65/35", "Sol ana editör %65, sağ yardımcı %35"),
        new("studio3", "Üçlü Stüdyo", "Sol %50, sağ üst %25, sağ alt %25"),
        new("cols3", "3 Sütun Eşit", "Geniş ekranlar için 3 eşit dikey şerit"),
        new("grid2x2", "2×2 Izgara", "4 eşit köşe penceresi")
    ];

    public static IReadOnlyList<SnapZoneSpec> ComputeZones(Rectangle workArea, string presetId, int gapPx = 10)
    {
        int x = workArea.Left + gapPx;
        int y = workArea.Top + gapPx;
        int w = Math.Max(320, workArea.Width - (gapPx * 2));
        int h = Math.Max(240, workArea.Height - (gapPx * 2));

        switch ((presetId ?? "code6535").Trim().ToLowerInvariant())
        {
            case "studio3":
                {
                    int leftW = (w - gapPx) / 2;
                    int rightW = w - leftW - gapPx;
                    int topH = (h - gapPx) / 2;
                    int botH = h - topH - gapPx;
                    return
                    [
                        new(1, "Ana Çalışma (%50)", new Rectangle(x, y, leftW, h)),
                        new(2, "Sağ Üst (%25)", new Rectangle(x + leftW + gapPx, y, rightW, topH)),
                        new(3, "Sağ Alt (%25)", new Rectangle(x + leftW + gapPx, y + topH + gapPx, rightW, botH))
                    ];
                }

            case "cols3":
                {
                    int c1 = (w - (gapPx * 2)) / 3;
                    int c2 = (w - (gapPx * 2)) / 3;
                    int c3 = w - c1 - c2 - (gapPx * 2);
                    return
                    [
                        new(1, "Sol Sütun", new Rectangle(x, y, c1, h)),
                        new(2, "Orta Odak", new Rectangle(x + c1 + gapPx, y, c2, h)),
                        new(3, "Sağ Sütun", new Rectangle(x + c1 + c2 + (gapPx * 2), y, c3, h))
                    ];
                }

            case "grid2x2":
                {
                    int halfW = (w - gapPx) / 2;
                    int rightW = w - halfW - gapPx;
                    int halfH = (h - gapPx) / 2;
                    int botH = h - halfH - gapPx;
                    return
                    [
                        new(1, "Sol Üst", new Rectangle(x, y, halfW, halfH)),
                        new(2, "Sağ Üst", new Rectangle(x + halfW + gapPx, y, rightW, halfH)),
                        new(3, "Sol Alt", new Rectangle(x, y + halfH + gapPx, halfW, botH)),
                        new(4, "Sağ Alt", new Rectangle(x + halfW + gapPx, y + halfH + gapPx, rightW, botH))
                    ];
                }

            default: // "code6535"
                {
                    int leftW = (int)Math.Round((w - gapPx) * 0.65);
                    int rightW = w - leftW - gapPx;
                    return
                    [
                        new(1, "Ana Bölge (%65)", new Rectangle(x, y, leftW, h)),
                        new(2, "Yan Panel (%35)", new Rectangle(x + leftW + gapPx, y, rightW, h))
                    ];
                }
        }
    }

    public static SnapZoneSpec? HitTestZone(IReadOnlyList<SnapZoneSpec> zones, Point cursorPx)
    {
        foreach (var z in zones)
        {
            if (z.BoundsPx.Contains(cursorPx))
                return z;
        }
        return null;
    }

    /// <summary>
    /// Snaps a top-level window into <paramref name="targetVisualRect"/> with DWM
    /// invisible shadow-border compensation so adjacent windows align flush with exact gaps.
    /// </summary>
    public static bool SnapWindowToRectExact(nint hwnd, Rectangle targetVisualRect)
    {
        if (hwnd == 0 || !IsWindow(hwnd))
            return false;

        try
        {
            if (IsZoomed(hwnd) || IsIconic(hwnd))
                ShowWindow(hwnd, SwRestore);

            int padLeft = 0, padTop = 0, padRight = 0, padBottom = 0;
            if (GetWindowRect(hwnd, out var winRect) &&
                DwmGetWindowAttribute(hwnd, DwmwaExtendedFrameBounds, out var extRect, Marshal.SizeOf<RECT>()) == 0)
            {
                padLeft = Math.Clamp(extRect.Left - winRect.Left, 0, 24);
                padTop = Math.Clamp(extRect.Top - winRect.Top, 0, 24);
                padRight = Math.Clamp(winRect.Right - extRect.Right, 0, 24);
                padBottom = Math.Clamp(winRect.Bottom - extRect.Bottom, 0, 24);
            }

            int finalX = targetVisualRect.Left - padLeft;
            int finalY = targetVisualRect.Top - padTop;
            int finalW = targetVisualRect.Width + padLeft + padRight;
            int finalH = targetVisualRect.Height + padTop + padBottom;

            return SetWindowPos(hwnd, 0, finalX, finalY, finalW, finalH, SwpNoZOrder | SwpNoActivate);
        }
        catch
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);
}
