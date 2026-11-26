use std::collections::{HashMap, HashSet};
use aurora_core::{BackgroundTrayApp, PrivacySnapshot, SystemMetricsSnapshot, TopProcessInfo};
use windows::core::PCWSTR;
use windows::Win32::Foundation::{CloseHandle, FILETIME, HWND};
use windows::Win32::System::Diagnostics::ToolHelp::{
    CreateToolhelp32Snapshot, Process32FirstW, Process32NextW, PROCESSENTRY32W, TH32CS_SNAPPROCESS,
};
use windows::Win32::System::Power::{
    GetSystemPowerStatus, SetThreadExecutionState, ES_CONTINUOUS, ES_DISPLAY_REQUIRED,
    ES_SYSTEM_REQUIRED, SYSTEM_POWER_STATUS,
};
use windows::Win32::System::ProcessStatus::{GetProcessMemoryInfo, PROCESS_MEMORY_COUNTERS};
use windows::Win32::System::Registry::{
    RegCloseKey, RegEnumKeyExW, RegOpenKeyExW, RegQueryValueExW, HKEY, HKEY_CURRENT_USER, KEY_READ,
    REG_EXPAND_SZ, REG_SZ, REG_VALUE_TYPE,
};
use windows::Win32::System::SystemInformation::{GlobalMemoryStatusEx, MEMORYSTATUSEX};
use windows::Win32::System::Threading::{
    GetSystemTimes, OpenProcess, TerminateProcess, PROCESS_QUERY_LIMITED_INFORMATION,
    PROCESS_TERMINATE,
};
use windows::Win32::UI::Input::KeyboardAndMouse::{
    ActivateKeyboardLayout, GetKeyboardLayout, GetKeyboardLayoutList, KLF_SETFORPROCESS,
};
use windows::Win32::UI::WindowsAndMessaging::{GetWindowThreadProcessId, PostMessageW};

/// Ultra-lightweight Win32 `CreateToolhelp32Snapshot` process name set (replaces heavy .NET `Process.GetProcesses()`!).
pub fn snapshot_running_process_names() -> HashSet<String> {
    let mut set = HashSet::with_capacity(200);
    unsafe {
        let Ok(snap) = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0) else {
            return set;
        };
        let mut pe = PROCESSENTRY32W {
            dwSize: std::mem::size_of::<PROCESSENTRY32W>() as u32,
            ..Default::default()
        };
        if Process32FirstW(snap, &mut pe).is_ok() {
            loop {
                let n = pe.szExeFile.iter().position(|&c| c == 0).unwrap_or(pe.szExeFile.len());
                let exe = String::from_utf16_lossy(&pe.szExeFile[..n]);
                let stem = exe.trim_end_matches(".exe").trim_end_matches(".EXE").to_ascii_lowercase();
                if !stem.is_empty() {
                    set.insert(stem);
                }
                if Process32NextW(snap, &mut pe).is_err() {
                    break;
                }
            }
        }
        let _ = CloseHandle(snap);
    }
    set
}

/// Zero-UAC Camera & Microphone Privacy LED detector with ghost-crash guard.
pub fn query_privacy_snapshot() -> PrivacySnapshot {
    let mut cached_procs: Option<HashSet<String>> = None;
    let cam_apps = read_consent_capability("webcam", &mut cached_procs);
    let mic_apps = read_consent_capability("microphone", &mut cached_procs);
    PrivacySnapshot {
        camera_active: !cam_apps.is_empty(),
        mic_active: !mic_apps.is_empty(),
        camera_apps: cam_apps,
        mic_apps,
    }
}

fn read_consent_capability(cap: &str, cached_procs: &mut Option<HashSet<String>>) -> Vec<String> {
    let mut active = Vec::new();
    let path_w: Vec<u16> = format!(
        r"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\{cap}"
    )
    .encode_utf16()
    .chain(std::iter::once(0))
    .collect();

    unsafe {
        let mut h_cap = HKEY::default();
        if RegOpenKeyExW(HKEY_CURRENT_USER, PCWSTR(path_w.as_ptr()), 0, KEY_READ, &mut h_cap).is_err() {
            return active;
        }
        let mut idx = 0u32;
        loop {
            let mut sub_buf = [0u16; 256];
            let mut sub_len = sub_buf.len() as u32;
            if RegEnumKeyExW(h_cap, idx, windows::core::PWSTR(sub_buf.as_mut_ptr()), &mut sub_len, None, windows::core::PWSTR::null(), None, None).is_err() {
                break;
            }
            idx += 1;
            let sub = String::from_utf16_lossy(&sub_buf[..sub_len as usize]);
            if sub.eq_ignore_ascii_case("NonPackaged") {
                let mut h_np = HKEY::default();
                if RegOpenKeyExW(h_cap, PCWSTR(sub_buf.as_ptr()), 0, KEY_READ, &mut h_np).is_ok() {
                    let mut i2 = 0u32;
                    loop {
                        let mut eb = [0u16; 320];
                        let mut el = eb.len() as u32;
                        if RegEnumKeyExW(h_np, i2, windows::core::PWSTR(eb.as_mut_ptr()), &mut el, None, windows::core::PWSTR::null(), None, None).is_err() {
                            break;
                        }
                        i2 += 1;
                        let mut h_app = HKEY::default();
                        if RegOpenKeyExW(h_np, PCWSTR(eb.as_ptr()), 0, KEY_READ, &mut h_app).is_ok() {
                            if is_consent_active(h_app) {
                                let raw = String::from_utf16_lossy(&eb[..el as usize]).replace('#', "\\");
                                let stem = std::path::Path::new(&raw)
                                    .file_stem()
                                    .and_then(|s| s.to_str())
                                    .unwrap_or("")
                                    .to_string();
                                let procs = cached_procs.get_or_insert_with(snapshot_running_process_names);
                                if !stem.is_empty()
                                    && !stem.to_ascii_lowercase().contains("aurora")
                                    && procs.contains(&stem.to_ascii_lowercase())
                                {
                                    active.push(stem);
                                }
                            }
                            let _ = RegCloseKey(h_app);
                        }
                    }
                    let _ = RegCloseKey(h_np);
                }
            } else {
                let mut h_app = HKEY::default();
                if RegOpenKeyExW(h_cap, PCWSTR(sub_buf.as_ptr()), 0, KEY_READ, &mut h_app).is_ok() {
                    if is_consent_active(h_app) && !sub.to_ascii_lowercase().contains("aurora") {
                        let friendly = sub.split('_').next().unwrap_or(&sub).to_string();
                        active.push(friendly);
                    }
                    let _ = RegCloseKey(h_app);
                }
            }
        }
        let _ = RegCloseKey(h_cap);
    }
    active.truncate(4);
    active
}

unsafe fn is_consent_active(hkey: HKEY) -> bool {
    let mut start = 0u64;
    let mut stop = 1u64;
    let mut sz1 = 8u32;
    let mut sz2 = 8u32;
    let ok1 = RegQueryValueExW(hkey, windows::core::w!("LastUsedTimeStart"), None, None, Some(&mut start as *mut _ as *mut u8), Some(&mut sz1)).is_ok();
    let ok2 = RegQueryValueExW(hkey, windows::core::w!("LastUsedTimeStop"), None, None, Some(&mut stop as *mut _ as *mut u8), Some(&mut sz2)).is_ok();
    ok1 && ok2 && start > 0 && stop == 0
}

/// Stateful CPU + RAM + Battery + Top-3 Process sampler (0% idle cost when Telemetry tab is closed!).
pub struct MetricsSampler {
    prev_idle: u64,
    prev_kernel: u64,
    prev_user: u64,
}

impl Default for MetricsSampler {
    fn default() -> Self {
        Self::new()
    }
}

impl MetricsSampler {
    pub fn new() -> Self {
        let (idle, kernel, user) = read_system_times();
        Self {
            prev_idle: idle,
            prev_kernel: kernel,
            prev_user: user,
        }
    }

    pub fn sample(&mut self, include_top_processes: bool) -> SystemMetricsSnapshot {
        let (idle, kernel, user) = read_system_times();
        let d_idle = idle.saturating_sub(self.prev_idle);
        let d_tot = kernel.saturating_sub(self.prev_kernel) + user.saturating_sub(self.prev_user);
        self.prev_idle = idle;
        self.prev_kernel = kernel;
        self.prev_user = user;
        let cpu_percent = if d_tot > 0 {
            (((d_tot.saturating_sub(d_idle)) as f64 / d_tot as f64) * 100.0).clamp(0.0, 100.0) as f32
        } else {
            0.0
        };

        let mut mem = MEMORYSTATUSEX {
            dwLength: std::mem::size_of::<MEMORYSTATUSEX>() as u32,
            ..Default::default()
        };
        let (ram_percent, ram_used_gb, ram_total_gb) = unsafe {
            if GlobalMemoryStatusEx(&mut mem).is_ok() {
                let tot = mem.ullTotalPhys as f32 / (1024.0 * 1024.0 * 1024.0);
                let avail = mem.ullAvailPhys as f32 / (1024.0 * 1024.0 * 1024.0);
                (mem.dwMemoryLoad as f32, (tot - avail).max(0.0), tot)
            } else {
                (0.0, 0.0, 16.0)
            }
        };

        let mut pwr = SYSTEM_POWER_STATUS::default();
        let (battery_present, battery_percent, is_charging) = unsafe {
            if GetSystemPowerStatus(&mut pwr).is_ok() && pwr.BatteryFlag != 128 && pwr.BatteryLifePercent <= 100 {
                (true, pwr.BatteryLifePercent, pwr.ACLineStatus == 1)
            } else {
                (false, 100, true)
            }
        };

        let top_processes = if include_top_processes {
            query_top_memory_processes(3)
        } else {
            vec![]
        };

        SystemMetricsSnapshot {
            cpu_percent,
            ram_percent,
            ram_used_gb,
            ram_total_gb,
            net_down_mbps: 0.0,
            net_up_mbps: 0.0,
            battery_present,
            battery_percent,
            is_charging,
            top_processes,
        }
    }
}

fn read_system_times() -> (u64, u64, u64) {
    let mut idle = FILETIME::default();
    let mut kernel = FILETIME::default();
    let mut user = FILETIME::default();
    unsafe {
        if GetSystemTimes(Some(&mut idle), Some(&mut kernel), Some(&mut user)).is_ok() {
            let f = |t: FILETIME| ((t.dwHighDateTime as u64) << 32) | (t.dwLowDateTime as u64);
            return (f(idle), f(kernel), f(user));
        }
    }
    (0, 0, 0)
}

pub fn query_top_memory_processes(limit: usize) -> Vec<TopProcessInfo> {
    let mut list = Vec::new();
    let own_pid = std::process::id();
    unsafe {
        let Ok(snap) = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0) else { return list };
        let mut pe = PROCESSENTRY32W {
            dwSize: std::mem::size_of::<PROCESSENTRY32W>() as u32,
            ..Default::default()
        };
        if Process32FirstW(snap, &mut pe).is_ok() {
            loop {
                let pid = pe.th32ProcessID;
                if pid > 4 && pid != own_pid {
                    let n = pe.szExeFile.iter().position(|&c| c == 0).unwrap_or(pe.szExeFile.len());
                    let exe = String::from_utf16_lossy(&pe.szExeFile[..n]);
                    let stem = exe.trim_end_matches(".exe").trim_end_matches(".EXE").to_string();
                    let low = stem.to_ascii_lowercase();
                    if !matches!(low.as_str(), "explorer" | "dwm" | "csrss" | "svchost" | "system" | "aurora") {
                        if let Ok(hp) = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid) {
                            let mut pmc = PROCESS_MEMORY_COUNTERS::default();
                            if GetProcessMemoryInfo(hp, &mut pmc, std::mem::size_of::<PROCESS_MEMORY_COUNTERS>() as u32).is_ok() {
                                let mb = (pmc.WorkingSetSize / (1024 * 1024)) as u32;
                                if mb >= 25 {
                                    list.push(TopProcessInfo { pid, name: stem, memory_mb: mb });
                                }
                            }
                            let _ = CloseHandle(hp);
                        }
                    }
                }
                if Process32NextW(snap, &mut pe).is_err() {
                    break;
                }
            }
        }
        let _ = CloseHandle(snap);
    }
    list.sort_by_key(|a| std::cmp::Reverse(a.memory_mb));
    list.truncate(limit);
    list
}

/// Returns `false` for critical OS processes and Aurora itself.
pub fn is_safe_to_terminate(name: &str) -> bool {
    let low = name.trim_end_matches(".exe").trim_end_matches(".EXE").to_ascii_lowercase();
    !matches!(
        low.as_str(),
        "system" | "explorer" | "dwm" | "csrss" | "wininit" | "lsass" | "smss" | "services" | "aurora" | "aurora.app"
    )
}

/// Safe user-process termination guard: strictly forbids terminating explorer, dwm, csrss, or aurora itself!
pub fn terminate_user_process_safe(name: &str, pid: u32) -> bool {
    if pid <= 4 || pid == std::process::id() || !is_safe_to_terminate(name) {
        return false;
    }
    unsafe {
        if let Ok(hp) = OpenProcess(PROCESS_TERMINATE, false, pid) {
            let ok = TerminateProcess(hp, 1).is_ok();
            let _ = CloseHandle(hp);
            return ok;
        }
    }
    false
}

fn read_reg_string_value(hkey: HKEY, name: &str) -> Option<String> {
    let wide_name: Vec<u16> = name.encode_utf16().chain(std::iter::once(0)).collect();
    let mut ty = REG_VALUE_TYPE::default();
    let mut bytes_len = 0u32;
    unsafe {
        let rc = RegQueryValueExW(
            hkey,
            PCWSTR(wide_name.as_ptr()),
            None,
            Some(&mut ty),
            None,
            Some(&mut bytes_len),
        );
        if rc.is_err() || bytes_len < 2 || (ty != REG_SZ && ty != REG_EXPAND_SZ) {
            return None;
        }
        let mut buf = vec![0u16; (bytes_len as usize).div_ceil(2) + 1];
        let rc2 = RegQueryValueExW(
            hkey,
            PCWSTR(wide_name.as_ptr()),
            None,
            Some(&mut ty),
            Some(buf.as_mut_ptr() as *mut u8),
            Some(&mut bytes_len),
        );
        if rc2.is_err() {
            return None;
        }
        let end = buf.iter().position(|&c| c == 0).unwrap_or(buf.len());
        let s = String::from_utf16_lossy(&buf[..end]).trim().to_string();
        if s.is_empty() { None } else { Some(s) }
    }
}

fn is_generic_tray_tooltip(tip: &str) -> bool {
    let low = tip.trim().to_ascii_lowercase();
    low.is_empty()
        || low.starts_with("notifyicon")
        || low == "not connected"
        || low == "ready"
        || low == "filtre tuşları"
}

/// Reads active Windows 11 system tray registrations from `HKCU\Control Panel\NotifyIconSettings`
/// keyed by lowercase executable stem (`(best_tooltip, raw_path)`).
pub fn read_windows_notify_icon_registry() -> HashMap<String, (String, String)> {
    let mut out: HashMap<String, (String, String)> = HashMap::new();
    let subkey: Vec<u16> = "Control Panel\\NotifyIconSettings"
        .encode_utf16()
        .chain(std::iter::once(0))
        .collect();
    unsafe {
        let mut root = HKEY::default();
        if RegOpenKeyExW(HKEY_CURRENT_USER, PCWSTR(subkey.as_ptr()), 0, KEY_READ, &mut root).is_err() {
            return out;
        }
        let mut idx = 0u32;
        let mut name_buf = [0u16; 260];
        loop {
            let mut name_len = name_buf.len() as u32;
            let rc = RegEnumKeyExW(
                root,
                idx,
                windows::core::PWSTR(name_buf.as_mut_ptr()),
                &mut name_len,
                None,
                windows::core::PWSTR::null(),
                None,
                None,
            );
            if rc.is_err() {
                break;
            }
            idx += 1;
            let mut item_key = HKEY::default();
            if RegOpenKeyExW(root, PCWSTR(name_buf.as_ptr()), 0, KEY_READ, &mut item_key).is_ok() {
                if let Some(exe_path) = read_reg_string_value(item_key, "ExecutablePath") {
                    let leaf = exe_path.rsplit(['\\', '/']).next().unwrap_or("");
                    let stem = leaf
                        .trim_end_matches(".exe")
                        .trim_end_matches(".EXE")
                        .to_ascii_lowercase();
                    if !stem.is_empty() {
                        let tip = read_reg_string_value(item_key, "InitialTooltip").unwrap_or_default();
                        let entry = out.entry(stem).or_insert_with(|| (String::new(), exe_path.clone()));
                        if (is_generic_tray_tooltip(&entry.0) && !is_generic_tray_tooltip(&tip))
                            || (entry.0.is_empty() && !tip.trim().is_empty())
                        {
                            entry.0 = tip.trim().to_string();
                        }
                    }
                }
                let _ = RegCloseKey(item_key);
            }
        }
        let _ = RegCloseKey(root);
    }
    out
}

fn title_case_tray_stem(stem: &str) -> String {
    let mut words = Vec::new();
    for part in stem.split(['_', '-', '.']) {
        let p = part.trim();
        if p.is_empty() || p.eq_ignore_ascii_case("root") || p.eq_ignore_ascii_case("app") {
            continue;
        }
        let mut ch = p.chars();
        if let Some(first) = ch.next() {
            words.push(format!("{}{}", first.to_uppercase(), ch.as_str()));
        }
    }
    if words.is_empty() {
        stem.to_string()
    } else {
        words.join(" ")
    }
}

/// Discovers active Windows 11 system tray & resident user applications using real Windows
/// `NotifyIconSettings` registrations cross-referenced with running processes + `KNOWN_TRAY`
/// friendly names, keeping live tray apps (WhatsApp, Spotify, Teams, MuMuPlayer, GlobalProtect,
/// NVIDIA, Vanguard, Security, Docker, RustWTM, etc.) visible just like Windows 11's tray.
pub fn query_background_tray_apps(visible_window_procs: &HashSet<String>) -> Vec<BackgroundTrayApp> {
    const KNOWN_TRAY: &[(&str, &str, &str)] = &[
        ("steam", "Steam", "Oyun Platformu"),
        ("steamwebhelper", "Steam", "Oyun Platformu"),
        ("discord", "Discord", "Ses & Sohbet"),
        ("spotify", "Spotify", "Müzik"),
        ("onedrive", "OneDrive", "Bulut Senkron"),
        ("lghub", "Logitech G HUB", "Donanım"),
        ("lghub_agent", "Logitech G HUB", "Donanım"),
        ("nvcontainer", "NVIDIA Ayarları", "Grafik"),
        ("nvdisplay.container", "NVIDIA Ayarları", "Grafik"),
        ("nvidia overlay", "NVIDIA Overlay", "Grafik"),
        ("radeonsoftware", "AMD Adrenalin", "Grafik"),
        ("igcctray", "Intel Graphics", "Grafik"),
        ("sharex", "ShareX", "Ekran Görüntüsü"),
        ("powertoys", "PowerToys", "Araçlar"),
        ("obs64", "OBS Studio", "Yayın / Kayıt"),
        ("telegram", "Telegram", "Mesajlaşma"),
        ("whatsapp", "WhatsApp", "Mesajlaşma"),
        ("whatsapp.root", "WhatsApp", "Mesajlaşma"),
        ("teams", "Microsoft Teams", "İletişim"),
        ("ms-teams", "Microsoft Teams", "İletişim"),
        ("docker desktop", "Docker Desktop", "Konteyner"),
        ("everything", "Everything", "Hızlı Arama"),
        ("razercentral", "Razer Synapse", "Donanım"),
        ("icue", "Corsair iCUE", "Donanım"),
        ("epicgameslauncher", "Epic Games", "Oyun Platformu"),
        ("vanguard", "Riot Vanguard", "Güvenlik"),
        ("vgtray", "Riot Vanguard", "Güvenlik"),
        ("riotclientservices", "Riot Client", "Oyun Platformu"),
        ("riotclientux", "Riot Client", "Oyun Platformu"),
        ("googledrivefs", "Google Drive", "Bulut"),
        ("dropbox", "Dropbox", "Bulut"),
        ("cloudflare-warp", "Cloudflare WARP", "VPN / Ağ"),
        ("tailscale-ipn", "Tailscale", "VPN / Ağ"),
        ("wireguard", "WireGuard", "VPN / Ağ"),
        ("nordvpn", "NordVPN", "VPN / Ağ"),
        ("protonvpn", "Proton VPN", "VPN / Ağ"),
        ("pangpa", "GlobalProtect VPN", "VPN / Ağ"),
        ("bitwarden", "Bitwarden", "Parola Kasası"),
        ("1password", "1Password", "Parola Kasası"),
        ("keepassxc", "KeePassXC", "Parola Kasası"),
        ("wallpaper64", "Wallpaper Engine", "Masaüstü"),
        ("wallpaper32", "Wallpaper Engine", "Masaüstü"),
        ("msiafterburner", "MSI Afterburner", "Donanım"),
        ("rtss", "RivaTuner", "Performans"),
        ("steelseriesgg", "SteelSeries GG", "Ses / Donanım"),
        ("lenovovantage", "Lenovo Vantage", "Sistem"),
        ("batterywidgethost", "Lenovo Pil Araçları", "Donanım"),
        ("imcontroller", "Lenovo System", "Sistem"),
        ("securityhealthsystray", "Windows Güvenliği", "Güvenlik"),
        ("phoneexperiencehost", "Telefon Bağlantısı", "Cihazlar"),
        ("yourphone", "Telefon Bağlantısı", "Cihazlar"),
        ("chatgpt", "ChatGPT", "Yapay Zeka"),
        ("claude", "Claude", "Yapay Zeka"),
        ("mumunxmain", "MuMuPlayer", "Emülatör"),
        ("rustwtm", "RustWTM", "Sistem / Görev"),
        ("rdpwin", "Secret Server RDP", "Uzak Masaüstü"),
        ("dbeaver", "DBeaver", "Veritabanı"),
        ("f.lux", "f.lux", "Ekran"),
        ("earrumpet", "EarTrumpet", "Ses"),
        ("eartrumpet", "EarTrumpet", "Ses"),
        ("qbittorrent", "qBittorrent", "İndirme"),
        ("idm", "IDM", "İndirme"),
        ("idman", "Internet Download Manager", "İndirme"),
    ];
    const SKIP_NOISE_STEMS: &[&str] = &[
        "explorer",
        "aurora",
        "msedge",
        "chrome",
        "brave",
        "firefox",
        "opera",
        "vivaldi",
        "applicationframehost",
        "shellexperiencehost",
        "startmenuexperiencehost",
        "searchhost",
        "textinputhost",
        "runtimebroker",
        "systemsettings",
        "monotificationux",
        "svchost",
        "conhost",
        "dllhost",
        "cmd",
        "powershell",
        "pwsh",
        "windowsterminal",
    ];
    const EDITOR_NOISE_STEMS: &[&str] = &["antigravity", "code", "cursor", "devenv", "idea64"];

    let notify_reg = read_windows_notify_icon_registry();
    let mut by_name: HashMap<String, BackgroundTrayApp> = HashMap::new();

    unsafe {
        let Ok(snap) = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0) else {
            return Vec::new();
        };
        let mut pe = PROCESSENTRY32W {
            dwSize: std::mem::size_of::<PROCESSENTRY32W>() as u32,
            ..Default::default()
        };
        if Process32FirstW(snap, &mut pe).is_ok() {
            loop {
                let n = pe.szExeFile.iter().position(|&c| c == 0).unwrap_or(pe.szExeFile.len());
                let exe = String::from_utf16_lossy(&pe.szExeFile[..n]);
                let stem = exe
                    .trim_end_matches(".exe")
                    .trim_end_matches(".EXE")
                    .to_ascii_lowercase();

                if !stem.is_empty()
                    && !SKIP_NOISE_STEMS.contains(&stem.as_str())
                    && !(EDITOR_NOISE_STEMS.contains(&stem.as_str())
                        && visible_window_procs.contains(&stem))
                {
                    let resolved: Option<(String, String)> =
                        if let Some(&(_, friendly, sub)) = KNOWN_TRAY.iter().find(|&&(k, _, _)| k == stem) {
                            Some((friendly.to_string(), sub.to_string()))
                        } else if let Some((tip, _)) = notify_reg.get(&stem) {
                            let clean_tip = tip
                                .split(['·', '\n', '\r'])
                                .next()
                                .unwrap_or("")
                                .trim();
                            let friendly = if !is_generic_tray_tooltip(clean_tip) && clean_tip.len() >= 2 && clean_tip.len() <= 36 {
                                clean_tip.to_string()
                            } else {
                                title_case_tray_stem(&stem)
                            };
                            let sub = if visible_window_procs.contains(&stem) {
                                "Tepside Aktif".to_string()
                            } else {
                                "Arka Plan".to_string()
                            };
                            Some((friendly, sub))
                        } else {
                            None
                        };

                    if let Some((friendly, sub)) = resolved {
                        let mem_mb = query_process_ws_mb(pe.th32ProcessID);
                        let key_low = friendly.to_ascii_lowercase();
                        let entry = by_name.entry(key_low).or_insert_with(|| {
                            let (_, mut full_path) =
                                crate::windows_catalog::query_process_image(pe.th32ProcessID);
                            if full_path.is_empty() {
                                if let Some((_, reg_path)) = notify_reg.get(&stem) {
                                    if !reg_path.starts_with('{') {
                                        full_path = reg_path.clone();
                                    }
                                }
                            }
                            BackgroundTrayApp {
                                id: format!("tray-{}", stem),
                                name: friendly.clone(),
                                executable_path: full_path,
                                process_id: pe.th32ProcessID,
                                memory_mb: 0,
                                subtitle: sub,
                            }
                        });
                        entry.memory_mb = entry.memory_mb.saturating_add(mem_mb);
                        if entry.executable_path.is_empty() {
                            let (_, full_path) =
                                crate::windows_catalog::query_process_image(pe.th32ProcessID);
                            if !full_path.is_empty() {
                                entry.executable_path = full_path;
                            }
                        }
                    }
                }
                if Process32NextW(snap, &mut pe).is_err() {
                    break;
                }
            }
        }
        let _ = CloseHandle(snap);
    }

    let mut out: Vec<BackgroundTrayApp> = by_name.into_values().collect();
    out.sort_by_key(|a| a.name.to_ascii_lowercase());
    out
}

fn query_process_ws_mb(pid: u32) -> u64 {
    unsafe {
        let Ok(h) = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid) else {
            return 0;
        };
        let mut pmc = PROCESS_MEMORY_COUNTERS::default();
        let ok = GetProcessMemoryInfo(
            h,
            &mut pmc,
            std::mem::size_of::<PROCESS_MEMORY_COUNTERS>() as u32,
        )
        .is_ok();
        let _ = CloseHandle(h);
        if ok {
            (pmc.WorkingSetSize as u64) / (1024 * 1024)
        } else {
            0
        }
    }
}

pub fn get_keyboard_layout_code(fg_hwnd: isize) -> (String, String) {
    unsafe {
        let tid = if fg_hwnd != 0 {
            GetWindowThreadProcessId(HWND(fg_hwnd as *mut _), None)
        } else {
            0
        };
        let hkl = GetKeyboardLayout(tid);
        let lang_id = (hkl.0 as usize) & 0xFFFF;
        match lang_id & 0x3FF {
            0x1F => ("TR".into(), "Türkçe Q".into()),
            0x09 => ("EN".into(), "English (US)".into()),
            0x07 => ("DE".into(), "Deutsch".into()),
            0x0C => ("FR".into(), "Français".into()),
            0x19 => ("RU".into(), "Русский".into()),
            _ => (format!("{:02X}", lang_id & 0xFF), "Klavye".into()),
        }
    }
}

pub fn cycle_keyboard_layout(fg_hwnd: isize) -> (String, String) {
    unsafe {
        let mut hkls = [windows::Win32::UI::Input::KeyboardAndMouse::HKL::default(); 8];
        let count = GetKeyboardLayoutList(Some(&mut hkls)) as usize;
        if count >= 2 {
            let tid = if fg_hwnd != 0 {
                GetWindowThreadProcessId(HWND(fg_hwnd as *mut _), None)
            } else {
                0
            };
            let cur = GetKeyboardLayout(tid);
            let cur_idx = hkls[..count].iter().position(|&h| h == cur).unwrap_or(0);
            let next = hkls[(cur_idx + 1) % count];
            let _ = ActivateKeyboardLayout(next, KLF_SETFORPROCESS);
            if fg_hwnd != 0 {
                let _ = PostMessageW(
                    HWND(fg_hwnd as *mut _),
                    0x0050, // WM_INPUTLANGCHANGEREQUEST
                    windows::Win32::Foundation::WPARAM(0),
                    windows::Win32::Foundation::LPARAM(next.0 as isize),
                );
            }
        }
    }
    get_keyboard_layout_code(fg_hwnd)
}

pub fn set_awake_keep_alive(active: bool) {
    unsafe {
        if active {
            SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
        } else {
            SetThreadExecutionState(ES_CONTINUOUS);
        }
    }
}

pub fn trigger_lock_workstation() {
    unsafe { let _ = windows::Win32::System::Shutdown::LockWorkStation(); }
}

pub fn trigger_sleep() {
    let _ = std::process::Command::new("rundll32.exe")
        .args(["powrprof.dll,SetSuspendState", "0,1,0"])
        .spawn();
}

pub fn trigger_power_command(restart: bool) {
    let flag = if restart { "/r" } else { "/s" };
    let _ = std::process::Command::new("shutdown.exe")
        .args([flag, "/t", "0"])
        .spawn();
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn notify_icon_registry_and_tray_query_return_deduplicated_apps() {
        let reg = read_windows_notify_icon_registry();
        assert!(reg.len() < 10_000);
        let trays = query_background_tray_apps(&HashSet::new());
        let mut names = HashSet::new();
        for t in &trays {
            assert!(names.insert(t.name.to_ascii_lowercase()), "duplicate tray app {}", t.name);
        }
    }
}
