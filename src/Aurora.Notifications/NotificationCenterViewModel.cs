using Aurora.Application.Services;
using Aurora.Core.Models;
using Aurora.Presentation;

namespace Aurora.Notifications;

public sealed record NotificationGroupViewModel(
    string GroupTitle,
    NotificationCategory Category,
    int Count,
    IReadOnlyList<AuroraNotification> Items);

public sealed class NotificationCenterViewModel(AuroraStateStore stateStore) : ObservableViewModel
{
    private IReadOnlyList<NotificationGroupViewModel> _groups = [];
    private bool _quietHoursEnabled;

    public IReadOnlyList<NotificationGroupViewModel> Groups
    {
        get => _groups;
        private set => SetProperty(ref _groups, value);
    }

    public bool QuietHoursEnabled
    {
        get => _quietHoursEnabled;
        private set => SetProperty(ref _quietHoursEnabled, value);
    }

    public bool CanUndoClearAll => stateStore.CanUndoClearNotifications;
    public int TotalActiveCount => stateStore.Notifications.Count;

    public void Refresh()
    {
        QuietHoursEnabled = stateStore.Preferences.Notifications.QuietHoursEnabled;
        var active = stateStore.Notifications;

        Groups = active
            .GroupBy(n => (n.Category, Label: FormatCategoryTitle(n.Category, n.SourceModule)))
            .Select(g => new NotificationGroupViewModel(
                GroupTitle: g.Key.Label,
                Category: g.Key.Category,
                Count: g.Count(),
                Items: g.OrderByDescending(x => x.CreatedAtUtc).ToList()))
            .ToList();

        OnPropertyChanged(nameof(CanUndoClearAll));
        OnPropertyChanged(nameof(TotalActiveCount));
    }

    public async Task ToggleQuietHoursAsync(CancellationToken ct = default)
    {
        bool next = !stateStore.Preferences.Notifications.QuietHoursEnabled;
        var updatedPrefs = stateStore.Preferences with
        {
            Notifications = stateStore.Preferences.Notifications with { QuietHoursEnabled = next }
        };
        await stateStore.UpdatePreferencesAsync(updatedPrefs, ct);
        Refresh();
    }

    public async Task DismissAsync(string id, CancellationToken ct = default)
    {
        await stateStore.DismissNotificationAsync(id, ct);
        Refresh();
    }

    public async Task ClearAllAsync(CancellationToken ct = default)
    {
        await stateStore.ClearAllNotificationsAsync(ct);
        Refresh();
    }

    public async Task UndoClearAllAsync(CancellationToken ct = default)
    {
        await stateStore.UndoClearAllNotificationsAsync(ct);
        Refresh();
    }

    public async Task CreateQuickReminderAsync(string title, string body, CancellationToken ct = default)
    {
        await stateStore.AddNotificationAsync(
            sourceModule: "Hızlı Hatırlatıcı",
            category: NotificationCategory.Reminder,
            severity: NotificationSeverity.Info,
            title: string.IsNullOrWhiteSpace(title) ? "Aurora Hatırlatıcısı" : title.Trim(),
            body: string.IsNullOrWhiteSpace(body) ? "Kullanıcı tarafından oluşturulan bildirim." : body.Trim(),
            showToast: true,
            ct: ct);
        Refresh();
    }

    private static string FormatCategoryTitle(NotificationCategory cat, string source) => cat switch
    {
        NotificationCategory.Workspace => $"Çalışma Alanı • {source}",
        NotificationCategory.Audio => $"Ses Sistemi • {source}",
        NotificationCategory.Reminder => $"Hatırlatıcılar • {source}",
        NotificationCategory.Security => $"Güvenlik • {source}",
        NotificationCategory.Recovery => $"Sistem ve Kurtarma • {source}",
        _ => $"Aurora Sistem • {source}"
    };
}
