use std::collections::HashMap;
use std::path::{Path, PathBuf};
use std::time::{Duration, SystemTime, UNIX_EPOCH};

use aurora_core::{AiProviderQuotaConfig, AiProviderQuotaSnapshot};
use chrono::{Local, Utc};
use serde_json::Value;
use windows::Win32::Foundation::{LocalFree, HLOCAL};
use windows::Win32::Security::Cryptography::{
    CryptProtectData, CryptUnprotectData, CRYPTPROTECT_UI_FORBIDDEN, CRYPT_INTEGER_BLOB,
};

/// Protects sensitive bytes using Windows DPAPI (`CryptProtectData`) bound to the current user profile.
pub fn dpapi_protect(plain: &[u8]) -> Option<Vec<u8>> {
    if plain.is_empty() {
        return Some(Vec::new());
    }
    unsafe {
        let in_blob = CRYPT_INTEGER_BLOB {
            cbData: plain.len() as u32,
            pbData: plain.as_ptr() as *mut u8,
        };
        let mut out_blob = CRYPT_INTEGER_BLOB::default();
        if CryptProtectData(
            &in_blob,
            None,
            None,
            None,
            None,
            CRYPTPROTECT_UI_FORBIDDEN,
            &mut out_blob,
        )
        .is_err()
        {
            return None;
        }
        let slice = std::slice::from_raw_parts(out_blob.pbData, out_blob.cbData as usize);
        let res = slice.to_vec();
        let _ = LocalFree(HLOCAL(out_blob.pbData as *mut _));
        Some(res)
    }
}

/// Decrypts DPAPI-protected bytes back into memory for the current Windows user.
pub fn dpapi_unprotect(cipher: &[u8]) -> Option<Vec<u8>> {
    if cipher.is_empty() {
        return Some(Vec::new());
    }
    unsafe {
        let in_blob = CRYPT_INTEGER_BLOB {
            cbData: cipher.len() as u32,
            pbData: cipher.as_ptr() as *mut u8,
        };
        let mut out_blob = CRYPT_INTEGER_BLOB::default();
        if CryptUnprotectData(
            &in_blob,
            None,
            None,
            None,
            None,
            CRYPTPROTECT_UI_FORBIDDEN,
            &mut out_blob,
        )
        .is_err()
        {
            return None;
        }
        let slice = std::slice::from_raw_parts(out_blob.pbData, out_blob.cbData as usize);
        let res = slice.to_vec();
        let _ = LocalFree(HLOCAL(out_blob.pbData as *mut _));
        Some(res)
    }
}

fn default_vault_path() -> PathBuf {
    let local = std::env::var_os("LOCALAPPDATA")
        .map(PathBuf::from)
        .unwrap_or_else(|| PathBuf::from("."));
    local.join("ProjectAurora").join("data").join("ai_vault.dpapi")
}

/// Saves or removes an optional API key for `"openai" | "gemini" | "claude"` inside DPAPI-encrypted storage.
pub fn save_ai_api_key(provider: &str, api_key: &str, custom_path: Option<&Path>) -> bool {
    let path = custom_path
        .map(Path::to_path_buf)
        .unwrap_or_else(default_vault_path);
    let mut map = load_all_ai_api_keys(Some(&path));
    let key_clean = api_key.trim();
    if key_clean.is_empty() {
        map.remove(provider);
    } else {
        map.insert(provider.to_string(), key_clean.to_string());
    }
    let Ok(json_bytes) = serde_json::to_vec(&map) else { return false };
    let Some(cipher) = dpapi_protect(&json_bytes) else { return false };
    if let Some(parent) = path.parent() {
        let _ = std::fs::create_dir_all(parent);
    }
    std::fs::write(&path, cipher).is_ok()
}

/// Loads all DPAPI-protected AI API keys into memory (never logged, never written plaintext).
pub fn load_all_ai_api_keys(custom_path: Option<&Path>) -> HashMap<String, String> {
    let path = custom_path
        .map(Path::to_path_buf)
        .unwrap_or_else(default_vault_path);
    let Ok(cipher) = std::fs::read(&path) else {
        return HashMap::new();
    };
    let Some(plain) = dpapi_unprotect(&cipher) else {
        return HashMap::new();
    };
    serde_json::from_slice(&plain).unwrap_or_default()
}

#[derive(Debug, Default, Clone, Copy)]
pub struct LocalCliUsageStats {
    pub messages_in_window: u64,
    pub tokens_in_window: u64,
    pub tokens_today: u64,
}

/// Parses a local JSONL transcript file (Claude Code `~/.claude/projects/**/*.jsonl` or Codex CLI)
/// counting real assistant responses & input/output tokens within the rolling window and today.
pub fn parse_cli_jsonl_usage(content: &str, window_secs: i64, now_epoch: i64) -> LocalCliUsageStats {
    let cutoff_win = now_epoch.saturating_sub(window_secs.max(60));
    let cutoff_day = now_epoch.saturating_sub(86_400);
    let mut stats = LocalCliUsageStats::default();

    for line in content.lines() {
        let trimmed = line.trim();
        if trimmed.is_empty() || !trimmed.starts_with('{') {
            continue;
        }
        let Ok(v) = serde_json::from_str::<Value>(trimmed) else {
            continue;
        };

        // Extract RFC3339 or epoch timestamp if present
        let ts = v
            .get("timestamp")
            .and_then(|t| {
                t.as_i64().or_else(|| {
                    t.as_str()
                        .and_then(|s| chrono::DateTime::parse_from_rfc3339(s).ok())
                        .map(|dt| dt.timestamp())
                })
            })
            .unwrap_or(now_epoch);

        if ts < cutoff_day {
            continue;
        }

        // Extract token usage from Claude / OpenAI / Gemini JSONL formats
        let usage = v
            .get("message")
            .and_then(|m| m.get("usage"))
            .or_else(|| v.get("usage"))
            .or_else(|| v.get("usageMetadata"));

        let is_assistant = v.get("type").and_then(|x| x.as_str()) == Some("assistant")
            || v.get("role").and_then(|x| x.as_str()) == Some("assistant")
            || v.get("message").and_then(|m| m.get("role")).and_then(|r| r.as_str()) == Some("assistant")
            || usage.is_some();

        let tok = if let Some(u) = usage {
            let inp = u
                .get("input_tokens")
                .or_else(|| u.get("prompt_tokens"))
                .or_else(|| u.get("promptTokenCount"))
                .and_then(|n| n.as_u64())
                .unwrap_or(0);
            let out = u
                .get("output_tokens")
                .or_else(|| u.get("completion_tokens"))
                .or_else(|| u.get("candidatesTokenCount"))
                .and_then(|n| n.as_u64())
                .unwrap_or(0);
            let cache_r = u
                .get("cache_read_input_tokens")
                .and_then(|n| n.as_u64())
                .unwrap_or(0);
            inp + out + cache_r
        } else {
            0
        };

        if tok > 0 {
            stats.tokens_today = stats.tokens_today.saturating_add(tok);
        }
        if ts >= cutoff_win && (is_assistant || tok > 0) {
            stats.messages_in_window = stats.messages_in_window.saturating_add(1);
            stats.tokens_in_window = stats.tokens_in_window.saturating_add(tok);
        }
    }
    stats
}

fn scan_recent_jsonl_dir(dir: &Path, window_secs: i64, now_epoch: i64, depth: u8) -> LocalCliUsageStats {
    let mut total = LocalCliUsageStats::default();
    if depth > 4 || !dir.is_dir() {
        return total;
    }
    let cutoff_sys = SystemTime::now()
        .checked_sub(Duration::from_secs(86_400))
        .unwrap_or(UNIX_EPOCH);

    let Ok(entries) = std::fs::read_dir(dir) else {
        return total;
    };
    let mut files_checked = 0usize;
    for ent in entries.flatten() {
        let p = ent.path();
        if p.is_dir() {
            let sub = scan_recent_jsonl_dir(&p, window_secs, now_epoch, depth + 1);
            total.messages_in_window += sub.messages_in_window;
            total.tokens_in_window += sub.tokens_in_window;
            total.tokens_today += sub.tokens_today;
        } else if p.extension().and_then(|e| e.to_str()) == Some("jsonl") && files_checked < 24 {
            let Ok(meta) = ent.metadata() else { continue };
            let modified = meta.modified().unwrap_or(UNIX_EPOCH);
            if modified < cutoff_sys || meta.len() > 8 * 1024 * 1024 {
                continue;
            }
            files_checked += 1;
            if let Ok(raw) = std::fs::read_to_string(&p) {
                let s = parse_cli_jsonl_usage(&raw, window_secs, now_epoch);
                total.messages_in_window += s.messages_in_window;
                total.tokens_in_window += s.tokens_in_window;
                total.tokens_today += s.tokens_today;
            }
        }
    }
    total
}

fn scan_local_cli_for_provider(provider: &str, window_secs: i64, now_epoch: i64) -> LocalCliUsageStats {
    let Some(home) = std::env::var_os("USERPROFILE").map(PathBuf::from) else {
        return LocalCliUsageStats::default();
    };
    let target = match provider {
        "claude" => home.join(".claude").join("projects"),
        "openai" => home.join(".codex").join("sessions"),
        "gemini" => home.join(".gemini").join("tmp"),
        _ => return LocalCliUsageStats::default(),
    };
    scan_recent_jsonl_dir(&target, window_secs, now_epoch, 0)
}

/// Optional Layer 1: queries official provider API spend / rate-limit headers when a DPAPI API key is stored.
fn query_live_api_usage(provider: &str, api_key: &str, budget_usd: f64) -> Option<(u8, String, String, Option<f64>)> {
    let agent = ureq::AgentBuilder::new()
        .timeout(Duration::from_secs(4))
        .build();

    match provider {
        "openai" => {
            // 1) Try official Organization Costs API (works with Admin keys)
            let start_of_month = Utc::now().format("%Y-%m-01T00:00:00Z").to_string();
            let start_epoch = chrono::DateTime::parse_from_rfc3339(&start_of_month)
                .map(|d| d.timestamp())
                .unwrap_or(0);
            let cost_url = format!(
                "https://api.openai.com/v1/organization/costs?start_time={start_epoch}&bucket_width=1d&limit=31"
            );
            if let Ok(resp) = agent
                .get(&cost_url)
                .set("Authorization", &format!("Bearer {api_key}"))
                .call()
            {
                if let Ok(val) = resp.into_json::<Value>() {
                    let mut usd = 0.0f64;
                    if let Some(arr) = val.get("data").and_then(|d| d.as_array()) {
                        for b in arr {
                            if let Some(res) = b.get("results").and_then(|r| r.as_array()) {
                                for r in res {
                                    usd += r
                                        .get("amount")
                                        .and_then(|a| a.get("value"))
                                        .and_then(|v| v.as_f64())
                                        .unwrap_or(0.0);
                                }
                            }
                        }
                    }
                    let cap = budget_usd.max(1.0);
                    let rem = ((1.0 - (usd / cap)).clamp(0.0, 1.0) * 100.0).round() as u8;
                    return Some((
                        rem,
                        format!("${usd:.2} / ${cap:.0}"),
                        "Aylık OpenAI API Harcaması".into(),
                        Some(usd),
                    ));
                }
            }
            // 2) Fallback: lightweight /v1/models check for key health + rate-limit headers
            if let Ok(resp) = agent
                .get("https://api.openai.com/v1/models")
                .set("Authorization", &format!("Bearer {api_key}"))
                .call()
            {
                let rem_req = resp
                    .header("x-ratelimit-remaining-requests")
                    .and_then(|s| s.parse::<u64>().ok());
                let lim_req = resp
                    .header("x-ratelimit-limit-requests")
                    .and_then(|s| s.parse::<u64>().ok());
                if let (Some(r), Some(l)) = (rem_req, lim_req) {
                    let pct = ((r as f64 / l.max(1) as f64).clamp(0.0, 1.0) * 100.0).round() as u8;
                    return Some((
                        pct,
                        format!("{r} / {l} RPM"),
                        "OpenAI API Bağlı · Canlı Kota".into(),
                        None,
                    ));
                }
                return Some((100, "API Anahtarı Aktif".into(), "OpenAI API doğrulandı".into(), None));
            }
            None
        }
        "claude" => {
            if let Ok(resp) = agent
                .get("https://api.anthropic.com/v1/models")
                .set("x-api-key", api_key)
                .set("anthropic-version", "2023-06-01")
                .call()
            {
                let rem_req = resp
                    .header("anthropic-ratelimit-requests-remaining")
                    .and_then(|s| s.parse::<u64>().ok());
                let lim_req = resp
                    .header("anthropic-ratelimit-requests-limit")
                    .and_then(|s| s.parse::<u64>().ok());
                if let (Some(r), Some(l)) = (rem_req, lim_req) {
                    let pct = ((r as f64 / l.max(1) as f64).clamp(0.0, 1.0) * 100.0).round() as u8;
                    return Some((
                        pct,
                        format!("{r} / {l} istek"),
                        "Anthropic API Bağlı · Canlı".into(),
                        None,
                    ));
                }
                return Some((100, "API Anahtarı Aktif".into(), "Anthropic API doğrulandı".into(), None));
            }
            None
        }
        "gemini" => {
            let url = format!(
                "https://generativelanguage.googleapis.com/v1beta/models?pageSize=1&key={api_key}"
            );
            if agent.get(&url).call().is_ok() {
                return Some((100, "Gemini API Aktif".into(), "Google AI Studio bağlı".into(), None));
            }
            None
        }
        _ => None,
    }
}

pub fn format_reset_countdown_tr(window_started_epoch: i64, window_minutes: u32, now_epoch: i64) -> String {
    if window_started_epoch <= 0 {
        return format!("{} dk pencere hazır", window_minutes);
    }
    let end = window_started_epoch + (window_minutes as i64 * 60);
    let rem = end - now_epoch;
    if rem <= 0 {
        return "Pencere yenilendi".into();
    }
    let hrs = rem / 3600;
    let mins = (rem % 3600) / 60;
    if hrs > 0 {
        format!("Yenilenmeye {hrs}s {mins}dk")
    } else {
        format!("Yenilenmeye {mins} dk")
    }
}

pub fn format_compact_tokens(tokens: u64) -> String {
    if tokens >= 1_000_000 {
        format!("{:.1}M token", tokens as f64 / 1_000_000.0)
    } else if tokens >= 1_000 {
        format!("{}K token", tokens / 1_000)
    } else {
        format!("{tokens} token")
    }
}

/// Computes live honest 3-layer hybrid quota snapshots for ChatGPT, Gemini, and Claude.
/// Runs off the UI thread (60s cadence, ONLY when an AI Quota widget is active or user refreshes).
pub fn query_ai_quota_snapshots(configs: &[AiProviderQuotaConfig]) -> Vec<AiProviderQuotaSnapshot> {
    let now_epoch = Utc::now().timestamp();
    let hhmm = Local::now().format("%H:%M").to_string();
    let api_keys = load_all_ai_api_keys(None);

    configs
        .iter()
        .map(|cfg| {
            let (display_name, usage_url) = match cfg.provider.as_str() {
                "openai" => ("ChatGPT", "https://chatgpt.com/"),
                "gemini" => ("Gemini", "https://aistudio.google.com/apikey"),
                "claude" => ("Claude", "https://claude.ai/settings/usage"),
                _ => ("AI", "https://chatgpt.com/"),
            };

            let window_secs = (cfg.window_minutes.max(15) as i64) * 60;
            let limit = cfg.window_limit.max(1) as u64;
            let api_key_opt = api_keys.get(&cfg.provider).filter(|k| !k.trim().is_empty());

            // Layer 1: Official live API check (if user saved DPAPI key)
            if let Some(key) = api_key_opt {
                if let Some((rem_pct, primary, secondary, spend)) =
                    query_live_api_usage(&cfg.provider, key, cfg.monthly_budget_usd)
                {
                    let cli = scan_local_cli_for_provider(&cfg.provider, window_secs, now_epoch);
                    return AiProviderQuotaSnapshot {
                        provider: cfg.provider.clone(),
                        display_name: display_name.into(),
                        plan_name: cfg.plan_name.clone(),
                        source_badge: "API Canlı".into(),
                        remaining_percent: rem_pct,
                        primary_label: primary,
                        secondary_label: secondary,
                        used_count: cli.messages_in_window,
                        limit_count: limit,
                        tokens_today: cli.tokens_today,
                        api_spend_usd: spend,
                        has_api_key: true,
                        usage_url: usage_url.into(),
                        updated_at_hhmm: hhmm.clone(),
                    };
                }
            }

            // Layer 2: Automatic local CLI log discovery (~/.claude, ~/.codex, ~/.gemini)
            let cli = scan_local_cli_for_provider(&cfg.provider, window_secs, now_epoch);
            if cli.messages_in_window > 0 || cli.tokens_today > 0 {
                let used = cli.messages_in_window;
                let rem_pct =
                    ((1.0 - (used as f64 / limit as f64)).clamp(0.0, 1.0) * 100.0).round() as u8;
                return AiProviderQuotaSnapshot {
                    provider: cfg.provider.clone(),
                    display_name: display_name.into(),
                    plan_name: cfg.plan_name.clone(),
                    source_badge: "CLI Log".into(),
                    remaining_percent: rem_pct,
                    primary_label: format!("{used} / {limit} mesaj"),
                    secondary_label: format!(
                        "{} bugün · {} dk pencere",
                        format_compact_tokens(cli.tokens_today),
                        cfg.window_minutes
                    ),
                    used_count: used,
                    limit_count: limit,
                    tokens_today: cli.tokens_today,
                    api_spend_usd: None,
                    has_api_key: api_key_opt.is_some(),
                    usage_url: usage_url.into(),
                    updated_at_hhmm: hhmm.clone(),
                };
            }

            // Layer 3: Smart rolling-window web subscription tracker
            let expired = cfg.window_started_epoch > 0
                && now_epoch >= cfg.window_started_epoch + window_secs;
            let used = if expired { 0 } else { cfg.manual_used as u64 };
            let rem_pct =
                ((1.0 - (used as f64 / limit as f64)).clamp(0.0, 1.0) * 100.0).round() as u8;
            let reset_str = if expired {
                "Pencere yenilendi".into()
            } else {
                format_reset_countdown_tr(cfg.window_started_epoch, cfg.window_minutes, now_epoch)
            };

            AiProviderQuotaSnapshot {
                provider: cfg.provider.clone(),
                display_name: display_name.into(),
                plan_name: cfg.plan_name.clone(),
                source_badge: "Sayaç".into(),
                remaining_percent: rem_pct,
                primary_label: format!("{used} / {limit} mesaj"),
                secondary_label: reset_str,
                used_count: used,
                limit_count: limit,
                tokens_today: 0,
                api_spend_usd: None,
                has_api_key: api_key_opt.is_some(),
                usage_url: usage_url.into(),
                updated_at_hhmm: hhmm.clone(),
            }
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn dpapi_encrypt_decrypt_roundtrip_protects_secrets() {
        let secret = b"sk-test-aurora-v4-dpapi-secret-key";
        let cipher = dpapi_protect(secret).expect("DPAPI CryptProtectData should succeed");
        assert_ne!(cipher.as_slice(), secret, "Ciphertext must not contain plaintext");
        let plain = dpapi_unprotect(&cipher).expect("DPAPI CryptUnprotectData should succeed");
        assert_eq!(plain.as_slice(), secret);

        // Full vault file roundtrip
        let tmp = std::env::temp_dir().join(format!("aurora_dpapi_test_{}.bin", std::process::id()));
        assert!(save_ai_api_key("openai", "sk-demo-1234", Some(&tmp)));
        let loaded = load_all_ai_api_keys(Some(&tmp));
        assert_eq!(loaded.get("openai").map(String::as_str), Some("sk-demo-1234"));
        assert!(save_ai_api_key("openai", "", Some(&tmp)));
        assert!(load_all_ai_api_keys(Some(&tmp)).is_empty());
        let _ = std::fs::remove_file(&tmp);
    }

    #[test]
    fn local_jsonl_parser_counts_recent_messages_and_tokens() {
        let now = 1_760_000_000i64;
        let sample = format!(
            concat!(
                r#"{{"type":"assistant","timestamp":{},"message":{{"role":"assistant","usage":{{"input_tokens":1200,"output_tokens":300}}}}}}"#, "\n",
                r#"{{"type":"assistant","timestamp":{},"message":{{"role":"assistant","usage":{{"input_tokens":500,"output_tokens":500}}}}}}"#, "\n",
                r#"{{"type":"assistant","timestamp":{},"message":{{"role":"assistant","usage":{{"input_tokens":9999,"output_tokens":1}}}}}}"#, "\n"
            ),
            now - 120,     // inside 5h window & today
            now - 25_000,  // outside 5h window (18000s), inside 24h today
            now - 120_000, // older than 24h -> ignored
        );
        let stats = parse_cli_jsonl_usage(&sample, 18_000, now);
        assert_eq!(stats.messages_in_window, 1);
        assert_eq!(stats.tokens_in_window, 1500);
        assert_eq!(stats.tokens_today, 2500);
    }

    #[test]
    fn window_countdown_formats_cleanly() {
        let now = 1_760_000_000i64;
        assert_eq!(format_reset_countdown_tr(now - 3600, 180, now), "Yenilenmeye 2s 0dk");
        assert_eq!(format_reset_countdown_tr(now - 12000, 180, now), "Pencere yenilendi");
        assert_eq!(format_compact_tokens(184_200), "184K token");
    }
}
