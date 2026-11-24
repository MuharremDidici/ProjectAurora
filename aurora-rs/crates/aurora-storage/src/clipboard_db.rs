use std::path::PathBuf;
use aurora_core::ClipboardVaultEntry;
use chrono::Utc;
use parking_lot::Mutex;
use rusqlite::{params, Connection};
use sha2::{Digest, Sha256};

use crate::aurora_data_dir;

const MAX_UNPINNED_ENTRIES: i64 = 200;

/// 100% schema-compatible with v2.x `%LOCALAPPDATA%\ProjectAurora\data\clipboard_vault.db`!
pub struct ClipboardVaultDb {
    db_path: PathBuf,
    images_dir: PathBuf,
    gate: Mutex<()>,
}

impl ClipboardVaultDb {
    pub fn open(custom_data_dir: Option<PathBuf>) -> Self {
        let base = custom_data_dir.unwrap_or_else(aurora_data_dir);
        let images_dir = base.join("clip_images");
        let _ = std::fs::create_dir_all(&images_dir);
        let db = Self {
            db_path: base.join("clipboard_vault.db"),
            images_dir,
            gate: Mutex::new(()),
        };
        let _ = db.ensure_schema();
        db
    }

    fn connect(&self) -> rusqlite::Result<Connection> {
        let conn = Connection::open(&self.db_path)?;
        conn.busy_timeout(std::time::Duration::from_millis(1200))?;
        Ok(conn)
    }

    fn ensure_schema(&self) -> rusqlite::Result<()> {
        let _lock = self.gate.lock();
        let conn = self.connect()?;
        conn.execute_batch(
            "PRAGMA journal_mode = WAL;
             CREATE TABLE IF NOT EXISTS ClipboardItems (
                 Id INTEGER PRIMARY KEY AUTOINCREMENT,
                 ContentHash TEXT NOT NULL UNIQUE,
                 Kind TEXT NOT NULL,
                 TextContent TEXT NOT NULL DEFAULT '',
                 PreviewText TEXT NOT NULL DEFAULT '',
                 ImagePath TEXT,
                 ThumbBase64 TEXT,
                 ImageWidth INTEGER NOT NULL DEFAULT 0,
                 ImageHeight INTEGER NOT NULL DEFAULT 0,
                 SourceApp TEXT NOT NULL DEFAULT '',
                 IsPinned INTEGER NOT NULL DEFAULT 0,
                 LastCopiedUtc TEXT NOT NULL
             );
             CREATE INDEX IF NOT EXISTS IX_ClipboardItems_Order
                 ON ClipboardItems (IsPinned DESC, LastCopiedUtc DESC);",
        )
    }

    pub fn record_text(&self, raw: &str, source_app: Option<&str>) -> Option<ClipboardVaultEntry> {
        let trimmed = raw.trim();
        if trimmed.is_empty() {
            return None;
        }
        let normalized: String = raw.chars().take(64_000).collect();
        let preview = build_preview(trimmed);
        let hash = sha256_hex(format!("T:{normalized}").as_bytes());
        let now = Utc::now().to_rfc3339();
        let app = source_app
            .map(str::trim)
            .filter(|s| !s.is_empty())
            .unwrap_or("Pano");

        let _lock = self.gate.lock();
        let conn = self.connect().ok()?;
        conn.execute(
            "INSERT INTO ClipboardItems (ContentHash, Kind, TextContent, PreviewText, SourceApp, IsPinned, LastCopiedUtc)
             VALUES (?1, 'text', ?2, ?3, ?4, 0, ?5)
             ON CONFLICT(ContentHash) DO UPDATE SET
                 LastCopiedUtc = excluded.LastCopiedUtc,
                 SourceApp = CASE WHEN excluded.SourceApp != '' THEN excluded.SourceApp ELSE ClipboardItems.SourceApp END;",
            params![hash, normalized, preview, app, now],
        )
        .ok()?;
        self.prune_overflow_locked(&conn);
        Self::get_by_hash_locked(&conn, &hash)
    }

    pub fn record_image(
        &self,
        png_bytes: &[u8],
        thumb_b64: Option<&str>,
        width: i32,
        height: i32,
        source_app: Option<&str>,
    ) -> Option<ClipboardVaultEntry> {
        if png_bytes.is_empty() {
            return None;
        }
        let hash = sha256_hex(png_bytes);
        let img_name = format!("{}.png", &hash[..24]);
        let img_path = self.images_dir.join(img_name);
        if !img_path.exists() {
            std::fs::write(&img_path, png_bytes).ok()?;
        }
        let preview = format!("Görsel ({width}×{height})");
        let now = Utc::now().to_rfc3339();
        let app = source_app
            .map(str::trim)
            .filter(|s| !s.is_empty())
            .unwrap_or("Ekran");

        let _lock = self.gate.lock();
        let conn = self.connect().ok()?;
        conn.execute(
            "INSERT INTO ClipboardItems (ContentHash, Kind, TextContent, PreviewText, ImagePath, ThumbBase64, ImageWidth, ImageHeight, SourceApp, IsPinned, LastCopiedUtc)
             VALUES (?1, 'image', '', ?2, ?3, ?4, ?5, ?6, ?7, 0, ?8)
             ON CONFLICT(ContentHash) DO UPDATE SET
                 LastCopiedUtc = excluded.LastCopiedUtc,
                 ImagePath = excluded.ImagePath,
                 ThumbBase64 = COALESCE(excluded.ThumbBase64, ClipboardItems.ThumbBase64);",
            params![hash, preview, img_path.to_string_lossy().to_string(), thumb_b64, width, height, app, now],
        )
        .ok()?;
        self.prune_overflow_locked(&conn);
        Self::get_by_hash_locked(&conn, &hash)
    }

    pub fn list(&self, filter: &str, query: &str, limit: usize) -> Vec<ClipboardVaultEntry> {
        let _lock = self.gate.lock();
        let Ok(conn) = self.connect() else { return vec![] };
        let q_like = format!("%{}%", query.trim());
        let sql = match filter {
            "pinned" => "SELECT Id, Kind, TextContent, PreviewText, ImagePath, ThumbBase64, ImageWidth, ImageHeight, SourceApp, IsPinned, LastCopiedUtc
                         FROM ClipboardItems WHERE IsPinned = 1 AND (TextContent LIKE ?1 OR PreviewText LIKE ?1 OR SourceApp LIKE ?1)
                         ORDER BY LastCopiedUtc DESC LIMIT ?2;",
            "image" => "SELECT Id, Kind, TextContent, PreviewText, ImagePath, ThumbBase64, ImageWidth, ImageHeight, SourceApp, IsPinned, LastCopiedUtc
                        FROM ClipboardItems WHERE Kind = 'image' AND (PreviewText LIKE ?1 OR SourceApp LIKE ?1)
                        ORDER BY IsPinned DESC, LastCopiedUtc DESC LIMIT ?2;",
            "text" => "SELECT Id, Kind, TextContent, PreviewText, ImagePath, ThumbBase64, ImageWidth, ImageHeight, SourceApp, IsPinned, LastCopiedUtc
                       FROM ClipboardItems WHERE Kind = 'text' AND (TextContent LIKE ?1 OR PreviewText LIKE ?1 OR SourceApp LIKE ?1)
                       ORDER BY IsPinned DESC, LastCopiedUtc DESC LIMIT ?2;",
            _ => "SELECT Id, Kind, TextContent, PreviewText, ImagePath, ThumbBase64, ImageWidth, ImageHeight, SourceApp, IsPinned, LastCopiedUtc
                  FROM ClipboardItems WHERE (TextContent LIKE ?1 OR PreviewText LIKE ?1 OR SourceApp LIKE ?1)
                  ORDER BY IsPinned DESC, LastCopiedUtc DESC LIMIT ?2;",
        };
        let Ok(mut stmt) = conn.prepare(sql) else { return vec![] };
        stmt.query_map(params![q_like, limit as i64], Self::map_row)
            .ok()
            .into_iter()
            .flatten()
            .filter_map(|r| r.ok())
            .collect()
    }

    pub fn toggle_pin(&self, id: i64) -> bool {
        let _lock = self.gate.lock();
        let Ok(conn) = self.connect() else { return false };
        conn.execute(
            "UPDATE ClipboardItems SET IsPinned = CASE WHEN IsPinned = 1 THEN 0 ELSE 1 END WHERE Id = ?1;",
            params![id],
        )
        .is_ok()
    }

    pub fn delete(&self, id: i64) -> bool {
        let _lock = self.gate.lock();
        let Ok(conn) = self.connect() else { return false };
        let img: Option<String> = conn
            .query_row("SELECT ImagePath FROM ClipboardItems WHERE Id = ?1;", params![id], |r| r.get(0))
            .ok()
            .flatten();
        let ok = conn.execute("DELETE FROM ClipboardItems WHERE Id = ?1;", params![id]).is_ok();
        if let Some(p) = img {
            let _ = std::fs::remove_file(p);
        }
        ok
    }

    pub fn clear_unpinned(&self) -> usize {
        let _lock = self.gate.lock();
        let Ok(conn) = self.connect() else { return 0 };
        let paths: Vec<String> = conn
            .prepare("SELECT ImagePath FROM ClipboardItems WHERE IsPinned = 0 AND ImagePath IS NOT NULL;")
            .ok()
            .and_then(|mut s| {
                s.query_map([], |r| r.get::<_, String>(0))
                    .ok()
                    .map(|rows| rows.filter_map(|r| r.ok()).collect())
            })
            .unwrap_or_default();
        let count = conn.execute("DELETE FROM ClipboardItems WHERE IsPinned = 0;", []).unwrap_or(0);
        for p in paths {
            let _ = std::fs::remove_file(p);
        }
        count
    }

    pub fn total_count(&self) -> usize {
        let _lock = self.gate.lock();
        let Ok(conn) = self.connect() else { return 0 };
        conn.query_row("SELECT COUNT(*) FROM ClipboardItems;", [], |r| r.get::<_, i64>(0))
            .unwrap_or(0) as usize
    }

    fn prune_overflow_locked(&self, conn: &Connection) {
        let stale: Vec<(i64, Option<String>)> = conn
            .prepare(
                "SELECT Id, ImagePath FROM ClipboardItems WHERE IsPinned = 0
                 ORDER BY LastCopiedUtc DESC LIMIT -1 OFFSET ?1;",
            )
            .ok()
            .and_then(|mut s| {
                s.query_map(params![MAX_UNPINNED_ENTRIES], |r| Ok((r.get(0)?, r.get(1)?)))
                    .ok()
                    .map(|rows| rows.filter_map(|r| r.ok()).collect())
            })
            .unwrap_or_default();
        for (id, img) in stale {
            let _ = conn.execute("DELETE FROM ClipboardItems WHERE Id = ?1;", params![id]);
            if let Some(p) = img {
                let _ = std::fs::remove_file(p);
            }
        }
    }

    fn get_by_hash_locked(conn: &Connection, hash: &str) -> Option<ClipboardVaultEntry> {
        conn.query_row(
            "SELECT Id, Kind, TextContent, PreviewText, ImagePath, ThumbBase64, ImageWidth, ImageHeight, SourceApp, IsPinned, LastCopiedUtc
             FROM ClipboardItems WHERE ContentHash = ?1;",
            params![hash],
            Self::map_row,
        )
        .ok()
    }

    fn map_row(r: &rusqlite::Row<'_>) -> rusqlite::Result<ClipboardVaultEntry> {
        Ok(ClipboardVaultEntry {
            id: r.get(0)?,
            kind: r.get(1)?,
            text_content: r.get(2)?,
            preview_text: r.get(3)?,
            image_path: r.get(4)?,
            thumb_base64: r.get(5)?,
            image_width: r.get(6)?,
            image_height: r.get(7)?,
            source_app: r.get(8)?,
            is_pinned: r.get::<_, i64>(9)? != 0,
            last_copied_utc: r.get(10)?,
        })
    }
}

fn build_preview(trimmed: &str) -> String {
    let single: String = trimmed
        .split_whitespace()
        .collect::<Vec<_>>()
        .join(" ");
    let mut out: String = single.chars().take(110).collect();
    if single.chars().count() > 110 {
        out.push('…');
    }
    out
}

fn sha256_hex(data: &[u8]) -> String {
    let mut hasher = Sha256::new();
    hasher.update(data);
    hex::encode_upper(hasher.finalize())
}
