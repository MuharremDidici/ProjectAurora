pub mod aurora_db;
pub mod aux_state;
pub mod clipboard_db;
pub mod logger;
pub mod prefs;
pub mod updater;
pub mod widgets_store;

pub use aurora_db::*;
pub use aux_state::*;
pub use clipboard_db::*;
pub use logger::*;
pub use prefs::*;
pub use updater::*;
pub use widgets_store::*;

use std::path::PathBuf;

/// Resolves `%LOCALAPPDATA%\ProjectAurora` (100% shared with v2.x for seamless zero-loss upgrades!).
pub fn aurora_root_dir() -> PathBuf {
    let local = std::env::var_os("LOCALAPPDATA")
        .map(PathBuf::from)
        .unwrap_or_else(|| PathBuf::from("."));
    let root = local.join("ProjectAurora");
    let _ = std::fs::create_dir_all(root.join("data"));
    let _ = std::fs::create_dir_all(root.join("logs"));
    let _ = std::fs::create_dir_all(root.join("cache"));
    root
}

pub fn aurora_data_dir() -> PathBuf {
    aurora_root_dir().join("data")
}

pub fn aurora_logs_dir() -> PathBuf {
    aurora_root_dir().join("logs")
}

pub fn atomic_write_bytes(path: &std::path::Path, bytes: &[u8]) -> std::io::Result<()> {
    if let Some(parent) = path.parent().filter(|p| !p.as_os_str().is_empty()) {
        std::fs::create_dir_all(parent)?;
    }
    let tmp = path.with_extension(format!(
        "{}.tmp",
        path.extension().and_then(|e| e.to_str()).unwrap_or("dat")
    ));
    std::fs::write(&tmp, bytes)?;
    std::fs::rename(&tmp, path)?;
    Ok(())
}
