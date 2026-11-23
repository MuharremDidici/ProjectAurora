using Aurora.Core.Models;
using Aurora.Core.Tokens;

namespace Aurora.Application.Services;

public static class DefaultCatalogFactory
{
    public static IReadOnlyList<PinnedShortcut> CreateDefaultPinnedShortcuts() =>
    [
        new("pin-explorer", "Dosya Gezgini", "explorer.exe", null, "\uEC50", 0),
        new("pin-terminal", "PowerShell", "powershell.exe", null, "\uE756", 1),
        new("pin-notepad", "Not Defteri", "notepad.exe", null, "\uE70F", 2),
        new("pin-calc", "Hesap Makinesi", "calc.exe", null, "\uE8EF", 3),
        new("pin-settings", "Windows Ayarları", "ms-settings:", null, "\uE713", 4)
    ];

    public static IReadOnlyList<WidgetInstanceConfig> CreateDefaultWidgets() =>
    [
        new("wg-clock", WidgetKind.ClockDate, "Saat ve Tarih", WidgetSizeClass.Medium, true, 0),
        new("wg-cpuram", WidgetKind.CpuRamMetrics, "CPU ve Bellek", WidgetSizeClass.Medium, true, 1),
        new("wg-network", WidgetKind.NetworkThroughput, "Ağ Akışı", WidgetSizeClass.Small, true, 2),
        new("wg-gpu", WidgetKind.GpuMetrics, "GPU Telemetrisi", WidgetSizeClass.Small, true, 3),
        new("wg-calendar", WidgetKind.Calendar, "Takvim", WidgetSizeClass.Medium, true, 4),
        new("wg-weather", WidgetKind.Weather, "Hava Durumu", WidgetSizeClass.Medium, true, 5)
    ];

    public static IReadOnlyList<WorkspaceProfile> CreateDefaultProfiles()
    {
        var baseWidgets = CreateDefaultWidgets();

        return
        [
            new WorkspaceProfile(
                Id: "profile-work",
                Name: "İş & Odak",
                IconGlyph: "\uE821",
                Description: "Aurora Glass teması, ortalanmış standart görev çubuğu ve üretkenlik kartları.",
                ThemeId: BuiltInThemes.AuroraGlassDark.Id,
                Taskbar: new TaskbarSettings(
                    Edge: TaskbarEdge.Bottom,
                    Alignment: TaskbarAlignment.Center,
                    Density: TaskbarDensity.Standard,
                    AutoHide: false,
                    FloatingDockStyle: true),
                ControlCenter: ControlCenterSettings.Default,
                Widgets: baseWidgets,
                EnableQuietHours: false),

            new WorkspaceProfile(
                Id: "profile-gaming",
                Name: "Oyun & Performans",
                IconGlyph: "\uE7FC",
                Description: "Nebula teması, kompakt görev çubuğu, GPU/Ağ izleme ve Sessiz Saatler aktif.",
                ThemeId: BuiltInThemes.NebulaDark.Id,
                Taskbar: new TaskbarSettings(
                    Edge: TaskbarEdge.Bottom,
                    Alignment: TaskbarAlignment.Center,
                    Density: TaskbarDensity.Compact,
                    AutoHide: false,
                    FloatingDockStyle: true),
                ControlCenter: ControlCenterSettings.Default with { Density = ControlCenterDensity.Compact },
                Widgets: baseWidgets.Select(w => w.Kind is WidgetKind.Weather ? w with { IsVisible = false } : w).ToList(),
                EnableQuietHours: true),

            new WorkspaceProfile(
                Id: "profile-personal",
                Name: "Kişisel & Akış",
                IconGlyph: "\uE77B",
                Description: "Aurora Flow dinamik vurgular, geniş ferah görev çubuğu ve medya odaklı düzen.",
                ThemeId: BuiltInThemes.AuroraFlowDark.Id,
                Taskbar: new TaskbarSettings(
                    Edge: TaskbarEdge.Bottom,
                    Alignment: TaskbarAlignment.Center,
                    Density: TaskbarDensity.Spacious,
                    AutoHide: false,
                    FloatingDockStyle: true),
                ControlCenter: ControlCenterSettings.Default with { Density = ControlCenterDensity.Expanded },
                Widgets: baseWidgets,
                EnableQuietHours: false),

            new WorkspaceProfile(
                Id: "profile-studio-pro",
                Name: "Obsidian Pro",
                IconGlyph: "\uE943",
                Description: "Yüksek kontrastlı yoğun profesyonel yüzeyler ve sola hizalı kompakt düzen.",
                ThemeId: BuiltInThemes.ObsidianProDark.Id,
                Taskbar: new TaskbarSettings(
                    Edge: TaskbarEdge.Bottom,
                    Alignment: TaskbarAlignment.Left,
                    Density: TaskbarDensity.Compact,
                    AutoHide: false,
                    FloatingDockStyle: false),
                ControlCenter: ControlCenterSettings.Default,
                Widgets: baseWidgets,
                EnableQuietHours: false)
        ];
    }
}
