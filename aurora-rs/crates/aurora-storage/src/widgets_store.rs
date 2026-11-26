use std::path::PathBuf;

use aurora_core::{
    default_ai_quota_configs, default_countdown, default_dock_widgets, default_starter_widgets,
    AiProviderQuotaConfig, CountdownConfig, DesktopWidgetInstance, DockWidgetConfig, TodoItem,
};
use serde::{Deserialize, Serialize};

use crate::{atomic_write_bytes, aurora_data_dir};

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct WidgetsPersistenceDoc {
    #[serde(default = "default_starter_widgets")]
    pub widgets: Vec<DesktopWidgetInstance>,
    #[serde(default = "default_ai_quota_configs")]
    pub ai_configs: Vec<AiProviderQuotaConfig>,
    #[serde(default = "default_dock_widgets")]
    pub dock_widgets: Vec<DockWidgetConfig>,
    #[serde(default)]
    pub todos: Vec<TodoItem>,
    #[serde(default = "default_countdown")]
    pub countdown: CountdownConfig,
}

impl Default for WidgetsPersistenceDoc {
    fn default() -> Self {
        Self {
            widgets: default_starter_widgets(),
            ai_configs: default_ai_quota_configs(),
            dock_widgets: default_dock_widgets(),
            todos: Vec::new(),
            countdown: default_countdown(),
        }
    }
}

/// Atomic JSON persistence for Desktop Widgets layout, Bottom Dock micro-widgets,
/// Todo/Countdown data, and AI Quota provider configs at `%LOCALAPPDATA%\ProjectAurora\data\widgets_layout.json`.
pub struct WidgetsLayoutStore {
    path: PathBuf,
}

impl WidgetsLayoutStore {
    pub fn new(custom_dir: Option<PathBuf>) -> Self {
        let dir = custom_dir.unwrap_or_else(aurora_data_dir);
        let _ = std::fs::create_dir_all(&dir);
        Self {
            path: dir.join("widgets_layout.json"),
        }
    }

    pub fn load(&self) -> WidgetsPersistenceDoc {
        if !self.path.exists() {
            let doc = WidgetsPersistenceDoc::default();
            self.save(&doc);
            return doc;
        }
        let mut doc: WidgetsPersistenceDoc = std::fs::read_to_string(&self.path)
            .ok()
            .and_then(|s| serde_json::from_str(&s).ok())
            .unwrap_or_default();
        // Ensure all 3 core AI providers exist in config list
        for def in default_ai_quota_configs() {
            if !doc.ai_configs.iter().any(|c| c.provider == def.provider) {
                doc.ai_configs.push(def);
            }
        }
        // Ensure all 14 Pro bottom Dock micro-widgets exist and normalize 4-zone fields on upgrade
        let mut upgraded = false;
        for w in &mut doc.dock_widgets {
            let before_z = w.zone.clone();
            w.normalize();
            if before_z != w.zone {
                upgraded = true;
            }
        }
        for def in default_dock_widgets() {
            if !doc.dock_widgets.iter().any(|w| w.kind == def.kind) {
                doc.dock_widgets.push(def);
                upgraded = true;
            }
        }
        if upgraded {
            self.save(&doc);
        }
        doc
    }

    pub fn save(&self, doc: &WidgetsPersistenceDoc) {
        if let Ok(bytes) = serde_json::to_vec_pretty(doc) {
            let _ = atomic_write_bytes(&self.path, &bytes);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn widgets_layout_roundtrip_and_starter_defaults() {
        let tmp = std::env::temp_dir().join(format!("aurora_wstore_test_{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&tmp);
        let store = WidgetsLayoutStore::new(Some(tmp.clone()));

        let initial = store.load();
        assert_eq!(initial.widgets.len(), 2, "First run should seed 2 starter widgets");
        assert_eq!(initial.ai_configs.len(), 3, "All 3 AI providers (openai/gemini/claude) present");
        assert_eq!(initial.dock_widgets.len(), 14, "All 14 Pro bottom Dock micro-widgets seeded");

        let mut modified = initial.clone();
        modified.widgets.push(DesktopWidgetInstance {
            id: "w-ai-1".into(),
            kind: "ai-quota".into(),
            variant: "cards".into(),
            skin: "braun".into(),
            size: "L".into(),
            x: 32,
            y: 64,
            opacity: Some(0.88),
            accent_hex: Some("#38BDF8".into()),
            pinned_top: false,
            note_text: None,
            photo_path: None,
        });
        store.save(&modified);

        let reloaded = store.load();
        assert_eq!(reloaded.widgets.len(), 3);
        assert_eq!(reloaded.widgets[2].kind, "ai-quota");
        assert_eq!(reloaded.widgets[2].size, "L");

        let _ = std::fs::remove_dir_all(&tmp);
    }

    #[test]
    fn dock_widgets_and_todos_roundtrip_persistence() {
        let tmp = std::env::temp_dir().join(format!("aurora_dw_test_{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&tmp);
        let store = WidgetsLayoutStore::new(Some(tmp.clone()));

        let mut doc = store.load();
        if let Some(ai) = doc.dock_widgets.iter_mut().find(|w| w.kind == "ai-quota") {
            ai.enabled = true;
            ai.zone = "before-apps".into();
            ai.order = 0;
            ai.size = "wide".into();
            ai.style_mode = "tinted".into();
            ai.wheel_action = "volume".into();
            ai.variant = "compact".into();
            ai.normalize();
        }
        doc.todos.push(TodoItem {
            id: "t-1".into(),
            text: "Aurora v4.2 incele".into(),
            done: false,
        });
        doc.countdown = CountdownConfig {
            title: "Lansman".into(),
            target_date: "2026-12-31".into(),
        };
        store.save(&doc);

        let loaded = store.load();
        let ai = loaded.dock_widgets.iter().find(|w| w.kind == "ai-quota").unwrap();
        assert!(ai.enabled);
        assert_eq!(ai.zone, "before-apps");
        assert_eq!(ai.slot, "left");
        assert_eq!(ai.size, "wide");
        assert_eq!(ai.style_mode, "tinted");
        assert_eq!(ai.wheel_action, "volume");
        assert_eq!(ai.variant, "compact");
        assert_eq!(loaded.todos.len(), 1);
        assert_eq!(loaded.todos[0].text, "Aurora v4.2 incele");
        assert_eq!(loaded.countdown.title, "Lansman");

        let _ = std::fs::remove_dir_all(&tmp);
    }
}
