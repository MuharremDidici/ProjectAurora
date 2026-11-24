use std::collections::{HashMap, HashSet};
use std::io::Cursor;
use std::path::{Path, PathBuf};

use aurora_core::InstalledAppEntry;
use base64::Engine;
use windows::core::{Interface, HSTRING, PCWSTR};
use windows::Win32::Foundation::{HGLOBAL, SIZE};
use windows::Win32::Graphics::Gdi::{
    CreateCompatibleDC, DeleteDC, DeleteObject, GetDIBits, GetObjectW, SelectObject, BITMAP,
    BITMAPINFO, BITMAPINFOHEADER, BI_RGB, DIB_RGB_COLORS, HBITMAP,
};
use windows::Win32::System::Com::{
    CoCreateInstance, CoInitializeEx, CoTaskMemFree, CLSCTX_INPROC_SERVER, COINIT_APARTMENTTHREADED,
};
use windows::Win32::System::DataExchange::{CloseClipboard, GetClipboardData, OpenClipboard};
use windows::Win32::System::Memory::{GlobalLock, GlobalUnlock};
use windows::Win32::System::Registry::{
    RegCloseKey, RegCreateKeyExW, RegDeleteValueW, RegOpenKeyExW, RegQueryValueExW,
    RegSetValueExW, HKEY, HKEY_CURRENT_USER, KEY_READ, KEY_WRITE, REG_OPTION_NON_VOLATILE, REG_SZ,
};
use windows::Win32::UI::Input::KeyboardAndMouse::{
    SendInput, INPUT, INPUT_0, INPUT_KEYBOARD, KEYBDINPUT, KEYEVENTF_KEYUP, VIRTUAL_KEY,
    VK_CONTROL, VK_LWIN, VK_TAB,
};
use windows::Win32::UI::Shell::{
    FileOpenDialog, IEnumShellItems, IFileOpenDialog, IShellItem, IShellItemImageFactory,
    SHCreateItemFromParsingName, SHGetKnownFolderItem, ShellExecuteW, BHID_EnumItems,
    FOLDERID_AppsFolder, FOS_FORCEFILESYSTEM, FOS_PICKFOLDERS, KF_FLAG_DEFAULT, SIGDN_FILESYSPATH,
    SIGDN_NORMALDISPLAY, SIGDN_PARENTRELATIVEPARSING, SIIGBF_BIGGERSIZEOK, SIIGBF_ICONONLY,
    SIIGBF_RESIZETOFIT,
};
use windows::Win32::UI::WindowsAndMessaging::SW_SHOWNORMAL;

use crate::productivity::format_size;
use crate::windows_catalog::extract_icon_bgra;

const NOISE_KEYWORDS: &[&str] = &[
    "uninstall",
    "kaldır",
    "readme",
    "benioku",
    "help",
    "yardım",
    "website",
    "license",
    "lisans",
    "release notes",
    "manual",
    "about ",
    "check for updates",
    "documentation",
    "dokümantasyon",
    "migration",
    "debuggable package",
    "application verifier",
];

const PRIORITY_KEYWORDS: &[&str] = &[
    "Dosya Gezgini",
    "Brave",
    "Chrome",
    "Opera",
    "Firefox",
    "Edge",
    "ChatGPT",
    "Claude",
    "Visual Studio",
    "Code",
    "Antigravity",
    "Android Studio",
    "Cursor",
    "Discord",
    "Spotify",
    "Steam",
    "Teams",
    "WhatsApp",
    "Telegram",
    "Ayarlar",
    "Görev Yöneticisi",
    "Ekran Alıntısı",
    "Hesap Makinesi",
    "Not Defteri",
    "PowerShell",
    "Terminal",
    "Fotoğraflar",
    "Microsoft Store",
    "Word",
    "Excel",
    "PowerPoint",
    "Paint",
];

const BUILTIN_SETTINGS_AND_TOOLS: &[(&str, &str, &str, &str, &str)] = &[
    ("Dosya Gezgini", "explorer.exe", "Sistem", "file explorer klasor dosyalar bilgisayarim this pc", "app"),
    ("Windows Ayarları", "ms-settings:", "Ayarlar", "settings sistem ayarlar denetim", "setting"),
    ("Görev Yöneticisi", "taskmgr.exe", "Sistem", "task manager performans cpu ram islemler", "tool"),
    ("Ekran Alıntısı Al", "ms-screenclip:", "Araçlar", "snipping tool screenshot ekran goruntusu kirp", "tool"),
    ("Hesap Makinesi", "calc.exe", "Araçlar", "calculator hesapla matematik", "app"),
    ("Not Defteri", "notepad.exe", "Araçlar", "notepad metin not text", "app"),
    ("Paint", "mspaint.exe", "Araçlar", "resim cizim boya", "app"),
    ("Denetim Masası", "control.exe", "Sistem", "control panel klasik ayarlar", "tool"),
    ("Komut İstemi (CMD)", "cmd.exe", "Geliştirme", "command prompt terminal konsol", "tool"),
    ("PowerShell", "powershell.exe", "Geliştirme", "terminal ps komut kabuk", "tool"),
    ("Windows Terminal", "wt.exe", "Geliştirme", "terminal powershell cmd wt", "app"),
    ("Ekran & Çözünürlük Ayarları", "ms-settings:display", "Ayarlar", "monitor parlaklik cozunurluk olcek gece isigi hdr hz", "setting"),
    ("Ses & Mikrofon Ayarları", "ms-settings:sound", "Ayarlar", "audio hoparlor kulaklik mikrofon volume ses", "setting"),
    ("Wi-Fi & Kablosuz Ağ", "ms-settings:network-wifi", "Ayarlar", "internet wifi kablosuz ag baglanti", "setting"),
    ("Ağ ve İnternet Ayarları", "ms-settings:network-status", "Ayarlar", "ethernet lan dns ip network", "setting"),
    ("Bluetooth ve Cihazlar", "ms-settings:bluetooth", "Ayarlar", "kulaklik fare klavye eslestir bluetooth", "setting"),
    ("Yüklü Uygulamalar & Kaldır", "ms-settings:appsfeatures", "Ayarlar", "program kaldir apps features uninstall", "setting"),
    ("Başlangıç Uygulamaları", "ms-settings:startupapps", "Ayarlar", "startup acilis otomatik baslat", "setting"),
    ("Depolama & Disk Alanı", "ms-settings:storagesense", "Ayarlar", "storage disk temizle alan ssd", "setting"),
    ("Güç, Uyku ve Pil", "ms-settings:powersleep", "Ayarlar", "battery power uyku ekran suresi enerji", "setting"),
    ("Windows Update (Güncelleme)", "ms-settings:windowsupdate", "Ayarlar", "guncelleme update yama", "setting"),
    ("Kişiselleştirme & Duvar Kağıdı", "ms-settings:personalization", "Ayarlar", "wallpaper arka plan tema personalization", "setting"),
    ("Renkler & Koyu/Açık Mod", "ms-settings:colors", "Ayarlar", "dark light tema renk accent", "setting"),
    ("Saat, Tarih ve Dil", "ms-settings:dateandtime", "Ayarlar", "clock time saat tarih bolge klavye dil", "setting"),
    ("Fare & Dokunmatik Yüzey", "ms-settings:mousetouchpad", "Ayarlar", "mouse imlec hiz touchpad", "setting"),
    ("Gizlilik ve Güvenlik", "ms-settings:privacy", "Ayarlar", "privacy kamera mikrofon konum izin", "setting"),
    ("Windows Güvenliği (Defender)", "windowsdefender:", "Ayarlar", "antivirus virus guvenlik duvari firewall", "setting"),
    ("Sistem Hakkında (Özellikler)", "ms-settings:about", "Ayarlar", "about bilgisayar adi ram islemci sistem ozellikleri", "setting"),
    ("Bildirim Ayarları", "ms-settings:notifications", "Ayarlar", "notifications odak rahatsiz etme", "setting"),
    ("VPN Ayarları", "ms-settings:network-vpn", "Ayarlar", "vpn baglanti ozel ag", "setting"),
    ("Proxy Ayarları", "ms-settings:network-proxy", "Ayarlar", "proxy vekil sunucu", "setting"),
    ("Oyun Modu", "ms-settings:gaming-gamemode", "Ayarlar", "game mode oyun performans", "setting"),
    ("Geliştirici Ayarları", "ms-settings:developers", "Ayarlar", "developer mode gelistirici sudo", "setting"),
    ("Varsayılan Uygulamalar", "ms-settings:defaultapps", "Ayarlar", "default browser varsayilan tarayici", "setting"),
    ("Yazıcılar ve Tarayıcılar", "ms-settings:printers", "Ayarlar", "printer yazici tarayici", "setting"),
    ("Hesaplar ve Oturum", "ms-settings:yourinfo", "Ayarlar", "account hesap parola pin", "setting"),
    ("Cihaz Yöneticisi", "devmgmt.msc", "Sistem", "device manager surucu driver donanim", "tool"),
    ("Hizmetler (Services)", "services.msc", "Sistem", "services servisler arka plan", "tool"),
    ("Disk Yönetimi", "diskmgmt.msc", "Sistem", "disk management bolum partition surucu", "tool"),
    ("Kayıt Defteri (Regedit)", "regedit.exe", "Sistem", "registry editor kayit defteri", "tool"),
    ("Sistem Bilgisi (msinfo32)", "msinfo32.exe", "Sistem", "system information bios anakart donanim", "tool"),
    ("DirectX Tanılama (dxdiag)", "dxdiag.exe", "Sistem", "gpu ekran karti directx", "tool"),
    ("Program Ekle veya Kaldır", "appwiz.cpl", "Sistem", "klasik program kaldir", "tool"),
    ("Ağ Bağlantıları (ncpa.cpl)", "ncpa.cpl", "Sistem", "adapter ethernet wifi dns", "tool"),
    ("Uzak Masaüstü (RDP)", "mstsc.exe", "Araçlar", "remote desktop uzak baglanti rdp", "tool"),
    ("Kaynak İzleyici", "resmon.exe", "Sistem", "resource monitor cpu disk ag izle", "tool"),
    ("Windows Sürümü (winver)", "winver.exe", "Sistem", "build version surum", "tool"),
    ("Ortam Değişkenleri", "rundll32.exe|sysdm.cpl,EditEnvironmentVariables", "Geliştirme", "path environment variables env", "tool"),
];

/// Full Windows 11 Installed Apps catalog via native `shell:AppsFolder` COM (discovers ALL UWP,
/// MSIX, Store, WhatsApp, Photos, Settings, Terminal, Electron & Win32 apps WITH crisp PNG icons!)
/// plus Desktop/Start Menu shortcuts and 48 curated Windows 11 Settings & Power Tools.
pub fn build_full_installed_app_catalog(include_icons: bool) -> Vec<InstalledAppEntry> {
    let mut by_name: HashMap<String, InstalledAppEntry> = HashMap::new();

    // 1. Native shell:AppsFolder COM enumeration (UWP + MSIX + Store + Win32)
    for app in enumerate_shell_apps_folder(include_icons) {
        let low = app.name.to_ascii_lowercase();
        if app.name.len() < 2 || NOISE_KEYWORDS.iter().any(|k| low.contains(k)) {
            continue;
        }
        by_name.entry(low).or_insert(app);
    }

    // 2. Merge Start Menu + Desktop .lnk / .url shortcuts for portable/game tools
    merge_shortcuts_from_roots(&mut by_name, include_icons);

    // 3. Merge 48 curated Windows 11 Settings pages & Admin Power Tools
    let mut seen_settings_icon: Option<String> = None;
    if include_icons {
        seen_settings_icon = by_name
            .values()
            .find(|a| a.name.eq_ignore_ascii_case("Ayarlar") || a.name.eq_ignore_ascii_case("Settings"))
            .and_then(|a| a.icon_base64.clone());
    }

    for &(name, target, cat, kw, kind) in BUILTIN_SETTINGS_AND_TOOLS {
        let key = name.to_ascii_lowercase();
        if let Some(existing) = by_name.get_mut(&key) {
            if !existing.keywords.contains(kw) {
                existing.keywords = format!("{} {kw}", existing.keywords).trim().to_string();
            }
            continue;
        }
        let icon = if !include_icons {
            None
        } else if kind == "setting" {
            seen_settings_icon.clone()
        } else {
            let exe = target.split('|').next().unwrap_or(target);
            extract_icon_png_base64(None, exe)
        };
        let id = format!("{kind}-{}", by_name.len());
        by_name.insert(
            key,
            InstalledAppEntry {
                id,
                name: name.into(),
                launch_path: target.into(),
                category: cat.into(),
                keywords: kw.into(),
                kind: kind.into(),
                icon_base64: icon,
            },
        );
    }

    let mut list: Vec<InstalledAppEntry> = by_name.into_values().collect();
    list.sort_by(|a, b| {
        let a_app = a.kind == "app";
        let b_app = b.kind == "app";
        let a_pri = PRIORITY_KEYWORDS.iter().any(|p| a.name.to_ascii_lowercase().contains(&p.to_ascii_lowercase()));
        let b_pri = PRIORITY_KEYWORDS.iter().any(|p| b.name.to_ascii_lowercase().contains(&p.to_ascii_lowercase()));
        let a_ic = a.icon_base64.is_some();
        let b_ic = b.icon_base64.is_some();
        b_app
            .cmp(&a_app)
            .then(b_pri.cmp(&a_pri))
            .then(b_ic.cmp(&a_ic))
            .then(a.name.to_ascii_lowercase().cmp(&b.name.to_ascii_lowercase()))
    });
    list
}

fn enumerate_shell_apps_folder(include_icons: bool) -> Vec<InstalledAppEntry> {
    let mut results = Vec::with_capacity(256);
    unsafe {
        let _ = CoInitializeEx(None, COINIT_APARTMENTTHREADED);
        let Ok(folder): windows::core::Result<IShellItem> =
            SHGetKnownFolderItem(&FOLDERID_AppsFolder, KF_FLAG_DEFAULT, None)
        else {
            return results;
        };
        let Ok(enumerator): windows::core::Result<IEnumShellItems> =
            folder.BindToHandler(None, &BHID_EnumItems)
        else {
            return results;
        };

        let mut idx = 0usize;
        loop {
            let mut batch: [Option<IShellItem>; 1] = [None];
            let mut fetched = 0u32;
            if enumerator.Next(&mut batch, Some(&mut fetched)).is_err() || fetched == 0 {
                break;
            }
            let Some(item) = batch[0].take() else {
                break;
            };

            let name = item
                .GetDisplayName(SIGDN_NORMALDISPLAY)
                .ok()
                .and_then(|pw| pwstr_to_string_and_free(pw));
            let app_id = item
                .GetDisplayName(SIGDN_PARENTRELATIVEPARSING)
                .ok()
                .and_then(|pw| pwstr_to_string_and_free(pw));

            let (Some(name), Some(app_id)) = (name, app_id) else {
                continue;
            };
            let name = name.trim().to_string();
            let app_id = app_id.trim().to_string();
            if name.is_empty() || app_id.is_empty() {
                continue;
            }

            let icon_base64 = if include_icons {
                extract_shell_item_icon_base64(&item, 32)
            } else {
                None
            };
            let category = aurora_core::classify_tidy_category(&name).label_tr().to_string();
            let keywords = format!("{} {}", name.to_ascii_lowercase(), app_id.to_ascii_lowercase());
            results.push(InstalledAppEntry {
                id: format!("appsfolder-{idx}"),
                name,
                launch_path: format!("appsfolder:{app_id}"),
                category,
                keywords,
                kind: "app".into(),
                icon_base64,
            });
            idx += 1;
            if idx >= 400 {
                break;
            }
        }
    }
    results
}

fn merge_shortcuts_from_roots(by_name: &mut HashMap<String, InstalledAppEntry>, include_icons: bool) {
    let mut roots: Vec<(PathBuf, &str, usize)> = Vec::new();
    if let Some(up) = std::env::var_os("USERPROFILE") {
        roots.push((PathBuf::from(&up).join("Desktop"), "Masaüstü", 0));
    }
    if let Some(pub_dir) = std::env::var_os("PUBLIC") {
        roots.push((PathBuf::from(pub_dir).join("Desktop"), "Masaüstü", 0));
    }
    if let Some(ad) = std::env::var_os("APPDATA") {
        roots.push((PathBuf::from(ad).join(r"Microsoft\Windows\Start Menu\Programs"), "Uygulama", 3));
    }
    if let Some(pd) = std::env::var_os("ProgramData") {
        roots.push((PathBuf::from(pd).join(r"Microsoft\Windows\Start Menu\Programs"), "Uygulama", 3));
    }

    for (root, default_cat, max_depth) in roots {
        scan_shortcut_dir(&root, default_cat, 0, max_depth, by_name, include_icons);
    }
}

fn scan_shortcut_dir(
    dir: &Path,
    default_cat: &str,
    depth: usize,
    max_depth: usize,
    by_name: &mut HashMap<String, InstalledAppEntry>,
    include_icons: bool,
) {
    if depth > max_depth || by_name.len() >= 480 {
        return;
    }
    let Ok(rd) = std::fs::read_dir(dir) else { return };
    for entry in rd.flatten() {
        let p = entry.path();
        if p.is_dir() {
            if depth < max_depth {
                let s = p.to_string_lossy();
                if !s.contains("Windows Kits") && !s.contains("Maintenance") {
                    scan_shortcut_dir(&p, default_cat, depth + 1, max_depth, by_name, include_icons);
                }
            }
            continue;
        }
        let ext = p.extension().and_then(|e| e.to_str()).unwrap_or("");
        if !ext.eq_ignore_ascii_case("lnk") && !ext.eq_ignore_ascii_case("url") {
            continue;
        }
        let Some(stem) = p.file_stem().and_then(|s| s.to_str()) else { continue };
        let name = stem.trim();
        let low = name.to_ascii_lowercase();
        if name.len() < 2 || name.starts_with('$') || NOISE_KEYWORDS.iter().any(|k| low.contains(k)) {
            continue;
        }
        if by_name.contains_key(&low) {
            continue;
        }
        let path_str = p.to_string_lossy().to_string();
        let icon_base64 = if include_icons {
            extract_shell_path_icon_base64(&path_str, 32).or_else(|| extract_icon_png_base64(None, &path_str))
        } else {
            None
        };
        let cat = if default_cat == "Masaüstü" {
            "Masaüstü".to_string()
        } else {
            aurora_core::classify_tidy_category(name).label_tr().to_string()
        };
        let id = format!("lnk-{}", by_name.len());
        by_name.insert(
            low.clone(),
            InstalledAppEntry {
                id,
                name: name.to_string(),
                launch_path: path_str,
                category: cat.clone(),
                keywords: format!("{low} {}", cat.to_ascii_lowercase()),
                kind: "app".into(),
                icon_base64,
            },
        );
    }
}

unsafe fn pwstr_to_string_and_free(pw: windows::core::PWSTR) -> Option<String> {
    if pw.0.is_null() {
        return None;
    }
    let s = pw.to_string().ok();
    CoTaskMemFree(Some(pw.0 as *const _));
    s
}

/// Extracts a crisp transparent ARGB PNG (base64) directly from an `IShellItem` via `IShellItemImageFactory`.
unsafe fn extract_shell_item_icon_base64(item: &IShellItem, size: i32) -> Option<String> {
    let factory: IShellItemImageFactory = item.cast().ok()?;
    let hbm = factory
        .GetImage(
            SIZE { cx: size, cy: size },
            SIIGBF_ICONONLY | SIIGBF_RESIZETOFIT | SIIGBF_BIGGERSIZEOK,
        )
        .or_else(|_| factory.GetImage(SIZE { cx: size, cy: size }, SIIGBF_RESIZETOFIT))
        .ok()?;
    let res = hbitmap_to_png_base64(hbm);
    let _ = DeleteObject(hbm);
    res
}

pub fn extract_shell_path_icon_base64(path: &str, size: i32) -> Option<String> {
    if path.trim().is_empty() {
        return None;
    }
    unsafe {
        let _ = CoInitializeEx(None, COINIT_APARTMENTTHREADED);
        let wide = HSTRING::from(path);
        let item: IShellItem = SHCreateItemFromParsingName(&wide, None).ok()?;
        extract_shell_item_icon_base64(&item, size)
    }
}

unsafe fn hbitmap_to_png_base64(hbm: HBITMAP) -> Option<String> {
    let mut bm = BITMAP::default();
    if GetObjectW(hbm, std::mem::size_of::<BITMAP>() as i32, Some(&mut bm as *mut _ as *mut _)) == 0 {
        return None;
    }
    let w = bm.bmWidth.clamp(1, 128);
    let h = bm.bmHeight.abs().clamp(1, 128);
    let hdc = CreateCompatibleDC(None);
    if hdc.0.is_null() {
        return None;
    }
    let mut bmi = BITMAPINFO {
        bmiHeader: BITMAPINFOHEADER {
            biSize: std::mem::size_of::<BITMAPINFOHEADER>() as u32,
            biWidth: w,
            biHeight: -h,
            biPlanes: 1,
            biBitCount: 32,
            biCompression: BI_RGB.0,
            ..Default::default()
        },
        ..Default::default()
    };
    let mut bgra = vec![0u8; (w * h * 4) as usize];
    let old = SelectObject(hdc, hbm);
    let lines = GetDIBits(hdc, hbm, 0, h as u32, Some(bgra.as_mut_ptr() as *mut _), &mut bmi, DIB_RGB_COLORS);
    SelectObject(hdc, old);
    let _ = DeleteDC(hdc);
    if lines <= 0 {
        return None;
    }
    encode_bgra_to_png_base64(w as u32, h as u32, &bgra, false)
}

/// Resolves bare executable names (e.g. `msedge.exe`, `powershell.exe`, `wt.exe`, `notepad.exe`)
/// to real filesystem paths using App Paths registry + Windows standard directories so icon extraction
/// never falls back to generic monogram squares!
fn resolve_executable_path(raw: &str) -> Option<String> {
    let trimmed = raw.trim().trim_matches('"');
    if trimmed.is_empty() {
        return None;
    }
    if Path::new(trimmed).exists() {
        return Some(trimmed.to_string());
    }
    // Check HKLM / HKCU App Paths registry (`msedge.exe`, `chrome.exe`, `firefox.exe`, `winword.exe`, etc.)
    let exe_name = if trimmed.to_ascii_lowercase().ends_with(".exe") {
        trimmed.to_string()
    } else {
        format!("{trimmed}.exe")
    };
    let subkey = format!(r"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exe_name}");
    for root in [windows::Win32::System::Registry::HKEY_LOCAL_MACHINE, HKEY_CURRENT_USER] {
        let mut hk = HKEY::default();
        let sub_w = HSTRING::from(&subkey);
        unsafe {
            if RegOpenKeyExW(root, PCWSTR(sub_w.as_ptr()), 0, KEY_READ, &mut hk).is_ok() {
                let mut buf = [0u16; 520];
                let mut sz = (buf.len() * 2) as u32;
                let ok = RegQueryValueExW(
                    hk,
                    PCWSTR::null(),
                    None,
                    None,
                    Some(buf.as_mut_ptr() as *mut u8),
                    Some(&mut sz),
                )
                .is_ok();
                let _ = RegCloseKey(hk);
                if ok && sz >= 2 {
                    let len = (sz as usize / 2).saturating_sub(1);
                    let p = String::from_utf16_lossy(&buf[..len])
                        .trim_end_matches('\0')
                        .trim()
                        .trim_matches('"')
                        .to_string();
                    if !p.is_empty() && Path::new(&p).exists() {
                        return Some(p);
                    }
                }
            }
        }
    }

    // Check standard Windows system & PowerShell & WindowsApps paths
    let win_dir = std::env::var_os("SystemRoot")
        .map(PathBuf::from)
        .unwrap_or_else(|| PathBuf::from(r"C:\Windows"));
    let local_app = std::env::var_os("LOCALAPPDATA").map(PathBuf::from);
    let mut dirs = vec![
        win_dir.clone(),
        win_dir.join("System32"),
        win_dir.join(r"System32\WindowsPowerShell\v1.0"),
        win_dir.join("SysWOW64"),
    ];
    if let Some(la) = local_app {
        dirs.push(la.join(r"Microsoft\WindowsApps"));
    }
    for d in dirs {
        let cand = d.join(&exe_name);
        if cand.exists() {
            return Some(cand.to_string_lossy().to_string());
        }
    }
    None
}

/// Extracts a crisp high-DPI PNG (base64) icon for any running HWND, executable path, `appsfolder:<AUMID>`,
/// or `ms-settings:` URI — guaranteed never to return a blank transparent image!
pub fn extract_icon_png_base64(hwnd_opt: Option<isize>, exe_path: &str) -> Option<String> {
    let trimmed = exe_path.trim();
    let lower = trimmed.to_ascii_lowercase();

    // 1. Direct UWP / MSIX / Store AppsFolder AUMID (`appsfolder:...`) or `ms-settings:`
    if let Some(aumid) = trimmed
        .strip_prefix("appsfolder:")
        .or_else(|| trimmed.strip_prefix("APPSFOLDER:"))
    {
        let shell_uri = format!(r"shell:AppsFolder\{}", aumid.trim());
        if let Some(b64) = extract_shell_path_icon_base64(&shell_uri, 48) {
            return Some(b64);
        }
    }
    if lower.starts_with("ms-settings:") {
        if let Some(b64) = extract_shell_path_icon_base64(
            r"shell:AppsFolder\windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel",
            48,
        ) {
            return Some(b64);
        }
    }
    if lower == "wt" || lower == "wt.exe" || lower.ends_with(r"\wt.exe") || lower.ends_with(r"\windowsterminal.exe") {
        if let Some(b64) = extract_shell_path_icon_base64(
            r"shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App",
            48,
        ) {
            return Some(b64);
        }
    }

    // 2. Resolve bare filenames (`msedge.exe`, `powershell.exe`, `notepad.exe`) to full real paths
    let resolved = resolve_executable_path(trimmed);
    let best_path = resolved.as_deref().unwrap_or(trimmed);

    // 3. For non-UWP-host binaries, try native Windows 11 48x48 IShellItemImageFactory first (sharpest!)
    let is_app_frame_host = best_path
        .to_ascii_lowercase()
        .ends_with(r"\applicationframehost.exe");
    if !best_path.is_empty() && !is_app_frame_host && Path::new(best_path).exists() {
        if let Some(b64) = extract_shell_path_icon_base64(best_path, 48) {
            return Some(b64);
        }
    }

    // 4. Fall back to live HWND icon (handles UWP ApplicationFrameHost & custom PWA window icons!)
    if let Some((w, h, premul)) = extract_icon_bgra(hwnd_opt, best_path) {
        if let Some(b64) = encode_bgra_to_png_base64(w, h, &premul, true) {
            return Some(b64);
        }
    }

    // 5. Final shell fallback on original string (never extract generic ApplicationFrameHost icon!)
    if !best_path.is_empty() && !is_app_frame_host {
        return extract_shell_path_icon_base64(best_path, 48);
    }
    None
}

pub fn encode_bgra_to_png_base64(w: u32, h: u32, bgra: &[u8], premultiplied: bool) -> Option<String> {
    if w == 0 || h == 0 || bgra.len() < (w * h * 4) as usize {
        return None;
    }
    let any_alpha = bgra.as_chunks::<4>().0.iter().any(|px| px[3] != 0);
    let mut rgba = Vec::with_capacity((w * h * 4) as usize);
    let mut visible_pixels = 0usize;
    for px in bgra.as_chunks::<4>().0 {
        let a = if !any_alpha {
            if px[0] != 0 || px[1] != 0 || px[2] != 0 { 255 } else { 0 }
        } else {
            px[3]
        };
        if a >= 16 {
            visible_pixels += 1;
        }
        if premultiplied && a > 0 && a < 255 {
            let inv = 255.0 / a as f32;
            let r = ((px[2] as f32) * inv).min(255.0) as u8;
            let g = ((px[1] as f32) * inv).min(255.0) as u8;
            let b = ((px[0] as f32) * inv).min(255.0) as u8;
            rgba.extend_from_slice(&[r, g, b, a]);
        } else {
            rgba.extend_from_slice(&[px[2], px[1], px[0], a]);
        }
    }
    // Never emit a completely transparent/blank PNG icon!
    if visible_pixels < 12 {
        return None;
    }
    let img = image::RgbaImage::from_raw(w, h, rgba)?;
    let mut out = Cursor::new(Vec::with_capacity(2048));
    img.write_to(&mut out, image::ImageFormat::Png).ok()?;
    Some(base64::engine::general_purpose::STANDARD.encode(out.into_inner()))
}

/// Full OmniSearch / Dock / Tray launcher supporting `appsfolder:<AUMID>`, `cmdrun:<cmd>`,
/// `reveal:<path>`, `exe|args`, `.lnk`, `.exe`, `ms-settings:`, `https://`, and folders!
pub fn launch_target(raw_target: &str) -> bool {
    let target = raw_target.trim();
    if target.is_empty() {
        return false;
    }

    // 1. UWP / MSIX / Store AppsFolder AUMID
    if let Some(app_id) = target.strip_prefix("appsfolder:").or_else(|| target.strip_prefix("APPSFOLDER:")) {
        let id = app_id.trim();
        if id.is_empty() {
            return false;
        }
        return std::process::Command::new("explorer.exe")
            .arg(format!("shell:AppsFolder\\{id}"))
            .spawn()
            .is_ok();
    }

    // 2. PowerShell quick command (`> ipconfig`, `> ping 1.1.1.1`)
    if let Some(cmd) = target.strip_prefix("cmdrun:") {
        let cmd = cmd.trim();
        if cmd.is_empty() {
            return false;
        }
        return std::process::Command::new("powershell.exe")
            .args(["-NoExit", "-Command", cmd])
            .spawn()
            .is_ok();
    }

    // 3. Reveal file in File Explorer (`reveal:<path>`)
    if let Some(path) = target.strip_prefix("reveal:") {
        let p = path.trim();
        if p.is_empty() {
            return false;
        }
        return std::process::Command::new("explorer.exe")
            .arg(format!("/select,\"{p}\""))
            .spawn()
            .is_ok();
    }

    // 4. Compound `exe|arguments`
    if let Some((exe, args)) = target.split_once('|') {
        let w_exe = HSTRING::from(exe.trim());
        let w_args = HSTRING::from(args.trim());
        let h = unsafe { ShellExecuteW(None, PCWSTR::null(), &w_exe, &w_args, PCWSTR::null(), SW_SHOWNORMAL) };
        return h.0 as isize > 32;
    }

    // 5. Standard ShellExecuteW (with working directory if file exists)
    let w_target = HSTRING::from(target);
    let w_dir = Path::new(target)
        .parent()
        .filter(|d| d.is_dir())
        .map(|d| HSTRING::from(d.to_string_lossy().as_ref()));
    let dir_ptr = w_dir.as_ref().map_or(PCWSTR::null(), |s| PCWSTR(s.as_ptr()));
    let h = unsafe { ShellExecuteW(None, PCWSTR::null(), &w_target, PCWSTR::null(), dir_ptr, SW_SHOWNORMAL) };
    h.0 as isize > 32
}

/// Native Windows COM Folder Picker (`IFileOpenDialog` + `FOS_PICKFOLDERS`) for `+ Klasör Ekle`!
pub fn pick_folder_dialog(title: &str) -> Option<String> {
    pick_shell_dialog(title, true)
}

/// Native Windows COM File Picker (`IFileOpenDialog`) for `+ Dosya Ekle` / `QR Paylaş`!
pub fn pick_file_dialog(title: &str) -> Option<String> {
    pick_shell_dialog(title, false)
}

fn pick_shell_dialog(title: &str, folder_only: bool) -> Option<String> {
    let title_owned = title.to_string();
    // Run on dedicated STA thread so COM dialog never clashes with WebView2 pump!
    std::thread::spawn(move || unsafe {
        let _ = CoInitializeEx(None, COINIT_APARTMENTTHREADED);
        let dialog: IFileOpenDialog = CoCreateInstance(&FileOpenDialog, None, CLSCTX_INPROC_SERVER).ok()?;
        let mut opts = dialog.GetOptions().unwrap_or_default();
        opts |= FOS_FORCEFILESYSTEM;
        if folder_only {
            opts |= FOS_PICKFOLDERS;
        }
        let _ = dialog.SetOptions(opts);
        let w_title = HSTRING::from(title_owned);
        let _ = dialog.SetTitle(&w_title);
        if dialog.Show(None).is_err() {
            return None;
        }
        let item = dialog.GetResult().ok()?;
        let pw = item.GetDisplayName(SIGDN_FILESYSPATH).ok()?;
        pwstr_to_string_and_free(pw)
    })
    .join()
    .ok()
    .flatten()
}

pub fn friendly_folder_name(path: &str) -> String {
    let p = Path::new(path);
    let name = p.file_name().and_then(|s| s.to_str()).unwrap_or(path);
    match name.to_ascii_lowercase().as_str() {
        "desktop" => "Masaüstü".into(),
        "downloads" => "İndirilenler".into(),
        "documents" => "Belgeler".into(),
        "pictures" => "Resimler".into(),
        "videos" => "Videolar".into(),
        "music" => "Müzik".into(),
        _ => name.to_string(),
    }
}

#[derive(Debug, Clone, serde::Serialize)]
#[serde(rename_all = "camelCase")]
pub struct FolderEntryDto {
    pub name: String,
    pub path: String,
    pub is_dir: bool,
    pub size_label: String,
    pub ext: String,
}

pub fn read_folder_stack_entries_full(folder_path: &str, limit: usize) -> Vec<FolderEntryDto> {
    let dir = Path::new(folder_path);
    let mut items = Vec::new();
    let Ok(rd) = std::fs::read_dir(dir) else {
        return items;
    };
    for ent in rd.flatten().take(96) {
        let p = ent.path();
        let name = ent.file_name().to_string_lossy().to_string();
        if name.starts_with('.') || name.eq_ignore_ascii_case("desktop.ini") || name.eq_ignore_ascii_case("thumbs.db") {
            continue;
        }
        let meta = ent.metadata().ok();
        let is_dir = meta.as_ref().is_some_and(|m| m.is_dir());
        let size_label = if is_dir {
            "Klasör".into()
        } else {
            format_size(meta.map(|m| m.len()).unwrap_or(0))
        };
        let ext = if is_dir {
            "dir".into()
        } else {
            p.extension()
                .and_then(|e| e.to_str())
                .unwrap_or("")
                .to_ascii_lowercase()
        };
        items.push(FolderEntryDto {
            name,
            path: p.to_string_lossy().to_string(),
            is_dir,
            size_label,
            ext,
        });
    }
    items.sort_by_key(|a| (!a.is_dir, a.name.to_ascii_lowercase()));
    items.truncate(limit.max(1));
    items
}

/// Checks whether Project Aurora is registered in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
pub fn is_startup_enabled() -> bool {
    unsafe {
        let sub = HSTRING::from(r"Software\Microsoft\Windows\CurrentVersion\Run");
        let val = HSTRING::from("ProjectAurora");
        let mut hkey = HKEY::default();
        if RegOpenKeyExW(HKEY_CURRENT_USER, &sub, 0, KEY_READ, &mut hkey).is_err() {
            return false;
        }
        let mut sz = 0u32;
        let ok = RegQueryValueExW(hkey, &val, None, None, None, Some(&mut sz)).is_ok() && sz > 2;
        let _ = RegCloseKey(hkey);
        ok
    }
}

/// Registers or removes Project Aurora from Windows User Startup cleanly (zero UAC).
pub fn set_startup_enabled(enabled: bool) -> bool {
    unsafe {
        let sub = HSTRING::from(r"Software\Microsoft\Windows\CurrentVersion\Run");
        let val = HSTRING::from("ProjectAurora");
        let mut hkey = HKEY::default();
        if RegCreateKeyExW(
            HKEY_CURRENT_USER,
            &sub,
            0,
            PCWSTR::null(),
            REG_OPTION_NON_VOLATILE,
            KEY_WRITE,
            None,
            &mut hkey,
            None,
        )
        .is_err()
        {
            return false;
        }
        let ok = if enabled {
            if let Ok(exe) = std::env::current_exe() {
                let cmd = format!("\"{}\"", exe.to_string_lossy());
                let wide: Vec<u16> = cmd.encode_utf16().chain(std::iter::once(0)).collect();
                let bytes = std::slice::from_raw_parts(wide.as_ptr() as *const u8, wide.len() * 2);
                RegSetValueExW(hkey, &val, 0, REG_SZ, Some(bytes)).is_ok()
            } else {
                false
            }
        } else {
            let _ = RegDeleteValueW(hkey, &val);
            true
        };
        let _ = RegCloseKey(hkey);
        ok
    }
}

/// Converts a Drop Shelf image file (`png` / `jpg` / `bmp`) next to the original file.
pub fn convert_image_file(src_path: &str, target_format: &str) -> Result<String, String> {
    let src = Path::new(src_path);
    if !src.is_file() {
        return Err("Dosya bulunamadı".into());
    }
    let img = image::open(src).map_err(|e| format!("Görsel açılamadı: {e}"))?;
    let (ext, fmt) = match target_format.to_ascii_lowercase().as_str() {
        "jpg" | "jpeg" => ("jpg", image::ImageFormat::Jpeg),
        "bmp" => ("bmp", image::ImageFormat::Bmp),
        _ => ("png", image::ImageFormat::Png),
    };
    let stem = src.file_stem().and_then(|s| s.to_str()).unwrap_or("converted");
    let parent = src.parent().unwrap_or_else(|| Path::new("."));
    let out = parent.join(format!("{stem}_aurora.{ext}"));
    if fmt == image::ImageFormat::Jpeg {
        img.to_rgb8().save_with_format(&out, fmt).map_err(|e| e.to_string())?;
    } else {
        img.save_with_format(&out, fmt).map_err(|e| e.to_string())?;
    }
    Ok(out.to_string_lossy().to_string())
}

/// Generates a crisp inline SVG markup for LAN QR AirDrop (matches `CrownScript`'s `qrShare.svg`).
pub fn render_qr_svg(modules: &[bool], size: usize) -> String {
    if size == 0 || modules.len() < size * size {
        return String::new();
    }
    let quiet = 2usize;
    let total = size + quiet * 2;
    let mut path = String::with_capacity(size * size * 8);
    for r in 0..size {
        for c in 0..size {
            if modules[r * size + c] {
                path.push_str(&format!("M{},{}h1v1h-1z", c + quiet, r + quiet));
            }
        }
    }
    format!(
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {total} {total}\" shape-rendering=\"crispEdges\"><rect width=\"{total}\" height=\"{total}\" fill=\"#FFFFFF\" rx=\"1\"/><path d=\"{path}\" fill=\"#0A0F1D\"/></svg>"
    )
}

pub fn open_recycle_bin() {
    let _ = launch_target("shell:RecycleBinFolder");
}

pub fn open_task_view() {
    send_key_combo(&[VK_LWIN, VK_TAB]);
}

pub fn create_virtual_desktop() {
    send_key_combo(&[VK_LWIN, VK_CONTROL, VIRTUAL_KEY(b'D' as u16)]);
}

pub fn send_ctrl_v_paste() {
    send_key_combo(&[VK_CONTROL, VIRTUAL_KEY(b'V' as u16)]);
}

fn send_key_combo(keys: &[VIRTUAL_KEY]) {
    let make = |vk: VIRTUAL_KEY, up: bool| INPUT {
        r#type: INPUT_KEYBOARD,
        Anonymous: INPUT_0 {
            ki: KEYBDINPUT {
                wVk: vk,
                dwFlags: if up { KEYEVENTF_KEYUP } else { Default::default() },
                ..Default::default()
            },
        },
    };
    let mut seq = Vec::with_capacity(keys.len() * 2);
    for &k in keys {
        seq.push(make(k, false));
    }
    for &k in keys.iter().rev() {
        seq.push(make(k, true));
    }
    unsafe {
        let _ = SendInput(&seq, std::mem::size_of::<INPUT>() as i32);
    }
}

/// Extracts an active progress percentage (`0..=100`) from window titles like `"Copying... 42%"`.
pub fn extract_title_progress_percent(title: &str) -> Option<u8> {
    let bytes = title.as_bytes();
    for (i, &b) in bytes.iter().enumerate() {
        if b == b'%' && i > 0 {
            let mut start = i;
            while start > 0 && bytes[start - 1].is_ascii_digit() {
                start -= 1;
            }
            if start < i {
                if let Ok(v) = title[start..i].parse::<u16>() {
                    if v <= 100 {
                        return Some(v as u8);
                    }
                }
            }
        }
    }
    None
}

/// Reads CF_DIB clipboard image if present and returns `(width, height, png_bytes, thumb_base64)`.
pub fn try_get_clipboard_image_png() -> Option<(i32, i32, Vec<u8>, String)> {
    const CF_DIB: u32 = 8;
    unsafe {
        if OpenClipboard(None).is_err() {
            return None;
        }
        let res = GetClipboardData(CF_DIB).ok().and_then(|h| {
            let hglob = HGLOBAL(h.0);
            let ptr = GlobalLock(hglob) as *const BITMAPINFOHEADER;
            if ptr.is_null() {
                return None;
            }
            let hdr = *ptr;
            let w = hdr.biWidth;
            let h_abs = hdr.biHeight.abs();
            let top_down = hdr.biHeight < 0;
            let bpp = hdr.biBitCount;
            let out = if w > 0 && w <= 4096 && h_abs > 0 && h_abs <= 4096 && (bpp == 24 || bpp == 32) {
                let header_bytes = hdr.biSize as usize
                    + if hdr.biCompression == 3 { 12 } else { 0 };
                let src = (ptr as *const u8).add(header_bytes);
                let stride = (w as usize * bpp as usize).div_ceil(32) * 4;
                let mut rgba = vec![0u8; (w as usize) * (h_abs as usize) * 4];
                for y in 0..(h_abs as usize) {
                    let sy = if top_down { y } else { (h_abs as usize) - 1 - y };
                    let row = src.add(sy * stride);
                    for x in 0..(w as usize) {
                        let dst_i = (y * (w as usize) + x) * 4;
                        let src_i = x * (bpp as usize / 8);
                        rgba[dst_i] = *row.add(src_i + 2);
                        rgba[dst_i + 1] = *row.add(src_i + 1);
                        rgba[dst_i + 2] = *row.add(src_i);
                        rgba[dst_i + 3] = 255;
                    }
                }
                image::RgbaImage::from_raw(w as u32, h_abs as u32, rgba).and_then(|img| {
                    let dyn_img = image::DynamicImage::ImageRgba8(img);
                    let mut full_png = Cursor::new(Vec::new());
                    dyn_img.write_to(&mut full_png, image::ImageFormat::Png).ok()?;
                    let thumb = dyn_img.thumbnail(140, 84);
                    let mut thumb_png = Cursor::new(Vec::new());
                    thumb.write_to(&mut thumb_png, image::ImageFormat::Png).ok()?;
                    let thumb_b64 = base64::engine::general_purpose::STANDARD.encode(thumb_png.into_inner());
                    Some((w, h_abs, full_png.into_inner(), thumb_b64))
                })
            } else {
                None
            };
            let _ = GlobalUnlock(hglob);
            out
        });
        let _ = CloseClipboard();
        res
    }
}

/// Ensures unique deduplicated folder list with friendly default fallback (`Documents`) if empty.
pub fn effective_dock_folders(configured: Option<&[String]>) -> Vec<String> {
    let mut out = Vec::new();
    let mut seen = HashSet::new();
    if let Some(paths) = configured {
        for p in paths {
            let trimmed = p.trim();
            if !trimmed.is_empty() && Path::new(trimmed).is_dir() && seen.insert(trimmed.to_ascii_lowercase()) {
                out.push(trimmed.to_string());
            }
        }
    }
    if out.is_empty() {
        if let Some(up) = std::env::var_os("USERPROFILE") {
            let docs = PathBuf::from(up).join("Documents");
            if docs.is_dir() {
                out.push(docs.to_string_lossy().to_string());
            }
        }
    }
    out.truncate(4);
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn catalog_contains_settings_tools_and_apps() {
        let _ = unsafe { CoInitializeEx(None, COINIT_APARTMENTTHREADED) };
        let items = build_full_installed_app_catalog(false);
        assert!(items.len() >= 48, "Expected at least 48 settings/tools/apps, got {}", items.len());
        assert!(items.iter().any(|a| a.kind == "setting"), "Missing Windows 11 Settings entries");
        assert!(items.iter().any(|a| a.kind == "tool"), "Missing Windows Admin Tools entries");
        assert!(items.iter().any(|a| a.kind == "app"), "Missing installed apps from AppsFolder/StartMenu");
    }

    #[test]
    fn title_progress_and_helpers_work() {
        assert_eq!(extract_title_progress_percent("Downloading 74% — Chrome"), Some(74));
        assert_eq!(extract_title_progress_percent("Project Aurora"), None);
        assert_eq!(friendly_folder_name(r"C:\Users\Demo\Documents"), "Belgeler");
        assert_eq!(friendly_folder_name(r"C:\Users\Demo\Downloads"), "İndirilenler");
        let svg = render_qr_svg(&[true, false, false, true], 2);
        assert!(svg.starts_with("<svg") && svg.contains("<rect"));
    }
}
