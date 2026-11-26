use std::collections::HashMap;
use serde::{Deserialize, Serialize};

use crate::tokens::{is_valid_hex_color, ThemeColorCustomization};

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
    #[serde(default)]
    pub aumid: Option<String>,
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
    #[serde(default)]
    pub aumid: Option<String>,
    #[serde(default)]
    pub audio_pid: Option<u32>,
    #[serde(default)]
    pub audio_volume: Option<u8>,
    #[serde(default)]
    pub audio_muted: bool,
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
    #[serde(default)]
    pub position_secs: u32,
    #[serde(default)]
    pub duration_secs: u32,
    #[serde(default)]
    pub timeline_updated_ms: i64,
    #[serde(default)]
    pub can_seek: bool,
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
    #[serde(default)]
    pub published_at_utc: String,
    pub download_url: String,
    #[serde(default)]
    pub sha256: String,
    #[serde(default)]
    pub release_notes: String,
}

fn default_true() -> bool { true }
fn default_mag() -> i32 { 48 }
fn default_omni_hotkey() -> String { "Alt+Space".into() }
fn default_dock_density() -> String { "Standard".into() }
fn default_snap_preset() -> String { "code6535".into() }
fn default_dock_edge() -> String { "Bottom".into() }
fn default_dock_scale() -> i32 { 100 }
fn default_dock_layout() -> String { "Split3Zone".into() }
fn default_crown_mode() -> String { "Dynamic".into() }
fn default_dock_hub_style() -> String { "dendritic".into() }
fn default_dock_hover_style() -> String { "lift".into() }
fn default_corner_radius_preset() -> String { "modern".into() }
fn default_day_theme_id() -> String { "nordic-frost-light".into() }
fn default_night_theme_id() -> String { "aurora-glass-dark".into() }
fn default_dock_start_style() -> String { "icon".into() }
fn default_dock_silhouette() -> String { "fixed".into() }
fn default_dock_indicator_style() -> String { "pill".into() }
fn default_dw_size() -> String { "normal".into() }
fn default_dw_style() -> String { "pill".into() }
fn default_dw_wheel() -> String { "default".into() }
fn default_empty_wheel() -> String { "volume".into() }
fn default_empty_dblclick() -> String { "show-desktop".into() }
fn default_empty_midclick() -> String { "media-play".into() }
fn default_shell_architecture() -> String { "tahoe-glass".into() }
fn default_island_style() -> String { "liquid-pill".into() }
fn default_island_idle_mode() -> String { "media-ambient".into() }
fn default_notification_mode() -> String { "windows-native".into() }

fn default_widget_variant() -> String { "default".into() }
fn default_widget_size() -> String { "M".into() }
fn default_widget_skin() -> String { "glass".into() }
fn default_ai_budget() -> f64 { 20.0 }
fn default_ai_window_limit() -> u32 { 80 }
fn default_ai_window_mins() -> u32 { 180 }

/// A single user-configurable Desktop Widget instance rendered on the zero-overhead
/// `HWND_BOTTOM` desktop widget canvas (`widgets.html`).
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DesktopWidgetInstance {
    pub id: String,
    /// Widget category (e.g. "clock" | "media" | "battery" | "ai-quota" | "weather" | "system" | "calendar" | "notes" | "pomodoro" | "launcher" | "audio-mixer" | "focus-rings" | "photo-frame" | "quick-drop")
    pub kind: String,
    /// Visual style variant per widget (e.g. clock: "solar"|"braun-pro"|"dotmatrix-led"|"orbital"|"editorial-poster"|"flip"|"world"|"word"|"dev-hex"|"hero")
    #[serde(default = "default_widget_variant")]
    pub variant: String,
    /// v6.0 Industrial Design Archetype: "frameless" | "glass" | "neoglass" | "braun" | "matrix" | "editorial"
    #[serde(default = "default_widget_skin")]
    pub skin: String,
    /// "S" | "M" | "L"
    #[serde(default = "default_widget_size")]
    pub size: String,
    /// DIP X coordinate on desktop canvas (negative values anchor from right monitor edge!)
    pub x: i32,
    /// DIP Y coordinate on desktop canvas
    pub y: i32,
    /// Custom per-widget glass opacity (0.45..=1.0), None = follow active Aurora theme
    #[serde(default)]
    pub opacity: Option<f32>,
    /// Optional per-widget accent override hex
    #[serde(default)]
    pub accent_hex: Option<String>,
    /// Keep this widget floating above normal windows (when false, stays strictly on desktop layer!)
    #[serde(default)]
    pub pinned_top: bool,
    /// Optional widget-specific note or title
    #[serde(default)]
    pub note_text: Option<String>,
    /// Optional local photo path for `photo-frame` desktop widget
    #[serde(default)]
    pub photo_path: Option<String>,
}

/// Alias matching callers referring to `DesktopWidget`.
pub type DesktopWidget = DesktopWidgetInstance;

/// Curated list of supported desktop widget kinds/archetypes.
pub const SUPPORTED_DESKTOP_WIDGET_KINDS: &[&str] = &[
    "clock",
    "media",
    "battery",
    "ai-quota",
    "weather",
    "system",
    "calendar",
    "notes",
    "pomodoro",
    "launcher",
    "audio-mixer",
    "focus-rings",
    "photo-frame",
    "quick-drop",
    "todo",
    "market",
    "countdown",
    "storage-net",
    "quote",
    "local-ai",
    "studio-strip",
    "world-clock",
    "forecast-strip",
    "habit-ring",
    "pomodoro-studio",
    "currency-ticker",
    "quick-note",
];

impl DesktopWidgetInstance {
    /// Returns `true` if `self.kind` is one of the recognized Aurora desktop widget kinds.
    pub fn is_supported_kind(&self) -> bool {
        let k = self.kind.trim().to_ascii_lowercase();
        SUPPORTED_DESKTOP_WIDGET_KINDS.contains(&k.as_str())
    }

    /// Normalizes widget fields (`kind`, `variant`, `skin`, `size`, coordinates, opacity, accent, note)
    /// cleanly without panicking and with full support for all v7.3+ studio widget kinds/variants.
    pub fn normalize(&mut self) {
        let kind_norm = self.kind.trim().to_ascii_lowercase();
        self.kind = if kind_norm.is_empty() {
            "clock".into()
        } else {
            kind_norm
        };

        let var_norm = self.variant.trim().to_ascii_lowercase();
        self.variant = if var_norm.is_empty() || var_norm == "default" {
            match self.kind.as_str() {
                "clock" => "swiss".into(),
                "media" | "studio-strip" => "studio-strip".into(),
                "world-clock" => "world-clock".into(),
                "weather" => "dynamic-sky".into(),
                "forecast-strip" => "forecast-strip".into(),
                "battery" | "system" | "focus-rings" => "rings".into(),
                "habit-ring" => "habit-ring".into(),
                "ai-quota" => "cards".into(),
                "calendar" => "today".into(),
                "notes" => "glass".into(),
                "quick-note" => "quick-note".into(),
                "pomodoro" => "ring".into(),
                "pomodoro-studio" => "pomodoro-studio".into(),
                "launcher" => "grid".into(),
                "audio-mixer" => "mixer".into(),
                "photo-frame" => "polaroid".into(),
                "quick-drop" => "drop".into(),
                "todo" => "list".into(),
                "market" => "cards".into(),
                "currency-ticker" => "currency-ticker".into(),
                "countdown" => "card".into(),
                "storage-net" => "bars".into(),
                "quote" => "editorial".into(),
                "local-ai" => "status".into(),
                _ => "default".into(),
            }
        } else {
            var_norm
        };

        let skin_norm = self.skin.trim().to_ascii_lowercase();
        self.skin = if matches!(
            skin_norm.as_str(),
            "frameless" | "glass" | "neoglass" | "braun" | "matrix" | "editorial"
        ) {
            skin_norm
        } else if matches!(self.kind.as_str(), "clock" | "world-clock") {
            "frameless".into()
        } else {
            "glass".into()
        };

        let size_norm = self.size.trim().to_ascii_uppercase();
        self.size = match size_norm.as_str() {
            "S" | "M" | "L" => size_norm,
            _ => "M".into(),
        };

        self.x = self.x.clamp(-3800, 3800);
        self.y = self.y.clamp(8, 2200);
        self.opacity = self
            .opacity
            .filter(|op| op.is_finite())
            .map(|op| op.clamp(0.45, 1.0));
        self.accent_hex = self
            .accent_hex
            .as_deref()
            .map(str::trim)
            .filter(|hex| is_valid_hex_color(hex))
            .map(|hex| hex.to_uppercase());
        if let Some(note) = &mut self.note_text {
            if note.chars().count() > 1200 {
                *note = note.chars().take(1200).collect();
            }
        }
    }
}

/// Tasteful 2-widget first-run desktop starter layout (right-side Frameless Swiss Clock & Optical Glass Battery Rings).
pub fn default_starter_widgets() -> Vec<DesktopWidgetInstance> {
    vec![
        DesktopWidgetInstance {
            id: "w-clock-1".into(),
            kind: "clock".into(),
            variant: "swiss".into(),
            skin: "frameless".into(),
            size: "M".into(),
            x: -344,
            y: 48,
            opacity: None,
            accent_hex: None,
            pinned_top: false,
            note_text: None,
            photo_path: None,
        },
        DesktopWidgetInstance {
            id: "w-battery-1".into(),
            kind: "battery".into(),
            variant: "rings".into(),
            skin: "glass".into(),
            size: "M".into(),
            x: -344,
            y: 236,
            opacity: None,
            accent_hex: None,
            pinned_top: false,
            note_text: None,
            photo_path: None,
        },
    ]
}

/// Per-provider configuration for the 3-layer real hybrid AI Quota widget (ChatGPT / Gemini / Claude).
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AiProviderQuotaConfig {
    /// "openai" | "gemini" | "claude"
    pub provider: String,
    /// Display plan label ("Plus" | "Pro" | "Max" | "API" | "Free")
    pub plan_name: String,
    #[serde(default = "default_ai_budget")]
    pub monthly_budget_usd: f64,
    #[serde(default = "default_ai_window_limit")]
    pub window_limit: u32,
    #[serde(default = "default_ai_window_mins")]
    pub window_minutes: u32,
    #[serde(default)]
    pub manual_used: u32,
    #[serde(default)]
    pub window_started_epoch: i64,
    #[serde(default)]
    pub has_api_key: bool,
}

pub fn default_ai_quota_configs() -> Vec<AiProviderQuotaConfig> {
    vec![
        AiProviderQuotaConfig {
            provider: "openai".into(),
            plan_name: "Plus".into(),
            monthly_budget_usd: 20.0,
            window_limit: 80,
            window_minutes: 180,
            manual_used: 0,
            window_started_epoch: 0,
            has_api_key: false,
        },
        AiProviderQuotaConfig {
            provider: "gemini".into(),
            plan_name: "Pro".into(),
            monthly_budget_usd: 20.0,
            window_limit: 100,
            window_minutes: 1440,
            manual_used: 0,
            window_started_epoch: 0,
            has_api_key: false,
        },
        AiProviderQuotaConfig {
            provider: "claude".into(),
            plan_name: "Pro".into(),
            monthly_budget_usd: 20.0,
            window_limit: 45,
            window_minutes: 300,
            manual_used: 0,
            window_started_epoch: 0,
            has_api_key: false,
        },
    ]
}

/// Live computed snapshot returned by `aurora-win32::ai_quota` for each AI provider.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AiProviderQuotaSnapshot {
    pub provider: String,
    pub display_name: String,
    pub plan_name: String,
    /// Honest source badge: "API Canlı" | "CLI Log" | "Sayaç"
    pub source_badge: String,
    /// Remaining quota percentage (0..=100)
    pub remaining_percent: u8,
    pub primary_label: String,
    pub secondary_label: String,
    pub used_count: u64,
    pub limit_count: u64,
    pub tokens_today: u64,
    pub api_spend_usd: Option<f64>,
    pub has_api_key: bool,
    pub usage_url: String,
    pub updated_at_hhmm: String,
}

/// Modular Pro micro-widget embedded directly inside the bottom Dock (4 zones, free drag-reorder, 0 extra VRAM).
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DockWidgetConfig {
    pub id: String,
    /// "clock" | "media" | "ai-quota" | "battery" | "weather" | "system" | "network" | "pomodoro"
    /// | "market" | "clipboard" | "volume-slider" | "brightness-slider" | "mic-privacy" | "desktops"
    pub kind: String,
    /// Legacy v4.1 compatibility ("left" | "right"); mirrored from `zone`.
    #[serde(default)]
    pub slot: String,
    /// v4.2 4-Zone placement: "far-left" | "before-apps" | "after-apps" | "far-right"
    #[serde(default)]
    pub zone: String,
    /// Free drag-and-drop sort index within the zone
    #[serde(default)]
    pub order: i32,
    #[serde(default = "default_widget_variant")]
    pub variant: String,
    /// "compact" | "normal" | "wide"
    #[serde(default = "default_dw_size")]
    pub size: String,
    /// "pill" | "clean" | "tinted"
    #[serde(default = "default_dw_style")]
    pub style_mode: String,
    /// "default" | "volume" | "brightness" | "desktop" | "media" | "none"
    #[serde(default = "default_dw_wheel")]
    pub wheel_action: String,
    #[serde(default = "default_true")]
    pub enabled: bool,
}

impl DockWidgetConfig {
    pub fn effective_zone(&self) -> &'static str {
        match self.zone.as_str() {
            "far-left" => "far-left",
            "before-apps" => "before-apps",
            "after-apps" => "after-apps",
            "far-right" => "far-right",
            _ => {
                if self.slot == "right" {
                    "far-right"
                } else {
                    "far-left"
                }
            }
        }
    }

    pub fn normalize(&mut self) {
        let z = self.effective_zone();
        self.zone = z.into();
        self.slot = if matches!(z, "after-apps" | "far-right") {
            "right".into()
        } else {
            "left".into()
        };
        if !matches!(self.size.as_str(), "compact" | "normal" | "wide") {
            self.size = "normal".into();
        }
        if !matches!(self.style_mode.as_str(), "pill" | "clean" | "tinted" | "glass" | "hardware") {
            self.style_mode = "pill".into();
        }
        if !matches!(
            self.wheel_action.as_str(),
            "default" | "volume" | "brightness" | "desktop" | "media" | "none"
        ) {
            self.wheel_action = "default".into();
        }
    }
}

fn mk_dw(id: &str, kind: &str, zone: &str, order: i32, variant: &str, enabled: bool) -> DockWidgetConfig {
    let slot = if matches!(zone, "after-apps" | "far-right") { "right" } else { "left" };
    DockWidgetConfig {
        id: id.into(),
        kind: kind.into(),
        slot: slot.into(),
        zone: zone.into(),
        order,
        variant: variant.into(),
        size: "normal".into(),
        style_mode: "clean".into(),
        wheel_action: "default".into(),
        enabled,
    }
}

pub fn default_dock_widgets() -> Vec<DockWidgetConfig> {
    vec![
        mk_dw("dw-media", "media", "far-left", 0, "studio-bar", true),
        mk_dw("dw-weather", "weather", "far-left", 1, "studio", true),
        mk_dw("dw-clock", "clock", "far-right", 0, "classic", true),
        mk_dw("dw-volume", "volume-slider", "after-apps", 0, "bar", false),
        mk_dw("dw-brightness", "brightness-slider", "after-apps", 1, "bar", false),
        mk_dw("dw-mic", "mic-privacy", "before-apps", 0, "pill", false),
        mk_dw("dw-desktops", "desktops", "before-apps", 1, "pills", false),
        mk_dw("dw-ai", "ai-quota", "far-right", 1, "dots", false),
        mk_dw("dw-battery", "battery", "far-right", 2, "studio", false),
        mk_dw("dw-system", "system", "far-left", 2, "studio", false),
        mk_dw("dw-network", "network", "far-left", 3, "speed", false),
        mk_dw("dw-pomodoro", "pomodoro", "before-apps", 2, "pill", false),
        mk_dw("dw-market", "market", "after-apps", 2, "ticker", false),
        mk_dw("dw-clipboard", "clipboard", "before-apps", 3, "latest", false),
    ]
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct TodoItem {
    pub id: String,
    pub text: String,
    #[serde(default)]
    pub done: bool,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CountdownConfig {
    pub title: String,
    /// ISO date string "YYYY-MM-DD"
    pub target_date: String,
}

pub fn default_countdown() -> CountdownConfig {
    CountdownConfig {
        title: "Yeni Yıl".into(),
        target_date: "2027-01-01".into(),
    }
}

#[derive(Debug, Clone, PartialEq, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MarketSnapshot {
    pub usd_try: f64,
    pub eur_try: f64,
    pub btc_usd: f64,
    pub btc_change_24h: f64,
    pub updated_at_hhmm: String,
    pub valid: bool,
}

#[derive(Debug, Clone, PartialEq, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DiskUsageEntry {
    pub drive: String,
    pub free_gb: f32,
    pub total_gb: f32,
    pub used_percent: u8,
}

#[derive(Debug, Clone, PartialEq, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct LocalAiStatusSnapshot {
    pub ollama_running: bool,
    pub ollama_models: Vec<String>,
    pub lmstudio_running: bool,
    pub lmstudio_models: Vec<String>,
    pub checked_at_hhmm: String,
}

/// 100% backward-compatible with v2.x/v3.x `%LOCALAPPDATA%\ProjectAurora\data\shell_settings.json`!
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ShellRuntimePreferences {
    #[serde(default)]
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
    #[serde(default = "default_true")]
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
    // ── v4.0 Flagship UX, Customization & Desktop Widget Engine Preferences ──
    #[serde(default = "default_crown_mode")]
    pub crown_visibility_mode: String,
    #[serde(default)]
    pub show_dock_status_cluster: Option<bool>,
    #[serde(default = "default_dock_hub_style")]
    pub dock_hub_style: String,
    #[serde(default = "default_dock_hover_style")]
    pub dock_hover_style: String,
    #[serde(default = "default_corner_radius_preset")]
    pub corner_radius_preset: String,
    #[serde(default)]
    pub auto_day_night_theme: bool,
    #[serde(default = "default_day_theme_id")]
    pub day_theme_id: String,
    #[serde(default = "default_night_theme_id")]
    pub night_theme_id: String,
    #[serde(default = "default_true")]
    pub widgets_enabled: bool,
    #[serde(default)]
    pub widgets_locked: bool,
    // ── v4.1 Modular Dock Widgets, 5 Silhouettes, Indicators & Eco Guard ──
    #[serde(default = "default_dock_start_style")]
    pub dock_start_style: String,
    #[serde(default = "default_dock_silhouette")]
    pub dock_silhouette: String,
    #[serde(default = "default_dock_indicator_style")]
    pub dock_indicator_style: String,
    #[serde(default = "default_true")]
    pub eco_mode_auto_on_low_battery: bool,
    // ── v4.2 Fluid Zero-Click Gestures, Scrubbing & Per-App Audio Controls ──
    #[serde(default = "default_empty_wheel")]
    pub dock_empty_wheel_action: String,
    #[serde(default = "default_empty_dblclick")]
    pub dock_empty_dblclick_action: String,
    #[serde(default = "default_empty_midclick")]
    pub dock_empty_midclick_action: String,
    #[serde(default = "default_true")]
    pub dock_scrub_gestures_enabled: bool,
    #[serde(default = "default_true")]
    pub dock_per_app_audio_enabled: bool,
    // ── v6.0 Next-Gen Shell Architectures & Live Dynamic Island ──
    #[serde(default = "default_shell_architecture")]
    pub shell_architecture: String,
    #[serde(default = "default_island_style")]
    pub island_style: String,
    #[serde(default = "default_island_idle_mode")]
    pub island_idle_mode: String,
    #[serde(default = "default_true")]
    pub island_bt_popin: bool,
    #[serde(default = "default_true")]
    pub island_privacy_guard: bool,
    // ── v7.0 Native Windows Notifications & Architectural Separation ──
    #[serde(default = "default_notification_mode")]
    pub notification_mode: String,
}

impl Default for ShellRuntimePreferences {
    fn default() -> Self {
        Self {
            smart_tidy_enabled: false,
            dock_auto_hide: false,
            per_monitor_window_filter: false,
            dock_magnification_percent: 48,
            crown_pinned: false,
            hide_native_taskbar_while_active: true,
            dwm_live_preview_enabled: true,
            system_notification_bridge_enabled: true,
            win_key_opens_omni_search: true,
            omni_hotkey_preset: "Alt+Space".into(),
            dock_visibility_mode: Some("AlwaysVisible".into()),
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
            dock_bar_layout: "Split3Zone".into(),
            theme_customizations: None,
            crown_visibility_mode: "Dynamic".into(),
            show_dock_status_cluster: Some(true),
            dock_hub_style: "dendritic".into(),
            dock_hover_style: "lift".into(),
            corner_radius_preset: "modern".into(),
            auto_day_night_theme: false,
            day_theme_id: "nordic-frost-light".into(),
            night_theme_id: "aurora-glass-dark".into(),
            widgets_enabled: true,
            widgets_locked: false,
            dock_start_style: "icon".into(),
            dock_silhouette: "fixed".into(),
            dock_indicator_style: "pill".into(),
            eco_mode_auto_on_low_battery: true,
            dock_empty_wheel_action: "volume".into(),
            dock_empty_dblclick_action: "show-desktop".into(),
            dock_empty_midclick_action: "media-play".into(),
            dock_scrub_gestures_enabled: true,
            dock_per_app_audio_enabled: true,
            shell_architecture: "tahoe-glass".into(),
            island_style: "liquid-pill".into(),
            island_idle_mode: "media-ambient".into(),
            island_bt_popin: true,
            island_privacy_guard: true,
            notification_mode: "windows-native".into(),
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

    pub fn effective_notification_mode(&self) -> &'static str {
        if self.notification_mode == "aurora-mirror" {
            "aurora-mirror"
        } else {
            "windows-native"
        }
    }

    pub fn effective_shell_architecture(&self) -> &'static str {
        match self.shell_architecture.as_str() {
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
        }
    }

    pub fn effective_island_style(&self) -> &'static str {
        match self.island_style.as_str() {
            "mac-notch" | "compact-notch" => "mac-notch",
            "edge-horizon" => "edge-horizon",
            "hardware-pod" => "hardware-pod",
            "glass-bar" => "glass-bar",
            "orbital-halo" => "orbital-halo",
            _ => "liquid-pill",
        }
    }

    pub fn effective_island_idle_mode(&self) -> &'static str {
        match self.island_idle_mode.as_str() {
            "auto-stealth" => "auto-stealth",
            "glance-weather" => "glance-weather",
            "system-pulse" => "system-pulse",
            "minimal-brand" => "minimal-brand",
            _ => "media-ambient",
        }
    }

    pub fn effective_crown_visibility_mode(&self) -> &'static str {
        match self.crown_visibility_mode.as_str() {
            "AlertsOnly" => "AlertsOnly",
            "Hidden" => "Hidden",
            _ => "Dynamic",
        }
    }

    pub fn effective_show_dock_status(&self) -> bool {
        self.show_dock_status_cluster.unwrap_or(true)
    }

    pub fn effective_dock_hub_style(&self) -> &'static str {
        match self.dock_hub_style.as_str() {
            "fan3d" => "fan3d",
            "cards" => "cards",
            "radial" => "radial",
            _ => "dendritic",
        }
    }

    pub fn effective_dock_hover_style(&self) -> &'static str {
        match self.dock_hover_style.as_str() {
            "wave" => "wave",
            "glow" => "glow",
            _ => "lift",
        }
    }

    pub fn effective_corner_radius_preset(&self) -> &'static str {
        match self.corner_radius_preset.as_str() {
            "sharp" => "sharp",
            "capsule" => "capsule",
            _ => "modern",
        }
    }

    pub fn effective_dock_start_style(&self) -> &'static str {
        match self.dock_start_style.as_str() {
            "capsule" => "capsule",
            _ => "icon",
        }
    }

    pub fn effective_dock_silhouette(&self) -> &'static str {
        match self.dock_silhouette.as_str() {
            "floating" => "floating",
            "islands" => "islands",
            "minimal-rail" => "minimal-rail",
            "compact" => "compact",
            _ => "fixed",
        }
    }

    pub fn effective_dock_indicator_style(&self) -> &'static str {
        match self.dock_indicator_style.as_str() {
            "dot" => "dot",
            "bar" => "bar",
            "frame" => "frame",
            "glow" => "glow",
            _ => "pill",
        }
    }

    pub fn effective_empty_wheel_action(&self) -> &'static str {
        match self.dock_empty_wheel_action.as_str() {
            "brightness" => "brightness",
            "desktop" => "desktop",
            "media" => "media",
            "none" => "none",
            _ => "volume",
        }
    }

    pub fn effective_empty_dblclick_action(&self) -> &'static str {
        match self.dock_empty_dblclick_action.as_str() {
            "quick-wheel" => "quick-wheel",
            "task-view" => "task-view",
            "lock" => "lock",
            "omni" => "omni",
            "awake" => "awake",
            "none" => "none",
            _ => "show-desktop",
        }
    }

    pub fn effective_empty_midclick_action(&self) -> &'static str {
        match self.dock_empty_midclick_action.as_str() {
            "mute" => "mute",
            "quick-wheel" => "quick-wheel",
            "show-desktop" => "show-desktop",
            "mic-mute" => "mic-mute",
            "ocr" => "ocr",
            "task-view" => "task-view",
            "none" => "none",
            _ => "media-play",
        }
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

/// Architecture-aware WorkArea reservation matching v6.0 visual heights (e.g. Linear Studio razor-thin 42px bottom bar).
pub fn compute_dock_reserved_work_area_for_arch(
    monitor_bounds: PhysicalRect,
    scale_factor: f64,
    dock_edge: &str,
    dock_scale_percent: i32,
    reserve: bool,
    dock_density: &str,
    shell_architecture: &str,
) -> PhysicalRect {
    if !reserve || monitor_bounds.width <= 0 || monitor_bounds.height <= 0 {
        return monitor_bounds;
    }
    if shell_architecture.eq_ignore_ascii_case("linear-studio")
        && !dock_edge.eq_ignore_ascii_case("Left")
        && !dock_edge.eq_ignore_ascii_case("Right")
    {
        let dpi = scale_factor.max(1.0);
        let mul = (if dock_scale_percent <= 0 { 100 } else { dock_scale_percent }).clamp(75, 135) as f64 / 100.0;
        let base = if dock_density.eq_ignore_ascii_case("Compact") {
            36.0
        } else if dock_density.eq_ignore_ascii_case("Cozy") {
            46.0
        } else {
            40.0
        };
        let inset = ((base * mul).clamp(34.0, 72.0) * dpi).round() as i32;
        let bottom = (monitor_bounds.y + 240).max(monitor_bounds.bottom() - inset);
        return PhysicalRect::new(monitor_bounds.x, monitor_bounds.y, monitor_bounds.width, bottom - monitor_bounds.y);
    }
    compute_dock_reserved_work_area(
        monitor_bounds,
        scale_factor,
        dock_edge,
        dock_scale_percent,
        reserve,
        dock_density,
    )
}

pub fn compute_appbar_strip_rect_for_arch(
    monitor_bounds: PhysicalRect,
    scale_factor: f64,
    dock_edge: &str,
    dock_scale_percent: i32,
    dock_density: &str,
    shell_architecture: &str,
) -> PhysicalRect {
    let wa = compute_dock_reserved_work_area_for_arch(
        monitor_bounds,
        scale_factor,
        dock_edge,
        dock_scale_percent,
        true,
        dock_density,
        shell_architecture,
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
        assert_eq!(prefs.effective_shell_architecture(), "tahoe-glass");
        assert_eq!(prefs.effective_island_style(), "liquid-pill");
        assert_eq!(prefs.effective_island_idle_mode(), "media-ambient");
        assert_eq!(prefs.effective_notification_mode(), "windows-native");
        assert!(prefs.island_bt_popin);
        assert!(prefs.island_privacy_guard);

        let mut legacy = prefs.clone();
        legacy.shell_architecture = "fluent-aurora".into();
        legacy.island_style = "compact-notch".into();
        assert_eq!(legacy.effective_shell_architecture(), "fluent-mica");
        assert_eq!(legacy.effective_island_style(), "mac-notch");
    }

    #[test]
    fn all_v7_architectures_and_island_styles_resolve_cleanly() {
        let mut p = ShellRuntimePreferences::default();
        for arch in [
            "tahoe-glass", "nothing-hardware", "linear-studio", "vision-orb", "arc-split",
            "braun-atelier", "swiss-editorial", "cockpit-hud", "fluent-mica", "zen-ghost",
        ] {
            p.shell_architecture = arch.into();
            assert_eq!(p.effective_shell_architecture(), arch);
        }
        for isl in [
            "liquid-pill", "mac-notch", "edge-horizon", "hardware-pod", "glass-bar", "orbital-halo",
        ] {
            p.island_style = isl.into();
            assert_eq!(p.effective_island_style(), isl);
        }
    }

    #[test]
    fn fixed_bar_work_area_matches_44_52_60_exact_pixels() {
        let mon = PhysicalRect::new(0, 0, 2560, 1440);
        assert_eq!(mon.bottom() - compute_dock_reserved_work_area(mon, 1.0, "Bottom", 100, true, "Compact").bottom(), 44);
        assert_eq!(mon.bottom() - compute_dock_reserved_work_area(mon, 1.0, "Bottom", 100, true, "Standard").bottom(), 52);
        assert_eq!(mon.bottom() - compute_dock_reserved_work_area(mon, 1.0, "Bottom", 100, true, "Cozy").bottom(), 60);
        assert_eq!(mon.bottom() - compute_dock_reserved_work_area(mon, 1.25, "Bottom", 100, true, "Compact").bottom(), 55);
        assert_eq!(
            mon.bottom() - compute_dock_reserved_work_area_for_arch(mon, 1.0, "Bottom", 100, true, "Standard", "linear-studio").bottom(),
            40
        );
    }

    #[test]
    fn legacy_v4_desktop_widget_without_skin_deserializes_cleanly() {
        let raw = r#"{"id":"w-1","kind":"clock","variant":"minimal","size":"M","x":-320,"y":48}"#;
        let w: DesktopWidgetInstance = serde_json::from_str(raw).unwrap();
        assert_eq!(w.skin, "glass");
        assert_eq!(w.variant, "minimal");
        assert_eq!(w.size, "M");
        assert_eq!(w.photo_path, None);
    }

    #[test]
    fn shell_user_preferences_backward_compatibility_and_dock_indicator_styles() {
        let legacy: ShellUserPreferences = serde_json::from_str("{}").unwrap();
        assert_eq!(legacy.dock_indicator_style, None);
        assert_eq!(legacy.effective_dock_indicator_style(), "dot");
        assert_eq!(legacy.active_theme_id, "aurora-glass-dark");

        for style in ["dot", "bar", "glow", "pill"] {
            let json = format!(r#"{{"dockIndicatorStyle":"{style}"}}"#);
            let mut parsed: ShellUserPreferences = serde_json::from_str(&json).unwrap();
            assert_eq!(parsed.effective_dock_indicator_style(), style);
            parsed.normalize();
            assert_eq!(parsed.dock_indicator_style.as_deref(), Some(style));
        }

        let mut unknown = ShellUserPreferences {
            dock_indicator_style: Some("invalid-style".into()),
            ..Default::default()
        };
        assert_eq!(unknown.effective_dock_indicator_style(), "dot");
        unknown.normalize();
        assert_eq!(unknown.dock_indicator_style.as_deref(), Some("dot"));
    }

    #[test]
    fn desktop_widget_normalize_supports_all_new_studio_widgets() {
        let new_kinds = [
            "studio-strip",
            "world-clock",
            "forecast-strip",
            "habit-ring",
            "pomodoro-studio",
            "currency-ticker",
            "quick-note",
        ];
        for kind in new_kinds {
            let mut w = DesktopWidget {
                id: format!("w-{kind}"),
                kind: format!("  {}  ", kind.to_uppercase()),
                variant: String::new(),
                skin: "invalid-skin".into(),
                size: "xl".into(),
                x: -9999,
                y: -50,
                opacity: Some(0.10),
                accent_hex: Some("not-a-hex".into()),
                pinned_top: false,
                note_text: None,
                photo_path: None,
            };
            w.normalize();
            assert!(w.is_supported_kind(), "{kind} should be supported");
            assert_eq!(w.kind, kind);
            assert_eq!(w.variant, kind);
            assert_eq!(w.size, "M");
            assert_eq!(w.x, -3800);
            assert_eq!(w.y, 8);
            assert_eq!(w.opacity, Some(0.45));
            assert_eq!(w.accent_hex, None);
        }
    }
}

fn default_user_theme_id() -> String {
    "aurora-glass-dark".into()
}

fn default_user_profile_id() -> String {
    "profile-personal".into()
}

/// Portable user-level shell preferences with full backward compatibility for older JSON configs.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ShellUserPreferences {
    #[serde(default = "default_user_theme_id")]
    pub active_theme_id: String,
    #[serde(default = "default_user_profile_id")]
    pub active_profile_id: String,
    #[serde(default)]
    pub focus_mode_enabled: bool,
    #[serde(default)]
    pub reduced_motion: bool,
    #[serde(default)]
    pub dock_indicator_style: Option<String>,
}

impl Default for ShellUserPreferences {
    fn default() -> Self {
        Self {
            active_theme_id: default_user_theme_id(),
            active_profile_id: default_user_profile_id(),
            focus_mode_enabled: false,
            reduced_motion: false,
            dock_indicator_style: Some("dot".into()),
        }
    }
}

impl ShellUserPreferences {
    /// Resolves `dock_indicator_style` to one of `"dot" | "bar" | "glow" | "pill"` (default `"dot"`).
    pub fn effective_dock_indicator_style(&self) -> &'static str {
        match self
            .dock_indicator_style
            .as_deref()
            .map(|s| s.trim().to_ascii_lowercase())
            .as_deref()
        {
            Some("bar") => "bar",
            Some("glow") => "glow",
            Some("pill") => "pill",
            _ => "dot",
        }
    }

    pub fn normalize(&mut self) {
        if self.active_theme_id.trim().is_empty() {
            self.active_theme_id = default_user_theme_id();
        }
        if self.active_profile_id.trim().is_empty() {
            self.active_profile_id = default_user_profile_id();
        }
        self.dock_indicator_style = Some(self.effective_dock_indicator_style().into());
    }
}
