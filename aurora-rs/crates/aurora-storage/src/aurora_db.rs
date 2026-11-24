use std::path::PathBuf;
use aurora_core::{default_workspace_profiles, PinnedShortcut, WorkspaceProfile};
use chrono::Utc;
use parking_lot::Mutex;
use rusqlite::{params, Connection};
use serde::{Deserialize, Serialize};

use crate::aurora_data_dir;

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UserPreferencesRecord {
    #[serde(default = "default_theme_id")]
    pub active_theme_id: String,
    #[serde(default = "default_profile_id")]
    pub active_profile_id: String,
    #[serde(default)]
    pub focus_mode_enabled: bool,
    #[serde(default)]
    pub reduced_motion: bool,
}

fn default_theme_id() -> String {
    "aurora-glass-dark".into()
}
fn default_profile_id() -> String {
    "profile-personal".into()
}

impl Default for UserPreferencesRecord {
    fn default() -> Self {
        Self {
            active_theme_id: default_theme_id(),
            active_profile_id: default_profile_id(),
            focus_mode_enabled: false,
            reduced_motion: false,
        }
    }
}

/// 100% schema-compatible with v2.x `%LOCALAPPDATA%\ProjectAurora\data\aurora.db`!
pub struct AuroraDb {
    db_path: PathBuf,
    gate: Mutex<()>,
    pub entered_recovery: bool,
}

impl AuroraDb {
    pub fn open(custom_path: Option<PathBuf>) -> Self {
        let db_path = custom_path.unwrap_or_else(|| aurora_data_dir().join("aurora.db"));
        if let Some(parent) = db_path.parent() {
            let _ = std::fs::create_dir_all(parent);
        }
        let mut db = Self {
            db_path,
            gate: Mutex::new(()),
            entered_recovery: false,
        };
        if db.ensure_schema().is_err() {
            db.entered_recovery = true;
            if db.db_path.exists() {
                let bak = db.db_path.with_extension(format!(
                    "db.recovery.{}.bak",
                    Utc::now().format("%Y%m%d%H%M%S")
                ));
                let _ = std::fs::rename(&db.db_path, bak);
            }
            let _ = db.ensure_schema();
        }
        db
    }

    fn connect(&self) -> rusqlite::Result<Connection> {
        let conn = Connection::open(&self.db_path)?;
        conn.busy_timeout(std::time::Duration::from_millis(1500))?;
        Ok(conn)
    }

    fn ensure_schema(&self) -> rusqlite::Result<()> {
        let _lock = self.gate.lock();
        let conn = self.connect()?;
        conn.execute_batch(
            "PRAGMA journal_mode=WAL;
             PRAGMA foreign_keys=ON;
             CREATE TABLE IF NOT EXISTS SchemaMigrations (
                 Version INTEGER PRIMARY KEY,
                 AppliedAtUtc TEXT NOT NULL
             );
             CREATE TABLE IF NOT EXISTS KeyValueState (
                 StateKey TEXT PRIMARY KEY,
                 JsonPayload TEXT NOT NULL,
                 UpdatedAtUtc TEXT NOT NULL
             );
             CREATE TABLE IF NOT EXISTS PinnedShortcuts (
                 Id TEXT PRIMARY KEY,
                 OrderIndex INTEGER NOT NULL,
                 JsonPayload TEXT NOT NULL
             );
             CREATE TABLE IF NOT EXISTS WorkspaceProfiles (
                 Id TEXT PRIMARY KEY,
                 OrderIndex INTEGER NOT NULL,
                 JsonPayload TEXT NOT NULL
             );
             CREATE TABLE IF NOT EXISTS NotificationHistory (
                 Id TEXT PRIMARY KEY,
                 CreatedAtUtc TEXT NOT NULL,
                 JsonPayload TEXT NOT NULL
             );
             CREATE TABLE IF NOT EXISTS CustomThemes (
                 Id TEXT PRIMARY KEY,
                 UpdatedAtUtc TEXT NOT NULL,
                 JsonPayload TEXT NOT NULL
             );",
        )?;
        let ver: i64 = conn
            .query_row("SELECT COALESCE(MAX(Version), 0) FROM SchemaMigrations;", [], |r| r.get(0))
            .unwrap_or(0);
        if ver < 1 {
            conn.execute(
                "INSERT OR IGNORE INTO SchemaMigrations (Version, AppliedAtUtc) VALUES (1, ?1);",
                params![Utc::now().to_rfc3339()],
            )?;
        }
        Ok(())
    }

    pub fn load_user_preferences(&self) -> UserPreferencesRecord {
        let _lock = self.gate.lock();
        let Ok(conn) = self.connect() else {
            return UserPreferencesRecord::default();
        };
        let raw: Option<String> = conn
            .query_row(
                "SELECT JsonPayload FROM KeyValueState WHERE StateKey = 'user_preferences';",
                [],
                |r| r.get(0),
            )
            .ok();
        raw.and_then(|s| serde_json::from_str(&s).ok())
            .unwrap_or_default()
    }

    pub fn save_user_preferences(&self, prefs: &UserPreferencesRecord) {
        let _lock = self.gate.lock();
        let Ok(conn) = self.connect() else { return };
        if let Ok(json) = serde_json::to_string_pretty(prefs) {
            let _ = conn.execute(
                "INSERT INTO KeyValueState (StateKey, JsonPayload, UpdatedAtUtc)
                 VALUES ('user_preferences', ?1, ?2)
                 ON CONFLICT(StateKey) DO UPDATE SET JsonPayload = excluded.JsonPayload, UpdatedAtUtc = excluded.UpdatedAtUtc;",
                params![json, Utc::now().to_rfc3339()],
            );
        }
    }

    pub fn load_pinned_shortcuts(&self) -> Vec<PinnedShortcut> {
        let _lock = self.gate.lock();
        let Ok(conn) = self.connect() else {
            return default_pinned_shortcuts();
        };
        let Ok(mut stmt) = conn.prepare("SELECT JsonPayload FROM PinnedShortcuts ORDER BY OrderIndex ASC;") else {
            return default_pinned_shortcuts();
        };
        let rows = stmt
            .query_map([], |r| r.get::<_, String>(0))
            .ok()
            .into_iter()
            .flatten()
            .filter_map(|res| res.ok())
            .filter_map(|json| serde_json::from_str::<PinnedShortcut>(&json).ok())
            .collect::<Vec<_>>();
        if rows.is_empty() {
            default_pinned_shortcuts()
        } else {
            rows
        }
    }

    pub fn save_pinned_shortcuts(&self, shortcuts: &[PinnedShortcut]) {
        let _lock = self.gate.lock();
        let Ok(mut conn) = self.connect() else { return };
        let Ok(tx) = conn.transaction() else { return };
        let _ = tx.execute("DELETE FROM PinnedShortcuts;", []);
        for (idx, item) in shortcuts.iter().enumerate() {
            if let Ok(json) = serde_json::to_string(item) {
                let _ = tx.execute(
                    "INSERT INTO PinnedShortcuts (Id, OrderIndex, JsonPayload) VALUES (?1, ?2, ?3);",
                    params![item.id, idx as i64, json],
                );
            }
        }
        let _ = tx.commit();
    }

    pub fn load_workspace_profiles(&self) -> Vec<WorkspaceProfile> {
        let _lock = self.gate.lock();
        let Ok(conn) = self.connect() else {
            return default_workspace_profiles();
        };
        let Ok(mut stmt) = conn.prepare("SELECT JsonPayload FROM WorkspaceProfiles ORDER BY OrderIndex ASC;") else {
            return default_workspace_profiles();
        };
        let rows = stmt
            .query_map([], |r| r.get::<_, String>(0))
            .ok()
            .into_iter()
            .flatten()
            .filter_map(|res| res.ok())
            .filter_map(|json| serde_json::from_str::<WorkspaceProfile>(&json).ok())
            .collect::<Vec<_>>();
        if rows.is_empty() {
            default_workspace_profiles()
        } else {
            rows
        }
    }
}

pub fn default_pinned_shortcuts() -> Vec<PinnedShortcut> {
    vec![
        PinnedShortcut {
            id: "pin-explorer".into(),
            display_name: "Dosya Gezgini".into(),
            executable_path: "explorer.exe".into(),
            arguments: String::new(),
            icon_key: None,
        },
        PinnedShortcut {
            id: "pin-terminal".into(),
            display_name: "Terminal".into(),
            executable_path: "wt.exe".into(),
            arguments: String::new(),
            icon_key: None,
        },
        PinnedShortcut {
            id: "pin-edge".into(),
            display_name: "Edge".into(),
            executable_path: "msedge.exe".into(),
            arguments: String::new(),
            icon_key: None,
        },
        PinnedShortcut {
            id: "pin-notepad".into(),
            display_name: "Not Defteri".into(),
            executable_path: "notepad.exe".into(),
            arguments: String::new(),
            icon_key: None,
        },
        PinnedShortcut {
            id: "pin-settings".into(),
            display_name: "Ayarlar".into(),
            executable_path: "ms-settings:".into(),
            arguments: String::new(),
            icon_key: None,
        },
    ]
}
