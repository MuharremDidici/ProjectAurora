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
                    if (nextHide)
                    {
                        SyncDockWorkAreaReservation();
                        SpawnCrashWatchdogIfNeeded();
                    }
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
                    SyncDockWorkAreaReservation();
                    EvaluateFullscreenYieldPerMonitor();
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
                    SyncDockWorkAreaReservation();
                    EvaluateFullscreenYieldPerMonitor();
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

            case "activateNotification":
                {
                    string id = msg.TryGetProperty("id", out var idProp) ? (idProp.GetString() ?? string.Empty) : string.Empty;
                    bool activated = false;

                    if (id.StartsWith("sys-", StringComparison.OrdinalIgnoreCase) && long.TryParse(id[4..], out long sysId))
                    {
                        var toast = _sysNotifBridge.GetRecentSystemToasts().FirstOrDefault(t => t.NotificationId == sysId);
                        if (toast is not null)
                        {
                            // 1. Direct protocol URI deep-link (ms-screensketch:, antigravity-ide:, whatsapp:, msteams:, https:, etc.)
                            if (NotificationActivationService.TryExtractLaunchUri(toast.LaunchArguments, toast.ActivationType, out var uri))
                            {
                                activated = NotificationActivationService.TryActivateViaProtocolUri(uri);
                            }

                            // 2. Native Windows Action Center COM AUMID activation (WhatsApp Desktop, Teams, Store/MSIX apps with chat launch args!)
                            if (!activated &&
                                !string.IsNullOrWhiteSpace(toast.PrimaryId) &&
                                (string.Equals(toast.HandlerType, "app:immersive", StringComparison.OrdinalIgnoreCase) || toast.PrimaryId.Contains('!')))
                            {
                                activated = NotificationActivationService.TryActivateViaAumid(toast.PrimaryId, toast.LaunchArguments);
                            }

                            // 3. Focus matching open desktop window (Antigravity, VS Code, Discord, Chrome, Opera, RustWTM, etc.)
                            if (!activated)
                            {
                                var targetWin = FindMatchingOpenWindowForNotification(toast);
                                if (targetWin is not null)
                                {
                                    _windowActivation.FocusWindow(targetWin.Hwnd);
                                    activated = true;
                                }
                            }

                            // 4. Fallback AUMID / shell:AppsFolder launch if app wasn't open yet
                            if (!activated && !string.IsNullOrWhiteSpace(toast.PrimaryId))
                            {
                                activated = NotificationActivationService.TryActivateViaAumid(toast.PrimaryId, toast.LaunchArguments)
                                    || NotificationActivationService.TryLaunchViaAppsFolder(toast.PrimaryId);
                            }

                            _dismissedSystemNotifIds.Add(sysId);
                            _attentionService.ClearAttention(toast.AppName);
                            if (_unreadNotifCount > 0) _unreadNotifCount--;
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(id))
                    {
                        await _stateStore.DismissNotificationAsync(id);
                        if (_unreadNotifCount > 0) _unreadNotifCount--;
                        activated = true;
                    }

                    await RefreshAndBroadcastStateAsync(windowTopologyOnly: true);
                    break;
                }

            case "toggleReserveDockWorkArea":
                {
                    _shellPrefs = _shellPrefs with { ReserveDockWorkArea = !_shellPrefs.ReserveDockWorkArea };
                    _shellPrefsStore.Save(_shellPrefs);
                    SyncDockWorkAreaReservation();
                    EvaluateFullscreenYieldPerMonitor();
                    SendToast(
                        sourceMonitorId,
                        _shellPrefs.ReserveDockWorkArea ? "Dock Alanı Ayrıldı" : "Tam Ekran Pencere Alanı",
                        _shellPrefs.ReserveDockWorkArea
                            ? "Tam ekran pencereler Dock'un hemen üstünde durur; yazı kutuları artık kapanmaz."
                            : "Pencereler tüm ekran yüksekliğini kullanır (Akıllı Kaçınma ile uyumludur).");
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

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
                    _ddcBrightnessService.QueueSetMonitorBrightness(id, val);
                    if (_shellPrefs.HardwareOsdEnabled)
                    {
                        SendOsdHud(sourceMonitorId, new HardwareOsdEvent("brightness", label, $"%{val}", val, true));
                    }
                    break;
                }

            case "refreshMonitorsBrightness":
                {
                    var found = await Task.Run(() => _ddcBrightnessService.DiscoverMonitors());
                    SendToast(sourceMonitorId, "Ekran Parlaklığı Tarandı", found.Count > 0
                        ? $"{found.Count} ekran algılandı: {string.Join(", ", found.Select(f => f.Name))}"
                        : "DDC/CI destekli ekran bulunamadı (monitör menüsünden DDC/CI açık olmalı).");
                    await RefreshAndBroadcastStateAsync(forceFull: true);
                    break;
                }

            case "refreshBluetoothBatteries":
                {
                    await PollBluetoothBatteriesBackgroundAsync(forceBroadcast: true);
                    SendToast(sourceMonitorId, "Bluetooth Pil Tarandı", _cachedBtDevices.Count > 0
                        ? string.Join(" · ", _cachedBtDevices.Select(d => $"{d.Name} %{d.BatteryPercent}"))
                        : "Pil bilgisi paylaşan bağlı Bluetooth cihazı algılanmadı.");
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
                        _gifBorderOverlay ??= new GifRecordingBorderForm();
                        _gifBorderOverlay.ShowAroundRegion(r);
                        if (_gifRecorder.StartRecording(r))
                        {
                            BroadcastGifRecordingState(true, 0);
                            await RefreshAndBroadcastStateAsync(forceFull: true);
                        }
                        else
                        {
                            _gifBorderOverlay.HideBorder();
                        }
                    }
                    break;
                }

            case "stopGifRecording":
                {
                    _gifBorderOverlay?.HideBorder();
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
                    SyncDockWorkAreaReservation();
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
                    SyncDockWorkAreaReservation();
                    EvaluateFullscreenYieldPerMonitor();
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

    private AppWindowEntry? FindMatchingOpenWindowForNotification(SystemToastNotification toast)
    {
        var wins = _taskbarVm.OpenWindows;
        if (wins.Count == 0)
            return null;

        var hints = new List<string>();
        if (!string.IsNullOrWhiteSpace(toast.AppName)) hints.Add(toast.AppName);
        if (!string.IsNullOrWhiteSpace(toast.PrimaryId))
        {
            foreach (var seg in toast.PrimaryId.Split(['.', '!', '_', '\\', '/'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (seg.Length >= 3 && !seg.Equals("app", StringComparison.OrdinalIgnoreCase) && !seg.Equals("electron", StringComparison.OrdinalIgnoreCase) && !seg.Equals("microsoft", StringComparison.OrdinalIgnoreCase))
                    hints.Add(seg);
            }
        }

        // 1. Prefer window whose title matches toast title + process matches hint
        if (!string.IsNullOrWhiteSpace(toast.Title) && toast.Title.Length >= 3)
        {
            var titleHit = wins.FirstOrDefault(w =>
                w.Title.Contains(toast.Title, StringComparison.OrdinalIgnoreCase) &&
                hints.Any(h => w.ProcessName.Contains(h, StringComparison.OrdinalIgnoreCase) || FormatName(w.ProcessName).Contains(h, StringComparison.OrdinalIgnoreCase)));
            if (titleHit is not null)
                return titleHit;
        }

        // 2. Match by friendly app name or process name
        foreach (var h in hints)
        {
            var hit = wins.FirstOrDefault(w =>
                w.ProcessName.Contains(h, StringComparison.OrdinalIgnoreCase) ||
                FormatName(w.ProcessName).Contains(h, StringComparison.OrdinalIgnoreCase) ||
                w.Title.Contains(h, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
                return hit;
        }

        return null;
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


}
