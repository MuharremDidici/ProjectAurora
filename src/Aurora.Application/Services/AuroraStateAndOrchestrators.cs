using Aurora.Core.Contracts;
using Aurora.Core.Events;
using Aurora.Core.Models;
using Aurora.Core.Primitives;
using Aurora.Core.Tokens;

namespace Aurora.Application.Services;

public sealed class AuroraStateStore(
    IAuroraRepository repository,
    IAuroraEventBus eventBus,
    IAuroraLogger logger)
{
    private readonly Lock _sync = new();
    private List<AuroraThemeTokens> _customThemes = [];
    private List<WorkspaceProfile> _profiles = [.. DefaultCatalogFactory.CreateDefaultProfiles()];
    private List<PinnedShortcut> _pinnedShortcuts = [.. DefaultCatalogFactory.CreateDefaultPinnedShortcuts()];
    private List<AuroraNotification> _notifications = [];
    private List<AuroraNotification> _lastClearedBackup = [];

    public UserPreferences Preferences { get; private set; } = UserPreferences.Default;
    public AuroraThemeTokens CommittedTheme { get; private set; } = BuiltInThemes.AuroraGlassDark;
    public AuroraThemeTokens? PreviewTheme { get; private set; }
    public AuroraThemeTokens EffectiveTheme => IsSafeMode ? BuiltInThemes.AuroraGlassDark : (PreviewTheme ?? CommittedTheme);
    public bool IsPreviewActive => PreviewTheme is not null;
    public bool IsSafeMode { get; private set; }

    public WorkspaceProfile ActiveProfile { get; private set; } = DefaultCatalogFactory.CreateDefaultProfiles()[0];
    public IReadOnlyList<WorkspaceProfile> Profiles => _profiles.AsReadOnly();
    public IReadOnlyList<PinnedShortcut> PinnedShortcuts => _pinnedShortcuts.AsReadOnly();
    public IReadOnlyList<AuroraNotification> Notifications => _notifications.Where(n => !n.IsDismissed).OrderByDescending(n => n.CreatedAtUtc).ToList();
    public IReadOnlyList<AuroraThemeTokens> AllThemes => [.. BuiltInThemes.All, .. _customThemes];
    public bool CanUndoClearNotifications => _lastClearedBackup.Count > 0;

    public async Task InitializeAsync(bool safeMode, CancellationToken ct = default)
    {
        IsSafeMode = safeMode;
        await repository.InitializeAsync(ct);

        Preferences = await repository.LoadPreferencesAsync(ct);
        var loadedPins = await repository.LoadPinnedShortcutsAsync(ct);
        if (loadedPins.Count > 0)
            _pinnedShortcuts = [.. loadedPins.OrderBy(p => p.OrderIndex)];

        var loadedProfiles = await repository.LoadProfilesAsync(ct);
        if (loadedProfiles.Count > 0)
            _profiles = [.. loadedProfiles];

        _customThemes = [.. await repository.LoadCustomThemesAsync(ct)];
        _notifications = [.. await repository.LoadNotificationsAsync(ct)];
        PruneExpiredNotifications();

        ActiveProfile = _profiles.FirstOrDefault(p => p.Id == Preferences.ActiveProfileId) ?? _profiles[0];
        CommittedTheme = ResolveThemeById(safeMode ? BuiltInThemes.AuroraGlassDark.Id : Preferences.ActiveThemeId);

        if (_notifications.Count == 0)
        {
            await AddNotificationAsync(
                sourceModule: "Aurora Başlangıç",
                category: NotificationCategory.System,
                severity: NotificationSeverity.Info,
                title: "Project Aurora Hazır",
                body: "Akıllı Görev Çubuğu, Kontrol Merkezi ve Kişiselleştirme Stüdyosu standart kullanıcı modunda aktif.",
                showToast: false,
                ct: ct);
        }

        if (safeMode)
        {
            eventBus.Publish(new SafeModeStateChangedEvent(true, "Güvenli Mod etkinleştirildi.", DateTimeOffset.UtcNow));
        }

        logger.Info("Application", "STATE_INIT", $"Aurora state initialized (SafeMode={safeMode}, Profiles={_profiles.Count}).");
    }

    public AuroraThemeTokens ResolveThemeById(string? id)
    {
        lock (_sync)
        {
            return AllThemes.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase))
                   ?? BuiltInThemes.AuroraGlassDark;
        }
    }

    public void StartThemePreview(AuroraThemeTokens candidateTheme)
    {
        ArgumentNullException.ThrowIfNull(candidateTheme);
        lock (_sync)
        {
            PreviewTheme = candidateTheme;
        }

        eventBus.Publish(new ThemePreviewStartedEvent(candidateTheme, DateTimeOffset.UtcNow));
        logger.Info("Personalization", "THEME_PREVIEW_START", $"Started sandboxed theme preview '{candidateTheme.Id}'.");
    }

    public void DiscardThemePreview()
    {
        AuroraThemeTokens restored;
        lock (_sync)
        {
            PreviewTheme = null;
            restored = CommittedTheme;
        }

        eventBus.Publish(new ThemePreviewDiscardedEvent(restored, DateTimeOffset.UtcNow));
        logger.Info("Personalization", "THEME_PREVIEW_DISCARD", "Sandboxed theme preview discarded.");
    }

    public async Task ApplyThemeAsync(AuroraThemeTokens theme, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(theme);
        lock (_sync)
        {
            if (!BuiltInThemes.All.Any(b => b.Id == theme.Id))
            {
                _customThemes.RemoveAll(t => t.Id == theme.Id);
                _customThemes.Add(theme);
            }

            PreviewTheme = null;
            CommittedTheme = theme;
            Preferences = Preferences with { ActiveThemeId = theme.Id };
            var idx = _profiles.FindIndex(p => p.Id == ActiveProfile.Id);
            if (idx >= 0)
            {
                ActiveProfile = ActiveProfile with { ThemeId = theme.Id };
                _profiles[idx] = ActiveProfile;
            }
        }

        if (!BuiltInThemes.All.Any(b => b.Id == theme.Id))
            await repository.SaveCustomThemeAsync(theme, ct);

        await repository.SaveProfilesAsync(_profiles, ct);
        await repository.SavePreferencesAsync(Preferences, ct);

        eventBus.Publish(new ThemeAppliedEvent(theme, DateTimeOffset.UtcNow));
        logger.Info("Personalization", "THEME_APPLIED", $"Theme '{theme.Id}' applied and persisted.");
    }

    public async Task ResetThemeToDefaultAsync(CancellationToken ct = default)
    {
        await ApplyThemeAsync(BuiltInThemes.AuroraGlassDark, ct);
    }

    public async Task<AuroraResult<WorkspaceProfile>> SwitchProfileAsync(string profileId, CancellationToken ct = default)
    {
        var previousProfile = ActiveProfile;
        var previousTheme = CommittedTheme;

        try
        {
            var target = _profiles.FirstOrDefault(p => p.Id == profileId);
            if (target is null)
            {
                return AuroraResult<WorkspaceProfile>.Fail("PROFILE_NOT_FOUND", $"Profil bulunamadı: '{profileId}'.", fallbackValue: previousProfile);
            }

            var resolvedTheme = ResolveThemeById(target.ThemeId);
            lock (_sync)
            {
                PreviewTheme = null;
                ActiveProfile = target;
                CommittedTheme = resolvedTheme;
                Preferences = Preferences with
                {
                    ActiveProfileId = target.Id,
                    ActiveThemeId = resolvedTheme.Id,
                    Notifications = Preferences.Notifications with { QuietHoursEnabled = target.EnableQuietHours }
                };
            }

            await repository.SavePreferencesAsync(Preferences, ct);
            eventBus.Publish(new ProfileChangedEvent(target, DateTimeOffset.UtcNow));
            eventBus.Publish(new ThemeAppliedEvent(resolvedTheme, DateTimeOffset.UtcNow));
            eventBus.Publish(new TaskbarLayoutChangedEvent(target.Taskbar, DateTimeOffset.UtcNow));

            logger.Info("Workspaces", "PROFILE_SWITCHED", $"Switched active workspace profile to '{target.Id}'.");
            return AuroraResult<WorkspaceProfile>.Ok(target);
        }
        catch (Exception ex)
        {
            // Deterministic rollback per PRD 5.6
            lock (_sync)
            {
                ActiveProfile = previousProfile;
                CommittedTheme = previousTheme;
            }

            logger.Error("Workspaces", "PROFILE_SWITCH_ROLLBACK", "Profile activation failed; rolled back deterministically.", ex);
            return AuroraResult<WorkspaceProfile>.Fail("PROFILE_SWITCH_FAILED", "Profil geçişi başarısız oldu, önceki profile geri dönüldü.", fallbackValue: previousProfile);
        }
    }

    public async Task UpdateTaskbarSettingsAsync(TaskbarSettings updated, CancellationToken ct = default)
    {
        lock (_sync)
        {
            ActiveProfile = ActiveProfile with { Taskbar = updated };
            var idx = _profiles.FindIndex(p => p.Id == ActiveProfile.Id);
            if (idx >= 0)
                _profiles[idx] = ActiveProfile;
        }

        await repository.SaveProfilesAsync(_profiles, ct);
        eventBus.Publish(new TaskbarLayoutChangedEvent(updated, DateTimeOffset.UtcNow));
    }

    public async Task UpdateControlCenterSettingsAsync(ControlCenterSettings updated, CancellationToken ct = default)
    {
        lock (_sync)
        {
            ActiveProfile = ActiveProfile with { ControlCenter = updated };
            var idx = _profiles.FindIndex(p => p.Id == ActiveProfile.Id);
            if (idx >= 0)
                _profiles[idx] = ActiveProfile;
        }

        await repository.SaveProfilesAsync(_profiles, ct);
    }

    public async Task UpdateWidgetsAsync(IReadOnlyList<WidgetInstanceConfig> widgets, CancellationToken ct = default)
    {
        lock (_sync)
        {
            ActiveProfile = ActiveProfile with { Widgets = [.. widgets] };
            var idx = _profiles.FindIndex(p => p.Id == ActiveProfile.Id);
            if (idx >= 0)
                _profiles[idx] = ActiveProfile;
        }

        await repository.SaveProfilesAsync(_profiles, ct);
    }

    public async Task UpdatePreferencesAsync(UserPreferences updated, CancellationToken ct = default)
    {
        lock (_sync)
        {
            Preferences = updated;
        }

        await repository.SavePreferencesAsync(updated, ct);
    }

    public async Task PinOrUnpinShortcutAsync(string displayName, string targetPath, string glyph = "\uE737", CancellationToken ct = default)
    {
        lock (_sync)
        {
            var existing = _pinnedShortcuts.FirstOrDefault(p => string.Equals(p.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                _pinnedShortcuts.Remove(existing);
            }
            else
            {
                _pinnedShortcuts.Add(new PinnedShortcut(
                    Id: $"pin-{Guid.NewGuid():N}"[..12],
                    DisplayName: displayName,
                    TargetPath: targetPath,
                    Arguments: null,
                    IconGlyph: glyph,
                    OrderIndex: _pinnedShortcuts.Count));
            }

            ReindexPins();
        }

        await repository.SavePinnedShortcutsAsync(_pinnedShortcuts, ct);
    }

    public async Task ReplacePinnedShortcutsAsync(IReadOnlyList<PinnedShortcut> shortcuts, CancellationToken ct = default)
    {
        if (shortcuts is null || shortcuts.Count == 0)
            return;

        lock (_sync)
        {
            _pinnedShortcuts.Clear();
            _pinnedShortcuts.AddRange(shortcuts.Where(s => !string.IsNullOrWhiteSpace(s.TargetPath)));
            ReindexPins();
        }

        await repository.SavePinnedShortcutsAsync(_pinnedShortcuts, ct);
    }

    public async Task ReorderPinnedShortcutToIndexAsync(string shortcutId, int targetIndex, CancellationToken ct = default)
    {
        lock (_sync)
        {
            var index = _pinnedShortcuts.FindIndex(p => p.Id == shortcutId);
            if (index < 0)
                return;

            var clamped = Math.Clamp(targetIndex, 0, _pinnedShortcuts.Count - 1);
            if (clamped == index)
                return;

            var item = _pinnedShortcuts[index];
            _pinnedShortcuts.RemoveAt(index);
            _pinnedShortcuts.Insert(clamped, item);
            ReindexPins();
        }

        await repository.SavePinnedShortcutsAsync(_pinnedShortcuts, ct);
    }

    public async Task MovePinnedShortcutAsync(string shortcutId, int delta, CancellationToken ct = default)
    {
        lock (_sync)
        {
            var index = _pinnedShortcuts.FindIndex(p => p.Id == shortcutId);
            if (index < 0)
                return;

            var targetIndex = Math.Clamp(index + delta, 0, _pinnedShortcuts.Count - 1);
            if (targetIndex == index)
                return;

            var item = _pinnedShortcuts[index];
            _pinnedShortcuts.RemoveAt(index);
            _pinnedShortcuts.Insert(targetIndex, item);
            ReindexPins();
        }

        await repository.SavePinnedShortcutsAsync(_pinnedShortcuts, ct);
    }

    private void ReindexPins()
    {
        for (int i = 0; i < _pinnedShortcuts.Count; i++)
            _pinnedShortcuts[i] = _pinnedShortcuts[i] with { OrderIndex = i };
    }

    public bool IsQuietHoursCurrentlyActive(DateTimeOffset localNow)
    {
        var ns = Preferences.Notifications;
        if (!ns.QuietHoursEnabled)
            return false;

        int h = localNow.Hour;
        if (ns.QuietHoursStartHour == ns.QuietHoursEndHour)
            return true;

        return ns.QuietHoursStartHour > ns.QuietHoursEndHour
            ? (h >= ns.QuietHoursStartHour || h < ns.QuietHoursEndHour)
            : (h >= ns.QuietHoursStartHour && h < ns.QuietHoursEndHour);
    }

    public async Task<AuroraNotification> AddNotificationAsync(
        string sourceModule,
        NotificationCategory category,
        NotificationSeverity severity,
        string title,
        string body,
        bool showToast = true,
        string? actionLabel = null,
        string? actionDeepLink = null,
        CancellationToken ct = default)
    {
        var item = new AuroraNotification(
            Id: $"ntf-{Guid.NewGuid():N}"[..14],
            SourceModule: sourceModule,
            Category: category,
            Severity: severity,
            Title: title,
            Body: body,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            IsRead: false,
            IsDismissed: false,
            ActionLabel: actionLabel,
            ActionDeepLink: actionDeepLink);

        bool suppressToast = !Preferences.Notifications.Enabled || IsQuietHoursCurrentlyActive(DateTimeOffset.Now);

        lock (_sync)
        {
            _notifications.Insert(0, item);
            PruneExpiredNotifications();
        }

        await repository.SaveNotificationsAsync(_notifications, ct);
        eventBus.Publish(new AuroraNotificationReceivedEvent(item, showToast && !suppressToast, DateTimeOffset.UtcNow));
        eventBus.Publish(new NotificationsUpdatedEvent(Notifications, DateTimeOffset.UtcNow));
        return item;
    }

    public async Task DismissNotificationAsync(string notificationId, CancellationToken ct = default)
    {
        lock (_sync)
        {
            var idx = _notifications.FindIndex(n => n.Id == notificationId);
            if (idx >= 0)
                _notifications[idx] = _notifications[idx] with { IsDismissed = true, IsRead = true };
        }

        await repository.SaveNotificationsAsync(_notifications, ct);
        eventBus.Publish(new NotificationsUpdatedEvent(Notifications, DateTimeOffset.UtcNow));
    }

    public async Task ClearAllNotificationsAsync(CancellationToken ct = default)
    {
        lock (_sync)
        {
            _lastClearedBackup = _notifications.Where(n => !n.IsDismissed).ToList();
            for (int i = 0; i < _notifications.Count; i++)
            {
                _notifications[i] = _notifications[i] with { IsDismissed = true, IsRead = true };
            }
        }

        await repository.SaveNotificationsAsync(_notifications, ct);
        eventBus.Publish(new NotificationsUpdatedEvent(Notifications, DateTimeOffset.UtcNow));
    }

    public async Task UndoClearAllNotificationsAsync(CancellationToken ct = default)
    {
        lock (_sync)
        {
            if (_lastClearedBackup.Count == 0)
                return;

            var ids = _lastClearedBackup.Select(b => b.Id).ToHashSet();
            for (int i = 0; i < _notifications.Count; i++)
            {
                if (ids.Contains(_notifications[i].Id))
                    _notifications[i] = _notifications[i] with { IsDismissed = false };
            }

            _lastClearedBackup.Clear();
        }

        await repository.SaveNotificationsAsync(_notifications, ct);
        eventBus.Publish(new NotificationsUpdatedEvent(Notifications, DateTimeOffset.UtcNow));
    }

    private void PruneExpiredNotifications()
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, Preferences.Notifications.RetentionDays));
        _notifications = _notifications
            .Where(n => n.CreatedAtUtc >= cutoff)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(Math.Max(20, Preferences.Notifications.MaxStoredCount))
            .ToList();
    }

    public static IReadOnlyList<AppWindowGroup> BuildTaskbarGroups(
        IReadOnlyList<PinnedShortcut> pins,
        IReadOnlyList<AppWindowEntry> openWindows,
        bool groupByApplication,
        Func<string, byte[]?>? shortcutIconResolver = null)
    {
        var groups = new List<AppWindowGroup>();
        var consumedHandles = new HashSet<nint>();

        foreach (var pin in pins.OrderBy(p => p.OrderIndex))
        {
            var pinFileName = Path.GetFileNameWithoutExtension(pin.TargetPath);
            var matching = groupByApplication
                ? openWindows.Where(w =>
                    !consumedHandles.Contains(w.Hwnd) &&
                    (string.Equals(w.ProcessName, pinFileName, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(w.ExecutablePath, pin.TargetPath, StringComparison.OrdinalIgnoreCase))).ToList()
                : [];

            foreach (var m in matching)
                consumedHandles.Add(m.Hwnd);

            var iconBytes = matching.FirstOrDefault(m => m.IconPngBytes is { Length: > 0 })?.IconPngBytes
                            ?? shortcutIconResolver?.Invoke(pin.TargetPath);

            groups.Add(new AppWindowGroup(
                GroupKey: pin.Id,
                DisplayName: pin.DisplayName,
                ExecutablePath: pin.TargetPath,
                IconGlyph: pin.IconGlyph,
                IsPinned: true,
                OrderIndex: pin.OrderIndex,
                Windows: matching,
                IconPngBytes: iconBytes));
        }

        var remaining = openWindows.Where(w => !consumedHandles.Contains(w.Hwnd)).ToList();
        if (groupByApplication)
        {
            int idx = groups.Count;
            foreach (var grp in remaining.GroupBy(w => w.ProcessName, StringComparer.OrdinalIgnoreCase))
            {
                var first = grp.First();
                var iconBytes = grp.FirstOrDefault(w => w.IconPngBytes is { Length: > 0 })?.IconPngBytes;
                groups.Add(new AppWindowGroup(
                    GroupKey: $"run-{grp.Key.ToLowerInvariant()}",
                    DisplayName: FormatProcessDisplayName(first.ProcessName),
                    ExecutablePath: first.ExecutablePath,
                    IconGlyph: ResolveProcessGlyph(first.ProcessName),
                    IsPinned: false,
                    OrderIndex: idx++,
                    Windows: grp.ToList(),
                    IconPngBytes: iconBytes));
            }
        }
        else
        {
            int idx = groups.Count;
            foreach (var win in remaining)
            {
                groups.Add(new AppWindowGroup(
                    GroupKey: $"hwnd-{win.Hwnd}",
                    DisplayName: string.IsNullOrWhiteSpace(win.Title) ? FormatProcessDisplayName(win.ProcessName) : win.Title,
                    ExecutablePath: win.ExecutablePath,
                    IconGlyph: ResolveProcessGlyph(win.ProcessName),
                    IsPinned: false,
                    OrderIndex: idx++,
                    Windows: [win],
                    IconPngBytes: win.IconPngBytes));
            }
        }

        return groups;
    }

    private static string FormatProcessDisplayName(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return "Uygulama";
        return char.ToUpperInvariant(processName[0]) + processName[1..];
    }

    private static string ResolveProcessGlyph(string processName) =>
        processName.ToLowerInvariant() switch
        {
            "explorer" => "\uEC50",
            "msedge" or "chrome" or "firefox" or "brave" => "\uE774",
            "code" or "devenv" or "antigravity" => "\uE943",
            "powershell" or "pwsh" or "windowsterminal" or "cmd" => "\uE756",
            "spotify" or "wmplayer" or "music.ui" => "\uEC4F",
            "notepad" => "\uE70F",
            "calculatorapp" or "calc" => "\uE8EF",
            "systemsettings" => "\uE713",
            "taskmgr" => "\uE9D9",
            _ => "\uE737"
        };
}
