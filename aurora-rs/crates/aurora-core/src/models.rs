use std::collections::HashMap;
use serde::{Deserialize, Serialize};

use crate::tokens::ThemeColorCustomization;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Default, Serialize, Deserialize)]
pub struct PhysicalRect {
    pub x: i32,
    pub y: i32,
    pub width: i32,
    pub height: i32,
}

impl PhysicalRect {
    pub const fn new(x: i32, y: i32, width: i32, height: i32) -> Self {
        Self { x, y, width, height }
    }
    pub const fn right(self) -> i32 {
        self.x + self.width
    }
    pub const fn bottom(self) -> i32 {
        self.y + self.height
    }
    pub const fn contains(self, px: i32, py: i32) -> bool {
        px >= self.x && px < self.right() && py >= self.y && py < self.bottom()
    }
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MonitorDescriptor {
    pub device_id: String,
    pub friendly_name: String,
    pub is_primary: bool,
    pub effective_dpi: u32,
    pub bounds: PhysicalRect,
    pub work_area: PhysicalRect,
}

impl MonitorDescriptor {
    pub fn scale_factor(&self) -> f64 {
        (self.effective_dpi.max(96) as f64) / 96.0
    }
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AppWindowEntry {
    pub hwnd: isize,
    pub process_id: u32,
    pub process_name: String,
    pub executable_path: String,
    pub title: String,
    pub monitor_device_id: String,
    pub is_active: bool,
    pub is_minimized: bool,
    pub is_top_most: bool,
    pub needs_attention: bool,
    pub progress_percent: Option<u8>,
}

fn de_null_string<'de, D>(de: D) -> Result<String, D::Error>
where
    D: serde::Deserializer<'de>,
{
    Ok(Option::<String>::deserialize(de)?.unwrap_or_default())
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PinnedShortcut {
    pub id: String,
    pub display_name: String,
    #[serde(alias = "targetPath")]
    pub executable_path: String,
    #[serde(default, deserialize_with = "de_null_string")]
    pub arguments: String,
    #[serde(default)]
    pub icon_key: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DockAppGroup {
    pub group_key: String,
    pub display_name: String,
    pub executable_path: String,
    pub is_pinned: bool,
    pub pin_id: Option<String>,
    pub has_active: bool,
    pub needs_attention: bool,
    pub progress_percent: Option<u8>,
    pub windows: Vec<AppWindowEntry>,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct WorkspaceProfile {
    pub id: String,
    pub name: String,
    pub theme_id: String,
    #[serde(default)]
    pub focus_mode_enabled: bool,
    #[serde(default)]
    pub description: String,
}

pub fn default_workspace_profiles() -> Vec<WorkspaceProfile> {
    vec![
        WorkspaceProfile {
            id: "profile-personal".into(),
            name: "Kişisel".into(),
            theme_id: "aurora-glass-dark".into(),
            focus_mode_enabled: false,
            description: "Günlük masaüstü, medya ve sohbet akışı.".into(),
        },
        WorkspaceProfile {
            id: "profile-work".into(),
            name: "İş".into(),
            theme_id: "obsidian-pro-dark".into(),
            focus_mode_enabled: false,
            description: "Geliştirme, doküman ve üretkenlik odaklı düzen.".into(),
        },
        WorkspaceProfile {
            id: "profile-focus".into(),
            name: "Odak".into(),
            theme_id: "mono-matrix-dark".into(),
            focus_mode_enabled: true,
            description: "Bildirimleri susturur, derin çalışma ortamı açar.".into(),
        },
        WorkspaceProfile {
            id: "profile-game".into(),
            name: "Oyun".into(),
            theme_id: "nebula-dark".into(),
            focus_mode_enabled: true,
            description: "Tam ekran oyunlarda sıfır kesinti ve sıfır gecikme.".into(),
        },
    ]
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SystemToastNotification {
    pub notification_id: i64,
    pub app_name: String,
    pub title: String,
    pub body: String,
    pub arrived_at_utc: String,
    #[serde(default)]
    pub primary_id: String,
    #[serde(default)]
    pub handler_type: String,
    #[serde(default)]
    pub launch_arguments: String,
    #[serde(default)]
    pub activation_type: String,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ClipboardVaultEntry {
    pub id: i64,
    pub kind: String, // "text" | "image"
    pub text_content: String,
    pub preview_text: String,
    pub image_path: Option<String>,
    pub thumb_base64: Option<String>,
    pub image_width: i32,
    pub image_height: i32,
    pub source_app: String,
    pub is_pinned: bool,
    pub last_copied_utc: String,
}

#[derive(Debug, Clone, PartialEq, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AudioDeviceInfo {
    pub id: String,
    pub name: String,
    pub is_default: bool,
}

#[derive(Debug, Clone, PartialEq, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PerAppAudioSession {
    pub process_id: u32,
    pub process_name: String,
    pub display_name: String,
    pub volume_percent: u8,
    pub is_muted: bool,
    pub routed_device_id: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AudioStateSnapshot {
    pub master_volume_percent: u8,
    pub is_master_muted: bool,
    pub default_output_id: String,
    pub default_output_name: String,
    pub mic_supported: bool,
    pub is_mic_muted: bool,
    pub outputs: Vec<AudioDeviceInfo>,
    pub app_sessions: Vec<PerAppAudioSession>,
    pub per_app_routing_supported: bool,
}

#[derive(Debug, Clone, PartialEq, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MediaSessionSnapshot {
    pub is_playing: bool,
    pub has_session: bool,
    pub title: String,
    pub artist: String,
    pub app_name: String,
    pub thumbnail_bytes: Option<Vec<u8>>,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct BluetoothBatteryDevice {
    pub id: String,
    pub name: String,
    pub battery_percent: u8,
    pub category: String, // "headset" | "mouse" | "keyboard" | "gamepad" | "device"
    pub is_low: bool,
    #[serde(default = "default_true_bt")]
    pub has_battery: bool,
    #[serde(default = "default_true_bt")]
    pub is_connected: bool,
}

fn default_true_bt() -> bool {
    true
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ExternalMonitorBrightness {
    pub id: String,
    pub name: String,
    pub brightness_percent: u8,
    pub is_internal: bool,
}

#[derive(Debug, Clone, PartialEq, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PrivacySnapshot {
    pub camera_active: bool,
    pub mic_active: bool,
    pub camera_apps: Vec<String>,
    pub mic_apps: Vec<String>,
}

#[derive(Debug, Clone, PartialEq, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct TopProcessInfo {
    pub pid: u32,
    pub name: String,
    pub memory_mb: u32,
}

#[derive(Debug, Clone, PartialEq, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SystemMetricsSnapshot {
    pub cpu_percent: f32,
    pub ram_percent: f32,
    pub ram_used_gb: f32,
    pub ram_total_gb: f32,
    pub net_down_mbps: f32,
    pub net_up_mbps: f32,
    pub battery_present: bool,
    pub battery_percent: u8,
    pub is_charging: bool,
    pub top_processes: Vec<TopProcessInfo>,
}

#[derive(Debug, Clone, PartialEq, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct WeatherSnapshot {
    pub city: String,
    pub temp_c: f32,
    pub condition_tr: String,
    pub valid: bool,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct InstalledAppEntry {
    pub id: String,
    pub name: String,
    pub launch_path: String,
    pub category: String,
    pub keywords: String,
    pub kind: String, // "app" | "uwp" | "setting" | "tool"
    #[serde(default)]
    pub icon_base64: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct BackgroundTrayApp {
    pub id: String,
    pub name: String,
    pub executable_path: String,
    pub process_id: u32,
    #[serde(default)]
    pub memory_mb: u64,
    #[serde(default)]
    pub subtitle: String,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct HardwareOsdEvent {
    pub kind: String, // "volume" | "mute" | "brightness" | "mic" | "caps" | "num"
    pub label: String,
    pub value_text: String,
    pub percent: Option<u8>,
    pub active: bool,
    pub duration_ms: u32,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct LanQrShareInfo {
    pub url: String,
    pub file_name: String,
    pub size_label: String,
    pub matrix_size: usize,
    pub modules: Vec<bool>,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ReleaseManifest {
    pub version: String,
    pub published_at_utc: String,
    pub download_url: String,
    pub sha256: String,
    pub release_notes: String,
}

fn default_true() -> bool { true }
fn default_mag() -> i32 { 48 }
fn default_omni_hotkey() -> String { "Alt+Space".into() }
fn default_dock_density() -> String { "Standard".into() }
fn default_snap_preset() -> String { "code6535".into() }
fn default_dock_edge() -> String { "Bottom".into() }
fn default_dock_scale() -> i32 { 100 }
fn default_dock_layout() -> String { "Centered".into() }

/// 100% backward-compatible with v2.x `%LOCALAPPDATA%\ProjectAurora\data\shell_settings.json`!
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ShellRuntimePreferences {
    #[serde(default = "default_true")]
    pub smart_tidy_enabled: bool,
    #[serde(default)]
    pub dock_auto_hide: bool,
    #[serde(default)]
    pub per_monitor_window_filter: bool,
    #[serde(default = "default_mag")]
    pub dock_magnification_percent: i32,
    #[serde(default)]
    pub crown_pinned: bool,
    #[serde(default = "default_true")]
    pub hide_native_taskbar_while_active: bool,
    #[serde(default = "default_true")]
    pub dwm_live_preview_enabled: bool,
    #[serde(default = "default_true")]
    pub system_notification_bridge_enabled: bool,
    #[serde(default)]
    pub win_key_opens_omni_search: bool,
    #[serde(default = "default_omni_hotkey")]
    pub omni_hotkey_preset: String,
    #[serde(default)]
    pub dock_visibility_mode: Option<String>,
    #[serde(default)]
    pub dock_folder_paths: Option<Vec<String>>,
    #[serde(default)]
    pub onboarding_completed: bool,
    #[serde(default = "default_dock_density")]
    pub dock_density: String,
    #[serde(default)]
    pub show_clock_seconds: bool,
    #[serde(default = "default_true")]
    pub snap_zones_enabled: bool,
    #[serde(default = "default_snap_preset")]
    pub snap_zone_preset: String,
    #[serde(default)]
    pub update_manifest_url: String,
    #[serde(default = "default_true")]
    pub auto_check_updates_enabled: bool,
    #[serde(default = "default_dock_edge")]
    pub dock_edge: String,
    #[serde(default = "default_true")]
    pub hardware_osd_enabled: bool,
    #[serde(default = "default_true")]
    pub alt_drag_enabled: bool,
    #[serde(default = "default_dock_scale")]
    pub dock_scale_percent: i32,
    #[serde(default)]
    pub dock_offset_percent: i32,
    #[serde(default)]
    pub clipboard_auto_paste_enabled: bool,
    #[serde(default)]
    pub reserve_dock_work_area: Option<bool>,
    #[serde(default = "default_dock_layout")]
    pub dock_bar_layout: String,
    #[serde(default)]
    pub theme_customizations: Option<HashMap<String, ThemeColorCustomization>>,
}

impl Default for ShellRuntimePreferences {
    fn default() -> Self {
        Self {
            smart_tidy_enabled: true,
            dock_auto_hide: false,
            per_monitor_window_filter: false,
            dock_magnification_percent: 48,
            crown_pinned: false,
            hide_native_taskbar_while_active: true,
            dwm_live_preview_enabled: true,
            system_notification_bridge_enabled: true,
            win_key_opens_omni_search: false,
            omni_hotkey_preset: "Alt+Space".into(),
            dock_visibility_mode: None,
            dock_folder_paths: None,
            onboarding_completed: false,
            dock_density: "Standard".into(),
            show_clock_seconds: false,
            snap_zones_enabled: true,
            snap_zone_preset: "code6535".into(),
            update_manifest_url: String::new(),
            auto_check_updates_enabled: true,
            dock_edge: "Bottom".into(),
            hardware_osd_enabled: true,
            alt_drag_enabled: true,
            dock_scale_percent: 100,
            dock_offset_percent: 0,
            clipboard_auto_paste_enabled: false,
            reserve_dock_work_area: Some(true),
            dock_bar_layout: "Centered".into(),
            theme_customizations: None,
        }
    }
}

impl ShellRuntimePreferences {
    pub fn normalize(&mut self) {
        self.dock_magnification_percent = self.dock_magnification_percent.clamp(0, 80);
        if self.dock_scale_percent <= 0 {
            self.dock_scale_percent = 100;
        }
        self.dock_scale_percent = self.dock_scale_percent.clamp(75, 135);
        self.dock_offset_percent = self.dock_offset_percent.clamp(-100, 100);
    }

    pub fn effective_dock_mode(&self) -> &'static str {
        match self.dock_visibility_mode.as_deref() {
            Some("AlwaysVisible") => "AlwaysVisible",
            Some("SmartDodge") => "SmartDodge",
            Some("AutoHide") => "AutoHide",
            _ => {
                if self.dock_auto_hide {
                    "AutoHide"
                } else {
                    "AlwaysVisible"
                }
            }
        }
    }

    pub fn effective_reserve_pref(&self) -> bool {
        self.reserve_dock_work_area.unwrap_or(true)
    }

    pub fn effective_reserve_dock_work_area(&self) -> bool {
        self.effective_reserve_pref() && self.effective_dock_mode() == "AlwaysVisible"
    }

    pub fn effective_dock_density(&self) -> &'static str {
        match self.dock_density.as_str() {
            "Compact" => "Compact",
            "Cozy" => "Cozy",
            _ => "Standard",
        }
    }

    pub fn effective_dock_edge(&self) -> &'static str {
        match self.dock_edge.as_str() {
            "Left" => "Left",
            "Right" => "Right",
            _ => "Bottom",
        }
    }

    pub fn effective_dock_bar_layout(&self) -> &'static str {
        if self.dock_bar_layout == "Split3Zone" {
            "Split3Zone"
        } else {
            "Centered"
        }
    }

    pub fn effective_snap_preset(&self) -> &'static str {
        match self.snap_zone_preset.as_str() {
            "studio3" => "studio3",
            "cols3" => "cols3",
            "grid2x2" => "grid2x2",
            _ => "code6535",
        }
    }

    pub fn theme_customization(&self, theme_id: &str) -> Option<&ThemeColorCustomization> {
        self.theme_customizations.as_ref()?.iter().find_map(|(k, v)| {
            if k.eq_ignore_ascii_case(theme_id) {
                Some(v)
            } else {
                None
            }
        })
    }
}

/// Computes the exact pixel-flush WorkArea rectangle for Aurora's Pinned Fixed Bar
/// (Bottom 44/52/60px, Left/Right 50/58/66px at 100% scale).
pub fn compute_dock_reserved_work_area(
    monitor_bounds: PhysicalRect,
    scale_factor: f64,
    dock_edge: &str,
    dock_scale_percent: i32,
    reserve: bool,
    dock_density: &str,
) -> PhysicalRect {
    if !reserve || monitor_bounds.width <= 0 || monitor_bounds.height <= 0 {
        return monitor_bounds;
    }

    let dpi = scale_factor.max(1.0);
    let mul = (if dock_scale_percent <= 0 { 100 } else { dock_scale_percent }).clamp(75, 135) as f64 / 100.0;
    let compact = dock_density.eq_ignore_ascii_case("Compact");
    let cozy = dock_density.eq_ignore_ascii_case("Cozy");

    let mut left = monitor_bounds.x;
    let top = monitor_bounds.y;
    let mut right = monitor_bounds.right();
    let mut bottom = monitor_bounds.bottom();

    if dock_edge.eq_ignore_ascii_case("Left") {
        let base = if compact { 50.0 } else if cozy { 66.0 } else { 58.0 };
        let inset = ((base * mul).clamp(42.0, 92.0) * dpi).round() as i32;
        left = (right - 320).min(left + inset);
    } else if dock_edge.eq_ignore_ascii_case("Right") {
        let base = if compact { 50.0 } else if cozy { 66.0 } else { 58.0 };
        let inset = ((base * mul).clamp(42.0, 92.0) * dpi).round() as i32;
        right = (left + 320).max(right - inset);
    } else {
        let base = if compact { 44.0 } else if cozy { 60.0 } else { 52.0 };
        let inset = ((base * mul).clamp(38.0, 84.0) * dpi).round() as i32;
        bottom = (top + 240).max(bottom - inset);
    }

    PhysicalRect::new(left, top, right - left, bottom - top)
}

pub fn compute_appbar_strip_rect(
    monitor_bounds: PhysicalRect,
    scale_factor: f64,
    dock_edge: &str,
    dock_scale_percent: i32,
    dock_density: &str,
) -> PhysicalRect {
    let wa = compute_dock_reserved_work_area(
        monitor_bounds,
        scale_factor,
        dock_edge,
        dock_scale_percent,
        true,
        dock_density,
    );
    if dock_edge.eq_ignore_ascii_case("Left") {
        PhysicalRect::new(monitor_bounds.x, monitor_bounds.y, (wa.x - monitor_bounds.x).max(1), monitor_bounds.height)
    } else if dock_edge.eq_ignore_ascii_case("Right") {
        PhysicalRect::new(wa.right(), monitor_bounds.y, (monitor_bounds.right() - wa.right()).max(1), monitor_bounds.height)
    } else {
        PhysicalRect::new(monitor_bounds.x, wa.bottom(), monitor_bounds.width, (monitor_bounds.bottom() - wa.bottom()).max(1))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn legacy_v2_shell_settings_deserialize_safely() {
        let raw = r#"{"dockVisibilityMode":"AlwaysVisible","dockDensity":"Compact","dockEdge":"Bottom","dockScalePercent":999,"dockOffsetPercent":-999}"#;
        let mut prefs: ShellRuntimePreferences = serde_json::from_str(raw).unwrap();
        prefs.normalize();
        assert!(prefs.effective_reserve_pref());
        assert!(prefs.effective_reserve_dock_work_area());
        assert_eq!(prefs.dock_scale_percent, 135);
        assert_eq!(prefs.dock_offset_percent, -100);
    }

    #[test]
    fn fixed_bar_work_area_matches_44_52_60_exact_pixels() {
        let mon = PhysicalRect::new(0, 0, 2560, 1440);
        assert_eq!(mon.bottom() - compute_dock_reserved_work_area(mon, 1.0, "Bottom", 100, true, "Compact").bottom(), 44);
        assert_eq!(mon.bottom() - compute_dock_reserved_work_area(mon, 1.0, "Bottom", 100, true, "Standard").bottom(), 52);
        assert_eq!(mon.bottom() - compute_dock_reserved_work_area(mon, 1.0, "Bottom", 100, true, "Cozy").bottom(), 60);
        assert_eq!(mon.bottom() - compute_dock_reserved_work_area(mon, 1.25, "Bottom", 100, true, "Compact").bottom(), 55);
    }
}
