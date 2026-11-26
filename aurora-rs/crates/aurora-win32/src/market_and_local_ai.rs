use std::time::Duration;

use aurora_core::{DiskUsageEntry, LocalAiStatusSnapshot, MarketSnapshot};
use serde_json::Value;
use windows::core::HSTRING;
use windows::Win32::Storage::FileSystem::{GetDiskFreeSpaceExW, GetDriveTypeW, GetLogicalDrives};

const DRIVE_FIXED_TYPE: u32 = 3;

/// Opt-in public FX & Crypto market snapshot (`USD/TRY`, `EUR/TRY`, `BTC/USD`).
/// Only invoked when the user has enabled a `market` Dock or Desktop widget.
pub fn fetch_market_snapshot() -> Option<MarketSnapshot> {
    let agent = ureq::AgentBuilder::new()
        .timeout_connect(Duration::from_secs(3))
        .timeout_read(Duration::from_secs(4))
        .user_agent("ProjectAurora/4.1")
        .build();

    let mut snap = MarketSnapshot {
        updated_at_hhmm: chrono::Local::now().format("%H:%M").to_string(),
        ..Default::default()
    };

    // 1. USD/TRY & EUR/TRY from open.er-api.com (keyless public endpoint)
    if let Ok(resp) = agent.get("https://open.er-api.com/v6/latest/USD").call() {
        if let Ok(v) = resp.into_json::<Value>() {
            if let Some(rates) = v.get("rates").and_then(|r| r.as_object()) {
                let try_per_usd = rates.get("TRY").and_then(|x| x.as_f64()).unwrap_or(0.0);
                let eur_per_usd = rates.get("EUR").and_then(|x| x.as_f64()).unwrap_or(0.0);
                if try_per_usd > 1.0 {
                    snap.usd_try = (try_per_usd * 100.0).round() / 100.0;
                    if eur_per_usd > 0.1 {
                        snap.eur_try = ((try_per_usd / eur_per_usd) * 100.0).round() / 100.0;
                    }
                    snap.valid = true;
                }
            }
        }
    }

    // 2. BTC/USD + 24h change from CoinGecko simple price (keyless public endpoint)
    if let Ok(resp) = agent
        .get("https://api.coingecko.com/api/v3/simple/price?ids=bitcoin&vs_currencies=usd&include_24hr_change=true")
        .call()
    {
        if let Ok(v) = resp.into_json::<Value>() {
            if let Some(btc) = v.get("bitcoin") {
                if let Some(usd) = btc.get("usd").and_then(|x| x.as_f64()) {
                    snap.btc_usd = usd.round();
                    snap.btc_change_24h = btc
                        .get("usd_24h_change")
                        .and_then(|x| x.as_f64())
                        .map(|c| (c * 10.0).round() / 10.0)
                        .unwrap_or(0.0);
                    snap.valid = true;
                }
            }
        }
    }

    if snap.valid {
        Some(snap)
    } else {
        None
    }
}

/// Fast local-only probe (400ms timeout) for running Ollama (`:11434`) & LM Studio (`:1234`) instances.
/// Only invoked when the user has added the `local-ai` widget!
pub fn query_local_ai_status() -> LocalAiStatusSnapshot {
    let agent = ureq::AgentBuilder::new()
        .timeout_connect(Duration::from_millis(350))
        .timeout_read(Duration::from_millis(450))
        .build();

    let mut out = LocalAiStatusSnapshot {
        checked_at_hhmm: chrono::Local::now().format("%H:%M").to_string(),
        ..Default::default()
    };

    if let Ok(resp) = agent.get("http://127.0.0.1:11434/api/ps").call() {
        out.ollama_running = true;
        if let Ok(v) = resp.into_json::<Value>() {
            if let Some(arr) = v.get("models").and_then(|m| m.as_array()) {
                for item in arr.iter().take(6) {
                    if let Some(name) = item.get("name").and_then(|n| n.as_str()) {
                        out.ollama_models.push(name.to_string());
                    }
                }
            }
        }
    }

    if let Ok(resp) = agent.get("http://127.0.0.1:1234/v1/models").call() {
        out.lmstudio_running = true;
        if let Ok(v) = resp.into_json::<Value>() {
            if let Some(arr) = v.get("data").and_then(|m| m.as_array()) {
                for item in arr.iter().take(6) {
                    if let Some(id) = item.get("id").and_then(|n| n.as_str()) {
                        out.lmstudio_models.push(id.to_string());
                    }
                }
            }
        }
    }

    out
}

/// Native Win32 `GetLogicalDrives` + `GetDiskFreeSpaceExW` fixed-drive health enumerator.
pub fn query_disk_usages() -> Vec<DiskUsageEntry> {
    let mut out = Vec::new();
    let mask = unsafe { GetLogicalDrives() };
    for i in 0..26u32 {
        if (mask & (1u32 << i)) == 0 {
            continue;
        }
        let letter = (b'A' + i as u8) as char;
        let root = format!("{letter}:\\");
        let wide = HSTRING::from(&root);
        let dtype = unsafe { GetDriveTypeW(&wide) };
        if dtype != DRIVE_FIXED_TYPE {
            continue;
        }
        let mut free_avail = 0u64;
        let mut total_bytes = 0u64;
        let mut total_free = 0u64;
        let ok = unsafe {
            GetDiskFreeSpaceExW(
                &wide,
                Some(&mut free_avail),
                Some(&mut total_bytes),
                Some(&mut total_free),
            )
            .is_ok()
        };
        if ok && total_bytes > 0 {
            let gb = 1024.0 * 1024.0 * 1024.0;
            let total_gb = ((total_bytes as f64 / gb) * 10.0).round() as f32 / 10.0;
            let free_gb = ((free_avail as f64 / gb) * 10.0).round() as f32 / 10.0;
            let used_pct = (((total_bytes.saturating_sub(free_avail)) as f64 / total_bytes as f64) * 100.0)
                .round()
                .clamp(0.0, 100.0) as u8;
            out.push(DiskUsageEntry {
                drive: format!("{letter}:"),
                free_gb,
                total_gb,
                used_percent: used_pct,
            });
        }
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn enumerates_fixed_system_disks_via_win32() {
        let disks = query_disk_usages();
        assert!(!disks.is_empty(), "At least C: drive should be enumerated");
        assert!(disks[0].total_gb > 1.0);
    }
}
