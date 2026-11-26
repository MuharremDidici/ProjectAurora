use std::collections::{HashMap, HashSet};
use std::path::PathBuf;

use aurora_core::{AppWindowEntry, DockAppGroup, PinnedShortcut};

pub fn update_stable_orders(
    open_windows: &[AppWindowEntry],
    pinned: &[PinnedShortcut],
    running_group_order: &mut Vec<String>,
    window_seen_order: &mut Vec<isize>,
) {
    let mut pinned_keys: HashSet<String> = HashSet::new();
    let mut pinned_aumids: HashSet<String> = HashSet::new();
    for pin in pinned {
        pinned_keys.insert(normalize_group_key(&pin.executable_path, &pin.display_name));
        if let Some(au) = extract_appsfolder_aumid(&pin.executable_path) {
            pinned_aumids.insert(au.to_ascii_lowercase());
        }
    }

    let live_hwnds: HashSet<isize> = open_windows.iter().map(|w| w.hwnd).collect();
    window_seen_order.retain(|h| live_hwnds.contains(h));
    for win in open_windows {
        if !window_seen_order.contains(&win.hwnd) {
            window_seen_order.push(win.hwnd);
        }
    }

    let mut live_unpinned: HashSet<String> = HashSet::new();
    for win in open_windows {
        let key = normalize_group_key(&win.process_name, &win.title);
        let is_pin = win
            .aumid
            .as_deref()
            .is_some_and(|au| pinned_aumids.contains(&au.to_ascii_lowercase()))
            || pinned_keys.contains(&key);
        if !is_pin {
            live_unpinned.insert(key);
        }
    }

    // Prune closed or newly pinned apps while preserving exact manual/first-seen order!
    running_group_order.retain(|k| live_unpinned.contains(k));

    // Append newly launched unpinned apps to the RIGHT end (never jump clicked apps to index 0!)
    for win in open_windows {
        let key = normalize_group_key(&win.process_name, &win.title);
        if live_unpinned.contains(&key) && !running_group_order.contains(&key) {
            running_group_order.push(key);
        }
    }
}

pub fn reorder_running_group(
    running_order: &mut Vec<String>,
    key: &str,
    target_index: usize,
) -> bool {
    let needle = key.trim().to_ascii_lowercase();
    if let Some(cur) = running_order.iter().position(|k| k == &needle) {
        let item = running_order.remove(cur);
        let dst = target_index.min(running_order.len());
        running_order.insert(dst, item);
        true
    } else {
        false
    }
}

pub fn build_dock_groups_ordered(
    pinned: &[PinnedShortcut],
    open_windows: &[AppWindowEntry],
    monitor_filter_device_id: Option<&str>,
    running_group_order: &[String],
    window_seen_order: &[isize],
) -> Vec<DockAppGroup> {
    let mut pinned_groups: Vec<DockAppGroup> = Vec::new();
    let mut pinned_idx_by_key: HashMap<String, usize> = HashMap::new();
    let mut pinned_idx_by_aumid: HashMap<String, usize> = HashMap::new();

    for pin in pinned {
        let key = normalize_group_key(&pin.executable_path, &pin.display_name);
        if pinned_idx_by_key.contains_key(&key) {
            continue;
        }
        let pin_aumid = extract_appsfolder_aumid(&pin.executable_path);
        let idx = pinned_groups.len();
        pinned_idx_by_key.insert(key.clone(), idx);
        if let Some(ref au) = pin_aumid {
            pinned_idx_by_aumid.insert(au.to_ascii_lowercase(), idx);
        }
        pinned_groups.push(DockAppGroup {
            group_key: key,
            display_name: pin.display_name.clone(),
            executable_path: pin.executable_path.clone(),
            is_pinned: true,
            pin_id: Some(pin.id.clone()),
            has_active: false,
            needs_attention: false,
            progress_percent: None,
            aumid: pin_aumid,
            audio_pid: None,
            audio_volume: None,
            audio_muted: false,
            windows: Vec::new(),
        });
    }

    let mut unpinned_map: HashMap<String, DockAppGroup> = HashMap::new();
    let mut unpinned_fallback_keys: Vec<String> = Vec::new();

    for win in open_windows {
        if let Some(mon_id) = monitor_filter_device_id {
            if !win.monitor_device_id.is_empty()
                && !win.monitor_device_id.eq_ignore_ascii_case(mon_id)
            {
                continue;
            }
        }
        let key = normalize_group_key(&win.process_name, &win.title);
        let matched_pinned = win
            .aumid
            .as_deref()
            .and_then(|au| pinned_idx_by_aumid.get(&au.to_ascii_lowercase()).copied())
            .or_else(|| pinned_idx_by_key.get(&key).copied());

        if let Some(idx) = matched_pinned {
            let g = &mut pinned_groups[idx];
            g.has_active |= win.is_active;
            g.needs_attention |= win.needs_attention;
            if g.progress_percent.is_none() && win.progress_percent.is_some() {
                g.progress_percent = win.progress_percent;
            }
            if g.executable_path.is_empty() {
                g.executable_path = win.executable_path.clone();
            }
            if g.aumid.is_none() && win.aumid.is_some() {
                g.aumid = win.aumid.clone();
            }
            g.windows.push(win.clone());
        } else if let Some(g) = unpinned_map.get_mut(&key) {
            g.has_active |= win.is_active;
            g.needs_attention |= win.needs_attention;
            if g.progress_percent.is_none() && win.progress_percent.is_some() {
                g.progress_percent = win.progress_percent;
            }
            if g.executable_path.is_empty() {
                g.executable_path = win.executable_path.clone();
            }
            if g.aumid.is_none() && win.aumid.is_some() {
                g.aumid = win.aumid.clone();
            }
            g.windows.push(win.clone());
        } else {
            let nice = pretty_process_name(&win.process_name, &win.title);
            unpinned_fallback_keys.push(key.clone());
            unpinned_map.insert(
                key.clone(),
                DockAppGroup {
                    group_key: key,
                    display_name: nice,
                    executable_path: win.executable_path.clone(),
                    is_pinned: false,
                    pin_id: None,
                    has_active: win.is_active,
                    needs_attention: win.needs_attention,
                    progress_percent: win.progress_percent,
                    aumid: win.aumid.clone(),
                    audio_pid: None,
                    audio_volume: None,
                    audio_muted: false,
                    windows: vec![win.clone()],
                },
            );
        }
    }

    let mut groups = pinned_groups;
    for ordered_key in running_group_order {
        if let Some(g) = unpinned_map.remove(ordered_key) {
            groups.push(g);
        }
    }
    for fallback_key in unpinned_fallback_keys {
        if let Some(g) = unpinned_map.remove(&fallback_key) {
            groups.push(g);
        }
    }

    if !window_seen_order.is_empty() {
        let win_pos: HashMap<isize, usize> = window_seen_order
            .iter()
            .enumerate()
            .map(|(i, &h)| (h, i))
            .collect();
        for g in &mut groups {
            if g.windows.len() > 1 {
                g.windows
                    .sort_by_key(|w| win_pos.get(&w.hwnd).copied().unwrap_or(usize::MAX));
            }
        }
    }

    // De-duplicate same keys defensively
    let mut seen = HashSet::new();
    groups.retain(|g| seen.insert(g.group_key.clone()));
    groups
}

fn extract_appsfolder_aumid(path: &str) -> Option<String> {
    let trimmed = path.trim();
    trimmed
        .strip_prefix("appsfolder:")
        .or_else(|| trimmed.strip_prefix("APPSFOLDER:"))
        .map(|s| s.trim().to_string())
        .filter(|s| !s.is_empty())
}

pub fn normalize_group_key(path_or_proc: &str, fallback: &str) -> String {
    let trimmed = path_or_proc.trim();
    if trimmed.to_ascii_lowercase().starts_with("appsfolder:") {
        let fb = fallback.trim().to_ascii_lowercase();
        if !fb.is_empty() {
            return canonical_app_key(&fb);
        }
        return trimmed.to_ascii_lowercase();
    }
    let raw = std::path::Path::new(trimmed)
        .file_name()
        .and_then(|s| s.to_str())
        .unwrap_or(trimmed)
        .trim()
        .to_ascii_lowercase();
    let stripped = raw.strip_suffix(".exe").unwrap_or(&raw);
    if stripped.is_empty() {
        canonical_app_key(&fallback.trim().to_ascii_lowercase())
    } else {
        canonical_app_key(stripped)
    }
}

fn canonical_app_key(raw: &str) -> String {
    match raw {
        "whatsapp.root" | "whatsapp" => "whatsapp".into(),
        "ms-teams" | "msteams" | "microsoft teams" => "ms-teams".into(),
        "windowsterminal" | "wt" | "windows terminal" => "windowsterminal".into(),
        "calculatorapp" | "calculator" | "hesap makinesi" => "calculator".into(),
        "winstore.app" | "microsoft store" => "store".into(),
        "systemsettings" | "ayarlar" | "settings" => "settings".into(),
        "photosapp" | "microsoft.photos" | "photos" => "photos".into(),
        other => other.to_string(),
    }
}

fn pretty_process_name(proc_name: &str, title: &str) -> String {
    let low = proc_name.trim().to_ascii_lowercase();
    match low.as_str() {
        "windowsterminal" | "wt" => "Terminal".into(),
        "code" => "VS Code".into(),
        "msedge" => "Edge".into(),
        "chrome" => "Chrome".into(),
        "explorer" => "Dosya Gezgini".into(),
        "whatsapp" | "whatsapp.root" => "WhatsApp".into(),
        "ms-teams" | "msteams" => "Microsoft Teams".into(),
        "calculatorapp" => "Hesap Makinesi".into(),
        "winstore.app" => "Microsoft Store".into(),
        "systemsettings" => "Ayarlar".into(),
        "photosapp" | "microsoft.photos" => "Fotoğraflar".into(),
        "notepad" => "Not Defteri".into(),
        "spotify" => "Spotify".into(),
        "discord" => "Discord".into(),
        _ => {
            if !proc_name.is_empty() {
                let base = proc_name.split('.').next().unwrap_or(proc_name);
                let mut c = base.chars();
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

#[cfg(test)]
mod tests {
    use super::*;

    fn mk_win(hwnd: isize, proc_name: &str, title: &str, active: bool) -> AppWindowEntry {
        AppWindowEntry {
            hwnd,
            title: title.into(),
            process_name: proc_name.into(),
            executable_path: format!("C:\\Apps\\{proc_name}.exe"),
            process_id: hwnd as u32,
            is_active: active,
            is_minimized: false,
            is_top_most: false,
            needs_attention: false,
            monitor_device_id: "MON1".into(),
            progress_percent: None,
            aumid: None,
        }
    }

    #[test]
    fn clicking_active_window_never_changes_dock_group_order() {
        let pinned: Vec<PinnedShortcut> = Vec::new();
        let mut running_order = Vec::new();
        let mut win_order = Vec::new();

        // Initial launch order: Chrome (2 windows), Notepad, Spotify
        let scan1 = vec![
            mk_win(101, "chrome", "Chrome Tab 1", true),
            mk_win(102, "chrome", "Chrome Tab 2", false),
            mk_win(201, "notepad", "Notlar.txt", false),
            mk_win(301, "spotify", "Spotify", false),
        ];
        update_stable_orders(&scan1, &pinned, &mut running_order, &mut win_order);
        let g1 = build_dock_groups_ordered(&pinned, &scan1, None, &running_order, &win_order);
        let keys1: Vec<&str> = g1.iter().map(|g| g.group_key.as_str()).collect();
        assert_eq!(keys1, vec!["chrome", "notepad", "spotify"]);
        assert_eq!(g1[0].windows.iter().map(|w| w.hwnd).collect::<Vec<_>>(), vec![101, 102]);

        // User clicks Spotify -> Win32 EnumWindows puts Spotify FIRST in Z-order!
        let scan2 = vec![
            mk_win(301, "spotify", "Spotify", true),
            mk_win(102, "chrome", "Chrome Tab 2", false),
            mk_win(101, "chrome", "Chrome Tab 1", false),
            mk_win(201, "notepad", "Notlar.txt", false),
        ];
        update_stable_orders(&scan2, &pinned, &mut running_order, &mut win_order);
        let g2 = build_dock_groups_ordered(&pinned, &scan2, None, &running_order, &win_order);
        let keys2: Vec<&str> = g2.iter().map(|g| g.group_key.as_str()).collect();
        // Order MUST NOT jump!
        assert_eq!(keys2, vec!["chrome", "notepad", "spotify"]);
        assert!(g2[2].has_active);
        // Even Chrome's internal window cards keep their original stable order!
        assert_eq!(g2[0].windows.iter().map(|w| w.hwnd).collect::<Vec<_>>(), vec![101, 102]);
    }

    #[test]
    fn manual_reorder_running_group_preserves_exact_position() {
        let pinned: Vec<PinnedShortcut> = Vec::new();
        let mut running_order = Vec::new();
        let mut win_order = Vec::new();

        let scan1 = vec![
            mk_win(101, "chrome", "Chrome", true),
            mk_win(201, "notepad", "Notlar", false),
            mk_win(301, "spotify", "Spotify", false),
        ];
        update_stable_orders(&scan1, &pinned, &mut running_order, &mut win_order);
        assert!(reorder_running_group(&mut running_order, "spotify", 0));

        // Now user clicks Notepad (Z-order puts Notepad first) -> manual order must hold!
        let scan2 = vec![
            mk_win(201, "notepad", "Notlar", true),
            mk_win(101, "chrome", "Chrome", false),
            mk_win(301, "spotify", "Spotify", false),
        ];
        update_stable_orders(&scan2, &pinned, &mut running_order, &mut win_order);
        let g = build_dock_groups_ordered(&pinned, &scan2, None, &running_order, &win_order);
        let keys: Vec<&str> = g.iter().map(|x| x.group_key.as_str()).collect();
        assert_eq!(keys, vec!["spotify", "chrome", "notepad"]);
    }
}
