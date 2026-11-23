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

internal sealed partial class AuroraDesktopCoordinator : ApplicationContext
{
    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyWorkingSet(nint hProcess);

    private static void TrimProcessMemorySafely()
    {
        try
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, blocking: false);
            EmptyWorkingSet(Process.GetCurrentProcess().Handle);
        }
        catch { }
    }
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
    private GifRecordingBorderForm? _gifBorderOverlay;
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
    private IReadOnlyList<BackgroundTrayAppEntry> _cachedTrayApps = [];
    private long _lastTrayRefreshMs;
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
                additionalBrowserArguments: "--renderer-process-limit=1 --disable-site-isolation-trials --js-flags=--max-old-space-size=96 --disable-background-networking --disable-component-update --disable-domain-reliability --disable-features=BackForwardCache,SpareRendererForSitePerProcess,Translate,msEdgeOnDeviceTranslation,msEdgeShoppingAssistant,msEdgeReadAloud,MediaRouter");
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
            // Passive software-change fallback at 900ms (hardware volume/brightness/caps keys trigger instantly via native hook!)
            _osdTimer = new System.Windows.Forms.Timer { Interval = 900 };
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
                    // Release transient cold-boot Shell icon decode buffers once hydration finishes
                    TrimProcessMemorySafely();
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

            // Relaxed 4500ms passive heartbeat (window/audio/clipboard events are instant via native Win32 hooks); yields during fullscreen gaming
            _syncTimer = new System.Windows.Forms.Timer { Interval = 4500 };
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
                        _gifBorderOverlay?.HideBorder();
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
            _gifBorderOverlay?.Dispose();
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
