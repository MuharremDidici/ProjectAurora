using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Aurora.App.Host;
using Aurora.Application.Services;
using Aurora.ControlCenter;
using Aurora.Core.Contracts;
using Aurora.Core.Events;
using Aurora.Core.Models;
using Aurora.Core.Primitives;
using Aurora.Core.Tokens;
using Aurora.Infrastructure.Logging;
using Aurora.Infrastructure.Notifications;
using Aurora.Infrastructure.Persistence;
using Aurora.Infrastructure.Recovery;
using Aurora.Infrastructure.Themes;
using Aurora.Infrastructure.Updates;
using Aurora.Media;
using Aurora.Notifications;
using Aurora.Personalization;
using Aurora.Platform.Windows.Apps;
using Aurora.Platform.Windows.Audio;
using Aurora.Platform.Windows.DisplayAndTaskbar;
using Aurora.Platform.Windows.Media;
using Aurora.Platform.Windows.Metrics;
using Aurora.Platform.Windows.ShellExtras;
using Aurora.Platform.Windows.SystemControls;
using Aurora.Taskbar;
using Aurora.Widgets;
using Aurora.Workspaces;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace Aurora.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // CLI Mode 1: Emergency / manual native Windows taskbar recovery
        if (args.Any(a => string.Equals(a, "--restore-taskbar", StringComparison.OrdinalIgnoreCase)))
        {
            using var restoreSvc = new Win32AppBarService(new PrivacySafeFileLogger());
            restoreSvc.RestoreNativeWindowsTaskbar(0);
            return;
        }

        // CLI Mode 2: Detached crash-safety watchdog (restores native Windows taskbar if Aurora terminates abnormally)
        var watchArg = args.FirstOrDefault(a => a.StartsWith("--watchdog-for-pid=", StringComparison.OrdinalIgnoreCase));
        if (watchArg is not null && int.TryParse(watchArg["--watchdog-for-pid=".Length..], out int targetPid))
        {
            int origState = 0;
            var stateArg = args.FirstOrDefault(a => a.StartsWith("--orig-state=", StringComparison.OrdinalIgnoreCase));
            if (stateArg is not null)
                int.TryParse(stateArg["--orig-state=".Length..], out origState);

            RunNativeTaskbarWatchdog(targetPid, origState);
            return;
        }

        System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

        using var host = new AuroraDesktopCoordinator(args);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => host.EnsureNativeTaskbarRestored();
        try
        {
            System.Windows.Forms.Application.Run(host);
        }
        finally
        {
            host.EnsureNativeTaskbarRestored();
        }
    }

    private static void RunNativeTaskbarWatchdog(int targetPid, int origState)
    {
        try
        {
            using var targetProc = Process.GetProcessById(targetPid);
            targetProc.WaitForExit();
        }
        catch { }

        try
        {
            using var restoreSvc = new Win32AppBarService(new PrivacySafeFileLogger());
            restoreSvc.RestoreNativeWindowsTaskbar(origState);
        }
        catch { }
    }
}

internal sealed class MonitorSurfaceHost(
    MonitorDescriptor monitor,
    TransparentSurfaceForm crownForm,
    TransparentSurfaceForm dockForm) : IDisposable
{
    public MonitorDescriptor Monitor { get; set; } = monitor;
    public TransparentSurfaceForm CrownForm { get; } = crownForm;
    public TransparentSurfaceForm DockForm { get; } = dockForm;

    public void Dispose()
    {
        try { CrownForm.Close(); CrownForm.Dispose(); } catch { }
        try { DockForm.Close(); DockForm.Dispose(); } catch { }
    }
}

internal sealed class AuroraDesktopCoordinator : ApplicationContext
{
    private const int HotkeyOmniBar = 9101;
    private const int HotkeyDeckControl = 9102;
    private const int HotkeyDeckStudio = 9103;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkSpace = 0x20;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LockWorkStation();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VkLButton = 0x01;
    private const int VkShift = 0x10;

    private readonly string[] _args;
    private readonly PrivacySafeFileLogger _logger = new();
    private readonly StartupCrashSentinel _crashSentinel = new();
    private readonly BoundedEventBus _eventBus = new();
    private readonly SqliteAuroraRepository _repository;
    private readonly ThemePackageArchiveService _themeStore;
    private readonly AuroraStateStore _stateStore;

    private readonly MonitorTopologyService _monitorService = new();
    private readonly FullscreenMonitorDetector _fullscreenDetector = new();
    private readonly Win32WindowEventHook _windowEventHook = new();
    private readonly Win32AppBarService _appBarService;
    private readonly Win32WindowCatalog _windowCatalog = new();
    private readonly Win32WindowActivationService _windowActivation;
    private readonly CoreAudioService _audioService;
    private readonly PerAppAudioRoutingService _perAppAudioRouting;
    private readonly bool _perAppAudioRoutingSupported;
    private readonly AwakeKeepAliveService _awakeService = new();
    private readonly ClipboardVaultService _clipboardVaultStore = new();
    private readonly GsmtcMediaSessionService _mediaService;
    private readonly WindowsRadioAndNetworkService _radioNetService;
    private readonly WindowsDisplayAndPowerService _displayPowerService;
    private readonly WindowsSystemMetricsService _metricsService = new();
    private readonly HkcuStartupRegistrationService _startupService;
    private readonly InstalledAppCatalogService _appCatalog = new();
    private readonly LocalFileSearchService _fileSearch = new();
    private readonly KeyboardLayoutService _keyboardService = new();
    private readonly BackgroundTrayAppsService _trayService = new();
    private readonly WindowThumbnailCaptureService _thumbnailService = new();
    private readonly ShellPreferencesStore _shellPrefsStore = new();
    private readonly WindowsNotificationBridgeService _sysNotifBridge = new();
    private readonly VirtualDesktopService _virtualDesktops = new();
    private readonly WinKeyLowLevelHook _winKeyHook = new();
    private readonly HardwareOsdMonitorService _osdMonitor = new();
    private readonly BluetoothBatteryService _btBatteryService = new();
    private readonly DdcCiMonitorBrightnessService _ddcBrightnessService = new();
    private readonly PrivacyIndicatorService _privacyService = new();
    private readonly LanQrFileShareService _lanQrShareService = new();
    private readonly AnimatedGifRecorderService _gifRecorder = new();
    private System.Windows.Forms.Timer? _btBatteryTimer;
    private System.Windows.Forms.Timer? _metricsLiveTimer;
    private IReadOnlyList<BluetoothBatteryDevice> _cachedBtDevices = [];
    private PrivacyIndicatorSnapshot _cachedPrivacy = new(false, false, [], []);
    private readonly List<double> _cpuHistory = [];
    private readonly List<double> _ramHistory = [];
    private readonly List<double> _netHistory = [];
    private TopProcessSnapshot[] _topProcesses = [];
    private bool _btPollInFlight;
    private string? _gifRecSourceMonitorId;
    private AltDragWindowService? _altDragService;
    private System.Windows.Forms.Timer? _osdTimer;
    private int _osdBrightnessCounter;
    private bool _cachedBrightSupported;
    private int _cachedBrightValue;
    private readonly WindowAttentionAndProgressService _attentionService = new();
    private readonly HashSet<long> _dismissedSystemNotifIds = [];
    private HashSet<string> _dockOverlappedMonitors = new(StringComparer.OrdinalIgnoreCase);
    private int _unreadNotifCount;
    private DwmLivePreviewForm? _dwmPreviewForm;
    private SnapZonesOverlayForm? _snapZonesOverlay;
    private DockDragOverlayForm? _dockDragOverlay;
    private System.Windows.Forms.Timer? _snapDragTimer;
    private System.Windows.Forms.Timer? _volumeRefreshDebounceTimer;
    private nint _draggingWindowHwnd;
    private int _dockResizeStartScale = 100;
    private int _dockResizeLiveScale = 100;
    private string _dockMovePreviewEdge = "Bottom";
    private int _dockMovePreviewOffset;
    private bool _suppressNextClipboardCapture;
    private string _lastUpdateStatusText = $"v{AuroraUpdateService.CurrentVersion}";
    private string? _pendingUpdateDownloadUrl;
    private AuroraReleaseManifest? _pendingUpdateManifest;
    private bool _isCheckingUpdates;
    private bool _isDownloadingUpdate;
    private int _updateDownloadPercent;
    private string? _lastNotifiedUpdateVersion;
    private System.Windows.Forms.Timer? _autoUpdateTimer;

    private readonly TaskbarViewModel _taskbarVm;
    private readonly MediaHubViewModel _mediaVm;
    private readonly WorkspaceManagerViewModel _workspaceVm;
    private readonly ControlCenterViewModel _ccVm;
    private readonly WidgetBoardViewModel _widgetVm;
    private readonly PersonalizationStudioViewModel _studioVm;

    private Mutex? _singleInstanceMutex;
    private CoreWebView2Environment? _sharedWebViewEnv;
    private readonly Dictionary<string, MonitorSurfaceHost> _monitorHosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _lastCrownJsonByMonitor = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _lastDockJsonByMonitor = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, nint> _lastFocusedWindowByGroup = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<nint> _recentFocusedHwnds = [];
    private readonly ConditionalWeakTable<byte[], string> _base64IconMemo = [];
    private HotkeyWindow? _hotkeyWindow;
    private System.Windows.Forms.Timer? _syncTimer;
    private System.Windows.Forms.Timer? _clockTickTimer;
    private System.Windows.Forms.Timer? _focusTimer;
    private System.Windows.Forms.Timer? _fullscreenTimer;
    private System.Windows.Forms.Timer? _windowEventDebounceTimer;
    private System.Windows.Forms.Timer? _trayPeekTimer;
    private Process? _watchdogProcess;
    private bool _reconcilingMonitors;
    private bool _refreshInFlight;
    private bool _allMonitorsFullscreen;
    private bool _metricsTabActive;

    private readonly List<nint> _minimizedForDesktop = [];
    private readonly Dictionary<string, string> _agendaNotes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _searchFrecency = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _dropShelfPaths = [];
    private object[]? _cachedInstalledAppDtos;
    private string? _cachedStaticCatalogJson;
    private DateTimeOffset _installedAppsCachedUtc = DateTimeOffset.MinValue;
    private string _scratchpadText = string.Empty;
    private readonly string _scratchpadFilePath;
    private readonly string _agendaFilePath;
    private readonly string _dropShelfFilePath;
    private readonly string _frecencyFilePath;
    private ShellRuntimePreferences _shellPrefs;
    private TimeSpan _focusRemaining = TimeSpan.Zero;
    private bool _focusRunning;
    private nint _lastExternalActiveHwnd;

    public AuroraDesktopCoordinator(string[] args)
    {
        _args = args;
        _repository = new SqliteAuroraRepository(_logger);
        _themeStore = new ThemePackageArchiveService(_logger);
        _stateStore = new AuroraStateStore(_repository, _eventBus, _logger);
        _appBarService = new Win32AppBarService(_logger);
        _windowActivation = new Win32WindowActivationService(_logger);
        _audioService = new CoreAudioService(_logger);
        _perAppAudioRouting = new PerAppAudioRoutingService(_logger);
        _perAppAudioRoutingSupported = _perAppAudioRouting.IsSupported();
        _mediaService = new GsmtcMediaSessionService(_logger);
        _radioNetService = new WindowsRadioAndNetworkService(_logger);
        _displayPowerService = new WindowsDisplayAndPowerService(_logger);
        _startupService = new HkcuStartupRegistrationService(_logger);

        _taskbarVm = new TaskbarViewModel(_stateStore, _windowCatalog, _windowActivation, _appBarService, _monitorService);
        _mediaVm = new MediaHubViewModel(_mediaService);
        _workspaceVm = new WorkspaceManagerViewModel(_stateStore);
        _ccVm = new ControlCenterViewModel(_stateStore, _audioService, _audioService, _radioNetService, _radioNetService, _displayPowerService, _displayPowerService);
        _widgetVm = new WidgetBoardViewModel(_stateStore, _metricsService) { IsSurfaceVisible = false };
        _studioVm = new PersonalizationStudioViewModel(_stateStore, _themeStore);
        _shellPrefs = _shellPrefsStore.Load();

        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectAurora", "data");
        Directory.CreateDirectory(dataDir);
        _scratchpadFilePath = Path.Combine(dataDir, "scratchpad.txt");
        _agendaFilePath = Path.Combine(dataDir, "agenda_notes.json");
        _dropShelfFilePath = Path.Combine(dataDir, "drop_shelf.json");
        _frecencyFilePath = Path.Combine(dataDir, "search_frecency.json");
        LoadAuxiliaryState();

        var bootTrigger = new System.Windows.Forms.Timer { Interval = 15 };
        bootTrigger.Tick += async (_, _) =>
        {
            bootTrigger.Stop();
            bootTrigger.Dispose();
            await BootAsync();
        };
        bootTrigger.Start();
    }

    private void LoadAuxiliaryState()
    {
        if (File.Exists(_scratchpadFilePath))
        {
            try { _scratchpadText = File.ReadAllText(_scratchpadFilePath); } catch { }
        }
        if (File.Exists(_agendaFilePath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_agendaFilePath));
                if (loaded is not null)
                {
                    foreach (var kv in loaded) _agendaNotes[kv.Key] = kv.Value;
                }
            }
            catch { }
        }
        if (File.Exists(_dropShelfFilePath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_dropShelfFilePath));
                if (loaded is not null)
                    _dropShelfPaths.AddRange(loaded.Where(p => File.Exists(p) || Directory.Exists(p)).Take(16));
            }
            catch { }
        }
        if (File.Exists(_frecencyFilePath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(_frecencyFilePath));
                if (loaded is not null)
                {
                    foreach (var kv in loaded) _searchFrecency[kv.Key] = kv.Value;
                }
            }
            catch { }
        }
    }

    private async Task BootAsync()
    {
        try
        {
            _singleInstanceMutex = new Mutex(true, @"Local\ProjectAurora_SingleInstance_v1", out bool isFirst);
            if (!isFirst)
            {
                int myPid = Environment.ProcessId;
                foreach (var p in Process.GetProcessesByName("Aurora.App"))
                {
                    if (p.Id != myPid)
                    {
                        try { p.Kill(); p.WaitForExit(1500); } catch { }
                    }
                }
                _singleInstanceMutex.Dispose();
                await Task.Delay(220);
                _singleInstanceMutex = new Mutex(true, @"Local\ProjectAurora_SingleInstance_v1", out _);
            }

            bool safeMode = _args.Any(a => string.Equals(a, "--safe-mode", StringComparison.OrdinalIgnoreCase))
                            || _crashSentinel.RecordBootAndCheckSafeModeRecommendation();

            await _stateStore.InitializeAsync(safeMode);

            if (_stateStore.EffectiveTheme.Id.StartsWith("custom-", StringComparison.OrdinalIgnoreCase))
                await _stateStore.ApplyThemeAsync(BuiltInThemes.AuroraGlassDark);

            bool suppressNativeTaskbar = !safeMode
                                         && _shellPrefs.HideNativeTaskbarWhileActive
                                         && ParseSmokeExitSeconds() is null
                                         && !_args.Any(a => string.Equals(a, "--keep-native-taskbar", StringComparison.OrdinalIgnoreCase));
            if (suppressNativeTaskbar)
            {
                _appBarService.HideNativeWindowsTaskbar();
                SpawnCrashWatchdogIfNeeded();
            }

            await _ccVm.RefreshAllAsync();
            await _mediaVm.RefreshSessionsAsync();
            _widgetVm.IsSurfaceVisible = true;
            await _widgetVm.RefreshTelemetryIfVisibleAsync(forceWeather: true);
            _widgetVm.IsSurfaceVisible = false;
            // Phase 1: Instant (<150ms) full 250+ app & settings catalog without blocking boot on Shell icon decoding
            EnsureInstalledAppsCached(force: false, includeIcons: false);
            _fileSearch.WarmIndexInBackground();

            var wvDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectAurora", "webview2_cache");
            var wvOptions = new CoreWebView2EnvironmentOptions(
                additionalBrowserArguments: "--renderer-process-limit=2 --disable-background-networking --disable-component-update --disable-domain-reliability --disable-features=Translate,msEdgeOnDeviceTranslation,msEdgeShoppingAssistant,msEdgeReadAloud,MediaRouter");
            _sharedWebViewEnv = await CoreWebView2Environment.CreateAsync(null, wvDataDir, wvOptions);

            await ReconcileMonitorHostsAsync();

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.SessionEnding += OnSessionEnding;

            _hotkeyWindow = new HotkeyWindow(id =>
            {
                var target = GetMonitorHostUnderCursor();
                if (target is null) return;

                if (id == HotkeyOmniBar)
                {
                    target.CrownForm.FocusAndActivateSurface();
                    target.CrownForm.PostJsonState("""{"type":"toggleOmni"}""");
                }
                else if (id == HotkeyDeckControl)
                {
                    target.CrownForm.FocusAndActivateSurface();
                    target.CrownForm.PostJsonState("""{"type":"toggleDeck","tab":"control"}""");
                }
                else if (id == HotkeyDeckStudio)
                {
                    target.CrownForm.FocusAndActivateSurface();
                    target.CrownForm.PostJsonState("""{"type":"toggleDeck","tab":"studio"}""");
                }
            }, () =>
            {
                SampleClipboardSafely();
                _ = RefreshAndBroadcastStateAsync(windowTopologyOnly: true);
            });
            _hotkeyWindow.RegisterHotkeys(_shellPrefs.OmniHotkeyPreset);

            _dwmPreviewForm = new DwmLivePreviewForm();
            _snapZonesOverlay = new SnapZonesOverlayForm();
            _snapDragTimer = new System.Windows.Forms.Timer { Interval = 28 };
            _snapDragTimer.Tick += (_, _) => UpdateSnapZonesOverlayDuringDrag();

            // Pillar 1: Live Windows 11 Notification Bridge (WhatsApp, Teams, Mail, System Toasts -> Crown Droplet!)
            _sysNotifBridge.IsEnabled = _shellPrefs.SystemNotificationBridgeEnabled;
            _sysNotifBridge.NewToastArrived += OnSystemToastArrived;
            _sysNotifBridge.Start();

            // Pillar 4: Optional Single-Tap Win Key -> Opens Aurora OmniSearch while preserving all Win+Key shortcuts
            _winKeyHook.SingleWinKeyTapped += (_, _) =>
            {
                var primaryForInvoke = GetPrimaryMonitorHost();
                if (primaryForInvoke is not null && !primaryForInvoke.CrownForm.IsDisposed && primaryForInvoke.CrownForm.IsHandleCreated)
                {
                    primaryForInvoke.CrownForm.BeginInvoke(() =>
                    {
                        var target = GetMonitorHostUnderCursor();
                        if (target is null) return;
                        target.CrownForm.FocusAndActivateSurface();
                        target.CrownForm.PostJsonState("""{"type":"toggleOmni"}""");
                    });
                }
            };
            _winKeyHook.SetEnabled(_shellPrefs.WinKeyOpensOmniSearch && ParseSmokeExitSeconds() is null);

            // Dynamic Droplet Live Hardware OSD HUD (Volume, Brightness, Mic Mute, CapsLock, NumLock)
            // Intercepts hardware VK_VOLUME_* directly when Live OSD is active so Windows 11's native volume flyout never appears!
            _volumeRefreshDebounceTimer = new System.Windows.Forms.Timer { Interval = 240 };
            _volumeRefreshDebounceTimer.Tick += async (_, _) =>
            {
                _volumeRefreshDebounceTimer.Stop();
                await RefreshAndBroadcastStateAsync(windowTopologyOnly: false);
            };
            _osdMonitor.SuppressNativeVolumeKeys = _shellPrefs.HardwareOsdEnabled;
            _osdMonitor.VolumeKeyIntercepted += vk =>
            {
                var primaryForInvoke = GetPrimaryMonitorHost();
                if (primaryForInvoke is not null && !primaryForInvoke.CrownForm.IsDisposed && primaryForInvoke.CrownForm.IsHandleCreated)
                {
                    primaryForInvoke.CrownForm.BeginInvoke(() => HandleInterceptedVolumeKey(vk));
                }
            };
            _osdMonitor.HardwareKeyObserved += _ =>
            {
                var primaryForInvoke = GetPrimaryMonitorHost();
                if (primaryForInvoke is not null && !primaryForInvoke.CrownForm.IsDisposed && primaryForInvoke.CrownForm.IsHandleCreated)
                {
                    primaryForInvoke.CrownForm.BeginInvoke(async () =>
                    {
                        await Task.Delay(28);
                        PollHardwareOsdSafely(forceBrightnessRead: false);
                    });
                }
            };
            bool notSmoke = ParseSmokeExitSeconds() is null;
            _osdMonitor.SetHookEnabled(_shellPrefs.HardwareOsdEnabled && notSmoke);
            _osdTimer = new System.Windows.Forms.Timer { Interval = 450 };
            _osdTimer.Tick += (_, _) => PollHardwareOsdSafely(forceBrightnessRead: false);
            if (notSmoke)
            {
                PollHardwareOsdSafely(forceBrightnessRead: true); // Seed baseline silently
                _osdTimer.Start();
            }

            // Alt+Left-Drag Move & Alt+Right-Drag 3x3 Resize for both normal windows AND Aurora's Dock itself!
            _altDragService = new AltDragWindowService(
                isExcludedHwnd: hwnd => CollectOwnedHwnds().Contains(hwnd),
                dockHitResolver: ResolveDockHwndAtPoint);
            _altDragService.AltMoveStarted += hwnd =>
            {
                var primaryForInvoke = GetPrimaryMonitorHost();
                if (primaryForInvoke is not null && !primaryForInvoke.CrownForm.IsDisposed && primaryForInvoke.CrownForm.IsHandleCreated)
                {
                    primaryForInvoke.CrownForm.BeginInvoke(() =>
                    {
                        if (_shellPrefs.SnapZonesEnabled && !_allMonitorsFullscreen)
                        {
                            _draggingWindowHwnd = hwnd;
                            _snapDragTimer?.Start();
                        }
                    });
                }
            };
            _altDragService.AltMoveEnded += hwnd =>
            {
                var primaryForInvoke = GetPrimaryMonitorHost();
                if (primaryForInvoke is not null && !primaryForInvoke.CrownForm.IsDisposed && primaryForInvoke.CrownForm.IsHandleCreated)
                {
                    primaryForInvoke.CrownForm.BeginInvoke(() => CompleteSnapZonesDrag(hwnd));
                }
            };
            _altDragService.DockAltDragStarted += (dockHwnd, isResize, startPt) =>
            {
                var primaryForInvoke = GetPrimaryMonitorHost();
                if (primaryForInvoke is not null && !primaryForInvoke.CrownForm.IsDisposed && primaryForInvoke.CrownForm.IsHandleCreated)
                {
                    primaryForInvoke.CrownForm.BeginInvoke(() => HandleDockAltDragStarted(isResize, startPt));
                }
            };
            _altDragService.DockAltDragMoved += (dockHwnd, isResize, startPt, curPt) =>
            {
                var primaryForInvoke = GetPrimaryMonitorHost();
                if (primaryForInvoke is not null && !primaryForInvoke.CrownForm.IsDisposed && primaryForInvoke.CrownForm.IsHandleCreated)
                {
                    primaryForInvoke.CrownForm.BeginInvoke(() => HandleDockAltDragMoved(isResize, startPt, curPt));
                }
            };
            _altDragService.DockAltDragEnded += (dockHwnd, isResize, startPt, endPt) =>
            {
                var primaryForInvoke = GetPrimaryMonitorHost();
                if (primaryForInvoke is not null && !primaryForInvoke.CrownForm.IsDisposed && primaryForInvoke.CrownForm.IsHandleCreated)
                {
                    primaryForInvoke.CrownForm.BeginInvoke(async () => await HandleDockAltDragEndedAsync(isResize, startPt, endPt));
                }
            };
            _altDragService.SetEnabled(_shellPrefs.AltDragEnabled && notSmoke);

            // Automatic GitHub Releases update checker (12s after boot + every 6 hours, non-blocking)
            _autoUpdateTimer = new System.Windows.Forms.Timer { Interval = 6 * 60 * 60 * 1000 };
            _autoUpdateTimer.Tick += async (_, _) => await CheckForUpdatesCoreAsync(sourceMonitorId: null, isAutomatic: true);
            if (notSmoke && _shellPrefs.AutoCheckUpdatesEnabled)
            {
                _autoUpdateTimer.Start();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(12000);
                        var primaryForUpd = GetPrimaryMonitorHost();
                        if (primaryForUpd is not null && !primaryForUpd.CrownForm.IsDisposed && primaryForUpd.CrownForm.IsHandleCreated)
                        {
                            primaryForUpd.CrownForm.BeginInvoke(async () =>
                                await CheckForUpdatesCoreAsync(sourceMonitorId: null, isAutomatic: true));
                        }
                    }
                    catch { }
                });
            }

            await Task.Delay(350);
            PostStaticCatalogToAllCrowns();
            await RefreshAndBroadcastStateAsync(forceFull: true);

            // Phase 2: Hydrate crisp 32-bit icons for all 250+ UWP/MSIX/Win32 apps on a dedicated background STA thread!
            var iconHydrationThread = new Thread(() =>
            {
                try
                {
                    EnsureInstalledAppsCached(force: true, includeIcons: true);
                    var primaryForInvoke = GetPrimaryMonitorHost();
                    if (primaryForInvoke is not null && !primaryForInvoke.CrownForm.IsDisposed && primaryForInvoke.CrownForm.IsHandleCreated)
                    {
                        primaryForInvoke.CrownForm.BeginInvoke(() => PostStaticCatalogToAllCrowns(forceRebuild: false));
                    }
                }
                catch { }
            })
            {
                IsBackground = true,
                Name = "Aurora-IconHydration"
            };
            iconHydrationThread.SetApartmentState(ApartmentState.STA);
            iconHydrationThread.Start();

            if (_args.Contains("--studio"))
            {
                var primaryHost = GetPrimaryMonitorHost();
                primaryHost?.CrownForm.FocusAndActivateSurface();
                primaryHost?.CrownForm.PostJsonState("""{"type":"toggleDeck","tab":"studio"}""");
            }
            else if (ParseSmokeExitSeconds() is null)
            {
                SendToast(
                    null,
                    $"Aurora {_monitorHosts.Count} Ekranda Aktif",
                    "Üstteki damla adanın üzerine gelin veya Alt+Space ile aramayı açın.");
            }

            // Event-driven Win32 window hook with 65ms coalescing -> instant Dock/Crown updates without busy polling
            _windowEventDebounceTimer = new System.Windows.Forms.Timer { Interval = 65 };
            _windowEventDebounceTimer.Tick += async (_, _) =>
            {
                _windowEventDebounceTimer?.Stop();
                await RefreshAndBroadcastStateAsync(windowTopologyOnly: true);
            };
            _windowEventHook.WindowEventObserved += (eventType, hwnd) =>
            {
                // Instant (<1ms) zero-peek lockdown: if Windows tries to show Shell_TrayWnd or Shell_SecondaryTrayWnd, hide immediately
                if (eventType == Win32WindowEventHook.EventObjectShow && _appBarService.IsNativeTaskbarSuppressed)
                {
                    _appBarService.EnforceNativeTaskbarSuppressed(hwnd);
                }

                var owned = CollectOwnedHwnds();
                if (owned.Contains(hwnd))
                    return;

                // Interactive Shift+Drag Snap Zones lifecycle (0% CPU when not dragging)
                if (eventType == Win32WindowEventHook.EventSystemMoveSizeStart)
                {
                    if (_shellPrefs.SnapZonesEnabled && !_allMonitorsFullscreen)
                    {
                        _draggingWindowHwnd = hwnd;
                        _snapDragTimer?.Start();
                    }
                    return;
                }
                if (eventType == Win32WindowEventHook.EventSystemMoveSizeEnd)
                {
                    CompleteSnapZonesDrag(hwnd);
                    if (_windowEventDebounceTimer is not null)
                    {
                        _windowEventDebounceTimer.Stop();
                        _windowEventDebounceTimer.Start();
                    }
                    return;
                }

                if (_allMonitorsFullscreen || _windowEventDebounceTimer is null)
                    return;

                if (eventType == Win32WindowEventHook.EventSystemAlert && hwnd != _lastExternalActiveHwnd)
                {
                    var alertWin = _taskbarVm.OpenWindows.FirstOrDefault(w => w.Hwnd == hwnd);
                    if (alertWin is not null && !alertWin.IsActive)
                        _attentionService.MarkAttention(alertWin.ProcessName);
                }

                _windowEventDebounceTimer.Stop();
                _windowEventDebounceTimer.Start();
            };
            _windowEventHook.Start();

            // Relaxed 3000ms passive heartbeat (window/audio/clipboard events are instant via native Win32 hooks); yields during fullscreen gaming
            _syncTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _syncTimer.Tick += async (_, _) =>
            {
                if (_awakeService.CheckExpiration())
                {
                    SendToast(null, "Uyanık Tut Süresi Doldu", "Normal Windows güç yönetimine dönüldü.");
                }
                if (!_allMonitorsFullscreen)
                    await RefreshAndBroadcastStateAsync(windowTopologyOnly: false);
            };
            _syncTimer.Start();

            // Featherweight 1-second clock-only ticker when Clock Seconds is ON (zero WMI/SQLite/SMTC/DOM rebuild overhead!)
            _clockTickTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _clockTickTimer.Tick += (_, _) =>
            {
                if (!_shellPrefs.ShowClockSeconds || _allMonitorsFullscreen)
                    return;
                string clockPayload = JsonSerializer.Serialize(new { type = "clockTick", clock = DateTime.Now.ToString("HH:mm:ss") }, JsonOpts);
                foreach (var host in _monitorHosts.Values)
                {
                    if (!host.CrownForm.IsSuppressedForFullscreen)
                        host.CrownForm.PostJsonState(clockPayload);
                }
            };
            if (_shellPrefs.ShowClockSeconds)
                _clockTickTimer.Start();

            _focusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _focusTimer.Tick += async (_, _) => await TickFocusAsync();

            // Live 1500ms sparkline & Top-3 process sampler — runs ONLY while Telemetry tab is open (0% idle overhead!)
            _metricsLiveTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            _metricsLiveTimer.Tick += async (_, _) =>
            {
                if (_metricsTabActive && !_allMonitorsFullscreen)
                {
                    await _widgetVm.RefreshTelemetryIfVisibleAsync();
                    RecordTelemetrySparklineSample();
                    await RefreshAndBroadcastStateAsync(windowTopologyOnly: true);
                }
            };

            // Bluetooth Battery background poller (every 18s on threadpool, 0ms UI stall!) + initial DDC/CI monitor discovery
            _btBatteryTimer = new System.Windows.Forms.Timer { Interval = 18000 };
            _btBatteryTimer.Tick += (_, _) => _ = PollBluetoothBatteriesBackgroundAsync();
            if (notSmoke)
            {
                _btBatteryTimer.Start();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var foundMons = _ddcBrightnessService.DiscoverMonitors();
                        if (foundMons.Count > 0)
                        {
                            var prim = GetPrimaryMonitorHost();
                            if (prim is not null && !prim.CrownForm.IsDisposed && prim.CrownForm.IsHandleCreated)
                                prim.CrownForm.BeginInvoke(async () => await RefreshAndBroadcastStateAsync(forceFull: true));
                        }
                        await Task.Delay(2400);
                        await PollBluetoothBatteriesBackgroundAsync(forceBroadcast: true);
                    }
                    catch { }
                });
            }

            // Local LAN QR AirDrop session updates -> live Crown card refresh
            _lanQrShareService.SessionChanged += () =>
            {
                var primaryForQr = GetPrimaryMonitorHost();
                if (primaryForQr is not null && !primaryForQr.CrownForm.IsDisposed && primaryForQr.CrownForm.IsHandleCreated)
                {
                    primaryForQr.CrownForm.BeginInvoke(async () => await RefreshAndBroadcastStateAsync(forceFull: true));
                }
            };

            // Screen GIF Recorder live elapsed ticker + completion callback
            _gifRecorder.ElapsedChanged += elapsedSec =>
            {
                var primaryForRec = GetPrimaryMonitorHost();
                if (primaryForRec is not null && !primaryForRec.CrownForm.IsDisposed && primaryForRec.CrownForm.IsHandleCreated)
                {
                    primaryForRec.CrownForm.BeginInvoke(() => BroadcastGifRecordingState(true, elapsedSec));
                }
            };
            _gifRecorder.RecordingFinished += outcome =>
            {
                var primaryForRec = GetPrimaryMonitorHost();
                if (primaryForRec is not null && !primaryForRec.CrownForm.IsDisposed && primaryForRec.CrownForm.IsHandleCreated)
                {
                    primaryForRec.CrownForm.BeginInvoke(async () =>
                    {
                        BroadcastGifRecordingState(false, 0);
                        string? srcMon = _gifRecSourceMonitorId;
                        _gifRecSourceMonitorId = null;
                        if (outcome.Success && File.Exists(outcome.FilePath))
                        {
                            await HandleDroppedFilesAsync(srcMon ?? string.Empty, [outcome.FilePath]);
                            _suppressNextClipboardCapture = true;
                            try { Clipboard.SetText(outcome.FilePath); } catch { }
                            SendToast(srcMon,
                                $"GIF Kaydedildi ({outcome.DurationSeconds:0.#} sn · {LanQrFileShareService.FormatSize(outcome.SizeBytes)})",
                                $"{Path.GetFileName(outcome.FilePath)} — Süper Rafa eklendi; QR ile telefona gönderebilirsiniz.");
                        }
                        else
                        {
                            SendToast(srcMon, "GIF Kaydı Tamamlanamadı", outcome.ErrorMessage ?? "Kayıt iptal edildi.");
                        }
                        await RefreshAndBroadcastStateAsync(forceFull: true);
                    });
                }
            };

            // 400ms lightweight per-monitor fullscreen detector + native taskbar zero-peek guard
            _fullscreenTimer = new System.Windows.Forms.Timer { Interval = 400 };
            _fullscreenTimer.Tick += (_, _) =>
            {
                _appBarService.EnforceNativeTaskbarSuppressed();
                EvaluateFullscreenYieldPerMonitor();
            };
            _fullscreenTimer.Start();

            _ = RunPostBootDiagnosticsAndCaptureAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("Coordinator", "BOOT_ERR", $"Boot error: {ex.Message}", ex);
            EnsureNativeTaskbarRestored();
        }
    }

    private void OnSessionEnding(object? sender, SessionEndingEventArgs e)
    {
        EnsureNativeTaskbarRestored();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        var primary = GetPrimaryMonitorHost();
        if (primary is not null && !primary.CrownForm.IsDisposed)
        {
            primary.CrownForm.BeginInvoke(async () =>
            {
                await Task.Delay(350);
                _appBarService.EnforceNativeTaskbarSuppressed();
                await ReconcileMonitorHostsAsync();
                _ = Task.Run(() => { try { _ddcBrightnessService.DiscoverMonitors(); } catch { } });
                await Task.Delay(300);
                PostStaticCatalogToAllCrowns();
                _lastCrownJsonByMonitor.Clear();
                _lastDockJsonByMonitor.Clear();
                await RefreshAndBroadcastStateAsync(forceFull: true);
            });
        }
    }

    private async Task ReconcileMonitorHostsAsync()
    {
        if (_reconcilingMonitors || _sharedWebViewEnv is null)
            return;

        _reconcilingMonitors = true;
        try
        {
            var monitors = _monitorService.GetMonitors();
            var activeIds = monitors.Select(m => m.DeviceId).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Remove hosts for unplugged monitors
            foreach (var staleId in _monitorHosts.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            {
                _monitorHosts[staleId].Dispose();
                _monitorHosts.Remove(staleId);
            }

            string crownHtml = AuroraSurfaceTemplates.BuildCrownHtml();
            string dockHtml = AuroraSurfaceTemplates.BuildCrystalDockHtml();

            // Add new monitors or update existing monitor bounds
            foreach (var mon in monitors)
            {
                var (crownRect, dockRect) = ComputeSurfaceRectsForMonitor(mon);
                if (_monitorHosts.TryGetValue(mon.DeviceId, out var existing))
                {
                    existing.Monitor = mon;
                    existing.CrownForm.SetPhysicalBounds(crownRect);
                    existing.DockForm.SetPhysicalBounds(dockRect);
                }
                else
                {
                    var crown = new TransparentSurfaceForm($"Aurora Crown ({mon.DeviceId})", mon.DeviceId, crownRect);
                    var dock = new TransparentSurfaceForm($"Aurora Dock ({mon.DeviceId})", mon.DeviceId, dockRect)
                    {
                        EnableNativeCursorWake = true
                    };

                    crown.MessageReceived += HandleSurfaceCommandAsync;
                    dock.MessageReceived += HandleSurfaceCommandAsync;
                    crown.FilesDropped += HandleDroppedFilesAsync;
                    crown.FilesDroppedWithContext += HandleCrownFilesDroppedWithContextAsync;
                    dock.FilesDroppedWithContext += HandleDockFilesDroppedAsync;

                    crown.Show();
                    dock.Show();
                    crown.SetPhysicalBounds(crownRect);
                    dock.SetPhysicalBounds(dockRect);

                    await crown.InitializeHtmlAsync(crownHtml, _sharedWebViewEnv);
                    await dock.InitializeHtmlAsync(dockHtml, _sharedWebViewEnv);

                    _monitorHosts[mon.DeviceId] = new MonitorSurfaceHost(mon, crown, dock);
                }
            }

            // Re-suppress secondary native taskbars if a new monitor was hot-plugged while Aurora hides native bars
            if (_appBarService.IsNativeTaskbarSuppressed)
            {
                _appBarService.EnforceNativeTaskbarSuppressed();
            }
        }
        catch (Exception ex)
        {
            _logger.Warn("Coordinator", "MON_RECONCILE_WARN", $"Monitor reconcile warning ({ex.GetType().Name}).");
        }
        finally
        {
            _reconcilingMonitors = false;
        }
    }

    private (Rectangle CrownRect, Rectangle DockRect) ComputeSurfaceRectsForMonitor(MonitorDescriptor mon)
    {
        double scale = mon.ScaleFactor;
        var b = mon.BoundsPx;
        var w = mon.WorkAreaPx;

        // 1. TOP-CENTER LIQUID DROPLET CROWN & POPUP CANVAS (generous width so Bell + Pin + Expand never clip even with seconds & media!)
        int crownW = Math.Min(b.Width - 12, (int)Math.Round(1260 * scale));
        int crownH = Math.Min(b.Height - 24, (int)Math.Round(640 * scale));
        int crownX = b.X + ((b.Width - crownW) / 2);
        int crownY = b.Y;

        // 2. CRYSTAL DOCK CANVAS — Supports Bottom (horizontal), Left (vertical), and Right (vertical) with full-edge Alt+Drag positioning!
        string edge = _shellPrefs.EffectiveDockEdge;
        Rectangle dockRect;
        if (edge == "Left")
        {
            int dockW = Math.Min(b.Width / 2, (int)Math.Round(460 * scale));
            int dockH = Math.Max(320, b.Height - (int)Math.Round(16 * scale));
            int dockX = b.X;
            int dockY = b.Y + ((b.Height - dockH) / 2);
            dockRect = new Rectangle(dockX, dockY, dockW, dockH);
        }
        else if (edge == "Right")
        {
            int dockW = Math.Min(b.Width / 2, (int)Math.Round(460 * scale));
            int dockH = Math.Max(320, b.Height - (int)Math.Round(16 * scale));
            int dockX = b.Right - dockW;
            int dockY = b.Y + ((b.Height - dockH) / 2);
            dockRect = new Rectangle(dockX, dockY, dockW, dockH);
        }
        else
        {
            int dockW = Math.Max(320, b.Width - 12);
            int dockH = Math.Min(b.Height / 2, (int)Math.Round(450 * scale));
            int dockX = b.X + ((b.Width - dockW) / 2);
            int bottomEdgePx = _appBarService.IsNativeTaskbarSuppressed ? b.Bottom : w.Bottom;
            int dockY = bottomEdgePx - dockH;
            dockRect = new Rectangle(dockX, dockY, dockW, dockH);
        }

        return (new Rectangle(crownX, crownY, crownW, crownH), dockRect);
    }

    private void RecomputeAndApplyAllMonitorBounds()
    {
        var freshMonitors = _monitorService.GetMonitors().ToDictionary(m => m.DeviceId, StringComparer.OrdinalIgnoreCase);
        foreach (var (id, host) in _monitorHosts)
        {
            if (freshMonitors.TryGetValue(id, out var fresh))
                host.Monitor = fresh;

            var (crownRect, dockRect) = ComputeSurfaceRectsForMonitor(host.Monitor);
            host.CrownForm.SetPhysicalBounds(crownRect);
            host.DockForm.SetPhysicalBounds(dockRect);
        }
    }

    private void EvaluateFullscreenYieldPerMonitor()
    {
        if (_monitorHosts.Count == 0)
            return;

        // Skip fullscreen hiding during automated PNG capture runs
        if (ParseSmokeExitSeconds() is not null)
            return;

        var ownedHwnds = CollectOwnedHwnds();
        var monitors = _monitorHosts.Values.Select(h => h.Monitor).ToList();
        var (fullscreenIds, overlappedIds) = _fullscreenDetector.AnalyzeMonitors(monitors, ownedHwnds, _shellPrefs.EffectiveDockEdge);

        int fsCount = 0;
        bool anyRestored = false;
        bool smartDodgeActive = string.Equals(_shellPrefs.EffectiveDockMode, "SmartDodge", StringComparison.OrdinalIgnoreCase);

        foreach (var (id, host) in _monitorHosts)
        {
            bool wasFs = host.CrownForm.IsSuppressedForFullscreen;
            bool isFs = fullscreenIds.Contains(id);
            if (isFs) fsCount++;

            host.CrownForm.SetSuppressedForFullscreen(isFs);
            host.DockForm.SetSuppressedForFullscreen(isFs);

            if (wasFs && !isFs)
            {
                _lastCrownJsonByMonitor.Remove(id);
                _lastDockJsonByMonitor.Remove(id);
                anyRestored = true;
            }

            if (smartDodgeActive && !isFs)
            {
                bool wasDodged = _dockOverlappedMonitors.Contains(id);
                bool nowDodged = overlappedIds.Contains(id);
                if (wasDodged != nowDodged)
                {
                    host.DockForm.PostJsonState(JsonSerializer.Serialize(new { type = "dodgeState", dodged = nowDodged }, JsonOpts));
                }
            }
        }

        _dockOverlappedMonitors = overlappedIds;
        _allMonitorsFullscreen = _monitorHosts.Count > 0 && fsCount == _monitorHosts.Count;
        if (anyRestored)
        {
            _ = RefreshAndBroadcastStateAsync(windowTopologyOnly: true);
        }
    }

    private HashSet<nint> CollectOwnedHwnds()
    {
        var owned = new HashSet<nint>();
        foreach (var h in _monitorHosts.Values)
        {
            if (!h.CrownForm.IsDisposed && h.CrownForm.IsHandleCreated) owned.Add(h.CrownForm.Handle);
            if (!h.DockForm.IsDisposed && h.DockForm.IsHandleCreated) owned.Add(h.DockForm.Handle);
        }
        if (_dwmPreviewForm is not null && !_dwmPreviewForm.IsDisposed && _dwmPreviewForm.IsHandleCreated)
            owned.Add(_dwmPreviewForm.Handle);
        if (_snapZonesOverlay is not null && !_snapZonesOverlay.IsDisposed && _snapZonesOverlay.IsHandleCreated)
            owned.Add(_snapZonesOverlay.Handle);
        if (_dockDragOverlay is not null && !_dockDragOverlay.IsDisposed && _dockDragOverlay.IsHandleCreated)
            owned.Add(_dockDragOverlay.Handle);
        return owned;
    }

    private MonitorSurfaceHost? GetPrimaryMonitorHost() =>
        _monitorHosts.Values.FirstOrDefault(h => h.Monitor.IsPrimary)
        ?? _monitorHosts.Values.FirstOrDefault();

    private MonitorSurfaceHost? GetMonitorHostById(string? deviceId) =>
        (!string.IsNullOrWhiteSpace(deviceId) && _monitorHosts.TryGetValue(deviceId, out var h))
            ? h
            : GetPrimaryMonitorHost();

    private MonitorSurfaceHost? GetMonitorHostUnderCursor()
    {
        var pt = Cursor.Position;
        foreach (var h in _monitorHosts.Values)
        {
            var b = h.Monitor.BoundsPx;
            if (pt.X >= b.X && pt.X < b.Right && pt.Y >= b.Y && pt.Y < b.Bottom)
                return h;
        }
        return GetPrimaryMonitorHost();
    }

    private void SpawnCrashWatchdogIfNeeded()
    {
        try
        {
            if (_watchdogProcess is { HasExited: false })
                return;

            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                return;

            int origState = _appBarService.OriginalNativeTaskbarState ?? 0;
            _watchdogProcess = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"--watchdog-for-pid={Environment.ProcessId} --orig-state={origState}",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch (Exception ex)
        {
            _logger.Warn("Coordinator", "WATCHDOG_SPAWN_WARN", $"Could not spawn taskbar crash watchdog ({ex.GetType().Name}).");
        }
    }

    private string? EncodePngMemoized(byte[]? pngBytes)
    {
        if (pngBytes is not { Length: > 0 })
            return null;
        return _base64IconMemo.GetValue(pngBytes, static b => Convert.ToBase64String(b));
    }

    private object[] EnsureInstalledAppsCached(bool force = false, bool includeIcons = true)
    {
        if (!force && _cachedInstalledAppDtos is not null && (DateTimeOffset.UtcNow - _installedAppsCachedUtc).TotalMinutes < 10)
            return _cachedInstalledAppDtos;

        var apps = _appCatalog.GetInstalledApps(forceRefresh: force, includeIcons: includeIcons);
        _cachedInstalledAppDtos = apps.Select(a => (object)new
        {
            id = a.Id,
            name = a.Name,
            path = a.LaunchPath,
            category = a.Category,
            keywords = a.Keywords,
            kind = a.Kind,
            icon = EncodePngMemoized(a.IconPngBytes)
        }).ToArray();
        _installedAppsCachedUtc = DateTimeOffset.UtcNow;
        _cachedStaticCatalogJson = null;
        return _cachedInstalledAppDtos;
    }

    private void PostStaticCatalogToAllCrowns(bool forceRebuild = false)
    {
        var appsDto = EnsureInstalledAppsCached(forceRebuild);
        if (_cachedStaticCatalogJson is null || forceRebuild)
        {
            var themesDto = BuiltInThemes.All.Select(t => new
            {
                id = t.Id,
                name = t.DisplayName,
                mode = t.ColorMode == AuroraColorMode.Light ? "Açık Kristal" : "Koyu Cam",
                accent = t.Colors.AccentPrimary
            }).ToArray();

            _cachedStaticCatalogJson = JsonSerializer.Serialize(new
            {
                type = "catalog",
                installedApps = appsDto,
                themes = themesDto,
                frecency = _searchFrecency
            }, JsonOpts);
        }

        foreach (var host in _monitorHosts.Values)
        {
            host.CrownForm.PostJsonState(_cachedStaticCatalogJson);
        }
    }

    private async Task RefreshAndBroadcastStateAsync(bool windowTopologyOnly = false, bool forceFull = false)
    {
        if (_refreshInFlight && !forceFull)
            return;

        _refreshInFlight = true;
        try
        {
            var owned = CollectOwnedHwnds();
            nint currentFg = _windowCatalog.GetForegroundWindowHandle();
            if (currentFg != 0 && !owned.Contains(currentFg))
            {
                _lastExternalActiveHwnd = currentFg;
                RecordFocusedHwnd(currentFg);
            }

            _taskbarVm.RefreshSnapshot(owned);
            if (_lastExternalActiveHwnd != 0)
            {
                var fgGroup = _taskbarVm.Groups.FirstOrDefault(g => g.Windows.Any(w => w.Hwnd == _lastExternalActiveHwnd));
                if (fgGroup is not null)
                    _lastFocusedWindowByGroup[fgGroup.GroupKey] = _lastExternalActiveHwnd;
            }

            if (!windowTopologyOnly || forceFull)
            {
                SampleClipboardSafely();
                await _mediaVm.RefreshSessionsAsync();
                await _ccVm.RefreshAllAsync();
                if (_metricsTabActive || forceFull)
                {
                    _widgetVm.IsSurfaceVisible = true;
                    await _widgetVm.RefreshTelemetryIfVisibleAsync();
                    RecordTelemetrySparklineSample();
                    _widgetVm.IsSurfaceVisible = _metricsTabActive;
                }
                _cachedPrivacy = _privacyService.GetCurrentSnapshot();
            }

            var activeWin = _taskbarVm.OpenWindows.FirstOrDefault(w => w.IsActive)
                            ?? _taskbarVm.OpenWindows.FirstOrDefault(w => w.Hwnd == _lastExternalActiveHwnd)
                            ?? _taskbarVm.OpenWindows.FirstOrDefault();
            if (activeWin is not null)
                _attentionService.ClearAttention(activeWin.ProcessName);

            var theme = _stateStore.EffectiveTheme;
            var m = _widgetVm.Metrics;
            var wx = _widgetVm.Weather;
            var media = _mediaVm.SelectedSession;
            var kb = _keyboardService.GetActiveLayout(_lastExternalActiveHwnd);
            var openProcNames = _taskbarVm.OpenWindows.Select(w => w.ProcessName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var trayApps = _trayService.GetBackgroundTrayApps(openProcNames);

            var dropShelfDto = BuildDropShelfDtos();
            var trayDtos = trayApps.Select(a => new
            {
                key = a.Key,
                name = a.DisplayName,
                subtitle = a.Subtitle,
                memoryMb = a.MemoryMb,
                canQuit = a.CanQuitCleanly,
                icon = EncodePngMemoized(a.IconPngBytes)
            }).ToArray();

            var clockStr = DateTime.Now.ToString(_shellPrefs.ShowClockSeconds ? "HH:mm:ss" : "HH:mm");
            var shortDateStr = DateTime.Now.ToString("dd MMM", CultureInfo.GetCultureInfo("tr-TR"));
            var themeDto = new
            {
                id = theme.Id,
                displayName = theme.DisplayName,
                colorMode = theme.ColorMode.ToString(),
                colors = new
                {
                    surface1 = theme.Colors.Surface1,
                    surface2 = theme.Colors.Surface2,
                    textPrimary = theme.Colors.TextPrimary,
                    textSecondary = theme.Colors.TextSecondary,
                    accentPrimary = theme.Colors.AccentPrimary,
                    accentSecondary = theme.Colors.AccentSecondary
                }
            };
            var profilesDto = _stateStore.Profiles.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                active = p.Id == _stateStore.ActiveProfile.Id
            }).ToArray();
            var openWinsDto = _taskbarVm.OpenWindows.Take(18).Select(w => new
            {
                hwnd = (long)w.Hwnd,
                title = w.Title,
                processName = FormatName(w.ProcessName),
                isTopMost = w.IsTopMost,
                isActive = w.IsActive,
                monitorId = w.MonitorDeviceId,
                icon = EncodePngMemoized(w.IconPngBytes)
            }).ToArray();

            var allGroupsDto = MapGroupsToDto(_taskbarVm.Groups, filterMonitorId: null);
            var vdSnap = _virtualDesktops.GetSnapshot();
            var virtualDesktopDto = new
            {
                index = vdSnap.CurrentIndex,
                count = vdSnap.TotalCount,
                name = vdSnap.ActiveName,
                list = vdSnap.Desktops.Select(d => new { index = d.Index, name = d.Name, current = d.IsCurrent }).ToArray()
            };

            var mergedNotifs = new List<object>();
            if (_shellPrefs.SystemNotificationBridgeEnabled)
            {
                foreach (var st in _sysNotifBridge.GetRecentSystemToasts().Where(s => !_dismissedSystemNotifIds.Contains(s.NotificationId)).Take(10))
                {
                    mergedNotifs.Add(new
                    {
                        id = $"sys-{st.NotificationId}",
                        app = st.AppName,
                        title = st.Title,
                        body = st.Body,
                        time = st.ArrivedAtUtc.ToLocalTime().ToString("HH:mm"),
                        isSystem = true
                    });
                }
            }
            foreach (var n in _stateStore.Notifications.Take(6))
            {
                mergedNotifs.Add(new
                {
                    id = n.Id,
                    app = n.SourceModule,
                    title = n.Title,
                    body = n.Body,
                    time = n.CreatedAtUtc.ToLocalTime().ToString("HH:mm"),
                    isSystem = false
                });
            }

            var effectiveDockFolders = GetEffectiveDockFolders();
            var dockFoldersDto = effectiveDockFolders
                .Where(Directory.Exists)
                .Take(4)
                .Select(p => new
                {
                    path = p,
                    name = DockFolderStackService.GetFriendlyFolderName(p)
                })
                .ToArray();

            string effectiveDockMode = _shellPrefs.EffectiveDockMode;
            string effectiveDockDensity = _shellPrefs.EffectiveDockDensity;
            string effectiveDockEdge = _shellPrefs.EffectiveDockEdge;
            var awakeSnap = _awakeService.GetSnapshot();
            var clipEntries = _clipboardVaultStore.GetItems(limit: 40);
            var clipboardDto = clipEntries.Select(c => new
            {
                id = c.Id,
                kind = c.Kind,
                preview = c.PreviewText,
                text = c.Kind == "text" ? (c.TextContent.Length > 400 ? c.TextContent[..400] : c.TextContent) : "",
                thumb = c.ThumbBase64,
                width = c.ImageWidth,
                height = c.ImageHeight,
                sourceApp = c.SourceApp,
                pinned = c.IsPinned,
                time = c.LastCopiedUtc.ToLocalTime().ToString("HH:mm")
            }).ToArray();
            int clipboardTotalCount = _clipboardVaultStore.GetTotalCount();
            var micState = _audioService.GetDefaultCaptureMuteState();
            var btDevicesDto = _cachedBtDevices.Take(6).Select(d => new
            {
                id = d.Id,
                name = d.Name,
                percent = d.BatteryPercent,
                category = d.Category,
                source = d.Source
            }).ToArray();
            var monBrightDto = _ddcBrightnessService.GetCachedMonitors().Take(5).Select(mb => new
            {
                id = mb.Id,
                name = mb.Name,
                percent = mb.BrightnessPercent,
                isInternal = mb.IsInternal,
                isPrimary = mb.IsPrimary
            }).ToArray();
            var qrActive = _lanQrShareService.GetActiveSession();
            var qrShareDto = qrActive is null ? null : new
            {
                url = qrActive.Url,
                name = qrActive.DisplayName,
                size = qrActive.SizeLabel,
                svg = qrActive.QrSvg,
                downloads = qrActive.DownloadCount
            };
            var topProcsDto = _topProcesses.Select(tp => new
            {
                pid = tp.Pid,
                name = tp.Name,
                displayName = tp.DisplayName,
                memoryMb = tp.MemoryMb,
                instances = tp.Instances
            }).ToArray();
            var audioOutputsDto = _ccVm.AudioEndpoints
                .Where(e => !e.IsCapture)
                .Take(6)
                .Select(e => new
                {
                    id = e.Id,
                    name = ShortAudioDeviceName(e.FriendlyName),
                    fullName = e.FriendlyName,
                    isDefault = e.IsDefault
                })
                .ToArray();
            bool isSmokeCapture = ParseSmokeExitSeconds() is not null;

            foreach (var host in _monitorHosts.Values)
            {
                string monId = host.Monitor.DeviceId;

                // Skip pushing JSON updates to screens currently hidden behind fullscreen video/games
                if (host.CrownForm.IsSuppressedForFullscreen && host.DockForm.IsSuppressedForFullscreen && !forceFull)
                    continue;

                // 1. SLIM DOCK PAYLOAD (zero installedApps / tray icons / weather / metrics overhead!)
                var dockGroupsDto = _shellPrefs.PerMonitorWindowFilter
                    ? MapGroupsToDto(_taskbarVm.Groups, monId)
                    : allGroupsDto;

                var dockPayload = new
                {
                    monitorId = monId,
                    dockAutoHide = effectiveDockMode == "AutoHide",
                    dockMode = effectiveDockMode,
                    dockDensity = effectiveDockDensity,
                    dockEdge = effectiveDockEdge,
                    dockScalePercent = _shellPrefs.EffectiveDockScalePercent,
                    dockOffsetPercent = _shellPrefs.EffectiveDockOffsetPercent,
                    dockDodged = effectiveDockMode == "SmartDodge" && _dockOverlappedMonitors.Contains(monId),
                    smartTidy = _shellPrefs.SmartTidyEnabled,
                    dockMagnification = _shellPrefs.DockMagnificationPercent,
                    dwmLivePreview = _shellPrefs.DwmLivePreviewEnabled,
                    trayCount = trayDtos.Length,
                    dockFolders = dockFoldersDto,
                    theme = themeDto,
                    groups = dockGroupsDto
                };
                var dockJson = JsonSerializer.Serialize(new { type = "state", payload = dockPayload }, JsonOpts);
                if (forceFull || !_lastDockJsonByMonitor.TryGetValue(monId, out var prevDock) || !string.Equals(prevDock, dockJson, StringComparison.Ordinal))
                {
                    _lastDockJsonByMonitor[monId] = dockJson;
                    host.DockForm.PostJsonState(dockJson);
                }

                // 2. LEAN CROWN PAYLOAD (static 280+ app catalog & themes are sent once via 'catalog', never repeated!)
                var crownPayload = new
                {
                    monitorId = monId,
                    monitorCount = _monitorHosts.Count,
                    clock = clockStr,
                    shortDate = shortDateStr,
                    keyboard = new { code = kb.ShortCode, name = kb.DisplayName },
                    virtualDesktop = virtualDesktopDto,
                    unreadNotifCount = _unreadNotifCount,
                    dockMode = effectiveDockMode,
                    dockDensity = effectiveDockDensity,
                    dockEdge = effectiveDockEdge,
                    dockScalePercent = _shellPrefs.EffectiveDockScalePercent,
                    dockOffsetPercent = _shellPrefs.EffectiveDockOffsetPercent,
                    hardwareOsdEnabled = _shellPrefs.HardwareOsdEnabled,
                    altDragEnabled = _shellPrefs.AltDragEnabled,
                    showClockSeconds = _shellPrefs.ShowClockSeconds,
                    snapZonesEnabled = _shellPrefs.SnapZonesEnabled,
                    snapZonePreset = _shellPrefs.EffectiveSnapZonePreset,
                    version = AuroraUpdateService.CurrentVersion,
                    updateStatus = _lastUpdateStatusText,
                    updateAvailable = _pendingUpdateManifest is not null,
                    updateLatestVersion = _pendingUpdateManifest?.Version,
                    updateReleaseNotes = _pendingUpdateManifest?.ReleaseNotes ?? string.Empty,
                    updateDownloadUrl = _pendingUpdateDownloadUrl,
                    updateDownloading = _isDownloadingUpdate,
                    updateDownloadPercent = _updateDownloadPercent,
                    autoCheckUpdatesEnabled = _shellPrefs.AutoCheckUpdatesEnabled,
                    repoUrl = AuroraUpdateService.DefaultRepositoryUrl,
                    awake = new
                    {
                        active = awakeSnap.IsActive,
                        mode = awakeSnap.ModeKey,
                        shortLabel = awakeSnap.ShortLabel,
                        fullLabel = awakeSnap.FullLabel
                    },
                    showOnboarding = !_shellPrefs.OnboardingCompleted && host.Monitor.IsPrimary && !isSmokeCapture,
                    profileName = _stateStore.ActiveProfile.Name.Split('&')[0].Trim(),
                    nativeTaskbarHidden = _appBarService.IsNativeTaskbarSuppressed,
                    dockAutoHide = _shellPrefs.DockAutoHide,
                    perMonitorFilter = _shellPrefs.PerMonitorWindowFilter,
                    smartTidy = _shellPrefs.SmartTidyEnabled,
                    dockMagnification = _shellPrefs.DockMagnificationPercent,
                    crownPinned = _shellPrefs.CrownPinned,
                    dwmLivePreview = _shellPrefs.DwmLivePreviewEnabled,
                    sysNotifBridge = _shellPrefs.SystemNotificationBridgeEnabled,
                    winKeyOmni = _shellPrefs.WinKeyOpensOmniSearch,
                    omniHotkeyPreset = _shellPrefs.OmniHotkeyPreset,
                    quietHours = _stateStore.Preferences.Notifications.QuietHoursEnabled,
                    startupEnabled = _startupService.IsStartupEnabled(),
                    scratchpad = _scratchpadText,
                    clipboard = clipboardDto,
                    clipboardCount = clipboardTotalCount,
                    clipboardAutoPaste = _shellPrefs.ClipboardAutoPasteEnabled,
                    btDevices = btDevicesDto,
                    monitorBrightness = monBrightDto,
                    privacy = new
                    {
                        camera = _cachedPrivacy.CameraActive,
                        mic = _cachedPrivacy.MicActive,
                        cameraApps = _cachedPrivacy.CameraApps,
                        micApps = _cachedPrivacy.MicApps
                    },
                    qrShare = qrShareDto,
                    gifRec = new
                    {
                        recording = _gifRecorder.IsRecording,
                        elapsed = _gifRecorder.ElapsedSeconds
                    },
                    topProcesses = topProcsDto,
                    agendaNotes = _agendaNotes,
                    dropShelf = dropShelfDto,
                    trayApps = trayDtos,
                    focus = new
                    {
                        running = _focusRunning,
                        label = $"{(int)_focusRemaining.TotalMinutes:00}:{_focusRemaining.Seconds:00}"
                    },
                    theme = themeDto,
                    profiles = profilesDto,
                    activeWindow = activeWin is null ? null : new
                    {
                        hwnd = (long)activeWin.Hwnd,
                        title = activeWin.Title,
                        processName = FormatName(activeWin.ProcessName),
                        isTopMost = activeWin.IsTopMost,
                        icon = EncodePngMemoized(activeWin.IconPngBytes)
                    },
                    openWindows = openWinsDto,
                    audio = new
                    {
                        volume = (int)Math.Round(_ccVm.MasterVolumePercent),
                        muted = _ccVm.IsMasterMuted,
                        device = _ccVm.DefaultAudioDeviceName
                    },
                    audioOutputs = audioOutputsDto,
                    mic = new
                    {
                        supported = micState.Supported,
                        muted = micState.IsMuted,
                        name = ShortAudioDeviceName(micState.DeviceName)
                    },
                    perAppRoutingSupported = _perAppAudioRoutingSupported,
                    audioSessions = _ccVm.AudioSessions.Take(5).Select(s => new
                    {
                        pid = s.ProcessId,
                        name = s.DisplayName,
                        volume = (int)Math.Round(s.VolumeScalar * 100),
                        routedDeviceId = _perAppAudioRoutingSupported ? (_perAppAudioRouting.GetAppOutputDeviceId(s.ProcessId) ?? string.Empty) : string.Empty
                    }),
                    brightness = new
                    {
                        supported = _ccVm.Brightness.IsSupported,
                        value = _ccVm.Brightness.BrightnessPercent
                    },
                    battery = new
                    {
                        percent = _ccVm.Battery.ChargePercent,
                        status = _ccVm.Battery.StatusText
                    },
                    wifi = new
                    {
                        on = _ccVm.WifiStatus.OperationalState == Aurora.Core.Primitives.ControlOperationalState.On,
                        status = _ccVm.WifiStatus.StatusText
                    },
                    bluetooth = new
                    {
                        on = _ccVm.BluetoothStatus.OperationalState == Aurora.Core.Primitives.ControlOperationalState.On,
                        status = _ccVm.BluetoothStatus.StatusText
                    },
                    media = media is null ? null : new
                    {
                        app = media.SourceAppId,
                        title = media.Title,
                        artist = media.Artist,
                        isPlaying = media.IsPlaying,
                        artwork = EncodePngMemoized(media.ArtworkBytes)
                    },
                    metrics = new
                    {
                        cpu = Math.Round(m.CpuUsagePercent),
                        ramPct = Math.Round(m.RamUsagePercent),
                        ramUsed = m.RamUsedGb,
                        ramTotal = m.RamTotalGb,
                        gpuName = m.GpuAdapterName,
                        rxKbps = m.NetworkDownloadKbps,
                        txKbps = m.NetworkUploadKbps,
                        cpuHist = _cpuHistory.ToArray(),
                        ramHist = _ramHistory.ToArray(),
                        netHist = _netHistory.ToArray()
                    },
                    weather = new
                    {
                        live = wx.IsLiveNetworkData,
                        city = wx.LocationName,
                        temp = wx.TemperatureCelsius,
                        summary = wx.ConditionSummary
                    },
                    notifications = mergedNotifs.Take(8).ToArray()
                };

                var crownJson = JsonSerializer.Serialize(new { type = "state", payload = crownPayload }, JsonOpts);
                if (forceFull || !_lastCrownJsonByMonitor.TryGetValue(monId, out var prevCrown) || !string.Equals(prevCrown, crownJson, StringComparison.Ordinal))
                {
                    _lastCrownJsonByMonitor[monId] = crownJson;
                    host.CrownForm.PostJsonState(crownJson);
                }
            }
        }
        finally
        {
            _refreshInFlight = false;
        }
    }

    private object[] MapGroupsToDto(IEnumerable<AppWindowGroup> groups, string? filterMonitorId)
    {
        var list = new List<object>();
        foreach (var g in groups)
        {
            var filteredWins = string.IsNullOrWhiteSpace(filterMonitorId)
                ? g.Windows
                : g.Windows.Where(w => string.Equals(w.MonitorDeviceId, filterMonitorId, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!g.IsPinned && filteredWins.Count == 0)
                continue;

            bool hasActive = filteredWins.Any(w => w.IsActive);
            string? procSample = filteredWins.FirstOrDefault()?.ProcessName;
            bool hasAttention = !hasActive && _attentionService.HasAttention(g.DisplayName, procSample);
            int? progressPct = WindowAttentionAndProgressService.ExtractProgressPercent(filteredWins.Select(w => w.Title));

            list.Add(new
            {
                key = g.GroupKey,
                displayName = g.DisplayName,
                executablePath = g.ExecutablePath,
                isPinned = g.IsPinned,
                hasActive,
                hasAttention,
                progress = progressPct ?? 0,
                icon = EncodePngMemoized(g.IconPngBytes),
                windows = filteredWins.Select(w => new
                {
                    hwnd = (long)w.Hwnd,
                    title = w.Title,
                    processName = w.ProcessName,
                    isActive = w.IsActive,
                    isTopMost = w.IsTopMost
                })
            });
        }
        return [.. list];
    }

    private async Task HandleSurfaceCommandAsync(string sourceMonitorId, JsonElement msg)
    {
        if (!msg.TryGetProperty("action", out var actProp))
            return;

        var sourceHost = GetMonitorHostById(sourceMonitorId);
        string action = actProp.GetString() ?? string.Empty;

        switch (action)
        {
            case "requestInteractiveFocus":
                sourceHost?.CrownForm.FocusAndActivateSurface();
                break;

            case "releaseInteractiveFocus":
                sourceHost?.CrownForm.ReleaseForcedInteractive();
                break;

            case "openOmni":
                sourceHost?.CrownForm.FocusAndActivateSurface();
                sourceHost?.CrownForm.PostJsonState("""{"type":"toggleOmni"}""");
                break;

            case "openTray":
                sourceHost?.CrownForm.FocusAndActivateSurface();
                sourceHost?.CrownForm.PostJsonState("""{"type":"toggleTray"}""");
                break;

            case "invokeGroup":
                {
                    var key = msg.GetProperty("key").GetString();
                    var grp = _taskbarVm.Groups.FirstOrDefault(g => g.GroupKey == key);
                    if (grp is not null)
                        _taskbarVm.InvokeGroupPrimaryAction(grp);
                    await RefreshAndBroadcastStateAsync(windowTopologyOnly: true);
                    break;
                }

            case "cycleGroupWindow":
                {
                    var key = msg.GetProperty("key").GetString() ?? string.Empty;
                    int delta = msg.TryGetProperty("delta", out var dProp) ? dProp.GetInt32() : 1;
                    bool fromWheel = msg.TryGetProperty("fromWheel", out var wProp) && wProp.GetBoolean();
                    var grp = _taskbarVm.Groups.FirstOrDefault(g => g.GroupKey == key);
                    if (grp is not null)
                    {
                        var wins = _shellPrefs.PerMonitorWindowFilter
                            ? grp.Windows.Where(w => string.Equals(w.MonitorDeviceId, sourceMonitorId, StringComparison.OrdinalIgnoreCase)).ToList()
                            : grp.Windows.ToList();
                        if (wins.Count == 0 && grp.Windows.Count > 0)
                            wins = grp.Windows.ToList();

                        if (wins.Count == 0)
                        {
                            _taskbarVm.InvokeGroupPrimaryAction(grp);
                        }
                        else if (wins.Count == 1)
                        {
                            if (fromWheel)
                                _windowActivation.FocusWindow(wins[0].Hwnd);
                            else
                                _windowActivation.ActivateOrToggleWindow(wins[0].Hwnd);
                            RecordFocusedHwnd(wins[0].Hwnd, key);
                        }
                        else
                        {
                            int activeIdx = wins.FindIndex(w => w.IsActive || w.Hwnd == _lastExternalActiveHwnd);
                            nint targetHwnd;
                            if (activeIdx >= 0)
                            {
                                int step = delta >= 0 ? 1 : -1;
                                int nextIdx = (activeIdx + step + wins.Count) % wins.Count;
                                targetHwnd = wins[nextIdx].Hwnd;
                            }
                            else if (_lastFocusedWindowByGroup.TryGetValue(key, out var remembered) && wins.Any(w => w.Hwnd == remembered))
                            {
                                int remIdx = wins.FindIndex(w => w.Hwnd == remembered);
                                if (fromWheel)
                                {
                                    int step = delta >= 0 ? 1 : -1;
                                    targetHwnd = wins[(remIdx + step + wins.Count) % wins.Count].Hwnd;
                                }
                                else
                                {
                                    targetHwnd = remembered;
                                }
                            }
                            else
                            {
                                targetHwnd = wins[0].Hwnd;
                            }

                            _windowActivation.FocusWindow(targetHwnd);
                            _lastExternalActiveHwnd = targetHwnd;
                            RecordFocusedHwnd(targetHwnd, key);
                        }
                    }
                    await RefreshAndBroadcastStateAsync(windowTopologyOnly: true);
                    break;
                }

            case "cycleCategoryWindows":
                {
                    if (msg.TryGetProperty("hwnds", out var arrProp) && arrProp.ValueKind == JsonValueKind.Array)
                    {
                        var hwnds = new List<nint>();
                        foreach (var el in arrProp.EnumerateArray())
                        {
                            nint h = (nint)el.GetInt64();
                            if (h != 0) hwnds.Add(h);
                        }

                        if (hwnds.Count > 0)
                        {
                            int delta = msg.TryGetProperty("delta", out var dProp) ? dProp.GetInt32() : 1;
                            bool fromWheel = msg.TryGetProperty("fromWheel", out var wProp) && wProp.GetBoolean();
                            int activeIdx = hwnds.FindIndex(h => h == _lastExternalActiveHwnd);
                            nint target;
                            if (activeIdx >= 0 && (hwnds.Count > 1))
                            {
                                int step = delta >= 0 ? 1 : -1;
                                target = hwnds[(activeIdx + step + hwnds.Count) % hwnds.Count];
                            }
                            else
                            {
                                nint mru = _recentFocusedHwnds.FirstOrDefault(r => hwnds.Contains(r));
                                if (mru != 0 && !fromWheel)
                                    target = mru;
                                else if (mru != 0 && fromWheel && hwnds.Count > 1)
                                {
                                    int mruIdx = hwnds.IndexOf(mru);
                                    int step = delta >= 0 ? 1 : -1;
                                    target = hwnds[(mruIdx + step + hwnds.Count) % hwnds.Count];
                                }
                                else
                                    target = hwnds[0];
                            }

                            _windowActivation.FocusWindow(target);
                            _lastExternalActiveHwnd = target;
                            RecordFocusedHwnd(target);
                        }
                    }
                    await RefreshAndBroadcastStateAsync(windowTopologyOnly: true);
                    break;
                }

            case "focusWindow":
                {
                    nint hwnd = (nint)msg.GetProperty("hwnd").GetInt64();
                    _windowActivation.FocusWindow(hwnd);
                    _lastExternalActiveHwnd = hwnd;
                    RecordFocusedHwnd(hwnd);
                    await RefreshAndBroadcastStateAsync(windowTopologyOnly: true);
                    break;
                }

            case "activateWindow":
                {
                    nint hwnd = (nint)msg.GetProperty("hwnd").GetInt64();
                    _windowActivation.ActivateOrToggleWindow(hwnd);
                    RecordFocusedHwnd(hwnd);
                    await RefreshAndBroadcastStateAsync(windowTopologyOnly: true);
                    break;
                }

            case "minimizeWindow":
                {
                    nint hwnd = (nint)msg.GetProperty("hwnd").GetInt64();
                    _windowActivation.MinimizeWindow(hwnd);
                    await RefreshAndBroadcastStateAsync();
                    break;
                }

            case "closeWindow":
                {
                    nint hwnd = (nint)msg.GetProperty("hwnd").GetInt64();
                    _windowActivation.CloseWindow(hwnd);
                    await Task.Delay(150);
                    await RefreshAndBroadcastStateAsync();
                    break;
                }

            case "arrangeWindow":
                {
                    nint hwnd = (nint)msg.GetProperty("hwnd").GetInt64();
                    string mode = msg.GetProperty("mode").GetString() ?? "left";
                    _windowActivation.ArrangeWindow(hwnd, mode);
                    await RefreshAndBroadcastStateAsync();
                    break;
                }

            case "arrangeActive":
                {
                    string mode = msg.GetProperty("mode").GetString() ?? "left";
                    nint target = _lastExternalActiveHwnd != 0
                        ? _lastExternalActiveHwnd
                        : (_taskbarVm.OpenWindows.FirstOrDefault()?.Hwnd ?? 0);
                    if (target != 0)
                    {
                        _windowActivation.ArrangeWindow(target, mode);
                        SendToast(sourceMonitorId, "Pencere Hizalandı", mode switch
                        {
                            "left" => "Aktif pencere sol yarıya yaslandı.",
                            "right" => "Aktif pencere sağ yarıya yaslandı.",
                            "center" => "Aktif pencere Zen odak modunda ortalandı.",
                            _ => "Aktif pencere tam ekran yapıldı."
                        });
                    }
                    await RefreshAndBroadcastStateAsync();
                    break;
                }

            case "toggleTopMost":
                {
                    nint hwnd = (nint)msg.GetProperty("hwnd").GetInt64();
                    _windowActivation.ToggleAlwaysOnTop(hwnd);
                    SendToast(sourceMonitorId, "Her Zaman Üstte", "Pencere sabitleme durumu güncellendi.");
                    await RefreshAndBroadcastStateAsync();
                    break;
                }

            case "togglePin":
                {
                    string name = msg.GetProperty("name").GetString() ?? "Uygulama";
                    string path = msg.GetProperty("path").GetString() ?? "";
                    await _stateStore.PinOrUnpinShortcutAsync(name, path);
                    SendToast(sourceMonitorId, "Kristal Dock Güncellendi", $"{name} sabitleme durumu güncellendi.");
                    await RefreshAndBroadcastStateAsync();
                    break;
                }

            case "reorderPin":
                {
                    string pinId = msg.GetProperty("id").GetString() ?? string.Empty;
                    int targetIdx = msg.GetProperty("targetIndex").GetInt32();
                    if (!string.IsNullOrWhiteSpace(pinId))
                    {
                        await _stateStore.ReorderPinnedShortcutToIndexAsync(pinId, targetIdx);
                        SendToast(sourceMonitorId, "Dock Sıralaması Kaydedildi", "Simge yeni konumuna taşındı.");
                        await RefreshAndBroadcastStateAsync();
                    }
                    break;
                }

            case "requestWindowPreview":
                {
                    nint hwnd = (nint)msg.GetProperty("hwnd").GetInt64();
                    var jpeg = _thumbnailService.CapturePreviewJpegBase64(hwnd);
                    if (!string.IsNullOrWhiteSpace(jpeg))
                    {
                        var previewJson = JsonSerializer.Serialize(new { type = "windowPreview", hwnd = (long)hwnd, jpeg }, JsonOpts);
                        sourceHost?.DockForm.PostJsonState(previewJson);
                    }
                    break;
                }

            case "launchPath":
                {
                    string target = msg.GetProperty("path").GetString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(target))
                    {
                        if (InstalledAppCatalogService.LaunchTarget(target, out var launchErr))
                        {
                            RecordSearchFrecency(target);
                        }
                        else
                        {
                            _logger.Warn("AppLauncher", "LAUNCH_ERR", $"Failed to launch '{target}': {launchErr}");
                            SendToast(sourceMonitorId, "Başlatılamadı", launchErr ?? Path.GetFileNameWithoutExtension(target));
                        }
                    }
                    break;
                }

            case "searchLocalFiles":
                {
                    string query = msg.TryGetProperty("query", out var qProp) ? (qProp.GetString() ?? string.Empty) : string.Empty;
                    int seq = msg.TryGetProperty("seq", out var sProp) ? sProp.GetInt32() : 0;
                    var targetCrown = sourceHost?.CrownForm;
                    if (targetCrown is not null && !string.IsNullOrWhiteSpace(query))
                    {
                        _ = Task.Run(() =>
                        {
                            var hits = _fileSearch.Search(query, 8);
                            var dto = hits.Select(h => new
                            {
                                name = h.Name,
                                path = h.FullPath,
                                subtitle = h.Subtitle,
                                badge = h.Badge,
                                isDir = h.IsDirectory,
                                score = h.Score,
                                icon = EncodePngMemoized(h.IconPngBytes)
                            }).ToArray();

                            var payloadJson = JsonSerializer.Serialize(new
                            {
                                type = "fileSearchResults",
                                seq,
                                query,
                                files = dto
                            }, JsonOpts);

                            if (!targetCrown.IsDisposed && targetCrown.IsHandleCreated)
                            {
                                try { targetCrown.BeginInvoke(() => targetCrown.PostJsonState(payloadJson)); } catch { }
                            }
                        });
                    }
                    break;
                }

            case "toggleHideNativeTaskbar":
                {
                    bool nextHide = !_appBarService.IsNativeTaskbarSuppressed;
                    _appBarService.SetNativeTaskbarHiddenWhileActive(nextHide);
                    if (nextHide) SpawnCrashWatchdogIfNeeded();
                    _shellPrefs = _shellPrefs with { HideNativeTaskbarWhileActive = nextHide };
                    _shellPrefsStore.Save(_shellPrefs);
                    await Task.Delay(80);
                    RecomputeAndApplyAllMonitorBounds();
                    SendToast(
                        sourceMonitorId,
                        nextHide ? "Windows Barı Gizlendi" : "Windows Barı Görünür",
                        nextHide ? "Tüm ekranlarda Kristal Dock alt sıfır kenarına yerleşti." : "Birlikte çalışma modu etkinleştirildi.");
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "toggleDockAutoHide":
                {
                    string nextMode = _shellPrefs.EffectiveDockMode == "AutoHide" ? "AlwaysVisible" : "AutoHide";
                    _shellPrefs = _shellPrefs with { DockVisibilityMode = nextMode, DockAutoHide = nextMode == "AutoHide" };
                    _shellPrefsStore.Save(_shellPrefs);
                    SendToast(sourceMonitorId, nextMode == "AutoHide" ? "Dock Otomatik Gizleme Açık" : "Dock Sabit Görünür",
                        nextMode == "AutoHide" ? "Alt kenara yaklaştığınızda Kristal Dock otomatik açılır." : "Kristal Dock her zaman görünür kalır (tam ekran hariç).");
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "cycleDockVisibilityMode":
                {
                    string nextMode = _shellPrefs.EffectiveDockMode switch
                    {
                        "AlwaysVisible" => "SmartDodge",
                        "SmartDodge" => "AutoHide",
                        _ => "AlwaysVisible"
                    };
                    _shellPrefs = _shellPrefs with { DockVisibilityMode = nextMode, DockAutoHide = nextMode == "AutoHide" };
                    _shellPrefsStore.Save(_shellPrefs);
                    var (tTitle, tBody) = nextMode switch
                    {
                        "SmartDodge" => ("Dock Modu: Akıllı Kaçınma", "Tam pencere alt kenara değdiğinde otomatik çekilir; masaüstünde hep görünür."),
                        "AutoHide" => ("Dock Modu: Otomatik Gizle", "İmleç ayrıldığında alt kenarda ince ışıklı çizgiye katlanır."),
                        _ => ("Dock Modu: Sabit Görünür", "Kristal Dock tüm ekranlarda daima açık kalır (tam ekran hariç).")
                    };
                    SendToast(sourceMonitorId, tTitle, tBody);
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "launchNewInstance":
                {
                    string key = msg.GetProperty("key").GetString() ?? string.Empty;
                    var grp = _taskbarVm.Groups.FirstOrDefault(g => g.GroupKey == key);
                    if (grp is not null && !string.IsNullOrWhiteSpace(grp.ExecutablePath))
                    {
                        if (!InstalledAppCatalogService.LaunchTarget(grp.ExecutablePath, out _))
                        {
                            _taskbarVm.InvokeGroupPrimaryAction(grp);
                        }
                    }
                    await Task.Delay(180);
                    await RefreshAndBroadcastStateAsync(windowTopologyOnly: true);
                    break;
                }

            case "closeAllGroupWindows":
                {
                    string key = msg.GetProperty("key").GetString() ?? string.Empty;
                    var grp = _taskbarVm.Groups.FirstOrDefault(g => g.GroupKey == key);
                    if (grp is not null)
                    {
                        foreach (var w in grp.Windows)
                            _windowActivation.CloseWindow(w.Hwnd);
                        await Task.Delay(180);
                        await RefreshAndBroadcastStateAsync(windowTopologyOnly: true);
                    }
                    break;
                }

            case "focusGroupForDrop":
                {
                    string key = msg.GetProperty("key").GetString() ?? string.Empty;
                    var grp = _taskbarVm.Groups.FirstOrDefault(g => g.GroupKey == key);
                    if (grp is not null && grp.Windows.Count > 0)
                    {
                        nint target = _lastFocusedWindowByGroup.TryGetValue(key, out var remembered) && grp.Windows.Any(w => w.Hwnd == remembered)
                            ? remembered
                            : grp.Windows[0].Hwnd;
                        _windowActivation.FocusWindow(target);
                        _lastExternalActiveHwnd = target;
                    }
                    break;
                }

            case "requestFolderStack":
                {
                    string folderPath = msg.TryGetProperty("path", out var pProp) ? (pProp.GetString() ?? string.Empty) : string.Empty;
                    var targetDock = sourceHost?.DockForm;
                    if (targetDock is not null && !string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
                    {
                        _ = Task.Run(() =>
                        {
                            var items = DockFolderStackService.ReadFolderStackItems(folderPath, 10);
                            var dto = items.Select(it => new
                            {
                                name = it.Name,
                                path = it.FullPath,
                                isDir = it.IsDirectory,
                                size = it.SizeLabel,
                                mod = it.ModifiedLabel,
                                icon = EncodePngMemoized(it.IconPngBytes)
                            }).ToArray();

                            var json = JsonSerializer.Serialize(new
                            {
                                type = "folderStackItems",
                                path = folderPath,
                                name = DockFolderStackService.GetFriendlyFolderName(folderPath),
                                items = dto
                            }, JsonOpts);

                            if (!targetDock.IsDisposed && targetDock.IsHandleCreated)
                            {
                                try { targetDock.BeginInvoke(() => targetDock.PostJsonState(json)); } catch { }
                            }
                        });
                    }
                    break;
                }

            case "pickDockFolder":
                {
                    using var fbd = new FolderBrowserDialog { Description = "Kristal Dock Klasör Yığınına Ekle" };
                    if (fbd.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        AddDockFolderPath(fbd.SelectedPath, sourceMonitorId);
                        await RefreshAndBroadcastStateAsync(forceFull: true);
                    }
                    break;
                }

            case "removeDockFolder":
                {
                    string folderPath = msg.TryGetProperty("path", out var pProp) ? (pProp.GetString() ?? string.Empty) : string.Empty;
                    if (!string.IsNullOrWhiteSpace(folderPath))
                    {
                        var folders = GetEffectiveDockFolders();
                        folders.RemoveAll(f => string.Equals(f, folderPath, StringComparison.OrdinalIgnoreCase));
                        _shellPrefs = _shellPrefs with { DockFolderPaths = folders };
                        _shellPrefsStore.Save(_shellPrefs);
                        SendToast(sourceMonitorId, "Klasör Yığını Kaldırıldı", DockFolderStackService.GetFriendlyFolderName(folderPath));
                        await RefreshAndBroadcastStateAsync(forceFull: true);
                    }
                    break;
                }

            case "markNotificationsRead":
                _unreadNotifCount = 0;
                await RefreshAndBroadcastStateAsync(windowTopologyOnly: true);
                break;

            case "dismissNotification":
                {
                    string id = msg.TryGetProperty("id", out var idProp) ? (idProp.GetString() ?? string.Empty) : string.Empty;
                    if (id.StartsWith("sys-", StringComparison.OrdinalIgnoreCase) && long.TryParse(id[4..], out long sysId))
                    {
                        _dismissedSystemNotifIds.Add(sysId);
                    }
                    else if (!string.IsNullOrWhiteSpace(id))
                    {
                        await _stateStore.DismissNotificationAsync(id);
                    }
                    if (_unreadNotifCount > 0) _unreadNotifCount--;
                    await RefreshAndBroadcastStateAsync(windowTopologyOnly: true);
                    break;
                }

            case "togglePerMonitorFilter":
                _shellPrefs = _shellPrefs with { PerMonitorWindowFilter = !_shellPrefs.PerMonitorWindowFilter };
                _shellPrefsStore.Save(_shellPrefs);
                SendToast(sourceMonitorId, _shellPrefs.PerMonitorWindowFilter ? "Ekran Bazlı Dock Filtresi Açık" : "Tüm Ekran Pencereleri Gösteriliyor",
                    _shellPrefs.PerMonitorWindowFilter ? "Her monitör yalnızca kendi üzerindeki açık pencereleri listeler." : "Tüm açık pencereler her ekranda gösterilir.");
                await RefreshAndBroadcastStateAsync(forceFull: true);
                break;

            case "toggleSmartTidy":
                _shellPrefs = _shellPrefs with { SmartTidyEnabled = !_shellPrefs.SmartTidyEnabled };
                _shellPrefsStore.Save(_shellPrefs);
                await RefreshAndBroadcastStateAsync(forceFull: true);
                break;

            case "toggleCrownPinned":
                _shellPrefs = _shellPrefs with { CrownPinned = !_shellPrefs.CrownPinned };
                _shellPrefsStore.Save(_shellPrefs);
                await RefreshAndBroadcastStateAsync(forceFull: true);
                break;

            case "toggleDwmLivePreview":
                _shellPrefs = _shellPrefs with { DwmLivePreviewEnabled = !_shellPrefs.DwmLivePreviewEnabled };
                _shellPrefsStore.Save(_shellPrefs);
                if (!_shellPrefs.DwmLivePreviewEnabled)
                    _dwmPreviewForm?.HidePreview();
                SendToast(sourceMonitorId,
                    _shellPrefs.DwmLivePreviewEnabled ? "60 FPS DWM Canlı Önizleme Açık" : "DWM Canlı Önizleme Kapalı",
                    _shellPrefs.DwmLivePreviewEnabled ? "Yuvarlak pencere küresinde 0.2 sn bekleyince canlı pencere görünür." : "Yalnızca temiz dairesel küreler gösterilir.");
                await RefreshAndBroadcastStateAsync(forceFull: true);
                break;

            case "toggleSysNotifBridge":
                _shellPrefs = _shellPrefs with { SystemNotificationBridgeEnabled = !_shellPrefs.SystemNotificationBridgeEnabled };
                _sysNotifBridge.IsEnabled = _shellPrefs.SystemNotificationBridgeEnabled;
                _shellPrefsStore.Save(_shellPrefs);
                if (_shellPrefs.SystemNotificationBridgeEnabled)
                    _sysNotifBridge.PollNow(raiseEvents: false);
                SendToast(sourceMonitorId,
                    _shellPrefs.SystemNotificationBridgeEnabled ? "Windows Bildirim Akışı Açık" : "Windows Bildirim Akışı Kapalı",
                    _shellPrefs.SystemNotificationBridgeEnabled ? "WhatsApp, Teams ve sistem bildirimleri Üst Damla Adaya canlı düşer." : "Yalnızca Aurora bildirimleri gösterilir.");
                await RefreshAndBroadcastStateAsync(forceFull: true);
                break;

            case "toggleWinKeyOmni":
                _shellPrefs = _shellPrefs with { WinKeyOpensOmniSearch = !_shellPrefs.WinKeyOpensOmniSearch };
                _winKeyHook.SetEnabled(_shellPrefs.WinKeyOpensOmniSearch);
                _shellPrefsStore.Save(_shellPrefs);
                SendToast(sourceMonitorId,
                    _shellPrefs.WinKeyOpensOmniSearch ? "Tek Basış Win Tuşu: Aurora Ara" : "Win Tuşu: Standart Windows",
                    _shellPrefs.WinKeyOpensOmniSearch ? "Win+E, Win+D, Win+L kısayolları korunur; tek basışta Aurora Ara açılır." : "Klasik Windows Başlat menüsü varsayılana döndü.");
                await RefreshAndBroadcastStateAsync(forceFull: true);
                break;

            case "cycleOmniHotkey":
                {
                    string nextPreset = _shellPrefs.OmniHotkeyPreset switch
                    {
                        "Alt+Space" => "Ctrl+Space",
                        "Ctrl+Space" => "Alt+Q",
                        _ => "Alt+Space"
                    };
                    _shellPrefs = _shellPrefs with { OmniHotkeyPreset = nextPreset };
                    _shellPrefsStore.Save(_shellPrefs);
                    _hotkeyWindow?.RegisterHotkeys(nextPreset);
                    SendToast(sourceMonitorId, $"Arama Kısayolu: {nextPreset}", "Anında etkinleştirildi ve kaydedildi.");
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "showDwmPreview":
                {
                    if (!_shellPrefs.DwmLivePreviewEnabled || _dwmPreviewForm is null || sourceHost is null)
                        break;

                    nint hwnd = (nint)msg.GetProperty("hwnd").GetInt64();
                    double hubLeft = msg.TryGetProperty("hubLeft", out var hl) ? hl.GetDouble() : 0;
                    double hubTop = msg.TryGetProperty("hubTop", out var ht) ? ht.GetDouble() : 0;
                    double hubW = msg.TryGetProperty("hubWidth", out var hw) ? hw.GetDouble() : 296;
                    double hubH = msg.TryGetProperty("hubHeight", out var hh) ? hh.GetDouble() : 280;
                    double vw = msg.TryGetProperty("vw", out var vwp) ? Math.Max(1.0, vwp.GetDouble()) : Math.Max(1, sourceHost.DockForm.Width);
                    double vh = msg.TryGetProperty("vh", out var vhp) ? Math.Max(1.0, vhp.GetDouble()) : Math.Max(1, sourceHost.DockForm.Height);

                    var dockBounds = sourceHost.DockForm.Bounds;
                    int sx = dockBounds.Left + (int)Math.Round((hubLeft / vw) * dockBounds.Width);
                    int sy = dockBounds.Top + (int)Math.Round((hubTop / vh) * dockBounds.Height);
                    int sw = (int)Math.Round((hubW / vw) * dockBounds.Width);
                    int sh = (int)Math.Round((hubH / vh) * dockBounds.Height);
                    bool isLight = _stateStore.EffectiveTheme.ColorMode == AuroraColorMode.Light;

                    _dwmPreviewForm.ShowLivePreview(hwnd, new Rectangle(sx, sy, sw, sh), sourceHost.Monitor, isLight);
                    break;
                }

            case "hideDwmPreview":
                _dwmPreviewForm?.HidePreview();
                break;

            case "switchVirtualDesktopDelta":
                {
                    int delta = msg.TryGetProperty("delta", out var dProp) ? dProp.GetInt32() : 1;
                    _virtualDesktops.SwitchRelative(delta);
                    await Task.Delay(220);
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "switchVirtualDesktopIndex":
                {
                    int idx = msg.TryGetProperty("index", out var iProp) ? iProp.GetInt32() : 0;
                    _virtualDesktops.SwitchToIndex(idx);
                    await Task.Delay(220);
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "createVirtualDesktop":
                _virtualDesktops.CreateNewDesktop();
                await Task.Delay(260);
                SendToast(sourceMonitorId, "Yeni Sanal Masaüstü", "Oluşturuldu ve geçiş yapıldı.");
                await RefreshAndBroadcastStateAsync(forceFull: true);
                break;

            case "openTaskView":
                _virtualDesktops.OpenTaskView();
                break;

            case "applyWindowLayout":
                {
                    string preset = msg.TryGetProperty("preset", out var pProp) ? (pProp.GetString() ?? "code6535") : "code6535";
                    int count = WindowLayoutEngine.ApplyPreset(preset, _taskbarVm.OpenWindows, _lastExternalActiveHwnd, sourceMonitorId);
                    string label = preset switch
                    {
                        "code6535" => "Kodlama 65/35",
                        "split5050" => "Eşit İkili 50/50",
                        "studio3" => "Üçlü Stüdyo 50/25/25",
                        _ => "Odak Zen %75"
                    };
                    SendToast(sourceMonitorId, $"Düzen Uygulandı: {label}", count > 0 ? $"{count} pencere çalışma alanına yerleştirildi." : "Hizalanacak açık pencere bulunamadı.");
                    await RefreshAndBroadcastStateAsync(windowTopologyOnly: true);
                    break;
                }

            case "exportBackup":
                {
                    using var sfd = new SaveFileDialog
                    {
                        Title = "Aurora Ayarlarını Yedekle",
                        Filter = "Aurora Yedek Paketi (*.aurora-backup)|*.aurora-backup",
                        FileName = $"aurora-yedek-{DateTime.Now:yyyyMMdd}.aurora-backup",
                        InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    };
                    if (sfd.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(sfd.FileName))
                    {
                        var bundle = new AuroraBackupBundle(
                            FormatVersion: "1.1",
                            ExportedAtUtc: DateTimeOffset.UtcNow,
                            ShellPreferences: _shellPrefs,
                            ActiveThemeId: _stateStore.EffectiveTheme.Id,
                            ActiveProfileId: _stateStore.ActiveProfile.Id,
                            PinnedShortcuts: _stateStore.PinnedShortcuts,
                            AgendaNotes: new Dictionary<string, string>(_agendaNotes, StringComparer.OrdinalIgnoreCase),
                            ScratchpadText: _scratchpadText,
                            DropShelfPaths: [.. _dropShelfPaths],
                            SearchFrecency: new Dictionary<string, int>(_searchFrecency, StringComparer.OrdinalIgnoreCase));
                        AuroraBackupService.ExportToFile(sfd.FileName, bundle);
                        SendToast(sourceMonitorId, "Yedek Oluşturuldu", Path.GetFileName(sfd.FileName));
                    }
                    break;
                }

            case "importBackup":
                {
                    using var ofd = new OpenFileDialog
                    {
                        Title = "Aurora Yedeğinden Geri Yükle",
                        Filter = "Aurora Yedek Paketi (*.aurora-backup)|*.aurora-backup",
                        InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    };
                    if (ofd.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(ofd.FileName))
                    {
                        var bundle = AuroraBackupService.ImportFromFile(ofd.FileName);
                        if (bundle is null)
                        {
                            SendToast(sourceMonitorId, "Yedek Okunamadı", "Seçilen dosya geçerli bir Aurora yedeği değil.");
                            break;
                        }

                        _shellPrefs = bundle.ShellPreferences;
                        _shellPrefsStore.Save(_shellPrefs);
                        _sysNotifBridge.IsEnabled = _shellPrefs.SystemNotificationBridgeEnabled;
                        _winKeyHook.SetEnabled(_shellPrefs.WinKeyOpensOmniSearch);
                        _hotkeyWindow?.RegisterHotkeys(_shellPrefs.OmniHotkeyPreset);
                        _appBarService.SetNativeTaskbarHiddenWhileActive(_shellPrefs.HideNativeTaskbarWhileActive);
                        RecomputeAndApplyAllMonitorBounds();

                        if (!string.IsNullOrWhiteSpace(bundle.ActiveProfileId))
                            await _workspaceVm.ActivateProfileAsync(bundle.ActiveProfileId);
                        if (!string.IsNullOrWhiteSpace(bundle.ActiveThemeId))
                            await _stateStore.ApplyThemeAsync(_stateStore.ResolveThemeById(bundle.ActiveThemeId));
                        if (bundle.PinnedShortcuts.Count > 0)
                            await _stateStore.ReplacePinnedShortcutsAsync(bundle.PinnedShortcuts);

                        _agendaNotes.Clear();
                        foreach (var kv in bundle.AgendaNotes) _agendaNotes[kv.Key] = kv.Value;
                        try { await File.WriteAllTextAsync(_agendaFilePath, JsonSerializer.Serialize(_agendaNotes)); } catch { }

                        _scratchpadText = bundle.ScratchpadText;
                        try { await File.WriteAllTextAsync(_scratchpadFilePath, _scratchpadText); } catch { }

                        _dropShelfPaths.Clear();
                        _dropShelfPaths.AddRange(bundle.DropShelfPaths.Where(p => File.Exists(p) || Directory.Exists(p)).Take(16));
                        PersistDropShelf();

                        _searchFrecency.Clear();
                        foreach (var kv in bundle.SearchFrecency) _searchFrecency[kv.Key] = kv.Value;
                        try { await File.WriteAllTextAsync(_frecencyFilePath, JsonSerializer.Serialize(_searchFrecency)); } catch { }

                        PostStaticCatalogToAllCrowns(forceRebuild: true);
                        SendToast(sourceMonitorId, "Yedek Geri Yüklendi", "Tema, sabitler, ayarlar ve notlar aktarıldı.");
                        await RefreshAndBroadcastStateAsync(forceFull: true);
                    }
                    break;
                }

            case "setMagnification":
                _shellPrefs = _shellPrefs with { DockMagnificationPercent = Math.Clamp(msg.GetProperty("value").GetInt32(), 0, 80) };
                _shellPrefsStore.Save(_shellPrefs);
                await RefreshAndBroadcastStateAsync(forceFull: true);
                break;

            case "setMetricsActive":
                {
                    bool active = msg.GetProperty("active").GetBoolean();
                    _metricsTabActive = active;
                    _widgetVm.IsSurfaceVisible = active;
                    if (_metricsLiveTimer is not null)
                    {
                        if (active) _metricsLiveTimer.Start();
                        else _metricsLiveTimer.Stop();
                    }
                    if (active)
                    {
                        await _widgetVm.RefreshTelemetryIfVisibleAsync();
                        RecordTelemetrySparklineSample();
                        await RefreshAndBroadcastStateAsync(forceFull: true);
                    }
                    break;
                }

            case "setVolume":
                _ccVm.SetMasterVolumePercent(msg.GetProperty("value").GetDouble());
                await RefreshAndBroadcastStateAsync();
                break;

            case "adjustVolumeDelta":
                _ccVm.SetMasterVolumePercent(_ccVm.MasterVolumePercent + msg.GetProperty("delta").GetDouble());
                await RefreshAndBroadcastStateAsync();
                break;

            case "toggleMute":
                _ccVm.ToggleMasterMute();
                await RefreshAndBroadcastStateAsync();
                break;

            case "setAppVolume":
                _ccVm.SetAppSessionVolumePercent((uint)msg.GetProperty("pid").GetInt64(), msg.GetProperty("value").GetDouble());
                break;

            case "setBrightness":
                _ccVm.SetBrightnessPercent(msg.GetProperty("value").GetInt32());
                break;

            case "setMonitorBrightness":
                {
                    string id = msg.GetProperty("id").GetString() ?? string.Empty;
                    int val = Math.Clamp(msg.GetProperty("value").GetInt32(), 0, 100);
                    string label = msg.TryGetProperty("name", out var nm) ? (nm.GetString() ?? "Parlaklık") : "Parlaklık";
                    _ = Task.Run(() => _ddcBrightnessService.TrySetMonitorBrightness(id, val));
                    if (_shellPrefs.HardwareOsdEnabled)
                    {
                        SendOsdHud(sourceMonitorId, new HardwareOsdEvent("brightness", label, $"%{val}", val, true));
                    }
                    break;
                }

            case "refreshMonitorsBrightness":
                {
                    await Task.Run(() => _ddcBrightnessService.DiscoverMonitors());
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "refreshBluetoothBatteries":
                {
                    await PollBluetoothBatteriesBackgroundAsync(forceBroadcast: true);
                    break;
                }

            case "toggleClipboardAutoPaste":
                {
                    _shellPrefs = _shellPrefs with { ClipboardAutoPasteEnabled = !_shellPrefs.ClipboardAutoPasteEnabled };
                    _shellPrefsStore.Save(_shellPrefs);
                    SendToast(sourceMonitorId,
                        _shellPrefs.ClipboardAutoPasteEnabled ? "Pano Tek Tık Yapıştırma Açık" : "Pano Yalnızca Kopyalama Modu",
                        _shellPrefs.ClipboardAutoPasteEnabled
                            ? "Pano öğesine tıklayınca aktif pencereye doğrudan Ctrl+V yapılır."
                            : "Pano öğesine tıklayınca panoya kopyalanır.");
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "killTopProcess":
                {
                    string procName = msg.TryGetProperty("name", out var pn) ? (pn.GetString() ?? string.Empty) : string.Empty;
                    int pid = msg.TryGetProperty("pid", out var pidEl) ? pidEl.GetInt32() : 0;
                    if (WindowsSystemMetricsService.TryTerminateUserProcessSafe(procName, pid))
                    {
                        SendToast(sourceMonitorId, "Uygulama Kapatıldı", FormatName(procName));
                        await Task.Delay(250);
                        RecordTelemetrySparklineSample();
                        await RefreshAndBroadcastStateAsync(forceFull: true);
                    }
                    break;
                }

            case "shareShelfFileQr":
                {
                    int idx = msg.GetProperty("index").GetInt32();
                    if (idx >= 0 && idx < _dropShelfPaths.Count)
                    {
                        StartQrShareAndToast(sourceMonitorId, [_dropShelfPaths[idx]]);
                        await RefreshAndBroadcastStateAsync(forceFull: true);
                    }
                    break;
                }

            case "pickAndShareQr":
                {
                    using var ofd = new OpenFileDialog { Multiselect = true, Title = "QR ile Telefona Gönderilecek Dosyaları Seçin" };
                    if (ofd.ShowDialog() == DialogResult.OK && ofd.FileNames.Length > 0)
                    {
                        StartQrShareAndToast(sourceMonitorId, ofd.FileNames);
                        await RefreshAndBroadcastStateAsync(forceFull: true);
                    }
                    break;
                }

            case "stopQrShare":
                {
                    _lanQrShareService.StopSharing();
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "copyQrUrl":
                {
                    var activeQr = _lanQrShareService.GetActiveSession();
                    if (activeQr is not null && !string.IsNullOrWhiteSpace(activeQr.Url))
                    {
                        _suppressNextClipboardCapture = true;
                        try { Clipboard.SetText(activeQr.Url); } catch { }
                        SendToast(sourceMonitorId, "Paylaşım Bağlantısı Kopyalandı", activeQr.Url);
                    }
                    break;
                }

            case "convertShelfFile":
                {
                    int idx = msg.GetProperty("index").GetInt32();
                    if (idx >= 0 && idx < _dropShelfPaths.Count)
                    {
                        string path = _dropShelfPaths[idx];
                        var res = await Task.Run(() => LanQrFileShareService.ExecuteQuickConvertOrZip([path]));
                        if (res.Success && File.Exists(res.OutputPath))
                        {
                            await HandleDroppedFilesAsync(sourceMonitorId, [res.OutputPath]);
                            SendToast(sourceMonitorId, res.SummaryTitle, res.SummaryDetail);
                        }
                        else
                        {
                            SendToast(sourceMonitorId, res.SummaryTitle, res.SummaryDetail);
                        }
                    }
                    break;
                }

            case "startGifRecording":
                {
                    if (_gifRecorder.IsRecording)
                    {
                        _gifRecorder.RequestStop();
                        break;
                    }
                    await Task.Delay(140);
                    var region = ScreenRegionOverlayForm.SelectGifRecordRegion();
                    if (region is { Width: >= 16, Height: >= 16 } r)
                    {
                        _gifRecSourceMonitorId = sourceMonitorId;
                        if (_gifRecorder.StartRecording(r))
                        {
                            BroadcastGifRecordingState(true, 0);
                            await RefreshAndBroadcastStateAsync(forceFull: true);
                        }
                    }
                    break;
                }

            case "stopGifRecording":
                {
                    _gifRecorder.RequestStop();
                    break;
                }

            case "toggleWifi":
                {
                    var r = await _ccVm.ToggleWifiAsync();
                    if (!r.IsSuccess && !string.IsNullOrWhiteSpace(r.Error?.FallbackDeepLinkUri))
                        OpenShellUri(r.Error.FallbackDeepLinkUri);
                    await RefreshAndBroadcastStateAsync();
                    break;
                }

            case "toggleBluetooth":
                {
                    var r = await _ccVm.ToggleBluetoothAsync();
                    if (!r.IsSuccess && !string.IsNullOrWhiteSpace(r.Error?.FallbackDeepLinkUri))
                        OpenShellUri(r.Error.FallbackDeepLinkUri);
                    await RefreshAndBroadcastStateAsync();
                    break;
                }

            case "toggleQuietHours":
                {
                    var p = _stateStore.Preferences;
                    await _stateStore.UpdatePreferencesAsync(p with
                    {
                        Notifications = p.Notifications with { QuietHoursEnabled = !p.Notifications.QuietHoursEnabled }
                    });
                    await RefreshAndBroadcastStateAsync();
                    break;
                }

            case "mediaCmd":
                {
                    string cmd = msg.GetProperty("cmd").GetString() ?? "playpause";
                    if (cmd == "next") await _mediaVm.NextTrackAsync();
                    else if (cmd == "previous") await _mediaVm.PreviousTrackAsync();
                    else await _mediaVm.TogglePlayPauseAsync();
                    await RefreshAndBroadcastStateAsync();
                    break;
                }

            case "applyTheme":
                {
                    string id = msg.GetProperty("id").GetString() ?? BuiltInThemes.AuroraGlassDark.Id;
                    var t = _stateStore.ResolveThemeById(id);
                    await _stateStore.ApplyThemeAsync(t);
                    await RefreshAndBroadcastStateAsync();
                    break;
                }

            case "switchProfile":
                {
                    string id = msg.GetProperty("id").GetString() ?? "profile-work";
                    await _workspaceVm.ActivateProfileAsync(id);
                    await RefreshAndBroadcastStateAsync();
                    break;
                }

            case "cycleProfile":
                {
                    var all = _stateStore.Profiles;
                    int idx = 0;
                    for (int i = 0; i < all.Count; i++)
                    {
                        if (all[i].Id == _stateStore.ActiveProfile.Id) { idx = i; break; }
                    }
                    var next = all[(idx + 1) % all.Count];
                    await _workspaceVm.ActivateProfileAsync(next.Id);
                    SendToast(sourceMonitorId, $"Profil: {next.Name}", next.Description);
                    await RefreshAndBroadcastStateAsync();
                    break;
                }

            case "saveScratchpad":
                _scratchpadText = msg.GetProperty("text").GetString() ?? string.Empty;
                try { await File.WriteAllTextAsync(_scratchpadFilePath, _scratchpadText); } catch { }
                break;

            case "copyClipIndex":
                {
                    int idx = msg.GetProperty("index").GetInt32();
                    var items = _clipboardVaultStore.GetItems(limit: 40);
                    if (idx >= 0 && idx < items.Count)
                        RestoreClipboardItemToSystem(items[idx], sourceMonitorId);
                    break;
                }

            case "copyClipById":
                {
                    long id = msg.GetProperty("id").GetInt64();
                    var item = _clipboardVaultStore.GetById(id);
                    if (item is not null)
                        RestoreClipboardItemToSystem(item, sourceMonitorId);
                    break;
                }

            case "togglePinClip":
                {
                    long id = msg.GetProperty("id").GetInt64();
                    if (_clipboardVaultStore.TogglePin(id))
                        await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "deleteClip":
                {
                    long id = msg.GetProperty("id").GetInt64();
                    if (_clipboardVaultStore.Delete(id))
                        await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "clearUnpinnedClips":
                {
                    int removed = _clipboardVaultStore.ClearUnpinned();
                    SendToast(sourceMonitorId, "Pano Temizlendi", removed > 0 ? $"{removed} sabitlenmemiş öğe silindi." : "Silinecek öğe yok (sabitlenenler korundu).");
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "setAwakeMode":
                {
                    string mode = msg.TryGetProperty("mode", out var mProp) ? (mProp.GetString() ?? "off") : "off";
                    var snap = _awakeService.SetModeByKey(mode);
                    SendToast(sourceMonitorId,
                        snap.IsActive ? $"Uyanık Tut Aktif ({snap.ShortLabel})" : "Uyanık Tut Kapatıldı",
                        snap.FullLabel);
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "cycleAwake":
                {
                    var snap = _awakeService.CycleMode();
                    SendToast(sourceMonitorId,
                        snap.IsActive ? $"Uyanık Tut: {snap.ShortLabel}" : "Uyanık Tut Kapatıldı",
                        snap.FullLabel);
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "setDockDensity":
                {
                    string density = msg.TryGetProperty("density", out var dProp) ? (dProp.GetString() ?? "Standard") : "Standard";
                    if (density is not ("Compact" or "Standard" or "Cozy"))
                        density = "Standard";
                    _shellPrefs = _shellPrefs with { DockDensity = density, DockScalePercent = 100 };
                    _shellPrefsStore.Save(_shellPrefs);
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "setDockEdge":
                {
                    string edge = msg.TryGetProperty("edge", out var eProp) ? (eProp.GetString() ?? "Bottom") : "Bottom";
                    if (edge is not ("Bottom" or "Left" or "Right"))
                        edge = "Bottom";
                    _shellPrefs = _shellPrefs with { DockEdge = edge, DockOffsetPercent = 0 };
                    _shellPrefsStore.Save(_shellPrefs);
                    RecomputeAndApplyAllMonitorBounds();
                    EvaluateFullscreenYieldPerMonitor();
                    string label = edge == "Left" ? "Sol Dikey" : (edge == "Right" ? "Sağ Dikey" : "Alt Yatay");
                    SendToast(sourceMonitorId, $"Dock Konumu: {label}", "Alt+Sol Tık: Taşı/Kenar Değiştir • Alt+Sağ Tık: Boyutlandır.");
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    foreach (var h in _monitorHosts.Values)
                        h.DockForm.RequestLayoutSync(force: true);
                    break;
                }

            case "resetDockTransform":
                {
                    _shellPrefs = _shellPrefs with { DockScalePercent = 100, DockOffsetPercent = 0 };
                    _shellPrefsStore.Save(_shellPrefs);
                    SendToast(sourceMonitorId, "Dock Sıfırlandı", "Konum ortalandı ve boyut %100 olarak ayarlandı.");
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "toggleHardwareOsd":
                {
                    _shellPrefs = _shellPrefs with { HardwareOsdEnabled = !_shellPrefs.HardwareOsdEnabled };
                    _shellPrefsStore.Save(_shellPrefs);
                    _osdMonitor.SuppressNativeVolumeKeys = _shellPrefs.HardwareOsdEnabled;
                    _osdMonitor.SetHookEnabled(_shellPrefs.HardwareOsdEnabled && ParseSmokeExitSeconds() is null);
                    if (_shellPrefs.HardwareOsdEnabled)
                    {
                        var vol = _audioService.GetDefaultOutputState();
                        int pct = vol.IsSuccess ? (int)Math.Round(vol.Value.VolumeScalar * 100) : 50;
                        SendOsdHud(sourceMonitorId, new HardwareOsdEvent("volume", "Canlı OSD", $"%{pct}", pct, Active: true, DurationMs: 1450));
                    }
                    else
                    {
                        SendToast(sourceMonitorId, "Canlı Damla OSD Kapalı", "Windows varsayılan ses göstergesi yeniden aktif.");
                    }
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "toggleAltDrag":
                {
                    _shellPrefs = _shellPrefs with { AltDragEnabled = !_shellPrefs.AltDragEnabled };
                    _shellPrefsStore.Save(_shellPrefs);
                    _altDragService?.SetEnabled(_shellPrefs.AltDragEnabled && ParseSmokeExitSeconds() is null);
                    SendToast(sourceMonitorId,
                        _shellPrefs.AltDragEnabled ? "Alt+Sürükle Pencere & Dock Yönetimi Açık" : "Alt+Sürükle Kapalı",
                        _shellPrefs.AltDragEnabled ? "Pencerelerde ve Dock'ta: Alt+Sol Tık Taşı • Alt+Sağ Tık Boyutlandır" : "Standart pencere sürükleme aktif.");
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "toggleClockSeconds":
                {
                    _shellPrefs = _shellPrefs with { ShowClockSeconds = !_shellPrefs.ShowClockSeconds };
                    _shellPrefsStore.Save(_shellPrefs);
                    if (_clockTickTimer is not null)
                    {
                        if (_shellPrefs.ShowClockSeconds) _clockTickTimer.Start();
                        else _clockTickTimer.Stop();
                    }
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "toggleSnapZones":
                {
                    _shellPrefs = _shellPrefs with { SnapZonesEnabled = !_shellPrefs.SnapZonesEnabled };
                    _shellPrefsStore.Save(_shellPrefs);
                    SendToast(sourceMonitorId,
                        _shellPrefs.SnapZonesEnabled ? "Shift+Sürükle Bölgeleri Açık" : "Shift+Sürükle Bölgeleri Kapalı",
                        _shellPrefs.SnapZonesEnabled ? "Bir pencereyi Shift tuşuyla sürükleyip cam bölgeye bırakın." : "Standart pencere sürükleme aktif.");
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "setSnapZonePreset":
                {
                    string preset = msg.TryGetProperty("preset", out var pProp) ? (pProp.GetString() ?? "code6535") : "code6535";
                    if (preset is not ("code6535" or "studio3" or "cols3" or "grid2x2"))
                        preset = "code6535";
                    _shellPrefs = _shellPrefs with { SnapZonePreset = preset };
                    _shellPrefsStore.Save(_shellPrefs);
                    var info = SnapZonesLayoutCatalog.AllPresets.FirstOrDefault(p => p.Id == preset);
                    SendToast(sourceMonitorId, $"Bölge Şablonu: {info?.DisplayName ?? preset}", info?.Description ?? "Shift+Sürükle ile hazır.");
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "setAppAudioOutput":
                {
                    uint pid = (uint)msg.GetProperty("pid").GetInt64();
                    string deviceId = msg.TryGetProperty("deviceId", out var devProp) ? (devProp.GetString() ?? string.Empty) : string.Empty;
                    var res = _perAppAudioRouting.SetAppOutputDevice(pid, string.IsNullOrWhiteSpace(deviceId) ? null : deviceId);
                    if (res.IsSuccess)
                    {
                        var ep = _ccVm.AudioEndpoints.FirstOrDefault(e => string.Equals(e.Id, deviceId, StringComparison.OrdinalIgnoreCase));
                        string targetName = ep is null ? "Sistem Varsayılanı" : ShortAudioDeviceName(ep.FriendlyName);
                        SendToast(sourceMonitorId, "Uygulama Sesi Yönlendirildi", targetName);
                    }
                    else if (!string.IsNullOrWhiteSpace(res.Error?.FallbackDeepLinkUri))
                    {
                        OpenShellUri(res.Error.FallbackDeepLinkUri);
                    }
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "startScreenOcr":
                {
                    await Task.Delay(130);
                    using var crop = ScreenRegionOverlayForm.CaptureOcrRegion();
                    if (crop is not null)
                    {
                        var ocr = await ScreenOcrService.ExtractTextFromBitmapAsync(crop);
                        if (ocr.Success && !string.IsNullOrWhiteSpace(ocr.Text))
                        {
                            _suppressNextClipboardCapture = true;
                            try { Clipboard.SetText(ocr.Text); } catch { }
                            _clipboardVaultStore.RecordText(ocr.Text, "Aurora OCR");
                            string snippet = ocr.Text.Replace("\r", " ").Replace("\n", " ");
                            if (snippet.Length > 90) snippet = snippet[..89] + "…";
                            SendToast(sourceMonitorId, $"Metin Yakalandı ({ocr.LineCount} satır)", snippet);
                            await RefreshAndBroadcastStateAsync(forceFull: true);
                        }
                        else
                        {
                            SendToast(sourceMonitorId, "OCR Metin Bulamadı", ocr.ErrorMessage ?? "Seçilen alanda metin tespit edilemedi.");
                        }
                    }
                    break;
                }

            case "startColorPicker":
                {
                    await Task.Delay(130);
                    var picked = ScreenRegionOverlayForm.PickColorFromScreen();
                    if (picked is not null)
                    {
                        string val = picked.CopyRgb ? picked.ColorInfo.Rgb : picked.ColorInfo.Hex;
                        _suppressNextClipboardCapture = true;
                        try { Clipboard.SetText(val); } catch { }
                        _clipboardVaultStore.RecordText(val, "Renk Damlalığı");
                        SendToast(sourceMonitorId, $"Renk Kopyalandı: {val}", $"{picked.ColorInfo.Hex} · {picked.ColorInfo.Rgb} · {picked.ColorInfo.Hsl}");
                        await RefreshAndBroadcastStateAsync(forceFull: true);
                    }
                    break;
                }

            case "checkForUpdates":
                {
                    await CheckForUpdatesCoreAsync(sourceMonitorId, isAutomatic: false);
                    break;
                }

            case "installAvailableUpdate":
                {
                    await DownloadAndInstallUpdateCoreAsync(sourceMonitorId);
                    break;
                }

            case "toggleAutoCheckUpdates":
                {
                    _shellPrefs = _shellPrefs with { AutoCheckUpdatesEnabled = !_shellPrefs.AutoCheckUpdatesEnabled };
                    _shellPrefsStore.Save(_shellPrefs);
                    if (_shellPrefs.AutoCheckUpdatesEnabled)
                        _autoUpdateTimer?.Start();
                    else
                        _autoUpdateTimer?.Stop();

                    SendToast(sourceMonitorId,
                        _shellPrefs.AutoCheckUpdatesEnabled ? "Otomatik Güncelleme Kontrolü Açık" : "Otomatik Güncelleme Kontrolü Kapalı",
                        _shellPrefs.AutoCheckUpdatesEnabled ? "Yeni sürüm yayınlandığında Üst Damla'da bildirim alırsınız." : "İstediğiniz zaman Stüdyo'dan manuel kontrol edebilirsiniz.");
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "copyText":
                {
                    string txt = msg.GetProperty("text").GetString() ?? string.Empty;
                    try { Clipboard.SetText(txt); SendToast(sourceMonitorId, "Kopyalandı", txt); } catch { }
                    break;
                }

            case "startFocusMinutes":
                {
                    int mins = Math.Clamp(msg.GetProperty("minutes").GetInt32(), 1, 120);
                    _focusRemaining = TimeSpan.FromMinutes(mins);
                    _focusRunning = true;
                    _focusTimer?.Start();
                    SendToast(sourceMonitorId, $"{mins} Dakikalık Odak Başladı", "Üst Damla Adadan süreyi takip edebilirsiniz.");
                    await RefreshAndBroadcastStateAsync();
                    break;
                }

            case "toggleFocus":
                _focusRunning = false;
                _focusTimer?.Stop();
                _focusRemaining = TimeSpan.Zero;
                await RefreshAndBroadcastStateAsync();
                break;

            case "toggleWeatherNet":
                {
                    var p = _stateStore.Preferences;
                    await _stateStore.UpdatePreferencesAsync(p with { WeatherNetworkOptIn = !p.WeatherNetworkOptIn });
                    await _widgetVm.RefreshTelemetryIfVisibleAsync(forceWeather: true);
                    await RefreshAndBroadcastStateAsync();
                    break;
                }

            case "clearNotifications":
                await _stateStore.ClearAllNotificationsAsync();
                foreach (var st in _sysNotifBridge.GetRecentSystemToasts())
                    _dismissedSystemNotifIds.Add(st.NotificationId);
                _unreadNotifCount = 0;
                await RefreshAndBroadcastStateAsync(forceFull: true);
                break;

            case "toggleStartup":
                {
                    bool next = !_startupService.IsStartupEnabled();
                    _startupService.SetStartupEnabled(next, Environment.ProcessPath ?? string.Empty);
                    await RefreshAndBroadcastStateAsync();
                    break;
                }

            case "exportTheme":
                {
                    var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ProjectAuroraThemes");
                    Directory.CreateDirectory(dir);
                    var path = Path.Combine(dir, $"{_stateStore.EffectiveTheme.Id}.auroratheme");
                    await _studioVm.ExportCurrentThemeAsync(path);
                    SendToast(sourceMonitorId, "Tema Paketi Aktarıldı", path);
                    break;
                }

            case "openTrayApp":
                {
                    int idx = msg.GetProperty("index").GetInt32();
                    var openProcNames = _taskbarVm.OpenWindows.Select(w => w.ProcessName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var apps = _trayService.GetBackgroundTrayApps(openProcNames);
                    if (idx >= 0 && idx < apps.Count)
                    {
                        var item = apps[idx];
                        _trayService.OpenOrRestoreTrayApp(item.ExecutablePath, item.LaunchUriOverride, item.RepresentativePid);
                    }
                    break;
                }

            case "openTrayAppByKey":
                {
                    string key = msg.GetProperty("key").GetString() ?? string.Empty;
                    var openProcNames = _taskbarVm.OpenWindows.Select(w => w.ProcessName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var item = _trayService.GetBackgroundTrayApps(openProcNames).FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase));
                    if (item is not null)
                        _trayService.OpenOrRestoreTrayApp(item.ExecutablePath, item.LaunchUriOverride, item.RepresentativePid);
                    break;
                }

            case "quitTrayApp":
                {
                    int idx = msg.GetProperty("index").GetInt32();
                    var openProcNames = _taskbarVm.OpenWindows.Select(w => w.ProcessName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var apps = _trayService.GetBackgroundTrayApps(openProcNames);
                    if (idx >= 0 && idx < apps.Count && apps[idx].CanQuitCleanly)
                    {
                        _trayService.QuitTrayAppByProcessName(apps[idx].ProcessName);
                        SendToast(sourceMonitorId, "Uygulama Kapatıldı", apps[idx].DisplayName);
                        await Task.Delay(180);
                        await RefreshAndBroadcastStateAsync();
                    }
                    break;
                }

            case "peekNativeTray":
                {
                    bool wasSuppressed = _appBarService.IsNativeTaskbarSuppressed;
                    _appBarService.SetNativeTaskbarHiddenWhileActive(false);
                    RecomputeAndApplyAllMonitorBounds();
                    SendToast(sourceMonitorId, "Klasik Sistem Tepsisi Gösterildi", wasSuppressed
                        ? "Sağ alt simgeleri kullanabilirsiniz; 10 sn sonra otomatik gizlenir."
                        : "Windows görev çubuğu görünür durumda.");
                    await RefreshAndBroadcastStateAsync();

                    if (wasSuppressed)
                    {
                        _trayPeekTimer?.Stop();
                        _trayPeekTimer?.Dispose();
                        _trayPeekTimer = new System.Windows.Forms.Timer { Interval = 10000 };
                        _trayPeekTimer.Tick += async (_, _) =>
                        {
                            _trayPeekTimer?.Stop();
                            _trayPeekTimer?.Dispose();
                            _trayPeekTimer = null;
                            _appBarService.SetNativeTaskbarHiddenWhileActive(true);
                            RecomputeAndApplyAllMonitorBounds();
                            await RefreshAndBroadcastStateAsync();
                        };
                        _trayPeekTimer.Start();
                    }
                    break;
                }

            case "cycleKeyboard":
                {
                    var nextKb = _keyboardService.CycleKeyboardLayout(_lastExternalActiveHwnd);
                    SendToast(sourceMonitorId, $"Klavye: {nextKb.ShortCode}", nextKb.DisplayName);
                    await RefreshAndBroadcastStateAsync();
                    break;
                }

            case "saveAgendaNote":
                {
                    string date = msg.GetProperty("date").GetString() ?? string.Empty;
                    string note = msg.GetProperty("note").GetString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(date))
                    {
                        if (string.IsNullOrWhiteSpace(note)) _agendaNotes.Remove(date);
                        else _agendaNotes[date] = note.Trim();
                        try { await File.WriteAllTextAsync(_agendaFilePath, JsonSerializer.Serialize(_agendaNotes)); } catch { }
                        SendToast(sourceMonitorId, "Ajanda Kaydedildi", date);
                        await RefreshAndBroadcastStateAsync();
                    }
                    break;
                }

            case "pickShelfFile":
                {
                    using var ofd = new OpenFileDialog { Title = "Aurora Dosya Rafına Ekle", Multiselect = true };
                    if (ofd.ShowDialog() == DialogResult.OK && ofd.FileNames.Length > 0)
                        await HandleDroppedFilesAsync(sourceMonitorId, ofd.FileNames);
                    break;
                }

            case "openShelfFile":
                {
                    int idx = msg.GetProperty("index").GetInt32();
                    if (idx >= 0 && idx < _dropShelfPaths.Count)
                        OpenShellUri(_dropShelfPaths[idx]);
                    break;
                }

            case "revealShelfFile":
                {
                    int idx = msg.GetProperty("index").GetInt32();
                    if (idx >= 0 && idx < _dropShelfPaths.Count)
                    {
                        try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_dropShelfPaths[idx]}\"") { UseShellExecute = true }); } catch { }
                    }
                    break;
                }

            case "removeShelfFile":
                {
                    int idx = msg.GetProperty("index").GetInt32();
                    if (idx >= 0 && idx < _dropShelfPaths.Count)
                    {
                        _dropShelfPaths.RemoveAt(idx);
                        PersistDropShelf();
                        await RefreshAndBroadcastStateAsync();
                    }
                    break;
                }

            case "clearShelfFiles":
                _dropShelfPaths.Clear();
                PersistDropShelf();
                await RefreshAndBroadcastStateAsync();
                break;

            case "showDesktop":
                ToggleShowDesktopCleanly(sourceMonitorId);
                await RefreshAndBroadcastStateAsync();
                break;

            case "setAudioOutputDevice":
                {
                    string id = msg.TryGetProperty("id", out var idProp) ? (idProp.GetString() ?? string.Empty) : string.Empty;
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        var res = _audioService.SetDefaultOutputDevice(id);
                        await _ccVm.RefreshAllAsync();
                        if (res.IsSuccess)
                        {
                            SendToast(sourceMonitorId, "Ses Çıkışı Değiştirildi", ShortAudioDeviceName(_ccVm.DefaultAudioDeviceName));
                        }
                        else if (!string.IsNullOrWhiteSpace(res.Error?.FallbackDeepLinkUri))
                        {
                            OpenShellUri(res.Error.FallbackDeepLinkUri);
                        }
                        await RefreshAndBroadcastStateAsync(forceFull: true);
                    }
                    break;
                }

            case "toggleMicMute":
                {
                    var res = _audioService.ToggleDefaultCaptureMute();
                    if (res.IsSuccess)
                    {
                        SendToast(sourceMonitorId,
                            res.Value ? "Mikrofon Sessize Alındı" : "Mikrofon Açık",
                            res.Value ? "Toplantı ve aramalarda sesiniz şu an kapalı." : "Mikrofonunuz aktif durumda.");
                    }
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "setDockMode":
                {
                    string mode = msg.TryGetProperty("mode", out var mProp) ? (mProp.GetString() ?? "AlwaysVisible") : "AlwaysVisible";
                    if (mode is not ("AlwaysVisible" or "SmartDodge" or "AutoHide"))
                        mode = "AlwaysVisible";
                    _shellPrefs = _shellPrefs with { DockVisibilityMode = mode, DockAutoHide = mode == "AutoHide" };
                    _shellPrefsStore.Save(_shellPrefs);
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "completeOnboarding":
                _shellPrefs = _shellPrefs with { OnboardingCompleted = true };
                _shellPrefsStore.Save(_shellPrefs);
                await RefreshAndBroadcastStateAsync(forceFull: true);
                break;

            case "sleepPc":
                BackgroundTrayAppsService.TriggerSystemSleep();
                break;

            case "restartPc":
                try { EnsureNativeTaskbarRestored(); Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { CreateNoWindow = true, UseShellExecute = false }); } catch { }
                break;

            case "shutdownPc":
                try { EnsureNativeTaskbarRestored(); Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 0") { CreateNoWindow = true, UseShellExecute = false }); } catch { }
                break;

            case "openRecycleBin":
                BackgroundTrayAppsService.OpenRecycleBin();
                break;

            case "lockPc":
                try { LockWorkStation(); } catch { }
                break;

            case "openUri":
                OpenShellUri(msg.GetProperty("uri").GetString() ?? "ms-settings:");
                break;

            case "exitAurora":
                ShutdownCleanly();
                break;
        }
    }

    private List<string> GetEffectiveDockFolders()
    {
        if (_shellPrefs.DockFolderPaths is not null)
            return [.. _shellPrefs.DockFolderPaths];
        return DockFolderStackService.GetDefaultFolders();
    }

    private void AddDockFolderPath(string folderPath, string sourceMonitorId)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return;
        var folders = GetEffectiveDockFolders();
        if (!folders.Any(f => string.Equals(f, folderPath, StringComparison.OrdinalIgnoreCase)))
        {
            folders.Add(folderPath);
            if (folders.Count > 4) folders.RemoveAt(0);
            _shellPrefs = _shellPrefs with { DockFolderPaths = folders };
            _shellPrefsStore.Save(_shellPrefs);
            SendToast(sourceMonitorId, "Dock Klasör Yığınına Eklendi", DockFolderStackService.GetFriendlyFolderName(folderPath));
        }
    }

    private async Task HandleDockFilesDroppedAsync(string sourceMonitorId, JsonElement ctx, IReadOnlyList<string> paths)
    {
        string action = ctx.TryGetProperty("action", out var actProp) ? (actProp.GetString() ?? string.Empty) : string.Empty;
        if (string.Equals(action, "dropOnDockApp", StringComparison.OrdinalIgnoreCase))
        {
            string key = ctx.TryGetProperty("key", out var kProp) ? (kProp.GetString() ?? string.Empty) : string.Empty;
            var grp = _taskbarVm.Groups.FirstOrDefault(g => g.GroupKey == key);
            if (grp is not null)
            {
                bool opened = DockFolderStackService.OpenFilesWithApp(grp.ExecutablePath, paths);
                if (!opened && grp.Windows.Count > 0)
                {
                    _windowActivation.FocusWindow(grp.Windows[0].Hwnd);
                }
                SendToast(sourceMonitorId, $"{grp.DisplayName} ile Açıldı", $"{paths.Count} öğe aktarıldı.");
                return;
            }
        }

        // If folders were dropped onto the Dock bar itself, pin them cleanly as Dock Folder Stacks!
        var droppedDirs = paths.Where(Directory.Exists).ToList();
        if (droppedDirs.Count > 0)
        {
            foreach (var d in droppedDirs)
                AddDockFolderPath(d, sourceMonitorId);
            await RefreshAndBroadcastStateAsync(forceFull: true);
            return;
        }

        // Otherwise pin dropped files to Aurora Drop Shelf cleanly
        await HandleDroppedFilesAsync(sourceMonitorId, paths);
    }

    private async Task HandleCrownFilesDroppedWithContextAsync(string sourceMonitorId, JsonElement ctx, IReadOnlyList<string> paths)
    {
        string zone = ctx.TryGetProperty("zone", out var z) ? (z.GetString() ?? "shelf") : "shelf";
        if (string.Equals(zone, "qr", StringComparison.OrdinalIgnoreCase))
        {
            StartQrShareAndToast(sourceMonitorId, paths);
            await RefreshAndBroadcastStateAsync(forceFull: true);
            return;
        }

        if (string.Equals(zone, "convert", StringComparison.OrdinalIgnoreCase))
        {
            var outcome = await Task.Run(() => LanQrFileShareService.ExecuteQuickConvertOrZip(paths));
            if (outcome.Success && File.Exists(outcome.OutputPath))
            {
                await HandleDroppedFilesAsync(sourceMonitorId, [outcome.OutputPath]);
                SendToast(sourceMonitorId, outcome.SummaryTitle, outcome.SummaryDetail);
            }
            else
            {
                SendToast(sourceMonitorId, outcome.SummaryTitle, outcome.SummaryDetail);
            }
            return;
        }

        await HandleDroppedFilesAsync(sourceMonitorId, paths);
    }

    private void StartQrShareAndToast(string sourceMonitorId, IReadOnlyList<string> paths)
    {
        try
        {
            var session = _lanQrShareService.StartSharing(paths);
            SendToast(sourceMonitorId, "QR Yerel Paylaşım Hazır", $"{session.DisplayName} ({session.SizeLabel}) — Telefonunuzla QR kodu okutun.");
        }
        catch (Exception ex)
        {
            SendToast(sourceMonitorId, "QR Paylaşım Başlatılamadı", ex.Message);
        }
    }

    private async Task PollBluetoothBatteriesBackgroundAsync(bool forceBroadcast = false)
    {
        if (_btPollInFlight) return;
        _btPollInFlight = true;
        try
        {
            var devices = await Task.Run(() => _btBatteryService.QueryConnectedDevicesAsync(includeBleGattFallback: true));
            bool changed = devices.Count != _cachedBtDevices.Count ||
                           !devices.SequenceEqual(_cachedBtDevices);
            _cachedBtDevices = devices;

            var alerts = _btBatteryService.EvaluateAlerts(devices);
            var primary = GetPrimaryMonitorHost();
            if (primary is not null && !primary.CrownForm.IsDisposed && primary.CrownForm.IsHandleCreated)
            {
                primary.CrownForm.BeginInvoke(async () =>
                {
                    foreach (var al in alerts)
                    {
                        if (_shellPrefs.HardwareOsdEnabled)
                        {
                            string kind = al.Kind == "lowBattery" ? "bluetoothLow" : "bluetooth";
                            string lbl = al.Kind == "lowBattery" ? $"{al.Device.Name} Pil Düşük" : $"{al.Device.Name} Bağlandı";
                            var host = GetMonitorHostUnderCursor();
                            if (host is not null && !host.CrownForm.IsSuppressedForFullscreen)
                            {
                                SendOsdHud(host.Monitor.DeviceId, new HardwareOsdEvent(kind, lbl, $"%{al.Device.BatteryPercent}", al.Device.BatteryPercent, al.Kind != "lowBattery", 2800));
                            }
                        }
                    }

                    if (changed || forceBroadcast || alerts.Count > 0)
                        await RefreshAndBroadcastStateAsync(windowTopologyOnly: true);
                });
            }
        }
        catch { }
        finally
        {
            _btPollInFlight = false;
        }
    }

    private void RecordTelemetrySparklineSample()
    {
        var m = _widgetVm.Metrics;
        _cpuHistory.Add(Math.Round(m.CpuUsagePercent, 1));
        _ramHistory.Add(Math.Round(m.RamUsagePercent, 1));
        _netHistory.Add(Math.Round(m.NetworkDownloadKbps + m.NetworkUploadKbps, 1));
        if (_cpuHistory.Count > 28) _cpuHistory.RemoveAt(0);
        if (_ramHistory.Count > 28) _ramHistory.RemoveAt(0);
        if (_netHistory.Count > 28) _netHistory.RemoveAt(0);
        _topProcesses = WindowsSystemMetricsService.GetTopMemoryProcesses(3);
    }

    private void BroadcastGifRecordingState(bool recording, int elapsedSec)
    {
        string json = JsonSerializer.Serialize(new { type = "gifRecState", recording, elapsed = elapsedSec }, JsonOpts);
        foreach (var h in _monitorHosts.Values)
            h.CrownForm.PostJsonState(json);
    }

    private async Task HandleDroppedFilesAsync(string sourceMonitorId, IReadOnlyList<string> paths)
    {
        int added = 0;
        foreach (var p in paths)
        {
            if (string.IsNullOrWhiteSpace(p) || (!File.Exists(p) && !Directory.Exists(p)))
                continue;
            _dropShelfPaths.RemoveAll(existing => string.Equals(existing, p, StringComparison.OrdinalIgnoreCase));
            _dropShelfPaths.Insert(0, p);
            added++;
        }

        if (_dropShelfPaths.Count > 16)
            _dropShelfPaths.RemoveRange(16, _dropShelfPaths.Count - 16);

        if (added > 0)
        {
            PersistDropShelf();
            SendToast(sourceMonitorId, "Dosya Rafına Eklendi", $"{added} öğe Aurora Süper Rafına sabitlendi.");
            await RefreshAndBroadcastStateAsync();
        }
    }

    private void PersistDropShelf()
    {
        try { File.WriteAllText(_dropShelfFilePath, JsonSerializer.Serialize(_dropShelfPaths)); } catch { }
    }

    private void RecordSearchFrecency(string targetKey)
    {
        if (string.IsNullOrWhiteSpace(targetKey) || targetKey.StartsWith("reveal:", StringComparison.OrdinalIgnoreCase))
            return;

        _searchFrecency[targetKey] = _searchFrecency.TryGetValue(targetKey, out int c) ? Math.Min(c + 1, 50) : 1;
        _cachedStaticCatalogJson = null;
        try { File.WriteAllText(_frecencyFilePath, JsonSerializer.Serialize(_searchFrecency)); } catch { }

        var frecJson = JsonSerializer.Serialize(new { type = "frecency", frecency = _searchFrecency }, JsonOpts);
        foreach (var h in _monitorHosts.Values)
            h.CrownForm.PostJsonState(frecJson);
    }

    private object[] BuildDropShelfDtos()
    {
        var list = new List<object>(_dropShelfPaths.Count);
        foreach (var p in _dropShelfPaths)
        {
            if (!File.Exists(p) && !Directory.Exists(p)) continue;
            string sizeLabel = "Klasör";
            if (File.Exists(p))
            {
                try
                {
                    long len = new FileInfo(p).Length;
                    sizeLabel = len >= 1024 * 1024 ? $"{len / (1024.0 * 1024.0):0.1} MB" : $"{Math.Max(1, len / 1024)} KB";
                }
                catch { sizeLabel = "Dosya"; }
            }
            var icon = Win32IconExtractor.ResolveShortcutIconPng(p);
            list.Add(new
            {
                name = Path.GetFileName(p),
                path = p,
                sizeLabel,
                icon = EncodePngMemoized(icon)
            });
        }
        return [.. list];
    }

    private async Task TickFocusAsync()
    {
        if (!_focusRunning) return;
        _focusRemaining -= TimeSpan.FromSeconds(1);
        if (_focusRemaining <= TimeSpan.Zero)
        {
            _focusRunning = false;
            _focusTimer?.Stop();
            _focusRemaining = TimeSpan.Zero;
            await _stateStore.AddNotificationAsync(
                "Odak Motoru",
                NotificationCategory.Reminder,
                NotificationSeverity.Success,
                "Odak Süresi Tamamlandı!",
                "Tebrikler! Kısa bir mola verebilirsiniz.");
            SendToast(null, "Odak Süresi Tamamlandı!", "Harika bir odak oturumu geçirdiniz.");
        }
        await RefreshAndBroadcastStateAsync();
    }

    private void RestoreClipboardItemToSystem(ClipboardVaultEntry item, string sourceMonitorId)
    {
        try
        {
            _suppressNextClipboardCapture = true;
            bool copied = false;
            if (item.Kind == "image" && !string.IsNullOrWhiteSpace(item.ImagePath) && File.Exists(item.ImagePath))
            {
                using var img = Image.FromFile(item.ImagePath);
                Clipboard.SetImage(img);
                copied = true;
                if (!_shellPrefs.ClipboardAutoPasteEnabled)
                    SendToast(sourceMonitorId, "Görsel Panoya Kopyalandı", item.PreviewText);
            }
            else if (!string.IsNullOrWhiteSpace(item.TextContent))
            {
                Clipboard.SetText(item.TextContent);
                copied = true;
                if (!_shellPrefs.ClipboardAutoPasteEnabled)
                    SendToast(sourceMonitorId, "Panoya Kopyalandı", item.PreviewText);
            }

            if (copied && _shellPrefs.ClipboardAutoPasteEnabled && _lastExternalActiveHwnd != 0)
            {
                var srcHost = GetMonitorHostById(sourceMonitorId);
                srcHost?.CrownForm.PostJsonState("""{"type":"collapseDeckForPaste"}""");
                nint targetHwnd = _lastExternalActiveHwnd;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(95);
                    _windowActivation.FocusWindow(targetHwnd);
                    await Task.Delay(75);
                    ScreenOcrService.SendCtrlVPaste();
                });
            }
        }
        catch
        {
            _suppressNextClipboardCapture = false;
        }
    }

    private void SampleClipboardSafely()
    {
        if (_suppressNextClipboardCapture)
        {
            _suppressNextClipboardCapture = false;
            return;
        }

        try
        {
            // Industrial privacy guard: never record passwords copied from Bitwarden/1Password/KeePass/Edge
            if (ScreenOcrService.ShouldIgnoreCurrentClipboardForPrivacy())
                return;

            var fgWin = _taskbarVm.OpenWindows.FirstOrDefault(w => w.Hwnd == _lastExternalActiveHwnd);
            string sourceApp = fgWin is not null ? FormatName(fgWin.ProcessName) : "Pano";

            if (Clipboard.ContainsText())
            {
                string txt = Clipboard.GetText();
                if (!string.IsNullOrWhiteSpace(txt))
                {
                    _clipboardVaultStore.RecordText(txt, sourceApp);
                }
                return;
            }

            if (Clipboard.ContainsImage())
            {
                using var img = Clipboard.GetImage();
                if (img is not null && img.Width >= 4 && img.Height >= 4 && img.Width <= 4096 && img.Height <= 4096)
                {
                    using var bmp = new Bitmap(img);
                    using var ms = new MemoryStream();
                    bmp.Save(ms, ImageFormat.Png);
                    string? thumbB64 = ScreenOcrService.CreatePngThumbnailBase64(bmp, 160, 92);
                    _clipboardVaultStore.RecordImage(ms.ToArray(), thumbB64, bmp.Width, bmp.Height, sourceApp);
                }
            }
        }
        catch { }
    }

    private void UpdateSnapZonesOverlayDuringDrag()
    {
        if (_draggingWindowHwnd == 0 || !_shellPrefs.SnapZonesEnabled || _snapZonesOverlay is null)
        {
            _snapDragTimer?.Stop();
            _snapZonesOverlay?.HideOverlay();
            return;
        }

        bool leftMouseDown = (GetAsyncKeyState(VkLButton) & 0x8000) != 0;
        if (!leftMouseDown)
        {
            CompleteSnapZonesDrag(_draggingWindowHwnd);
            return;
        }

        bool shiftHeld = (GetAsyncKeyState(VkShift) & 0x8000) != 0;
        if (!shiftHeld)
        {
            _snapZonesOverlay.HideOverlay();
            return;
        }

        var host = GetMonitorHostUnderCursor();
        if (host is null)
            return;

        var mon = host.Monitor;
        double scale = mon.EffectiveDpi / 96.0;
        var b = mon.BoundsPx;
        var w = mon.WorkAreaPx;

        // Compute clean snap work-area respecting Aurora's Crown, Dock Edge (Bottom/Left/Right), and Dock Mode
        string edge = _shellPrefs.EffectiveDockEdge;
        bool pinnedDock = _shellPrefs.EffectiveDockMode == "AlwaysVisible";
        int topInset = (int)Math.Round(10 * scale);
        int leftInset = (pinnedDock && edge == "Left") ? (int)Math.Round(66 * scale) : 0;
        int rightInset = (pinnedDock && edge == "Right") ? (int)Math.Round(66 * scale) : 0;
        int bottomInset = _appBarService.IsNativeTaskbarSuppressed
            ? ((pinnedDock && edge == "Bottom") ? (int)Math.Round(68 * scale) : (int)Math.Round(10 * scale))
            : Math.Max(0, b.Bottom - w.Bottom);

        var workRect = new Rectangle(
            b.X + leftInset,
            b.Y + topInset,
            Math.Max(320, b.Width - leftInset - rightInset),
            Math.Max(240, b.Height - topInset - bottomInset));

        string presetId = _shellPrefs.EffectiveSnapZonePreset;
        var presetInfo = SnapZonesLayoutCatalog.AllPresets.FirstOrDefault(p => p.Id == presetId);
        var zones = SnapZonesLayoutCatalog.ComputeZones(workRect, presetId, gapPx: (int)Math.Round(10 * scale));
        _snapZonesOverlay.UpdateZones(
            new Rectangle(b.X, b.Y, b.Width, b.Height),
            zones,
            presetInfo?.DisplayName ?? "Kodlama 65/35",
            Cursor.Position);
    }

    private void CompleteSnapZonesDrag(nint hwnd)
    {
        _snapDragTimer?.Stop();
        var targetHwnd = _draggingWindowHwnd != 0 ? _draggingWindowHwnd : hwnd;
        _draggingWindowHwnd = 0;

        if (_snapZonesOverlay is null)
            return;

        SnapZoneSpec? zone = _snapZonesOverlay.IsOverlayVisible ? _snapZonesOverlay.ActiveHoveredZone : null;
        _snapZonesOverlay.HideOverlay();

        if (zone is not null && targetHwnd != 0)
        {
            SnapZonesLayoutCatalog.SnapWindowToRectExact(targetHwnd, zone.BoundsPx);
        }
    }

    private IReadOnlyList<Rectangle> GetAllMonitorRectangles()
    {
        var list = new List<Rectangle>(_monitorHosts.Count);
        foreach (var h in _monitorHosts.Values)
        {
            var b = h.Monitor.BoundsPx;
            list.Add(new Rectangle(b.X, b.Y, b.Width, b.Height));
        }
        return list;
    }

    private MonitorSurfaceHost? GetMonitorHostAtPoint(Point pt)
    {
        foreach (var h in _monitorHosts.Values)
        {
            var b = h.Monitor.BoundsPx;
            if (pt.X >= b.X && pt.X < b.Right && pt.Y >= b.Y && pt.Y < b.Bottom)
                return h;
        }
        return GetMonitorHostUnderCursor() ?? GetPrimaryMonitorHost();
    }

    private nint ResolveDockHwndAtPoint(Point pt)
    {
        if (_allMonitorsFullscreen)
            return 0;

        foreach (var h in _monitorHosts.Values)
        {
            if (h.DockForm.ContainsInteractivePoint(pt))
                return h.DockForm.Handle;
        }
        return 0;
    }

    private void HandleDockAltDragStarted(bool isResize, Point startPt)
    {
        _dockDragOverlay ??= new DockDragOverlayForm();
        var host = GetMonitorHostAtPoint(startPt);
        if (host is null) return;
        var b = host.Monitor.BoundsPx;
        var monRect = new Rectangle(b.X, b.Y, b.Width, b.Height);

        if (isResize)
        {
            _dockResizeStartScale = _shellPrefs.EffectiveDockScalePercent;
            _dockResizeLiveScale = _dockResizeStartScale;
            _dockDragOverlay.UpdateResizeHud(monRect, _shellPrefs.EffectiveDockEdge, _dockResizeLiveScale, startPt);
        }
        else
        {
            var (edge, offset) = AltDragWindowService.ResolveTargetDockEdgeAndOffset(monRect, startPt);
            _dockMovePreviewEdge = edge;
            _dockMovePreviewOffset = offset;
            _dockDragOverlay.UpdateMovePreview(monRect, edge, offset, _shellPrefs.EffectiveDockScalePercent);
        }
    }

    private void HandleDockAltDragMoved(bool isResize, Point startPt, Point curPt)
    {
        _dockDragOverlay ??= new DockDragOverlayForm();
        var host = GetMonitorHostAtPoint(curPt);
        if (host is null) return;
        var b = host.Monitor.BoundsPx;
        var monRect = new Rectangle(b.X, b.Y, b.Width, b.Height);

        if (isResize)
        {
            int nextScale = AltDragWindowService.ComputeResizedDockScale(
                _dockResizeStartScale,
                _shellPrefs.EffectiveDockEdge,
                curPt.X - startPt.X,
                curPt.Y - startPt.Y);

            if (nextScale != _dockResizeLiveScale)
            {
                _dockResizeLiveScale = nextScale;
                var liveJson = JsonSerializer.Serialize(new { type = "dockLivePreview", scalePercent = nextScale }, JsonOpts);
                foreach (var h in _monitorHosts.Values)
                    h.DockForm.PostJsonState(liveJson);
            }
            _dockDragOverlay.UpdateResizeHud(monRect, _shellPrefs.EffectiveDockEdge, _dockResizeLiveScale, curPt);
        }
        else
        {
            var (edge, offset) = AltDragWindowService.ResolveTargetDockEdgeAndOffset(monRect, curPt);
            _dockMovePreviewEdge = edge;
            _dockMovePreviewOffset = offset;
            _dockDragOverlay.UpdateMovePreview(monRect, edge, offset, _shellPrefs.EffectiveDockScalePercent);
        }
    }

    private async Task HandleDockAltDragEndedAsync(bool isResize, Point startPt, Point endPt)
    {
        _dockDragOverlay?.HideOverlay();
        var host = GetMonitorHostAtPoint(endPt);
        string? monId = host?.Monitor.DeviceId;

        if (isResize)
        {
            int finalScale = AltDragWindowService.ComputeResizedDockScale(
                _dockResizeStartScale,
                _shellPrefs.EffectiveDockEdge,
                endPt.X - startPt.X,
                endPt.Y - startPt.Y);

            if (finalScale != _shellPrefs.EffectiveDockScalePercent)
            {
                _shellPrefs = _shellPrefs with { DockScalePercent = finalScale };
                _shellPrefsStore.Save(_shellPrefs);
            }
            await RefreshAndBroadcastStateAsync(forceFull: true);
        }
        else
        {
            if (host is not null)
            {
                var b = host.Monitor.BoundsPx;
                var monRect = new Rectangle(b.X, b.Y, b.Width, b.Height);
                var (edge, offset) = AltDragWindowService.ResolveTargetDockEdgeAndOffset(monRect, endPt);
                _dockMovePreviewEdge = edge;
                _dockMovePreviewOffset = offset;
            }

            bool edgeChanged = !string.Equals(_shellPrefs.EffectiveDockEdge, _dockMovePreviewEdge, StringComparison.Ordinal);
            bool offsetChanged = _shellPrefs.EffectiveDockOffsetPercent != _dockMovePreviewOffset;

            if (edgeChanged || offsetChanged)
            {
                _shellPrefs = _shellPrefs with
                {
                    DockEdge = _dockMovePreviewEdge,
                    DockOffsetPercent = _dockMovePreviewOffset
                };
                _shellPrefsStore.Save(_shellPrefs);
                if (edgeChanged)
                {
                    RecomputeAndApplyAllMonitorBounds();
                }
                EvaluateFullscreenYieldPerMonitor();

                string edgeLbl = _dockMovePreviewEdge == "Left" ? "Sol Dikey" : (_dockMovePreviewEdge == "Right" ? "Sağ Dikey" : "Alt Yatay");
                string posLbl = _dockMovePreviewOffset == 0 ? "Orta" : (_dockMovePreviewOffset > 0 ? $"+%{_dockMovePreviewOffset}" : $"%{_dockMovePreviewOffset}");
                SendToast(monId, $"Dock Yerleştirildi: {edgeLbl}", $"Hizalama: {posLbl} • Alt+Sağ Tık ile boyutlandırabilirsin.");
                await RefreshAndBroadcastStateAsync(forceFull: true);
                foreach (var h in _monitorHosts.Values)
                    h.DockForm.RequestLayoutSync(force: true);
            }
        }
    }

    private void HandleInterceptedVolumeKey(uint vk)
    {
        if (!_shellPrefs.HardwareOsdEnabled)
            return;

        try
        {
            var volRes = _audioService.GetDefaultOutputState();
            int curPct = volRes.IsSuccess ? (int)Math.Round(volRes.Value.VolumeScalar * 100) : (int)Math.Round(_ccVm.MasterVolumePercent);
            bool curMuted = volRes.IsSuccess ? volRes.Value.IsMuted : _ccVm.IsMasterMuted;

            int nextPct = curPct;
            bool nextMuted = curMuted;

            if (vk == HardwareOsdMonitorService.VkVolumeUp)
            {
                nextPct = HardwareOsdMonitorService.ComputeNextVolumeStep(curPct, stepUp: true);
                nextMuted = false;
                if (curMuted) _audioService.SetMasterMute(false);
                _audioService.SetMasterVolume(nextPct / 100.0);
            }
            else if (vk == HardwareOsdMonitorService.VkVolumeDown)
            {
                nextPct = HardwareOsdMonitorService.ComputeNextVolumeStep(curPct, stepUp: false);
                nextMuted = nextPct == 0 && curMuted;
                if (curMuted && nextPct > 0)
                {
                    nextMuted = false;
                    _audioService.SetMasterMute(false);
                }
                _audioService.SetMasterVolume(nextPct / 100.0);
            }
            else if (vk == HardwareOsdMonitorService.VkVolumeMute)
            {
                nextMuted = !curMuted;
                _audioService.SetMasterMute(nextMuted);
            }

            _osdMonitor.SyncVolumeBaseline(nextPct, nextMuted);

            var evt = nextMuted || nextPct == 0
                ? new HardwareOsdEvent("mute", "Ses", "Sessiz", 0, Active: false)
                : new HardwareOsdEvent("volume", "Ses", $"%{nextPct}", nextPct, Active: true);

            var activeHost = GetMonitorHostUnderCursor();
            if (activeHost is not null && !activeHost.CrownForm.IsSuppressedForFullscreen)
            {
                SendOsdHud(activeHost.Monitor.DeviceId, evt);
            }

            if (_volumeRefreshDebounceTimer is not null)
            {
                _volumeRefreshDebounceTimer.Stop();
                _volumeRefreshDebounceTimer.Start();
            }
        }
        catch { }
    }

    private void PollHardwareOsdSafely(bool forceBrightnessRead)
    {
        if (!_shellPrefs.HardwareOsdEnabled || _allMonitorsFullscreen)
            return;

        try
        {
            var volRes = _audioService.GetDefaultOutputState();
            int volPct = volRes.IsSuccess ? (int)Math.Round(volRes.Value.VolumeScalar * 100) : (int)Math.Round(_ccVm.MasterVolumePercent);
            bool muted = volRes.IsSuccess ? volRes.Value.IsMuted : _ccVm.IsMasterMuted;
            var mic = _audioService.GetDefaultCaptureMuteState();

            // Throttle WMI internal display brightness query (~2.25s when supported, ~13s when unsupported) to keep idle CPU ~0%
            _osdBrightnessCounter++;
            int brightMod = _cachedBrightSupported ? 5 : 30;
            if (forceBrightnessRead || (_osdBrightnessCounter % brightMod == 0))
            {
                var br = _displayPowerService.GetBrightness();
                _cachedBrightSupported = br.IsSupported;
                _cachedBrightValue = br.BrightnessPercent;
            }

            var evt = _osdMonitor.ObserveState(
                volumePercent: volPct,
                masterMuted: muted,
                micSupported: mic.Supported,
                micMuted: mic.IsMuted,
                brightnessSupported: _cachedBrightSupported,
                brightnessPercent: _cachedBrightValue,
                capsOn: HardwareOsdMonitorService.IsCapsLockOn(),
                numOn: HardwareOsdMonitorService.IsNumLockOn());

            if (evt is not null)
            {
                if (evt.Kind is "volume" or "mute" or "brightness")
                {
                    var monRects = GetAllMonitorRectangles();
                    _osdMonitor.TrySuppressNativeHardwareOsdWindows(monRects);
                    var primaryForLateSuppress = GetPrimaryMonitorHost();
                    if (primaryForLateSuppress is not null && !primaryForLateSuppress.CrownForm.IsDisposed && primaryForLateSuppress.CrownForm.IsHandleCreated)
                    {
                        primaryForLateSuppress.CrownForm.BeginInvoke(async () =>
                        {
                            await Task.Delay(75);
                            _osdMonitor.TrySuppressNativeHardwareOsdWindows(GetAllMonitorRectangles());
                        });
                    }
                }

                var activeHost = GetMonitorHostUnderCursor();
                if (activeHost is not null && !activeHost.CrownForm.IsSuppressedForFullscreen)
                {
                    SendOsdHud(activeHost.Monitor.DeviceId, evt);
                }
            }
        }
        catch { }
    }

    private async Task CheckForUpdatesCoreAsync(string? sourceMonitorId, bool isAutomatic)
    {
        if (_isCheckingUpdates || _isDownloadingUpdate)
            return;

        _isCheckingUpdates = true;
        try
        {
            if (!isAutomatic)
            {
                _lastUpdateStatusText = "Kontrol ediliyor...";
                await RefreshAndBroadcastStateAsync(forceFull: true);
            }

            var upd = await AuroraUpdateService.CheckForUpdatesAsync(_shellPrefs.UpdateManifestUrl);
            if (upd.Success && upd.UpdateAvailable && upd.Manifest is not null)
            {
                _pendingUpdateManifest = upd.Manifest;
                _pendingUpdateDownloadUrl = upd.Manifest.DownloadUrl;
                _lastUpdateStatusText = $"Yeni: v{upd.LatestVersion}";

                bool shouldNotify = !isAutomatic || !string.Equals(_lastNotifiedUpdateVersion, upd.LatestVersion, StringComparison.OrdinalIgnoreCase);
                if (shouldNotify)
                {
                    _lastNotifiedUpdateVersion = upd.LatestVersion;
                    string? targetMon = sourceMonitorId ?? GetMonitorHostUnderCursor()?.Monitor.DeviceId ?? GetPrimaryMonitorHost()?.Monitor.DeviceId;
                    SendToast(
                        targetMon,
                        $"Yeni Aurora Sürümü: v{upd.LatestVersion}",
                        "Stüdyo & Ayarlar → İndir ve Güncelle ile ayarların korunarak tek tıkla güncelleyebilirsin.");
                }
            }
            else if (upd.Success)
            {
                _pendingUpdateManifest = null;
                _pendingUpdateDownloadUrl = null;
                _lastUpdateStatusText = $"v{upd.CurrentVersion} Güncel";
                if (!isAutomatic)
                {
                    SendToast(sourceMonitorId, "Sürüm Kontrolü", upd.Message);
                }
            }
            else
            {
                _lastUpdateStatusText = $"v{AuroraUpdateService.CurrentVersion}";
                if (!isAutomatic)
                {
                    SendToast(sourceMonitorId, "Sürüm Kontrolü", upd.Message);
                }
            }

            await RefreshAndBroadcastStateAsync(forceFull: true);
        }
        finally
        {
            _isCheckingUpdates = false;
        }
    }

    private async Task DownloadAndInstallUpdateCoreAsync(string? sourceMonitorId)
    {
        if (_isDownloadingUpdate)
            return;

        if (_pendingUpdateManifest is null)
        {
            await CheckForUpdatesCoreAsync(sourceMonitorId, isAutomatic: false);
            if (_pendingUpdateManifest is null)
                return;
        }

        var manifest = _pendingUpdateManifest;
        if (!AuroraUpdateService.IsDirectInstallerDownloadUrl(manifest.DownloadUrl))
        {
            if (!string.IsNullOrWhiteSpace(manifest.DownloadUrl))
            {
                try { Process.Start(new ProcessStartInfo(manifest.DownloadUrl) { UseShellExecute = true }); } catch { }
            }
            return;
        }

        _isDownloadingUpdate = true;
        _updateDownloadPercent = 0;
        _lastUpdateStatusText = "İndiriliyor %0...";
        await RefreshAndBroadcastStateAsync(forceFull: true);

        try
        {
            long lastBroadcastMs = 0;
            var progress = new Progress<AuroraUpdateDownloadProgress>(p =>
            {
                _updateDownloadPercent = p.Percent;
                _lastUpdateStatusText = p.StageText;
                long now = Environment.TickCount64;
                if (p.Percent >= 99 || now - lastBroadcastMs >= 260)
                {
                    lastBroadcastMs = now;
                    _ = RefreshAndBroadcastStateAsync(forceFull: true);
                }
            });

            var dl = await AuroraUpdateService.DownloadAndVerifyInstallerAsync(manifest, progress);
            if (!dl.Success || string.IsNullOrWhiteSpace(dl.InstallerPath) || !File.Exists(dl.InstallerPath))
            {
                _lastUpdateStatusText = $"Yeni: v{manifest.Version}";
                SendToast(sourceMonitorId, "Güncelleme İndirilemedi", dl.Message);
                return;
            }

            _updateDownloadPercent = 100;
            _lastUpdateStatusText = $"v{manifest.Version} Kuruluyor...";
            SendToast(
                sourceMonitorId,
                $"v{manifest.Version} Kuruluyor",
                dl.Sha256Verified
                    ? "SHA-256 doğrulandı. Ayarlarınız korunarak güncellenip yeniden başlatılacak..."
                    : "Ayarlarınız korunarak güncellenip yeniden başlatılacak...");
            await RefreshAndBroadcastStateAsync(forceFull: true);
            await Task.Delay(550);

            Process.Start(new ProcessStartInfo
            {
                FileName = dl.InstallerPath,
                Arguments = $"--update --wait-pid={Environment.ProcessId}",
                UseShellExecute = true
            });

            ShutdownCleanly();
        }
        catch (Exception ex)
        {
            _lastUpdateStatusText = $"Yeni: v{manifest.Version}";
            SendToast(sourceMonitorId, "Güncelleme Hatası", ex.Message);
        }
        finally
        {
            _isDownloadingUpdate = false;
            await RefreshAndBroadcastStateAsync(forceFull: true);
        }
    }

    private void SendOsdHud(string? targetMonitorId, HardwareOsdEvent evt)
    {
        var json = JsonSerializer.Serialize(new
        {
            type = "osdHud",
            kind = evt.Kind,
            label = evt.Label,
            valueText = evt.ValueText,
            percent = evt.Percent,
            active = evt.Active,
            durationMs = evt.DurationMs
        }, JsonOpts);
        GetMonitorHostById(targetMonitorId)?.CrownForm.PostJsonState(json);
    }

    private void SendToast(string? targetMonitorId, string title, string body)
    {
        var json = JsonSerializer.Serialize(new { type = "toast", title, body }, JsonOpts);
        GetMonitorHostById(targetMonitorId)?.CrownForm.PostJsonState(json);
    }

    private void OnSystemToastArrived(object? sender, SystemToastNotification toast)
    {
        if (!_shellPrefs.SystemNotificationBridgeEnabled)
            return;

        var primary = GetPrimaryMonitorHost();
        if (primary is null || primary.CrownForm.IsDisposed || !primary.CrownForm.IsHandleCreated)
            return;

        try
        {
            primary.CrownForm.BeginInvoke(async () =>
            {
                _unreadNotifCount++;
                var activeWin = _taskbarVm.OpenWindows.FirstOrDefault(w => w.IsActive);
                if (activeWin is null || !toast.AppName.Contains(activeWin.ProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    _attentionService.MarkAttention(toast.AppName);
                }

                bool quiet = _stateStore.Preferences.Notifications.QuietHoursEnabled;
                if (!quiet && !_allMonitorsFullscreen)
                {
                    var activeHost = GetMonitorHostUnderCursor();
                    string header = string.Equals(toast.AppName, toast.Title, StringComparison.OrdinalIgnoreCase)
                        ? toast.AppName
                        : $"{toast.AppName} · {toast.Title}";
                    SendToast(activeHost?.Monitor.DeviceId, header, string.IsNullOrWhiteSpace(toast.Body) ? "Yeni bildirim" : toast.Body);
                }

                await RefreshAndBroadcastStateAsync(windowTopologyOnly: true);
            });
        }
        catch { }
    }

    private async Task RunPostBootDiagnosticsAndCaptureAsync()
    {
        await Task.Delay(1800);
        _crashSentinel.MarkHealthyOrGracefulShutdown();

        try
        {
            using var proc = Process.GetCurrentProcess();
            var health = new
            {
                SettledAtUtc = DateTimeOffset.UtcNow,
                ProcessId = proc.Id,
                Architecture = "MultiMonitor_EventDriven_DeltaSync_v2.0",
                Version = AuroraUpdateService.CurrentVersion,
                PerAppAudioRoutingSupported = _perAppAudioRoutingSupported,
                ClipboardVaultItems = _clipboardVaultStore.GetTotalCount(),
                WorkingSetMb = Math.Round(proc.WorkingSet64 / (1024.0 * 1024.0), 1),
                PrivateMemoryMb = Math.Round(proc.PrivateMemorySize64 / (1024.0 * 1024.0), 1),
                EventDrivenWinHook = _windowEventHook.IsActive,
                PersistedShellPrefs = _shellPrefs,
                ActiveTheme = _stateStore.EffectiveTheme.Id,
                ActiveProfile = _stateStore.ActiveProfile.Id,
                MonitorCount = _monitorHosts.Count,
                MonitorDevices = _monitorHosts.Keys.ToArray(),
                MonitorSurfaces = _monitorHosts.Values.Select(h => new
                {
                    DeviceId = h.Monitor.DeviceId,
                    Dpi = h.Monitor.EffectiveDpi,
                    DockBounds = $"{h.DockForm.Bounds.X},{h.DockForm.Bounds.Y},{h.DockForm.Bounds.Width}x{h.DockForm.Bounds.Height}",
                    DockWin32Rect = $"{h.DockForm.ActualWin32Rect.X},{h.DockForm.ActualWin32Rect.Y},{h.DockForm.ActualWin32Rect.Width}x{h.DockForm.ActualWin32Rect.Height}",
                    DockWebViewBounds = $"{h.DockForm.WebViewControlBounds.X},{h.DockForm.WebViewControlBounds.Y},{h.DockForm.WebViewControlBounds.Width}x{h.DockForm.WebViewControlBounds.Height}",
                    DockViewportCss = $"{Math.Round(h.DockForm.ViewportCssSize.Width)}x{Math.Round(h.DockForm.ViewportCssSize.Height)}",
                    DockHitRects = h.DockForm.InteractiveRectCount,
                    DockHitRectCoords = h.DockForm.InteractiveRectsSummary,
                    DockClickThrough = h.DockForm.IsCurrentlyClickThrough,
                    DockSuppressedFs = h.DockForm.IsSuppressedForFullscreen
                }).ToArray(),
                KeyboardLayout = _keyboardService.GetActiveLayout(_lastExternalActiveHwnd).ShortCode,
                BackgroundTrayApps = _trayService.GetBackgroundTrayApps().Count,
                NativeTaskbarSuppressed = _appBarService.IsNativeTaskbarSuppressed,
                IndexedInstalledApps = EnsureInstalledAppsCached().Length,
                TotalDockGroups = _taskbarVm.Groups.Count
            };

            var healthPath = Path.Combine(_logger.LogDirectoryPath, "runtime_health.json");
            await File.WriteAllTextAsync(healthPath, JsonSerializer.Serialize(health, new JsonSerializerOptions { WriteIndented = true }));

            if (_args.Any(a => a.Equals("--probe-edge-transitions", StringComparison.OrdinalIgnoreCase)))
            {
                var origEdge = _shellPrefs.DockEdge;
                var steps = new List<object>();
                foreach (var (testEdge, tucked, testOff) in new[] { ("Bottom", false, 0), ("Left", false, -15), ("Bottom", true, 0), ("Left", true, -15) })
                {
                    _shellPrefs = _shellPrefs with { DockEdge = testEdge, DockOffsetPercent = testOff, DockScalePercent = 75 };
                    RecomputeAndApplyAllMonitorBounds();
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    foreach (var h in _monitorHosts.Values)
                    {
                        h.DockForm.PostJsonState(JsonSerializer.Serialize(new { type = "dodgeState", dodged = tucked }, JsonOpts));
                        h.DockForm.RequestLayoutSync(force: true);
                    }
                    await Task.Delay(700);
                    steps.Add(new
                    {
                        Edge = testEdge,
                        Tucked = tucked,
                        Offset = testOff,
                        Surfaces = _monitorHosts.Values.Select(h =>
                        {
                            var probePt = new Point(
                                testEdge == "Left" ? h.Monitor.BoundsPx.X + 26 : (testEdge == "Right" ? h.Monitor.BoundsPx.Right - 26 : h.Monitor.BoundsPx.X + h.Monitor.BoundsPx.Width / 2),
                                testEdge == "Bottom" ? h.Monitor.BoundsPx.Bottom - 20 : h.Monitor.BoundsPx.Y + h.Monitor.BoundsPx.Height / 2);
                            return new
                            {
                                DeviceId = h.Monitor.DeviceId,
                                Dpi = h.Monitor.EffectiveDpi,
                                DockWin32Rect = $"{h.DockForm.ActualWin32Rect.X},{h.DockForm.ActualWin32Rect.Y},{h.DockForm.ActualWin32Rect.Width}x{h.DockForm.ActualWin32Rect.Height}",
                                DockViewportCss = $"{Math.Round(h.DockForm.ViewportCssSize.Width)}x{Math.Round(h.DockForm.ViewportCssSize.Height)}",
                                DockHitRects = h.DockForm.InteractiveRectsSummary,
                                ContainsPt = h.DockForm.ContainsInteractivePoint(probePt),
                                Win32Probe = h.DockForm.ProbeWin32HitRouting(probePt)
                            };
                        }).ToArray()
                    });
                }
                _shellPrefs = _shellPrefs with { DockEdge = origEdge };
                var probePath = Path.Combine(_logger.LogDirectoryPath, "edge_probe.json");
                await File.WriteAllTextAsync(probePath, JsonSerializer.Serialize(steps, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch { }

        var capArg = _args.FirstOrDefault(a => a.StartsWith("--capture-ui-dir=", StringComparison.OrdinalIgnoreCase));
        var primary = GetPrimaryMonitorHost();
        if (capArg is not null && primary is not null)
        {
            var dir = capArg["--capture-ui-dir=".Length..].Trim('"');
            await primary.CrownForm.CapturePngAsync(Path.Combine(dir, "crown_droplet.png"));
            await primary.DockForm.CapturePngAsync(Path.Combine(dir, "crystal_dock.png"));

            primary.DockForm.PostJsonState("""{"type":"openRadialPreview"}""");
            await Task.Delay(450);
            await primary.DockForm.CapturePngAsync(Path.Combine(dir, "radial_dock.png"));

            primary.DockForm.PostJsonState("""{"type":"openAccordionPreview"}""");
            await Task.Delay(450);
            await primary.DockForm.CapturePngAsync(Path.Combine(dir, "accordion_dock.png"));

            // Visually verify Left Vertical Dock mode in-memory without touching user's saved preferences!
            primary.DockForm.PostJsonState("""{"type":"outsideDeactivate"}""");
            var savedEdgeBeforeCap = _shellPrefs.DockEdge;
            _shellPrefs = _shellPrefs with { DockEdge = "Left", DockOffsetPercent = 0 };
            RecomputeAndApplyAllMonitorBounds();
            await RefreshAndBroadcastStateAsync(forceFull: true);
            await Task.Delay(520);
            await primary.DockForm.CapturePngAsync(Path.Combine(dir, "dock_left_vertical.png"));
            _shellPrefs = _shellPrefs with { DockEdge = savedEdgeBeforeCap };
            RecomputeAndApplyAllMonitorBounds();
            await RefreshAndBroadcastStateAsync(forceFull: true);
            await Task.Delay(200);

            primary.CrownForm.PostJsonState("""{"type":"setDropletPreview","hovered":false}""");
            await Task.Delay(120);
            primary.CrownForm.PostJsonState("""{"type":"osdHud","kind":"volume","label":"Ses","valueText":"%68","percent":68,"active":true,"durationMs":2500}""");
            await Task.Delay(320);
            await primary.CrownForm.CapturePngAsync(Path.Combine(dir, "crown_osd_hud.png"));

            primary.CrownForm.PostJsonState("""{"type":"setDropletPreview","hovered":true}""");
            await Task.Delay(420);
            await primary.CrownForm.CapturePngAsync(Path.Combine(dir, "crown_pill_hovered.png"));

            primary.CrownForm.PostJsonState("""{"type":"toggleTray"}""");
            await Task.Delay(450);
            await primary.CrownForm.CapturePngAsync(Path.Combine(dir, "tray_drawer.png"));

            primary.CrownForm.PostJsonState("""{"type":"toggleDeck","tab":"control"}""");
            await Task.Delay(450);
            await primary.CrownForm.CapturePngAsync(Path.Combine(dir, "crown_control.png"));

            primary.CrownForm.PostJsonState("""{"type":"toggleDeck","tab":"shelf"}""");
            await Task.Delay(120);
            primary.CrownForm.PostJsonState("""{"type":"toggleDeck","tab":"shelf"}""");
            await Task.Delay(450);
            await primary.CrownForm.CapturePngAsync(Path.Combine(dir, "crown_shelf.png"));

            // Populate sparkline history + Top-3 processes for Telemetry visual verification
            for (int si = 0; si < 6; si++)
            {
                _widgetVm.IsSurfaceVisible = true;
                await _widgetVm.RefreshTelemetryIfVisibleAsync();
                RecordTelemetrySparklineSample();
            }
            _widgetVm.IsSurfaceVisible = false;
            await RefreshAndBroadcastStateAsync(forceFull: true);
            primary.CrownForm.PostJsonState("""{"type":"toggleDeck","tab":"metrics"}""");
            await Task.Delay(120);
            primary.CrownForm.PostJsonState("""{"type":"toggleDeck","tab":"metrics"}""");
            await Task.Delay(450);
            await primary.CrownForm.CapturePngAsync(Path.Combine(dir, "crown_metrics.png"));

            primary.CrownForm.PostJsonState("""{"type":"toggleDeck","tab":"studio"}""");
            await Task.Delay(120);
            primary.CrownForm.PostJsonState("""{"type":"toggleDeck","tab":"studio"}""");
            await Task.Delay(450);
            await primary.CrownForm.CapturePngAsync(Path.Combine(dir, "crown_studio.png"));

            primary.CrownForm.PostJsonState("""{"type":"toggleOmni"}""");
            await Task.Delay(450);
            await primary.CrownForm.CapturePngAsync(Path.Combine(dir, "omnibar_apps.png"));
        }

        if (ParseSmokeExitSeconds() is { } sec)
        {
            await Task.Delay(TimeSpan.FromSeconds(sec));
            ShutdownCleanly();
        }
    }

    private void RecordFocusedHwnd(nint hwnd, string? groupKey = null)
    {
        if (hwnd == 0) return;
        _recentFocusedHwnds.Remove(hwnd);
        _recentFocusedHwnds.Insert(0, hwnd);
        if (_recentFocusedHwnds.Count > 32)
            _recentFocusedHwnds.RemoveRange(32, _recentFocusedHwnds.Count - 32);

        if (!string.IsNullOrWhiteSpace(groupKey))
        {
            _lastFocusedWindowByGroup[groupKey] = hwnd;
            return;
        }

        var grp = _taskbarVm.Groups.FirstOrDefault(g => g.Windows.Any(w => w.Hwnd == hwnd));
        if (grp is not null)
            _lastFocusedWindowByGroup[grp.GroupKey] = hwnd;
    }

    private void ToggleShowDesktopCleanly(string sourceMonitorId)
    {
        var visibleWindows = _taskbarVm.OpenWindows.Where(w => !w.IsMinimized).Select(w => w.Hwnd).ToList();
        if (visibleWindows.Count > 0)
        {
            _minimizedForDesktop.Clear();
            _minimizedForDesktop.AddRange(visibleWindows);
            foreach (var h in visibleWindows)
                _windowActivation.MinimizeWindow(h);
            SendToast(sourceMonitorId, "Masaüstü Gösteriliyor", "Pencereleri geri getirmek için tekrar tıklayın.");
        }
        else if (_minimizedForDesktop.Count > 0)
        {
            foreach (var h in _minimizedForDesktop)
                _windowActivation.RestoreWindow(h);
            _minimizedForDesktop.Clear();
        }
    }

    public void EnsureNativeTaskbarRestored()
    {
        try { _appBarService.UnregisterAll(); } catch { }
    }

    private int? ParseSmokeExitSeconds()
    {
        const string pfx = "--smoke-exit-after-seconds=";
        var m = _args.FirstOrDefault(a => a.StartsWith(pfx, StringComparison.OrdinalIgnoreCase));
        return m is not null && int.TryParse(m[pfx.Length..], out int s) ? Math.Clamp(s, 1, 60) : null;
    }

    private static string FormatName(string raw) =>
        string.IsNullOrWhiteSpace(raw) ? "Uygulama" : char.ToUpperInvariant(raw[0]) + raw[1..];

    private static string ShortAudioDeviceName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Hoparlör";
        string trimmed = raw.Trim();
        int openIdx = trimmed.IndexOf('(');
        int closeIdx = trimmed.LastIndexOf(')');

        if (openIdx >= 2 && closeIdx > openIdx + 1)
        {
            string prefix = trimmed[..openIdx].Trim();
            string inner = trimmed[(openIdx + 1)..closeIdx].Trim();

            bool genericPrefix =
                prefix.StartsWith("Hoparlör", StringComparison.OrdinalIgnoreCase) ||
                prefix.StartsWith("Speaker", StringComparison.OrdinalIgnoreCase) ||
                prefix.StartsWith("Kulaklık", StringComparison.OrdinalIgnoreCase) ||
                prefix.StartsWith("Headphone", StringComparison.OrdinalIgnoreCase) ||
                prefix.StartsWith("Headset", StringComparison.OrdinalIgnoreCase) ||
                prefix.StartsWith("Mikrofon", StringComparison.OrdinalIgnoreCase) ||
                prefix.StartsWith("Microphone", StringComparison.OrdinalIgnoreCase) ||
                prefix.StartsWith("Dijital", StringComparison.OrdinalIgnoreCase) ||
                prefix.StartsWith("Digital", StringComparison.OrdinalIgnoreCase);

            if (genericPrefix && inner.Length >= 2)
            {
                // Clean Windows enumeration prefixes like "2- " and trademark noise
                if (inner.Length > 3 && char.IsDigit(inner[0]) && inner[1] == '-')
                    inner = inner[2..].Trim();
                inner = inner.Replace("(R)", "", StringComparison.OrdinalIgnoreCase)
                             .Replace("(TM)", "", StringComparison.OrdinalIgnoreCase)
                             .Replace("High Definition Audio", "HD Audio", StringComparison.OrdinalIgnoreCase)
                             .Trim();
                trimmed = inner.Length >= 2 ? inner : prefix;
            }
            else
            {
                trimmed = prefix;
            }
        }

        return trimmed.Length > 22 ? trimmed[..21] + "…" : trimmed;
    }

    private static void OpenShellUri(string uri)
    {
        try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); } catch { }
    }

    private void ShutdownCleanly()
    {
        try
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            SystemEvents.SessionEnding -= OnSessionEnding;
            _awakeService.Dispose();
            _osdMonitor.Dispose();
            _lanQrShareService.Dispose();
            _gifRecorder.Dispose();
            _btBatteryTimer?.Stop();
            _btBatteryTimer?.Dispose();
            _metricsLiveTimer?.Stop();
            _metricsLiveTimer?.Dispose();
            _altDragService?.Dispose();
            _osdTimer?.Stop();
            _autoUpdateTimer?.Stop();
            _autoUpdateTimer?.Dispose();
            _sysNotifBridge.Dispose();
            _winKeyHook.Dispose();
            _dwmPreviewForm?.Dispose();
            _snapZonesOverlay?.Dispose();
            _dockDragOverlay?.Dispose();
            _snapDragTimer?.Stop();
            _volumeRefreshDebounceTimer?.Stop();
            _volumeRefreshDebounceTimer?.Dispose();
            _windowEventHook.Dispose();
            _windowEventDebounceTimer?.Stop();
            _syncTimer?.Stop();
            _clockTickTimer?.Stop();
            _clockTickTimer?.Dispose();
            _focusTimer?.Stop();
            _fullscreenTimer?.Stop();
            _trayPeekTimer?.Stop();
            _hotkeyWindow?.Dispose();
            EnsureNativeTaskbarRestored();
            _crashSentinel.MarkHealthyOrGracefulShutdown();
            foreach (var h in _monitorHosts.Values) h.Dispose();
            _monitorHosts.Clear();
            _eventBus.Dispose();
            _logger.Info("AppHost", "GRACEFUL_EXIT", "Aurora Multi-Monitor Shell exited cleanly and restored native taskbars.");
        }
        catch { }
        finally
        {
            ExitThread();
            Environment.Exit(0);
        }
    }

    private sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private const int WmHotkey = 0x0312;
        private const int WmClipboardUpdate = 0x031D;
        private readonly Action<int> _onHotkey;
        private readonly Action? _onClipboardUpdated;
        private bool _clipboardListenerAdded;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(nint hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddClipboardFormatListener(nint hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveClipboardFormatListener(nint hwnd);

        public HotkeyWindow(Action<int> onHotkey, Action? onClipboardUpdated = null)
        {
            _onHotkey = onHotkey;
            _onClipboardUpdated = onClipboardUpdated;
            CreateHandle(new CreateParams());
            if (Handle != 0 && _onClipboardUpdated is not null)
            {
                _clipboardListenerAdded = AddClipboardFormatListener(Handle);
            }
        }

        public void RegisterHotkeys(string omniPreset = "Alt+Space")
        {
            if (Handle == 0) return;
            UnregisterHotKey(Handle, HotkeyOmniBar);
            UnregisterHotKey(Handle, HotkeyDeckControl);
            UnregisterHotKey(Handle, HotkeyDeckStudio);

            var (omniMods, omniVk) = omniPreset switch
            {
                "Ctrl+Space" => (ModControl | ModNoRepeat, VkSpace),
                "Alt+Q" => (ModAlt | ModNoRepeat, (uint)'Q'),
                _ => (ModAlt | ModNoRepeat, VkSpace)
            };
            RegisterHotKey(Handle, HotkeyOmniBar, omniMods, omniVk);
            RegisterHotKey(Handle, HotkeyDeckControl, ModControl | ModAlt | ModNoRepeat, 'A');
            RegisterHotKey(Handle, HotkeyDeckStudio, ModControl | ModAlt | ModNoRepeat, 'S');
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey)
            {
                _onHotkey((int)m.WParam);
                return;
            }
            if (m.Msg == WmClipboardUpdate)
            {
                _onClipboardUpdated?.Invoke();
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Handle != 0)
            {
                if (_clipboardListenerAdded)
                {
                    RemoveClipboardFormatListener(Handle);
                    _clipboardListenerAdded = false;
                }
                UnregisterHotKey(Handle, HotkeyOmniBar);
                UnregisterHotKey(Handle, HotkeyDeckControl);
                UnregisterHotKey(Handle, HotkeyDeckStudio);
                DestroyHandle();
            }
        }
    }
}
