use std::path::PathBuf;

use aurora_core::{
    default_ai_quota_configs, default_countdown, default_dock_widgets, default_starter_widgets,
    AiProviderQuotaConfig, CountdownConfig, DesktopWidgetInstance, DockWidgetConfig, TodoItem,
};
use serde::{Deserialize, Serialize};

use crate::{atomic_write_bytes, aurora_data_dir};

/// Current schema version for bottom Dock micro-widgets polish & migration.
pub const CURRENT_DOCK_WIDGETS_SCHEMA_VER: u8 = 2;

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
    #[serde(default)]
    pub dock_widgets_schema_ver: u8,
}

impl Default for WidgetsPersistenceDoc {
    fn default() -> Self {
        Self {
            widgets: default_starter_widgets(),
            ai_configs: default_ai_quota_configs(),
            dock_widgets: default_dock_widgets(),
            todos: Vec::new(),
            countdown: default_countdown(),
            dock_widgets_schema_ver: CURRENT_DOCK_WIDGETS_SCHEMA_VER,
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
        // Ensure all 15 Pro bottom Dock micro-widgets exist and run one-time v2 polish migration
        let mut upgraded = false;
        if doc.dock_widgets_schema_ver < CURRENT_DOCK_WIDGETS_SCHEMA_VER {
            doc.dock_widgets_schema_ver = CURRENT_DOCK_WIDGETS_SCHEMA_VER;
            upgraded = true;
            for dw in &mut doc.dock_widgets {
                if matches!(dw.style_mode.as_str(), "pill" | "tinted" | "glass" | "") {
                    dw.style_mode = "clean".into();
                }
                if dw.kind == "media" && dw.variant == "vinyl" {
                    dw.variant = "studio-bar".into();
                }
                if dw.kind == "weather" && dw.variant == "pill" {
                    dw.variant = "studio".into();
                }
                if dw.kind == "clock" && (dw.variant == "classic" || dw.variant == "default" || dw.variant.is_empty()) {
                    dw.variant = "editorial-2line".into();
                }
            }
            // Prevent legacy 1280px+ dock crowding by trimming overcrowded enabled pill sets to airy defaults
            let enabled_count = doc.dock_widgets.iter().filter(|w| w.enabled).count();
            if enabled_count > 3 {
                let mut kept = 0usize;
                for dw in &mut doc.dock_widgets {
                    if !dw.enabled {
                        continue;
                    }
                    let is_core = matches!(dw.kind.as_str(), "media" | "weather" | "clock");
                    if is_core && kept < 3 {
                        kept += 1;
                    } else {
                        dw.enabled = false;
                    }
                }
            }
        }
        for w in &mut doc.dock_widgets {
            let before_z = w.zone.clone();
            let before_v = w.variant.clone();
            let before_s = w.style_mode.clone();
            w.normalize();
            if before_z != w.zone || before_v != w.variant || before_s != w.style_mode {
                upgraded = true;
            }
        }
        for def in default_dock_widgets() {
            if !doc.dock_widgets.iter().any(|w| w.kind == def.kind) {
                doc.dock_widgets.push(def);
                upgraded = true;
            }
        }
        let healed_cd = heal_turkish_mojibake(&doc.countdown.title);
        if healed_cd != doc.countdown.title {
            doc.countdown.title = healed_cd;
            upgraded = true;
        }
        for todo in &mut doc.todos {
            let healed_t = heal_turkish_mojibake(&todo.text);
            if healed_t != todo.text {
                todo.text = healed_t;
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

pub fn heal_turkish_mojibake(s: &str) -> String {
    if !s.contains('Ä') && !s.contains('Ã') && !s.contains('Å') {
        return s.to_string();
    }
    let mut out = s.to_string();
    for (bad, good) in [
        ("Ä±", "ı"),
        ("Ä°", "İ"),
        ("Ã§", "ç"),
        ("Ã‡", "Ç"),
        ("ÅŸ", "ş"),
        ("Åž", "Ş"),
        ("ÄŸ", "ğ"),
        ("Äž", "Ğ"),
        ("Ã¶", "ö"),
        ("Ã–", "Ö"),
        ("Ã¼", "ü"),
        ("Ãœ", "Ü"),
    ] {
        if out.contains(bad) {
            out = out.replace(bad, good);
        }
    }
    out
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
        assert_eq!(initial.dock_widgets.len(), 15, "All 15 Pro bottom Dock micro-widgets seeded");
        assert_eq!(initial.dock_widgets_schema_ver, CURRENT_DOCK_WIDGETS_SCHEMA_VER);

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

    #[test]
    fn legacy_dock_widgets_migrate_to_studio_clean_once() {
        let tmp = std::env::temp_dir().join(format!("aurora_dw_mig_{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&tmp);
        std::fs::create_dir_all(&tmp).unwrap();
        let legacy_json = r#"{
            "dockWidgets": [
                {"id":"dw-media","kind":"media","slot":"left","zone":"far-left","order":0,"variant":"vinyl","size":"normal","styleMode":"pill","wheelAction":"default","enabled":true},
                {"id":"dw-weather","kind":"weather","slot":"left","zone":"far-left","order":1,"variant":"pill","size":"normal","styleMode":"pill","wheelAction":"default","enabled":true}
            ]
        }"#;
        std::fs::write(tmp.join("widgets_layout.json"), legacy_json).unwrap();
        let store = WidgetsLayoutStore::new(Some(tmp.clone()));
        let mut doc = store.load();
        assert_eq!(doc.dock_widgets_schema_ver, 2);
        let med = doc.dock_widgets.iter().find(|w| w.kind == "media").unwrap();
        assert_eq!(med.variant, "studio-bar");
        assert_eq!(med.style_mode, "clean");
        let wth = doc.dock_widgets.iter().find(|w| w.kind == "weather").unwrap();
        assert_eq!(wth.variant, "studio");

        // User changes style back to pill afterwards -> schema_ver == 2 preserves override!
        doc.dock_widgets.iter_mut().find(|w| w.kind == "media").unwrap().style_mode = "pill".into();
        store.save(&doc);
        let reloaded = store.load();
        assert_eq!(reloaded.dock_widgets.iter().find(|w| w.kind == "media").unwrap().style_mode, "pill");

        let _ = std::fs::remove_dir_all(&tmp);
    }

    #[test]
    fn dock_widgets_schema_ver_2_migration_polishes_v1_crowded_pills_once() {
        let tmp = std::env::temp_dir().join(format!("aurora_dw_mig_v2_{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&tmp);
        std::fs::create_dir_all(&tmp).unwrap();
        let v1_json = r#"{
            "dockWidgetsSchemaVer": 1,
            "dockWidgets": [
                {"id":"dw-media","kind":"media","slot":"left","zone":"far-left","order":0,"variant":"studio-bar","size":"normal","styleMode":"pill","wheelAction":"default","enabled":true},
                {"id":"dw-weather","kind":"weather","slot":"left","zone":"far-left","order":1,"variant":"studio","size":"normal","styleMode":"tinted","wheelAction":"default","enabled":true},
                {"id":"dw-clock","kind":"clock","slot":"right","zone":"far-right","order":0,"variant":"classic","size":"normal","styleMode":"glass","wheelAction":"default","enabled":true},
                {"id":"dw-volume","kind":"volume-slider","slot":"right","zone":"after-apps","order":0,"variant":"bar","size":"normal","styleMode":"pill","wheelAction":"default","enabled":true},
                {"id":"dw-mic","kind":"mic-privacy","slot":"left","zone":"before-apps","order":0,"variant":"pill","size":"normal","styleMode":"pill","wheelAction":"default","enabled":true}
            ]
        }"#;
        std::fs::write(tmp.join("widgets_layout.json"), v1_json).unwrap();
        let store = WidgetsLayoutStore::new(Some(tmp.clone()));
        let mut doc = store.load();
        assert_eq!(doc.dock_widgets_schema_ver, 2);
        assert!(doc.dock_widgets.iter().any(|w| w.kind == "quick-tools"));

        // Bordered legacy pills cleaned & crowded extras trimmed for 1280px+ displays
        for w in &doc.dock_widgets {
            assert_eq!(w.style_mode, "clean", "{} should be polished to clean", w.kind);
        }
        assert_eq!(doc.dock_widgets.iter().find(|w| w.kind == "clock").unwrap().variant, "editorial-2line");
        let enabled_kinds: Vec<&str> = doc.dock_widgets.iter().filter(|w| w.enabled).map(|w| w.kind.as_str()).collect();
        assert_eq!(enabled_kinds, vec!["media", "weather", "clock"]);

        // Re-enabling quick-tools with custom style after schema v2 is preserved across reloads
        let qt = doc.dock_widgets.iter_mut().find(|w| w.kind == "quick-tools").unwrap();
        qt.enabled = true;
        qt.style_mode = "tinted".into();
        store.save(&doc);
        let reloaded = store.load();
        let qt2 = reloaded.dock_widgets.iter().find(|w| w.kind == "quick-tools").unwrap();
        assert!(qt2.enabled);
        assert_eq!(qt2.style_mode, "tinted");

        let _ = std::fs::remove_dir_all(&tmp);
    }

    #[test]
    fn zero_panic_storage_parsing_handles_corrupt_and_adversarial_inputs() {
        use crate::{AuroraBackupBundle, AuroraDb, AuxiliaryStateStore, ShellPreferencesStore};

        let pid = std::process::id();
        let dir = std::env::temp_dir().join(format!("aurora_zero_panic_storage_{pid}"));
        let _ = std::fs::remove_dir_all(&dir);
        assert!(std::fs::create_dir_all(&dir).is_ok());

        // 1. Corrupt/truncated/type-mismatched JSON in WidgetsLayoutStore
        for bogus in [
            "{not valid json",
            "null",
            "[]",
            r#"{"widgets":"bad","dockWidgets":123,"todos":null}"#,
            "",
        ] {
            let _ = std::fs::write(dir.join("widgets_layout.json"), bogus);
            let doc = WidgetsLayoutStore::new(Some(dir.clone())).load();
            assert_eq!(doc.ai_configs.len(), 3);
            assert!(doc.dock_widgets.len() >= 14);
        }

        // 2. Corrupt JSON in ShellPreferencesStore
        let prefs_path = dir.join("shell_settings.json");
        for bogus in ["{bad", "null", r#"{"dockScalePercent":"nan","dockEdge":999}"#] {
            let _ = std::fs::write(&prefs_path, bogus);
            let prefs = ShellPreferencesStore::new(Some(prefs_path.clone())).load();
            assert_eq!(prefs.effective_dock_edge(), "Bottom");
        }

        // 3. Corrupt JSON in AuxiliaryStateStore & AuroraBackupBundle
        let _ = std::fs::write(dir.join("agenda_notes.json"), "{corrupt");
        let _ = std::fs::write(dir.join("drop_shelf.json"), "12345");
        let _ = std::fs::write(dir.join("search_frecency.json"), "[broken");
        let _ = std::fs::write(dir.join("scratchpad.txt"), [0xFFu8, 0xFE, 0x00]);
        let aux = AuxiliaryStateStore::new(Some(dir.clone())).load();
        assert!(aux.agenda_notes.is_empty());
        assert!(aux.drop_shelf_paths.is_empty());
        assert!(aux.search_frecency.is_empty());
        assert!(AuroraBackupBundle::import_from_file(&dir.join("agenda_notes.json")).is_none());

        // 4. Corrupt SQLite DB triggers automatic recovery without panic
        let corrupt_db = dir.join("corrupt.db");
        let _ = std::fs::write(&corrupt_db, b"NOT A SQLITE DATABASE AT ALL");
        let db = AuroraDb::open(Some(corrupt_db));
        assert!(db.entered_recovery);
        assert!(!db.load_pinned_shortcuts().is_empty());

        let _ = std::fs::remove_dir_all(&dir);
    }

    #[test]
    fn mojibake_turkish_text_heals_once_without_altering_valid_utf8() {
        assert_eq!(heal_turkish_mojibake("Yeni YÄ±l"), "Yeni Yıl");
        assert_eq!(
            heal_turkish_mojibake("Ä°stanbul Ã‡aÄŸrÄ± ÅŸafak Ã¶zÃ¼r gÃ¼nÃ¼"),
            "İstanbul Çağrı şafak özür günü"
        );
        assert_eq!(heal_turkish_mojibake("Yeni Yıl 2027 🎉"), "Yeni Yıl 2027 🎉");
        assert_eq!(heal_turkish_mojibake("Lansman & Kontrol"), "Lansman & Kontrol");

        let pid = std::process::id();
        let tmp = std::env::temp_dir().join(format!("aurora_mojibake_{pid}"));
        let _ = std::fs::remove_dir_all(&tmp);
        assert!(std::fs::create_dir_all(&tmp).is_ok());
        let json = r#"{
            "dockWidgetsSchemaVer": 2,
            "todos": [{"id":"t-1","text":"AkÅŸam koÅŸusu","done":false}],
            "countdown": {"title":"Yeni YÄ±l","targetDate":"2027-01-01"}
        }"#;
        let _ = std::fs::write(tmp.join("widgets_layout.json"), json);
        let doc = WidgetsLayoutStore::new(Some(tmp.clone())).load();
        assert_eq!(doc.countdown.title, "Yeni Yıl");
        assert_eq!(doc.todos[0].text, "Akşam koşusu");
        let _ = std::fs::remove_dir_all(&tmp);
    }
}
