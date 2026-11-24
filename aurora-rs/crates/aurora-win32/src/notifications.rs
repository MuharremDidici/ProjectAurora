use std::collections::HashSet;
use std::path::{Path, PathBuf};
use aurora_core::{AppWindowEntry, SystemToastNotification};
use chrono::{DateTime, Utc};
use rusqlite::{Connection, OpenFlags};
use windows::core::{PCWSTR, HSTRING};
use windows::Win32::System::Com::{CoCreateInstance, CLSCTX_LOCAL_SERVER};
use windows::Win32::UI::Shell::{
    ApplicationActivationManager, IApplicationActivationManager, ShellExecuteW, AO_NONE,
};
use windows::Win32::UI::WindowsAndMessaging::SW_SHOWNORMAL;

use crate::windows_catalog::focus_window;

/// Schema-adaptive Windows 11 `wpndatabase.db` reader with shared-read WAL snapshot (never locks WpnUserService!).
/// Tracks `(len, mtime)` of `wpndatabase.db` + `-wal` so idle ticks perform ZERO disk copies & ZERO SQLite opens!
pub struct WpnNotificationBridge {
    wpn_db_path: PathBuf,
    snap_dir: PathBuf,
    seen_ids: HashSet<i64>,
    high_water_ft: i64,
    initialized: bool,
    last_db_sig: Option<(u64, u128, u64, u128)>,
    recent: Vec<SystemToastNotification>,
}

fn file_sig(path: &Path) -> (u64, u128) {
    match std::fs::metadata(path) {
        Ok(m) => {
            let ns = m
                .modified()
                .ok()
                .and_then(|t| t.duration_since(std::time::UNIX_EPOCH).ok())
                .map(|d| d.as_nanos())
                .unwrap_or(0);
            (m.len(), ns)
        }
        Err(_) => (0, 0),
    }
}

impl Default for WpnNotificationBridge {
    fn default() -> Self {
        Self::new(None)
    }
}

impl WpnNotificationBridge {
    pub fn new(custom_db_path: Option<PathBuf>) -> Self {
        let local = std::env::var_os("LOCALAPPDATA")
            .map(PathBuf::from)
            .unwrap_or_else(|| PathBuf::from("."));
        let wpn_db_path = custom_db_path.unwrap_or_else(|| {
            local.join(r"Microsoft\Windows\Notifications\wpndatabase.db")
        });
        let snap_dir = local.join(r"ProjectAurora\cache\wpn_snap");
        Self {
            wpn_db_path,
            snap_dir,
            seen_ids: HashSet::new(),
            high_water_ft: 0,
            initialized: false,
            last_db_sig: None,
            recent: Vec::new(),
        }
    }

    pub fn recent(&self) -> &[SystemToastNotification] {
        &self.recent
    }

    /// Snapshots `wpndatabase.db` (+ `-wal`/`-shm`) ONLY when file metadata changed and returns brand-new toasts.
    pub fn poll_new_toasts(&mut self) -> Vec<SystemToastNotification> {
        if !self.wpn_db_path.exists() {
            return vec![];
        }
        let wal_src = PathBuf::from(format!("{}-wal", self.wpn_db_path.display()));
        let (db_len, db_ns) = file_sig(&self.wpn_db_path);
        let (wal_len, wal_ns) = file_sig(&wal_src);
        let cur_sig = (db_len, db_ns, wal_len, wal_ns);
        if self.initialized && self.last_db_sig == Some(cur_sig) {
            return vec![];
        }
        self.last_db_sig = Some(cur_sig);

        let _ = std::fs::create_dir_all(&self.snap_dir);
        let prefix = self.snap_dir.join(format!("wpn_rs_{}", std::process::id()));
        let snap_db = prefix.with_extension("db");
        let snap_wal = PathBuf::from(format!("{}-wal", snap_db.display()));
        let snap_shm = PathBuf::from(format!("{}-shm", snap_db.display()));

        if std::fs::copy(&self.wpn_db_path, &snap_db).is_err() {
            return vec![];
        }
        let shm_src = PathBuf::from(format!("{}-shm", self.wpn_db_path.display()));
        if wal_src.exists() { let _ = std::fs::copy(&wal_src, &snap_wal); } else { let _ = std::fs::remove_file(&snap_wal); }
        if shm_src.exists() { let _ = std::fs::copy(&shm_src, &snap_shm); } else { let _ = std::fs::remove_file(&snap_shm); }

        let parsed = read_toasts_from_sqlite(&snap_db);
        self.recent = parsed.clone();

        let mut arrived = Vec::new();
        if !self.initialized {
            self.initialized = true;
            for t in &parsed {
                self.seen_ids.insert(t.notification_id);
                if let Ok(dt) = DateTime::parse_from_rfc3339(&t.arrived_at_utc) {
                    let ft = unix_to_filetime(dt.timestamp());
                    if ft > self.high_water_ft {
                        self.high_water_ft = ft;
                    }
                }
            }
        } else {
            for t in parsed.into_iter().rev() {
                if self.seen_ids.insert(t.notification_id) {
                    arrived.push(t);
                }
            }
        }
        arrived
    }
}

pub fn read_toasts_from_sqlite(db_path: &Path) -> Vec<SystemToastNotification> {
    let Ok(conn) = Connection::open_with_flags(db_path, OpenFlags::SQLITE_OPEN_READ_ONLY | OpenFlags::SQLITE_OPEN_NO_MUTEX) else {
        return vec![];
    };

    // Schema-adaptive check for HandlerType column (Windows 10/11 variants)
    let has_handler_type = conn
        .prepare("PRAGMA table_info(NotificationHandler);")
        .ok()
        .and_then(|mut s| {
            let found = s
                .query_map([], |r| r.get::<_, String>(1))
                .ok()?
                .filter_map(|r| r.ok())
                .any(|c| c.eq_ignore_ascii_case("HandlerType"));
            Some(found)
        })
        .unwrap_or(false);

    let sql = if has_handler_type {
        "SELECT n.Id, COALESCE(h.PrimaryId, ''), COALESCE(h.HandlerType, ''), n.Payload, n.ArrivalTime
         FROM Notification n
         LEFT JOIN NotificationHandler h ON n.HandlerId = h.RecordId
         WHERE n.Type = 'toast' AND n.Payload IS NOT NULL
         ORDER BY n.ArrivalTime DESC LIMIT 32;"
    } else {
        "SELECT n.Id, COALESCE(h.PrimaryId, ''), '', n.Payload, n.ArrivalTime
         FROM Notification n
         LEFT JOIN NotificationHandler h ON n.HandlerId = h.RecordId
         WHERE n.Type = 'toast' AND n.Payload IS NOT NULL
         ORDER BY n.ArrivalTime DESC LIMIT 32;"
    };

    let Ok(mut stmt) = conn.prepare(sql) else {
        return vec![];
    };
    stmt.query_map([], |r| {
        let id: i64 = r.get(0)?;
        let primary_id: String = r.get(1)?;
        let handler_type: String = r.get(2)?;
        let payload_bytes: Vec<u8> = r.get(3)?;
        let arrival_ft: i64 = r.get(4)?;
        Ok((id, primary_id, handler_type, payload_bytes, arrival_ft))
    })
    .ok()
    .into_iter()
    .flatten()
    .filter_map(|r| r.ok())
    .filter_map(|(id, primary_id, handler_type, bytes, ft)| {
        let xml = String::from_utf8_lossy(&bytes);
        let (title, body, launch, act_type) = parse_toast_xml(&xml)?;
        let app_name = friendly_app_from_primary_id(&primary_id);
        let dt = filetime_to_utc(ft);
        Some(SystemToastNotification {
            notification_id: id,
            app_name,
            title,
            body,
            arrived_at_utc: dt.to_rfc3339(),
            primary_id,
            handler_type,
            launch_arguments: launch,
            activation_type: act_type,
        })
    })
    .collect()
}

/// Extracts `<text>` lines, `<toast launch="...">`, and `activationType` cleanly from Windows Toast XML.
pub fn parse_toast_xml(xml: &str) -> Option<(String, String, String, String)> {
    let mut texts = Vec::new();
    let mut rest = xml;
    while let Some(s) = rest.find("<text") {
        let after_open = &rest[s..];
        let Some(gt) = after_open.find('>') else { break };
        let body_start = &after_open[gt + 1..];
        let Some(end) = body_start.find("</text>") else { break };
        let t = unescape_xml(body_start[..end].trim());
        if !t.is_empty() {
            texts.push(t);
        }
        rest = &body_start[end + 7..];
    }
    if texts.is_empty() {
        return None;
    }
    let title = texts[0].clone();
    let body = texts.get(1..).map(|s| s.join(" — ")).unwrap_or_default();

    let toast_tag = xml
        .find("<toast")
        .and_then(|s| xml[s..].find('>').map(|e| &xml[s..s + e + 1]))
        .unwrap_or("");
    let mut launch = extract_xml_attr(toast_tag, "launch").unwrap_or_default();
    let mut act_type = extract_xml_attr(toast_tag, "activationType").unwrap_or_default();

    if launch.is_empty() {
        if let Some(a_idx) = xml.find("<action") {
            if let Some(a_end) = xml[a_idx..].find('>') {
                let tag = &xml[a_idx..a_idx + a_end + 1];
                let t = extract_xml_attr(tag, "activationType").unwrap_or_default();
                if t.is_empty() || t.eq_ignore_ascii_case("foreground") || t.eq_ignore_ascii_case("protocol") {
                    launch = extract_xml_attr(tag, "arguments").unwrap_or_default();
                    act_type = t;
                }
            }
        }
    }
    Some((title, body, launch, act_type))
}

fn extract_xml_attr(tag: &str, attr: &str) -> Option<String> {
    let needle = format!("{attr}=\"");
    let start = tag.find(&needle)? + needle.len();
    let end = tag[start..].find('"')?;
    Some(unescape_xml(&tag[start..start + end]))
}

fn unescape_xml(s: &str) -> String {
    s.replace("&amp;", "&")
        .replace("&lt;", "<")
        .replace("&gt;", ">")
        .replace("&quot;", "\"")
        .replace("&apos;", "'")
}

/// 4-Tier Native Zero-UAC Notification Activator (WhatsApp chat, Antigravity conversation, Teams, Snipping Tool!).
pub fn activate_notification(toast: &SystemToastNotification, open_windows: &[AppWindowEntry]) -> bool {
    // Tier 1: Direct protocol URI deep-link (antigravity-ide://, ms-screensketch:, whatsapp://, msteams:)
    if let Some(uri) = try_extract_launch_uri(&toast.launch_arguments, &toast.activation_type) {
        if shell_execute(&uri) {
            return true;
        }
    }

    // Tier 2: COM IApplicationActivationManager (MSIX / Store apps like WhatsApp Desktop!)
    let is_msix = toast.primary_id.contains('!') || toast.handler_type.to_ascii_lowercase().contains("immersive");
    if is_msix && !toast.primary_id.is_empty() {
        unsafe {
            if let Ok(mgr) = CoCreateInstance::<_, IApplicationActivationManager>(&ApplicationActivationManager, None, CLSCTX_LOCAL_SERVER) {
                let aumid = HSTRING::from(&toast.primary_id);
                let args = HSTRING::from(&toast.launch_arguments);
                if mgr.ActivateApplication(&aumid, &args, AO_NONE).is_ok() {
                    return true;
                }
            }
        }
    }

    // Tier 3: Match open desktop window (Electron / Win32 apps like Antigravity, Discord, Slack)
    let needle = toast.app_name.to_ascii_lowercase();
    let pid_low = toast.primary_id.to_ascii_lowercase();
    if let Some(win) = open_windows.iter().find(|w| {
        let pn = w.process_name.to_ascii_lowercase();
        let tl = w.title.to_ascii_lowercase();
        (!needle.is_empty() && (pn.contains(&needle) || tl.contains(&needle)))
            || (!pid_low.is_empty() && pid_low.contains(&pn) && pn.len() >= 3)
    }) {
        focus_window(win.hwnd);
        return true;
    }

    // Tier 4: shell:AppsFolder\<PrimaryId> fallback
    if !toast.primary_id.is_empty() {
        return shell_execute(&format!("shell:AppsFolder\\{}", toast.primary_id));
    }
    false
}

pub fn try_extract_launch_uri(launch_args: &str, activation_type: &str) -> Option<String> {
    let trimmed = launch_args.trim();
    if trimmed.is_empty() {
        return None;
    }
    let colon = trimmed.find(':')?;
    if !(2..=24).contains(&colon) {
        return None;
    }
    let scheme = &trimmed[..colon];
    if !scheme.chars().all(|c| c.is_ascii_alphanumeric() || matches!(c, '+' | '-' | '.')) {
        return None;
    }
    if activation_type.eq_ignore_ascii_case("protocol")
        || trimmed.contains("://")
        || scheme.starts_with("ms-")
        || matches!(scheme.to_ascii_lowercase().as_str(), "mailto" | "tel" | "whatsapp" | "msteams")
    {
        return Some(trimmed.to_string());
    }
    None
}

pub fn shell_execute(target: &str) -> bool {
    let wide: Vec<u16> = target.encode_utf16().chain(std::iter::once(0)).collect();
    unsafe {
        let r = ShellExecuteW(None, windows::core::w!("open"), PCWSTR(wide.as_ptr()), None, None, SW_SHOWNORMAL);
        (r.0 as isize) > 32
    }
}

fn friendly_app_from_primary_id(primary_id: &str) -> String {
    let low = primary_id.to_ascii_lowercase();
    if low.contains("whatsapp") { "WhatsApp".into() }
    else if low.contains("antigravity") || low.contains("visualstudiocode") { "Antigravity".into() }
    else if low.contains("teams") { "Microsoft Teams".into() }
    else if low.contains("screensketch") { "Ekran Alıntısı".into() }
    else if low.contains("discord") { "Discord".into() }
    else if low.contains("telegram") { "Telegram".into() }
    else if low.contains("outlook") { "Outlook".into() }
    else {
        primary_id
            .split(['!', '_', '.'])
            .find(|p| p.len() >= 3 && !p.eq_ignore_ascii_case("microsoft") && !p.eq_ignore_ascii_case("electron") && !p.eq_ignore_ascii_case("app"))
            .unwrap_or("Bildirim")
            .to_string()
    }
}

fn filetime_to_utc(ft: i64) -> DateTime<Utc> {
    const EPOCH_DIFF: i64 = 116_444_736_000_000_000;
    let secs = (ft - EPOCH_DIFF) / 10_000_000;
    DateTime::from_timestamp(secs, 0).unwrap_or_else(Utc::now)
}

fn unix_to_filetime(unix_secs: i64) -> i64 {
    const EPOCH_DIFF: i64 = 116_444_736_000_000_000;
    unix_secs * 10_000_000 + EPOCH_DIFF
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn toast_xml_and_protocol_extraction_work_for_whatsapp_and_antigravity() {
        let xml = r#"<toast launch="antigravity-ide://open?id=42" activationType="protocol"><visual><binding><text>Antigravity</text><text>Derleme tamamlandı</text></binding></visual></toast>"#;
        let (title, body, launch, act) = parse_toast_xml(xml).unwrap();
        assert_eq!(title, "Antigravity");
        assert_eq!(body, "Derleme tamamlandı");
        assert_eq!(try_extract_launch_uri(&launch, &act).unwrap(), "antigravity-ide://open?id=42");
        assert!(try_extract_launch_uri("chatId=905551112233@s.whatsapp.net", "foreground").is_none());
    }
}
