using System.Globalization;
using Aurora.Application.Services;
using Aurora.Core.Contracts;
using Aurora.Core.Models;
using Aurora.Presentation;

namespace Aurora.Widgets;

public sealed record CalendarDayCell(int DayNumber, bool IsCurrentMonth, bool IsToday);

public sealed class WidgetBoardViewModel(
    AuroraStateStore stateStore,
    ISystemMetricsService metricsService) : ObservableViewModel
{
    private SystemMetricsSnapshot _metrics = new(0, 0, 0, 16, 0, false, "GPU", 0, 0, DateTimeOffset.UtcNow);
    private WeatherSnapshot _weather = new(false, "İstanbul", 19.0, "Çevrimdışı Mod", 58, 12.0, "Çevrimdışı Yerel Mod", DateTimeOffset.UtcNow);
    private bool _isSurfaceVisible;
    private DateTime _displayedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public IReadOnlyList<WidgetInstanceConfig> AllWidgets =>
        stateStore.ActiveProfile.Widgets.OrderBy(w => w.OrderIndex).ToList();

    public IReadOnlyList<WidgetInstanceConfig> VisibleWidgets =>
        stateStore.ActiveProfile.Widgets.Where(w => w.IsVisible).OrderBy(w => w.OrderIndex).ToList();

    public SystemMetricsSnapshot Metrics
    {
        get => _metrics;
        private set => SetProperty(ref _metrics, value);
    }

    public WeatherSnapshot Weather
    {
        get => _weather;
        private set => SetProperty(ref _weather, value);
    }

    public bool IsSurfaceVisible
    {
        get => _isSurfaceVisible;
        set => SetProperty(ref _isSurfaceVisible, value);
    }

    public string CurrentMonthHeader => _displayedMonth.ToString("MMMM yyyy", new CultureInfo("tr-TR"));

    public async Task RefreshTelemetryIfVisibleAsync(bool forceWeather = false, CancellationToken ct = default)
    {
        if (!IsSurfaceVisible && !forceWeather)
            return; // Pause sampling when hidden per TDD §10 & WG-004

        Metrics = metricsService.SampleMetrics();

        if (forceWeather || (DateTimeOffset.UtcNow - Weather.UpdatedAtUtc).TotalMinutes >= 15)
        {
            var prefs = stateStore.Preferences;
            Weather = await metricsService.GetWeatherAsync(
                prefs.WeatherCityName,
                prefs.WeatherLatitude,
                prefs.WeatherLongitude,
                prefs.WeatherNetworkOptIn,
                ct);
        }

        OnPropertyChanged(nameof(AllWidgets));
        OnPropertyChanged(nameof(VisibleWidgets));
    }

    public IReadOnlyList<CalendarDayCell> BuildCalendarGrid()
    {
        var cells = new List<CalendarDayCell>(42);
        var firstDay = new DateTime(_displayedMonth.Year, _displayedMonth.Month, 1);
        int offset = ((int)firstDay.DayOfWeek + 6) % 7; // Monday = 0
        var startDate = firstDay.AddDays(-offset);
        var today = DateTime.Today;

        for (int i = 0; i < 42; i++)
        {
            var d = startDate.AddDays(i);
            cells.Add(new CalendarDayCell(
                DayNumber: d.Day,
                IsCurrentMonth: d.Month == _displayedMonth.Month,
                IsToday: d.Date == today));
        }

        return cells;
    }

    public void NavigateCalendarMonth(int deltaMonths)
    {
        _displayedMonth = _displayedMonth.AddMonths(deltaMonths);
        OnPropertyChanged(nameof(CurrentMonthHeader));
    }

    public async Task CycleWidgetSizeAsync(string widgetId, CancellationToken ct = default)
    {
        var updated = stateStore.ActiveProfile.Widgets.Select(w =>
        {
            if (w.Id != widgetId)
                return w;
            var next = w.Size switch
            {
                WidgetSizeClass.Small => WidgetSizeClass.Medium,
                WidgetSizeClass.Medium => WidgetSizeClass.Large,
                _ => WidgetSizeClass.Small
            };
            return w with { Size = next };
        }).ToList();

        await stateStore.UpdateWidgetsAsync(updated, ct);
        OnPropertyChanged(nameof(AllWidgets));
        OnPropertyChanged(nameof(VisibleWidgets));
    }

    public async Task SetWidgetVisibilityAsync(string widgetId, bool visible, CancellationToken ct = default)
    {
        var updated = stateStore.ActiveProfile.Widgets
            .Select(w => w.Id == widgetId ? w with { IsVisible = visible } : w)
            .ToList();

        await stateStore.UpdateWidgetsAsync(updated, ct);
        OnPropertyChanged(nameof(AllWidgets));
        OnPropertyChanged(nameof(VisibleWidgets));
    }

    public async Task MoveWidgetOrderAsync(string widgetId, int delta, CancellationToken ct = default)
    {
        var list = stateStore.ActiveProfile.Widgets.OrderBy(w => w.OrderIndex).ToList();
        var idx = list.FindIndex(w => w.Id == widgetId);
        if (idx < 0)
            return;

        var target = Math.Clamp(idx + delta, 0, list.Count - 1);
        var item = list[idx];
        list.RemoveAt(idx);
        list.Insert(target, item);
        for (int i = 0; i < list.Count; i++)
            list[i] = list[i] with { OrderIndex = i };

        await stateStore.UpdateWidgetsAsync(list, ct);
        OnPropertyChanged(nameof(AllWidgets));
        OnPropertyChanged(nameof(VisibleWidgets));
    }
}
