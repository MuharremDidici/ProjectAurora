using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aurora.Core.Primitives;
using Aurora.Core.Tokens;

namespace Aurora.Presentation;

public abstract class ObservableViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record AuroraComponentSpecification(
    string ComponentName,
    string AccessibleRole,
    double MinTouchTargetDip,
    IReadOnlyList<string> SupportedVariants,
    IReadOnlyList<ControlOperationalState> SupportedStates,
    string KeyboardContract);

public static class DesignSystemComponentCatalog
{
    public const double RecommendedTouchTargetDip = 40.0;

    public static IReadOnlyList<AuroraComponentSpecification> AllComponents { get; } =
    [
        Spec("AuroraSurface", "Pane", 40, ["Canvas", "Elevated", "Frosted"], "Tab passes to child interactive controls"),
        Spec("AuroraPanel", "Dialog", 40, ["Compact", "Standard", "Expanded"], "Escape dismisses transient panel and restores focus"),
        Spec("AuroraCard", "Group", 40, ["Flat", "Elevated", "Interactive"], "Enter/Space activates interactive card"),
        Spec("AuroraIconButton", "Button", 40, ["Subtle", "Standard", "Accent", "Danger"], "Enter/Space invokes command; Alt+Arrow reorders when pinnable"),
        Spec("AuroraToggle", "CheckBox", 40, ["Pill", "Tile", "Compact"], "Space/Enter toggles state; announces Pending/Error honestly"),
        Spec("AuroraSlider", "Slider", 40, ["Audio", "Brightness", "TokenScale"], "Left/Right ±1%, PageUp/PageDown ±10%, Home/End min/max"),
        Spec("AuroraSegmentedControl", "TabList", 40, ["Density", "Alignment", "ThemeMode"], "Arrow keys move selection across segments"),
        Spec("AuroraDeviceRow", "ListItem", 40, ["OutputDevice", "InputDevice", "AppAudioSession"], "Enter selects or opens device settings"),
        Spec("AuroraStatusPill", "StatusBar", 32, ["Coexistence", "Capability", "SafeMode", "Preview"], "Focusable info badge with screen-reader summary"),
        Spec("AuroraNotificationCard", "ListItem", 40, ["Info", "Success", "Warning", "Error"], "Delete dismisses notification; Enter triggers action"),
        Spec("AuroraWidgetFrame", "Region", 40, ["Small", "Medium", "Large"], "Context menu / buttons resize, reorder or hide widget"),
        Spec("AuroraMenu", "Menu", 40, ["TaskbarContext", "ProfileQuickMenu"], "Up/Down navigates items, Escape closes menu"),
        Spec("AuroraTooltip", "ToolTip", 24, ["DelayedInfo", "ShortcutHint"], "Appears on hover/focus without stealing focus"),
        Spec("AuroraEmptyState", "Group", 40, ["NotificationsEmpty", "MediaIdle", "LogsEmpty"], "Provides actionable primary button when applicable"),
        Spec("AuroraErrorState", "Alert", 40, ["CapabilityFallback", "ValidationFailure", "RecoveryAlert"], "Exposes deep-link fallback or retry button"),
        Spec("AuroraSkeleton", "ProgressBar", 32, ["Line", "Card", "Artwork"], "Respects Reduced Motion by switching shimmer to static pulse"),
        Spec("AuroraFocusRing", "Border", 40, ["HighVisibility", "AccentNeon"], "2px high-contrast ring visible across all 4 theme families")
    ];

    private static AuroraComponentSpecification Spec(
        string name,
        string role,
        double minDip,
        IReadOnlyList<string> variants,
        string keyboard) =>
        new(
            ComponentName: name,
            AccessibleRole: role,
            MinTouchTargetDip: minDip,
            SupportedVariants: variants,
            SupportedStates:
            [
                ControlOperationalState.Off,
                ControlOperationalState.On,
                ControlOperationalState.Pending,
                ControlOperationalState.Unavailable,
                ControlOperationalState.PermissionRequired,
                ControlOperationalState.Error
            ],
            KeyboardContract: keyboard);

    public static AuroraThemeTokens ApplyRuntimeAccessibilityOverrides(
        AuroraThemeTokens baseTheme,
        bool systemHighContrast,
        bool reducedMotion,
        bool lowPowerBatteryMode)
    {
        if (systemHighContrast)
            return BuiltInThemes.HighContrastSafe;

        var result = baseTheme;
        if (reducedMotion || lowPowerBatteryMode)
        {
            result = result with
            {
                Motion = result.Motion with { ReducedMotion = true },
                Material = lowPowerBatteryMode
                    ? result.Material with { Blur = AuroraBlurLevel.None, SurfaceOpacity = Math.Max(0.95, result.Material.SurfaceOpacity), AmbientGradientEnabled = false }
                    : result.Material
            };
        }

        return result;
    }
}
