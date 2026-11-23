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

        // 2. CRYSTAL DOCK / FIXED BAR CANVAS — Edge-to-edge full span on Bottom, Left, and Right so Pinned Fixed Bar has 0px corner gaps!
        string edge = _shellPrefs.EffectiveDockEdge;
        Rectangle dockRect;
        if (edge == "Left")
        {
            int dockW = Math.Min(b.Width / 2, (int)Math.Round(460 * scale));
            int dockH = Math.Max(320, b.Height);
            int dockX = b.X;
            int dockY = b.Y;
            dockRect = new Rectangle(dockX, dockY, dockW, dockH);
        }
        else if (edge == "Right")
        {
            int dockW = Math.Min(b.Width / 2, (int)Math.Round(460 * scale));
            int dockH = Math.Max(320, b.Height);
            int dockX = b.Right - dockW;
            int dockY = b.Y;
            dockRect = new Rectangle(dockX, dockY, dockW, dockH);
        }
        else
        {
            int dockW = Math.Max(320, b.Width);
            int dockH = Math.Min(b.Height / 2, (int)Math.Round(450 * scale));
            int dockX = b.X;
            int bottomEdgePx = _appBarService.IsNativeTaskbarSuppressed ? b.Bottom : w.Bottom;
            int dockY = bottomEdgePx - dockH;
            dockRect = new Rectangle(dockX, dockY, dockW, dockH);
        }

        return (new Rectangle(crownX, crownY, crownW, crownH), dockRect);
    }

    private void SyncDockWorkAreaReservation(bool forceReapply = false)
    {
        if (!_appBarService.IsNativeTaskbarSuppressed)
            return;

        bool changed = _appBarService.ApplyDockWorkAreaReservation(
            _shellPrefs.EffectiveDockEdge,
            _shellPrefs.EffectiveDockScalePercent,
            _shellPrefs.EffectiveReserveDockWorkArea,
            _shellPrefs.EffectiveDockDensity,
            forceReapply: forceReapply);

        if (changed)
        {
            ScheduleOneShotMaximizedWindowRefit();
        }
    }

    /// <summary>
    /// Single one-shot post-transition check (~160ms after an explicit Pin/Mode/Edge/Scale change,
    /// NEVER repeating) so any lagging Chromium/Electron/WhatsApp window snaps cleanly to the new
    /// WorkArea while windows already resized by Windows Explorer are left completely untouched.
    /// </summary>
    private void ScheduleOneShotMaximizedWindowRefit()
    {
        int seq = Interlocked.Increment(ref _workAreaRefitSeq);
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(160);
                if (seq != Volatile.Read(ref _workAreaRefitSeq))
                    return;

                var prim = GetPrimaryMonitorHost();
                if (prim is not null && !prim.CrownForm.IsDisposed && prim.CrownForm.IsHandleCreated)
                {
                    prim.CrownForm.BeginInvoke(() =>
                    {
                        if (seq != Volatile.Read(ref _workAreaRefitSeq))
                            return;
                        _appBarService.RefitMismatchedMaximizedWindowsOnce();
                        EvaluateFullscreenYieldPerMonitor();
                    });
                }
                else
                {
                    _appBarService.RefitMismatchedMaximizedWindowsOnce();
                }
            }
            catch { }
        });
    }

    /// <summary>
    /// Single delayed reapply after Windows Explorer asynchronously settles
    /// SHAppBarMessage(ABM_SETSTATE, ABS_AUTOHIDE) or a display topology change (~550ms).
    /// </summary>
    private void ScheduleDockWorkAreaReapply()
    {
        if (!_appBarService.IsNativeTaskbarSuppressed)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(550);
                var prim = GetPrimaryMonitorHost();
                if (prim is not null && !prim.CrownForm.IsDisposed && prim.CrownForm.IsHandleCreated)
                {
                    prim.CrownForm.BeginInvoke(() => SyncDockWorkAreaReservation(forceReapply: true));
                }
                else
                {
                    SyncDockWorkAreaReservation(forceReapply: true);
                }
            }
            catch { }
        });
    }

    private void RecomputeAndApplyAllMonitorBounds()
    {
        SyncDockWorkAreaReservation();
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
        if (_gifBorderOverlay is not null && !_gifBorderOverlay.IsDisposed && _gifBorderOverlay.IsHandleCreated)
            owned.Add(_gifBorderOverlay.Handle);
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

        // Compute clean snap work-area respecting Aurora's Crown, Dock Edge (Bottom/Left/Right), and Pinned Fixed Bar
        string edge = _shellPrefs.EffectiveDockEdge;
        bool reserveForSnap = _appBarService.IsNativeTaskbarSuppressed && _shellPrefs.EffectiveReserveDockWorkArea;
        var reservedPx = Win32AppBarService.ComputeDockReservedWorkArea(
            b,
            scale,
            edge,
            _shellPrefs.EffectiveDockScalePercent,
            reserveForSnap,
            _shellPrefs.EffectiveDockDensity);

        int topInset = (int)Math.Round(10 * scale);
        int leftInset = Math.Max(0, reservedPx.X - b.X);
        int rightInset = Math.Max(0, b.Right - reservedPx.Right);
        int bottomInset = _appBarService.IsNativeTaskbarSuppressed
            ? Math.Max((int)Math.Round(10 * scale), b.Bottom - reservedPx.Bottom)
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
                SyncDockWorkAreaReservation();
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


}
