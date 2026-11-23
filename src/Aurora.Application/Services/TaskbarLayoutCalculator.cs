using Aurora.Core.Models;
using Aurora.Core.Primitives;

namespace Aurora.Application.Services;

public static class TaskbarLayoutCalculator
{
    public static EffectiveDipRect CalculateTaskbarBoundsDip(
        MonitorDescriptor monitor,
        TaskbarSettings settings,
        NativeTaskbarStatus nativeStatus)
    {
        var thickness = settings.BarThicknessDip;
        var margin = settings.FloatingDockStyle ? 10.0 : 0.0;

        var area = (nativeStatus.CoexistenceActive && nativeStatus.Edge == settings.Edge)
            ? monitor.WorkAreaDip
            : monitor.BoundsDip;

        return settings.Edge switch
        {
            TaskbarEdge.Top => new EffectiveDipRect(
                X: area.X + margin,
                Y: area.Y + (settings.FloatingDockStyle ? 6.0 : 0.0),
                Width: Math.Max(320.0, area.Width - (margin * 2.0)),
                Height: thickness),

            TaskbarEdge.Left => new EffectiveDipRect(
                X: area.X + (settings.FloatingDockStyle ? 6.0 : 0.0),
                Y: area.Y + margin,
                Width: thickness + 8.0,
                Height: Math.Max(280.0, area.Height - (margin * 2.0))),

            TaskbarEdge.Right => new EffectiveDipRect(
                X: area.Right - (thickness + 8.0) - (settings.FloatingDockStyle ? 6.0 : 0.0),
                Y: area.Y + margin,
                Width: thickness + 8.0,
                Height: Math.Max(280.0, area.Height - (margin * 2.0))),

            _ => new EffectiveDipRect(
                X: area.X + margin,
                Y: area.Bottom - thickness - (settings.FloatingDockStyle ? 6.0 : 0.0),
                Width: Math.Max(320.0, area.Width - (margin * 2.0)),
                Height: thickness)
        };
    }

    public static EffectiveDipRect ClampFlyoutToWorkAreaDip(
        MonitorDescriptor monitor,
        EffectiveDipRect taskbarBoundsDip,
        TaskbarEdge edge,
        double flyoutWidthDip,
        double flyoutHeightDip,
        double anchorCenterXDip)
    {
        var work = monitor.WorkAreaDip;
        const double gap = 10.0;

        var width = Math.Min(flyoutWidthDip, Math.Max(260.0, work.Width - (gap * 2)));
        var height = Math.Min(flyoutHeightDip, Math.Max(240.0, work.Height - (gap * 2)));

        var targetX = anchorCenterXDip - (width / 2.0);
        var clampedX = Math.Clamp(targetX, work.X + gap, Math.Max(work.X + gap, work.Right - width - gap));

        double targetY = edge switch
        {
            TaskbarEdge.Top => taskbarBoundsDip.Bottom + gap,
            _ => taskbarBoundsDip.Y - height - gap
        };

        var clampedY = Math.Clamp(targetY, work.Y + gap, Math.Max(work.Y + gap, work.Bottom - height - gap));
        return new EffectiveDipRect(clampedX, clampedY, width, height);
    }
}
