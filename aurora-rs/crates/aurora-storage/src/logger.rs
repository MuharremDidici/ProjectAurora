use std::fs::OpenOptions;
use std::io::Write;
use std::path::PathBuf;
use chrono::Utc;
use parking_lot::Mutex;
use serde::Serialize;

use crate::aurora_logs_dir;

#[derive(Serialize)]
#[serde(rename_all = "PascalCase")]
struct LogEntry<'a> {
    timestamp_utc: String,
    level: &'a str,
    category: &'a str,
    event_code: &'a str,
    sanitized_message: &'a str,
}

pub struct PrivacySafeLogger {
    logs_dir: PathBuf,
    gate: Mutex<()>,
}

impl PrivacySafeLogger {
    pub fn new(custom_dir: Option<PathBuf>) -> Self {
        let logs_dir = custom_dir.unwrap_or_else(aurora_logs_dir);
        let _ = std::fs::create_dir_all(&logs_dir);
        Self {
            logs_dir,
            gate: Mutex::new(()),
        }
    }

    pub fn logs_dir(&self) -> &PathBuf {
        &self.logs_dir
    }

    pub fn info(&self, cat: &str, code: &str, msg: &str) {
        self.write("INFO", cat, code, msg);
    }

    pub fn warn(&self, cat: &str, code: &str, msg: &str) {
        self.write("WARN", cat, code, msg);
    }

    pub fn error(&self, cat: &str, code: &str, msg: &str) {
        self.write("ERROR", cat, code, msg);
    }

    fn write(&self, level: &str, category: &str, event_code: &str, message: &str) {
        let now = Utc::now();
        let file_name = format!("aurora-{}.jsonl", now.format("%Y%m%d"));
        let entry = LogEntry {
            timestamp_utc: now.to_rfc3339(),
            level,
            category,
            event_code,
            sanitized_message: message,
        };
        if let Ok(mut line) = serde_json::to_string(&entry) {
            line.push('\n');
            let _lock = self.gate.lock();
            if let Ok(mut f) = OpenOptions::new()
                .create(true)
                .append(true)
                .open(self.logs_dir.join(file_name))
            {
                let _ = f.write_all(line.as_bytes());
            }
        }
    }
}
