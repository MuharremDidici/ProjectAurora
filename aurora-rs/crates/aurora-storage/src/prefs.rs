use std::path::PathBuf;
use aurora_core::ShellRuntimePreferences;
use crate::{atomic_write_bytes, aurora_data_dir};

pub struct ShellPreferencesStore {
    file_path: PathBuf,
}

impl ShellPreferencesStore {
    pub fn new(custom_path: Option<PathBuf>) -> Self {
        let file_path = custom_path.unwrap_or_else(|| aurora_data_dir().join("shell_settings.json"));
        Self { file_path }
    }

    pub fn file_path(&self) -> &PathBuf {
        &self.file_path
    }

    pub fn load(&self) -> ShellRuntimePreferences {
        let Ok(raw) = std::fs::read_to_string(&self.file_path) else {
            return ShellRuntimePreferences::default();
        };
        let mut prefs: ShellRuntimePreferences =
            serde_json::from_str(&raw).unwrap_or_default();
        prefs.normalize();
        prefs
    }

    pub fn save(&self, prefs: &ShellRuntimePreferences) {
        let mut normalized = prefs.clone();
        normalized.normalize();
        if let Ok(json) = serde_json::to_vec_pretty(&normalized) {
            let _ = atomic_write_bytes(&self.file_path, &json);
        }
    }
}
