#![windows_subsystem = "windows"]
#![recursion_limit = "512"]

mod actions;
mod bridge;
mod capture_suite;
mod webview_host;
mod window;

use std::collections::{HashMap, HashSet};
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use std::time::{Duration, Instant};

use actions::{build_dock_groups_ordered, update_stable_orders, user_known_folder};
use aurora_core::{
    theme_by_id, AiProviderQuotaSnapshot, AudioStateSnapshot, BackgroundTrayApp,
    BluetoothBatteryDevice, Color, DiskUsageEntry, ExternalMonitorBrightness, InstalledAppEntry,
    LocalAiStatusSnapshot, MarketSnapshot, MediaSessionSnapshot, MonitorDescriptor, PhysicalRect,
    PrivacySnapshot, SystemMetricsSnapshot, SystemToastNotification, ThemeColorCustomization,
    WeatherSnapshot, AURORA_VERSION,
};
use aurora_render::{FontFamily, GpuContext, RectF, TextAlign};
use aurora_storage::{
    aurora_data_dir, aurora_root_dir, check_for_updates, download_and_verify_installer, AuroraDb,
    AuxiliaryStateStore, ClipboardVaultDb, PrivacySafeLogger, ShellPreferencesStore,
    StartupCrashSentinel, UserPreferencesRecord, WidgetsLayoutStore, WidgetsPersistenceDoc,
};
use aurora_surfaces::SnipMode;
use aurora_win32::{
    analyze_fullscreen_and_dodge, build_full_installed_app_catalog, compute_next_volume_step,
    compute_snap_zones, enable_per_monitor_dpi_v2, enumerate_monitors, enumerate_open_windows,
    extract_icon_png_base64, extract_shell_path_icon_base64, extract_wallpaper_accent_colors,
    fetch_live_weather, fetch_market_snapshot, force_foreground_window, friendly_folder_name,
    get_clipboard_text, get_foreground_hwnd, get_keyboard_layout_code,
    is_point_inside_window_or_owned, is_window_owned_by, launch_target, pick_file_dialog,
    pick_folder_dialog, post_wake_ui, query_ai_quota_snapshots, query_background_tray_apps,
    query_bluetooth_batteries, query_disk_usages, query_local_ai_status, query_media_session,
    query_privacy_snapshot, read_folder_stack_entries_full, recognize_screen_region_ocr,
    record_gif_region, register_aurora_hotkeys, restore_taskbar_standalone,
    sample_screen_pixel_hex, search_local_files, set_alt_drag_enabled, set_clipboard_text,
    set_snap_zones_enabled, set_watch_external_clicks, try_get_clipboard_image_png,
    try_suppress_win11_shell_overlay, unregister_aurora_hotkeys, BrightnessController,
    CoreAudioController, DwmLivePreviewWindow, LanQrShareServer, LiveMonitorHandle,
    LowLevelKeyEvent, LowLevelKeyboardHook, MetricsSampler, NativeAppBarManager, ShellEventHooks,
    ShellHookEvent, WpnNotificationBridge, HOTKEY_CROWN_DECK, HOTKEY_EYEDROPPER,
    HOTKEY_GIF_RECORDER, HOTKEY_OCR_SNIP, HOTKEY_OMNI_SEARCH,
};
use bridge::{handle_bridge_action, start_qr_for_path, BgCommand, BridgeEnv, RuntimeState};
use crossbeam_channel::{bounded, unbounded, Receiver, Sender};
use serde_json::{json, Value};
use webview2_com::Microsoft::Web::WebView2::Win32::ICoreWebView2Environment;
use webview_host::{create_shared_webview_env, SurfaceMessage, WebViewOverlaySurface};
use window::{ShellOverlayWindow, WindowEvent};
use windows::core::w;
use windows::Win32::Foundation::{
    CloseHandle, GetLastError, ERROR_ALREADY_EXISTS, HWND, POINT, WAIT_OBJECT_0,
};
use windows::Win32::System::Com::{CoInitializeEx, COINIT_APARTMENTTHREADED};
use windows::Win32::System::Threading::{
    CreateMutexW, OpenProcess, WaitForSingleObject, INFINITE, PROCESS_SYNCHRONIZE,
};
use windows::Win32::UI::Input::KeyboardAndMouse::{
    GetAsyncKeyState, VK_ESCAPE, VK_LBUTTON, VK_SHIFT,
};
use windows::Win32::UI::WindowsAndMessaging::{
    DispatchMessageW, GetCursorPos, IsZoomed, MsgWaitForMultipleObjectsEx, PeekMessageW,
    SetWindowPos, ShowWindowAsync, TranslateMessage, MSG, MWMO_INPUTAVAILABLE, PM_REMOVE,
    QS_ALLINPUT, SWP_ASYNCWINDOWPOS, SWP_NOACTIVATE, SWP_NOZORDER, SW_RESTORE,
};

const CROWN_HTML: &str = include_str!("../ui/crown.html");
const DOCK_HTML: &str = include_str!("../ui/dock.html");
const WIDGETS_HTML: &str = include_str!("../ui/widgets.html");
const WIDGETS_SURFACE_ID: usize = 100;

enum BgReply {
    InstalledApps(Vec<InstalledAppEntry>),
    Weather(WeatherSnapshot),
    AiQuota(Vec<AiProviderQuotaSnapshot>),
    Market(MarketSnapshot),
    LocalAiAndDisks {
        local_ai: LocalAiStatusSnapshot,
        disks: Vec<DiskUsageEntry>,
    },
    WallpaperAccent(Option<(String, String)>),
    Media(MediaSessionSnapshot),
    AudioSnapshot(AudioStateSnapshot),
    Bluetooth(Vec<BluetoothBatteryDevice>),
    Brightness(Vec<ExternalMonitorBrightness>),
    Privacy(PrivacySnapshot),
    Metrics(SystemMetricsSnapshot),
    TrayApps(Vec<BackgroundTrayApp>),
    FreshNotifications(Vec<SystemToastNotification>),
    ClipboardText {
        text: String,
    },
    ClipboardImage {
        width: i32,
        height: i32,
        png_bytes: Vec<u8>,
        thumb_b64: String,
    },
    FileSearch {
        seq: u64,
        query: String,
        files: Vec<Value>,
    },
    FolderStackReady {
        monitor_id: String,
        path: String,
        name: String,
        items: Vec<Value>,
    },
    DockFolderPicked(String),
    ShelfFilePicked {
        path: String,
        share_qr_after: bool,
    },
    WidgetPhotoLoaded {
        widget_id: String,
        path: String,
        data_uri: String,
    },
    IconReady {
        key: String,
        b64: String,
    },
    OcrDone(Option<String>),
    GifDone(Result<(u32, u64, String), String>),
    UpdateChecked(aurora_storage::UpdateCheckOutcome),
    UpdateDownloaded(Result<PathBuf, String>),
}

/// On-demand full-screen Direct2D / DirectComposition overlay instantiated ONLY during
/// OCR Snip, Eyedropper, GIF Region selection, or Shift+Drag Snap Zones (0 MB idle overhead!).
struct TransientD2DOverlay {
    gpu: GpuContext,
    win: ShellOverlayWindow,
    rx: Receiver<WindowEvent>,
    snip_drag_start: Option<(f32, f32)>,
    snip_drag_cur: (f32, f32),
    eyedropper_hex: String,
}

impl TransientD2DOverlay {
    fn open(bounds_px: PhysicalRect, dpi_scale: f32) -> anyhow::Result<Self> {
        let gpu = GpuContext::new()?;
        let (tx, rx) = unbounded::<WindowEvent>();
        let win = ShellOverlayWindow::create(99, &gpu, bounds_px, dpi_scale, tx)?;
        Ok(Self {
            gpu,
            win,
            rx,
            snip_drag_start: None,
            snip_drag_cur: (
                bounds_px.width as f32 / (2.0 * dpi_scale),
                bounds_px.height as f32 / (2.0 * dpi_scale),
            ),
            eyedropper_hex: "#000000".into(),
        })
    }

    fn paint(
        &mut self,
        snip_mode: Option<SnipMode>,
        snap_preset: &str,
        snap_active: bool,
        snap_hover: Option<usize>,
        work_area_px: PhysicalRect,
        accent_hex: &str,
    ) {
        let scale = self.win.dpi_scale.max(1.0);
        let bounds = self.win.bounds_px;
        let drag_start = self.snip_drag_start;
        let drag_cur = self.snip_drag_cur;
        let eye_hex = self.eyedropper_hex.clone();
        let accent = Color::from_hex(accent_hex);
        let dark_card = Color::from_hex("#0C101A").with_alpha(0.92);

        let _ = self.win.render(&self.gpu, snip_mode.is_some(), |p, hits, w, h| {
            hits.clear();
            if snap_active {
                let zones = compute_snap_zones(work_area_px, snap_preset, 10);
                for (idx, (label, z)) in zones.iter().enumerate() {
                    let zr = RectF::new(
                        (z.x - bounds.x) as f32 / scale,
                        (z.y - bounds.y) as f32 / scale,
                        z.width as f32 / scale,
                        z.height as f32 / scale,
                    )
                    .inset(6.0, 6.0);
                    let hov = snap_hover == Some(idx);
                    p.fill_rounded_rect(
                        zr,
                        18.0,
                        accent.with_alpha(if hov { 0.30 } else { 0.12 }),
                    );
                    p.stroke_rounded_rect(
                        zr,
                        18.0,
                        accent.with_alpha(if hov { 0.95 } else { 0.55 }),
                        if hov { 2.5 } else { 1.4 },
                        false,
                    );
                    p.draw_text(
                        label,
                        zr,
                        16.0,
                        700,
                        Color::WHITE.with_alpha(if hov { 0.96 } else { 0.72 }),
                        FontFamily::Ui,
                        TextAlign::Center,
                        true,
                        false,
                    );
                }
            } else if let Some(mode) = snip_mode {
                hits.opaque_shells.push(RectF::new(0.0, 0.0, w, h));
                let dim_alpha = if mode == SnipMode::Eyedropper { 0.08 } else { 0.34 };
                p.fill_rounded_rect(
                    RectF::new(0.0, 0.0, w, h),
                    0.0,
                    Color::BLACK.with_alpha(dim_alpha),
                );
                let hint = match mode {
                    SnipMode::Ocr => "OCR Ekran Metni — Metin alanını sürükleyip seçin (ESC iptal)",
                    SnipMode::Eyedropper => {
                        "Renk Damlalığı — Kopyalamak için herhangi bir piksele tıklayın (ESC iptal)"
                    }
                    SnipMode::GifRegion => {
                        "GIF Kaydedici — Kaydedilecek bölgeyi sürükleyip seçin (ESC iptal)"
                    }
                };
                let pill = RectF::new((w - 500.0) * 0.5, 28.0, 500.0, 36.0);
                p.fill_rounded_rect(pill, 18.0, dark_card);
                p.stroke_rounded_rect(pill, 18.0, accent.with_alpha(0.75), 1.2, false);
                p.draw_text(
                    hint,
                    pill,
                    12.5,
                    600,
                    Color::WHITE,
                    FontFamily::Ui,
                    TextAlign::Center,
                    true,
                    false,
                );

                if mode == SnipMode::Eyedropper {
                    let (cx, cy) = drag_cur;
                    let badge = RectF::new(
                        (cx + 18.0).min(w - 150.0),
                        (cy + 18.0).min(h - 48.0),
                        132.0,
                        36.0,
                    );
                    p.fill_rounded_rect(badge, 10.0, dark_card);
                    p.stroke_rounded_rect(badge, 10.0, Color::WHITE.with_alpha(0.32), 1.3, false);
                    let sw = RectF::new(badge.x + 8.0, badge.y + 8.0, 20.0, 20.0);
                    p.fill_rounded_rect(sw, 6.0, Color::from_hex(&eye_hex));
                    p.stroke_rounded_rect(sw, 6.0, Color::WHITE.with_alpha(0.6), 1.0, false);
                    p.draw_text(
                        &eye_hex,
                        RectF::new(badge.x + 34.0, badge.y, badge.w - 38.0, badge.h),
                        13.0,
                        700,
                        Color::WHITE,
                        FontFamily::Mono,
                        TextAlign::Left,
                        true,
                        false,
                    );
                } else if let Some((sx, sy)) = drag_start {
                    let (cx, cy) = drag_cur;
                    let sel = RectF::new(
                        sx.min(cx),
                        sy.min(cy),
                        (cx - sx).abs().max(2.0),
                        (cy - sy).abs().max(2.0),
                    );
                    let border = if mode == SnipMode::GifRegion {
                        Color::from_hex("#FB7185")
                    } else {
                        accent
                    };
                    p.fill_rounded_rect(sel, 6.0, border.with_alpha(0.16));
                    p.stroke_rounded_rect(sel, 6.0, border, 2.0, false);
                }
            }
        });
    }
}

fn snap_window_to_rect(hwnd_raw: isize, z: PhysicalRect) {
    let hwnd = HWND(hwnd_raw as *mut _);
    if hwnd.0.is_null() {
        return;
    }
    unsafe {
        if IsZoomed(hwnd).as_bool() {
            let _ = ShowWindowAsync(hwnd, SW_RESTORE);
        }
        let _ = SetWindowPos(
            hwnd,
            None,
            z.x,
            z.y,
            z.width.max(120),
            z.height.max(120),
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS,
        );
    }
}

fn main() {
    let args: Vec<String> = std::env::args().collect();

    // 1. Emergency Taskbar Restore CLI Mode
    if args.iter().any(|a| a == "--restore-taskbar") {
        restore_taskbar_standalone();
        return;
    }

    // 2. Zero-CPU Watchdog Subprocess Mode (restores native Windows taskbar in <100ms if main dies!)
    if let Some(pos) = args.iter().position(|a| a == "--watchdog-pid") {
        if let Some(pid) = args.get(pos + 1).and_then(|s| s.parse::<u32>().ok()) {
            run_watchdog(pid);
        }
        return;
    }

    // 3. Headless GPU 7-Theme Visual Capture Suite Mode
    if let Some(dir) = parse_arg_value(&args, "--capture-ui-dir") {
        enable_per_monitor_dpi_v2();
        let _ = unsafe { CoInitializeEx(None, COINIT_APARTMENTTHREADED) };
        if let Err(e) = capture_suite::run_visual_capture_suite(Path::new(&dir)) {
            eprintln!("capture suite failed: {e}");
            std::process::exit(1);
        }
        return;
    }

    // 4. Single-Instance Guard
    let _mutex_guard = unsafe {
        let h = CreateMutexW(None, true, w!("Global\\ProjectAuroraV3SingleInstance")).ok();
        if GetLastError() == ERROR_ALREADY_EXISTS {
            return;
        }
        h
    };

    enable_per_monitor_dpi_v2();
    let _ = unsafe { CoInitializeEx(None, COINIT_APARTMENTTHREADED) };

    let smoke_exit_secs = parse_arg_value(&args, "--smoke-exit-after-seconds")
        .and_then(|s| s.parse::<u64>().ok());
    let open_studio = args.iter().any(|a| a == "--studio");
    let force_safe_mode = args.iter().any(|a| a == "--safe-mode");

    if let Err(e) = run_shell_host(smoke_exit_secs, open_studio, force_safe_mode) {
        let logger = PrivacySafeLogger::new(None);
        logger.error("APP", "FATAL", &format!("Aurora v{AURORA_VERSION} exit: {e}"));
        restore_taskbar_standalone();
    }
}

fn parse_arg_value(args: &[String], flag: &str) -> Option<String> {
    for (i, a) in args.iter().enumerate() {
        if let Some(rest) = a.strip_prefix(&format!("{flag}=")) {
            return Some(rest.to_string());
        }
        if a == flag {
            return args.get(i + 1).cloned();
        }
    }
    None
}

fn trim_process_working_set() {
    use std::sync::atomic::{AtomicU64, Ordering};
    use windows::Win32::System::Diagnostics::ToolHelp::{
        CreateToolhelp32Snapshot, Process32FirstW, Process32NextW, PROCESSENTRY32W,
        TH32CS_SNAPPROCESS,
    };
    use windows::Win32::System::ProcessStatus::K32EmptyWorkingSet;
    use windows::Win32::System::SystemInformation::GetTickCount64;
    use windows::Win32::System::Threading::{
        GetCurrentProcess, PROCESS_QUERY_LIMITED_INFORMATION, PROCESS_SET_QUOTA,
    };

    static LAST_TREE_TRIM_MS: AtomicU64 = AtomicU64::new(0);
    unsafe {
        let cur = GetCurrentProcess();
        let _ = K32EmptyWorkingSet(cur);

        // Gently trim cold working-set pages of Aurora's OWN child msedgewebview2.exe tree
        // (debounced to at most once per 25s; NEVER touches external apps or browsers!).
        let now = GetTickCount64();
        let prev = LAST_TREE_TRIM_MS.load(Ordering::Relaxed);
        if prev != 0 && now.saturating_sub(prev) < 25_000 {
            return;
        }
        LAST_TREE_TRIM_MS.store(now, Ordering::Relaxed);

        let Ok(snap) = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0) else { return };
        let mut children: HashMap<u32, Vec<(u32, String)>> = HashMap::new();
        let mut pe = PROCESSENTRY32W {
            dwSize: std::mem::size_of::<PROCESSENTRY32W>() as u32,
            ..Default::default()
        };
        if Process32FirstW(snap, &mut pe).is_ok() {
            loop {
                let len = pe.szExeFile.iter().position(|&c| c == 0).unwrap_or(pe.szExeFile.len());
                let name = String::from_utf16_lossy(&pe.szExeFile[..len]).to_ascii_lowercase();
                children.entry(pe.th32ParentProcessID).or_default().push((pe.th32ProcessID, name));
                if Process32NextW(snap, &mut pe).is_err() {
                    break;
                }
            }
        }
        let _ = CloseHandle(snap);

        let mut queue = vec![std::process::id()];
        let mut visited = HashSet::new();
        visited.insert(std::process::id());
        while let Some(pid) = queue.pop() {
            if let Some(kids) = children.get(&pid) {
                for (child_pid, child_name) in kids {
                    if !visited.insert(*child_pid) {
                        continue;
                    }
                    if child_name == "msedgewebview2.exe" {
                        if let Ok(hp) = OpenProcess(
                            PROCESS_SET_QUOTA | PROCESS_QUERY_LIMITED_INFORMATION,
                            false,
                            *child_pid,
                        ) {
                            let _ = K32EmptyWorkingSet(hp);
                            let _ = CloseHandle(hp);
                        }
                        queue.push(*child_pid);
                    }
                }
            }
        }
    }
}

fn run_watchdog(parent_pid: u32) {
    trim_process_working_set();
    unsafe {
        if let Ok(handle) = OpenProcess(PROCESS_SYNCHRONIZE, false, parent_pid) {
            let wait = WaitForSingleObject(handle, INFINITE);
            let _ = CloseHandle(handle);
            if wait == WAIT_OBJECT_0 {
                let clean_flag = aurora_data_dir().join("clean_exit.flag");
                if !clean_flag.exists() {
                    restore_taskbar_standalone();
                } else {
                    let _ = std::fs::remove_file(clean_flag);
                }
            }
        }
    }
}

fn spawn_watchdog() {
    use std::process::Stdio;
    if let Ok(exe) = std::env::current_exe() {
        let pid = std::process::id().to_string();
        let _ = std::process::Command::new(exe)
            .args(["--watchdog-pid", &pid])
            .stdin(Stdio::null())
            .stdout(Stdio::null())
            .stderr(Stdio::null())
            .spawn();
    }
}

fn materialize_ui_assets() -> anyhow::Result<PathBuf> {
    let ui_dir = aurora_root_dir().join("cache").join("ui");
    std::fs::create_dir_all(&ui_dir)?;
    write_if_changed(&ui_dir.join("crown.html"), CROWN_HTML)?;
    write_if_changed(&ui_dir.join("dock.html"), DOCK_HTML)?;
    write_if_changed(&ui_dir.join("widgets.html"), WIDGETS_HTML)?;
    Ok(ui_dir)
}

fn write_if_changed(path: &Path, content: &str) -> anyhow::Result<()> {
    if let Ok(existing) = std::fs::read_to_string(path) {
        if existing == content {
            return Ok(());
        }
    }
    std::fs::write(path, content)?;
    Ok(())
}

fn compute_crown_bounds(mon: &LiveMonitorHandle) -> PhysicalRect {
    let b = mon.descriptor.bounds;
    let scale = mon.descriptor.scale_factor().max(1.0);
    let w = ((1260.0 * scale).round() as i32).min(b.width - 12).max(360);
    let h = b.height.max(320);
    let x = b.x + (b.width - w) / 2;
    let y = b.y;
    PhysicalRect { x, y, width: w, height: h }
}

fn compute_dock_bounds(
    mon: &LiveMonitorHandle,
    edge: &str,
    native_tb_hidden: bool,
) -> PhysicalRect {
    let b = mon.descriptor.bounds;
    let wa = mon.descriptor.work_area;
    let scale = mon.descriptor.scale_factor().max(1.0);
    match edge {
        "Left" => {
            let w = ((460.0 * scale).round() as i32).min(b.width / 2).max(260);
            PhysicalRect { x: b.x, y: b.y, width: w, height: b.height.max(320) }
        }
        "Right" => {
            let w = ((460.0 * scale).round() as i32).min(b.width / 2).max(260);
            PhysicalRect { x: b.right() - w, y: b.y, width: w, height: b.height.max(320) }
        }
        _ => {
            let h = ((450.0 * scale).round() as i32).min(b.height / 2).max(260);
            let bottom_anchor = if native_tb_hidden { b.bottom() } else { wa.bottom() };
            PhysicalRect {
                x: b.x,
                y: bottom_anchor - h,
                width: b.width.max(320),
                height: h,
            }
        }
    }
}

fn sync_taskbar_and_appbar(
    appbar: &mut NativeAppBarManager,
    state: &RuntimeState,
    peek_active: bool,
) {
    let hide_tb = state.prefs.hide_native_taskbar_while_active && !peek_active;
    if hide_tb {
        if !appbar.is_native_taskbar_suppressed() {
            appbar.hide_native_taskbar();
        } else {
            appbar.enforce_native_taskbar_suppressed(None);
        }
        let reserve = state.prefs.effective_reserve_dock_work_area()
            && state.prefs.effective_dock_mode() == "AlwaysVisible";
        let edge = state.prefs.effective_dock_edge();
        let density = state.prefs.effective_dock_density();
        let arch = state.prefs.effective_shell_architecture();
        let changed = appbar.apply_dock_reservation(
            edge,
            state.prefs.dock_scale_percent,
            reserve,
            density,
            arch,
            false,
        );
        if changed {
            appbar.refit_mismatched_maximized_windows_once();
        }
    } else if appbar.is_native_taskbar_suppressed() {
        appbar.restore_native_taskbar();
    }
}

fn create_dock_surfaces(
    env: &ICoreWebView2Environment,
    ui_dir: &Path,
    monitors: &[LiveMonitorHandle],
    edge: &str,
    native_tb_hidden: bool,
    tx: &Sender<SurfaceMessage>,
    wake_hwnd: isize,
) -> Vec<WebViewOverlaySurface> {
    let mut docks = Vec::new();
    for (idx, mon) in monitors.iter().enumerate() {
        let bounds = compute_dock_bounds(mon, edge, native_tb_hidden);
        if let Ok(surf) = WebViewOverlaySurface::create(
            idx + 1,
            mon.descriptor.device_id.clone(),
            false,
            env,
            ui_dir,
            "dock.html",
            bounds,
            mon.descriptor.scale_factor() as f32,
            tx.clone(),
            Some(wake_hwnd),
            true,
        ) {
            docks.push(surf);
        }
    }
    docks
}

fn run_shell_host(
    smoke_exit_secs: Option<u64>,
    open_studio: bool,
    force_safe_mode: bool,
) -> anyhow::Result<()> {
    let logger = PrivacySafeLogger::new(None);
    let _ = std::fs::remove_file(aurora_data_dir().join("clean_exit.flag"));

    let sentinel = StartupCrashSentinel::new(None);
    let sentinel_safe = sentinel.record_boot_and_check_safe_mode();
    let safe_mode = force_safe_mode || sentinel_safe;

    let prefs_store = ShellPreferencesStore::new(None);
    let mut prefs = prefs_store.load();
    if safe_mode {
        prefs.hide_native_taskbar_while_active = false;
        prefs.reserve_dock_work_area = Some(false);
        if force_safe_mode {
            logger.warn("BOOT", "SAFE_MODE", "launched with --safe-mode flag");
        } else {
            logger.warn("BOOT", "SAFE_MODE", "3 unclean boots detected; entered safe mode");
        }
    }

    let aurora_db = AuroraDb::open(None);
    let clip_db = ClipboardVaultDb::open(None);
    let aux_store = AuxiliaryStateStore::new(None);
    let widgets_store = WidgetsLayoutStore::new(None);

    let user_prefs = aurora_db.load_user_preferences();
    let aux = aux_store.load();
    let wdoc = widgets_store.load();
    let profiles = aurora_db.load_workspace_profiles();
    let pinned = aurora_db.load_pinned_shortcuts();
    let clips = clip_db.list("all", "", 60);

    let mut state = RuntimeState::new(
        user_prefs,
        prefs,
        profiles,
        pinned,
        wdoc.widgets,
        wdoc.dock_widgets,
        wdoc.todos,
        wdoc.countdown,
        wdoc.ai_configs,
        clips,
        aux.drop_shelf_paths,
        aux.agenda_notes,
        aux.scratchpad_text,
        aux.search_frecency,
    );

    if smoke_exit_secs.is_none() {
        spawn_watchdog();
    }

    // 1. Write embedded 2026 HTML/CSS/JS UI templates into local cache for virtual host `https://aurora.local/`
    let ui_dir = materialize_ui_assets()?;
    let wv2_data_dir = aurora_root_dir().join("cache").join("wv2_rs");
    let wv_env = create_shared_webview_env(&wv2_data_dir)?;

    // 2. Event Channels & Win32 Services
    let (surf_tx, surf_rx) = unbounded::<SurfaceMessage>();
    let (hook_tx, hook_rx) = bounded::<ShellHookEvent>(192);
    let (key_tx, key_rx) = bounded::<LowLevelKeyEvent>(64);
    let (vol_tx, vol_rx) = bounded::<(u8, bool)>(64);
    let (bg_tx, bg_rx_cmd) = unbounded::<BgCommand>();
    let (reply_tx, reply_rx) = unbounded::<BgReply>();

    for w in &state.widgets {
        if let Some(ref p) = w.photo_path {
            if !p.trim().is_empty() {
                let _ = bg_tx.try_send(BgCommand::LoadWidgetPhoto {
                    widget_id: w.id.clone(),
                    path: p.clone(),
                });
            }
        }
    }

    let mut monitors = enumerate_monitors();
    if monitors.is_empty() {
        anyhow::bail!("No active displays detected");
    }
    let mut appbar = NativeAppBarManager::new();
    let mut audio = CoreAudioController::new();
    state.audio = audio.get_snapshot();
    // Sample real laptop battery & AC power immediately so Frame 1 shows exact % (never stale 100%)
    state.metrics = MetricsSampler::new().sample(false);
    let brightness = Arc::new(BrightnessController::new());
    let mut qr_server = LanQrShareServer::new();
    let mut dwm_preview = DwmLivePreviewWindow::create();

    set_alt_drag_enabled(state.prefs.alt_drag_enabled);
    set_snap_zones_enabled(state.prefs.snap_zones_enabled);

    // 3. Apply initial Windows taskbar lockdown & AppBar BEFORE positioning docks so work areas settle!
    sync_taskbar_and_appbar(&mut appbar, &state, false);
    monitors = enumerate_monitors();

    // 4. Create Primary Crown (1260x640) + Per-Monitor Docks (450px/460px) sharing ONE WebView2 renderer!
    let mut primary_mon = monitors[0].clone();
    let crown_bounds = compute_crown_bounds(&primary_mon);
    let mut crown = WebViewOverlaySurface::create(
        0,
        primary_mon.descriptor.device_id.clone(),
        true,
        &wv_env,
        &ui_dir,
        "crown.html",
        crown_bounds,
        primary_mon.descriptor.scale_factor() as f32,
        surf_tx.clone(),
        None,
        false,
    )?;
    let wake_hwnd = crown.hwnd.0 as isize;
    let mut docks = create_dock_surfaces(
        &wv_env,
        &ui_dir,
        &monitors,
        state.prefs.effective_dock_edge(),
        appbar.is_native_taskbar_suppressed(),
        &surf_tx,
        wake_hwnd,
    );

    // 5. Install CoreAudio callback, Global Hotkeys, WinEvent/Mouse hooks, and zero-stick LL Keyboard Hook!
    audio.start_volume_callback(vol_tx, wake_hwnd);
    let mut hk_results = register_aurora_hotkeys(crown.hwnd, &state.prefs.omni_hotkey_preset);
    let omni_hk_ok = hk_results.iter().any(|&(name, ok)| name == "OmniSearch" && ok);
    let win_hooks = ShellEventHooks::install(hook_tx, wake_hwnd);
    let mut key_hook = LowLevelKeyboardHook::new();
    let kbd_hook_ok = key_hook.start(
        key_tx,
        wake_hwnd,
        state.prefs.hardware_osd_enabled,
        state.prefs.win_key_opens_omni_search,
        !omni_hk_ok,
    );

    // 6. Auto-heal any corrupted/backslash-stripped pins on startup, then pre-populate open windows & dock groups!
    {
        let healed = state.heal_pinned_shortcuts();
        if !healed.is_empty() {
            aurora_db.save_pinned_shortcuts(&state.pinned_shortcuts);
            for (name, old_p, new_p) in &healed {
                logger.info(
                    "BOOT",
                    "PIN_AUTO_HEALED",
                    &format!("Repaired pinned shortcut '{name}': '{old_p}' -> '{new_p}'"),
                );
            }
        }
        let mut own_hwnds: HashSet<isize> = HashSet::new();
        own_hwnds.insert(crown.hwnd.0 as isize);
        for d in &docks {
            own_hwnds.insert(d.hwnd.0 as isize);
        }
        let wins = enumerate_open_windows(&own_hwnds, &state.attention_procs);
        update_stable_orders(
            &wins,
            &state.pinned_shortcuts,
            &mut state.running_group_order,
            &mut state.window_seen_order,
        );
        let groups = build_dock_groups_ordered(
            &state.pinned_shortcuts,
            &wins,
            None,
            &state.running_group_order,
            &state.window_seen_order,
        );
        for g in &groups {
            if !state.icon_png_b64.contains_key(&g.group_key)
                && state.icon_requested.insert(g.group_key.clone())
            {
                let _ = bg_tx.try_send(BgCommand::ExtractIconPng {
                    key: g.group_key.clone(),
                    hwnd: g.windows.first().map(|w| w.hwnd),
                    exe_path: g.executable_path.clone(),
                    aumid: g.aumid.clone(),
                });
            }
        }
        state.open_windows = wins;
        state.dock_groups = groups;
    }

    // 7. Spawn non-blocking background workers (app catalog, file search, folder stacks, telemetry, OCR/GIF)
    let notif_mirror_active = Arc::new(AtomicBool::new(
        state.prefs.effective_notification_mode() == "aurora-mirror"
            && state.prefs.system_notification_bridge_enabled,
    ));
    spawn_background_workers(
        bg_rx_cmd,
        reply_tx,
        wake_hwnd,
        brightness.clone(),
        notif_mirror_active.clone(),
    );

    let start_instant = Instant::now();
    let mut last_clock_sec = chrono::Local::now().timestamp();
    let mut last_window_scan = Instant::now() - Duration::from_secs(10);
    let mut last_tb_guard = Instant::now();
    let mut last_edge_wake = Instant::now();
    let mut gif_stop_flag: Option<Arc<AtomicBool>> = None;
    let mut transient_overlay: Option<TransientD2DOverlay> = None;
    let mut peek_tray_until_ms: u64 = 0;
    let mut popup_opened_at_ms: u64 = 0;
    let mut prev_crown_interactive = false;
    let mut prev_watch_clicks = false;
    let mut crown_ever_focused = false;
    let mut popup_opened_prev_fg: isize = 0;
    let mut last_omni_toggle_ms: u64 = 0;
    let mut crown_ready = false;
    let mut crown_nav_done = false;
    let mut dock_ready: HashSet<usize> = HashSet::new();
    let mut dock_ui_ready: HashSet<usize> = HashSet::new();
    let mut dock_nav_started: HashMap<usize, Instant> = HashMap::new();
    let mut dock_reload_attempts: HashMap<usize, u8> = HashMap::new();
    let mut widgets_surface: Option<WebViewOverlaySurface> = None;
    let mut widgets_ready = false;
    let mut widgets_ui_ready = false;
    let mut widgets_nav_started: Option<Instant> = None;
    let mut widgets_reload_attempts: u8 = 0;
    let mut last_widgets_json = String::new();
    let mut last_ai_quota_refresh = Instant::now() - Duration::from_secs(120);
    let mut last_market_refresh = Instant::now() - Duration::from_secs(600);
    let mut last_local_ai_refresh = Instant::now() - Duration::from_secs(60);
    let mut last_day_night_check = Instant::now() - Duration::from_secs(120);
    let mut post_display_trim_at: Option<Instant> = None;
    let mut snap_mon_bounds = primary_mon.descriptor.bounds;
    let mut snap_mon_scale = primary_mon.descriptor.scale_factor() as f32;
    let mut snap_work_area = primary_mon.descriptor.work_area;
    let mut last_crown_json = String::new();
    let mut last_dock_json: HashMap<String, String> = HashMap::new();
    let mut trimmed_boot_ws = false;
    let mut trimmed_settled_ws = false;
    let mut pending_open_studio = open_studio;
    let mut pending_open_tour =
        !state.prefs.onboarding_completed && smoke_exit_secs.is_none() && !open_studio;
    let mut should_exit = false;

    let hk_summary: String = hk_results
        .iter()
        .map(|(n, ok)| format!("{n}:{ok}"))
        .collect::<Vec<_>>()
        .join(",");
    logger.info(
        "BOOT",
        "V4_READY",
        &format!(
            "Project Aurora v{AURORA_VERSION} Hybrid-Rust started on {} monitor(s); crown=({},{},{}x{}) docks={} widgets={} mode={} reserve={} tb_hidden={} kbd_hook={} mouse_hook={} hotkeys=[{}]",
            monitors.len(),
            crown_bounds.x,
            crown_bounds.y,
            crown_bounds.width,
            crown_bounds.height,
            docks.len(),
            state.widgets.len(),
            state.prefs.effective_dock_mode(),
            state.prefs.effective_reserve_dock_work_area(),
            appbar.is_native_taskbar_suppressed(),
            kbd_hook_ok,
            win_hooks.mouse_hook_ok,
            hk_summary,
        ),
    );

    // ═══════════ EVENT-DRIVEN ZERO-POLLING MAIN LOOP ═══════════
    while !should_exit {
        let now_ms = start_instant.elapsed().as_millis() as u64;
        if let Some(secs) = smoke_exit_secs {
            if now_ms >= secs * 1000 {
                break;
            }
        }

        // 1. Pump all pending Win32 messages
        unsafe {
            let mut msg = MSG::default();
            while PeekMessageW(&mut msg, None, 0, 0, PM_REMOVE).as_bool() {
                let _ = TranslateMessage(&msg);
                DispatchMessageW(&msg);
            }
        }

        let mut needs_taskbar_sync = false;
        let mut needs_bounds_sync = false;
        let mut needs_hotkey_sync = false;
        let mut needs_widgets_sync = false;
        let mut needs_state_push = false;
        let mut needs_catalog_push = false;

        // 2. Drain CoreAudio instant volume callbacks (<1ms USB/BT headset wheel response!)
        while let Ok((pct, muted)) = vol_rx.try_recv() {
            let changed =
                state.audio.master_volume_percent != pct || state.audio.is_master_muted != muted;
            state.audio.master_volume_percent = pct;
            state.audio.is_master_muted = muted;
            if changed {
                if state.prefs.hardware_osd_enabled && crown_ready {
                    post_osd_json(
                        &crown,
                        if muted { "mute" } else { "volume" },
                        if muted { "Sessiz" } else { "Ses" },
                        if muted { "KAPALI".into() } else { format!("{pct}%") },
                        Some(pct),
                        !muted,
                        1500,
                    );
                }
                needs_state_push = true;
            }
        }

        // 3. Drain Low-Level Hardware Keys (Volume, Caps/Num Lock, Single Win Tap, Alt+Space fallback)
        while let Ok(kev) = key_rx.try_recv() {
            match kev {
                LowLevelKeyEvent::VolumeKey(vk) => {
                    let (pct, muted) = if vk == 0xAD {
                        let muted = audio.toggle_master_mute();
                        state.audio.is_master_muted = muted;
                        (state.audio.master_volume_percent, muted)
                    } else {
                        let next = compute_next_volume_step(
                            state.audio.master_volume_percent,
                            vk == 0xAF,
                        );
                        audio.set_master_volume(next);
                        state.audio.master_volume_percent = next;
                        state.audio.is_master_muted = false;
                        (next, false)
                    };
                    if state.prefs.hardware_osd_enabled && crown_ready {
                        post_osd_json(
                            &crown,
                            if muted { "mute" } else { "volume" },
                            if muted { "Sessiz" } else { "Ses" },
                            if muted { "KAPALI".into() } else { format!("{pct}%") },
                            Some(pct),
                            !muted,
                            1500,
                        );
                    }
                    needs_state_push = true;
                }
                LowLevelKeyEvent::ToggleIndicatorKey { vk, next_on } => {
                    if state.prefs.hardware_osd_enabled && crown_ready {
                        let label = if vk == 0x14 { "Caps Lock" } else { "Num Lock" };
                        let val = if next_on { "AÇIK" } else { "KAPALI" };
                        post_osd_json(&crown, "toggle", label, val.into(), None, next_on, 1300);
                    }
                }
                LowLevelKeyEvent::SingleWinKeyTapped | LowLevelKeyEvent::OmniHotkeyTapped => {
                    if now_ms.saturating_sub(last_omni_toggle_ms) >= 220 {
                        last_omni_toggle_ms = now_ms;
                        popup_opened_at_ms = now_ms;
                        crown.focus_and_activate();
                        crown.post_json(r#"{"type":"toggleOmni"}"#);
                    }
                }
            }
        }

        // 4. Drain WinEvent / Mouse Shell Hooks (instant taskbar lockdown + window catalog + Shift+Drag Snap!)
        let mut catalog_changed = false;
        while let Ok(hev) = hook_rx.try_recv() {
            match hev {
                ShellHookEvent::ObjectShown { hwnd } => {
                    let peek_active = peek_tray_until_ms > now_ms;
                    if state.prefs.hide_native_taskbar_while_active && !peek_active {
                        appbar.enforce_native_taskbar_suppressed(Some(HWND(hwnd as *mut _)));
                    }
                    let mon_rects: Vec<PhysicalRect> =
                        monitors.iter().map(|m| m.descriptor.bounds).collect();
                    let suppress_sys_notif = state.prefs.effective_notification_mode() == "aurora-mirror"
                        && state.prefs.system_notification_bridge_enabled;
                    let _ = try_suppress_win11_shell_overlay(
                        HWND(hwnd as *mut _),
                        &mon_rects,
                        state.prefs.hardware_osd_enabled,
                        suppress_sys_notif,
                    );
                }
                ShellHookEvent::WindowCatalogChanged { foreground_hwnd } => {
                    catalog_changed = true;
                    let is_own = foreground_hwnd == crown.hwnd.0 as isize
                        || is_window_owned_by(foreground_hwnd, crown.hwnd)
                        || docks.iter().any(|d| {
                            d.hwnd.0 as isize == foreground_hwnd
                                || is_window_owned_by(foreground_hwnd, d.hwnd)
                        })
                        || widgets_surface
                            .as_ref()
                            .is_some_and(|w| {
                                w.hwnd.0 as isize == foreground_hwnd
                                    || is_window_owned_by(foreground_hwnd, w.hwnd)
                            })
                        || transient_overlay
                            .as_ref()
                            .is_some_and(|o| o.win.hwnd.0 as isize == foreground_hwnd);
                    if !is_own && foreground_hwnd != 0 {
                        state.last_external_hwnd = foreground_hwnd;
                        if crown.is_interactive()
                            && now_ms.saturating_sub(popup_opened_at_ms) >= 200
                            && (crown_ever_focused
                                || (popup_opened_prev_fg != 0
                                    && foreground_hwnd != popup_opened_prev_fg))
                        {
                            crown.release_interactive();
                            crown.post_json(r#"{"type":"outsideDeactivate"}"#);
                            state.crown_stage_open = false;
                            crown_ever_focused = false;
                            popup_opened_prev_fg = 0;
                            prev_crown_interactive = false;
                        } else if state.crown_stage_open {
                            state.crown_stage_open = false;
                            crown.post_json(r#"{"type":"outsideDeactivate"}"#);
                        }
                        if !state.dock_popover_open_mons.is_empty() {
                            state.dock_popover_open_mons.clear();
                        }
                        for d in &mut docks {
                            d.release_inline_input();
                            d.post_json(r#"{"type":"outsideDeactivate"}"#);
                        }
                        if let Some(ws) = widgets_surface.as_mut() {
                            ws.release_inline_input();
                        }
                        if let Some(prev) = dwm_preview.as_mut() {
                            prev.hide();
                        }
                    }
                }
                ShellHookEvent::MoveSizeStarted { .. } => {}
                ShellHookEvent::ShiftDragMoved { hwnd, px, py } => {
                    if state.prefs.snap_zones_enabled {
                        state.snap_overlay_active = true;
                        state.active_snap_hwnd = hwnd;
                        let target_mon = monitor_at_point(&monitors, px, py).unwrap_or(&primary_mon);
                        snap_mon_bounds = target_mon.descriptor.bounds;
                        snap_mon_scale = target_mon.descriptor.scale_factor() as f32;
                        snap_work_area = target_mon.descriptor.work_area;
                        let zones = compute_snap_zones(snap_work_area, state.prefs.effective_snap_preset(), 10);
                        state.snap_hover_idx = zones.iter().position(|(_, z)| {
                            px >= z.x && px <= z.right() && py >= z.y && py <= z.bottom()
                        });
                    }
                }
                ShellHookEvent::ShiftDragDropped { hwnd, px, py } => {
                    if state.snap_overlay_active {
                        let wa = monitor_at_point(&monitors, px, py)
                            .map(|m| m.descriptor.work_area)
                            .unwrap_or(snap_work_area);
                        let zones = compute_snap_zones(wa, state.prefs.effective_snap_preset(), 10);
                        if let Some((_, z)) = zones.iter().find(|(_, z)| {
                            px >= z.x && px <= z.right() && py >= z.y && py <= z.bottom()
                        }) {
                            snap_window_to_rect(hwnd, *z);
                            catalog_changed = true;
                        }
                        state.snap_overlay_active = false;
                        state.snap_hover_idx = None;
                        state.active_snap_hwnd = 0;
                    }
                }
                ShellHookEvent::MoveSizeEnded { .. } => {
                    if state.snap_overlay_active {
                        state.snap_overlay_active = false;
                        state.snap_hover_idx = None;
                        state.active_snap_hwnd = 0;
                    }
                    catalog_changed = true;
                }
                ShellHookEvent::AltDragMove { hwnd, x, y, w, h } => {
                    if hwnd != 0 {
                        unsafe {
                            let _ = SetWindowPos(
                                HWND(hwnd as *mut _),
                                None,
                                x,
                                y,
                                w.max(240),
                                h.max(160),
                                SWP_NOACTIVATE | SWP_NOZORDER | SWP_ASYNCWINDOWPOS,
                            );
                        }
                    }
                }
                ShellHookEvent::ExternalMouseClick { px, py } => {
                    if (crown.is_interactive() || state.crown_stage_open)
                        && (!crown.is_interactive()
                            || now_ms.saturating_sub(popup_opened_at_ms) >= 180)
                        && !is_point_inside_window_or_owned(px, py, crown.hwnd)
                    {
                        if crown.is_interactive() {
                            crown.release_interactive();
                        }
                        crown.post_json(r#"{"type":"outsideDeactivate"}"#);
                        state.crown_stage_open = false;
                        crown_ever_focused = false;
                        popup_opened_prev_fg = 0;
                        prev_crown_interactive = false;
                    }
                    if !state.dock_popover_open_mons.is_empty()
                        && now_ms.saturating_sub(state.dock_popover_opened_at_ms) >= 100
                        && !docks.iter().any(|d| is_point_inside_window_or_owned(px, py, d.hwnd))
                    {
                        state.dock_popover_open_mons.clear();
                        for d in &docks {
                            d.post_json(r#"{"type":"outsideDeactivate"}"#);
                        }
                        if let Some(prev) = dwm_preview.as_mut() {
                            prev.hide();
                        }
                    }
                }
            }
        }

        // 5. Drain WebView2 Surface Messages (Ready, Bridge Actions, Hotkeys, Deactivated, DisplayChanged)
        let mut coalesced_display_change = false;
        let mut coalesced_power_change = false;
        let mut coalesced_power_resume = false;
        while let Ok(smsg) = surf_rx.try_recv() {
            match smsg {
                SurfaceMessage::Diag { surface_id, detail } => {
                    logger.info("UI", "SURFACE_DIAG", &format!("surface={surface_id} {detail}"));
                }
                SurfaceMessage::Ready { surface_id, source } => {
                    state.reset_surface_icon_cache(surface_id);
                    if surface_id == 0 {
                        if source == "nav" {
                            crown_nav_done = true;
                        }
                        if !crown_ready {
                            logger.info("UI", "SURFACE_READY", &format!("crown ready via {source}"));
                        }
                        crown_ready = true;
                        crown.post_json(&state.build_catalog_json_for_surface(0));
                        let j = state.build_crown_state_json_for_surface(
                            0,
                            &crown.monitor_id,
                            monitors.len(),
                            appbar.is_native_taskbar_suppressed(),
                            now_ms,
                        );
                        crown.post_json(&j);
                        last_crown_json = j;
                        crown.post_json(r#"{"type":"syncLayout"}"#);
                        if pending_open_studio {
                            pending_open_studio = false;
                            popup_opened_at_ms = now_ms;
                            crown.focus_and_activate();
                            crown.post_json(r#"{"type":"openSettings","section":"appearance"}"#);
                        } else if pending_open_tour {
                            pending_open_tour = false;
                            popup_opened_at_ms = now_ms;
                            crown.focus_and_activate();
                            crown.post_json(r#"{"type":"openTour","step":0}"#);
                        }
                    } else if surface_id == WIDGETS_SURFACE_ID {
                        if source == "uiReady" {
                            widgets_ui_ready = true;
                        }
                        let first_time = !widgets_ready;
                        widgets_ready = true;
                        if let Some(ws) = widgets_surface.as_ref() {
                            let j = state.build_widgets_state_json();
                            if first_time || source == "uiReady" {
                                logger.info(
                                    "UI",
                                    "SURFACE_READY",
                                    &format!(
                                        "widgets surface={surface_id} mon={} bounds=({},{},{}x{}) ready via {source} count={} payload_bytes={}",
                                        ws.monitor_id,
                                        ws.bounds_px.x,
                                        ws.bounds_px.y,
                                        ws.bounds_px.width,
                                        ws.bounds_px.height,
                                        state.widgets.len(),
                                        j.len()
                                    ),
                                );
                            }
                            ws.post_json(&j);
                            last_widgets_json = j;
                            ws.post_json(r#"{"type":"syncLayout"}"#);
                        }
                    } else {
                        if source == "uiReady" {
                            dock_ui_ready.insert(surface_id);
                        }
                        let first_time = dock_ready.insert(surface_id);
                        if let Some((idx, mon_id, bx, by, bw, bh)) = docks
                            .iter()
                            .enumerate()
                            .find(|(_, d)| d.surface_id == surface_id)
                            .map(|(i, d)| {
                                (
                                    i,
                                    d.monitor_id.clone(),
                                    d.bounds_px.x,
                                    d.bounds_px.y,
                                    d.bounds_px.width,
                                    d.bounds_px.height,
                                )
                            })
                        {
                            let cat_j = state.build_catalog_json_for_surface(surface_id);
                            let j = state.build_dock_state_json_for_surface(
                                surface_id,
                                &mon_id,
                                idx == 0,
                                now_ms,
                            );
                            if first_time || source == "uiReady" {
                                logger.info(
                                    "UI",
                                    "SURFACE_READY",
                                    &format!(
                                        "dock surface={surface_id} mon={mon_id} bounds=({bx},{by},{bw}x{bh}) ready via {source} payload_bytes={}",
                                        j.len()
                                    ),
                                );
                            }
                            if let Some(d) = docks.iter().find(|d| d.surface_id == surface_id) {
                                d.post_json(&cat_j);
                                d.post_json(&j);
                                last_dock_json.insert(mon_id, j);
                                d.post_json(r#"{"type":"syncLayout"}"#);
                            }
                        }
                    }
                }
                SurfaceMessage::BridgeAction {
                    monitor_id,
                    payload,
                    dropped_files,
                } => {
                    let mut env = BridgeEnv {
                        state: &mut state,
                        crown: &mut crown,
                        docks: &mut docks,
                        widgets_surface: &mut widgets_surface,
                        prefs_store: &prefs_store,
                        widgets_store: &widgets_store,
                        aurora_db: &aurora_db,
                        clip_db: &clip_db,
                        aux_store: &aux_store,
                        logger: &logger,
                        appbar: &mut appbar,
                        audio: &audio,
                        brightness: &brightness,
                        qr_server: &mut qr_server,
                        dwm_preview: &mut dwm_preview,
                        bg_tx: &bg_tx,
                        gif_stop_flag: &mut gif_stop_flag,
                        should_exit: &mut should_exit,
                        needs_taskbar_sync: &mut needs_taskbar_sync,
                        needs_bounds_sync: &mut needs_bounds_sync,
                        needs_hotkey_sync: &mut needs_hotkey_sync,
                        needs_widgets_sync: &mut needs_widgets_sync,
                        needs_state_push: &mut needs_state_push,
                        popup_opened_at_ms: &mut popup_opened_at_ms,
                        peek_tray_until_ms: &mut peek_tray_until_ms,
                        now_ms,
                    };
                    handle_bridge_action(&mut env, &monitor_id, &payload, &dropped_files);
                }
                SurfaceMessage::Hotkey { id } => match id {
                    HOTKEY_OMNI_SEARCH => {
                        if now_ms.saturating_sub(last_omni_toggle_ms) >= 220 {
                            last_omni_toggle_ms = now_ms;
                            popup_opened_at_ms = now_ms;
                            crown.focus_and_activate();
                            crown.post_json(r#"{"type":"toggleOmni"}"#);
                        }
                    }
                    HOTKEY_CROWN_DECK => {
                        popup_opened_at_ms = now_ms;
                        crown.focus_and_activate();
                        crown.post_json(r#"{"type":"toggleDeck","tab":"control"}"#);
                    }
                    HOTKEY_OCR_SNIP => {
                        state.active_snip_mode = Some(SnipMode::Ocr);
                    }
                    HOTKEY_EYEDROPPER => {
                        state.active_snip_mode = Some(SnipMode::Eyedropper);
                    }
                    HOTKEY_GIF_RECORDER => {
                        if state.gif_recording {
                            if let Some(flag) = gif_stop_flag.take() {
                                flag.store(true, std::sync::atomic::Ordering::Relaxed);
                            }
                            state.gif_recording = false;
                            crown.post_json(r#"{"type":"gifRecState","recording":false,"elapsed":0}"#);
                            needs_state_push = true;
                        } else {
                            state.active_snip_mode = Some(SnipMode::GifRegion);
                        }
                    }
                    _ => {}
                },
                SurfaceMessage::Deactivated { surface_id } => {
                    let fg = get_foreground_hwnd();
                    if surface_id == 0 {
                        if crown.is_interactive()
                            && now_ms.saturating_sub(popup_opened_at_ms) >= 200
                            && !is_window_owned_by(fg, crown.hwnd)
                        {
                            crown.release_interactive();
                            crown.post_json(r#"{"type":"outsideDeactivate"}"#);
                            state.crown_stage_open = false;
                            crown_ever_focused = false;
                            popup_opened_prev_fg = 0;
                            prev_crown_interactive = false;
                            trim_process_working_set();
                        }
                    } else if surface_id == WIDGETS_SURFACE_ID {
                        if let Some(ws) = widgets_surface.as_mut() {
                            if !is_window_owned_by(fg, ws.hwnd) {
                                ws.release_inline_input();
                            }
                        }
                    } else if let Some(d) = docks.iter_mut().find(|d| d.surface_id == surface_id) {
                        if !is_window_owned_by(fg, d.hwnd) {
                            d.release_inline_input();
                        }
                    }
                }
                SurfaceMessage::PowerChanged => {
                    coalesced_power_change = true;
                }
                SurfaceMessage::PowerResumed => {
                    coalesced_power_resume = true;
                    coalesced_power_change = true;
                    coalesced_display_change = true;
                }
                SurfaceMessage::ClipboardChanged => {
                    let _ = bg_tx.try_send(BgCommand::CheckClipboard);
                }
                SurfaceMessage::DisplayChanged | SurfaceMessage::TaskbarRecreated => {
                    coalesced_display_change = true;
                }
            }
        }
        if coalesced_power_change {
            let fresh = MetricsSampler::new().sample(false);
            state.metrics.battery_present = fresh.battery_present;
            state.metrics.battery_percent = fresh.battery_percent;
            state.metrics.is_charging = fresh.is_charging;
            needs_state_push = true;
        }
        if coalesced_power_resume {
            aurora_win32::set_awake_keep_alive(state.awake_mode != "off");
            let fresh_audio = audio.get_snapshot();
            if state.audio != fresh_audio {
                state.audio = fresh_audio;
            }
            let _ = bg_tx.try_send(BgCommand::RefreshWeather);
        }
        if coalesced_display_change {
            monitors = enumerate_monitors();
            if let Some(m0) = monitors.first() {
                primary_mon = m0.clone();
            }
            last_crown_json.clear();
            last_dock_json.clear();
            last_widgets_json.clear();
            crown.force_resync_region();
            for d in &mut docks {
                d.force_resync_region();
            }
            if let Some(ws) = widgets_surface.as_mut() {
                ws.force_resync_region();
            }
            post_display_trim_at = Some(Instant::now() + Duration::from_secs(5));
            let _ = bg_tx.try_send(BgCommand::RefreshSlowHardware);
            needs_taskbar_sync = true;
            needs_bounds_sync = true;
            catalog_changed = true;
            needs_state_push = true;
        }

        // 6. Drain Background Worker Replies
        while let Ok(rep) = reply_rx.try_recv() {
            match rep {
                BgReply::InstalledApps(apps) => {
                    logger.info(
                        "APPS",
                        "CATALOG_LOADED",
                        &format!("Loaded {} installed apps/settings/tools", apps.len()),
                    );
                    state.installed_apps = apps;
                    let healed = state.heal_pinned_shortcuts();
                    if !healed.is_empty() {
                        aurora_db.save_pinned_shortcuts(&state.pinned_shortcuts);
                        for (name, old_p, new_p) in &healed {
                            logger.info(
                                "APPS",
                                "PIN_AUTO_HEALED",
                                &format!("Catalog-healed pinned shortcut '{name}': '{old_p}' -> '{new_p}'"),
                            );
                        }
                        state.dock_groups = build_dock_groups_ordered(
                            &state.pinned_shortcuts,
                            &state.open_windows,
                            None,
                            &state.running_group_order,
                            &state.window_seen_order,
                        );
                    }
                    needs_catalog_push = true;
                    needs_state_push = true;
                }
                BgReply::Weather(w) => {
                    state.weather = w;
                    needs_state_push = true;
                }
                BgReply::AiQuota(snaps) => {
                    state.ai_snapshots = snaps;
                    needs_state_push = true;
                }
                BgReply::Market(m) => {
                    state.market = m;
                    needs_state_push = true;
                }
                BgReply::LocalAiAndDisks { local_ai, disks } => {
                    state.local_ai = local_ai;
                    state.disks = disks;
                    needs_state_push = true;
                }
                BgReply::WallpaperAccent(colors_opt) => match colors_opt {
                    Some((a1, a2)) => {
                        let key = state.theme_id.clone();
                        let map = state
                            .prefs
                            .theme_customizations
                            .get_or_insert_with(HashMap::new);
                        let entry: &mut ThemeColorCustomization = map.entry(key).or_default();
                        entry.accent_primary = Some(a1.clone());
                        entry.accent_secondary = Some(a2.clone());
                        prefs_store.save(&state.prefs);
                        post_toast_json(
                            &crown,
                            "Duvar Kağıdı Renkleri Uygulandı",
                            &format!("Ana: {a1} · İkincil: {a2} (WCAG AA+)"),
                            "",
                        );
                        needs_state_push = true;
                    }
                    None => {
                        post_toast_json(
                            &crown,
                            "Duvar Kağıdı Okunamadı",
                            "Mevcut duvar kağıdı düz renk veya erişilemez olabilir.",
                            "",
                        );
                    }
                },
                BgReply::Media(m) => {
                    let predicted_pos = if state.media.is_playing && state.media.timeline_updated_ms > 0 {
                        let elapsed = ((m.timeline_updated_ms - state.media.timeline_updated_ms).max(0) / 1000) as u32;
                        let p = state.media.position_secs.saturating_add(elapsed);
                        if state.media.duration_secs > 0 { p.min(state.media.duration_secs) } else { p }
                    } else {
                        state.media.position_secs
                    };
                    let timeline_changed = state.media.duration_secs != m.duration_secs
                        || state.media.can_seek != m.can_seek
                        || predicted_pos.abs_diff(m.position_secs) >= 3;
                    if state.media.title != m.title
                        || state.media.artist != m.artist
                        || state.media.app_name != m.app_name
                        || state.media.is_playing != m.is_playing
                        || state.media.has_session != m.has_session
                        || state.media.thumbnail_bytes.is_some() != m.thumbnail_bytes.is_some()
                        || timeline_changed
                    {
                        state.media = m;
                        needs_state_push = true;
                    }
                }
                BgReply::AudioSnapshot(snap) => {
                    if state.audio != snap {
                        state.audio = snap;
                        needs_state_push = true;
                    }
                }
                BgReply::Bluetooth(bt) => {
                    if state.bt_boot_initialized && state.prefs.island_bt_popin {
                        if let Some(new_dev) = bt.iter().find(|d| {
                            d.is_connected
                                && !state
                                    .bluetooth_devices
                                    .iter()
                                    .any(|prev| prev.is_connected && prev.id == d.id)
                        }) {
                            let msg_json = json!({
                                "type": "btPopin",
                                "name": new_dev.name,
                                "percent": new_dev.battery_percent,
                                "hasBattery": new_dev.has_battery,
                                "category": new_dev.category,
                            })
                            .to_string();
                            crown.post_json(&msg_json);
                        }
                    }
                    state.bt_boot_initialized = true;
                    state.bluetooth_devices = bt;
                    needs_state_push = true;
                }
                BgReply::WidgetPhotoLoaded { widget_id, path, data_uri } => {
                    state.widget_photos.insert(widget_id.clone(), data_uri);
                    if let Some(w) = state.widgets.iter_mut().find(|w| w.id == widget_id) {
                        if w.photo_path.as_deref() != Some(path.as_str()) {
                            w.photo_path = Some(path);
                            widgets_store.save(&WidgetsPersistenceDoc {
                                widgets: state.widgets.clone(),
                                dock_widgets: state.dock_widgets.clone(),
                                dock_widgets_schema_ver: 1,
                                todos: state.todos.clone(),
                                countdown: state.countdown.clone(),
                                ai_configs: state.ai_configs.clone(),
                            });
                        }
                    }
                    needs_state_push = true;
                }
                BgReply::Brightness(mb) => {
                    state.monitors_brightness = mb;
                    needs_state_push = true;
                }
                BgReply::Privacy(p) => {
                    if state.privacy.camera_active != p.camera_active
                        || state.privacy.mic_active != p.mic_active
                        || state.privacy.camera_apps != p.camera_apps
                        || state.privacy.mic_apps != p.mic_apps
                    {
                        state.privacy = p;
                        needs_state_push = true;
                    }
                }
                BgReply::Metrics(m) => {
                    state.cpu_history.push(m.cpu_percent);
                    if state.cpu_history.len() > 24 {
                        state.cpu_history.remove(0);
                    }
                    state.ram_history.push(m.ram_percent);
                    if state.ram_history.len() > 24 {
                        state.ram_history.remove(0);
                    }
                    state.net_history.push((m.net_down_mbps + m.net_up_mbps) * 125.0);
                    if state.net_history.len() > 24 {
                        state.net_history.remove(0);
                    }
                    let changed_battery = state.metrics.battery_percent != m.battery_percent
                        || state.metrics.is_charging != m.is_charging;
                    state.metrics = m;
                    if state.metrics_tab_active || changed_battery || state.dock_needs_live_metrics() {
                        needs_state_push = true;
                    }
                }
                BgReply::TrayApps(trays) => {
                    for t in &trays {
                        let stem = Path::new(&t.executable_path)
                            .file_stem()
                            .and_then(|s| s.to_str())
                            .map(|s| s.to_ascii_lowercase())
                            .unwrap_or_default();
                        let icon_key = if stem.is_empty() { t.id.clone() } else { stem.clone() };
                        if !state.icon_png_b64.contains_key(&icon_key)
                            && state.icon_requested.insert(icon_key.clone())
                        {
                            let aumid = if aurora_win32::is_windows_apps_sandbox_path(&t.executable_path) {
                                state
                                    .installed_apps
                                    .iter()
                                    .find(|a| {
                                        let lt = a.launch_path.to_ascii_lowercase();
                                        lt.starts_with("appsfolder:")
                                            && (a.name.eq_ignore_ascii_case(&t.name)
                                                || lt.contains(&stem))
                                    })
                                    .and_then(|a| {
                                        a.launch_path
                                            .strip_prefix("appsfolder:")
                                            .or_else(|| a.launch_path.strip_prefix("APPSFOLDER:"))
                                            .map(|s| s.to_string())
                                    })
                            } else {
                                None
                            };
                            let _ = bg_tx.try_send(BgCommand::ExtractIconPng {
                                key: icon_key,
                                hwnd: None,
                                exe_path: t.executable_path.clone(),
                                aumid,
                            });
                        }
                    }
                    let changed = state.tray_apps.len() != trays.len()
                        || state
                            .tray_apps
                            .iter()
                            .zip(trays.iter())
                            .any(|(a, b)| a.id != b.id || a.memory_mb != b.memory_mb);
                    if changed {
                        state.tray_apps = trays;
                        needs_state_push = true;
                    }
                }
                BgReply::FreshNotifications(fresh) => {
                    if state.prefs.effective_notification_mode() != "aurora-mirror"
                        || !state.prefs.system_notification_bridge_enabled
                    {
                        continue;
                    }
                    let mut added_any = false;
                    for n in fresh {
                        if state.dismissed_sys_notif_ids.contains(&n.notification_id)
                            || state
                                .notifications
                                .iter()
                                .any(|e| e.notification_id == n.notification_id)
                        {
                            continue;
                        }
                        if state.prefs.system_notification_bridge_enabled
                            && !state.focus_mode_enabled
                        {
                            state.unread_notif_count = state.unread_notif_count.saturating_add(1);
                            let body = if n.body.is_empty() {
                                n.app_name.clone()
                            } else {
                                format!("{} — {}", n.app_name, n.body)
                            };
                            post_toast_json(
                                &crown,
                                &n.title,
                                &body,
                                &n.notification_id.to_string(),
                            );
                        }
                        state.notifications.insert(0, n);
                        added_any = true;
                    }
                    state.notifications.truncate(30);
                    if added_any {
                        needs_state_push = true;
                    }
                }
                BgReply::ClipboardText { text } => {
                    let src = state
                        .open_windows
                        .iter()
                        .find(|w| w.is_active)
                        .map(|w| w.process_name.as_str());
                    if clip_db.record_text(&text, src).is_some() {
                        state.clipboard_items = clip_db.list("all", "", 60);
                        needs_state_push = true;
                    }
                }
                BgReply::ClipboardImage { width, height, png_bytes, thumb_b64 } => {
                    let src = state
                        .open_windows
                        .iter()
                        .find(|w| w.is_active)
                        .map(|w| w.process_name.as_str());
                    if clip_db
                        .record_image(&png_bytes, Some(&thumb_b64), width, height, src)
                        .is_some()
                    {
                        state.clipboard_items = clip_db.list("all", "", 60);
                        needs_state_push = true;
                    }
                }
                BgReply::FileSearch { seq, query, files } => {
                    let j = json!({
                        "type": "fileSearchResults",
                        "seq": seq,
                        "query": query,
                        "files": files,
                    })
                    .to_string();
                    crown.post_json(&j);
                }
                BgReply::FolderStackReady { monitor_id, path, name, items } => {
                    let j = json!({
                        "type": "folderStackItems",
                        "path": path,
                        "name": name,
                        "items": items,
                    })
                    .to_string();
                    let mut sent = false;
                    for d in &docks {
                        if d.monitor_id.eq_ignore_ascii_case(&monitor_id) {
                            d.post_json(&j);
                            sent = true;
                        }
                    }
                    if !sent {
                        for d in &docks {
                            d.post_json(&j);
                        }
                    }
                }
                BgReply::DockFolderPicked(path) => {
                    let list = state.prefs.dock_folder_paths.get_or_insert_with(Vec::new);
                    if !list.iter().any(|p| p.eq_ignore_ascii_case(&path)) {
                        list.push(path.clone());
                        prefs_store.save(&state.prefs);
                        post_toast_json(
                            &crown,
                            "Dock Klasörü Eklendi",
                            &format!("{} alt Dock'a sabitlendi.", friendly_folder_name(&path)),
                            "",
                        );
                        needs_state_push = true;
                    }
                }
                BgReply::ShelfFilePicked { path, share_qr_after } => {
                    if !state.drop_shelf_paths.contains(&path) {
                        state.drop_shelf_paths.insert(0, path.clone());
                        state.drop_shelf_paths.truncate(16);
                        aux_store.save_drop_shelf(&state.drop_shelf_paths);
                    }
                    if share_qr_after {
                        let mut env = BridgeEnv {
                            state: &mut state,
                            crown: &mut crown,
                            docks: &mut docks,
                            widgets_surface: &mut widgets_surface,
                            prefs_store: &prefs_store,
                            widgets_store: &widgets_store,
                            aurora_db: &aurora_db,
                            clip_db: &clip_db,
                            aux_store: &aux_store,
                            logger: &logger,
                            appbar: &mut appbar,
                            audio: &audio,
                            brightness: &brightness,
                            qr_server: &mut qr_server,
                            dwm_preview: &mut dwm_preview,
                            bg_tx: &bg_tx,
                            gif_stop_flag: &mut gif_stop_flag,
                            should_exit: &mut should_exit,
                            needs_taskbar_sync: &mut needs_taskbar_sync,
                            needs_bounds_sync: &mut needs_bounds_sync,
                            needs_hotkey_sync: &mut needs_hotkey_sync,
                            needs_widgets_sync: &mut needs_widgets_sync,
                            needs_state_push: &mut needs_state_push,
                            popup_opened_at_ms: &mut popup_opened_at_ms,
                            peek_tray_until_ms: &mut peek_tray_until_ms,
                            now_ms,
                        };
                        start_qr_for_path(&mut env, &path);
                    } else {
                        post_toast_json(&crown, "Süper Rafa Eklendi", &path, "");
                    }
                    needs_state_push = true;
                }
                BgReply::IconReady { key, b64 } => {
                    state.icon_png_b64.insert(key, b64);
                    needs_state_push = true;
                }
                BgReply::OcrDone(text_opt) => match text_opt {
                    Some(txt) if !txt.trim().is_empty() => {
                        set_clipboard_text(&txt);
                        post_toast_json(
                            &crown,
                            "OCR Metni Kopyalandı",
                            &txt.chars().take(120).collect::<String>(),
                            "",
                        );
                    }
                    _ => {
                        post_toast_json(&crown, "OCR Sonucu", "Seçili alanda metin bulunamadı.", "");
                    }
                },
                BgReply::GifDone(res) => {
                    state.gif_recording = false;
                    state.gif_elapsed_secs = 0;
                    crown.post_json(r#"{"type":"gifRecState","recording":false,"elapsed":0}"#);
                    match res {
                        Ok((frames, bytes, path)) => {
                            post_toast_json(
                                &crown,
                                "GIF Kaydedildi",
                                &format!("{frames} kare ({} KB) → {path}", bytes / 1024),
                                "",
                            );
                        }
                        Err(e) => {
                            post_toast_json(&crown, "GIF Kayıt Hatası", &e, "");
                        }
                    }
                    needs_state_push = true;
                }
                BgReply::UpdateChecked(outcome) => {
                    state.update_status = outcome.message.clone();
                    if outcome.update_available {
                        if let Some(man) = outcome.manifest {
                            post_toast_json(
                                &crown,
                                "Aurora Güncellemesi",
                                &format!("v{} indirilebilir.", man.version),
                                "",
                            );
                            state.latest_update = Some(man);
                        }
                    } else {
                        state.latest_update = None;
                    }
                    needs_state_push = true;
                }
                BgReply::UpdateDownloaded(res) => {
                    state.update_downloading = false;
                    state.update_download_pct = 100;
                    match res {
                        Ok(installer_path) => {
                            let _ = launch_target(&format!(
                                "{}|--silent --relaunch --wait-pid={}",
                                installer_path.to_string_lossy(),
                                std::process::id()
                            ));
                            should_exit = true;
                        }
                        Err(e) => {
                            state.update_status = format!("İndirme hatası: {e}");
                            post_toast_json(&crown, "Güncelleme Hatası", &e, "");
                            needs_state_push = true;
                        }
                    }
                }
            }
        }

        // 6b. Industrial Crown Interactive Focus & External-Click State Machine:
        // - Tracks whether Crown actually gained Windows foreground (`crown_ever_focused`) so Windows
        //   Foreground Lock NEVER slams shut a popup 450ms after the user clicked Crown to open it!
        // - Enables read-only `ExternalMouseClick` observation ONLY while interactive so clicking
        //   outside ALWAYS dismisses on click #1 without eating the user's click!
        let crown_active = crown.is_interactive();
        if crown_active && !prev_crown_interactive {
            let fg = get_foreground_hwnd();
            let owned = is_window_owned_by(fg, crown.hwnd);
            popup_opened_prev_fg = if owned { 0 } else { fg };
            crown_ever_focused = owned;
        } else if !crown_active && prev_crown_interactive {
            crown_ever_focused = false;
            popup_opened_prev_fg = 0;
        }
        prev_crown_interactive = crown_active;

        if crown_active {
            let fg = get_foreground_hwnd();
            if is_window_owned_by(fg, crown.hwnd) {
                crown_ever_focused = true;
            } else if now_ms.saturating_sub(popup_opened_at_ms) >= 350
                && fg != 0
                && (crown_ever_focused
                    || (popup_opened_prev_fg != 0 && fg != popup_opened_prev_fg))
            {
                crown.release_interactive();
                crown.post_json(r#"{"type":"outsideDeactivate"}"#);
                state.crown_stage_open = false;
                crown_ever_focused = false;
                popup_opened_prev_fg = 0;
                prev_crown_interactive = false;
            }
        }

        let want_watch_clicks = crown.is_interactive()
            || state.crown_stage_open
            || !state.dock_popover_open_mons.is_empty();
        if want_watch_clicks != prev_watch_clicks {
            prev_watch_clicks = want_watch_clicks;
            set_watch_external_clicks(want_watch_clicks);
        }

        // 7. Manage Lazy On-Demand Direct2D Overlay (OCR / Eyedropper / GIF Region / Shift+Drag Snap!)
        // Physical button/key failsafe: Snap Zones can NEVER remain stuck after Shift or LButton release!
        if state.snap_overlay_active {
            let lbtn_down =
                unsafe { (GetAsyncKeyState(VK_LBUTTON.0 as i32) as u16 & 0x8000) != 0 };
            let shift_down =
                unsafe { (GetAsyncKeyState(VK_SHIFT.0 as i32) as u16 & 0x8000) != 0 };
            if !lbtn_down || !shift_down {
                state.snap_overlay_active = false;
                state.snap_hover_idx = None;
                state.active_snap_hwnd = 0;
            }
        }
        let want_overlay = state.active_snip_mode.is_some() || state.snap_overlay_active;
        if want_overlay && transient_overlay.is_none() {
            let (mon_bounds, mon_scale) = if state.snap_overlay_active {
                (snap_mon_bounds, snap_mon_scale)
            } else {
                let mut cpt = POINT::default();
                let target_mon = if unsafe { GetCursorPos(&mut cpt).is_ok() } {
                    monitor_at_point(&monitors, cpt.x, cpt.y).unwrap_or(&primary_mon)
                } else {
                    &primary_mon
                };
                (
                    target_mon.descriptor.bounds,
                    target_mon.descriptor.scale_factor() as f32,
                )
            };
            if let Ok(ov) = TransientD2DOverlay::open(mon_bounds, mon_scale) {
                if state.active_snip_mode.is_some() {
                    ov.win.set_click_through(false);
                    ov.win.set_allow_activate(true);
                    force_foreground_window(ov.win.hwnd);
                } else {
                    // Visual-only Snap Zones MUST stay WS_EX_TRANSPARENT so clicks NEVER get blocked!
                    ov.win.set_click_through(true);
                    ov.win.set_allow_activate(false);
                }
                transient_overlay = Some(ov);
            }
        } else if !want_overlay && transient_overlay.is_some() {
            transient_overlay = None;
        }

        if let Some(ov) = transient_overlay.as_mut() {
            let snip_active = state.active_snip_mode.is_some();
            if !snip_active && state.snap_overlay_active {
                let _ = ov.win.set_bounds_px(&ov.gpu, snap_mon_bounds, snap_mon_scale);
            }
            ov.win.set_click_through(!snip_active);
            ov.win.set_allow_activate(snip_active);
            let scale = ov.win.dpi_scale.max(1.0);
            let origin = ov.win.bounds_px;
            let mut close_snip = false;
            if snip_active {
                let esc_down =
                    unsafe { (GetAsyncKeyState(VK_ESCAPE.0 as i32) as u16 & 0x8000) != 0 };
                if esc_down {
                    close_snip = true;
                }
            }
            while let Ok(ev) = ov.rx.try_recv() {
                match ev {
                    WindowEvent::MouseMove { dip_x, dip_y, .. } => {
                        ov.snip_drag_cur = (dip_x, dip_y);
                        if state.active_snip_mode == Some(SnipMode::Eyedropper) {
                            let px = origin.x + (dip_x * scale).round() as i32;
                            let py = origin.y + (dip_y * scale).round() as i32;
                            let (_, _, _, hex) = sample_screen_pixel_hex(px, py);
                            ov.eyedropper_hex = hex;
                        }
                    }
                    WindowEvent::LeftDown { dip_x, dip_y, .. } => {
                        if state.active_snip_mode == Some(SnipMode::Eyedropper) {
                            let px = origin.x + (dip_x * scale).round() as i32;
                            let py = origin.y + (dip_y * scale).round() as i32;
                            let (_, _, _, hex) = sample_screen_pixel_hex(px, py);
                            set_clipboard_text(&hex);
                            post_toast_json(&crown, "Renk Kopyalandı", &hex, "");
                            close_snip = true;
                        } else if state.active_snip_mode.is_some() {
                            ov.snip_drag_start = Some((dip_x, dip_y));
                            ov.snip_drag_cur = (dip_x, dip_y);
                        }
                    }
                    WindowEvent::LeftUp { dip_x, dip_y } => {
                        if let (Some(mode), Some((sx, sy))) =
                            (state.active_snip_mode.clone(), ov.snip_drag_start.take())
                        {
                            let x0 = origin.x + (sx.min(dip_x) * scale).round() as i32;
                            let y0 = origin.y + (sy.min(dip_y) * scale).round() as i32;
                            let x1 = origin.x + (sx.max(dip_x) * scale).round() as i32;
                            let y1 = origin.y + (sy.max(dip_y) * scale).round() as i32;
                            let rect = PhysicalRect {
                                x: x0,
                                y: y0,
                                width: (x1 - x0).max(8),
                                height: (y1 - y0).max(8),
                            };
                            if rect.width >= 16 && rect.height >= 16 {
                                match mode {
                                    SnipMode::Ocr => {
                                        let _ = bg_tx.try_send(BgCommand::RunOcr(rect));
                                    }
                                    SnipMode::GifRegion => {
                                        let stop = Arc::new(AtomicBool::new(false));
                                        gif_stop_flag = Some(stop.clone());
                                        state.gif_recording = true;
                                        state.gif_elapsed_secs = 0;
                                        crown.post_json(
                                            r#"{"type":"gifRecState","recording":true,"elapsed":0}"#,
                                        );
                                        let _ = bg_tx.try_send(BgCommand::StartGifRecord {
                                            rect,
                                            stop_flag: stop,
                                        });
                                        needs_state_push = true;
                                    }
                                    SnipMode::Eyedropper => {}
                                }
                            }
                            close_snip = true;
                        }
                    }
                    WindowEvent::RightDown { .. } | WindowEvent::Deactivated { .. } => {
                        close_snip = true;
                    }
                    WindowEvent::KeyDown { vk, .. } if vk == VK_ESCAPE.0 => {
                        close_snip = true;
                    }
                    _ => {}
                }
            }
            if close_snip {
                state.active_snip_mode = None;
                transient_overlay = None;
            } else {
                let (eff, _, _) = state.effective_theme();
                ov.paint(
                    state.active_snip_mode.clone(),
                    state.prefs.effective_snap_preset(),
                    state.snap_overlay_active,
                    state.snap_hover_idx,
                    snap_work_area,
                    &eff.colors.accent_primary,
                );
            }
        }

        // 8. Refresh Open Window Catalog & Fullscreen/SmartDodge State (on hook event or 1.4s fallback)
        if catalog_changed || last_window_scan.elapsed() >= Duration::from_millis(1400) {
            last_window_scan = Instant::now();
            let mut own_hwnds: HashSet<isize> = HashSet::new();
            own_hwnds.insert(crown.hwnd.0 as isize);
            for d in &docks {
                own_hwnds.insert(d.hwnd.0 as isize);
            }
            if let Some(ws) = widgets_surface.as_ref() {
                own_hwnds.insert(ws.hwnd.0 as isize);
            }
            if let Some(ov) = transient_overlay.as_ref() {
                own_hwnds.insert(ov.win.hwnd.0 as isize);
            }

            let mut wins = enumerate_open_windows(&own_hwnds, &state.attention_procs);
            for w in &mut wins {
                if w.is_active {
                    state.attention_procs.remove(&w.process_name.to_ascii_lowercase());
                } else if state.attention_procs.contains(&w.process_name.to_ascii_lowercase()) {
                    w.needs_attention = true;
                }
            }

            update_stable_orders(
                &wins,
                &state.pinned_shortcuts,
                &mut state.running_group_order,
                &mut state.window_seen_order,
            );
            let dock_groups = build_dock_groups_ordered(
                &state.pinned_shortcuts,
                &wins,
                None,
                &state.running_group_order,
                &state.window_seen_order,
            );
            for g in &dock_groups {
                if !state.icon_png_b64.contains_key(&g.group_key)
                    && state.icon_requested.insert(g.group_key.clone())
                {
                    let _ = bg_tx.try_send(BgCommand::ExtractIconPng {
                        key: g.group_key.clone(),
                        hwnd: g.windows.first().map(|w| w.hwnd),
                        exe_path: g.executable_path.clone(),
                        aumid: g.aumid.clone(),
                    });
                }
            }

            let mon_descs: Vec<MonitorDescriptor> =
                monitors.iter().map(|m| m.descriptor.clone()).collect();
            let (fs_mons, ov_mons) = analyze_fullscreen_and_dodge(
                &mon_descs,
                &own_hwnds,
                state.prefs.effective_dock_edge(),
            );

            // Apply per-monitor Fullscreen suppression cleanly (never hide interactive Crown!)
            crown.set_suppressed_fullscreen(fs_mons.contains(&crown.monitor_id));
            if let Some(ws) = widgets_surface.as_mut() {
                let is_fs = fs_mons.contains(&ws.monitor_id);
                let prev_fs = ws.suppressed_fullscreen;
                ws.set_suppressed_fullscreen(is_fs);
                if prev_fs != is_fs && widgets_ready {
                    let pv = json!({ "type": "widgetVisibility", "paused": is_fs }).to_string();
                    ws.post_json(&pv);
                }
            }
            for d in &mut docks {
                let is_fs = fs_mons.contains(&d.monitor_id);
                d.set_suppressed_fullscreen(is_fs);
                let prev_ov = state.overlapped_monitors.contains(&d.monitor_id);
                let now_ov = ov_mons.contains(&d.monitor_id);
                if prev_ov != now_ov && state.prefs.effective_dock_mode() == "SmartDodge" {
                    let j = json!({ "type": "dodgeState", "dodged": now_ov }).to_string();
                    d.post_json(&j);
                }
            }

            let fg = if state.last_external_hwnd != 0 {
                state.last_external_hwnd
            } else {
                get_foreground_hwnd()
            };
            let (kb_code, kb_name) = get_keyboard_layout_code(fg);
            if kb_code != state.keyboard_code {
                state.keyboard_code = kb_code;
                state.keyboard_name = kb_name;
                needs_state_push = true;
            }

            if wins.len() != state.open_windows.len()
                || wins.iter().zip(state.open_windows.iter()).any(|(a, b)| {
                    a.hwnd != b.hwnd
                        || a.title != b.title
                        || a.is_active != b.is_active
                        || a.is_top_most != b.is_top_most
                        || a.needs_attention != b.needs_attention
                })
                || fs_mons != state.fullscreen_monitors
                || ov_mons != state.overlapped_monitors
            {
                state.open_windows = wins;
                state.dock_groups = dock_groups;
                state.fullscreen_monitors = fs_mons;
                state.overlapped_monitors = ov_mons;
                needs_state_push = true;
            }
        }

        // 9. Native Windows Taskbar Lockdown Guard (1.2s periodic sweep + Peek expiry!)
        if peek_tray_until_ms > 0 && now_ms >= peek_tray_until_ms {
            peek_tray_until_ms = 0;
            needs_taskbar_sync = true;
        }
        if last_tb_guard.elapsed() >= Duration::from_millis(1200) {
            last_tb_guard = Instant::now();
            if state.prefs.hide_native_taskbar_while_active && peek_tray_until_ms == 0 {
                appbar.enforce_native_taskbar_suppressed(None);
                // Re-verify AppBar WorkArea edge alignment every sweep (zero-cost no-op when healthy!)
                sync_taskbar_and_appbar(&mut appbar, &state, false);
            }
        }

        // 10. AutoHide / SmartDodge edge cursor wake (ONLY active when mode is AutoHide or SmartDodge!)
        let dock_mode = state.prefs.effective_dock_mode();
        if (dock_mode == "AutoHide" || dock_mode == "SmartDodge")
            && last_edge_wake.elapsed() >= Duration::from_millis(160)
        {
            last_edge_wake = Instant::now();
            let mut pt = POINT::default();
            if unsafe { GetCursorPos(&mut pt).is_ok() } {
                let edge = state.prefs.effective_dock_edge();
                for (i, mon) in monitors.iter().enumerate() {
                    let b = mon.descriptor.bounds;
                    let on_mon =
                        pt.x >= b.x && pt.x <= b.right() && pt.y >= b.y && pt.y <= b.bottom();
                    if !on_mon {
                        continue;
                    }
                    let at_edge = match edge {
                        "Left" => pt.x <= b.x + 6,
                        "Right" => pt.x >= b.right() - 6,
                        _ => pt.y >= b.bottom() - 6,
                    };
                    if at_edge {
                        if let Some(d) = docks.get(i) {
                            d.post_json(r#"{"type":"cursorWake"}"#);
                        }
                    }
                }
            }
        }

        // 11. 1Hz Lightweight Clock Tick + Pomodoro / Awake / GIF / AI Quota / Auto Day-Night
        let cur_sec = chrono::Local::now().timestamp();
        if cur_sec != last_clock_sec {
            last_clock_sec = cur_sec;
            let now_dt = chrono::Local::now();
            let clock_str = if state.prefs.show_clock_seconds {
                now_dt.format("%H:%M:%S").to_string()
            } else {
                now_dt.format("%H:%M").to_string()
            };
            let tick = json!({ "type": "clockTick", "clock": clock_str }).to_string();
            if crown_ready {
                crown.post_json(&tick);
            }
            for d in &docks {
                if dock_ready.contains(&d.surface_id) {
                    d.post_json(&tick);
                }
            }
            if let Some(ws) = widgets_surface.as_ref() {
                if widgets_ready && !ws.suppressed_fullscreen {
                    ws.post_json(&tick);
                }
            }
            if state.focus_running {
                if state.focus_seconds > 0 {
                    state.focus_seconds -= 1;
                } else {
                    let mins = state.focus_preset_mins.clamp(5, 120);
                    state.focus_running = false;
                    state.focus_seconds = mins * 60;
                    state.focus_completed_mins = state.focus_completed_mins.saturating_add(mins);
                    post_toast_json(&crown, "Odak Tamamlandı", &format!("{mins} dakikalık seans bitti!"), "");
                }
                needs_state_push = true;
            }
            if let Some(until) = state.awake_until_ms {
                if now_ms >= until {
                    state.awake_mode = "off".into();
                    state.awake_until_ms = None;
                    aurora_win32::set_awake_keep_alive(false);
                    post_toast_json(&crown, "Uyanık Kal Bitti", "Normal uyku planına dönüldü.", "");
                    needs_state_push = true;
                }
            }
            if state.gif_recording {
                state.gif_elapsed_secs = state.gif_elapsed_secs.saturating_add(1);
                let g = json!({
                    "type": "gifRecState",
                    "recording": true,
                    "elapsed": state.gif_elapsed_secs,
                })
                .to_string();
                crown.post_json(&g);
            }
            // 60s periodic AI Quota refresh ONLY when user has an active AI Quota widget (Dock or Desktop)!
            if state.needs_ai_quota_poll()
                && last_ai_quota_refresh.elapsed() >= Duration::from_secs(60)
            {
                last_ai_quota_refresh = Instant::now();
                let _ = bg_tx.try_send(BgCommand::RefreshAiQuota(state.ai_configs.clone()));
            }
            // 5m opt-in Market ticker refresh ONLY when user enabled a Market widget (Dock or Desktop)!
            if state.needs_market_poll()
                && last_market_refresh.elapsed() >= Duration::from_secs(300)
            {
                last_market_refresh = Instant::now();
                let _ = bg_tx.try_send(BgCommand::RefreshMarket);
            }
            // 15s opt-in Local AI (Ollama/LM Studio) & Disk health check ONLY when active!
            if state.needs_local_ai_or_disks_poll()
                && last_local_ai_refresh.elapsed() >= Duration::from_secs(15)
            {
                last_local_ai_refresh = Instant::now();
                let _ = bg_tx.try_send(BgCommand::RefreshLocalAiAndDisks);
            }
            // 60s check for Automatic Day/Night Theme switching (07:00..19:00 -> Day, else Night)
            if state.prefs.auto_day_night_theme
                && last_day_night_check.elapsed() >= Duration::from_secs(60)
            {
                use chrono::Timelike;
                last_day_night_check = Instant::now();
                let hr = now_dt.hour();
                let target_id = if (7..19).contains(&hr) {
                    theme_by_id(&state.prefs.day_theme_id).id
                } else {
                    theme_by_id(&state.prefs.night_theme_id).id
                };
                if state.theme_id != target_id {
                    state.theme_id = target_id;
                    aurora_db.save_user_preferences(&UserPreferencesRecord {
                        active_theme_id: state.theme_id.clone(),
                        active_profile_id: state.active_profile_id.clone(),
                        focus_mode_enabled: state.focus_mode_enabled,
                        reduced_motion: false,
                    });
                    needs_state_push = true;
                }
            }
            if let Some(trim_t) = post_display_trim_at {
                if Instant::now() >= trim_t {
                    post_display_trim_at = None;
                    trim_process_working_set();
                }
            }
            if !trimmed_boot_ws && now_ms >= 5000 {
                trimmed_boot_ws = true;
                sentinel.mark_healthy();
                trim_process_working_set();
            } else if !trimmed_settled_ws && now_ms >= 35000 {
                trimmed_settled_ws = true;
                trim_process_working_set();
            }
        }

        // 12. Apply deferred Taskbar / Bounds / Hotkey syncs
        if needs_taskbar_sync {
            let peek_active = peek_tray_until_ms > now_ms;
            sync_taskbar_and_appbar(&mut appbar, &state, peek_active);
            monitors = enumerate_monitors();
            needs_bounds_sync = true;
            needs_state_push = true;
        }
        if needs_bounds_sync && !monitors.is_empty() {
            primary_mon = monitors[0].clone();
            let p_mon = &monitors[0];
            crown.set_bounds_px(
                compute_crown_bounds(p_mon),
                p_mon.descriptor.scale_factor() as f32,
            );
            let edge = state.prefs.effective_dock_edge();
            let tb_hidden = appbar.is_native_taskbar_suppressed();
            if docks.len() != monitors.len() {
                state.clear_dock_icon_caches();
                docks = create_dock_surfaces(
                    &wv_env,
                    &ui_dir,
                    &monitors,
                    edge,
                    tb_hidden,
                    &surf_tx,
                    wake_hwnd,
                );
                dock_ready.clear();
                dock_ui_ready.clear();
                dock_nav_started.clear();
                dock_reload_attempts.clear();
                last_dock_json.clear();
            } else {
                for (d, mon) in docks.iter_mut().zip(monitors.iter()) {
                    let mon_changed = !d.monitor_id.eq_ignore_ascii_case(&mon.descriptor.device_id);
                    d.monitor_id = mon.descriptor.device_id.clone();
                    d.set_bounds_px(
                        compute_dock_bounds(mon, edge, tb_hidden),
                        mon.descriptor.scale_factor() as f32,
                    );
                    if mon_changed {
                        d.force_resync_region();
                    }
                }
            }
            if let Some(ws) = widgets_surface.as_mut() {
                ws.monitor_id = p_mon.descriptor.device_id.clone();
                ws.set_bounds_px(
                    p_mon.descriptor.work_area,
                    p_mon.descriptor.scale_factor() as f32,
                );
            }
        }
        if needs_hotkey_sync {
            unregister_aurora_hotkeys(crown.hwnd);
            hk_results = register_aurora_hotkeys(crown.hwnd, &state.prefs.omni_hotkey_preset);
            let omni_ok = hk_results.iter().any(|&(name, ok)| name == "OmniSearch" && ok);
            key_hook.update_flags(
                state.prefs.hardware_osd_enabled,
                state.prefs.win_key_opens_omni_search,
                !omni_ok,
            );
        }

        // 12b. Staged Sequential Dock Navigation + Self-Healing Hydration Watchdog (v5.0)!
        // Wait for crown cold-parse to settle before launching multi-monitor docks sequentially so
        // the single shared `--process-per-site` renderer hydrates every monitor cleanly on attempt 0!
        if crown_ready || crown_nav_done || now_ms >= 900 {
            let mut prev_dock_ok = true;
            for d in &docks {
                let sid = d.surface_id;
                if dock_ui_ready.contains(&sid) {
                    continue;
                }
                if let Some(&started) = dock_nav_started.get(&sid) {
                    let attempts = *dock_reload_attempts.get(&sid).unwrap_or(&0);
                    if started.elapsed() >= Duration::from_millis(4000) && attempts < 3 {
                        dock_reload_attempts.insert(sid, attempts + 1);
                        dock_nav_started.insert(sid, Instant::now());
                        logger.warn(
                            "UI",
                            "DOCK_RECOVERY_RELOAD",
                            &format!(
                                "dock surface={sid} missed uiReady after 4000ms; re-navigating (attempt {})",
                                attempts + 1
                            ),
                        );
                        d.navigate_to_page();
                    }
                    // Allow next monitor dock to start once this one has had 450ms head-start
                    if started.elapsed() < Duration::from_millis(450) {
                        prev_dock_ok = false;
                    }
                } else if prev_dock_ok {
                    dock_nav_started.insert(sid, Instant::now());
                    d.navigate_to_page();
                    prev_dock_ok = false;
                }
            }
        }

        // 12c. Lazy Desktop Widgets Surface (HWND_BOTTOM, zero VRAM when disabled/empty, staged after docks!)
        let want_widgets = state.prefs.widgets_enabled && !state.widgets.is_empty();
        let docks_done = dock_ui_ready.len() >= docks.len();
        if !want_widgets && widgets_surface.is_some() {
            widgets_surface = None;
            widgets_ready = false;
            widgets_ui_ready = false;
            widgets_nav_started = None;
            widgets_reload_attempts = 0;
            last_widgets_json.clear();
            trim_process_working_set();
        } else if want_widgets
            && widgets_surface.is_none()
            && !monitors.is_empty()
            && (needs_widgets_sync || (crown_nav_done && docks_done) || now_ms >= 1400)
        {
            let p_mon = &monitors[0];
            if let Ok(ws) = WebViewOverlaySurface::create_desktop_widgets(
                WIDGETS_SURFACE_ID,
                p_mon.descriptor.device_id.clone(),
                &wv_env,
                &ui_dir,
                p_mon.descriptor.work_area,
                p_mon.descriptor.scale_factor() as f32,
                surf_tx.clone(),
                Some(wake_hwnd),
                false,
            ) {
                widgets_nav_started = Some(Instant::now());
                widgets_reload_attempts = 0;
                widgets_surface = Some(ws);
                needs_widgets_sync = true;
            }
        }
        if let Some(ws) = widgets_surface.as_mut() {
            if !widgets_ui_ready {
                if let Some(started) = widgets_nav_started {
                    if started.elapsed() >= Duration::from_millis(1600) && widgets_reload_attempts < 3 {
                        widgets_reload_attempts += 1;
                        widgets_nav_started = Some(Instant::now());
                        ws.navigate_to_page();
                    }
                }
            }
            if needs_widgets_sync {
                let elevate = state.widget_edit_mode || state.widgets.iter().any(|w| w.pinned_top);
                ws.set_desktop_elevated(elevate);
            }
        }

        // 13. Change-Hashed State Push to Crown, Per-Monitor Docks & Desktop Widgets (zero redundant DOM churn!)
        if needs_catalog_push {
            if crown_ready {
                crown.post_json(&state.build_catalog_json_for_surface(0));
            }
            for d in &docks {
                if dock_ready.contains(&d.surface_id) {
                    d.post_json(&state.build_catalog_json_for_surface(d.surface_id));
                }
            }
        }
        if needs_state_push {
            notif_mirror_active.store(
                state.prefs.effective_notification_mode() == "aurora-mirror"
                    && state.prefs.system_notification_bridge_enabled,
                Ordering::Relaxed,
            );
            if crown_ready {
                let cj = state.build_crown_state_json_for_surface(
                    0,
                    &crown.monitor_id,
                    monitors.len(),
                    appbar.is_native_taskbar_suppressed(),
                    now_ms,
                );
                if cj != last_crown_json {
                    crown.post_json(&cj);
                    last_crown_json = cj;
                }
            }
            for (idx, d) in docks.iter().enumerate() {
                if !dock_ready.contains(&d.surface_id) {
                    continue;
                }
                let dj = state.build_dock_state_json_for_surface(
                    d.surface_id,
                    &d.monitor_id,
                    idx == 0,
                    now_ms,
                );
                if last_dock_json.get(&d.monitor_id).map(String::as_str) != Some(dj.as_str()) {
                    d.post_json(&dj);
                    last_dock_json.insert(d.monitor_id.clone(), dj);
                }
            }
            if let Some(ws) = widgets_surface.as_ref() {
                if widgets_ready {
                    let wj = state.build_widgets_state_json();
                    if wj != last_widgets_json {
                        ws.post_json(&wj);
                        last_widgets_json = wj;
                    }
                }
            }
        }

        // 14. Zero-CPU Kernel Wait until next Win32 message / WM_AURORA_WAKE / timer
        let docks_hydrating = dock_ui_ready.len() < docks.len()
            || (widgets_surface.is_some() && !widgets_ui_ready);
        let wait_ms = if transient_overlay.is_some() {
            16
        } else if docks_hydrating {
            50
        } else if dock_mode == "AutoHide" || dock_mode == "SmartDodge" {
            160
        } else {
            400
        };
        unsafe {
            let _ = MsgWaitForMultipleObjectsEx(None, wait_ms, QS_ALLINPUT, MWMO_INPUTAVAILABLE);
        }
    }

    // ═══════════ GRACEFUL ZERO-LEAK SHUTDOWN ═══════════
    if let Some(flag) = gif_stop_flag.take() {
        flag.store(true, std::sync::atomic::Ordering::Relaxed);
    }
    drop(transient_overlay);
    qr_server.stop();
    drop(dwm_preview);
    drop(key_hook);
    drop(win_hooks);
    unregister_aurora_hotkeys(crown.hwnd);
    drop(widgets_surface);
    drop(docks);
    drop(crown);
    appbar.restore_native_taskbar();
    let _ = std::fs::write(aurora_data_dir().join("clean_exit.flag"), b"ok");
    sentinel.mark_healthy();
    logger.info("APP", "CLEAN_EXIT", &format!("Project Aurora v{AURORA_VERSION} exited cleanly."));
    Ok(())
}

fn post_toast_json(crown: &WebViewOverlaySurface, title: &str, body: &str, notif_id: &str) {
    let j = json!({
        "type": "toast",
        "title": title,
        "body": body,
        "notificationId": notif_id,
    })
    .to_string();
    crown.post_json(&j);
}

fn post_osd_json(
    crown: &WebViewOverlaySurface,
    kind: &str,
    label: &str,
    value_text: String,
    percent: Option<u8>,
    active: bool,
    duration_ms: u32,
) {
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
    crown.post_json(&j);
}

fn spawn_background_workers(
    cmd_rx: Receiver<BgCommand>,
    reply_tx: Sender<BgReply>,
    wake_hwnd: isize,
    brightness: Arc<BrightnessController>,
    notif_mirror_active: Arc<AtomicBool>,
) {
    // Worker 1: On-Demand Command Processor (Native Pickers, Folder Stacks, File Search, Icons, OCR, GIF, Updates)
    let tx_cmd = reply_tx.clone();
    let bri_cmd = brightness.clone();
    std::thread::Builder::new()
        .name("aurora-bg-cmd".into())
        .spawn(move || {
            let _ = unsafe { CoInitializeEx(None, COINIT_APARTMENTTHREADED) };
            let mut last_clip_text = get_clipboard_text().unwrap_or_default();
            let mut last_clip_img_len = 0usize;
            while let Ok(cmd) = cmd_rx.recv() {
                match cmd {
                    BgCommand::PickDockFolder => {
                        if let Some(p) = pick_folder_dialog("Alt Dock'a Eklenecek Klasörü Seçin") {
                            let _ = tx_cmd.send(BgReply::DockFolderPicked(p));
                            post_wake_ui(wake_hwnd);
                        }
                    }
                    BgCommand::PickShelfFile { share_qr_after } => {
                        if let Some(p) = pick_file_dialog("Süper Rafa Eklenecek Dosyayı Seçin") {
                            let _ = tx_cmd.send(BgReply::ShelfFilePicked {
                                path: p,
                                share_qr_after,
                            });
                            post_wake_ui(wake_hwnd);
                        }
                    }
                    BgCommand::PickWidgetPhoto { widget_id } => {
                        if let Some(p) = pick_file_dialog("Widget Fotoğrafı Seçin") {
                            if let Some(data_uri) = encode_widget_photo_data_uri(&p) {
                                let _ = tx_cmd.send(BgReply::WidgetPhotoLoaded {
                                    widget_id,
                                    path: p,
                                    data_uri,
                                });
                                post_wake_ui(wake_hwnd);
                            }
                        }
                    }
                    BgCommand::LoadWidgetPhoto { widget_id, path } => {
                        if let Some(data_uri) = encode_widget_photo_data_uri(&path) {
                            let _ = tx_cmd.send(BgReply::WidgetPhotoLoaded {
                                widget_id,
                                path,
                                data_uri,
                            });
                            post_wake_ui(wake_hwnd);
                        }
                    }
                    BgCommand::ReadFolderStack { monitor_id, path } => {
                        let raw = read_folder_stack_entries_full(&path, 28);
                        let items: Vec<Value> = raw
                            .into_iter()
                            .map(|e| {
                                let icon = extract_shell_path_icon_base64(&e.path, 24);
                                let mod_str = std::fs::metadata(&e.path)
                                    .and_then(|m| m.modified())
                                    .ok()
                                    .map(|st| {
                                        let dt: chrono::DateTime<chrono::Local> = st.into();
                                        dt.format("%d.%m %H:%M").to_string()
                                    })
                                    .unwrap_or_default();
                                json!({
                                    "name": e.name,
                                    "path": e.path,
                                    "isDir": e.is_dir,
                                    "size": e.size_label,
                                    "mod": mod_str,
                                    "icon": icon,
                                })
                            })
                            .collect();
                        let _ = tx_cmd.send(BgReply::FolderStackReady {
                            monitor_id,
                            name: friendly_folder_name(&path),
                            path,
                            items,
                        });
                        post_wake_ui(wake_hwnd);
                    }
                    BgCommand::SearchFiles { query, seq } => {
                        let hits = search_local_files(&query, 12);
                        let files: Vec<Value> = hits
                            .into_iter()
                            .enumerate()
                            .map(|(idx, (name, path))| {
                                let p = Path::new(&path);
                                let is_dir = p.is_dir();
                                let parent = p
                                    .parent()
                                    .and_then(|pr| pr.file_name())
                                    .and_then(|n| n.to_str())
                                    .unwrap_or("Yerel Disk");
                                let icon = extract_shell_path_icon_base64(&path, 24);
                                json!({
                                    "name": name,
                                    "path": path,
                                    "subtitle": parent,
                                    "isDir": is_dir,
                                    "badge": if is_dir { "Klasör" } else { "Dosya" },
                                    "score": 520i32.saturating_sub(idx as i32 * 18),
                                    "icon": icon,
                                })
                            })
                            .collect();
                        let _ = tx_cmd.send(BgReply::FileSearch { seq, query, files });
                        post_wake_ui(wake_hwnd);
                    }
                    BgCommand::ExtractIconPng { key, hwnd, exe_path, aumid } => {
                        if let Some(b64) = extract_icon_png_base64(hwnd, &exe_path, aumid.as_deref()) {
                            let _ = tx_cmd.send(BgReply::IconReady { key, b64 });
                            post_wake_ui(wake_hwnd);
                        }
                    }
                    BgCommand::RunOcr(rect) => {
                        let txt = recognize_screen_region_ocr(rect);
                        let _ = tx_cmd.send(BgReply::OcrDone(txt));
                        post_wake_ui(wake_hwnd);
                    }
                    BgCommand::StartGifRecord { rect, stop_flag } => {
                        let out = user_known_folder("Desktop").join(format!(
                            "Aurora-Kayit-{}.gif",
                            chrono::Local::now().format("%Y%m%d-%H%M%S")
                        ));
                        let res = record_gif_region(rect, 15, &out, stop_flag)
                            .map(|(f, b)| (f, b, out.to_string_lossy().to_string()))
                            .map_err(|e| e.to_string());
                        let _ = tx_cmd.send(BgReply::GifDone(res));
                        post_wake_ui(wake_hwnd);
                    }
                    BgCommand::CheckUpdates(cur) => {
                        let out = check_for_updates(&cur);
                        let _ = tx_cmd.send(BgReply::UpdateChecked(out));
                        post_wake_ui(wake_hwnd);
                    }
                    BgCommand::InstallUpdate(man) => {
                        let dl_dir = aurora_root_dir().join("updates");
                        let res = download_and_verify_installer(&man, &dl_dir, |_| {})
                            .map_err(|e| e.to_string());
                        let _ = tx_cmd.send(BgReply::UpdateDownloaded(res));
                        post_wake_ui(wake_hwnd);
                    }
                    BgCommand::RefreshSlowHardware => {
                        let _ = tx_cmd.send(BgReply::Bluetooth(query_bluetooth_batteries()));
                        let _ = tx_cmd.send(BgReply::Brightness(bri_cmd.discover()));
                        post_wake_ui(wake_hwnd);
                    }
                    BgCommand::RefreshWeather => {
                        let w = fetch_live_weather();
                        if w.valid {
                            let _ = tx_cmd.send(BgReply::Weather(w));
                            post_wake_ui(wake_hwnd);
                        }
                    }
                    BgCommand::RefreshAiQuota(cfgs) => {
                        let snaps = query_ai_quota_snapshots(&cfgs);
                        let _ = tx_cmd.send(BgReply::AiQuota(snaps));
                        post_wake_ui(wake_hwnd);
                    }
                    BgCommand::RefreshMarket => {
                        if let Some(snap) = fetch_market_snapshot() {
                            let _ = tx_cmd.send(BgReply::Market(snap));
                            post_wake_ui(wake_hwnd);
                        }
                    }
                    BgCommand::RefreshLocalAiAndDisks => {
                        let local_ai = query_local_ai_status();
                        let disks = query_disk_usages();
                        let _ = tx_cmd.send(BgReply::LocalAiAndDisks { local_ai, disks });
                        post_wake_ui(wake_hwnd);
                    }
                    BgCommand::SyncWallpaperAccent(surface_hex) => {
                        let res = extract_wallpaper_accent_colors(&surface_hex);
                        let _ = tx_cmd.send(BgReply::WallpaperAccent(res));
                        post_wake_ui(wake_hwnd);
                    }
                    BgCommand::CheckClipboard => {
                        if let Some(txt) = get_clipboard_text() {
                            if !txt.is_empty() && txt != last_clip_text {
                                last_clip_text = txt.clone();
                                let _ = tx_cmd.send(BgReply::ClipboardText { text: txt });
                                post_wake_ui(wake_hwnd);
                            }
                        } else if let Some((w, h, png, thumb)) = try_get_clipboard_image_png() {
                            if png.len() != last_clip_img_len {
                                last_clip_img_len = png.len();
                                let _ = tx_cmd.send(BgReply::ClipboardImage {
                                    width: w,
                                    height: h,
                                    png_bytes: png,
                                    thumb_b64: thumb,
                                });
                                post_wake_ui(wake_hwnd);
                            }
                        }
                    }
                }
            }
        })
        .ok();

    // Worker 2: Battery-Friendly Smart Telemetry & mtime-Gated Toast Bridge (zero clipboard polling!)
    std::thread::Builder::new()
        .name("aurora-telemetry".into())
        .spawn(move || {
            let _ = unsafe { CoInitializeEx(None, COINIT_APARTMENTTHREADED) };

            // Fast sub-50ms hardware, live media & tray boot scans FIRST (never block behind 300-app icon catalog!)
            let init_media = query_media_session();
            let _ = reply_tx.send(BgReply::Media(init_media.clone()));
            let init_bt = query_bluetooth_batteries();
            let _ = reply_tx.send(BgReply::Bluetooth(init_bt.clone()));
            let visible_procs: HashSet<String> = enumerate_open_windows(&HashSet::new(), &HashSet::new())
                .into_iter()
                .map(|w| w.process_name.to_ascii_lowercase())
                .collect();
            let _ = reply_tx.send(BgReply::TrayApps(query_background_tray_apps(&visible_procs)));
            post_wake_ui(wake_hwnd);

            // Full UWP + Win32 + 48 Settings catalog with 32x32 ARGB PNG icons
            let apps = build_full_installed_app_catalog(true);
            let _ = reply_tx.send(BgReply::InstalledApps(apps));
            let _ = reply_tx.send(BgReply::Brightness(brightness.discover()));
            post_wake_ui(wake_hwnd);

            let w = fetch_live_weather();
            if w.valid {
                let _ = reply_tx.send(BgReply::Weather(w));
                post_wake_ui(wake_hwnd);
            }

            let mut metrics = MetricsSampler::new();
            let mut wpn = WpnNotificationBridge::new(None);
            let audio_poll = CoreAudioController::new();
            let mut last_audio_snap = audio_poll.get_snapshot();
            let mut last_bt_snapshot: Vec<BluetoothBatteryDevice> = init_bt;
            let mut last_media_snap: MediaSessionSnapshot = init_media;
            let mut last_privacy_snap: Option<PrivacySnapshot> = None;
            let mut last_metrics_snap: Option<SystemMetricsSnapshot> = None;
            let mut last_brightness_snap: Option<Vec<ExternalMonitorBrightness>> = None;
            let mut last_metrics_push = Instant::now() - Duration::from_secs(10);
            let mut last_audio_tick = Instant::now();
            let mut last_tray_tick = Instant::now();
            let mut last_bt_tick = Instant::now();
            let mut last_slow_hw_tick = Instant::now();
            let mut last_weather_tick = Instant::now();

            loop {
                let mut should_wake = false;

                // Unified 1.5s battery-friendly cadence: mtime-gated Toasts (opt-in) + diff-throttled Media/Privacy/Metrics
                if notif_mirror_active.load(Ordering::Relaxed) {
                    let fresh = wpn.poll_new_toasts();
                    if !fresh.is_empty() {
                        let _ = reply_tx.send(BgReply::FreshNotifications(fresh));
                        should_wake = true;
                    }
                }

                let media_snap = query_media_session();
                if media_meaningfully_changed(&last_media_snap, &media_snap) {
                    last_media_snap = media_snap.clone();
                    let _ = reply_tx.send(BgReply::Media(media_snap));
                    should_wake = true;
                }

                let privacy_snap = query_privacy_snapshot();
                if last_privacy_snap.as_ref() != Some(&privacy_snap) {
                    last_privacy_snap = Some(privacy_snap.clone());
                    let _ = reply_tx.send(BgReply::Privacy(privacy_snap));
                    should_wake = true;
                }

                let cur_metrics = metrics.sample(true);
                if should_push_metrics(last_metrics_snap.as_ref(), &cur_metrics, last_metrics_push.elapsed()) {
                    last_metrics_snap = Some(cur_metrics.clone());
                    last_metrics_push = Instant::now();
                    let _ = reply_tx.send(BgReply::Metrics(cur_metrics));
                    should_wake = true;
                }

                // 3s loop: Per-app audio sessions & mic mute (change-filtered, <0.5ms COM read)
                if last_audio_tick.elapsed() >= Duration::from_secs(3) {
                    last_audio_tick = Instant::now();
                    let fresh_audio = audio_poll.get_snapshot();
                    if fresh_audio != last_audio_snap {
                        last_audio_snap = fresh_audio.clone();
                        let _ = reply_tx.send(BgReply::AudioSnapshot(fresh_audio));
                        should_wake = true;
                    }
                }

                // 12s loop: System Tray background apps (excluding only apps with visible windows!)
                if last_tray_tick.elapsed() >= Duration::from_secs(12) {
                    last_tray_tick = Instant::now();
                    let visible_procs: HashSet<String> =
                        enumerate_open_windows(&HashSet::new(), &HashSet::new())
                            .into_iter()
                            .map(|w| w.process_name.to_ascii_lowercase())
                            .collect();
                    let _ = reply_tx.send(BgReply::TrayApps(query_background_tray_apps(&visible_procs)));
                    should_wake = true;
                }

                // 8s loop: Live Connected Bluetooth Devices & Batteries (sub-3ms cfgmgr32 scan, change-filtered!)
                if last_bt_tick.elapsed() >= Duration::from_secs(8) {
                    last_bt_tick = Instant::now();
                    let fresh_bt = query_bluetooth_batteries();
                    if fresh_bt != last_bt_snapshot {
                        last_bt_snapshot = fresh_bt.clone();
                        let _ = reply_tx.send(BgReply::Bluetooth(fresh_bt));
                        should_wake = true;
                    }
                }

                // 45s loop: External Monitor DDC/CI Brightness (change-filtered!)
                if last_slow_hw_tick.elapsed() >= Duration::from_secs(45) {
                    last_slow_hw_tick = Instant::now();
                    let fresh_bri = brightness.discover();
                    if last_brightness_snap.as_ref() != Some(&fresh_bri) {
                        last_brightness_snap = Some(fresh_bri.clone());
                        let _ = reply_tx.send(BgReply::Brightness(fresh_bri));
                        should_wake = true;
                    }
                }

                // 20m loop: Live Weather
                if last_weather_tick.elapsed() >= Duration::from_secs(1200) {
                    last_weather_tick = Instant::now();
                    let w = fetch_live_weather();
                    if w.valid {
                        let _ = reply_tx.send(BgReply::Weather(w));
                        should_wake = true;
                    }
                }

                if should_wake {
                    post_wake_ui(wake_hwnd);
                }

                std::thread::sleep(Duration::from_millis(1500));
            }
        })
        .ok();
}

fn monitor_at_point(
    monitors: &[LiveMonitorHandle],
    px: i32,
    py: i32,
) -> Option<&LiveMonitorHandle> {
    monitors
        .iter()
        .find(|m| m.descriptor.bounds.contains(px, py))
        .or_else(|| monitors.first())
}

fn media_meaningfully_changed(prev: &MediaSessionSnapshot, cur: &MediaSessionSnapshot) -> bool {
    let predicted_pos = if prev.is_playing && prev.timeline_updated_ms > 0 {
        let elapsed = ((cur.timeline_updated_ms - prev.timeline_updated_ms).max(0) / 1000) as u32;
        let p = prev.position_secs.saturating_add(elapsed);
        if prev.duration_secs > 0 { p.min(prev.duration_secs) } else { p }
    } else {
        prev.position_secs
    };
    prev.title != cur.title
        || prev.artist != cur.artist
        || prev.app_name != cur.app_name
        || prev.is_playing != cur.is_playing
        || prev.has_session != cur.has_session
        || prev.duration_secs != cur.duration_secs
        || prev.can_seek != cur.can_seek
        || prev.thumbnail_bytes.is_some() != cur.thumbnail_bytes.is_some()
        || prev.thumbnail_bytes.as_ref().map(Vec::len) != cur.thumbnail_bytes.as_ref().map(Vec::len)
        || predicted_pos.abs_diff(cur.position_secs) >= 3
}

fn should_push_metrics(
    prev_opt: Option<&SystemMetricsSnapshot>,
    cur: &SystemMetricsSnapshot,
    elapsed: Duration,
) -> bool {
    let Some(prev) = prev_opt else {
        return true;
    };
    if elapsed >= Duration::from_secs(6) {
        return true;
    }
    let cpu_delta = (cur.cpu_percent - prev.cpu_percent).abs() >= 2.0;
    let ram_delta = (cur.ram_percent - prev.ram_percent).abs() >= 2.0;
    let net_delta = (cur.net_down_mbps - prev.net_down_mbps).abs() >= 0.05
        || (cur.net_up_mbps - prev.net_up_mbps).abs() >= 0.05
        || (cur.net_down_mbps > 0.01) != (prev.net_down_mbps > 0.01)
        || (cur.net_up_mbps > 0.01) != (prev.net_up_mbps > 0.01);
    let battery_changed = cur.battery_present != prev.battery_present
        || cur.battery_percent != prev.battery_percent
        || cur.is_charging != prev.is_charging;
    cpu_delta || ram_delta || net_delta || battery_changed
}

fn encode_widget_photo_data_uri(path: &str) -> Option<String> {
    use base64::Engine;
    use image::ImageFormat;
    let img = image::open(path).ok()?;
    let thumb = img.thumbnail(640, 640);
    let mut buf = Vec::new();
    let mut cursor = std::io::Cursor::new(&mut buf);
    thumb.write_to(&mut cursor, ImageFormat::Jpeg).ok()?;
    Some(format!(
        "data:image/jpeg;base64,{}",
        base64::engine::general_purpose::STANDARD.encode(&buf)
    ))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn smart_idle_telemetry_throttles_small_cpu_ram_deltas_under_6s() {
        let base = SystemMetricsSnapshot {
            cpu_percent: 12.0,
            ram_percent: 45.0,
            net_down_mbps: 0.0,
            net_up_mbps: 0.0,
            battery_present: true,
            battery_percent: 80,
            is_charging: false,
            ..Default::default()
        };
        assert!(should_push_metrics(None, &base, Duration::from_secs(0)));

        let tiny_jitter = SystemMetricsSnapshot {
            cpu_percent: 13.4,
            ram_percent: 46.2,
            ..base.clone()
        };
        assert!(!should_push_metrics(Some(&base), &tiny_jitter, Duration::from_millis(1500)));
        assert!(should_push_metrics(Some(&base), &tiny_jitter, Duration::from_secs(6)));

        let cpu_spike = SystemMetricsSnapshot { cpu_percent: 14.5, ..base.clone() };
        assert!(should_push_metrics(Some(&base), &cpu_spike, Duration::from_millis(1500)));

        let ram_spike = SystemMetricsSnapshot { ram_percent: 47.1, ..base.clone() };
        assert!(should_push_metrics(Some(&base), &ram_spike, Duration::from_millis(1500)));

        let net_burst = SystemMetricsSnapshot { net_down_mbps: 0.12, ..base.clone() };
        assert!(should_push_metrics(Some(&base), &net_burst, Duration::from_millis(1500)));

        let bat_drop = SystemMetricsSnapshot { battery_percent: 79, ..base.clone() };
        assert!(should_push_metrics(Some(&base), &bat_drop, Duration::from_millis(1500)));
    }
}
