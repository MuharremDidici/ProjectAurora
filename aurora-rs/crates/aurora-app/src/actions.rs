use std::collections::{HashMap, HashSet};
use std::path::PathBuf;

use aurora_core::{AppWindowEntry, DockAppGroup, PinnedShortcut};

pub fn build_dock_groups(
    pinned: &[PinnedShortcut],
    open_windows: &[AppWindowEntry],
    monitor_filter_device_id: Option<&str>,
) -> Vec<DockAppGroup> {
    let mut groups: Vec<DockAppGroup> = Vec::new();
    let mut index_by_key: HashMap<String, usize> = HashMap::new();

    for pin in pinned {
        let key = normalize_group_key(&pin.executable_path, &pin.display_name);
        if index_by_key.contains_key(&key) {
            continue;
        }
        index_by_key.insert(key.clone(), groups.len());
        groups.push(DockAppGroup {
            group_key: key,
            display_name: pin.display_name.clone(),
            executable_path: pin.executable_path.clone(),
            is_pinned: true,
            pin_id: Some(pin.id.clone()),
            has_active: false,
            needs_attention: false,
            progress_percent: None,
            windows: Vec::new(),
        });
    }

    for win in open_windows {
        if let Some(mon_id) = monitor_filter_device_id {
            if !win.monitor_device_id.is_empty()
                && !win.monitor_device_id.eq_ignore_ascii_case(mon_id)
            {
                continue;
            }
        }
        let key = normalize_group_key(&win.process_name, &win.title);
        if let Some(&idx) = index_by_key.get(&key) {
            let g = &mut groups[idx];
            g.has_active |= win.is_active;
            g.needs_attention |= win.needs_attention;
            if g.executable_path.is_empty() {
                g.executable_path = win.executable_path.clone();
            }
            g.windows.push(win.clone());
        } else {
            index_by_key.insert(key.clone(), groups.len());
            let nice = pretty_process_name(&win.process_name, &win.title);
            groups.push(DockAppGroup {
                group_key: key,
                display_name: nice,
                executable_path: win.executable_path.clone(),
                is_pinned: false,
                pin_id: None,
                has_active: win.is_active,
                needs_attention: win.needs_attention,
                progress_percent: win.progress_percent,
                windows: vec![win.clone()],
            });
        }
    }

    // De-duplicate same keys defensively
    let mut seen = HashSet::new();
    groups.retain(|g| seen.insert(g.group_key.clone()));
    groups
}

fn normalize_group_key(path_or_proc: &str, fallback: &str) -> String {
    let trimmed = path_or_proc.trim();
    if trimmed.to_ascii_lowercase().starts_with("appsfolder:") {
        let fb = fallback.trim().to_ascii_lowercase();
        if !fb.is_empty() {
            return fb;
        }
        return trimmed.to_ascii_lowercase();
    }
    let raw = std::path::Path::new(trimmed)
        .file_stem()
        .and_then(|s| s.to_str())
        .unwrap_or(trimmed)
        .trim()
        .to_ascii_lowercase();
    if raw.is_empty() {
        fallback.trim().to_ascii_lowercase()
    } else {
        raw
    }
}

fn pretty_process_name(proc_name: &str, title: &str) -> String {
    match proc_name.to_ascii_lowercase().as_str() {
        "windowsterminal" | "wt" => "Terminal".into(),
        "code" => "VS Code".into(),
        "msedge" => "Edge".into(),
        "chrome" => "Chrome".into(),
        "explorer" => "Dosya Gezgini".into(),
        "whatsapp" => "WhatsApp".into(),
        "spotify" => "Spotify".into(),
        "discord" => "Discord".into(),
        _ => {
            if !proc_name.is_empty() {
                let mut c = proc_name.chars();
                match c.next() {
                    Some(first) => first.to_uppercase().collect::<String>() + c.as_str(),
                    None => title.chars().take(18).collect(),
                }
            } else {
                title.chars().take(18).collect()
            }
        }
    }
}

pub fn user_known_folder(name: &str) -> PathBuf {
    let profile = std::env::var_os("USERPROFILE")
        .map(PathBuf::from)
        .unwrap_or_else(|| PathBuf::from("."));
    profile.join(name)
}
