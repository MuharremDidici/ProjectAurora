use std::io::Read;
use std::path::{Path, PathBuf};
use aurora_core::{
    ReleaseManifest, AURORA_VERSION, DEFAULT_GITHUB_API_LATEST_URL, DEFAULT_GITHUB_MANIFEST_URL,
};
use sha2::{Digest, Sha256};

#[derive(Debug, Clone)]
pub struct UpdateCheckOutcome {
    pub success: bool,
    pub update_available: bool,
    pub current_version: String,
    pub latest_version: String,
    pub message: String,
    pub manifest: Option<ReleaseManifest>,
}

pub fn compare_semver(a: &str, b: &str) -> std::cmp::Ordering {
    let parse = |s: &str| -> (u32, u32, u32) {
        let clean = s.trim().trim_start_matches(['v', 'V']);
        let main = clean.split(['-', '+']).next().unwrap_or("0.0.0");
        let mut it = main.split('.').map(|p| p.parse::<u32>().unwrap_or(0));
        (it.next().unwrap_or(0), it.next().unwrap_or(0), it.next().unwrap_or(0))
    };
    parse(a).cmp(&parse(b))
}

pub fn parse_manifest_or_github_api(body: &str) -> Option<ReleaseManifest> {
    if let Ok(man) = serde_json::from_str::<ReleaseManifest>(body) {
        if !man.version.trim().is_empty() && !man.download_url.trim().is_empty() {
            return Some(man);
        }
    }

    let v: serde_json::Value = serde_json::from_str(body).ok()?;
    let tag_raw = v.get("tag_name").and_then(|x| x.as_str())?;
    let version = tag_raw.trim().trim_start_matches(['v', 'V']).to_string();
    if version.is_empty() {
        return None;
    }

    let assets = v.get("assets").and_then(|x| x.as_array())?;
    let mut preferred: Option<String> = None;
    let mut fallback: Option<String> = None;
    for asset in assets {
        let name = asset
            .get("name")
            .and_then(|x| x.as_str())
            .unwrap_or_default()
            .to_ascii_lowercase();
        let url = asset
            .get("browser_download_url")
            .and_then(|x| x.as_str())
            .unwrap_or_default();
        if url.is_empty() || !name.ends_with(".exe") {
            continue;
        }
        if name.contains("aurora-kurulum") {
            preferred = Some(url.to_string());
            break;
        }
        if fallback.is_none() {
            fallback = Some(url.to_string());
        }
    }
    let download_url = preferred.or(fallback)?;
    let published_at_utc = v
        .get("published_at")
        .and_then(|x| x.as_str())
        .unwrap_or_default()
        .to_string();
    let release_notes = v
        .get("body")
        .and_then(|x| x.as_str())
        .unwrap_or_default()
        .trim()
        .to_string();

    Some(ReleaseManifest {
        version,
        published_at_utc,
        download_url,
        sha256: String::new(),
        release_notes,
    })
}

pub fn check_for_updates(custom_manifest_url: &str) -> UpdateCheckOutcome {
    let url = if custom_manifest_url.trim().is_empty() {
        DEFAULT_GITHUB_MANIFEST_URL
    } else {
        custom_manifest_url.trim()
    };

    let fetch_manifest = |target_url: &str, accept_github: bool| -> Option<ReleaseManifest> {
        let mut req = ureq::get(target_url)
            .set("User-Agent", &format!("ProjectAurora/{AURORA_VERSION}"))
            .timeout(std::time::Duration::from_secs(12));
        if accept_github {
            req = req.set("Accept", "application/vnd.github+json");
        }
        let resp = req.call().ok()?;
        let body = resp.into_string().ok()?;
        parse_manifest_or_github_api(&body)
    };

    let mut manifest = fetch_manifest(url, false);
    if manifest.is_none() && url.eq_ignore_ascii_case(DEFAULT_GITHUB_MANIFEST_URL) {
        manifest = fetch_manifest(DEFAULT_GITHUB_API_LATEST_URL, true);
    }

    let Some(manifest) = manifest else {
        return UpdateCheckOutcome {
            success: false,
            update_available: false,
            current_version: AURORA_VERSION.into(),
            latest_version: AURORA_VERSION.into(),
            message: "Güncelleme sunucusuna ulaşılamadı veya sürüm manifesti okunamadı.".into(),
            manifest: None,
        };
    };

    let newer = compare_semver(&manifest.version, AURORA_VERSION).is_gt();
    UpdateCheckOutcome {
        success: true,
        update_available: newer,
        current_version: AURORA_VERSION.into(),
        latest_version: manifest.version.clone(),
        message: if newer {
            format!("Yeni sürüm hazır: v{}", manifest.version)
        } else {
            format!("Aurora v{AURORA_VERSION} en güncel sürüm.")
        },
        manifest: if newer { Some(manifest) } else { None },
    }
}

pub fn download_and_verify_installer<F>(
    manifest: &ReleaseManifest,
    dest_dir: &Path,
    mut on_progress: F,
) -> anyhow::Result<PathBuf>
where
    F: FnMut(u8),
{
    if !manifest.download_url.starts_with("https://") {
        anyhow::bail!("Yalnızca HTTPS güncelleme kaynakları kabul edilir.");
    }
    std::fs::create_dir_all(dest_dir)?;
    let out_path = dest_dir.join(format!("Aurora-Kurulum-v{}-x64.exe", manifest.version));

    let resp = ureq::get(&manifest.download_url)
        .set("User-Agent", &format!("ProjectAurora/{AURORA_VERSION}"))
        .timeout(std::time::Duration::from_secs(90))
        .call()?;

    let total_len: u64 = resp
        .header("Content-Length")
        .and_then(|v| v.parse().ok())
        .unwrap_or(0);

    let mut reader = resp.into_reader();
    let mut file = std::fs::File::create(&out_path)?;
    let mut hasher = Sha256::new();
    let mut buf = [0u8; 32 * 1024];
    let mut downloaded = 0u64;

    loop {
        let n = reader.read(&mut buf)?;
        if n == 0 {
            break;
        }
        std::io::Write::write_all(&mut file, &buf[..n])?;
        hasher.update(&buf[..n]);
        downloaded += n as u64;
        if let Some(pct) = downloaded.saturating_mul(100).checked_div(total_len) {
            on_progress(pct.min(99) as u8);
        }
    }

    let actual_hex = hex::encode_upper(hasher.finalize());
    let expected = manifest.sha256.trim().to_ascii_uppercase();
    if !expected.is_empty() && actual_hex != expected {
        let _ = std::fs::remove_file(&out_path);
        anyhow::bail!("SHA-256 doğrulama hatası! İndirilen dosya reddedildi.");
    }
    on_progress(100);
    Ok(out_path)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{AuroraDb, ClipboardVaultDb, ShellPreferencesStore};

    #[test]
    fn semver_comparison_handles_v_prefix() {
        assert!(compare_semver("v3.0.0", "2.5.1").is_gt());
        assert!(compare_semver("7.2.0", "3.0.0").is_gt());
        assert!(compare_semver("7.2.0", "2.5.1").is_gt());
        assert!(compare_semver("3.0.0", "3.0.0").is_eq());
        assert!(compare_semver("2.4.1", "3.0.0").is_lt());
    }

    #[test]
    fn parses_manifest_and_github_release_api_fallback() {
        let direct_json = r#"{
            "version": "7.2.0",
            "publishedAtUtc": "2026-11-26T09:00:00Z",
            "downloadUrl": "https://github.com/MuharremDidici/ProjectAurora/releases/download/v7.2.0/Aurora-Kurulum-v7.2.0-x64.exe",
            "sha256": "ABCDEF",
            "releaseNotes": "v7.2.0 notes"
        }"#;
        let m1 = parse_manifest_or_github_api(direct_json).expect("direct manifest");
        assert_eq!(m1.version, "7.2.0");
        assert_eq!(m1.sha256, "ABCDEF");
        assert!(m1.download_url.ends_with("Aurora-Kurulum-v7.2.0-x64.exe"));

        let github_api_json = r#"{
            "tag_name": "v7.2.0",
            "published_at": "2026-11-26T09:05:00Z",
            "body": "Aurora v7.2.0 release",
            "assets": [
                {
                    "name": "Project-Aurora-TekTik-Kurulum-v7.2.0.zip",
                    "browser_download_url": "https://github.com/MuharremDidici/ProjectAurora/releases/download/v7.2.0/Project-Aurora-TekTik-Kurulum-v7.2.0.zip"
                },
                {
                    "name": "Aurora-Kurulum-v7.2.0-x64.exe",
                    "browser_download_url": "https://github.com/MuharremDidici/ProjectAurora/releases/download/v7.2.0/Aurora-Kurulum-v7.2.0-x64.exe"
                }
            ]
        }"#;
        let m2 = parse_manifest_or_github_api(github_api_json).expect("github api fallback");
        assert_eq!(m2.version, "7.2.0");
        assert_eq!(m2.published_at_utc, "2026-11-26T09:05:00Z");
        assert_eq!(m2.release_notes, "Aurora v7.2.0 release");
        assert!(m2.download_url.ends_with("Aurora-Kurulum-v7.2.0-x64.exe"));
    }

    #[test]
    fn v2_compatible_sqlite_and_prefs_roundtrip() {
        let dir = std::env::temp_dir().join(format!("aurora_rs_test_{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&dir);
        std::fs::create_dir_all(&dir).unwrap();

        let prefs_store = ShellPreferencesStore::new(Some(dir.join("shell_settings.json")));
        let mut p = prefs_store.load();
        p.dock_density = "Compact".into();
        p.show_clock_seconds = true;
        prefs_store.save(&p);
        let p2 = prefs_store.load();
        assert_eq!(p2.effective_dock_density(), "Compact");
        assert!(p2.show_clock_seconds);
        assert!(p2.effective_reserve_dock_work_area());

        let db = AuroraDb::open(Some(dir.join("aurora.db")));
        assert!(!db.entered_recovery);
        let shortcuts = db.load_pinned_shortcuts();
        assert!(shortcuts.len() >= 5);

        let vault = ClipboardVaultDb::open(Some(dir.clone()));
        let e1 = vault.record_text("https://aurora.local", Some("Opera")).unwrap();
        let _e2 = vault.record_text("https://aurora.local", Some("Opera")).unwrap();
        assert_eq!(vault.total_count(), 1);
        assert!(vault.toggle_pin(e1.id));
        let _e3 = vault.record_text("İkinci pano kaydı", Some("Antigravity")).unwrap();
        assert_eq!(vault.clear_unpinned(), 1);
        assert_eq!(vault.total_count(), 1);

        let _ = std::fs::remove_dir_all(&dir);
    }

    #[test]
    fn zero_panic_manifest_and_semver_parsing_handles_malformed_inputs() {
        for bad in ["", "   ", "{bad", "null", "[]", r#"{"tag_name":"","assets":"oops"}"#] {
            assert!(parse_manifest_or_github_api(bad).is_none());
        }
        assert_eq!(compare_semver("v7.3.0-beta.1", "7.3.0"), std::cmp::Ordering::Equal);
        assert_eq!(compare_semver("invalid", "0.0.0"), std::cmp::Ordering::Equal);
        assert!(compare_semver("99999999999999999999.0.0", "1.0.0").is_lt());
    }
}
