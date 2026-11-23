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

    private static string GetThemeStyleTag(string visualStyle) => visualStyle switch
    {
        "liquid-glass" => "Refraktif Cam",
        "bento-silk" => "Porselen",
        "linear-precision" => "İsviçre Pro",
        "mono-matrix" => "Donanım",
        "spatial-vision" => "Uzamsal",
        "cyber-prism" => "Taktik HUD",
        "high-contrast" => "WCAG AAA",
        _ => "Modern Stil"
    };

    private (AuroraThemeTokens Effective, AuroraThemeTokens Base, bool HasCustomColors) ResolveEffectiveCustomizedTheme()
    {
        var baseTheme = _stateStore.EffectiveTheme;
        var custom = _shellPrefs.GetThemeCustomization(baseTheme.Id);
        if (custom is null || !custom.HasAnyOverride)
            return (baseTheme, baseTheme, false);

        var applied = ThemeCustomizationHelper.ApplyOverrides(
            baseTheme,
            accentPrimary: custom.AccentPrimary,
            accentSecondary: custom.AccentSecondary,
            surface1: custom.Surface1,
            surface2: custom.Surface2,
            textPrimary: custom.TextPrimary,
            surfaceOpacity: custom.SurfaceOpacity);

        return (applied, baseTheme, true);
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
                desc = t.Description,
                visualStyle = t.VisualStyle,
                styleTag = GetThemeStyleTag(t.VisualStyle),
                mode = t.ColorMode == AuroraColorMode.Light ? "Açık" : "Koyu",
                isLight = t.ColorMode == AuroraColorMode.Light,
                accent = t.Colors.AccentPrimary,
                accent2 = t.Colors.AccentSecondary,
                surface1 = t.Colors.Surface1,
                surface2 = t.Colors.Surface2
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

            var (theme, baseTheme, hasCustomColors) = ResolveEffectiveCustomizedTheme();
            var m = _widgetVm.Metrics;
            var wx = _widgetVm.Weather;
            var media = _mediaVm.SelectedSession;
            var kb = _keyboardService.GetActiveLayout(_lastExternalActiveHwnd);
            var openProcNames = _taskbarVm.OpenWindows.Select(w => w.ProcessName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            long nowMs = Environment.TickCount64;
            if (forceFull || _cachedTrayApps.Count == 0 || (nowMs - _lastTrayRefreshMs) >= 6000)
            {
                _cachedTrayApps = _trayService.GetBackgroundTrayApps(openProcNames);
                _lastTrayRefreshMs = nowMs;
            }
            var trayApps = _cachedTrayApps;

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
            double contrastRatio = ContrastCalculator.CalculateContrastRatio(theme.Colors.TextPrimary, theme.Colors.Surface1);
            var themeDto = new
            {
                id = theme.Id,
                displayName = theme.DisplayName,
                visualStyle = theme.VisualStyle,
                styleTag = GetThemeStyleTag(theme.VisualStyle),
                colorMode = theme.ColorMode.ToString(),
                surfaceOpacity = theme.Material.SurfaceOpacity,
                hasCustomColors,
                contrastRatio,
                meetsWcagAA = contrastRatio >= 4.5,
                colors = new
                {
                    canvas = theme.Colors.Canvas,
                    surface1 = theme.Colors.Surface1,
                    surface2 = theme.Colors.Surface2,
                    surface3 = theme.Colors.Surface3,
                    textPrimary = theme.Colors.TextPrimary,
                    textSecondary = theme.Colors.TextSecondary,
                    accentPrimary = theme.Colors.AccentPrimary,
                    accentSecondary = theme.Colors.AccentSecondary,
                    borderSubtle = theme.Colors.BorderSubtle,
                    borderStrong = theme.Colors.BorderStrong
                },
                defaultColors = new
                {
                    surface1 = baseTheme.Colors.Surface1,
                    surface2 = baseTheme.Colors.Surface2,
                    textPrimary = baseTheme.Colors.TextPrimary,
                    accentPrimary = baseTheme.Colors.AccentPrimary,
                    accentSecondary = baseTheme.Colors.AccentSecondary,
                    surfaceOpacity = baseTheme.Material.SurfaceOpacity
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
                    dockBarLayout = _shellPrefs.EffectiveDockBarLayout,
                    dockDensity = effectiveDockDensity,
                    dockEdge = effectiveDockEdge,
                    dockScalePercent = _shellPrefs.EffectiveDockScalePercent,
                    dockOffsetPercent = _shellPrefs.EffectiveDockOffsetPercent,
                    reserveDockWorkArea = _shellPrefs.EffectiveReserveDockWorkAreaPreference,
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
                    dockBarLayout = _shellPrefs.EffectiveDockBarLayout,
                    dockDensity = effectiveDockDensity,
                    dockEdge = effectiveDockEdge,
                    dockScalePercent = _shellPrefs.EffectiveDockScalePercent,
                    dockOffsetPercent = _shellPrefs.EffectiveDockOffsetPercent,
                    reserveDockWorkArea = _shellPrefs.EffectiveReserveDockWorkAreaPreference,
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


}
