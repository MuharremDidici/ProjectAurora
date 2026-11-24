use std::collections::HashMap;
use aurora_core::{
    built_in_themes, evaluate_omni_expression, fuzzy_match_score, theme_by_id, AudioStateSnapshot,
    BackgroundTrayApp, BluetoothBatteryDevice, ClipboardVaultEntry, DockAppGroup,
    ExternalMonitorBrightness, HardwareOsdEvent, InstalledAppEntry, LanQrShareInfo,
    MediaSessionSnapshot, PinnedShortcut, PrivacySnapshot, ReleaseManifest, ResolvedTheme,
    ShellRuntimePreferences, Spring, SpringConfig, SystemMetricsSnapshot, SystemToastNotification,
    ThemeTokens, WeatherSnapshot, WorkspaceProfile, default_workspace_profiles,
};
use aurora_ui::TextInputState;
use chrono::{DateTime, Datelike, Local};

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum SnipMode {
    Ocr,
    Eyedropper,
    GifRegion,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum OverlayPopup {
    None,
    OmniSearch,
    Calendar,
    Notifications,
    Tray,
    RadialHub(String),
    FolderStack(String),
    Tour(usize),
    SnapOverlay,
    RegionSnip(SnipMode),
}

#[derive(Debug, Clone)]
pub struct OmniResult {
    pub id: String,
    pub title: String,
    pub subtitle: String,
    pub badge: String,
    pub launch_path: String,
    pub is_copy_value: bool,
    pub copy_value: String,
    pub score: i32,
}

#[derive(Debug, Clone)]
pub struct FolderStackItem {
    pub name: String,
    pub path: String,
    pub is_dir: bool,
    pub size_label: String,
}

#[derive(Debug, Clone)]
pub struct ActiveOsd {
    pub event: HardwareOsdEvent,
    pub expires_at_ms: u64,
}

#[derive(Debug, Clone)]
pub struct ActiveToast {
    pub notification: SystemToastNotification,
    pub expires_at_ms: u64,
}

#[derive(Debug, Clone)]
pub struct SurfaceAnimations {
    pub crown_morph: Spring,
    pub dock_reveal: Spring,
    pub osd_level: Spring,
    pub radial_bloom: Spring,
    pub toast_slide: Spring,
    pub eq_phase: f32,
}

impl Default for SurfaceAnimations {
    fn default() -> Self {
        Self {
            crown_morph: Spring::new(0.0, SpringConfig::DROPLET_MORPH),
            dock_reveal: Spring::new(1.0, SpringConfig::SNAPPY),
            osd_level: Spring::new(0.0, SpringConfig::OSD_BAR),
            radial_bloom: Spring::new(0.0, SpringConfig::BLOOM),
            toast_slide: Spring::new(0.0, SpringConfig::DROPLET_MORPH),
            eq_phase: 0.0,
        }
    }
}

impl SurfaceAnimations {
    /// Steps all active springs at real frame delta `dt` (120Hz capable).
    /// Returns `true` ONLY if any spring is still moving (so idle consumes 0% CPU/GPU!).
    pub fn step(&mut self, dt: f32, media_playing: bool) -> bool {
        let mut moving = false;
        moving |= self.crown_morph.step(dt);
        moving |= self.dock_reveal.step(dt);
        moving |= self.osd_level.step(dt);
        moving |= self.radial_bloom.step(dt);
        moving |= self.toast_slide.step(dt);
        if media_playing {
            self.eq_phase = (self.eq_phase + dt * 5.2) % (std::f32::consts::TAU);
            moving = true;
        }
        moving
    }
}

/// Complete reactive shell state snapshot rendered directly on GPU by `aurora-surfaces`.
#[derive(Debug, Clone)]
pub struct ShellState {
    pub version: String,
    pub theme_id: String,
    pub theme_tokens: ThemeTokens,
    pub theme: ResolvedTheme,
    pub prefs: ShellRuntimePreferences,
    pub profiles: Vec<WorkspaceProfile>,
    pub active_profile_id: String,

    // Live hardware & OS snapshots
    pub audio: AudioStateSnapshot,
    pub media: MediaSessionSnapshot,
    pub privacy: PrivacySnapshot,
    pub metrics: SystemMetricsSnapshot,
    pub weather: WeatherSnapshot,
    pub bluetooth_devices: Vec<BluetoothBatteryDevice>,
    pub monitors_brightness: Vec<ExternalMonitorBrightness>,
    pub notifications: Vec<SystemToastNotification>,
    pub tray_apps: Vec<BackgroundTrayApp>,
    pub installed_apps: Vec<InstalledAppEntry>,
    pub dock_groups: Vec<DockAppGroup>,
    pub pinned_shortcuts: Vec<PinnedShortcut>,
    pub clipboard_items: Vec<ClipboardVaultEntry>,
    pub drop_shelf_paths: Vec<String>,
    pub qr_share: Option<LanQrShareInfo>,
    pub cpu_history: Vec<f32>,
    pub ram_history: Vec<f32>,
    pub agenda_notes: HashMap<String, String>,
    pub frecency: HashMap<String, u32>,

    // Live system flags
    pub wifi_enabled: bool,
    pub bluetooth_enabled: bool,
    pub focus_mode_enabled: bool,
    pub awake_minutes_remaining: Option<u32>, // None=off, Some(0)=indefinite, Some(m)=timer
    pub focus_timer_running: bool,
    pub focus_timer_seconds: u32,
    pub gif_recording: bool,
    pub gif_elapsed_secs: u32,
    pub keyboard_code: String,
    pub keyboard_name: String,
    pub virtual_desktop_index: i32,
    pub virtual_desktop_count: i32,
    pub latest_update: Option<ReleaseManifest>,
    pub update_status: String,
    pub status_banner: Option<String>,

    // Interactive UI state
    pub hovered_id: Option<String>,
    pub crown_expanded: bool,
    pub crown_hover_pill: bool,
    pub active_deck_tab: String, // "control" | "shelf" | "metrics" | "studio"
    pub open_popup: OverlayPopup,
    pub active_osd: Option<ActiveOsd>,
    pub active_toast: Option<ActiveToast>,
    pub expanded_accordion: Option<String>,
    pub folder_stack_entries: Vec<FolderStackItem>,
    pub armed_power: Option<String>,
    pub armed_kill_pid: Option<u32>,
    pub clipboard_filter: String, // "all" | "pinned" | "text" | "image"
    pub clipboard_search: TextInputState,
    pub scratchpad: TextInputState,
    pub agenda_input: TextInputState,
    pub omni_input: TextInputState,
    pub omni_filter: String, // "all" | "app" | "file" | "setting"
    pub omni_selected: usize,
    pub omni_results: Vec<OmniResult>,
    pub selected_day_iso: String,
    pub calendar_year: i32,
    pub calendar_month: u32,

    // Studio color customizer
    pub studio_color_slot: String, // "accent1" | "accent2" | "surface1" | "surface2" | "text"
    pub studio_hue: f32,
    pub studio_sat: f32,
    pub studio_val: f32,
    pub studio_scroll_y: f32,
    pub control_scroll_y: f32,

    // Dock & Monitor layout interaction
    pub dock_hover_cursor: Option<(f32, f32)>,
    pub dock_tucked: bool,
    pub is_fullscreen_on_monitor: bool,
    pub is_dragging_file: bool,
    pub active_snap_hover_idx: Option<usize>,
    pub eyedropper_hex: String,
    pub eyedropper_cursor: (f32, f32),
    pub snip_drag_start: Option<(f32, f32)>,
    pub snip_drag_end: Option<(f32, f32)>,
    pub now_local: DateTime<Local>,

    pub anim: SurfaceAnimations,
}

impl ShellState {
    pub fn new(theme_id: &str, prefs: ShellRuntimePreferences) -> Self {
        let now = Local::now();
        let day_iso = format!("{:04}-{:02}-{:02}", now.year(), now.month(), now.day());
        let base_theme = theme_by_id(theme_id);
        let applied = base_theme.with_customization(prefs.theme_customization(&base_theme.id));
        let resolved = applied.resolved();
        Self {
            version: "3.0.0".into(),
            theme_id: base_theme.id.clone(),
            theme_tokens: applied,
            theme: resolved,
            prefs,
            profiles: default_workspace_profiles(),
            active_profile_id: "profile-personal".into(),
            audio: AudioStateSnapshot::default(),
            media: MediaSessionSnapshot::default(),
            privacy: PrivacySnapshot::default(),
            metrics: SystemMetricsSnapshot::default(),
            weather: WeatherSnapshot::default(),
            bluetooth_devices: Vec::new(),
            monitors_brightness: Vec::new(),
            notifications: Vec::new(),
            tray_apps: Vec::new(),
            installed_apps: Vec::new(),
            dock_groups: Vec::new(),
            pinned_shortcuts: Vec::new(),
            clipboard_items: Vec::new(),
            drop_shelf_paths: Vec::new(),
            qr_share: None,
            cpu_history: vec![12.0, 14.0, 18.0, 15.0, 11.0, 13.0],
            ram_history: vec![42.0, 42.5, 43.0, 42.8, 42.2, 42.4],
            agenda_notes: HashMap::new(),
            frecency: HashMap::new(),
            wifi_enabled: true,
            bluetooth_enabled: true,
            focus_mode_enabled: false,
            awake_minutes_remaining: None,
            focus_timer_running: false,
            focus_timer_seconds: 25 * 60,
            gif_recording: false,
            gif_elapsed_secs: 0,
            keyboard_code: "TR".into(),
            keyboard_name: "Türkçe Q".into(),
            virtual_desktop_index: 1,
            virtual_desktop_count: 2,
            latest_update: None,
            update_status: "Güncel (v3.0.0)".into(),
            status_banner: None,
            hovered_id: None,
            crown_expanded: false,
            crown_hover_pill: false,
            active_deck_tab: "control".into(),
            open_popup: OverlayPopup::None,
            active_osd: None,
            active_toast: None,
            expanded_accordion: None,
            folder_stack_entries: Vec::new(),
            armed_power: None,
            armed_kill_pid: None,
            clipboard_filter: "all".into(),
            clipboard_search: TextInputState::new("Pano geçmişinde ara..."),
            scratchpad: TextInputState::new("Hızlı notlarınızı buraya yazın (otomatik kaydedilir)..."),
            agenda_input: TextInputState::new("Seçili gün için not ekle..."),
            omni_input: TextInputState::new("Uygulama, dosya, hesap (= 24*15) veya ayar ara..."),
            omni_filter: "all".into(),
            omni_selected: 0,
            omni_results: Vec::new(),
            selected_day_iso: day_iso,
            calendar_year: now.year(),
            calendar_month: now.month(),
            studio_color_slot: "accent1".into(),
            studio_hue: 200.0,
            studio_sat: 0.78,
            studio_val: 0.96,
            studio_scroll_y: 0.0,
            control_scroll_y: 0.0,
            dock_hover_cursor: None,
            dock_tucked: false,
            is_fullscreen_on_monitor: false,
            is_dragging_file: false,
            active_snap_hover_idx: None,
            eyedropper_hex: "#38BDF8".into(),
            eyedropper_cursor: (0.0, 0.0),
            snip_drag_start: None,
            snip_drag_end: None,
            now_local: now,
            anim: SurfaceAnimations::default(),
        }
    }

    pub fn recompute_theme(&mut self) {
        let base = theme_by_id(&self.theme_id);
        let applied = base.with_customization(self.prefs.theme_customization(&base.id));
        self.theme = applied.resolved();
        self.theme_tokens = applied;
    }

    pub fn set_theme(&mut self, theme_id: &str) {
        self.theme_id = theme_by_id(theme_id).id;
        self.recompute_theme();
    }

    pub fn rebuild_omni_results(&mut self, file_matches: &[(String, String)]) {
        let q = self.omni_input.text.trim();
        let mut results = Vec::new();

        if let Some((answer, desc)) = evaluate_omni_expression(q) {
            results.push(OmniResult {
                id: "calc-res".into(),
                title: answer.clone(),
                subtitle: format!("{desc}  •  Enter ile sonucu panoya kopyala"),
                badge: "HESAP".into(),
                launch_path: String::new(),
                is_copy_value: true,
                copy_value: answer,
                score: 10_000,
            });
        }

        if self.omni_filter == "all" || self.omni_filter == "app" || self.omni_filter == "setting" {
            for app in &self.installed_apps {
                if self.omni_filter == "app" && app.kind == "setting" {
                    continue;
                }
                if self.omni_filter == "setting" && app.kind != "setting" && app.kind != "tool" {
                    continue;
                }
                let freq = self.frecency.get(&app.id).copied().unwrap_or(0);
                let score = if q.is_empty() {
                    Some(100 + freq as i32 * 12)
                } else {
                    let s1 = fuzzy_match_score(q, &app.name, freq);
                    let s2 = fuzzy_match_score(q, &app.keywords, freq).map(|v| v / 2);
                    match (s1, s2) {
                        (Some(a), Some(b)) => Some(a.max(b)),
                        (Some(a), None) => Some(a),
                        (None, Some(b)) => Some(b),
                        (None, None) => None,
                    }
                };
                if let Some(sc) = score {
                    results.push(OmniResult {
                        id: app.id.clone(),
                        title: app.name.clone(),
                        subtitle: app.category.clone(),
                        badge: if app.kind == "setting" { "AYAR" } else if app.kind == "tool" { "ARAÇ" } else { "UYGULAMA" }.into(),
                        launch_path: app.launch_path.clone(),
                        is_copy_value: false,
                        copy_value: String::new(),
                        score: sc,
                    });
                }
            }
        }

        if self.omni_filter == "all" || self.omni_filter == "file" {
            for (name, path) in file_matches {
                results.push(OmniResult {
                    id: format!("file:{path}"),
                    title: name.clone(),
                    subtitle: path.clone(),
                    badge: "DOSYA".into(),
                    launch_path: path.clone(),
                    is_copy_value: false,
                    copy_value: String::new(),
                    score: 420,
                });
            }
        }

        results.sort_by_key(|a| std::cmp::Reverse(a.score));
        results.truncate(9);
        if self.omni_selected >= results.len() {
            self.omni_selected = 0;
        }
        self.omni_results = results;
    }

    pub fn formatted_clock(&self) -> String {
        if self.prefs.show_clock_seconds {
            self.now_local.format("%H:%M:%S").to_string()
        } else {
            self.now_local.format("%H:%M").to_string()
        }
    }

    pub fn formatted_date_tr(&self) -> String {
        let months = [
            "Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara",
        ];
        let m = months[(self.now_local.month().saturating_sub(1) as usize).min(11)];
        format!("{} {}", self.now_local.day(), m)
    }

    /// Rich deterministic fixture state for headless visual verification across all 7 themes!
    pub fn demo_for_capture(theme_id: &str) -> Self {
        use aurora_core::{
            AppWindowEntry, AudioDeviceInfo, PerAppAudioSession, TopProcessInfo,
        };
        let prefs = ShellRuntimePreferences {
            show_clock_seconds: true,
            dock_visibility_mode: Some("AlwaysVisible".into()),
            reserve_dock_work_area: Some(true),
            ..Default::default()
        };
        let mut s = Self::new(theme_id, prefs);

        s.weather = WeatherSnapshot {
            city: "İstanbul".into(),
            temp_c: 19.0,
            condition_tr: "Parçalı Bulutlu".into(),
            valid: true,
        };
        s.media = MediaSessionSnapshot {
            is_playing: true,
            has_session: true,
            title: "Midnight City (Remastered)".into(),
            artist: "M83 • Hurry Up, We're Dreaming".into(),
            app_name: "Spotify".into(),
            thumbnail_bytes: None,
        };
        s.audio = AudioStateSnapshot {
            master_volume_percent: 72,
            is_master_muted: false,
            default_output_id: "out-1".into(),
            default_output_name: "Sony WH-1000XM5".into(),
            mic_supported: true,
            is_mic_muted: false,
            outputs: vec![
                AudioDeviceInfo { id: "out-1".into(), name: "Sony WH-1000XM5".into(), is_default: true },
                AudioDeviceInfo { id: "out-2".into(), name: "Realtek Hoparlör".into(), is_default: false },
                AudioDeviceInfo { id: "out-3".into(), name: "ViewSonic VA2715 HDMI".into(), is_default: false },
            ],
            app_sessions: vec![
                PerAppAudioSession { process_id: 1001, process_name: "spotify".into(), display_name: "Spotify".into(), volume_percent: 82, is_muted: false, routed_device_id: Some("out-1".into()) },
                PerAppAudioSession { process_id: 1002, process_name: "chrome".into(), display_name: "Google Chrome".into(), volume_percent: 64, is_muted: false, routed_device_id: None },
                PerAppAudioSession { process_id: 1003, process_name: "discord".into(), display_name: "Discord".into(), volume_percent: 100, is_muted: false, routed_device_id: Some("out-1".into()) },
            ],
            per_app_routing_supported: true,
        };
        s.monitors_brightness = vec![
            ExternalMonitorBrightness { id: "wmi-0".into(), name: "Dahili Laptop Ekranı".into(), brightness_percent: 80, is_internal: true },
            ExternalMonitorBrightness { id: "ddc-1".into(), name: "ViewSonic VA2715-2K-MHD".into(), brightness_percent: 68, is_internal: false },
            ExternalMonitorBrightness { id: "ddc-2".into(), name: "Samsung SyncMaster 19".into(), brightness_percent: 55, is_internal: false },
        ];
        s.bluetooth_devices = vec![
            BluetoothBatteryDevice { id: "bt-1".into(), name: "Sony WH-1000XM5".into(), battery_percent: 84, category: "headset".into(), is_low: false, has_battery: true, is_connected: true },
            BluetoothBatteryDevice { id: "bt-2".into(), name: "Logitech MX Master 3S".into(), battery_percent: 14, category: "mouse".into(), is_low: true, has_battery: true, is_connected: true },
            BluetoothBatteryDevice { id: "bt-3".into(), name: "Keychron K3 Pro".into(), battery_percent: 62, category: "keyboard".into(), is_low: false, has_battery: true, is_connected: true },
        ];
        s.privacy = PrivacySnapshot {
            camera_active: false,
            mic_active: true,
            camera_apps: vec![],
            mic_apps: vec!["Discord".into()],
        };
        s.metrics = SystemMetricsSnapshot {
            cpu_percent: 18.4,
            ram_percent: 44.2,
            ram_used_gb: 14.1,
            ram_total_gb: 32.0,
            net_down_mbps: 42.6,
            net_up_mbps: 8.3,
            battery_present: true,
            battery_percent: 88,
            is_charging: true,
            top_processes: vec![
                TopProcessInfo { pid: 4120, name: "Code.exe".into(), memory_mb: 842 },
                TopProcessInfo { pid: 5510, name: "chrome.exe".into(), memory_mb: 615 },
                TopProcessInfo { pid: 6211, name: "Discord.exe".into(), memory_mb: 318 },
            ],
        };
        s.cpu_history = vec![14.0, 19.0, 24.0, 17.0, 15.0, 21.0, 28.0, 22.0, 16.0, 18.4];
        s.ram_history = vec![41.2, 41.8, 42.5, 43.0, 43.4, 43.9, 44.1, 44.0, 44.2, 44.2];
        s.notifications = vec![
            SystemToastNotification {
                notification_id: 101,
                app_name: "WhatsApp".into(),
                title: "Ahmet Yılmaz".into(),
                body: "Aurora v3.0 tasarımını gördüm, akıcılık inanılmaz olmuş!".into(),
                arrived_at_utc: "14:28".into(),
                primary_id: "5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App".into(),
                handler_type: "app:immersive".into(),
                launch_arguments: String::new(),
                activation_type: "foreground".into(),
            },
            SystemToastNotification {
                notification_id: 102,
                app_name: "GitHub".into(),
                title: "Release v3.0.0 Hazır".into(),
                body: "Windows-x64 Rust native derleme 0 uyarıyla tamamlandı.".into(),
                arrived_at_utc: "14:15".into(),
                primary_id: String::new(),
                handler_type: "app:desktop".into(),
                launch_arguments: String::new(),
                activation_type: "foreground".into(),
            },
        ];
        s.clipboard_items = vec![
            ClipboardVaultEntry {
                id: 1,
                kind: "text".into(),
                text_content: "cargo build --release --workspace".into(),
                preview_text: "cargo build --release --workspace".into(),
                image_path: None,
                thumb_base64: None,
                image_width: 0,
                image_height: 0,
                source_app: "Windows Terminal".into(),
                is_pinned: true,
                last_copied_utc: "14:26".into(),
            },
            ClipboardVaultEntry {
                id: 2,
                kind: "text".into(),
                text_content: "https://github.com/MuharremDidici/ProjectAurora".into(),
                preview_text: "https://github.com/MuharremDidici/ProjectAurora".into(),
                image_path: None,
                thumb_base64: None,
                image_width: 0,
                image_height: 0,
                source_app: "Chrome".into(),
                is_pinned: false,
                last_copied_utc: "14:12".into(),
            },
            ClipboardVaultEntry {
                id: 3,
                kind: "image".into(),
                text_content: String::new(),
                preview_text: "Ekran Görüntüsü (1920×1080 PNG)".into(),
                image_path: Some("clip_3.png".into()),
                thumb_base64: None,
                image_width: 1920,
                image_height: 1080,
                source_app: "Snipping Tool".into(),
                is_pinned: false,
                last_copied_utc: "13:55".into(),
            },
        ];
        s.drop_shelf_paths = vec![
            r"C:\Users\Lenovo\Documents\Aurora-v3-Sunum.pdf".into(),
            r"C:\Users\Lenovo\Pictures\Desktop-Mockup.png".into(),
        ];
        s.qr_share = Some(LanQrShareInfo {
            url: "http://192.168.1.42:54321/a9f3".into(),
            file_name: "Aurora-v3-Sunum.pdf".into(),
            size_label: "4.2 MB".into(),
            matrix_size: 21,
            modules: (0..441)
                .map(|i| {
                    let (r, c) = (i / 21, i % 21);
                    let finder = (r < 7 && c < 7) || (r < 7 && c >= 14) || (r >= 14 && c < 7);
                    finder || ((r * 7 + c * 13 + i) % 3 == 0)
                })
                .collect(),
        });
        s.scratchpad.set_text("• v3.0.0 Native GPU motoru: <50 MB RAM, 0 WebView2\n• 7 tema WCAG AA+ doğrulandı");
        s.dock_groups = vec![
            DockAppGroup {
                group_key: "explorer".into(),
                display_name: "Dosya Gezgini".into(),
                executable_path: r"C:\Windows\explorer.exe".into(),
                is_pinned: true,
                pin_id: Some("pin-1".into()),
                has_active: false,
                needs_attention: false,
                progress_percent: None,
                windows: vec![],
            },
            DockAppGroup {
                group_key: "code".into(),
                display_name: "VS Code".into(),
                executable_path: r"C:\Program Files\Microsoft VS Code\Code.exe".into(),
                is_pinned: true,
                pin_id: Some("pin-2".into()),
                has_active: true,
                needs_attention: false,
                progress_percent: None,
                windows: vec![
                    AppWindowEntry {
                        hwnd: 101,
                        process_id: 4120,
                        process_name: "code".into(),
                        executable_path: String::new(),
                        title: "aurora-rs — Visual Studio Code".into(),
                        monitor_device_id: "DISPLAY3".into(),
                        is_active: true,
                        is_minimized: false,
                        is_top_most: false,
                        needs_attention: false,
                        progress_percent: None,
                    },
                    AppWindowEntry {
                        hwnd: 102,
                        process_id: 4120,
                        process_name: "code".into(),
                        executable_path: String::new(),
                        title: "implementation_plan.md — VS Code".into(),
                        monitor_device_id: "DISPLAY3".into(),
                        is_active: false,
                        is_minimized: false,
                        is_top_most: false,
                        needs_attention: false,
                        progress_percent: None,
                    },
                ],
            },
            DockAppGroup {
                group_key: "windowsterminal".into(),
                display_name: "Terminal".into(),
                executable_path: "wt.exe".into(),
                is_pinned: true,
                pin_id: Some("pin-3".into()),
                has_active: false,
                needs_attention: false,
                progress_percent: None,
                windows: vec![AppWindowEntry {
                    hwnd: 103,
                    process_id: 4200,
                    process_name: "windowsterminal".into(),
                    executable_path: String::new(),
                    title: "PowerShell 7 — cargo test".into(),
                    monitor_device_id: "DISPLAY3".into(),
                    is_active: false,
                    is_minimized: false,
                    is_top_most: false,
                    needs_attention: false,
                    progress_percent: None,
                }],
            },
            DockAppGroup {
                group_key: "chrome".into(),
                display_name: "Chrome".into(),
                executable_path: "chrome.exe".into(),
                is_pinned: true,
                pin_id: Some("pin-4".into()),
                has_active: false,
                needs_attention: false,
                progress_percent: None,
                windows: vec![AppWindowEntry {
                    hwnd: 104,
                    process_id: 5510,
                    process_name: "chrome".into(),
                    executable_path: String::new(),
                    title: "ProjectAurora — GitHub".into(),
                    monitor_device_id: "DISPLAY3".into(),
                    is_active: false,
                    is_minimized: false,
                    is_top_most: false,
                    needs_attention: false,
                    progress_percent: None,
                }],
            },
            DockAppGroup {
                group_key: "whatsapp".into(),
                display_name: "WhatsApp".into(),
                executable_path: "whatsapp.exe".into(),
                is_pinned: false,
                pin_id: None,
                has_active: false,
                needs_attention: true,
                progress_percent: None,
                windows: vec![AppWindowEntry {
                    hwnd: 105,
                    process_id: 6012,
                    process_name: "whatsapp".into(),
                    executable_path: String::new(),
                    title: "WhatsApp (1 yeni mesaj)".into(),
                    monitor_device_id: "DISPLAY3".into(),
                    is_active: false,
                    is_minimized: false,
                    is_top_most: false,
                    needs_attention: true,
                    progress_percent: None,
                }],
            },
            DockAppGroup {
                group_key: "spotify".into(),
                display_name: "Spotify".into(),
                executable_path: "spotify.exe".into(),
                is_pinned: true,
                pin_id: Some("pin-5".into()),
                has_active: false,
                needs_attention: false,
                progress_percent: None,
                windows: vec![AppWindowEntry {
                    hwnd: 106,
                    process_id: 7020,
                    process_name: "spotify".into(),
                    executable_path: String::new(),
                    title: "Spotify Premium".into(),
                    monitor_device_id: "DISPLAY3".into(),
                    is_active: false,
                    is_minimized: false,
                    is_top_most: false,
                    needs_attention: false,
                    progress_percent: None,
                }],
            },
        ];
        s.installed_apps = vec![
            InstalledAppEntry { id: "app-code".into(), name: "Visual Studio Code".into(), launch_path: "code".into(), category: "Geliştirme".into(), keywords: "ide editor rust".into(), kind: "app".into(), icon_base64: None },
            InstalledAppEntry { id: "app-wt".into(), name: "Windows Terminal".into(), launch_path: "wt.exe".into(), category: "Geliştirme".into(), keywords: "shell powershell cmd".into(), kind: "app".into(), icon_base64: None },
            InstalledAppEntry { id: "app-wa".into(), name: "WhatsApp".into(), launch_path: "whatsapp:".into(), category: "İletişim".into(), keywords: "mesaj sohbet".into(), kind: "uwp".into(), icon_base64: None },
            InstalledAppEntry { id: "set-disp".into(), name: "Ekran & Ölçekleme Ayarları".into(), launch_path: "ms-settings:display".into(), category: "Windows Ayarı".into(), keywords: "monitör dpi parlaklık".into(), kind: "setting".into(), icon_base64: None },
            InstalledAppEntry { id: "tool-ocr".into(), name: "Ekran Metni Kopyala (OCR)".into(), launch_path: "aurora:ocr".into(), category: "Aurora Aracı".into(), keywords: "yazı tarama snip".into(), kind: "tool".into(), icon_base64: None },
        ];
        s.tray_apps = vec![
            BackgroundTrayApp { id: "t1".into(), name: "NVIDIA".into(), executable_path: String::new(), process_id: 901, memory_mb: 42, subtitle: "Grafik".into() },
            BackgroundTrayApp { id: "t2".into(), name: "Steam".into(), executable_path: String::new(), process_id: 902, memory_mb: 185, subtitle: "Oyun Platformu".into() },
            BackgroundTrayApp { id: "t3".into(), name: "OneDrive".into(), executable_path: String::new(), process_id: 903, memory_mb: 64, subtitle: "Bulut Senkron".into() },
            BackgroundTrayApp { id: "t4".into(), name: "Logitech G HUB".into(), executable_path: String::new(), process_id: 904, memory_mb: 98, subtitle: "Donanım".into() },
        ];
        s.omni_input.set_text("24 * 15");
        s.rebuild_omni_results(&[("implementation_plan.md".into(), r"C:\Users\Lenovo\Documents\windowsTaskBar\implementation_plan.md".into())]);
        let _ = built_in_themes();
        s
    }
}
