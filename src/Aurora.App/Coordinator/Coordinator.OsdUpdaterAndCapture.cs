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

internal sealed partial class AuroraDesktopCoordinator
{
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

            // Hydrate live DDC/CI monitors & Bluetooth battery devices for accurate Control Center visual verification
            await Task.Run(async () =>
            {
                try
                {
                    _ddcBrightnessService.DiscoverMonitors();
                    _cachedBtDevices = await _btBatteryService.QueryConnectedDevicesAsync(includeBleGattFallback: false);
                }
                catch { }
            });
            await RefreshAndBroadcastStateAsync(forceFull: true);
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

            // Visually verify Light Theme (Aurora Glass Açık) across ALL 8 surfaces!
            var prevTheme = _stateStore.EffectiveTheme;
            await _stateStore.ApplyThemeAsync(BuiltInThemes.AuroraGlassLight);
            _shellPrefs = _shellPrefs with { DockEdge = "Bottom", DockOffsetPercent = 0 };
            RecomputeAndApplyAllMonitorBounds();
            await RefreshAndBroadcastStateAsync(forceFull: true);
            await Task.Delay(350);
            await primary.DockForm.CapturePngAsync(Path.Combine(dir, "crystal_dock_light.png"));

            _shellPrefs = _shellPrefs with { DockEdge = "Left", DockOffsetPercent = 0 };
            RecomputeAndApplyAllMonitorBounds();
            await RefreshAndBroadcastStateAsync(forceFull: true);
            await Task.Delay(350);
            await primary.DockForm.CapturePngAsync(Path.Combine(dir, "dock_left_vertical_light.png"));

            primary.CrownForm.PostJsonState("""{"type":"outsideDeactivate"}""");
            await Task.Delay(150);
            primary.CrownForm.PostJsonState("""{"type":"toggleDeck","tab":"control"}""");
            await Task.Delay(420);
            await primary.CrownForm.CapturePngAsync(Path.Combine(dir, "crown_control_light.png"));

            primary.CrownForm.PostJsonState("""{"type":"toggleDeck","tab":"shelf"}""");
            await Task.Delay(120);
            primary.CrownForm.PostJsonState("""{"type":"toggleDeck","tab":"shelf"}""");
            await Task.Delay(420);
            await primary.CrownForm.CapturePngAsync(Path.Combine(dir, "crown_shelf_light.png"));

            primary.CrownForm.PostJsonState("""{"type":"toggleDeck","tab":"metrics"}""");
            await Task.Delay(120);
            primary.CrownForm.PostJsonState("""{"type":"toggleDeck","tab":"metrics"}""");
            await Task.Delay(420);
            await primary.CrownForm.CapturePngAsync(Path.Combine(dir, "crown_metrics_light.png"));

            primary.CrownForm.PostJsonState("""{"type":"toggleDeck","tab":"studio"}""");
            await Task.Delay(120);
            primary.CrownForm.PostJsonState("""{"type":"toggleDeck","tab":"studio"}""");
            await Task.Delay(420);
            await primary.CrownForm.CapturePngAsync(Path.Combine(dir, "crown_studio_light.png"));

            primary.CrownForm.PostJsonState("""{"type":"outsideDeactivate"}""");
            await Task.Delay(150);
            primary.CrownForm.PostJsonState("""{"type":"toggleOmni"}""");
            await Task.Delay(420);
            await primary.CrownForm.CapturePngAsync(Path.Combine(dir, "omnibar_apps_light.png"));

            primary.CrownForm.PostJsonState("""{"type":"outsideDeactivate"}""");
            await Task.Delay(150);
            primary.CrownForm.PostJsonState("""{"type":"setDropletPreview","hovered":true}""");
            await Task.Delay(150);
            primary.CrownForm.PostJsonState("""{"type":"toggleTray"}""");
            await Task.Delay(420);
            await primary.CrownForm.CapturePngAsync(Path.Combine(dir, "tray_drawer_light.png"));

            await _stateStore.ApplyThemeAsync(prevTheme);
            _shellPrefs = _shellPrefs with { DockEdge = savedEdgeBeforeCap };
            RecomputeAndApplyAllMonitorBounds();
        }

        if (ParseSmokeExitSeconds() is { } sec)
        {
            await Task.Delay(TimeSpan.FromSeconds(sec));
            ShutdownCleanly();
        }
    }


}
