use std::collections::HashMap;
use std::path::{Path, PathBuf};
use aurora_core::{PinnedShortcut, ShellRuntimePreferences};
use chrono::Utc;
use serde::{Deserialize, Serialize};

use crate::{atomic_write_bytes, aurora_data_dir};

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AuxiliaryState {
    pub scratchpad_text: String,
    pub agenda_notes: HashMap<String, String>,
    pub drop_shelf_paths: Vec<String>,
    pub search_frecency: HashMap<String, u32>,
}

pub struct AuxiliaryStateStore {
    data_dir: PathBuf,
}

impl AuxiliaryStateStore {
    pub fn new(custom_dir: Option<PathBuf>) -> Self {
        let data_dir = custom_dir.unwrap_or_else(aurora_data_dir);
        let _ = std::fs::create_dir_all(&data_dir);
        Self { data_dir }
    }

    pub fn load(&self) -> AuxiliaryState {
        let scratchpad_text = std::fs::read_to_string(self.data_dir.join("scratchpad.txt")).unwrap_or_default();
        let agenda_notes = std::fs::read_to_string(self.data_dir.join("agenda_notes.json"))
            .ok()
            .and_then(|s| serde_json::from_str(&s).ok())
            .unwrap_or_default();
        let drop_shelf_paths = std::fs::read_to_string(self.data_dir.join("drop_shelf.json"))
            .ok()
            .and_then(|s| serde_json::from_str::<Vec<String>>(&s).ok())
            .unwrap_or_default()
            .into_iter()
            .filter(|p| Path::new(p).exists())
            .take(16)
            .collect();
        let search_frecency = std::fs::read_to_string(self.data_dir.join("search_frecency.json"))
            .ok()
            .and_then(|s| serde_json::from_str(&s).ok())
            .unwrap_or_default();

        AuxiliaryState {
            scratchpad_text,
            agenda_notes,
            drop_shelf_paths,
            search_frecency,
        }
    }

    pub fn save_scratchpad(&self, text: &str) {
        let _ = atomic_write_bytes(&self.data_dir.join("scratchpad.txt"), text.as_bytes());
    }

    pub fn save_agenda(&self, notes: &HashMap<String, String>) {
        if let Ok(b) = serde_json::to_vec_pretty(notes) {
            let _ = atomic_write_bytes(&self.data_dir.join("agenda_notes.json"), &b);
        }
    }

    pub fn save_drop_shelf(&self, paths: &[String]) {
        if let Ok(b) = serde_json::to_vec_pretty(paths) {
            let _ = atomic_write_bytes(&self.data_dir.join("drop_shelf.json"), &b);
        }
    }

    pub fn save_frecency(&self, map: &HashMap<String, u32>) {
        if let Ok(b) = serde_json::to_vec_pretty(map) {
            let _ = atomic_write_bytes(&self.data_dir.join("search_frecency.json"), &b);
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AuroraBackupBundle {
    pub exported_at_utc: String,
    pub aurora_version: String,
    pub active_theme_id: String,
    pub active_profile_id: String,
    pub shell_preferences: ShellRuntimePreferences,
    pub pinned_shortcuts: Vec<PinnedShortcut>,
    pub agenda_notes: HashMap<String, String>,
    pub scratchpad_text: String,
}

impl AuroraBackupBundle {
    pub fn export_to_file(&self, path: &Path) -> std::io::Result<()> {
        let bytes = serde_json::to_vec_pretty(self)
            .map_err(|e| std::io::Error::new(std::io::ErrorKind::InvalidData, e))?;
        atomic_write_bytes(path, &bytes)
    }

    pub fn import_from_file(path: &Path) -> Option<Self> {
        let raw = std::fs::read_to_string(path).ok()?;
        serde_json::from_str(&raw).ok()
    }
}

#[derive(Debug, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct BootSentinelState {
    consecutive_unclean_boots: u32,
    last_boot_utc: String,
}

/// Automatically triggers Safe Mode if 3 consecutive boots terminated abnormally within 90 seconds.
pub struct StartupCrashSentinel {
    path: PathBuf,
}

impl StartupCrashSentinel {
    pub fn new(custom_dir: Option<PathBuf>) -> Self {
        let dir = custom_dir.unwrap_or_else(aurora_data_dir);
        Self {
            path: dir.join("boot_sentinel.json"),
        }
    }

    pub fn record_boot_and_check_safe_mode(&self) -> bool {
        let mut st: BootSentinelState = std::fs::read_to_string(&self.path)
            .ok()
            .and_then(|s| serde_json::from_str(&s).ok())
            .unwrap_or_default();
        st.consecutive_unclean_boots = st.consecutive_unclean_boots.saturating_add(1);
        st.last_boot_utc = Utc::now().to_rfc3339();
        if let Ok(b) = serde_json::to_vec_pretty(&st) {
            let _ = atomic_write_bytes(&self.path, &b);
        }
        st.consecutive_unclean_boots >= 3
    }

    pub fn mark_healthy(&self) {
        let st = BootSentinelState {
            consecutive_unclean_boots: 0,
            last_boot_utc: Utc::now().to_rfc3339(),
        };
        if let Ok(b) = serde_json::to_vec_pretty(&st) {
            let _ = atomic_write_bytes(&self.path, &b);
        }
    }
}
