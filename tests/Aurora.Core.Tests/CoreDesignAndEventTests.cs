using Aurora.Core.Events;
using Aurora.Core.Primitives;
using Aurora.Core.Tokens;
using Xunit;

namespace Aurora.Core.Tests;

public sealed class CoreDesignAndEventTests
{
    [Fact]
    public void AllBuiltInThemes_MeetWcagAA_PrimaryTextAndFocusRingContrast()
    {
        foreach (var theme in BuiltInThemes.All)
        {
            bool textOk = ContrastCalculator.MeetsWcagAA(theme.Colors.TextPrimary, theme.Colors.Surface1);
            bool focusOk = ContrastCalculator.MeetsWcagAA(theme.Colors.FocusRing, theme.Colors.Surface1, largeText: true);

            Assert.True(textOk, $"Theme '{theme.Id}' failed WCAG AA primary text contrast.");
            Assert.True(focusOk, $"Theme '{theme.Id}' failed WCAG AA focus ring contrast.");
        }
    }

    [Theory]
    [InlineData(96u, 1.0, 100)]
    [InlineData(120u, 1.25, 125)]
    [InlineData(144u, 1.5, 150)]
    [InlineData(192u, 2.0, 200)]
    public void EffectiveDipConversion_ScalesAccuratelyAcrossDpiMatrix(uint dpi, double expectedScale, int expectedPercent)
    {
        var mon = new MonitorDescriptor(
            "DISPLAY1",
            "Primary",
            true,
            dpi,
            new PhysicalPixelRect(0, 0, (int)(1920 * expectedScale), (int)(1080 * expectedScale)),
            new PhysicalPixelRect(0, 0, (int)(1920 * expectedScale), (int)(1032 * expectedScale)));

        Assert.Equal(expectedScale, mon.ScaleFactor, 3);
        Assert.Equal(expectedPercent, mon.ScalePercent);
        Assert.Equal(1920, mon.BoundsDip.Width, 1);
        Assert.Equal(1080, mon.BoundsDip.Height, 1);

        var barDip = new EffectiveDipRect(10, 970, 1900, 50);
        var px = barDip.ToPhysicalPixels(mon.ScaleFactor);
        var roundTrip = EffectiveDipRect.FromPhysicalPixels(px, mon.ScaleFactor);

        Assert.InRange(Math.Abs(barDip.Width - roundTrip.Width), 0.0, 1.0);
        Assert.InRange(Math.Abs(barDip.Height - roundTrip.Height), 0.0, 1.0);
    }

    [Fact]
    public void BoundedEventBus_IsolatesSubscriberExceptions_AndCleansUpSubscriptions()
    {
        using var bus = new BoundedEventBus();
        int receivedCount = 0;

        using var badSub = bus.Subscribe<ThemeAppliedEvent>(_ => throw new InvalidOperationException("Simulated fault"));
        var goodSub = bus.Subscribe<ThemeAppliedEvent>(_ => receivedCount++);

        Assert.Equal(2, bus.GetSubscriberCount<ThemeAppliedEvent>());

        bus.Publish(new ThemeAppliedEvent(BuiltInThemes.NebulaDark, DateTimeOffset.UtcNow));
        Assert.Equal(1, receivedCount);

        goodSub.Dispose();
        Assert.Equal(1, bus.GetSubscriberCount<ThemeAppliedEvent>());
    }
}
