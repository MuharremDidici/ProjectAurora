using Aurora.Application.Services;
using Aurora.Core.Contracts;
using Aurora.Core.Models;
using Aurora.Core.Primitives;
using Aurora.Presentation;

namespace Aurora.Taskbar;

public sealed class TaskbarViewModel(
    AuroraStateStore stateStore,
    IWindowCatalog windowCatalog,
    IWindowActivationService activationService,
    IAppBarService appBarService,
    IMonitorTopologyService monitorService) : ObservableViewModel
{
    private IReadOnlyList<AppWindowGroup> _groups = [];
    private IReadOnlyList<AppWindowEntry> _openWindows = [];
    private NativeTaskbarStatus _nativeStatus = new(
        NativeTaskbarVisibilityState.AlwaysVisible,
        TaskbarEdge.Bottom,
        new PhysicalPixelRect(0, 0, 0, 48),
        true,
        "Birlikte Çalışma Modu");
    private string _clockTimeText = DateTime.Now.ToString("HH:mm");
    private string _clockDateText = DateTime.Now.ToString("dd MMM ddd");
    private int _unreadNotificationCount;
    private bool _isAutoHiddenCollapsed;

    public IReadOnlyList<AppWindowGroup> Groups
    {
        get => _groups;
        private set => SetProperty(ref _groups, value);
    }

    public IReadOnlyList<AppWindowEntry> OpenWindows
    {
        get => _openWindows;
        private set => SetProperty(ref _openWindows, value);
    }

    public NativeTaskbarStatus NativeStatus
    {
        get => _nativeStatus;
        private set => SetProperty(ref _nativeStatus, value);
    }

    public string ClockTimeText
    {
        get => _clockTimeText;
        private set => SetProperty(ref _clockTimeText, value);
    }

    public string ClockDateText
    {
        get => _clockDateText;
        private set => SetProperty(ref _clockDateText, value);
    }

    public int UnreadNotificationCount
    {
        get => _unreadNotificationCount;
        private set => SetProperty(ref _unreadNotificationCount, value);
    }

    public bool IsAutoHiddenCollapsed
    {
        get => _isAutoHiddenCollapsed;
        set => SetProperty(ref _isAutoHiddenCollapsed, value);
    }

    public TaskbarSettings Settings => stateStore.ActiveProfile.Taskbar;
    public WorkspaceProfile ActiveProfile => stateStore.ActiveProfile;

    public void RefreshSnapshot(IReadOnlySet<nint>? auroraOwnedHwnds = null)
    {
        var now = DateTime.Now;
        ClockTimeText = now.ToString("HH:mm");
        ClockDateText = now.ToString("dd.MM.yyyy");
        UnreadNotificationCount = stateStore.Notifications.Count(n => !n.IsDismissed);
        NativeStatus = appBarService.QueryNativeTaskbarStatus();

        var winRes = windowCatalog.GetOpenWindows(auroraOwnedHwnds);
        var openWindows = winRes.IsSuccess && winRes.Value is not null ? winRes.Value : [];
        OpenWindows = openWindows;
        Groups = AuroraStateStore.BuildTaskbarGroups(
            stateStore.PinnedShortcuts,
            openWindows,
            Settings.GroupWindowsByApp,
            windowCatalog.ResolveShortcutIconPng);
    }

    public EffectiveDipRect ComputePrimaryBarBoundsDip()
    {
        var primary = monitorService.GetPrimaryMonitor();
        return TaskbarLayoutCalculator.CalculateTaskbarBoundsDip(primary, Settings, NativeStatus);
    }

    public AuroraResult InvokeGroupPrimaryAction(AppWindowGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (group.Windows.Count > 0)
        {
            var target = group.Windows.FirstOrDefault(w => !w.IsActive) ?? group.Windows[0];
            return activationService.ActivateOrToggleWindow(target.Hwnd);
        }

        var pin = stateStore.PinnedShortcuts.FirstOrDefault(p => p.Id == group.GroupKey)
                  ?? new PinnedShortcut(group.GroupKey, group.DisplayName, group.ExecutablePath, null, group.IconGlyph, group.OrderIndex);
        return activationService.LaunchShortcut(pin);
    }

    public Task ReorderPinnedByKeyboardAsync(string shortcutId, int direction, CancellationToken ct = default) =>
        stateStore.MovePinnedShortcutAsync(shortcutId, direction, ct);
}
