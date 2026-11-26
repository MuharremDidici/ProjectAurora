use std::collections::{HashMap, HashSet};
use std::path::Path;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;

use aurora_core::{
    built_in_themes, contrast_ratio, default_dock_widgets, default_starter_widgets,
    default_workspace_profiles, theme_by_id, AiProviderQuotaConfig, AiProviderQuotaSnapshot,
    AppWindowEntry, AudioStateSnapshot, BackgroundTrayApp, BluetoothBatteryDevice,
    ClipboardVaultEntry, ColorMode, CountdownConfig, DesktopWidgetInstance, DiskUsageEntry,
    DockAppGroup, DockWidgetConfig, ExternalMonitorBrightness, InstalledAppEntry, LanQrShareInfo,
    LocalAiStatusSnapshot, MarketSnapshot, MediaSessionSnapshot, PhysicalRect, PinnedShortcut,
    PrivacySnapshot, ReleaseManifest, ShellRuntimePreferences, SystemMetricsSnapshot,
    SystemToastNotification, ThemeColorCustomization, ThemeTokens, TodoItem, WeatherSnapshot,
    WorkspaceProfile, AURORA_VERSION,
};
use aurora_storage::{
    AuroraBackupBundle, AuroraDb, AuxiliaryStateStore, ClipboardVaultDb, PrivacySafeLogger,
    ShellPreferencesStore, UserPreferencesRecord, WidgetsLayoutStore, WidgetsPersistenceDoc,
};
use aurora_surfaces::SnipMode;
use aurora_win32::{
    activate_notification, activate_or_toggle_window, arrange_window, close_window,
    compute_next_volume_step, convert_image_file, create_virtual_desktop, cycle_keyboard_layout,
    effective_dock_folders, extract_title_progress_percent, focus_window, format_size,
    friendly_folder_name, is_startup_enabled, is_windows_apps_sandbox_path, launch_target,
    launch_target_with_args, minimize_window, open_recycle_bin, open_task_view,
    open_windows_notification_center, render_qr_svg, repair_corrupted_windows_path,
    save_ai_api_key, send_ctrl_v_paste, send_media_command,
    set_alt_drag_enabled, set_awake_keep_alive, set_clipboard_text, set_snap_zones_enabled,
    set_startup_enabled, terminate_user_process_safe, toggle_always_on_top,
    trigger_lock_workstation, trigger_power_command, trigger_sleep, zip_shelf_files,
    BrightnessController, CoreAudioController, DwmLivePreviewWindow, LanQrShareServer,
    NativeAppBarManager,
};
use crossbeam_channel::Sender;
use serde_json::{json, Value};

use crate::actions::{
    build_dock_groups_ordered, normalize_group_key, reorder_running_group, update_stable_orders,
    user_known_folder,
};
use crate::webview_host::WebViewOverlaySurface;

/// Background async worker requests dispatched off the UI thread.
#[derive(Debug, Clone)]
pub enum BgCommand {
    CheckUpdates(String),
    InstallUpdate(ReleaseManifest),
    SearchFiles { query: String, seq: u64 },
    RunOcr(PhysicalRect),
    StartGifRecord { rect: PhysicalRect, stop_flag: Arc<AtomicBool> },
    ExtractIconPng { key: String, hwnd: Option<isize>, exe_path: String, aumid: Option<String> },
    PickDockFolder,
    PickShelfFile { share_qr_after: bool },
    PickWidgetPhoto { widget_id: String },
    LoadWidgetPhoto { widget_id: String, path: String },
    ReadFolderStack { monitor_id: String, path: String },
    RefreshSlowHardware,
    RefreshWeather,
    RefreshAiQuota(Vec<AiProviderQuotaConfig>),
    RefreshMarket,
    RefreshLocalAiAndDisks,
    SyncWallpaperAccent(String),
    CheckClipboard,
}

pub struct RuntimeState {
    pub version: String,
    pub theme_id: String,
    pub active_profile_id: String,
    pub focus_mode_enabled: bool,
    pub prefs: ShellRuntimePreferences,
    pub profiles: Vec<WorkspaceProfile>,
    pub pinned_shortcuts: Vec<PinnedShortcut>,
    pub widgets: Vec<DesktopWidgetInstance>,
    pub dock_widgets: Vec<DockWidgetConfig>,
    pub todos: Vec<TodoItem>,
    pub countdown: CountdownConfig,
    pub ai_configs: Vec<AiProviderQuotaConfig>,
    pub ai_snapshots: Vec<AiProviderQuotaSnapshot>,
    pub market: MarketSnapshot,
    pub local_ai: LocalAiStatusSnapshot,
    pub disks: Vec<DiskUsageEntry>,
    pub widget_edit_mode: bool,
    pub dock_edit_mode: bool,

    pub open_windows: Vec<AppWindowEntry>,
    pub dock_groups: Vec<DockAppGroup>,
    pub running_group_order: Vec<String>,
    pub window_seen_order: Vec<isize>,
    pub icon_png_b64: HashMap<String, String>,
    pub icon_requested: HashSet<String>,
    pub installed_apps: Vec<InstalledAppEntry>,
    pub audio: AudioStateSnapshot,
    pub media: MediaSessionSnapshot,
    pub privacy: PrivacySnapshot,
    pub metrics: SystemMetricsSnapshot,
    pub weather: WeatherSnapshot,
    pub bluetooth_devices: Vec<BluetoothBatteryDevice>,
    pub monitors_brightness: Vec<ExternalMonitorBrightness>,
    pub notifications: Vec<SystemToastNotification>,
    pub dismissed_sys_notif_ids: HashSet<i64>,
    pub unread_notif_count: usize,
    pub tray_apps: Vec<BackgroundTrayApp>,
    pub clipboard_items: Vec<ClipboardVaultEntry>,
    pub drop_shelf_paths: Vec<String>,
    pub agenda_notes: HashMap<String, String>,
    pub scratchpad_text: String,
    pub frecency: HashMap<String, u32>,

    pub qr_share: Option<LanQrShareInfo>,
    pub qr_svg: String,
    pub cpu_history: Vec<f32>,
    pub ram_history: Vec<f32>,
    pub net_history: Vec<f32>,
    pub attention_procs: HashSet<String>,
    pub fullscreen_monitors: HashSet<String>,
    pub overlapped_monitors: HashSet<String>,

    pub wifi_enabled: bool,
    pub bluetooth_enabled: bool,
    pub weather_net_enabled: bool,
    pub metrics_tab_active: bool,
    pub awake_mode: String, // "off" | "30m" | "60m" | "indefinite"
    pub awake_until_ms: Option<u64>,
    pub focus_running: bool,
    pub focus_seconds: u32,
    pub gif_recording: bool,
    pub gif_elapsed_secs: u32,
    pub keyboard_code: String,
    pub keyboard_name: String,
    pub virtual_desktop_index: i32,
    pub virtual_desktop_count: i32,
    pub latest_update: Option<ReleaseManifest>,
    pub update_status: String,
    pub update_downloading: bool,
    pub update_download_pct: u8,
    pub last_external_hwnd: isize,
    pub last_focused_by_group: HashMap<String, isize>,
    pub active_snip_mode: Option<SnipMode>,
    pub snap_overlay_active: bool,
    pub snap_hover_idx: Option<usize>,
    pub active_snap_hwnd: isize,
    pub widget_photos: HashMap<String, String>,
    pub bt_boot_initialized: bool,
    pub focus_completed_mins: u32,
    pub focus_goal_mins: u32,
    pub dock_popover_open_mons: HashSet<String>,
    pub dock_popover_opened_at_ms: u64,
    pub crown_stage_open: bool,
}

impl RuntimeState {
    #[allow(clippy::too_many_arguments)]
    pub fn new(
        user_prefs: UserPreferencesRecord,
        prefs: ShellRuntimePreferences,
        profiles: Vec<WorkspaceProfile>,
        pinned: Vec<PinnedShortcut>,
        widgets: Vec<DesktopWidgetInstance>,
        dock_widgets: Vec<DockWidgetConfig>,
        todos: Vec<TodoItem>,
        countdown: CountdownConfig,
        ai_configs: Vec<AiProviderQuotaConfig>,
        clips: Vec<ClipboardVaultEntry>,
        drop_shelf: Vec<String>,
        agenda: HashMap<String, String>,
        scratchpad: String,
        frecency: HashMap<String, u32>,
    ) -> Self {
        let profiles = if profiles.is_empty() {
            default_workspace_profiles()
        } else {
            profiles
        };
        Self {
            version: AURORA_VERSION.into(),
            theme_id: theme_by_id(&user_prefs.active_theme_id).id,
            active_profile_id: user_prefs.active_profile_id,
            focus_mode_enabled: user_prefs.focus_mode_enabled,
            prefs,
            profiles,
            pinned_shortcuts: pinned,
            widgets,
            dock_widgets,
            todos,
            countdown,
            ai_configs,
            ai_snapshots: Vec::new(),
            market: MarketSnapshot::default(),
            local_ai: LocalAiStatusSnapshot::default(),
            disks: Vec::new(),
            widget_edit_mode: false,
            dock_edit_mode: false,
            open_windows: Vec::new(),
            dock_groups: Vec::new(),
            running_group_order: Vec::new(),
            window_seen_order: Vec::new(),
            icon_png_b64: HashMap::new(),
            icon_requested: HashSet::new(),
            installed_apps: Vec::new(),
            audio: AudioStateSnapshot::default(),
            media: MediaSessionSnapshot::default(),
            privacy: PrivacySnapshot::default(),
            metrics: SystemMetricsSnapshot::default(),
            weather: WeatherSnapshot::default(),
            bluetooth_devices: Vec::new(),
            monitors_brightness: Vec::new(),
            notifications: Vec::new(),
            dismissed_sys_notif_ids: HashSet::new(),
            unread_notif_count: 0,
            tray_apps: Vec::new(),
            clipboard_items: clips,
            drop_shelf_paths: drop_shelf,
            agenda_notes: agenda,
            scratchpad_text: scratchpad,
            frecency,
            qr_share: None,
            qr_svg: String::new(),
            cpu_history: vec![8.0; 16],
            ram_history: vec![40.0; 16],
            net_history: vec![0.0; 16],
            attention_procs: HashSet::new(),
            fullscreen_monitors: HashSet::new(),
            overlapped_monitors: HashSet::new(),
            wifi_enabled: true,
            bluetooth_enabled: true,
            weather_net_enabled: true,
            metrics_tab_active: false,
            awake_mode: "off".into(),
            awake_until_ms: None,
            focus_running: false,
            focus_seconds: 25 * 60,
            gif_recording: false,
            gif_elapsed_secs: 0,
            keyboard_code: "TR".into(),
            keyboard_name: "Türkçe Q".into(),
            virtual_desktop_index: 1,
            virtual_desktop_count: 2,
            latest_update: None,
            update_status: format!("Güncel (v{AURORA_VERSION})"),
            update_downloading: false,
            update_download_pct: 0,
            last_external_hwnd: 0,
            last_focused_by_group: HashMap::new(),
            active_snip_mode: None,
            snap_overlay_active: false,
            snap_hover_idx: None,
            active_snap_hwnd: 0,
            widget_photos: HashMap::new(),
            bt_boot_initialized: false,
            focus_completed_mins: 0,
            focus_goal_mins: 90,
            dock_popover_open_mons: HashSet::new(),
            dock_popover_opened_at_ms: 0,
            crown_stage_open: false,
        }
    }

    pub fn effective_theme(&self) -> (ThemeTokens, ThemeTokens, bool) {
        let base = theme_by_id(&self.theme_id);
        let custom = self.prefs.theme_customization(&base.id);
        let has_custom = custom.is_some_and(|c| c.has_any_override());
        let eff = base.with_customization(custom);
        (eff, base, has_custom)
    }

    pub fn eco_mode_active(&self) -> bool {
        self.prefs.eco_mode_auto_on_low_battery
            && self.metrics.battery_present
            && !self.metrics.is_charging
            && self.metrics.battery_percent <= 20
    }

    pub fn needs_ai_quota_poll(&self) -> bool {
        self.dock_widgets.iter().any(|w| w.enabled && w.kind == "ai-quota")
            || (self.prefs.widgets_enabled && self.widgets.iter().any(|w| w.kind == "ai-quota"))
    }

    pub fn needs_market_poll(&self) -> bool {
        self.dock_widgets.iter().any(|w| w.enabled && w.kind == "market")
            || (self.prefs.widgets_enabled && self.widgets.iter().any(|w| w.kind == "market"))
    }

    pub fn needs_local_ai_or_disks_poll(&self) -> bool {
        self.prefs.widgets_enabled
            && self.widgets.iter().any(|w| w.kind == "local-ai" || w.kind == "storage-net")
    }

    pub fn dock_needs_live_metrics(&self) -> bool {
        self.prefs.effective_island_idle_mode() == "system-pulse"
            || self
                .dock_widgets
                .iter()
                .any(|w| w.enabled && (w.kind == "system" || w.kind == "network"))
            || (self.prefs.widgets_enabled
                && self
                    .widgets
                    .iter()
                    .any(|w| w.kind == "system" || w.kind == "storage-net"))
    }

    /// Resolves a broken, stale, or WindowsApps-sandboxed shortcut path against the live
    /// filesystem reconstructor AND the `installed_apps` COM AppsFolder + Start Menu catalog.
    pub fn resolve_launch_fallback(
        &self,
        raw_path: &str,
        display_name: &str,
        aumid: Option<&str>,
    ) -> Option<String> {
        let trimmed = raw_path.trim();
        let is_sandbox = is_windows_apps_sandbox_path(trimmed);

        // 1. If we already know the exact MSIX/Store AUMID and the target is sandbox-blocked or empty, use COM AUMID!
        if let Some(au) = aumid.map(str::trim).filter(|s| !s.is_empty()) {
            if trimmed.is_empty() || is_sandbox {
                return Some(format!("appsfolder:{au}"));
            }
        }

        // 2. Deterministic filesystem reconstruction for backslash-stripped paths (e.g. `C:UsersLenovo...`)
        if !trimmed.is_empty() && !is_sandbox {
            if let Some(repaired) = repair_corrupted_windows_path(trimmed) {
                if !is_windows_apps_sandbox_path(&repaired) {
                    return Some(repaired);
                }
            }
        }

        // 3. Cross-match against the 300-item installed apps catalog (preferring appsfolder:<AUMID> for Store apps)
        let norm_key = normalize_group_key(trimmed, display_name);
        let disp_low = display_name.trim().to_ascii_lowercase();
        let stripped_raw = trimmed.replace(['\\', '/'], "").to_ascii_lowercase();

        let mut best: Option<(u8, String)> = None;
        for app in &self.installed_apps {
            let lp = app.launch_path.trim();
            if lp.is_empty() || is_windows_apps_sandbox_path(lp) {
                continue;
            }
            let an = app.name.trim().to_ascii_lowercase();
            let lp_low = lp.to_ascii_lowercase();
            let lp_stripped = lp.replace(['\\', '/'], "").to_ascii_lowercase();

            let mut score = 0u8;
            if !stripped_raw.is_empty() && stripped_raw.len() >= 10 && lp_stripped == stripped_raw {
                score = 100;
            } else if !disp_low.is_empty() && an == disp_low {
                score = if lp_low.starts_with("appsfolder:") { 96 } else { 94 };
            } else if !norm_key.is_empty() && norm_key.len() >= 3 {
                let app_key = normalize_group_key(lp, &app.name);
                if app_key == norm_key {
                    score = if lp_low.starts_with("appsfolder:") { 88 } else { 85 };
                } else if an.contains(&norm_key) || (!disp_low.is_empty() && an.contains(&disp_low)) {
                    score = 72;
                }
            }
            if score > 0 {
                let is_better = best.as_ref().is_none_or(|(bs, _)| score > *bs);
                if is_better {
                    best = Some((score, lp.to_string()));
                }
            }
        }
        best.map(|(_, path)| path)
    }

    /// Startup & catalog-load self-healer for `PinnedShortcuts` in `aurora.db`.
    /// Repairs backslash-stripped paths (like `C:UsersLenovo...`) and upgrades sandbox-blocked
    /// `\WindowsApps\...\*.exe` pins to native COM `appsfolder:<AUMID>` targets automatically!
    pub fn heal_pinned_shortcuts(&mut self) -> Vec<(String, String, String)> {
        let mut repaired = Vec::new();
        let snapshot = self.pinned_shortcuts.clone();
        for (idx, pin) in snapshot.iter().enumerate() {
            let cur = pin.executable_path.trim();
            let is_protocol = cur.starts_with("appsfolder:")
                || cur.starts_with("APPSFOLDER:")
                || cur.starts_with("cmdrun:")
                || cur.starts_with("reveal:")
                || cur.starts_with("shell:")
                || cur.starts_with("ms-")
                || cur.starts_with("http://")
                || cur.starts_with("https://");
            if is_protocol {
                continue;
            }
            let is_bare_cmd = !cur.is_empty() && !cur.contains(':') && !cur.contains('\\') && !cur.contains('/');
            if is_bare_cmd {
                continue;
            }
            let exe_part = cur.split('|').next().unwrap_or(cur).trim();
            let needs_heal = exe_part.is_empty()
                || is_windows_apps_sandbox_path(exe_part)
                || !Path::new(exe_part).exists();
            if !needs_heal {
                continue;
            }
            if let Some(fixed) = self.resolve_launch_fallback(exe_part, &pin.display_name, None) {
                if !fixed.eq_ignore_ascii_case(cur) {
                    repaired.push((pin.display_name.clone(), cur.to_string(), fixed.clone()));
                    self.pinned_shortcuts[idx].executable_path = fixed;
                }
            }
        }
        repaired
    }

    pub fn build_catalog_json(&self) -> String {
        let apps: Vec<Value> = self
            .installed_apps
            .iter()
            .map(|a| {
                json!({
                    "id": a.id,
                    "name": a.name,
                    "path": a.launch_path,
                    "category": a.category,
                    "keywords": a.keywords,
                    "kind": a.kind,
                    "icon": a.icon_base64,
                })
            })
            .collect();

        let themes: Vec<Value> = built_in_themes()
            .into_iter()
            .map(|t| {
                let is_light = t.color_mode == ColorMode::Light;
                json!({
                    "id": t.id,
                    "name": t.display_name,
                    "desc": t.description,
                    "visualStyle": t.visual_style.as_str(),
                    "styleTag": t.visual_style.style_tag_tr(),
                    "mode": if is_light { "Açık" } else { "Koyu" },
                    "isLight": is_light,
                    "accent": t.colors.accent_primary,
                    "accent2": t.colors.accent_secondary,
                    "surface1": t.colors.surface1,
                    "surface2": t.colors.surface2,
                })
            })
            .collect();

        json!({
            "type": "catalog",
            "installedApps": apps,
            "themes": themes,
            "frecency": self.frecency,
        })
        .to_string()
    }

    fn build_theme_dto(&self) -> Value {
        let (eff, base, has_custom) = self.effective_theme();
        let ratio = contrast_ratio(&eff.colors.text_primary, &eff.colors.surface1);
        json!({
            "id": eff.id,
            "displayName": eff.display_name,
            "visualStyle": eff.visual_style.as_str(),
            "styleTag": eff.visual_style.style_tag_tr(),
            "colorMode": if eff.color_mode == ColorMode::Light { "Light" } else { "Dark" },
            "surfaceOpacity": eff.surface_opacity,
            "hasCustomColors": has_custom,
            "contrastRatio": ratio,
            "meetsWcagAA": ratio >= 4.5,
            "colors": {
                "canvas": eff.colors.canvas,
                "surface1": eff.colors.surface1,
                "surface2": eff.colors.surface2,
                "surface3": eff.colors.surface3,
                "textPrimary": eff.colors.text_primary,
                "textSecondary": eff.colors.text_secondary,
                "accentPrimary": eff.colors.accent_primary,
                "accentSecondary": eff.colors.accent_secondary,
                "borderSubtle": eff.colors.border_subtle,
                "borderStrong": eff.colors.border_strong,
            },
            "defaultColors": {
                "surface1": base.colors.surface1,
                "surface2": base.colors.surface2,
                "textPrimary": base.colors.text_primary,
                "accentPrimary": base.colors.accent_primary,
                "accentSecondary": base.colors.accent_secondary,
                "surfaceOpacity": base.surface_opacity,
            }
        })
    }

    fn resolve_icon(
        &self,
        key: &str,
        display_name: &str,
        exe_path: &str,
        aumid: Option<&str>,
    ) -> Option<&str> {
        if let Some(b64) = self.icon_png_b64.get(key) {
            return Some(b64.as_str());
        }
        let k_low = key.to_ascii_lowercase();
        let d_low = display_name.to_ascii_lowercase();
        let e_low = exe_path.to_ascii_lowercase();
        let au_low = aumid.map(|s| s.trim().to_ascii_lowercase()).unwrap_or_default();
        let pfn_low = au_low.split('!').next().unwrap_or("").to_string();
        // Fast cross-match against the 300-item COM AppsFolder + StartMenu catalog!
        for app in &self.installed_apps {
            let Some(ref ic) = app.icon_base64 else {
                continue;
            };
            let lp = app.launch_path.to_ascii_lowercase();
            let an = app.name.to_ascii_lowercase();
            if (!au_low.is_empty() && (lp == format!("appsfolder:{au_low}") || lp.contains(&au_low)))
                || (!pfn_low.is_empty() && pfn_low.len() >= 8 && lp.contains(&pfn_low))
                || (!e_low.is_empty() && lp == e_low)
                || (!d_low.is_empty() && (an == d_low || an.contains(&d_low) || d_low.contains(&an)))
                || (!k_low.is_empty()
                    && (((k_low == "wt" || k_low == "windowsterminal") && an.contains("terminal"))
                        || (k_low == "msedge" && an.contains("edge"))
                        || (k_low == "whatsapp" && an.contains("whatsapp"))
                        || (k_low == "ms-teams" && an.contains("teams"))
                        || (k_low == "calculator" && (an.contains("hesap makinesi") || an.contains("calculator")))
                        || (k_low == "store" && an.contains("microsoft store"))
                        || (k_low == "explorer" && (an.contains("dosya gezgini") || an.contains("file explorer")))
                        || (k_low == "powershell" && an.contains("powershell"))
                        || (k_low == "code" && an.contains("visual studio code"))
                        || (k_low.len() >= 4 && (an.contains(&k_low) || lp.contains(&k_low)))))
            {
                return Some(ic.as_str());
            }
        }
        None
    }

    fn map_groups_dto(&self, filter_monitor_id: Option<&str>) -> Vec<Value> {
        let groups = if let Some(mid) = filter_monitor_id {
            build_dock_groups_ordered(
                &self.pinned_shortcuts,
                &self.open_windows,
                Some(mid),
                &self.running_group_order,
                &self.window_seen_order,
            )
        } else {
            self.dock_groups.clone()
        };
        groups
            .iter()
            .filter(|g| g.is_pinned || !g.windows.is_empty())
            .map(|g| {
                let progress = g
                    .windows
                    .iter()
                    .find_map(|w| extract_title_progress_percent(&w.title))
                    .unwrap_or(0);
                let wins: Vec<Value> = g
                    .windows
                    .iter()
                    .map(|w| {
                        json!({
                            "hwnd": w.hwnd,
                            "title": w.title,
                            "processName": w.process_name,
                            "isActive": w.is_active,
                            "isTopMost": w.is_top_most,
                        })
                    })
                    .collect();
                let icon = self.resolve_icon(
                    &g.group_key,
                    &g.display_name,
                    &g.executable_path,
                    g.aumid.as_deref(),
                );
                let matched_audio = if !g.windows.is_empty() {
                    self.audio.app_sessions.iter().find(|s| {
                        g.windows.iter().any(|w| w.process_id == s.process_id)
                            || (!s.process_name.is_empty()
                                && normalize_group_key(&s.process_name, "") == g.group_key)
                    })
                } else {
                    None
                };
                json!({
                    "key": g.group_key,
                    "pinId": g.pin_id,
                    "displayName": g.display_name,
                    "executablePath": g.executable_path,
                    "aumid": g.aumid,
                    "isPinned": g.is_pinned,
                    "hasActive": g.has_active,
                    "hasAttention": g.needs_attention,
                    "progress": progress,
                    "icon": icon,
                    "audioPid": matched_audio.map(|s| s.process_id),
                    "audioVolume": matched_audio.map(|s| s.volume_percent),
                    "audioMuted": matched_audio.is_some_and(|s| s.is_muted),
                    "windows": wins,
                })
            })
            .collect()
    }

    fn clock_and_short_date(&self) -> (String, String) {
        use chrono::Datelike;
        let now = chrono::Local::now();
        let clock_str = if self.prefs.show_clock_seconds {
            now.format("%H:%M:%S").to_string()
        } else {
            now.format("%H:%M").to_string()
        };
        let months = [
            "Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara",
        ];
        let short_date = format!(
            "{:02} {}",
            now.day(),
            months[(now.month().saturating_sub(1) as usize).min(11)]
        );
        (clock_str, short_date)
    }

    fn build_media_dto(&self) -> Option<Value> {
        if self.media.has_session && !self.media.title.is_empty() {
            let art = self.media.thumbnail_bytes.as_ref().map(|b| {
                use base64::Engine;
                base64::engine::general_purpose::STANDARD.encode(b)
            });
            Some(json!({
                "app": self.media.app_name,
                "title": self.media.title,
                "artist": self.media.artist,
                "isPlaying": self.media.is_playing,
                "artwork": art,
            }))
        } else {
            None
        }
    }

    fn build_battery_dto(&self) -> Value {
        json!({
            "present": self.metrics.battery_present,
            "percent": self.metrics.battery_percent,
            "charging": self.metrics.is_charging,
            "status": if !self.metrics.battery_present {
                "AC Güç"
            } else if self.metrics.is_charging {
                "Şarj Oluyor"
            } else {
                "Pil Modu"
            },
        })
    }

    fn build_bt_devices_dto(&self) -> Vec<Value> {
        self.bluetooth_devices
            .iter()
            .take(6)
            .map(|d| {
                json!({
                    "id": d.id,
                    "name": d.name,
                    "percent": d.battery_percent,
                    "hasBattery": d.has_battery,
                    "connected": d.is_connected,
                    "category": d.category,
                    "source": "Bluetooth",
                })
            })
            .collect()
    }

    fn build_monitor_brightness_dto(&self) -> Vec<Value> {
        self.monitors_brightness
            .iter()
            .take(5)
            .enumerate()
            .map(|(i, m)| {
                json!({
                    "id": m.id,
                    "name": m.name,
                    "percent": m.brightness_percent,
                    "isInternal": m.is_internal,
                    "isPrimary": i == 0,
                })
            })
            .collect()
    }

    fn build_audio_outputs_dto(&self) -> Vec<Value> {
        self.audio
            .outputs
            .iter()
            .take(6)
            .map(|o| {
                let short: String = if o.name.chars().count() > 26 {
                    format!("{}…", o.name.chars().take(25).collect::<String>())
                } else {
                    o.name.clone()
                };
                json!({
                    "id": o.id,
                    "name": short,
                    "fullName": o.name,
                    "isDefault": o.is_default,
                })
            })
            .collect()
    }

    fn build_audio_sessions_dto(&self) -> Vec<Value> {
        self.audio
            .app_sessions
            .iter()
            .take(5)
            .map(|s| {
                json!({
                    "pid": s.process_id,
                    "name": s.display_name,
                    "volume": s.volume_percent,
                    "muted": s.is_muted,
                    "routedDeviceId": s.routed_device_id.as_deref().unwrap_or(""),
                })
            })
            .collect()
    }

    fn build_desktop_clipboard_dto(&self) -> Vec<Value> {
        self.clipboard_items
            .iter()
            .take(5)
            .map(|c| {
                let short_text: String = if c.kind == "text" {
                    c.text_content.chars().take(200).collect()
                } else {
                    String::new()
                };
                json!({
                    "id": c.id,
                    "kind": c.kind,
                    "preview": c.preview_text,
                    "text": short_text,
                })
            })
            .collect()
    }

    fn build_notifications_dto(&self, limit: usize) -> Vec<Value> {
        self.notifications
            .iter()
            .filter(|n| !self.dismissed_sys_notif_ids.contains(&n.notification_id))
            .take(limit)
            .map(|n| {
                json!({
                    "id": format!("sys-{}", n.notification_id),
                    "app": n.app_name,
                    "title": n.title,
                    "body": n.body,
                    "time": n.arrived_at_utc,
                    "isSystem": true,
                })
            })
            .collect()
    }

    fn build_trays_dto(&self, limit: usize) -> Vec<Value> {
        self.tray_apps
            .iter()
            .take(limit)
            .map(|a| {
                let stem = Path::new(&a.executable_path)
                    .file_stem()
                    .and_then(|s| s.to_str())
                    .map(|s| s.to_ascii_lowercase())
                    .unwrap_or_default();
                let icon_key = if stem.is_empty() { a.id.as_str() } else { stem.as_str() };
                let aumid = if is_windows_apps_sandbox_path(&a.executable_path) {
                    self.installed_apps
                        .iter()
                        .find(|inst| {
                            let lt = inst.launch_path.to_ascii_lowercase();
                            lt.starts_with("appsfolder:")
                                && (inst.name.eq_ignore_ascii_case(&a.name)
                                    || (!stem.is_empty() && lt.contains(&stem)))
                        })
                        .and_then(|inst| {
                            inst.launch_path
                                .strip_prefix("appsfolder:")
                                .or_else(|| inst.launch_path.strip_prefix("APPSFOLDER:"))
                        })
                } else {
                    None
                };
                let icon = self.resolve_icon(icon_key, &a.name, &a.executable_path, aumid);
                json!({
                    "key": a.id,
                    "name": a.name,
                    "subtitle": if a.subtitle.is_empty() { "Arka Plan" } else { a.subtitle.as_str() },
                    "memoryMb": a.memory_mb,
                    "canQuit": true,
                    "icon": icon,
                })
            })
            .collect()
    }

    fn build_pinned_shortcuts_dto(&self, limit: usize) -> Vec<Value> {
        self.pinned_shortcuts
            .iter()
            .take(limit)
            .map(|p| {
                let stem = Path::new(&p.executable_path)
                    .file_stem()
                    .and_then(|s| s.to_str())
                    .unwrap_or(&p.display_name)
                    .to_ascii_lowercase();
                let aumid = p
                    .executable_path
                    .strip_prefix("appsfolder:")
                    .or_else(|| p.executable_path.strip_prefix("APPSFOLDER:"));
                let icon = self.resolve_icon(&stem, &p.display_name, &p.executable_path, aumid);
                json!({
                    "id": p.id,
                    "name": p.display_name,
                    "path": p.executable_path,
                    "icon": icon,
                })
            })
            .collect()
    }

    pub fn build_dock_state_json(&self, mon_id: &str, is_primary: bool, now_ms: u64) -> String {
        let mode = self.prefs.effective_dock_mode();
        let filter = self.prefs.per_monitor_window_filter.then_some(mon_id);
        let (clock_str, short_date) = self.clock_and_short_date();
        let folders: Vec<Value> = effective_dock_folders(self.prefs.dock_folder_paths.as_deref())
            .into_iter()
            .map(|p| {
                json!({
                    "path": p,
                    "name": friendly_folder_name(&p),
                })
            })
            .collect();
        let awake_active = self.awake_mode != "off";
        let awake_short = match self.awake_mode.as_str() {
            "indefinite" => "Süresiz".to_string(),
            "30m" | "60m" => {
                let rem = self.awake_until_ms.map(|u| u.saturating_sub(now_ms) / 60_000 + 1).unwrap_or(30);
                format!("{rem} dk")
            }
            _ => "Kapalı".to_string(),
        };
        let payload = json!({
            "monitorId": mon_id,
            "isPrimary": is_primary,
            "clock": clock_str,
            "shortDate": short_date,
            "showClockSeconds": self.prefs.show_clock_seconds,
            "dockAutoHide": mode == "AutoHide",
            "dockMode": mode,
            "dockBarLayout": self.prefs.effective_dock_bar_layout(),
            "dockDensity": self.prefs.effective_dock_density(),
            "dockEdge": self.prefs.effective_dock_edge(),
            "dockScalePercent": self.prefs.dock_scale_percent,
            "dockOffsetPercent": self.prefs.dock_offset_percent,
            "reserveDockWorkArea": self.prefs.effective_reserve_pref(),
            "dockDodged": mode == "SmartDodge" && self.overlapped_monitors.contains(mon_id),
            "smartTidy": self.prefs.smart_tidy_enabled,
            "dockMagnification": self.prefs.dock_magnification_percent,
            "dwmLivePreview": self.prefs.dwm_live_preview_enabled,
            "showDockStatusCluster": self.prefs.effective_show_dock_status(),
            "dockHubStyle": self.prefs.effective_dock_hub_style(),
            "dockHoverStyle": self.prefs.effective_dock_hover_style(),
            "cornerRadiusPreset": self.prefs.effective_corner_radius_preset(),
            "dockStartStyle": self.prefs.effective_dock_start_style(),
            "dockSilhouette": self.prefs.effective_dock_silhouette(),
            "dockIndicatorStyle": self.prefs.effective_dock_indicator_style(),
            "shellArchitecture": self.prefs.effective_shell_architecture(),
            "islandStyle": self.prefs.effective_island_style(),
            "islandIdleMode": self.prefs.effective_island_idle_mode(),
            "islandBtPopin": self.prefs.island_bt_popin,
            "islandPrivacyGuard": self.prefs.island_privacy_guard,
            "notificationMode": self.prefs.effective_notification_mode(),
            "focusStats": {
                "completedMins": self.focus_completed_mins,
                "goalMins": self.focus_goal_mins,
            },
            "ecoModeAuto": self.prefs.eco_mode_auto_on_low_battery,
            "ecoModeActive": self.eco_mode_active(),
            "dockWidgets": self.dock_widgets,
            "dockEditMode": self.dock_edit_mode,
            "dockEmptyWheelAction": self.prefs.effective_empty_wheel_action(),
            "dockEmptyDblClickAction": self.prefs.effective_empty_dblclick_action(),
            "dockEmptyMidClickAction": self.prefs.effective_empty_midclick_action(),
            "dockScrubGestures": self.prefs.dock_scrub_gestures_enabled,
            "dockPerAppAudio": self.prefs.dock_per_app_audio_enabled,
            "widgetsEnabled": self.prefs.widgets_enabled,
            "widgetEditMode": self.widget_edit_mode,
            "crownVisibilityMode": self.prefs.effective_crown_visibility_mode(),
            "privacy": {
                "camera": self.privacy.camera_active,
                "mic": self.privacy.mic_active,
                "cameraApps": self.privacy.camera_apps,
                "micApps": self.privacy.mic_apps,
            },
            "virtualDesktop": {
                "index": self.virtual_desktop_index,
                "count": self.virtual_desktop_count,
                "name": format!("Masaüstü {}", self.virtual_desktop_index),
            },
            "aiSnapshots": self.ai_snapshots,
            "aiConfigs": self.ai_configs,
            "weather": {
                "live": self.weather.valid && self.weather_net_enabled,
                "city": self.weather.city,
                "temp": self.weather.temp_c.round(),
                "summary": self.weather.condition_tr,
            },
            "metrics": {
                "cpu": self.metrics.cpu_percent.round(),
                "ramPct": self.metrics.ram_percent.round(),
                "rxKbps": (self.metrics.net_down_mbps * 125.0).round(),
                "txKbps": (self.metrics.net_up_mbps * 125.0).round(),
                "cpuHist": self.cpu_history.iter().rev().take(12).rev().copied().collect::<Vec<_>>(),
                "ramHist": self.ram_history.iter().rev().take(12).rev().copied().collect::<Vec<_>>(),
            },
            "focus": {
                "running": self.focus_running,
                "seconds": self.focus_seconds,
                "label": format!("{:02}:{:02}", self.focus_seconds / 60, self.focus_seconds % 60),
            },
            "market": self.market,
            "latestClip": self.clipboard_items.first().map(|c| json!({
                "id": c.id,
                "kind": c.kind,
                "preview": c.preview_text,
            })),
            "trayCount": self.tray_apps.len(),
            "trayApps": self.build_trays_dto(32),
            "dockFolders": folders,
            "audio": {
                "volume": self.audio.master_volume_percent,
                "muted": self.audio.is_master_muted,
                "device": self.audio.default_output_name,
            },
            "audioOutputs": self.build_audio_outputs_dto(),
            "mic": {
                "supported": self.audio.mic_supported,
                "muted": self.audio.is_mic_muted,
            },
            "wifi": {
                "on": self.wifi_enabled,
                "status": if self.wifi_enabled { "Bağlı" } else { "Kapalı" },
            },
            "bluetooth": {
                "on": self.bluetooth_enabled,
                "status": if self.bluetooth_enabled { "Aktif" } else { "Kapalı" },
            },
            "quietHours": self.focus_mode_enabled,
            "awake": {
                "active": awake_active,
                "mode": self.awake_mode,
                "shortLabel": awake_short,
            },
            "battery": self.build_battery_dto(),
            "btDevices": self.build_bt_devices_dto(),
            "monitorBrightness": self.build_monitor_brightness_dto(),
            "media": self.build_media_dto(),
            "unreadNotifCount": self.unread_notif_count,
            "notifications": self.build_notifications_dto(8),
            "agendaNotes": self.agenda_notes,
            "theme": self.build_theme_dto(),
            "groups": self.map_groups_dto(filter),
        });
        json!({ "type": "state", "payload": payload }).to_string()
    }

    pub fn build_widgets_state_json(&self) -> String {
        let (clock_str, short_date) = self.clock_and_short_date();
        let payload = json!({
            "theme": self.build_theme_dto(),
            "shellArchitecture": self.prefs.effective_shell_architecture(),
            "cornerRadiusPreset": self.prefs.effective_corner_radius_preset(),
            "clock": clock_str,
            "shortDate": short_date,
            "showClockSeconds": self.prefs.show_clock_seconds,
            "media": self.build_media_dto(),
            "battery": self.build_battery_dto(),
            "btDevices": self.build_bt_devices_dto(),
            "audio": {
                "volume": self.audio.master_volume_percent,
                "muted": self.audio.is_master_muted,
                "device": self.audio.default_output_name,
            },
            "audioOutputs": self.build_audio_outputs_dto(),
            "audioSessions": self.build_audio_sessions_dto(),
            "clipboard": self.build_desktop_clipboard_dto(),
            "focusStats": {
                "completedMins": self.focus_completed_mins,
                "goalMins": self.focus_goal_mins,
            },
            "widgetPhotos": self.widget_photos,
            "metrics": {
                "cpu": self.metrics.cpu_percent.round(),
                "ramPct": self.metrics.ram_percent.round(),
                "ramUsed": (self.metrics.ram_used_gb * 10.0).round() / 10.0,
                "ramTotal": self.metrics.ram_total_gb.round(),
                "rxKbps": (self.metrics.net_down_mbps * 125.0).round(),
                "txKbps": (self.metrics.net_up_mbps * 125.0).round(),
                "cpuHist": self.cpu_history,
                "ramHist": self.ram_history,
                "netHist": self.net_history,
            },
            "weather": {
                "live": self.weather.valid && self.weather_net_enabled,
                "city": self.weather.city,
                "temp": self.weather.temp_c.round(),
                "summary": self.weather.condition_tr,
            },
            "agendaNotes": self.agenda_notes,
            "scratchpad": self.scratchpad_text,
            "focus": {
                "running": self.focus_running,
                "seconds": self.focus_seconds,
                "label": format!("{:02}:{:02}", self.focus_seconds / 60, self.focus_seconds % 60),
            },
            "pinnedShortcuts": self.build_pinned_shortcuts_dto(12),
            "widgets": self.widgets,
            "widgetsLocked": self.prefs.widgets_locked,
            "widgetEditMode": self.widget_edit_mode,
            "ecoModeActive": self.eco_mode_active(),
            "aiConfigs": self.ai_configs,
            "aiSnapshots": self.ai_snapshots,
            "todos": self.todos,
            "countdown": self.countdown,
            "market": self.market,
            "localAi": self.local_ai,
            "disks": self.disks,
        });
        json!({ "type": "state", "payload": payload }).to_string()
    }

    pub fn build_crown_state_json(
        &self,
        mon_id: &str,
        mon_count: usize,
        native_tb_hidden: bool,
        now_ms: u64,
    ) -> String {
        let now = chrono::Local::now();
        let clock_str = if self.prefs.show_clock_seconds {
            now.format("%H:%M:%S").to_string()
        } else {
            now.format("%H:%M").to_string()
        };
        let months = [
            "Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara",
        ];
        use chrono::Datelike;
        let short_date = format!(
            "{:02} {}",
            now.day(),
            months[(now.month().saturating_sub(1) as usize).min(11)]
        );

        let profiles: Vec<Value> = self
            .profiles
            .iter()
            .map(|p| {
                json!({
                    "id": p.id,
                    "name": p.name,
                    "active": p.id == self.active_profile_id,
                })
            })
            .collect();
        let active_prof_name = self
            .profiles
            .iter()
            .find(|p| p.id == self.active_profile_id)
            .map(|p| p.name.clone())
            .unwrap_or_else(|| "Kişisel".into());

        let open_wins: Vec<Value> = self
            .open_windows
            .iter()
            .take(18)
            .map(|w| {
                let key = w.process_name.to_ascii_lowercase();
                json!({
                    "hwnd": w.hwnd,
                    "title": w.title,
                    "processName": w.process_name,
                    "isTopMost": w.is_top_most,
                    "isActive": w.is_active,
                    "monitorId": w.monitor_device_id,
                    "icon": self.icon_png_b64.get(&key),
                })
            })
            .collect();

        let active_win = self
            .open_windows
            .iter()
            .find(|w| w.is_active)
            .or_else(|| self.open_windows.iter().find(|w| w.hwnd == self.last_external_hwnd))
            .or_else(|| self.open_windows.first())
            .map(|w| {
                let key = w.process_name.to_ascii_lowercase();
                json!({
                    "hwnd": w.hwnd,
                    "title": w.title,
                    "processName": w.process_name,
                    "isTopMost": w.is_top_most,
                    "icon": self.icon_png_b64.get(&key),
                })
            });

        let vd_list: Vec<Value> = (1..=self.virtual_desktop_count.max(1))
            .map(|i| {
                json!({
                    "index": i,
                    "name": format!("Masaüstü {i}"),
                    "current": i == self.virtual_desktop_index,
                })
            })
            .collect();

        let notifs: Vec<Value> = self
            .notifications
            .iter()
            .filter(|n| !self.dismissed_sys_notif_ids.contains(&n.notification_id))
            .take(10)
            .map(|n| {
                json!({
                    "id": format!("sys-{}", n.notification_id),
                    "app": n.app_name,
                    "title": n.title,
                    "body": n.body,
                    "time": n.arrived_at_utc,
                    "isSystem": true,
                })
            })
            .collect();

        let shelf: Vec<Value> = self
            .drop_shelf_paths
            .iter()
            .filter(|p| Path::new(p).exists())
            .map(|p| {
                let path = Path::new(p);
                let name = path
                    .file_name()
                    .and_then(|s| s.to_str())
                    .unwrap_or(p)
                    .to_string();
                let size_label = std::fs::metadata(path)
                    .map(|m| if m.is_dir() { "Klasör".into() } else { format_size(m.len()) })
                    .unwrap_or_else(|_| "Dosya".into());
                json!({
                    "name": name,
                    "path": p,
                    "sizeLabel": size_label,
                    "icon": self.icon_png_b64.get(p),
                })
            })
            .collect();

        let trays: Vec<Value> = self
            .tray_apps
            .iter()
            .map(|a| {
                let icon = self.resolve_icon(&a.id, &a.name, &a.executable_path, None);
                json!({
                    "key": a.id,
                    "name": a.name,
                    "subtitle": if a.subtitle.is_empty() { "Arka Plan" } else { a.subtitle.as_str() },
                    "memoryMb": a.memory_mb,
                    "canQuit": true,
                    "icon": icon,
                })
            })
            .collect();

        let clips: Vec<Value> = self
            .clipboard_items
            .iter()
            .take(40)
            .map(|c| {
                let short_text: String = if c.kind == "text" {
                    c.text_content.chars().take(400).collect()
                } else {
                    String::new()
                };
                json!({
                    "id": c.id,
                    "kind": c.kind,
                    "preview": c.preview_text,
                    "text": short_text,
                    "thumb": c.thumb_base64,
                    "width": c.image_width,
                    "height": c.image_height,
                    "sourceApp": c.source_app,
                    "pinned": c.is_pinned,
                    "time": c.last_copied_utc,
                })
            })
            .collect();

        let bt_devs: Vec<Value> = self
            .bluetooth_devices
            .iter()
            .take(6)
            .map(|d| {
                json!({
                    "id": d.id,
                    "name": d.name,
                    "percent": d.battery_percent,
                    "hasBattery": d.has_battery,
                    "connected": d.is_connected,
                    "category": d.category,
                    "source": "Bluetooth",
                })
            })
            .collect();

        let mon_bri: Vec<Value> = self
            .monitors_brightness
            .iter()
            .take(5)
            .enumerate()
            .map(|(i, m)| {
                json!({
                    "id": m.id,
                    "name": m.name,
                    "percent": m.brightness_percent,
                    "isInternal": m.is_internal,
                    "isPrimary": i == 0,
                })
            })
            .collect();

        let audio_outputs: Vec<Value> = self
            .audio
            .outputs
            .iter()
            .take(6)
            .map(|o| {
                let short: String = if o.name.chars().count() > 26 {
                    format!("{}…", o.name.chars().take(25).collect::<String>())
                } else {
                    o.name.clone()
                };
                json!({
                    "id": o.id,
                    "name": short,
                    "fullName": o.name,
                    "isDefault": o.is_default,
                })
            })
            .collect();

        let audio_sessions: Vec<Value> = self
            .audio
            .app_sessions
            .iter()
            .take(5)
            .map(|s| {
                json!({
                    "pid": s.process_id,
                    "name": s.display_name,
                    "volume": s.volume_percent,
                    "routedDeviceId": s.routed_device_id.as_deref().unwrap_or(""),
                })
            })
            .collect();

        let top_procs: Vec<Value> = self
            .metrics
            .top_processes
            .iter()
            .map(|tp| {
                json!({
                    "pid": tp.pid,
                    "name": tp.name,
                    "displayName": tp.name,
                    "memoryMb": tp.memory_mb,
                    "instances": 1,
                })
            })
            .collect();

        let qr_dto = self.qr_share.as_ref().map(|q| {
            json!({
                "url": q.url,
                "name": q.file_name,
                "size": q.size_label,
                "svg": self.qr_svg,
                "downloads": 0,
            })
        });

        let media_dto = if self.media.has_session && !self.media.title.is_empty() {
            let art = self.media.thumbnail_bytes.as_ref().map(|b| {
                use base64::Engine;
                base64::engine::general_purpose::STANDARD.encode(b)
            });
            Some(json!({
                "app": self.media.app_name,
                "title": self.media.title,
                "artist": self.media.artist,
                "isPlaying": self.media.is_playing,
                "artwork": art,
            }))
        } else {
            None
        };

        let awake_active = self.awake_mode != "off";
        let (awake_short, awake_full) = match self.awake_mode.as_str() {
            "indefinite" => ("Süresiz".to_string(), "Süresiz Uyanık".to_string()),
            "30m" | "60m" => {
                let rem_min = self
                    .awake_until_ms
                    .map(|u| u.saturating_sub(now_ms) / 60_000 + 1)
                    .unwrap_or(30);
                (format!("{rem_min} dk"), format!("{rem_min} dakika uyanık"))
            }
            _ => ("Kapalı".to_string(), "Uyku Normal".to_string()),
        };

        let internal_bri = self
            .monitors_brightness
            .iter()
            .find(|m| m.is_internal)
            .or_else(|| self.monitors_brightness.first());

        let payload = json!({
            "monitorId": mon_id,
            "monitorCount": mon_count,
            "clock": clock_str,
            "shortDate": short_date,
            "keyboard": { "code": self.keyboard_code, "name": self.keyboard_name },
            "virtualDesktop": {
                "index": self.virtual_desktop_index,
                "count": self.virtual_desktop_count,
                "name": format!("Masaüstü {}", self.virtual_desktop_index),
                "list": vd_list,
            },
            "unreadNotifCount": self.unread_notif_count,
            "dockMode": self.prefs.effective_dock_mode(),
            "dockBarLayout": self.prefs.effective_dock_bar_layout(),
            "dockDensity": self.prefs.effective_dock_density(),
            "dockEdge": self.prefs.effective_dock_edge(),
            "dockScalePercent": self.prefs.dock_scale_percent,
            "dockOffsetPercent": self.prefs.dock_offset_percent,
            "reserveDockWorkArea": self.prefs.effective_reserve_pref(),
            "crownVisibilityMode": self.prefs.effective_crown_visibility_mode(),
            "showDockStatusCluster": self.prefs.effective_show_dock_status(),
            "dockHubStyle": self.prefs.effective_dock_hub_style(),
            "dockHoverStyle": self.prefs.effective_dock_hover_style(),
            "cornerRadiusPreset": self.prefs.effective_corner_radius_preset(),
            "dockStartStyle": self.prefs.effective_dock_start_style(),
            "dockSilhouette": self.prefs.effective_dock_silhouette(),
            "dockIndicatorStyle": self.prefs.effective_dock_indicator_style(),
            "shellArchitecture": self.prefs.effective_shell_architecture(),
            "islandStyle": self.prefs.effective_island_style(),
            "islandIdleMode": self.prefs.effective_island_idle_mode(),
            "islandBtPopin": self.prefs.island_bt_popin,
            "islandPrivacyGuard": self.prefs.island_privacy_guard,
            "focusStats": {
                "completedMins": self.focus_completed_mins,
                "goalMins": self.focus_goal_mins,
            },
            "ecoModeAuto": self.prefs.eco_mode_auto_on_low_battery,
            "ecoModeActive": self.eco_mode_active(),
            "dockWidgets": self.dock_widgets,
            "dockEditMode": self.dock_edit_mode,
            "dockEmptyWheelAction": self.prefs.effective_empty_wheel_action(),
            "dockEmptyDblClickAction": self.prefs.effective_empty_dblclick_action(),
            "dockEmptyMidClickAction": self.prefs.effective_empty_midclick_action(),
            "dockScrubGestures": self.prefs.dock_scrub_gestures_enabled,
            "dockPerAppAudio": self.prefs.dock_per_app_audio_enabled,
            "autoDayNightTheme": self.prefs.auto_day_night_theme,
            "dayThemeId": self.prefs.day_theme_id,
            "nightThemeId": self.prefs.night_theme_id,
            "widgetsEnabled": self.prefs.widgets_enabled,
            "widgetsLocked": self.prefs.widgets_locked,
            "widgetEditMode": self.widget_edit_mode,
            "widgets": self.widgets,
            "todos": self.todos,
            "countdown": self.countdown,
            "market": self.market,
            "localAi": self.local_ai,
            "disks": self.disks,
            "aiConfigs": self.ai_configs,
            "aiSnapshots": self.ai_snapshots,
            "pinnedShortcuts": self.build_pinned_shortcuts_dto(18),
            "hardwareOsdEnabled": self.prefs.hardware_osd_enabled,
            "altDragEnabled": self.prefs.alt_drag_enabled,
            "showClockSeconds": self.prefs.show_clock_seconds,
            "snapZonesEnabled": self.prefs.snap_zones_enabled,
            "snapZonePreset": self.prefs.effective_snap_preset(),
            "version": self.version,
            "updateStatus": self.update_status,
            "updateAvailable": self.latest_update.is_some(),
            "updateLatestVersion": self.latest_update.as_ref().map(|u| u.version.as_str()),
            "updateReleaseNotes": self.latest_update.as_ref().map(|u| u.release_notes.as_str()).unwrap_or(""),
            "updateDownloadUrl": self.latest_update.as_ref().map(|u| u.download_url.as_str()),
            "updateDownloading": self.update_downloading,
            "updateDownloadPercent": self.update_download_pct,
            "autoCheckUpdatesEnabled": self.prefs.auto_check_updates_enabled,
            "repoUrl": "https://github.com/MuharremDidici/ProjectAurora",
            "awake": {
                "active": awake_active,
                "mode": self.awake_mode,
                "shortLabel": awake_short,
                "fullLabel": awake_full,
            },
            "showOnboarding": !self.prefs.onboarding_completed,
            "profileName": active_prof_name,
            "nativeTaskbarHidden": native_tb_hidden,
            "dockAutoHide": self.prefs.dock_auto_hide,
            "perMonitorFilter": self.prefs.per_monitor_window_filter,
            "smartTidy": self.prefs.smart_tidy_enabled,
            "dockMagnification": self.prefs.dock_magnification_percent,
            "crownPinned": self.prefs.crown_pinned,
            "dwmLivePreview": self.prefs.dwm_live_preview_enabled,
            "sysNotifBridge": self.prefs.system_notification_bridge_enabled,
            "notificationMode": self.prefs.effective_notification_mode(),
            "winKeyOmni": self.prefs.win_key_opens_omni_search,
            "omniHotkeyPreset": self.prefs.omni_hotkey_preset,
            "quietHours": self.focus_mode_enabled,
            "startupEnabled": is_startup_enabled(),
            "scratchpad": self.scratchpad_text,
            "clipboard": clips,
            "clipboardCount": self.clipboard_items.len(),
            "clipboardAutoPaste": self.prefs.clipboard_auto_paste_enabled,
            "btDevices": bt_devs,
            "monitorBrightness": mon_bri,
            "privacy": {
                "camera": self.privacy.camera_active,
                "mic": self.privacy.mic_active,
                "cameraApps": self.privacy.camera_apps,
                "micApps": self.privacy.mic_apps,
            },
            "qrShare": qr_dto,
            "gifRec": {
                "recording": self.gif_recording,
                "elapsed": self.gif_elapsed_secs,
            },
            "topProcesses": top_procs,
            "agendaNotes": self.agenda_notes,
            "dropShelf": shelf,
            "trayApps": trays,
            "focus": {
                "running": self.focus_running,
                "label": format!("{:02}:{:02}", self.focus_seconds / 60, self.focus_seconds % 60),
            },
            "theme": self.build_theme_dto(),
            "profiles": profiles,
            "activeWindow": active_win,
            "openWindows": open_wins,
            "audio": {
                "volume": self.audio.master_volume_percent,
                "muted": self.audio.is_master_muted,
                "device": self.audio.default_output_name,
            },
            "audioOutputs": audio_outputs,
            "mic": {
                "supported": self.audio.mic_supported,
                "muted": self.audio.is_mic_muted,
                "name": "Mikrofon",
            },
            "perAppRoutingSupported": self.audio.per_app_routing_supported,
            "audioSessions": audio_sessions,
            "brightness": {
                "supported": internal_bri.is_some(),
                "value": internal_bri.map(|m| m.brightness_percent).unwrap_or(80),
            },
            "battery": {
                "present": self.metrics.battery_present,
                "percent": self.metrics.battery_percent,
                "charging": self.metrics.is_charging,
                "status": if !self.metrics.battery_present {
                    "AC Güç"
                } else if self.metrics.is_charging {
                    "Şarj Oluyor"
                } else {
                    "Pil Modu"
                },
            },
            "wifi": {
                "on": self.wifi_enabled,
                "status": if self.wifi_enabled { "Bağlı" } else { "Kapalı" },
            },
            "bluetooth": {
                "on": self.bluetooth_enabled,
                "status": if self.bluetooth_enabled { "Aktif" } else { "Kapalı" },
            },
            "media": media_dto,
            "metrics": {
                "cpu": self.metrics.cpu_percent.round(),
                "ramPct": self.metrics.ram_percent.round(),
                "ramUsed": (self.metrics.ram_used_gb * 10.0).round() / 10.0,
                "ramTotal": self.metrics.ram_total_gb.round(),
                "gpuName": "Direct3D 11 Hardware",
                "rxKbps": (self.metrics.net_down_mbps * 125.0).round(),
                "txKbps": (self.metrics.net_up_mbps * 125.0).round(),
                "cpuHist": self.cpu_history,
                "ramHist": self.ram_history,
                "netHist": self.net_history,
            },
            "weather": {
                "live": self.weather.valid && self.weather_net_enabled,
                "city": self.weather.city,
                "temp": self.weather.temp_c.round(),
                "summary": self.weather.condition_tr,
            },
            "notifications": notifs,
        });

        json!({ "type": "state", "payload": payload }).to_string()
    }
}

pub struct BridgeEnv<'a> {
    pub state: &'a mut RuntimeState,
    pub crown: &'a mut WebViewOverlaySurface,
    pub docks: &'a mut [WebViewOverlaySurface],
    pub widgets_surface: &'a mut Option<WebViewOverlaySurface>,
    pub prefs_store: &'a ShellPreferencesStore,
    pub widgets_store: &'a WidgetsLayoutStore,
    pub aurora_db: &'a AuroraDb,
    pub clip_db: &'a ClipboardVaultDb,
    pub aux_store: &'a AuxiliaryStateStore,
    pub logger: &'a PrivacySafeLogger,
    pub appbar: &'a mut NativeAppBarManager,
    pub audio: &'a CoreAudioController,
    pub brightness: &'a BrightnessController,
    pub qr_server: &'a mut LanQrShareServer,
    pub dwm_preview: &'a mut Option<DwmLivePreviewWindow>,
    pub bg_tx: &'a Sender<BgCommand>,
    pub gif_stop_flag: &'a mut Option<Arc<AtomicBool>>,
    pub should_exit: &'a mut bool,
    pub needs_taskbar_sync: &'a mut bool,
    pub needs_bounds_sync: &'a mut bool,
    pub needs_hotkey_sync: &'a mut bool,
    pub needs_widgets_sync: &'a mut bool,
    pub needs_state_push: &'a mut bool,
    pub popup_opened_at_ms: &'a mut u64,
    pub peek_tray_until_ms: &'a mut u64,
    pub now_ms: u64,
}

impl<'a> BridgeEnv<'a> {
    pub fn post_toast(&self, title: &str, body: &str, notif_id: Option<&str>) {
        let j = json!({
            "type": "toast",
            "title": title,
            "body": body,
            "notificationId": notif_id.unwrap_or(""),
        })
        .to_string();
        self.crown.post_json(&j);
    }

    pub fn post_osd(&self, kind: &str, label: &str, value_text: &str, percent: Option<u8>, active: bool, duration_ms: u32) {
        if !self.state.prefs.hardware_osd_enabled {
            return;
        }
        let j = json!({
            "type": "osdHud",
            "kind": kind,
            "label": label,
            "valueText": value_text,
            "percent": percent,
            "active": active,
            "durationMs": duration_ms,
        })
        .to_string();
        self.crown.post_json(&j);
    }

    fn save_prefs(&self) {
        self.prefs_store.save(&self.state.prefs);
    }

    fn save_widgets(&self) {
        self.widgets_store.save(&WidgetsPersistenceDoc {
            widgets: self.state.widgets.clone(),
            ai_configs: self.state.ai_configs.clone(),
            dock_widgets: self.state.dock_widgets.clone(),
            todos: self.state.todos.clone(),
            countdown: self.state.countdown.clone(),
        });
    }

    fn save_user_prefs(&self) {
        self.aurora_db.save_user_preferences(&UserPreferencesRecord {
            active_theme_id: self.state.theme_id.clone(),
            active_profile_id: self.state.active_profile_id.clone(),
            focus_mode_enabled: self.state.focus_mode_enabled,
            reduced_motion: false,
        });
    }
}

fn str_prop<'a>(v: &'a Value, key: &str) -> &'a str {
    v.get(key).and_then(|x| x.as_str()).unwrap_or("")
}

fn i64_prop(v: &Value, key: &str, def: i64) -> i64 {
    v.get(key)
        .and_then(|x| x.as_i64().or_else(|| x.as_f64().map(|f| f.round() as i64)))
        .unwrap_or(def)
}

fn f64_prop(v: &Value, key: &str, def: f64) -> f64 {
    v.get(key).and_then(|x| x.as_f64()).unwrap_or(def)
}

fn bool_prop(v: &Value, key: &str) -> bool {
    v.get(key).and_then(|x| x.as_bool()).unwrap_or(false)
}

/// Bulletproof Dock app launcher (v5.0):
/// - Prefers native COM `appsfolder:<AUMID>` for Store/MSIX/WindowsApps apps.
/// - Passes saved `PinnedShortcut.arguments`.
/// - Falls back to filesystem backslash-reconstruction + `installed_apps` catalog lookup
///   and automatically heals corrupted DB pins in-place!
/// - Logs & shows an actionable toast if a target genuinely no longer exists.
fn launch_dock_group_reliably(env: &mut BridgeEnv<'_>, g: &DockAppGroup) {
    let saved_args = g
        .pin_id
        .as_deref()
        .and_then(|pid| env.state.pinned_shortcuts.iter().find(|p| p.id == pid))
        .or_else(|| {
            env.state
                .pinned_shortcuts
                .iter()
                .find(|p| normalize_group_key(&p.executable_path, &p.display_name) == g.group_key)
        })
        .map(|p| p.arguments.clone())
        .unwrap_or_default();

    let primary_target = if (g.executable_path.trim().is_empty()
        || is_windows_apps_sandbox_path(&g.executable_path))
        && g.aumid.as_deref().is_some_and(|a| !a.trim().is_empty())
    {
        format!("appsfolder:{}", g.aumid.as_deref().unwrap_or("").trim())
    } else {
        g.executable_path.trim().to_string()
    };

    if !primary_target.is_empty() && launch_target_with_args(&primary_target, &saved_args).is_ok() {
        return;
    }

    // Automatic fallback + live pin self-healing
    if let Some(resolved) = env.state.resolve_launch_fallback(
        &g.executable_path,
        &g.display_name,
        g.aumid.as_deref(),
    ) {
        if launch_target_with_args(&resolved, &saved_args).is_ok() {
            let mut db_dirty = false;
            for p in &mut env.state.pinned_shortcuts {
                let matches = g.pin_id.as_deref() == Some(p.id.as_str())
                    || normalize_group_key(&p.executable_path, &p.display_name) == g.group_key;
                if matches && !p.executable_path.eq_ignore_ascii_case(&resolved) {
                    p.executable_path = resolved.clone();
                    db_dirty = true;
                }
            }
            if db_dirty {
                env.aurora_db.save_pinned_shortcuts(&env.state.pinned_shortcuts);
                env.logger.info(
                    "LAUNCH",
                    "PIN_HEALED_ON_CLICK",
                    &format!("Repaired pin '{}' -> {}", g.display_name, resolved),
                );
            }
            return;
        }
    }

    env.logger.warn(
        "LAUNCH",
        "LAUNCH_FAILED",
        &format!(
            "Failed to launch '{}' (target='{}', aumid={:?})",
            g.display_name, g.executable_path, g.aumid
        ),
    );
    env.post_toast(
        "Uygulama Açılamadı",
        &format!(
            "{} başlatılamadı. Kısayol taşınmış olabilir; sağ tıklayıp yeniden sabitleyebilirsiniz.",
            g.display_name
        ),
        None,
    );
}

/// Complete 128-action command router matching `Coordinator.MessageRouter.cs` 1:1!
pub fn handle_bridge_action(
    env: &mut BridgeEnv<'_>,
    source_mon_id: &str,
    msg: &Value,
    dropped_files: &[String],
) {
    let action = str_prop(msg, "action");

    // Drag-and-drop file handlers from WebView2 AdditionalObjects
    if !dropped_files.is_empty() {
        match action {
            "dropFolderToStack" => {
                let mut added = false;
                let list = env.state.prefs.dock_folder_paths.get_or_insert_with(Vec::new);
                for p in dropped_files {
                    if Path::new(p).is_dir() && !list.iter().any(|e| e.eq_ignore_ascii_case(p)) {
                        list.push(p.clone());
                        added = true;
                    }
                }
                if added {
                    env.save_prefs();
                    env.post_toast("Dock Klasörü Eklendi", "Sürüklenen klasör alt Dock'a sabitlendi.", None);
                    *env.needs_state_push = true;
                }
                return;
            }
            "dropOnDockApp" => {
                let key = str_prop(msg, "key");
                if let Some(g) = env.state.dock_groups.iter().find(|g| g.group_key == key) {
                    if let Some(w) = g.windows.first() {
                        focus_window(w.hwnd);
                    }
                }
                return;
            }
            _ => {
                let zone = str_prop(msg, "zone");
                if zone == "qr" {
                    if let Some(first) = dropped_files.first() {
                        start_qr_for_path(env, first);
                    }
                    *env.needs_state_push = true;
                    return;
                }
                if zone == "zip" || zone == "convert" {
                    let desktop = user_known_folder("Desktop");
                    let out = desktop.join(format!("Aurora-Paket-{}.zip", chrono::Local::now().format("%H%M%S")));
                    if let Ok(sz) = zip_shelf_files(dropped_files, &out) {
                        env.post_toast("ZIP Oluşturuldu", &format!("Masaüstüne kaydedildi ({})", format_size(sz)), None);
                    }
                    return;
                }
                for p in dropped_files {
                    if !env.state.drop_shelf_paths.contains(p) {
                        env.state.drop_shelf_paths.insert(0, p.clone());
                    }
                }
                env.state.drop_shelf_paths.truncate(16);
                env.aux_store.save_drop_shelf(&env.state.drop_shelf_paths);
                env.post_toast("Süper Rafa Eklendi", &format!("{} dosya rafa alındı.", dropped_files.len()), None);
                *env.needs_state_push = true;
                return;
            }
        }
    }

    match action {
        // ── Focus & Overlay Coordination ──
        "requestInteractiveFocus" => {
            *env.popup_opened_at_ms = env.now_ms;
            env.crown.focus_and_activate();
        }
        "releaseInteractiveFocus" => {
            env.crown.release_interactive_to(env.state.last_external_hwnd);
        }
        "setDockPopoverState" => {
            let open = bool_prop(msg, "open");
            if open {
                if env.state.dock_popover_open_mons.is_empty() {
                    env.state.dock_popover_opened_at_ms = env.now_ms;
                }
                env.state
                    .dock_popover_open_mons
                    .insert(source_mon_id.to_string());
            } else {
                env.state.dock_popover_open_mons.remove(source_mon_id);
            }
        }
        "setCrownStageState" => {
            env.state.crown_stage_open = bool_prop(msg, "open");
        }
        "openOmni" => {
            *env.popup_opened_at_ms = env.now_ms;
            env.crown.focus_and_activate();
            env.crown.post_json(r#"{"type":"toggleOmni"}"#);
        }
        "openTray" => {
            *env.popup_opened_at_ms = env.now_ms;
            env.crown.focus_and_activate();
            env.crown.post_json(r#"{"type":"toggleTray"}"#);
        }
        "setMetricsActive" => {
            env.state.metrics_tab_active = bool_prop(msg, "active");
        }
        "completeOnboarding" => {
            env.state.prefs.onboarding_completed = true;
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "requestDockFocus" => {
            if let Some(d) = env.docks.iter_mut().find(|d| d.monitor_id.eq_ignore_ascii_case(source_mon_id)) {
                d.focus_for_inline_input();
            }
        }
        "releaseDockFocus" => {
            if let Some(d) = env.docks.iter_mut().find(|d| d.monitor_id.eq_ignore_ascii_case(source_mon_id)) {
                d.release_inline_input_to(env.state.last_external_hwnd);
            }
        }
        "requestWidgetFocus" => {
            if let Some(ws) = env.widgets_surface.as_mut() {
                ws.focus_for_inline_input();
            }
        }
        "releaseWidgetFocus" => {
            if let Some(ws) = env.widgets_surface.as_mut() {
                ws.release_inline_input();
            }
        }

        // ── v4.0 Panel Routing (Start, Quick Controls, Calendar/Notifications, Tools, Settings, Widget Gallery) ──
        "openStartHub" => {
            *env.popup_opened_at_ms = env.now_ms;
            env.crown.focus_and_activate();
            env.crown.post_json(r#"{"type":"toggleOmni"}"#);
        }
        "openQuickControls" => {
            if env.state.prefs.effective_show_dock_status() && !env.docks.is_empty() && str_prop(msg, "target") != "crown" {
                env.crown.release_interactive();
                env.docks[0].post_json(r#"{"type":"toggleQuickControls"}"#);
            } else {
                *env.popup_opened_at_ms = env.now_ms;
                env.crown.focus_and_activate();
                env.crown.post_json(r#"{"type":"openSettings","section":"appearance"}"#);
            }
        }
        "openCalendar" => {
            if env.state.prefs.effective_show_dock_status() && !env.docks.is_empty() {
                env.crown.release_interactive();
                env.docks[0].post_json(r#"{"type":"toggleCalNotif","tab":"calendar"}"#);
            } else {
                *env.popup_opened_at_ms = env.now_ms;
                env.crown.focus_and_activate();
                env.crown.post_json(r#"{"type":"toggleCalendar"}"#);
            }
        }
        "openNotifications" => {
            if env.state.prefs.effective_notification_mode() != "aurora-mirror" {
                env.crown.release_interactive();
                open_windows_notification_center();
            } else if env.state.prefs.effective_show_dock_status() && !env.docks.is_empty() {
                env.crown.release_interactive();
                env.docks[0].post_json(r#"{"type":"toggleCalNotif","tab":"notifications"}"#);
            } else {
                *env.popup_opened_at_ms = env.now_ms;
                env.crown.focus_and_activate();
                env.crown.post_json(r#"{"type":"toggleCalendar"}"#);
            }
        }
        "openWindowsNotifications" => {
            env.crown.release_interactive();
            open_windows_notification_center();
        }
        "setNotificationMode" => {
            let mode = if str_prop(msg, "mode") == "aurora-mirror" {
                "aurora-mirror"
            } else {
                "windows-native"
            };
            env.state.prefs.notification_mode = mode.into();
            env.state.prefs.system_notification_bridge_enabled = mode == "aurora-mirror";
            if mode == "windows-native" {
                env.state.unread_notif_count = 0;
            }
            env.save_prefs();
            *env.needs_state_push = true;
            let desc = if mode == "aurora-mirror" {
                "Aurora Bildirim Yansıtma Açık"
            } else {
                "Windows 11 Yerel Bildirimler (Çift Bildirim Yok)"
            };
            env.post_toast("Bildirim Yönetimi", desc, None);
        }
        "openToolsHub" => {
            *env.popup_opened_at_ms = env.now_ms;
            env.crown.focus_and_activate();
            let tab = str_prop(msg, "tab");
            let j = json!({ "type": "openTools", "tab": if tab.is_empty() { "shelf" } else { tab } }).to_string();
            env.crown.post_json(&j);
        }
        "openSettings" => {
            *env.popup_opened_at_ms = env.now_ms;
            env.crown.focus_and_activate();
            let sec = str_prop(msg, "section");
            let j = json!({ "type": "openSettings", "section": if sec.is_empty() { "appearance" } else { sec } }).to_string();
            env.crown.post_json(&j);
        }
        "openWidgetGallery" => {
            *env.popup_opened_at_ms = env.now_ms;
            env.crown.focus_and_activate();
            env.crown.post_json(r#"{"type":"openSettings","section":"widgets"}"#);
        }

        // ── v4.0 1-Click Experience Presets & Fine-Grained Customization ──
        "applyExperiencePreset" => {
            let preset = str_prop(msg, "preset");
            match preset {
                "windows-familiar" => {
                    env.state.prefs.dock_visibility_mode = Some("AlwaysVisible".into());
                    env.state.prefs.dock_auto_hide = false;
                    env.state.prefs.reserve_dock_work_area = Some(true);
                    env.state.prefs.dock_bar_layout = "Split3Zone".into();
                    env.state.prefs.dock_density = "Standard".into();
                    env.state.prefs.dock_edge = "Bottom".into();
                    env.state.prefs.smart_tidy_enabled = false;
                    env.state.prefs.show_dock_status_cluster = Some(true);
                    env.state.prefs.crown_visibility_mode = "Dynamic".into();
                    env.state.prefs.dock_hub_style = "cards".into();
                    env.state.prefs.dock_hover_style = "lift".into();
                    env.state.prefs.corner_radius_preset = "modern".into();
                    env.state.prefs.dock_silhouette = "fixed".into();
                    env.state.prefs.dock_start_style = "icon".into();
                    env.state.prefs.dock_indicator_style = "pill".into();
                    env.state.prefs.win_key_opens_omni_search = true;
                    env.post_toast("Windows 11 Tanıdık", "Sabit alt çubuk, sağ alt kontroller ve modern pencere kartları etkin.", None);
                }
                "floating-dock" => {
                    env.state.prefs.dock_visibility_mode = Some("SmartDodge".into());
                    env.state.prefs.dock_auto_hide = false;
                    env.state.prefs.reserve_dock_work_area = Some(false);
                    env.state.prefs.dock_bar_layout = "Centered".into();
                    env.state.prefs.dock_density = "Standard".into();
                    env.state.prefs.dock_edge = "Bottom".into();
                    env.state.prefs.smart_tidy_enabled = false;
                    env.state.prefs.show_dock_status_cluster = Some(true);
                    env.state.prefs.crown_visibility_mode = "Dynamic".into();
                    env.state.prefs.dock_hub_style = "dendritic".into();
                    env.state.prefs.dock_hover_style = "wave".into();
                    env.state.prefs.corner_radius_preset = "capsule".into();
                    env.state.prefs.dock_silhouette = "floating".into();
                    env.state.prefs.dock_indicator_style = "dot".into();
                    env.post_toast("macOS Tahoe Cam", "Yüzen optik cam kapsül, dalga efekti ve dallanan küre etkin.", None);
                }
                "split-islands" => {
                    env.state.prefs.dock_visibility_mode = Some("AlwaysVisible".into());
                    env.state.prefs.dock_auto_hide = false;
                    env.state.prefs.reserve_dock_work_area = Some(true);
                    env.state.prefs.dock_bar_layout = "Split3Zone".into();
                    env.state.prefs.dock_density = "Standard".into();
                    env.state.prefs.dock_edge = "Bottom".into();
                    env.state.prefs.show_dock_status_cluster = Some(true);
                    env.state.prefs.crown_visibility_mode = "Dynamic".into();
                    env.state.prefs.dock_hub_style = "fan3d".into();
                    env.state.prefs.dock_hover_style = "lift".into();
                    env.state.prefs.corner_radius_preset = "capsule".into();
                    env.state.prefs.dock_silhouette = "islands".into();
                    env.state.prefs.dock_indicator_style = "glow".into();
                    env.post_toast("Ayrık Adalar 2026", "3 bağımsız yüzen cam ada, 3D Yelpaze ve parlak gösterge etkin.", None);
                }
                "developer-pro" => {
                    env.state.prefs.dock_visibility_mode = Some("AlwaysVisible".into());
                    env.state.prefs.dock_auto_hide = false;
                    env.state.prefs.reserve_dock_work_area = Some(true);
                    env.state.prefs.dock_bar_layout = "Split3Zone".into();
                    env.state.prefs.dock_density = "Compact".into();
                    env.state.prefs.dock_edge = "Bottom".into();
                    env.state.prefs.smart_tidy_enabled = false;
                    env.state.prefs.show_dock_status_cluster = Some(true);
                    env.state.prefs.crown_visibility_mode = "Dynamic".into();
                    env.state.prefs.dock_hub_style = "dendritic".into();
                    env.state.prefs.dock_hover_style = "glow".into();
                    env.state.prefs.corner_radius_preset = "sharp".into();
                    env.state.prefs.dock_silhouette = "fixed".into();
                    env.state.prefs.dock_indicator_style = "frame".into();
                    env.state.prefs.show_clock_seconds = true;
                    env.post_toast("Geliştirici Pro", "Kompakt çubuk, hassas köşeler, saniyeli saat ve hızlı araçlar etkin.", None);
                }
                "minimalist-zen" => {
                    env.state.prefs.dock_visibility_mode = Some("AlwaysVisible".into());
                    env.state.prefs.dock_auto_hide = false;
                    env.state.prefs.reserve_dock_work_area = Some(false);
                    env.state.prefs.dock_bar_layout = "Centered".into();
                    env.state.prefs.dock_density = "Compact".into();
                    env.state.prefs.dock_edge = "Bottom".into();
                    env.state.prefs.show_dock_status_cluster = Some(true);
                    env.state.prefs.crown_visibility_mode = "AlertsOnly".into();
                    env.state.prefs.dock_hub_style = "cards".into();
                    env.state.prefs.dock_hover_style = "lift".into();
                    env.state.prefs.corner_radius_preset = "modern".into();
                    env.state.prefs.dock_silhouette = "minimal-rail".into();
                    env.state.prefs.dock_indicator_style = "dot".into();
                    env.post_toast("Minimalist Zen", "İnce alt şerit, sakin üst ada ve sıfır görsel kalabalık etkin.", None);
                }
                _ => {}
            }
            env.save_prefs();
            *env.needs_taskbar_sync = true;
            *env.needs_bounds_sync = true;
            *env.needs_hotkey_sync = true;
            *env.needs_state_push = true;
        }
        "setCrownVisibilityMode" => {
            let m = match str_prop(msg, "mode") {
                "AlertsOnly" => "AlertsOnly",
                "Hidden" => "Hidden",
                _ => "Dynamic",
            };
            env.state.prefs.crown_visibility_mode = m.into();
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "toggleShowDockStatus" => {
            let next = !env.state.prefs.effective_show_dock_status();
            env.state.prefs.show_dock_status_cluster = Some(next);
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "setDockHubStyle" => {
            let style = match str_prop(msg, "style") {
                "fan3d" => "fan3d",
                "cards" => "cards",
                "radial" => "radial",
                _ => "dendritic",
            };
            env.state.prefs.dock_hub_style = style.into();
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "setDockHoverStyle" => {
            let style = match str_prop(msg, "style") {
                "wave" => "wave",
                "glow" => "glow",
                _ => "lift",
            };
            env.state.prefs.dock_hover_style = style.into();
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "setCornerRadiusPreset" => {
            let p = match str_prop(msg, "preset") {
                "sharp" => "sharp",
                "capsule" => "capsule",
                _ => "modern",
            };
            env.state.prefs.corner_radius_preset = p.into();
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "setDockStartStyle" => {
            let s = match str_prop(msg, "style") {
                "capsule" => "capsule",
                _ => "icon",
            };
            env.state.prefs.dock_start_style = s.into();
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "setDockSilhouette" => {
            let s = match str_prop(msg, "silhouette") {
                "floating" => "floating",
                "islands" => "islands",
                "minimal-rail" => "minimal-rail",
                "compact" => "compact",
                _ => "fixed",
            };
            env.state.prefs.dock_silhouette = s.into();
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "setDockIndicatorStyle" => {
            let s = match str_prop(msg, "style") {
                "dot" => "dot",
                "frame" => "frame",
                "glow" => "glow",
                _ => "pill",
            };
            env.state.prefs.dock_indicator_style = s.into();
            env.save_prefs();
            *env.needs_state_push = true;
        }
        // ── v7.0 Next-Gen Shell Architectures (10 Distinct Styles) & Live Dynamic Island (6 Styles) ──
        "setShellArchitecture" => {
            let raw = str_prop(msg, "arch");
            let arch = match raw {
                "nothing-hardware" => "nothing-hardware",
                "linear-studio" => "linear-studio",
                "vision-orb" => "vision-orb",
                "arc-split" => "arc-split",
                "braun-atelier" => "braun-atelier",
                "swiss-editorial" => "swiss-editorial",
                "cockpit-hud" => "cockpit-hud",
                "fluent-mica" | "fluent-aurora" => "fluent-mica",
                "zen-ghost" => "zen-ghost",
                _ => "tahoe-glass",
            };
            env.state.prefs.shell_architecture = arch.into();
            env.state.prefs.dock_silhouette = match arch {
                "tahoe-glass" | "vision-orb" | "braun-atelier" | "cockpit-hud" => "floating",
                "nothing-hardware" | "arc-split" | "zen-ghost" => "islands",
                "linear-studio" => "minimal-rail",
                _ => "fixed",
            }
            .into();
            env.state.prefs.island_style = match arch {
                "nothing-hardware" | "braun-atelier" => "hardware-pod",
                "linear-studio" | "cockpit-hud" | "zen-ghost" => "edge-horizon",
                "vision-orb" => "orbital-halo",
                "swiss-editorial" => "glass-bar",
                "fluent-mica" => "mac-notch",
                _ => "liquid-pill",
            }
            .into();
            let toast_name = match arch {
                "nothing-hardware" => "Nothing 3-Ada Donanım Mimarisi",
                "linear-studio" => "Linear Ultra-Craft (40px)",
                "vision-orb" => "visionOS Spatial Küre Mimarisi",
                "arc-split" => "Arc Çift Bölümlü Mimari",
                "braun-atelier" => "Braun Dieter Rams Konsol",
                "swiss-editorial" => "İsviçre Editoryal Izgara",
                "cockpit-hud" => "Aero Kokpit HUD Mimarisi",
                "fluent-mica" => "Windows 11 Fluent Pro",
                "zen-ghost" => "Zen Şeffaf Hayalet Şerit",
                _ => "macOS Tahoe Spatial Mimarisi",
            };
            env.save_prefs();
            *env.needs_taskbar_sync = true;
            *env.needs_bounds_sync = true;
            *env.needs_state_push = true;
            env.post_toast("Kabuk Mimarisi Değişti", toast_name, None);
        }
        "setIslandConfig" => {
            if let Some(st) = msg.get("style").and_then(|v| v.as_str()) {
                env.state.prefs.island_style = match st {
                    "mac-notch" | "compact-notch" => "mac-notch",
                    "edge-horizon" => "edge-horizon",
                    "hardware-pod" => "hardware-pod",
                    "glass-bar" => "glass-bar",
                    "orbital-halo" => "orbital-halo",
                    _ => "liquid-pill",
                }
                .into();
            }
            if let Some(im) = msg.get("idleMode").and_then(|v| v.as_str()) {
                env.state.prefs.island_idle_mode = match im {
                    "auto-stealth" => "auto-stealth",
                    "glance-weather" => "glance-weather",
                    "system-pulse" => "system-pulse",
                    "minimal-brand" => "minimal-brand",
                    _ => "media-ambient",
                }
                .into();
            }
            if let Some(vm) = msg.get("visibilityMode").and_then(|v| v.as_str()) {
                env.state.prefs.crown_visibility_mode = match vm {
                    "AlertsOnly" => "AlertsOnly",
                    "Hidden" => "Hidden",
                    _ => "Dynamic",
                }
                .into();
            }
            if let Some(bt) = msg.get("btPopin").and_then(|v| v.as_bool()) {
                env.state.prefs.island_bt_popin = bt;
            }
            if let Some(pg) = msg.get("privacyGuard").and_then(|v| v.as_bool()) {
                env.state.prefs.island_privacy_guard = pg;
            }
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "toggleEcoAuto" => {
            env.state.prefs.eco_mode_auto_on_low_battery = !env.state.prefs.eco_mode_auto_on_low_battery;
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "syncWallpaperAccent" => {
            let (eff, _, _) = env.state.effective_theme();
            let _ = env.bg_tx.try_send(BgCommand::SyncWallpaperAccent(eff.colors.surface1));
            env.post_toast("Duvar Kağıdı Taranıyor", "Baskın WCAG AA+ vurgu renkleri çıkarılıyor...", None);
        }
        "toggleDockWidget" => {
            let kind = str_prop(msg, "kind");
            let id = str_prop(msg, "id");
            let mut activated_kind: Option<String> = None;
            if let Some(dw) = env.state.dock_widgets.iter_mut().find(|w| (!id.is_empty() && w.id == id) || (!kind.is_empty() && w.kind == kind)) {
                dw.enabled = !dw.enabled;
                if dw.enabled {
                    activated_kind = Some(dw.kind.clone());
                }
                env.save_widgets();
                *env.needs_state_push = true;
            }
            if let Some(k) = activated_kind {
                if k == "ai-quota" {
                    let _ = env.bg_tx.try_send(BgCommand::RefreshAiQuota(env.state.ai_configs.clone()));
                } else if k == "market" {
                    let _ = env.bg_tx.try_send(BgCommand::RefreshMarket);
                }
            }
        }
        "updateDockWidget" => {
            let kind = str_prop(msg, "kind");
            let id = str_prop(msg, "id");
            let mut activated_kind: Option<String> = None;
            let mut next_zone_order: Option<i32> = None;
            if let Some(z) = msg.get("zone").and_then(|v| v.as_str()) {
                if matches!(z, "far-left" | "before-apps" | "after-apps" | "far-right")
                    && msg.get("order").is_none()
                {
                    let max_ord = env
                        .state
                        .dock_widgets
                        .iter()
                        .filter(|w| w.effective_zone() == z)
                        .map(|w| w.order)
                        .max()
                        .map(|m| m + 1)
                        .unwrap_or(0);
                    next_zone_order = Some(max_ord);
                }
            }
            if let Some(dw) = env.state.dock_widgets.iter_mut().find(|w| (!id.is_empty() && w.id == id) || (!kind.is_empty() && w.kind == kind)) {
                if let Some(en) = msg.get("enabled").and_then(|v| v.as_bool()) {
                    dw.enabled = en;
                    if en {
                        activated_kind = Some(dw.kind.clone());
                    }
                }
                if let Some(sl) = msg.get("slot").and_then(|v| v.as_str()) {
                    if matches!(sl, "left" | "right") {
                        dw.slot = sl.into();
                        if msg.get("zone").is_none() {
                            dw.zone = if sl == "left" { "before-apps".into() } else { "after-apps".into() };
                        }
                    }
                }
                if let Some(z) = msg.get("zone").and_then(|v| v.as_str()) {
                    if matches!(z, "far-left" | "before-apps" | "after-apps" | "far-right") {
                        dw.zone = z.into();
                        dw.slot = if matches!(z, "far-left" | "before-apps") { "left".into() } else { "right".into() };
                        if let Some(ord) = next_zone_order {
                            dw.order = ord;
                        }
                    }
                }
                if let Some(ord) = msg.get("order").and_then(|v| v.as_i64()) {
                    dw.order = ord.clamp(0, 999) as i32;
                }
                if let Some(var) = msg.get("variant").and_then(|v| v.as_str()) {
                    if !var.is_empty() {
                        dw.variant = var.into();
                    }
                }
                if let Some(sz) = msg.get("size").and_then(|v| v.as_str()) {
                    if matches!(sz, "compact" | "normal" | "wide") {
                        dw.size = sz.into();
                    }
                }
                if let Some(sm) = msg.get("styleMode").and_then(|v| v.as_str()) {
                    if matches!(sm, "pill" | "clean" | "tinted" | "glass" | "hardware") {
                        dw.style_mode = sm.into();
                    }
                }
                if let Some(wa) = msg.get("wheelAction").and_then(|v| v.as_str()) {
                    if matches!(wa, "default" | "volume" | "brightness" | "desktop" | "media" | "none") {
                        dw.wheel_action = wa.into();
                    }
                }
                dw.normalize();
                env.save_widgets();
                *env.needs_state_push = true;
            }
            if let Some(k) = activated_kind {
                if k == "ai-quota" {
                    let _ = env.bg_tx.try_send(BgCommand::RefreshAiQuota(env.state.ai_configs.clone()));
                } else if k == "market" {
                    let _ = env.bg_tx.try_send(BgCommand::RefreshMarket);
                }
            }
        }
        "moveDockWidget" => {
            let kind = str_prop(msg, "kind");
            let id = str_prop(msg, "id");
            let target_zone = match str_prop(msg, "targetZone") {
                "far-left" => "far-left",
                "before-apps" => "before-apps",
                "after-apps" => "after-apps",
                "far-right" => "far-right",
                _ => "",
            };
            let target_idx = i64_prop(msg, "targetIndex", 999).max(0) as usize;
            if !target_zone.is_empty() {
                if let Some(moved_pos) = env.state.dock_widgets.iter().position(|w| {
                    (!id.is_empty() && w.id == id) || (!kind.is_empty() && w.kind == kind)
                }) {
                    let moved_id = env.state.dock_widgets[moved_pos].id.clone();
                    let moved_kind = env.state.dock_widgets[moved_pos].kind.clone();
                    {
                        let dw = &mut env.state.dock_widgets[moved_pos];
                        dw.zone = target_zone.into();
                        dw.slot = if matches!(target_zone, "far-left" | "before-apps") {
                            "left".into()
                        } else {
                            "right".into()
                        };
                        dw.enabled = true;
                        dw.normalize();
                    }
                    let mut zone_members: Vec<(usize, i32)> = env
                        .state
                        .dock_widgets
                        .iter()
                        .enumerate()
                        .filter(|&(i, w)| i != moved_pos && w.effective_zone() == target_zone)
                        .map(|(i, w)| (i, w.order))
                        .collect();
                    zone_members.sort_by_key(|&(i, ord)| (ord, i));
                    let mut ordered_indices: Vec<usize> =
                        zone_members.into_iter().map(|(i, _)| i).collect();
                    let dst = target_idx.min(ordered_indices.len());
                    ordered_indices.insert(dst, moved_pos);
                    for (ord, idx) in ordered_indices.into_iter().enumerate() {
                        env.state.dock_widgets[idx].order = ord as i32;
                    }
                    let _ = moved_id;
                    if moved_kind == "ai-quota" {
                        let _ = env.bg_tx.try_send(BgCommand::RefreshAiQuota(env.state.ai_configs.clone()));
                    } else if moved_kind == "market" {
                        let _ = env.bg_tx.try_send(BgCommand::RefreshMarket);
                    }
                    env.save_widgets();
                    *env.needs_state_push = true;
                }
            }
        }
        "setDockEditMode" => {
            let active = bool_prop(msg, "active");
            env.state.dock_edit_mode = active;
            if active {
                env.crown.release_interactive();
                env.crown.post_json(r#"{"type":"outsideDeactivate"}"#);
                env.post_toast(
                    "Alt Dock Düzenleme Modu",
                    "Widget'ları 4 bölge arasında sürükleyin veya sağ tıklayın. Bitince ESC / Tamam'a basın.",
                    None,
                );
            }
            *env.needs_state_push = true;
        }
        "setDockGesturePrefs" => {
            if let Some(s) = msg.get("emptyWheel").and_then(|v| v.as_str()) {
                if matches!(s, "volume" | "brightness" | "desktop" | "media" | "none") {
                    env.state.prefs.dock_empty_wheel_action = s.into();
                }
            }
            if let Some(s) = msg.get("emptyDblClick").and_then(|v| v.as_str()) {
                if matches!(s, "show-desktop" | "omni" | "quick-wheel" | "task-view" | "lock" | "none") {
                    env.state.prefs.dock_empty_dblclick_action = s.into();
                }
            }
            if let Some(s) = msg.get("emptyMidClick").and_then(|v| v.as_str()) {
                if matches!(s, "media-play" | "mute" | "quick-wheel" | "show-desktop" | "none") {
                    env.state.prefs.dock_empty_midclick_action = s.into();
                }
            }
            if let Some(b) = msg.get("scrubGestures").and_then(|v| v.as_bool()) {
                env.state.prefs.dock_scrub_gestures_enabled = b;
            }
            if let Some(b) = msg.get("perAppAudio").and_then(|v| v.as_bool()) {
                env.state.prefs.dock_per_app_audio_enabled = b;
            }
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "runEmptyDockAction" => {
            match str_prop(msg, "actionKind") {
                "show-desktop" => send_win_d(),
                "omni" => {
                    *env.popup_opened_at_ms = env.now_ms;
                    env.crown.focus_and_activate();
                    env.crown.post_json(r#"{"type":"toggleOmni"}"#);
                }
                "media-play" => {
                    send_media_command("play_pause");
                    env.state.media.is_playing = !env.state.media.is_playing;
                    *env.needs_state_push = true;
                }
                "mute" => {
                    let muted = env.audio.toggle_master_mute();
                    env.state.audio.is_master_muted = muted;
                    let pct = env.state.audio.master_volume_percent;
                    let vol_str = format!("%{pct}");
                    env.post_osd(
                        if muted { "mute" } else { "volume" },
                        "Ses",
                        if muted { "Sessiz" } else { &vol_str },
                        Some(if muted { 0 } else { pct }),
                        !muted,
                        1400,
                    );
                    *env.needs_state_push = true;
                }
                "task-view" => open_task_view(),
                "lock" => trigger_lock_workstation(),
                _ => {}
            }
        }
        "resetDockWidgets" => {
            env.state.dock_widgets = default_dock_widgets();
            env.save_widgets();
            env.post_toast("Alt Dock Widget'ları Sıfırlandı", "Varsayılan Müzik, Hava ve Saat kapsülleri geri yüklendi.", None);
            *env.needs_state_push = true;
        }
        "addTodo" => {
            let txt = str_prop(msg, "text").trim();
            if !txt.is_empty() {
                env.state.todos.insert(
                    0,
                    TodoItem {
                        id: format!("td-{:x}", env.now_ms ^ (env.state.todos.len() as u64 + 17)),
                        text: txt.chars().take(120).collect(),
                        done: false,
                    },
                );
                env.state.todos.truncate(24);
                env.save_widgets();
                *env.needs_state_push = true;
            }
        }
        "toggleTodo" => {
            let id = str_prop(msg, "id");
            if let Some(t) = env.state.todos.iter_mut().find(|t| t.id == id) {
                t.done = !t.done;
                env.save_widgets();
                *env.needs_state_push = true;
            }
        }
        "removeTodo" => {
            let id = str_prop(msg, "id");
            let before = env.state.todos.len();
            env.state.todos.retain(|t| t.id != id);
            if env.state.todos.len() != before {
                env.save_widgets();
                *env.needs_state_push = true;
            }
        }
        "saveCountdown" => {
            let title = str_prop(msg, "title").trim();
            let date = str_prop(msg, "targetDate").trim();
            if !title.is_empty() {
                env.state.countdown.title = title.chars().take(48).collect();
            }
            if date.len() == 10 && date.chars().nth(4) == Some('-') && date.chars().nth(7) == Some('-') {
                env.state.countdown.target_date = date.into();
            }
            env.save_widgets();
            *env.needs_state_push = true;
        }
        "refreshMarket" => {
            let _ = env.bg_tx.try_send(BgCommand::RefreshMarket);
        }
        "refreshLocalAi" => {
            let _ = env.bg_tx.try_send(BgCommand::RefreshLocalAiAndDisks);
        }
        "toggleAutoDayNight" => {
            env.state.prefs.auto_day_night_theme = !env.state.prefs.auto_day_night_theme;
            if env.state.prefs.auto_day_night_theme {
                use chrono::Timelike;
                let hr = chrono::Local::now().hour();
                let tid = if (7..19).contains(&hr) {
                    env.state.prefs.day_theme_id.clone()
                } else {
                    env.state.prefs.night_theme_id.clone()
                };
                env.state.theme_id = theme_by_id(&tid).id;
                env.save_user_prefs();
            }
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "setDayNightThemes" => {
            let day = str_prop(msg, "dayThemeId");
            let night = str_prop(msg, "nightThemeId");
            if !day.is_empty() {
                env.state.prefs.day_theme_id = theme_by_id(day).id;
            }
            if !night.is_empty() {
                env.state.prefs.night_theme_id = theme_by_id(night).id;
            }
            env.save_prefs();
            *env.needs_state_push = true;
        }

        // ── v4.0 Desktop Widget Engine Actions ──
        "toggleWidgetsEnabled" => {
            env.state.prefs.widgets_enabled = !env.state.prefs.widgets_enabled;
            if !env.state.prefs.widgets_enabled {
                env.state.widget_edit_mode = false;
            }
            env.save_prefs();
            *env.needs_widgets_sync = true;
            *env.needs_state_push = true;
        }
        "toggleWidgetsLocked" => {
            env.state.prefs.widgets_locked = !env.state.prefs.widgets_locked;
            if env.state.prefs.widgets_locked {
                env.state.widget_edit_mode = false;
            }
            env.save_prefs();
            *env.needs_widgets_sync = true;
            *env.needs_state_push = true;
        }
        "setWidgetEditMode" => {
            let active = bool_prop(msg, "active");
            env.state.widget_edit_mode = active;
            if active {
                env.state.prefs.widgets_enabled = true;
                env.state.prefs.widgets_locked = false;
                env.save_prefs();
                env.crown.release_interactive();
                env.crown.post_json(r#"{"type":"outsideDeactivate"}"#);
            }
            *env.needs_widgets_sync = true;
            *env.needs_state_push = true;
        }
        "addDesktopWidget" => {
            let kind = str_prop(msg, "kind").trim();
            if !kind.is_empty() {
                let def_variant = match kind {
                    "clock" => "swiss",
                    "media" => "ambient",
                    "battery" => "rings",
                    "ai-quota" => "cards",
                    "weather" => "atmospheric",
                    "system" => "rings",
                    "calendar" => "today",
                    "notes" => "glass",
                    "pomodoro" => "ring",
                    "launcher" => "grid",
                    "todo" => "list",
                    "market" => "cards",
                    "countdown" => "card",
                    "storage-net" => "bars",
                    "quote" => "editorial",
                    "local-ai" => "status",
                    _ => "default",
                };
                let variant_in = str_prop(msg, "variant");
                let variant = if variant_in.is_empty() {
                    match kind {
                        "audio-mixer" => "mixer",
                        "focus-rings" => "rings",
                        "photo-frame" => "polaroid",
                        "quick-drop" => "drop",
                        _ => def_variant,
                    }
                } else {
                    variant_in
                };
                let skin_in = str_prop(msg, "skin");
                let skin = match skin_in {
                    "frameless" | "glass" | "neoglass" | "braun" | "matrix" | "editorial" => skin_in,
                    _ => if kind == "clock" { "frameless" } else { "neoglass" },
                };
                let size_in = str_prop(msg, "size");
                let size = match size_in {
                    "S" | "M" | "L" => size_in,
                    _ => if kind == "ai-quota" { "L" } else { "M" },
                };
                let idx = env.state.widgets.len() as i32;
                let col = (idx / 3) % 3;
                let row = idx % 3;
                let x = -344 - col * 336;
                let y = 48 + row * 196;
                let id = format!("w-{}-{:x}", kind, env.now_ms ^ ((idx as u64 + 1) * 131));
                env.state.widgets.push(DesktopWidgetInstance {
                    id,
                    kind: kind.into(),
                    variant: variant.into(),
                    skin: skin.into(),
                    size: size.into(),
                    x,
                    y,
                    opacity: None,
                    accent_hex: None,
                    pinned_top: false,
                    note_text: (kind == "notes").then(|| "Bugünkü notlarım...".into()),
                    photo_path: None,
                });
                env.state.prefs.widgets_enabled = true;
                env.save_prefs();
                env.save_widgets();
                if kind == "ai-quota" {
                    let _ = env.bg_tx.try_send(BgCommand::RefreshAiQuota(env.state.ai_configs.clone()));
                } else if kind == "market" {
                    let _ = env.bg_tx.try_send(BgCommand::RefreshMarket);
                } else if kind == "local-ai" || kind == "storage-net" {
                    let _ = env.bg_tx.try_send(BgCommand::RefreshLocalAiAndDisks);
                }
                env.post_toast("Widget Eklendi", "Masaüstünüze yerleştirildi. Düzenle ile taşıyabilirsiniz.", None);
                *env.needs_widgets_sync = true;
                *env.needs_state_push = true;
            }
        }
        "updateDesktopWidget" => {
            let id = str_prop(msg, "id");
            let mut pin_changed = false;
            if let Some(w) = env.state.widgets.iter_mut().find(|w| w.id == id) {
                if let Some(v) = msg.get("x").and_then(|x| x.as_i64().or_else(|| x.as_f64().map(|f| f.round() as i64))) {
                    w.x = v.clamp(-3800, 3800) as i32;
                }
                if let Some(v) = msg.get("y").and_then(|y| y.as_i64().or_else(|| y.as_f64().map(|f| f.round() as i64))) {
                    w.y = v.clamp(8, 2200) as i32;
                }
                if let Some(s) = msg.get("size").and_then(|v| v.as_str()) {
                    if matches!(s, "S" | "M" | "L") {
                        w.size = s.into();
                    }
                }
                if let Some(var) = msg.get("variant").and_then(|v| v.as_str()) {
                    if !var.is_empty() {
                        w.variant = var.into();
                    }
                }
                if let Some(sk) = msg.get("skin").and_then(|v| v.as_str()) {
                    if matches!(sk, "frameless" | "glass" | "neoglass" | "braun" | "matrix" | "editorial") {
                        w.skin = sk.into();
                    }
                }
                if let Some(op) = msg.get("opacity").and_then(|v| v.as_f64()) {
                    w.opacity = Some((op as f32).clamp(0.45, 1.0));
                }
                if let Some(acc) = msg.get("accentHex").and_then(|v| v.as_str()) {
                    w.accent_hex = if acc.is_empty() { None } else { Some(acc.into()) };
                }
                if let Some(pt) = msg.get("pinnedTop").and_then(|v| v.as_bool()) {
                    if w.pinned_top != pt {
                        w.pinned_top = pt;
                        pin_changed = true;
                    }
                }
                if let Some(nt) = msg.get("noteText").and_then(|v| v.as_str()) {
                    w.note_text = Some(nt.chars().take(1200).collect());
                }
                env.save_widgets();
                if pin_changed {
                    *env.needs_widgets_sync = true;
                }
                *env.needs_state_push = true;
            }
        }
        "pickWidgetPhoto" => {
            let id = str_prop(msg, "id");
            if !id.is_empty() {
                let _ = env.bg_tx.try_send(BgCommand::PickWidgetPhoto {
                    widget_id: id.into(),
                });
            }
        }
        "logFocusMinutes" => {
            let delta = i64_prop(msg, "delta", 15).clamp(1, 180) as u32;
            env.state.focus_completed_mins = env.state.focus_completed_mins.saturating_add(delta);
            *env.needs_state_push = true;
        }
        "applyWidgetBundle" => {
            let bundle = str_prop(msg, "bundle");
            let mk = |idx: usize, kind: &str, variant: &str, skin: &str, size: &str, x: i32, y: i32| DesktopWidgetInstance {
                id: format!("w-b-{kind}-{idx}"),
                kind: kind.into(),
                variant: variant.into(),
                skin: skin.into(),
                size: size.into(),
                x,
                y,
                opacity: None,
                accent_hex: None,
                pinned_top: false,
                note_text: None,
                photo_path: None,
            };
            let (label, list) = match bundle {
                "nothing-hardware" => (
                    "Nothing Donanım Paketi",
                    vec![
                        mk(0, "clock", "dotmatrix-led", "matrix", "M", -348, 48),
                        mk(1, "system", "oscilloscope", "matrix", "M", -348, 224),
                        mk(2, "media", "tp7", "matrix", "M", -348, 412),
                        mk(3, "battery", "rings", "matrix", "S", -348, 594),
                    ],
                ),
                "productivity-studio" => (
                    "Üretkenlik Stüdyosu Paketi",
                    vec![
                        mk(0, "focus-rings", "rings", "neoglass", "M", -348, 48),
                        mk(1, "calendar", "month", "glass", "M", -348, 240),
                        mk(2, "audio-mixer", "mixer", "glass", "M", -348, 478),
                        mk(3, "quick-drop", "drop", "glass", "M", -692, 48),
                    ],
                ),
                "dev-cockpit" => (
                    "Geliştirici Kokpit Paketi",
                    vec![
                        mk(0, "clock", "dev-hex", "braun", "M", -348, 48),
                        mk(1, "system", "oscilloscope", "braun", "M", -348, 220),
                        mk(2, "ai-quota", "cards", "braun", "M", -348, 410),
                        mk(3, "local-ai", "models", "braun", "M", -692, 48),
                    ],
                ),
                _ => (
                    "Editoryal Minimal Paketi",
                    vec![
                        mk(0, "clock", "editorial-poster", "editorial", "M", -348, 48),
                        mk(1, "weather", "dynamic-sky", "neoglass", "M", -348, 250),
                        mk(2, "media", "poster", "neoglass", "M", -348, 430),
                    ],
                ),
            };
            env.state.widgets = list;
            env.state.prefs.widgets_enabled = true;
            env.state.prefs.widgets_locked = false;
            env.save_prefs();
            env.save_widgets();
            let _ = env.bg_tx.try_send(BgCommand::RefreshWeather);
            let _ = env.bg_tx.try_send(BgCommand::RefreshAiQuota(env.state.ai_configs.clone()));
            let _ = env.bg_tx.try_send(BgCommand::RefreshLocalAiAndDisks);
            env.post_toast("Masaüstü Paketi Uygulandı", label, None);
            *env.needs_widgets_sync = true;
            *env.needs_state_push = true;
        }
        "removeDesktopWidget" => {
            let id = str_prop(msg, "id");
            let before = env.state.widgets.len();
            env.state.widgets.retain(|w| w.id != id);
            if env.state.widgets.len() != before {
                env.save_widgets();
                *env.needs_widgets_sync = true;
                *env.needs_state_push = true;
            }
        }
        "resetDesktopWidgets" => {
            env.state.widgets = default_starter_widgets();
            env.state.prefs.widgets_enabled = true;
            env.state.prefs.widgets_locked = false;
            env.state.widget_edit_mode = false;
            env.save_prefs();
            env.save_widgets();
            env.post_toast("Widget'lar Sıfırlandı", "Varsayılan Saat ve Pil widget'ları geri yüklendi.", None);
            *env.needs_widgets_sync = true;
            *env.needs_state_push = true;
        }

        // ── v4.0 AI Quota Engine (DPAPI Encrypted API Keys + Local CLI Logs + Manual Rolling Tracker) ──
        "saveAiProviderConfig" => {
            let prov = str_prop(msg, "provider");
            if let Some(cfg) = env.state.ai_configs.iter_mut().find(|c| c.provider == prov) {
                if let Some(p) = msg.get("planName").and_then(|v| v.as_str()) {
                    if !p.trim().is_empty() {
                        cfg.plan_name = p.trim().chars().take(24).collect();
                    }
                }
                if let Some(lim) = msg.get("windowLimit").and_then(|v| v.as_u64()) {
                    cfg.window_limit = (lim as u32).clamp(1, 100_000);
                }
                if let Some(mins) = msg.get("windowMinutes").and_then(|v| v.as_u64()) {
                    cfg.window_minutes = (mins as u32).clamp(15, 43_200);
                }
                if let Some(b) = msg.get("monthlyBudgetUsd").and_then(|v| v.as_f64()) {
                    cfg.monthly_budget_usd = b.clamp(1.0, 10_000.0);
                }
                env.save_widgets();
                let _ = env.bg_tx.try_send(BgCommand::RefreshAiQuota(env.state.ai_configs.clone()));
                *env.needs_state_push = true;
            }
        }
        "saveAiApiKey" => {
            let prov = str_prop(msg, "provider");
            let key = str_prop(msg, "apiKey");
            if matches!(prov, "openai" | "gemini" | "claude")
                && save_ai_api_key(prov, key, None)
            {
                if let Some(cfg) = env.state.ai_configs.iter_mut().find(|c| c.provider == prov) {
                    cfg.has_api_key = !key.trim().is_empty();
                }
                env.save_widgets();
                let _ = env.bg_tx.try_send(BgCommand::RefreshAiQuota(env.state.ai_configs.clone()));
                env.post_toast(
                    if key.trim().is_empty() { "API Anahtarı Kaldırıldı" } else { "DPAPI Kasasına Kaydedildi" },
                    if key.trim().is_empty() {
                        "Yerel CLI / Sayaç moduna dönüldü."
                    } else {
                        "Anahtar Windows DPAPI ile şifrelendi. Canlı sorgu başlatılıyor."
                    },
                    None,
                );
                *env.needs_state_push = true;
            }
        }
        "bumpAiManualUsage" => {
            let prov = str_prop(msg, "provider");
            let delta = i64_prop(msg, "delta", 1);
            if let Some(cfg) = env.state.ai_configs.iter_mut().find(|c| c.provider == prov) {
                let now_epoch = chrono::Utc::now().timestamp();
                let win_secs = (cfg.window_minutes.max(15) as i64) * 60;
                if cfg.window_started_epoch == 0 || (now_epoch - cfg.window_started_epoch) >= win_secs {
                    cfg.manual_used = 0;
                    cfg.window_started_epoch = now_epoch;
                }
                if delta >= 0 {
                    cfg.manual_used = cfg.manual_used.saturating_add(delta as u32);
                } else {
                    cfg.manual_used = cfg.manual_used.saturating_sub(delta.unsigned_abs() as u32);
                }
                env.save_widgets();
                let _ = env.bg_tx.try_send(BgCommand::RefreshAiQuota(env.state.ai_configs.clone()));
                *env.needs_state_push = true;
            }
        }
        "resetAiManualUsage" => {
            let prov = str_prop(msg, "provider");
            if let Some(cfg) = env.state.ai_configs.iter_mut().find(|c| c.provider == prov) {
                cfg.manual_used = 0;
                cfg.window_started_epoch = chrono::Utc::now().timestamp();
                env.save_widgets();
                let _ = env.bg_tx.try_send(BgCommand::RefreshAiQuota(env.state.ai_configs.clone()));
                *env.needs_state_push = true;
            }
        }
        "refreshAiQuota" => {
            let _ = env.bg_tx.try_send(BgCommand::RefreshAiQuota(env.state.ai_configs.clone()));
            env.post_toast("AI Kotaları Yenileniyor", "API / Yerel CLI günlükleri taranıyor...", None);
        }

        // ── Dock & Window Management ──
        "invokeGroup" => {
            let key = str_prop(msg, "key");
            if let Some(g) = env.state.dock_groups.iter().find(|g| g.group_key == key).cloned() {
                if g.windows.is_empty() {
                    launch_dock_group_reliably(env, &g);
                } else {
                    activate_or_toggle_window(g.windows[0].hwnd);
                }
            }
            *env.needs_state_push = true;
        }
        "cycleGroupWindow" => {
            let key = str_prop(msg, "key");
            let delta = i64_prop(msg, "delta", 1);
            let from_wheel = bool_prop(msg, "fromWheel");
            if let Some(g) = env.state.dock_groups.iter().find(|g| g.group_key == key).cloned() {
                let mut wins: Vec<&AppWindowEntry> = if env.state.prefs.per_monitor_window_filter {
                    g.windows.iter().filter(|w| w.monitor_device_id.eq_ignore_ascii_case(source_mon_id)).collect()
                } else {
                    g.windows.iter().collect()
                };
                if wins.is_empty() && !g.windows.is_empty() {
                    wins = g.windows.iter().collect();
                }
                if wins.is_empty() {
                    launch_dock_group_reliably(env, &g);
                } else if wins.len() == 1 {
                    if from_wheel {
                        focus_window(wins[0].hwnd);
                    } else {
                        activate_or_toggle_window(wins[0].hwnd);
                    }
                    env.state.last_focused_by_group.insert(key.into(), wins[0].hwnd);
                } else {
                    let active_idx = wins
                        .iter()
                        .position(|w| w.is_active || w.hwnd == env.state.last_external_hwnd);
                    let target = if let Some(idx) = active_idx {
                        let step = if delta >= 0 { 1isize } else { -1isize };
                        let next = (idx as isize + step).rem_euclid(wins.len() as isize) as usize;
                        wins[next].hwnd
                    } else if let Some(&rem) = env.state.last_focused_by_group.get(key) {
                        if let Some(ridx) = wins.iter().position(|w| w.hwnd == rem) {
                            if from_wheel {
                                let step = if delta >= 0 { 1isize } else { -1isize };
                                let next = (ridx as isize + step).rem_euclid(wins.len() as isize) as usize;
                                wins[next].hwnd
                            } else {
                                rem
                            }
                        } else {
                            wins[0].hwnd
                        }
                    } else {
                        wins[0].hwnd
                    };
                    focus_window(target);
                    env.state.last_external_hwnd = target;
                    env.state.last_focused_by_group.insert(key.into(), target);
                }
            }
            *env.needs_state_push = true;
        }
        "cycleCategoryWindows" => {
            let delta = i64_prop(msg, "delta", 1);
            if let Some(arr) = msg.get("hwnds").and_then(|v| v.as_array()) {
                let hwnds: Vec<isize> = arr.iter().filter_map(|v| v.as_i64()).map(|h| h as isize).filter(|&h| h != 0).collect();
                if !hwnds.is_empty() {
                    let cur = hwnds.iter().position(|&h| h == env.state.last_external_hwnd);
                    let target = if let Some(idx) = cur {
                        let step = if delta >= 0 { 1isize } else { -1isize };
                        hwnds[(idx as isize + step).rem_euclid(hwnds.len() as isize) as usize]
                    } else {
                        hwnds[0]
                    };
                    focus_window(target);
                    env.state.last_external_hwnd = target;
                }
            }
            *env.needs_state_push = true;
        }
        "focusWindow" => {
            let h = i64_prop(msg, "hwnd", 0) as isize;
            if h != 0 {
                focus_window(h);
                env.state.last_external_hwnd = h;
            }
            *env.needs_state_push = true;
        }
        "activateWindow" => {
            let h = i64_prop(msg, "hwnd", 0) as isize;
            if h != 0 {
                activate_or_toggle_window(h);
            }
            *env.needs_state_push = true;
        }
        "minimizeWindow" => {
            let h = i64_prop(msg, "hwnd", 0) as isize;
            if h != 0 { minimize_window(h); }
            *env.needs_state_push = true;
        }
        "closeWindow" => {
            let h = i64_prop(msg, "hwnd", 0) as isize;
            if h != 0 { close_window(h); }
            *env.needs_state_push = true;
        }
        "arrangeWindow" => {
            let h = i64_prop(msg, "hwnd", 0) as isize;
            let mode = str_prop(msg, "mode");
            if h != 0 { arrange_window(h, mode); }
            *env.needs_state_push = true;
        }
        "pairGroupWindows" => {
            let key = str_prop(msg, "key");
            if let Some(g) = env.state.dock_groups.iter().find(|g| g.group_key == key).cloned() {
                if g.windows.len() >= 2 {
                    arrange_window(g.windows[0].hwnd, "left");
                    arrange_window(g.windows[1].hwnd, "right");
                    env.post_toast(
                        "50/50 Yan Yana Dizildi",
                        &format!("{} pencereleri yan yana yerleştirildi.", g.display_name),
                        None,
                    );
                } else if let Some(w0) = g.windows.first() {
                    arrange_window(w0.hwnd, "center");
                }
            }
            *env.needs_state_push = true;
        }
        "arrangeActive" => {
            let mode = str_prop(msg, "mode");
            let target = env.state.last_external_hwnd;
            if target != 0 { arrange_window(target, mode); }
            *env.needs_state_push = true;
        }
        "applyWindowLayout" => {
            let preset = str_prop(msg, "preset");
            let wins: Vec<isize> = env.state.open_windows.iter().take(2).map(|w| w.hwnd).collect();
            if let Some(&h0) = wins.first() {
                match preset {
                    "split" | "sideBySide" => {
                        arrange_window(h0, "left");
                        if let Some(&h1) = wins.get(1) { arrange_window(h1, "right"); }
                    }
                    "focus" | "center" => arrange_window(h0, "center"),
                    _ => arrange_window(h0, "max"),
                }
            }
        }
        "toggleTopMost" => {
            let h = i64_prop(msg, "hwnd", env.state.last_external_hwnd as i64) as isize;
            if h != 0 {
                let on = toggle_always_on_top(h);
                env.post_toast("Üstte Tut", if on { "Pencere her zaman üstte sabitlendi." } else { "Üstte sabitleme kaldırıldı." }, None);
            }
            *env.needs_state_push = true;
        }
        "togglePin" => {
            let name = str_prop(msg, "name").trim();
            let raw_path = str_prop(msg, "path").trim();
            let aumid = str_prop(msg, "aumid").trim();
            let safe_path = if !aumid.is_empty()
                && (raw_path.is_empty() || is_windows_apps_sandbox_path(raw_path))
            {
                format!("appsfolder:{aumid}")
            } else if is_windows_apps_sandbox_path(raw_path) {
                env.state
                    .resolve_launch_fallback(raw_path, name, if aumid.is_empty() { None } else { Some(aumid) })
                    .unwrap_or_else(|| raw_path.to_string())
            } else if let Some(repaired) = repair_corrupted_windows_path(raw_path) {
                repaired
            } else {
                raw_path.to_string()
            };
            let path = safe_path.as_str();
            if !path.is_empty() {
                let norm = normalize_group_key(path, name);
                if let Some(pos) = env.state.pinned_shortcuts.iter().position(|p| {
                    p.executable_path.eq_ignore_ascii_case(path)
                        || p.executable_path.eq_ignore_ascii_case(raw_path)
                        || normalize_group_key(&p.executable_path, &p.display_name) == norm
                }) {
                    let removed = env.state.pinned_shortcuts.remove(pos);
                    env.post_toast("Sabitleme Kaldırıldı", &format!("{} Dock'tan kaldırıldı.", removed.display_name), None);
                } else {
                    let disp = if name.is_empty() { norm.clone() } else { name.to_string() };
                    let id = format!("pin-{:x}", env.now_ms ^ (env.state.pinned_shortcuts.len() as u64 * 97));
                    env.state.pinned_shortcuts.push(PinnedShortcut {
                        id,
                        display_name: disp.clone(),
                        executable_path: path.into(),
                        arguments: String::new(),
                        icon_key: None,
                    });
                    env.post_toast("Dock'a Sabitlendi", &format!("{disp} alt çubuğa eklendi."), None);
                }
                env.aurora_db.save_pinned_shortcuts(&env.state.pinned_shortcuts);
                update_stable_orders(
                    &env.state.open_windows,
                    &env.state.pinned_shortcuts,
                    &mut env.state.running_group_order,
                    &mut env.state.window_seen_order,
                );
                env.state.dock_groups = build_dock_groups_ordered(
                    &env.state.pinned_shortcuts,
                    &env.state.open_windows,
                    None,
                    &env.state.running_group_order,
                    &env.state.window_seen_order,
                );
                *env.needs_state_push = true;
            }
        }
        "reorderPin" => {
            let id_or_key = str_prop(msg, "id");
            let key_low = id_or_key.trim().to_ascii_lowercase();
            let target_idx = i64_prop(msg, "targetIndex", 0).max(0) as usize;
            if let Some(cur) = env.state.pinned_shortcuts.iter().position(|p| {
                p.id == id_or_key
                    || normalize_group_key(&p.executable_path, &p.display_name) == key_low
            }) {
                let item = env.state.pinned_shortcuts.remove(cur);
                let dst = target_idx.min(env.state.pinned_shortcuts.len());
                env.state.pinned_shortcuts.insert(dst, item);
                env.aurora_db.save_pinned_shortcuts(&env.state.pinned_shortcuts);
                env.state.dock_groups = build_dock_groups_ordered(
                    &env.state.pinned_shortcuts,
                    &env.state.open_windows,
                    None,
                    &env.state.running_group_order,
                    &env.state.window_seen_order,
                );
                *env.needs_state_push = true;
            }
        }
        "reorderDockApp" => {
            let key = str_prop(msg, "key").trim();
            let key_low = key.to_ascii_lowercase();
            let is_pinned = bool_prop(msg, "isPinned");
            let target_pinned = bool_prop(msg, "targetPinned");
            let target_idx = i64_prop(msg, "targetIndex", 0).max(0) as usize;
            let mut changed = false;

            if is_pinned {
                // Always keep pinned apps inside pinned zone so users never lose a pin accidentally
                if let Some(cur) = env.state.pinned_shortcuts.iter().position(|p| {
                    p.id == key
                        || normalize_group_key(&p.executable_path, &p.display_name) == key_low
                }) {
                    let item = env.state.pinned_shortcuts.remove(cur);
                    let dst = if target_pinned {
                        target_idx.min(env.state.pinned_shortcuts.len())
                    } else {
                        env.state.pinned_shortcuts.len()
                    };
                    env.state.pinned_shortcuts.insert(dst, item);
                    env.aurora_db.save_pinned_shortcuts(&env.state.pinned_shortcuts);
                    changed = true;
                }
            } else if target_pinned {
                // Dragging an unpinned running app into the pinned zone auto-pins it at that exact slot!
                if let Some(grp) = env
                    .state
                    .dock_groups
                    .iter()
                    .find(|g| g.group_key == key_low)
                    .cloned()
                {
                    let launch_path = if let Some(au) = grp.aumid.as_ref().filter(|a| !a.trim().is_empty()) {
                        if grp.executable_path.is_empty() || is_windows_apps_sandbox_path(&grp.executable_path) {
                            format!("appsfolder:{}", au.trim())
                        } else {
                            grp.executable_path.clone()
                        }
                    } else if is_windows_apps_sandbox_path(&grp.executable_path) {
                        env.state
                            .resolve_launch_fallback(&grp.executable_path, &grp.display_name, None)
                            .unwrap_or_else(|| grp.executable_path.clone())
                    } else if !grp.executable_path.is_empty() {
                        grp.executable_path.clone()
                    } else {
                        String::new()
                    };
                    if !launch_path.is_empty() {
                        let id = format!(
                            "pin-{:x}",
                            env.now_ms ^ (env.state.pinned_shortcuts.len() as u64 * 131 + 7)
                        );
                        let dst = target_idx.min(env.state.pinned_shortcuts.len());
                        env.state.pinned_shortcuts.insert(
                            dst,
                            PinnedShortcut {
                                id,
                                display_name: grp.display_name.clone(),
                                executable_path: launch_path,
                                arguments: String::new(),
                                icon_key: None,
                            },
                        );
                        env.state.running_group_order.retain(|k| k != &key_low);
                        env.aurora_db.save_pinned_shortcuts(&env.state.pinned_shortcuts);
                        env.post_toast(
                            "Dock'a Sabitlendi",
                            &format!("{} seçtiğiniz sıraya sabitlendi.", grp.display_name),
                            None,
                        );
                        changed = true;
                    }
                }
            } else if reorder_running_group(&mut env.state.running_group_order, &key_low, target_idx)
            {
                changed = true;
            }

            if changed {
                update_stable_orders(
                    &env.state.open_windows,
                    &env.state.pinned_shortcuts,
                    &mut env.state.running_group_order,
                    &mut env.state.window_seen_order,
                );
                env.state.dock_groups = build_dock_groups_ordered(
                    &env.state.pinned_shortcuts,
                    &env.state.open_windows,
                    None,
                    &env.state.running_group_order,
                    &env.state.window_seen_order,
                );
                *env.needs_state_push = true;
            }
        }
        "launchNewInstance" => {
            let key = str_prop(msg, "key");
            if let Some(g) = env.state.dock_groups.iter().find(|g| g.group_key == key).cloned() {
                launch_dock_group_reliably(env, &g);
            }
        }
        "closeAllGroupWindows" => {
            let key = str_prop(msg, "key");
            if let Some(g) = env.state.dock_groups.iter().find(|g| g.group_key == key) {
                for w in &g.windows {
                    close_window(w.hwnd);
                }
            }
            *env.needs_state_push = true;
        }
        "focusGroupForDrop" => {
            let key = str_prop(msg, "key");
            if let Some(g) = env.state.dock_groups.iter().find(|g| g.group_key == key) {
                if let Some(w) = g.windows.first() {
                    focus_window(w.hwnd);
                }
            }
        }
        "showDwmPreview" => {
            let hwnd = i64_prop(msg, "hwnd", 0) as isize;
            if hwnd != 0 && env.state.prefs.dwm_live_preview_enabled {
                if let Some(dw) = env.docks.iter().find(|d| d.monitor_id.eq_ignore_ascii_case(source_mon_id)) {
                    let vw = f64_prop(msg, "vw", dw.bounds_px.width as f64).max(1.0);
                    let vh = f64_prop(msg, "vh", dw.bounds_px.height as f64).max(1.0);
                    let sx = dw.bounds_px.width as f64 / vw;
                    let sy = dw.bounds_px.height as f64 / vh;
                    let hl = f64_prop(msg, "hubLeft", 0.0);
                    let ht = f64_prop(msg, "hubTop", 0.0);
                    let hw = f64_prop(msg, "hubWidth", 260.0);
                    let px_w = (260.0 * dw.dpi_scale as f64).round() as i32;
                    let px_h = (154.0 * dw.dpi_scale as f64).round() as i32;
                    let cx = dw.bounds_px.x + ((hl + hw * 0.5) * sx).round() as i32;
                    let py = (dw.bounds_px.y + (ht * sy).round() as i32 - px_h - 10).max(12);
                    if let Some(prev) = env.dwm_preview.as_mut() {
                        prev.show(hwnd, PhysicalRect::new(cx - px_w / 2, py, px_w, px_h));
                    }
                }
            }
        }
        "hideDwmPreview" | "requestWindowPreview" => {
            if action == "hideDwmPreview" {
                if let Some(prev) = env.dwm_preview.as_mut() {
                    prev.hide();
                }
            }
        }

        // ── Custom Dock Folders (+ Klasör Ekle & Folder Stack) ──
        "pickDockFolder" => {
            let _ = env.bg_tx.try_send(BgCommand::PickDockFolder);
        }
        "removeDockFolder" => {
            let path = str_prop(msg, "path");
            if let Some(list) = env.state.prefs.dock_folder_paths.as_mut() {
                list.retain(|p| !p.eq_ignore_ascii_case(path));
                env.save_prefs();
                *env.needs_state_push = true;
            }
        }
        "requestFolderStack" => {
            let path = str_prop(msg, "path");
            if !path.is_empty() {
                let _ = env.bg_tx.try_send(BgCommand::ReadFolderStack {
                    monitor_id: source_mon_id.to_string(),
                    path: path.to_string(),
                });
            }
        }

        // ── OmniSearch, Launch & Local File Search ──
        "launchPath" => {
            let p = str_prop(msg, "path");
            if !p.is_empty() {
                *env.state.frecency.entry(p.to_string()).or_insert(0) += 1;
                env.aux_store.save_frecency(&env.state.frecency);
                let ok = if launch_target_with_args(p, "").is_ok() {
                    true
                } else if let Some(fallback) = env.state.resolve_launch_fallback(p, "", None) {
                    launch_target_with_args(&fallback, "").is_ok()
                } else {
                    false
                };
                if !ok {
                    env.logger.warn("LAUNCH", "LAUNCH_PATH_FAILED", &format!("Failed path='{p}'"));
                    env.post_toast("Hedef Açılamadı", "Seçilen uygulama veya dosya yolu bulunamadı.", None);
                }
                let j = json!({ "type": "frecency", "frecency": env.state.frecency }).to_string();
                env.crown.post_json(&j);
            }
        }
        "openUri" => {
            let uri = str_prop(msg, "uri");
            if !uri.is_empty() {
                let _ = launch_target(uri);
            }
        }
        "searchLocalFiles" => {
            let q = str_prop(msg, "query").to_string();
            let seq = i64_prop(msg, "seq", 0).max(0) as u64;
            let _ = env.bg_tx.try_send(BgCommand::SearchFiles { query: q, seq });
        }

        // ── Notifications ──
        "markNotificationsRead" => {
            env.state.unread_notif_count = 0;
            *env.needs_state_push = true;
        }
        "activateNotification" => {
            let id_raw = str_prop(msg, "id");
            let nid = id_raw.strip_prefix("sys-").unwrap_or(id_raw).parse::<i64>().unwrap_or(-1);
            if let Some(n) = env.state.notifications.iter().find(|n| n.notification_id == nid).cloned() {
                let _ = activate_notification(&n, &env.state.open_windows);
            }
            env.crown.release_interactive();
            *env.needs_state_push = true;
        }
        "dismissNotification" => {
            let id_raw = str_prop(msg, "id");
            if let Ok(nid) = id_raw.strip_prefix("sys-").unwrap_or(id_raw).parse::<i64>() {
                env.state.dismissed_sys_notif_ids.insert(nid);
                env.state.notifications.retain(|n| n.notification_id != nid);
                *env.needs_state_push = true;
            }
        }
        "clearNotifications" => {
            for n in &env.state.notifications {
                env.state.dismissed_sys_notif_ids.insert(n.notification_id);
            }
            env.state.notifications.clear();
            env.state.unread_notif_count = 0;
            *env.needs_state_push = true;
        }

        // ── Dock Appearance, Mode, WorkArea & Shell Preferences ──
        "toggleHideNativeTaskbar" => {
            env.state.prefs.hide_native_taskbar_while_active = !env.state.prefs.hide_native_taskbar_while_active;
            env.save_prefs();
            *env.needs_taskbar_sync = true;
            *env.needs_bounds_sync = true;
            *env.needs_state_push = true;
        }
        "toggleDockPinMode" => {
            let pinned = env.state.prefs.effective_reserve_dock_work_area();
            if pinned {
                env.state.prefs.reserve_dock_work_area = Some(false);
                env.state.prefs.dock_visibility_mode = Some("SmartDodge".into());
                env.state.prefs.dock_auto_hide = false;
                env.post_toast("Yüzen Dock", "Akıllı Kaçış (Intellihide) etkin.", None);
            } else {
                env.state.prefs.reserve_dock_work_area = Some(true);
                env.state.prefs.dock_visibility_mode = Some("AlwaysVisible".into());
                env.state.prefs.dock_auto_hide = false;
                env.post_toast("Sabit Çubuk", "Çalışma alanı korumalı sabit bar etkin.", None);
            }
            env.save_prefs();
            *env.needs_taskbar_sync = true;
            *env.needs_bounds_sync = true;
            *env.needs_state_push = true;
        }
        "toggleReserveDockWorkArea" => {
            let next = !env.state.prefs.effective_reserve_pref();
            env.state.prefs.reserve_dock_work_area = Some(next);
            env.save_prefs();
            *env.needs_taskbar_sync = true;
            *env.needs_bounds_sync = true;
            *env.needs_state_push = true;
        }
        "toggleDockAutoHide" => {
            let next = env.state.prefs.effective_dock_mode() != "AutoHide";
            env.state.prefs.dock_auto_hide = next;
            env.state.prefs.dock_visibility_mode = Some(if next { "AutoHide" } else { "AlwaysVisible" }.into());
            env.state.prefs.reserve_dock_work_area = Some(!next);
            env.save_prefs();
            *env.needs_taskbar_sync = true;
            *env.needs_state_push = true;
        }
        "setDockAutoHideMode" => {
            env.state.prefs.dock_visibility_mode = Some("AutoHide".into());
            env.state.prefs.dock_auto_hide = true;
            env.state.prefs.reserve_dock_work_area = Some(false);
            env.save_prefs();
            *env.needs_taskbar_sync = true;
            *env.needs_state_push = true;
        }
        "setDockMode" => {
            let mode = match str_prop(msg, "mode") {
                "SmartDodge" => "SmartDodge",
                "AutoHide" => "AutoHide",
                _ => "AlwaysVisible",
            };
            env.state.prefs.dock_visibility_mode = Some(mode.into());
            env.state.prefs.dock_auto_hide = mode == "AutoHide";
            env.state.prefs.reserve_dock_work_area = Some(mode == "AlwaysVisible");
            env.save_prefs();
            *env.needs_taskbar_sync = true;
            *env.needs_bounds_sync = true;
            *env.needs_state_push = true;
        }
        "cycleDockVisibilityMode" => {
            let next = match env.state.prefs.effective_dock_mode() {
                "AlwaysVisible" => "SmartDodge",
                "SmartDodge" => "AutoHide",
                _ => "AlwaysVisible",
            };
            env.state.prefs.dock_visibility_mode = Some(next.into());
            env.state.prefs.dock_auto_hide = next == "AutoHide";
            env.state.prefs.reserve_dock_work_area = Some(next == "AlwaysVisible");
            env.save_prefs();
            *env.needs_taskbar_sync = true;
            *env.needs_bounds_sync = true;
            *env.needs_state_push = true;
        }
        "toggleDockBarLayout" => {
            env.state.prefs.dock_bar_layout = if env.state.prefs.effective_dock_bar_layout() == "Split3Zone" {
                "Centered".into()
            } else {
                "Split3Zone".into()
            };
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "setDockDensity" => {
            env.state.prefs.dock_density = str_prop(msg, "density").into();
            env.save_prefs();
            *env.needs_taskbar_sync = true;
            *env.needs_bounds_sync = true;
            *env.needs_state_push = true;
        }
        "setDockEdge" => {
            env.state.prefs.dock_edge = str_prop(msg, "edge").into();
            env.save_prefs();
            *env.needs_taskbar_sync = true;
            *env.needs_bounds_sync = true;
            *env.needs_state_push = true;
        }
        "setMagnification" => {
            env.state.prefs.dock_magnification_percent = i64_prop(msg, "value", 40).clamp(0, 80) as i32;
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "resetDockTransform" => {
            env.state.prefs.dock_scale_percent = 100;
            env.state.prefs.dock_offset_percent = 0;
            env.state.prefs.dock_edge = "Bottom".into();
            env.save_prefs();
            *env.needs_taskbar_sync = true;
            *env.needs_bounds_sync = true;
            *env.needs_state_push = true;
        }
        "togglePerMonitorFilter" => {
            env.state.prefs.per_monitor_window_filter = !env.state.prefs.per_monitor_window_filter;
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "toggleSmartTidy" => {
            env.state.prefs.smart_tidy_enabled = !env.state.prefs.smart_tidy_enabled;
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "toggleCrownPinned" => {
            env.state.prefs.crown_pinned = !env.state.prefs.crown_pinned;
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "toggleDwmLivePreview" => {
            env.state.prefs.dwm_live_preview_enabled = !env.state.prefs.dwm_live_preview_enabled;
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "toggleSysNotifBridge" => {
            let next = env.state.prefs.effective_notification_mode() != "aurora-mirror";
            env.state.prefs.system_notification_bridge_enabled = next;
            env.state.prefs.notification_mode = if next {
                "aurora-mirror".into()
            } else {
                env.state.unread_notif_count = 0;
                "windows-native".into()
            };
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "toggleWinKeyOmni" => {
            env.state.prefs.win_key_opens_omni_search = !env.state.prefs.win_key_opens_omni_search;
            env.save_prefs();
            *env.needs_hotkey_sync = true;
            *env.needs_state_push = true;
        }
        "cycleOmniHotkey" => {
            env.state.prefs.omni_hotkey_preset = match env.state.prefs.omni_hotkey_preset.as_str() {
                "Alt+Space" => "Ctrl+Space".into(),
                "Ctrl+Space" => "Ctrl+Shift+Space".into(),
                _ => "Alt+Space".into(),
            };
            env.save_prefs();
            *env.needs_hotkey_sync = true;
            *env.needs_state_push = true;
        }
        "toggleHardwareOsd" => {
            env.state.prefs.hardware_osd_enabled = !env.state.prefs.hardware_osd_enabled;
            env.save_prefs();
            *env.needs_hotkey_sync = true;
            *env.needs_state_push = true;
        }
        "toggleAltDrag" => {
            env.state.prefs.alt_drag_enabled = !env.state.prefs.alt_drag_enabled;
            set_alt_drag_enabled(env.state.prefs.alt_drag_enabled);
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "toggleClockSeconds" => {
            env.state.prefs.show_clock_seconds = !env.state.prefs.show_clock_seconds;
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "toggleSnapZones" => {
            env.state.prefs.snap_zones_enabled = !env.state.prefs.snap_zones_enabled;
            set_snap_zones_enabled(env.state.prefs.snap_zones_enabled);
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "setSnapZonePreset" => {
            env.state.prefs.snap_zone_preset = str_prop(msg, "preset").into();
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "toggleClipboardAutoPaste" => {
            env.state.prefs.clipboard_auto_paste_enabled = !env.state.prefs.clipboard_auto_paste_enabled;
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "toggleAutoCheckUpdates" => {
            env.state.prefs.auto_check_updates_enabled = !env.state.prefs.auto_check_updates_enabled;
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "toggleStartup" => {
            let next = !is_startup_enabled();
            let _ = set_startup_enabled(next);
            env.post_toast("Başlangıç", if next { "Windows açılışında otomatik başlayacak." } else { "Otomatik başlatma kapatıldı." }, None);
            *env.needs_state_push = true;
        }
        "toggleWeatherNet" => {
            env.state.weather_net_enabled = !env.state.weather_net_enabled;
            if env.state.weather_net_enabled {
                let _ = env.bg_tx.try_send(BgCommand::RefreshWeather);
            }
            *env.needs_state_push = true;
        }

        // ── Audio, Brightness, Media, Hardware & System Controls ──
        "setVolume" => {
            let v = i64_prop(msg, "value", 50).clamp(0, 100) as u8;
            env.audio.set_master_volume(v);
            env.state.audio.master_volume_percent = v;
            env.state.audio.is_master_muted = false;
            env.post_osd("volume", "Ses", &format!("%{v}"), Some(v), true, 1400);
            *env.needs_state_push = true;
        }
        "adjustVolumeDelta" => {
            let d = i64_prop(msg, "delta", 2);
            let next = compute_next_volume_step(env.state.audio.master_volume_percent, d > 0);
            env.audio.set_master_volume(next);
            env.state.audio.master_volume_percent = next;
            env.state.audio.is_master_muted = false;
            env.post_osd("volume", "Ses", &format!("%{next}"), Some(next), true, 1400);
            *env.needs_state_push = true;
        }
        "toggleMute" => {
            let muted = env.audio.toggle_master_mute();
            env.state.audio.is_master_muted = muted;
            let pct = env.state.audio.master_volume_percent;
            let vol_str = format!("%{pct}");
            env.post_osd(
                if muted { "mute" } else { "volume" },
                "Ses",
                if muted { "Sessiz" } else { &vol_str },
                Some(if muted { 0 } else { pct }),
                !muted,
                1400,
            );
            *env.needs_state_push = true;
        }
        "toggleMicMute" => {
            if let Some(muted) = env.audio.toggle_mic_mute() {
                env.state.audio.is_mic_muted = muted;
                env.post_osd("mic", "Mikrofon", if muted { "KAPALI" } else { "AÇIK" }, None, !muted, 1400);
                *env.needs_state_push = true;
            }
        }
        "setAudioOutputDevice" => {
            let id = str_prop(msg, "id");
            if env.audio.set_default_output_device(id) {
                env.state.audio = env.audio.get_snapshot();
                env.post_toast("Ses Çıkışı", &env.state.audio.default_output_name.clone(), None);
                *env.needs_state_push = true;
            }
        }
        "setAppVolume" => {
            let pid = i64_prop(msg, "pid", 0).max(0) as u32;
            let val = i64_prop(msg, "value", 50).clamp(0, 100) as u8;
            env.audio.set_app_volume(pid, val);
            if let Some(s) = env.state.audio.app_sessions.iter_mut().find(|a| a.process_id == pid) {
                s.volume_percent = val;
                if val > 0 {
                    s.is_muted = false;
                }
            }
            *env.needs_state_push = true;
        }
        "toggleAppMute" => {
            let req_pid = i64_prop(msg, "pid", 0).max(0) as u32;
            let gkey = str_prop(msg, "groupKey").trim().to_ascii_lowercase();
            let resolved_pid = if req_pid > 0 {
                Some(req_pid)
            } else {
                env.state
                    .audio
                    .app_sessions
                    .iter()
                    .find(|s| normalize_group_key(&s.process_name, "") == gkey)
                    .map(|s| s.process_id)
            };
            if let Some(pid) = resolved_pid {
                if let Some(muted) = env.audio.toggle_app_mute(pid) {
                    let mut app_label = "Uygulama Sesi".to_string();
                    let mut vol_pct = 100u8;
                    if let Some(s) = env.state.audio.app_sessions.iter_mut().find(|a| a.process_id == pid) {
                        s.is_muted = muted;
                        app_label = s.display_name.clone();
                        vol_pct = s.volume_percent;
                    }
                    let val_str = if muted { "Sessiz".into() } else { format!("%{vol_pct}") };
                    env.post_osd(
                        if muted { "mute" } else { "volume" },
                        &app_label,
                        &val_str,
                        Some(if muted { 0 } else { vol_pct }),
                        !muted,
                        1300,
                    );
                    *env.needs_state_push = true;
                }
            }
        }
        "adjustAppVolumeDelta" => {
            let req_pid = i64_prop(msg, "pid", 0).max(0) as u32;
            let gkey = str_prop(msg, "groupKey").trim().to_ascii_lowercase();
            let delta = i64_prop(msg, "delta", 5).clamp(-25, 25) as i8;
            let resolved_pid = if req_pid > 0 {
                Some(req_pid)
            } else {
                env.state
                    .audio
                    .app_sessions
                    .iter()
                    .find(|s| normalize_group_key(&s.process_name, "") == gkey)
                    .map(|s| s.process_id)
            };
            if let Some(pid) = resolved_pid {
                if let Some((next_vol, muted)) = env.audio.adjust_app_volume(pid, delta) {
                    let mut app_label = "Uygulama Sesi".to_string();
                    if let Some(s) = env.state.audio.app_sessions.iter_mut().find(|a| a.process_id == pid) {
                        s.volume_percent = next_vol;
                        s.is_muted = muted;
                        app_label = s.display_name.clone();
                    }
                    let val_str = format!("%{next_vol}");
                    env.post_osd("volume", &app_label, &val_str, Some(next_vol), !muted, 1200);
                    *env.needs_state_push = true;
                }
            }
        }
        "cycleAudioOutput" => {
            if let Some(dev_name) = env.audio.cycle_default_output_device() {
                env.state.audio = env.audio.get_snapshot();
                env.post_toast("Ses Çıkışı Değişti", &dev_name, None);
                *env.needs_state_push = true;
            }
        }
        "adjustBrightnessDelta" => {
            let delta = i64_prop(msg, "delta", 5).clamp(-30, 30);
            if let Some(m) = env.state.monitors_brightness.first_mut() {
                let next = (m.brightness_percent as i64 + delta).clamp(5, 100) as u8;
                let id = m.id.clone();
                m.brightness_percent = next;
                env.brightness.set_brightness(&id, next);
                env.post_osd("brightness", "Parlaklık", &format!("%{next}"), Some(next), true, 1200);
                *env.needs_state_push = true;
            }
        }
        "setAppAudioOutput" => {
            let dev_id = str_prop(msg, "deviceId");
            if !dev_id.is_empty() && env.audio.set_default_output_device(dev_id) {
                env.state.audio = env.audio.get_snapshot();
                *env.needs_state_push = true;
            }
        }
        "setBrightness" => {
            let val = i64_prop(msg, "value", 75).clamp(0, 100) as u8;
            if let Some(first_id) = env.state.monitors_brightness.first().map(|m| m.id.clone()) {
                env.brightness.set_brightness(&first_id, val);
                if let Some(m) = env.state.monitors_brightness.first_mut() {
                    m.brightness_percent = val;
                }
            }
            env.post_osd("brightness", "Parlaklık", &format!("%{val}"), Some(val), true, 1400);
            *env.needs_state_push = true;
        }
        "setMonitorBrightness" => {
            let id = str_prop(msg, "id");
            let val = i64_prop(msg, "value", 75).clamp(0, 100) as u8;
            env.brightness.set_brightness(id, val);
            if let Some(m) = env.state.monitors_brightness.iter_mut().find(|m| m.id == id) {
                m.brightness_percent = val;
            }
            env.post_osd("brightness", "Parlaklık", &format!("%{val}"), Some(val), true, 1400);
            *env.needs_state_push = true;
        }
        "refreshMonitorsBrightness" | "refreshBluetoothBatteries" => {
            let _ = env.bg_tx.try_send(BgCommand::RefreshSlowHardware);
        }
        "mediaCmd" => {
            let cmd = str_prop(msg, "cmd");
            let norm = match cmd {
                "prev" | "previous" => "prev",
                "next" => "next",
                _ => "play_pause",
            };
            send_media_command(norm);
            if norm == "play_pause" {
                env.state.media.is_playing = !env.state.media.is_playing;
                *env.needs_state_push = true;
            }
        }
        "toggleWifi" => {
            let _ = launch_target("ms-settings:network-wifi");
            env.state.wifi_enabled = !env.state.wifi_enabled;
            *env.needs_state_push = true;
        }
        "toggleBluetooth" => {
            let _ = launch_target("ms-settings:bluetooth");
            env.state.bluetooth_enabled = !env.state.bluetooth_enabled;
            *env.needs_state_push = true;
        }
        "toggleQuietHours" => {
            env.state.focus_mode_enabled = !env.state.focus_mode_enabled;
            env.save_user_prefs();
            env.post_toast("Odak / Sessiz", if env.state.focus_mode_enabled { "Bildirimler susturuldu." } else { "Bildirimler açıldı." }, None);
            *env.needs_state_push = true;
        }

        // ── Themes, Colors & Profiles ──
        "applyTheme" => {
            let id = str_prop(msg, "id");
            env.state.theme_id = theme_by_id(id).id;
            env.save_user_prefs();
            *env.needs_state_push = true;
        }
        "updateThemeColors" => {
            let tid = str_prop(msg, "themeId");
            let key = if tid.is_empty() { env.state.theme_id.clone() } else { tid.to_string() };
            let map = env.state.prefs.theme_customizations.get_or_insert_with(HashMap::new);
            let entry: &mut ThemeColorCustomization = map.entry(key).or_default();
            if let Some(s) = msg.get("accentPrimary").and_then(|v| v.as_str()) { entry.accent_primary = Some(s.into()); }
            if let Some(s) = msg.get("accentSecondary").and_then(|v| v.as_str()) { entry.accent_secondary = Some(s.into()); }
            if let Some(s) = msg.get("surface1").and_then(|v| v.as_str()) { entry.surface1 = Some(s.into()); }
            if let Some(s) = msg.get("surface2").and_then(|v| v.as_str()) { entry.surface2 = Some(s.into()); }
            if let Some(s) = msg.get("textPrimary").and_then(|v| v.as_str()) { entry.text_primary = Some(s.into()); }
            if let Some(op) = msg.get("surfaceOpacity").and_then(|v| v.as_f64()) {
                entry.surface_opacity = Some(op.clamp(0.55, 1.0));
            }
            env.save_prefs();
            *env.needs_state_push = true;
        }
        "resetThemeColors" => {
            let tid = str_prop(msg, "themeId");
            let key = if tid.is_empty() { env.state.theme_id.clone() } else { tid.to_string() };
            if let Some(map) = env.state.prefs.theme_customizations.as_mut() {
                map.remove(&key);
            }
            env.save_prefs();
            env.post_toast("Tema Sıfırlandı", "Varsayılan 2026 renk paletine dönüldü.", None);
            *env.needs_state_push = true;
        }
        "switchProfile" => {
            let id = str_prop(msg, "id");
            if let Some(p) = env.state.profiles.iter().find(|p| p.id == id).cloned() {
                env.state.active_profile_id = p.id;
                env.state.theme_id = theme_by_id(&p.theme_id).id;
                env.state.focus_mode_enabled = p.focus_mode_enabled;
                env.save_user_prefs();
                env.post_toast("Profil Aktif", &p.name, None);
                *env.needs_state_push = true;
            }
        }
        "cycleProfile" => {
            if !env.state.profiles.is_empty() {
                let idx = env.state.profiles.iter().position(|p| p.id == env.state.active_profile_id).unwrap_or(0);
                let next = env.state.profiles[(idx + 1) % env.state.profiles.len()].clone();
                env.state.active_profile_id = next.id;
                env.state.theme_id = theme_by_id(&next.theme_id).id;
                env.state.focus_mode_enabled = next.focus_mode_enabled;
                env.save_user_prefs();
                env.post_toast("Profil Değişti", &next.name, None);
                *env.needs_state_push = true;
            }
        }
        "exportTheme" => {
            let (eff, _, _) = env.state.effective_theme();
            let dest = user_known_folder("Desktop").join(format!("Aurora-Tema-{}.json", eff.id));
            if let Ok(text) = serde_json::to_string_pretty(&eff) {
                if std::fs::write(&dest, text).is_ok() {
                    env.post_toast("Tema Dışa Aktarıldı", "Masaüstüne JSON olarak kaydedildi.", None);
                }
            }
        }

        // ── Productivity: Clipboard, Scratchpad, Agenda, Shelf, QR, OCR, Eyedropper, GIF ──
        "saveScratchpad" => {
            env.state.scratchpad_text = str_prop(msg, "text").to_string();
            env.aux_store.save_scratchpad(&env.state.scratchpad_text);
        }
        "saveAgendaNote" => {
            let date = str_prop(msg, "date");
            let note = str_prop(msg, "note");
            if !date.is_empty() {
                if note.trim().is_empty() {
                    env.state.agenda_notes.remove(date);
                } else {
                    env.state.agenda_notes.insert(date.to_string(), note.to_string());
                }
                env.aux_store.save_agenda(&env.state.agenda_notes);
                *env.needs_state_push = true;
            }
        }
        "copyText" => {
            let txt = str_prop(msg, "text");
            if !txt.is_empty() {
                set_clipboard_text(txt);
                env.post_toast("Kopyalandı", "Panoya aktarıldı ✓", None);
            }
        }
        "copyClipById" => {
            let id = i64_prop(msg, "id", -1);
            if let Some(item) = env.state.clipboard_items.iter().find(|c| c.id == id).cloned() {
                use_clipboard_item(env, &item);
            }
        }
        "copyClipIndex" => {
            let idx = i64_prop(msg, "index", 0).max(0) as usize;
            if let Some(item) = env.state.clipboard_items.get(idx).cloned() {
                use_clipboard_item(env, &item);
            }
        }
        "togglePinClip" => {
            let id = i64_prop(msg, "id", -1);
            env.clip_db.toggle_pin(id);
            env.state.clipboard_items = env.clip_db.list("all", "", 60);
            *env.needs_state_push = true;
        }
        "deleteClip" => {
            let id = i64_prop(msg, "id", -1);
            env.clip_db.delete(id);
            env.state.clipboard_items = env.clip_db.list("all", "", 60);
            *env.needs_state_push = true;
        }
        "clearUnpinnedClips" => {
            env.clip_db.clear_unpinned();
            env.state.clipboard_items = env.clip_db.list("all", "", 60);
            env.post_toast("Pano Temizlendi", "Sabitlenmemiş kayıtlar silindi.", None);
            *env.needs_state_push = true;
        }
        "pickShelfFile" => {
            let _ = env.bg_tx.try_send(BgCommand::PickShelfFile { share_qr_after: false });
        }
        "pickAndShareQr" => {
            let _ = env.bg_tx.try_send(BgCommand::PickShelfFile { share_qr_after: true });
        }
        "openShelfFile" => {
            let idx = i64_prop(msg, "index", 0).max(0) as usize;
            if let Some(p) = env.state.drop_shelf_paths.get(idx) {
                let _ = launch_target(p);
            }
        }
        "revealShelfFile" => {
            let idx = i64_prop(msg, "index", 0).max(0) as usize;
            if let Some(p) = env.state.drop_shelf_paths.get(idx) {
                let _ = launch_target(&format!("reveal:{p}"));
            }
        }
        "removeShelfFile" => {
            let idx = i64_prop(msg, "index", 0).max(0) as usize;
            if idx < env.state.drop_shelf_paths.len() {
                env.state.drop_shelf_paths.remove(idx);
                env.aux_store.save_drop_shelf(&env.state.drop_shelf_paths);
                *env.needs_state_push = true;
            }
        }
        "clearShelfFiles" => {
            env.state.drop_shelf_paths.clear();
            env.aux_store.save_drop_shelf(&[]);
            env.qr_server.stop();
            env.state.qr_share = None;
            env.state.qr_svg.clear();
            *env.needs_state_push = true;
        }
        "convertShelfFile" => {
            let idx = i64_prop(msg, "index", 0).max(0) as usize;
            if let Some(p) = env.state.drop_shelf_paths.get(idx).cloned() {
                let ext = Path::new(&p).extension().and_then(|e| e.to_str()).unwrap_or("").to_ascii_lowercase();
                if matches!(ext.as_str(), "png" | "jpg" | "jpeg" | "bmp") {
                    let target = if ext == "png" { "jpg" } else { "png" };
                    match convert_image_file(&p, target) {
                        Ok(out) => {
                            env.post_toast("Dönüştürüldü ✓", &out, None);
                            let _ = launch_target(&format!("reveal:{out}"));
                        }
                        Err(e) => env.post_toast("Dönüştürme Hatası", &e, None),
                    }
                } else {
                    let desktop = user_known_folder("Desktop");
                    let out = desktop.join(format!("Aurora-Raf-{}.zip", chrono::Local::now().format("%H%M%S")));
                    if let Ok(sz) = zip_shelf_files(&[p], &out) {
                        env.post_toast("ZIP Oluşturuldu", &format!("Masaüstü ({})", format_size(sz)), None);
                    }
                }
            }
        }
        "shareShelfFileQr" => {
            let idx = i64_prop(msg, "index", 0).max(0) as usize;
            if let Some(p) = env.state.drop_shelf_paths.get(idx).cloned() {
                start_qr_for_path(env, &p);
                *env.needs_state_push = true;
            }
        }
        "stopQrShare" => {
            env.qr_server.stop();
            env.state.qr_share = None;
            env.state.qr_svg.clear();
            *env.needs_state_push = true;
        }
        "copyQrUrl" => {
            if let Some(q) = &env.state.qr_share {
                set_clipboard_text(&q.url);
                env.post_toast("QR Bağlantısı Kopyalandı", &q.url.clone(), None);
            }
        }
        "startScreenOcr" => {
            env.crown.release_interactive();
            env.state.active_snip_mode = Some(SnipMode::Ocr);
        }
        "startColorPicker" => {
            env.crown.release_interactive();
            env.state.active_snip_mode = Some(SnipMode::Eyedropper);
        }
        "startGifRecording" => {
            env.crown.release_interactive();
            env.state.active_snip_mode = Some(SnipMode::GifRegion);
        }
        "stopGifRecording" => {
            if let Some(flag) = env.gif_stop_flag.take() {
                flag.store(true, Ordering::Relaxed);
            }
            env.state.gif_recording = false;
            env.crown.post_json(r#"{"type":"gifRecState","recording":false,"elapsed":0}"#);
            *env.needs_state_push = true;
        }

        // ── Awake, Pomodoro, Virtual Desktop, Tray, Telemetry & Power ──
        "setAwakeMode" => {
            let m = str_prop(msg, "mode");
            apply_awake_mode(env, m);
            *env.needs_state_push = true;
        }
        "cycleAwake" => {
            let next = match env.state.awake_mode.as_str() {
                "off" => "indefinite",
                "indefinite" => "30m",
                "30m" => "60m",
                _ => "off",
            };
            apply_awake_mode(env, next);
            *env.needs_state_push = true;
        }
        "startFocusMinutes" => {
            let mins = i64_prop(msg, "minutes", 25).clamp(1, 120) as u32;
            env.state.focus_seconds = mins * 60;
            env.state.focus_running = true;
            *env.needs_state_push = true;
        }
        "toggleFocus" => {
            env.state.focus_running = !env.state.focus_running;
            if env.state.focus_seconds == 0 {
                env.state.focus_seconds = 25 * 60;
            }
            *env.needs_state_push = true;
        }
        "cycleKeyboard" => {
            let (c, n) = cycle_keyboard_layout(env.state.last_external_hwnd);
            env.state.keyboard_code = c;
            env.state.keyboard_name = n;
            *env.needs_state_push = true;
        }
        "switchVirtualDesktopDelta" => {
            let d = i64_prop(msg, "delta", 1) as i32;
            let total = env.state.virtual_desktop_count.max(2);
            env.state.virtual_desktop_index = ((env.state.virtual_desktop_index - 1 + d).rem_euclid(total)) + 1;
            send_virtual_desktop_step(d >= 0);
            *env.needs_state_push = true;
        }
        "switchVirtualDesktopIndex" => {
            let target = i64_prop(msg, "index", 1).max(1) as i32;
            let diff = target - env.state.virtual_desktop_index;
            for _ in 0..diff.abs().min(8) {
                send_virtual_desktop_step(diff > 0);
            }
            env.state.virtual_desktop_index = target;
            *env.needs_state_push = true;
        }
        "createVirtualDesktop" => {
            create_virtual_desktop();
            env.state.virtual_desktop_count = (env.state.virtual_desktop_count + 1).min(8);
            env.state.virtual_desktop_index = env.state.virtual_desktop_count;
            *env.needs_state_push = true;
        }
        "openTaskView" => open_task_view(),
        "openRecycleBin" => open_recycle_bin(),
        "showDesktop" => send_win_d(),
        "openTrayApp" => {
            let idx = i64_prop(msg, "index", 0).max(0) as usize;
            if let Some(app) = env.state.tray_apps.get(idx).cloned() {
                activate_tray_app(env.state, &app);
            }
        }
        "openTrayAppByKey" => {
            let key = str_prop(msg, "key");
            if let Some(app) = env.state.tray_apps.iter().find(|a| a.id == key).cloned() {
                activate_tray_app(env.state, &app);
            }
        }
        "quitTrayApp" => {
            let idx = i64_prop(msg, "index", 0).max(0) as usize;
            if let Some(app) = env.state.tray_apps.get(idx).cloned() {
                if terminate_user_process_safe(&app.name, app.process_id) {
                    env.state.tray_apps.retain(|a| a.process_id != app.process_id);
                    env.post_toast("Kapatıldı", &format!("{} güvenli şekilde sonlandırıldı.", app.name), None);
                    *env.needs_state_push = true;
                }
            }
        }
        "peekNativeTray" => {
            env.appbar.restore_native_taskbar();
            *env.peek_tray_until_ms = env.now_ms + 10_000;
            env.post_toast("Klasik Görev Çubuğu", "10 saniyeliğine gösteriliyor...", None);
            *env.needs_state_push = true;
        }
        "killTopProcess" => {
            let pid = i64_prop(msg, "pid", 0).max(0) as u32;
            let name = str_prop(msg, "name");
            if terminate_user_process_safe(name, pid) {
                env.state.metrics.top_processes.retain(|p| p.pid != pid);
                env.post_toast("İşlem Sonlandırıldı", &format!("{name} kapatıldı."), None);
                *env.needs_state_push = true;
            } else {
                env.post_toast("Korumalı Süreç", "Kritik Windows süreci korunuyor.", None);
            }
        }
        "lockPc" => trigger_lock_workstation(),
        "sleepPc" => trigger_sleep(),
        "restartPc" => trigger_power_command(true),
        "shutdownPc" => trigger_power_command(false),

        // ── Backup, Updates & Clean Exit ──
        "exportBackup" => {
            let bundle = AuroraBackupBundle {
                exported_at_utc: chrono::Utc::now().to_rfc3339(),
                aurora_version: env.state.version.clone(),
                active_theme_id: env.state.theme_id.clone(),
                active_profile_id: env.state.active_profile_id.clone(),
                shell_preferences: env.state.prefs.clone(),
                pinned_shortcuts: env.state.pinned_shortcuts.clone(),
                agenda_notes: env.state.agenda_notes.clone(),
                scratchpad_text: env.state.scratchpad_text.clone(),
                widgets: env.state.widgets.clone(),
                dock_widgets: env.state.dock_widgets.clone(),
            };
            let dest = user_known_folder("Desktop").join("ProjectAurora-Yedek.json");
            if bundle.export_to_file(&dest).is_ok() {
                env.post_toast("Yedek Kaydedildi", "Masaüstü\\ProjectAurora-Yedek.json", None);
            }
        }
        "importBackup" => {
            let src = user_known_folder("Desktop").join("ProjectAurora-Yedek.json");
            if let Some(bundle) = AuroraBackupBundle::import_from_file(&src) {
                env.state.prefs = bundle.shell_preferences;
                env.state.theme_id = theme_by_id(&bundle.active_theme_id).id;
                env.state.active_profile_id = bundle.active_profile_id;
                env.state.pinned_shortcuts = bundle.pinned_shortcuts;
                env.state.agenda_notes = bundle.agenda_notes;
                env.state.scratchpad_text = bundle.scratchpad_text;
                if !bundle.widgets.is_empty() {
                    env.state.widgets = bundle.widgets;
                    if !bundle.dock_widgets.is_empty() {
                        env.state.dock_widgets = bundle.dock_widgets;
                    }
                    env.save_widgets();
                    *env.needs_widgets_sync = true;
                } else if !bundle.dock_widgets.is_empty() {
                    env.state.dock_widgets = bundle.dock_widgets;
                    env.save_widgets();
                }
                env.save_prefs();
                env.save_user_prefs();
                env.aurora_db.save_pinned_shortcuts(&env.state.pinned_shortcuts);
                env.aux_store.save_agenda(&env.state.agenda_notes);
                env.aux_store.save_scratchpad(&env.state.scratchpad_text);
                env.post_toast("Yedek Geri Yüklendi", "Tüm ayarlarınız aktarıldı ✓", None);
                *env.needs_taskbar_sync = true;
                *env.needs_bounds_sync = true;
                *env.needs_state_push = true;
            } else {
                env.post_toast("Yedek Bulunamadı", "Masaüstünde ProjectAurora-Yedek.json yok.", None);
            }
        }
        "checkForUpdates" => {
            env.state.update_status = "GitHub kontrol ediliyor...".into();
            let _ = env.bg_tx.try_send(BgCommand::CheckUpdates(env.state.prefs.update_manifest_url.clone()));
            *env.needs_state_push = true;
        }
        "installAvailableUpdate" => {
            if let Some(man) = env.state.latest_update.clone() {
                env.state.update_downloading = true;
                env.state.update_status = "İndiriliyor ve SHA-256 doğrulanıyor...".into();
                let _ = env.bg_tx.try_send(BgCommand::InstallUpdate(man));
                *env.needs_state_push = true;
            }
        }
        "exitAurora" => {
            *env.should_exit = true;
        }
        _ => {
            env.logger.warn("BRIDGE", "UNKNOWN_ACTION", &format!("Unhandled action: {action}"));
        }
    }
}

fn use_clipboard_item(env: &mut BridgeEnv<'_>, item: &ClipboardVaultEntry) {
    if !item.text_content.is_empty() {
        set_clipboard_text(&item.text_content);
        if env.state.prefs.clipboard_auto_paste_enabled && env.state.last_external_hwnd != 0 {
            env.crown.post_json(r#"{"type":"collapseDeckForPaste"}"#);
            env.crown.release_interactive();
            focus_window(env.state.last_external_hwnd);
            // NEVER sleep on the main Win32 UI thread!
            std::thread::spawn(|| {
                std::thread::sleep(std::time::Duration::from_millis(60));
                send_ctrl_v_paste();
            });
        } else {
            env.post_toast("Panoya Kopyalandı", &item.preview_text, None);
        }
    } else if let Some(img) = &item.image_path {
        let _ = launch_target(img);
    }
}

pub fn start_qr_for_path(env: &mut BridgeEnv<'_>, path: &str) {
    match env.qr_server.start_sharing(Path::new(path)) {
        Ok(info) => {
            env.state.qr_svg = render_qr_svg(&info.modules, info.matrix_size);
            env.state.qr_share = Some(info);
            env.post_toast("QR AirDrop Hazır", "Telefonunuzla okutup anında indirebilirsiniz.", None);
        }
        Err(e) => {
            env.logger.warn("QR", "START_FAIL", &e.to_string());
            env.post_toast("QR Hatası", "Paylaşım başlatılamadı.", None);
        }
    }
}

fn apply_awake_mode(env: &mut BridgeEnv<'_>, mode: &str) {
    match mode {
        "indefinite" => {
            env.state.awake_mode = "indefinite".into();
            env.state.awake_until_ms = None;
            set_awake_keep_alive(true);
        }
        "30m" => {
            env.state.awake_mode = "30m".into();
            env.state.awake_until_ms = Some(env.now_ms + 30 * 60_000);
            set_awake_keep_alive(true);
        }
        "60m" => {
            env.state.awake_mode = "60m".into();
            env.state.awake_until_ms = Some(env.now_ms + 60 * 60_000);
            set_awake_keep_alive(true);
        }
        _ => {
            env.state.awake_mode = "off".into();
            env.state.awake_until_ms = None;
            set_awake_keep_alive(false);
        }
    }
}

fn send_virtual_desktop_step(right: bool) {
    use windows::Win32::UI::Input::KeyboardAndMouse::{
        SendInput, INPUT, INPUT_0, INPUT_KEYBOARD, KEYBDINPUT, KEYEVENTF_KEYUP, VK_CONTROL,
        VK_LEFT, VK_LWIN, VK_RIGHT,
    };
    let arrow = if right { VK_RIGHT } else { VK_LEFT };
    let key = |vk, up| INPUT {
        r#type: INPUT_KEYBOARD,
        Anonymous: INPUT_0 {
            ki: KEYBDINPUT {
                wVk: vk,
                dwFlags: if up { KEYEVENTF_KEYUP } else { Default::default() },
                ..Default::default()
            },
        },
    };
    let seq = [
        key(VK_LWIN, false),
        key(VK_CONTROL, false),
        key(arrow, false),
        key(arrow, true),
        key(VK_CONTROL, true),
        key(VK_LWIN, true),
    ];
    unsafe { let _ = SendInput(&seq, std::mem::size_of::<INPUT>() as i32); }
}

fn send_win_d() {
    use windows::Win32::UI::Input::KeyboardAndMouse::{
        SendInput, INPUT, INPUT_0, INPUT_KEYBOARD, KEYBDINPUT, KEYEVENTF_KEYUP, VIRTUAL_KEY,
        VK_LWIN,
    };
    let vk_d = VIRTUAL_KEY(b'D' as u16);
    let key = |vk, up| INPUT {
        r#type: INPUT_KEYBOARD,
        Anonymous: INPUT_0 {
            ki: KEYBDINPUT {
                wVk: vk,
                dwFlags: if up { KEYEVENTF_KEYUP } else { Default::default() },
                ..Default::default()
            },
        },
    };
    let seq = [key(VK_LWIN, false), key(vk_d, false), key(vk_d, true), key(VK_LWIN, true)];
    unsafe { let _ = SendInput(&seq, std::mem::size_of::<INPUT>() as i32); }
}

fn activate_tray_app(state: &RuntimeState, app: &BackgroundTrayApp) {
    let stem = Path::new(&app.executable_path)
        .file_stem()
        .and_then(|s| s.to_str())
        .map(|s| s.to_ascii_lowercase())
        .unwrap_or_default();
    let app_low = app.name.to_ascii_lowercase();

    if let Some(win) = state.open_windows.iter().find(|w| {
        w.process_id == app.process_id
            || (!stem.is_empty() && w.process_name.eq_ignore_ascii_case(&stem))
            || (!app_low.is_empty() && w.title.to_ascii_lowercase().contains(&app_low))
    }) {
        focus_window(win.hwnd);
        return;
    }

    if !app.executable_path.is_empty() && is_windows_apps_sandbox_path(&app.executable_path) {
        if let Some(inst) = state.installed_apps.iter().find(|a| {
            let lt = a.launch_path.to_ascii_lowercase();
            lt.starts_with("appsfolder:")
                && (a.name.eq_ignore_ascii_case(&app.name) || (!stem.is_empty() && lt.contains(&stem)))
        }) {
            let _ = launch_target(&inst.launch_path);
            return;
        }
    }

    if !app.executable_path.is_empty() {
        let _ = launch_target(&app.executable_path);
    }
}
