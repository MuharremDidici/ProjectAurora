use std::io::Write;
use std::net::UdpSocket;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use aurora_core::{InstalledAppEntry, LanQrShareInfo, PhysicalRect, WeatherSnapshot};
use qrcode::QrCode;
use windows::Graphics::Imaging::{BitmapPixelFormat, SoftwareBitmap};
use windows::Media::Ocr::OcrEngine;
use windows::Security::Cryptography::CryptographicBuffer;
use windows::Win32::Graphics::Gdi::{
    BitBlt, CreateCompatibleBitmap, CreateCompatibleDC, DeleteDC, DeleteObject, GetDC, GetDIBits,
    GetPixel, ReleaseDC, SelectObject, BITMAPINFO, BITMAPINFOHEADER, BI_RGB, DIB_RGB_COLORS,
    SRCCOPY,
};
use windows::Win32::System::DataExchange::{
    CloseClipboard, EmptyClipboard, GetClipboardData, OpenClipboard, SetClipboardData,
};
use windows::Win32::System::Memory::{GlobalAlloc, GlobalLock, GlobalUnlock, GMEM_MOVEABLE};

/// Built-in Windows Settings & System Tools + Start Menu `.lnk` scanner (instant <100ms catalog!).
pub fn build_installed_app_catalog() -> Vec<InstalledAppEntry> {
    let mut list = vec![
        app_entry("set-display", "Ekran ve Parlaklık", "ms-settings:display", "Ayarlar", "ekran cozunurluk parlaklik monitor", "setting"),
        app_entry("set-sound", "Ses Ayarları", "ms-settings:sound", "Ayarlar", "ses hoparlor kulaklik mikrofon", "setting"),
        app_entry("set-bluetooth", "Bluetooth ve Cihazlar", "ms-settings:bluetooth", "Ayarlar", "bluetooth kulaklik fare klavye", "setting"),
        app_entry("set-wifi", "Ağ ve Wi-Fi", "ms-settings:network-wifi", "Ayarlar", "wifi internet ag", "setting"),
        app_entry("set-power", "Güç ve Pil", "ms-settings:powersleep", "Ayarlar", "guc pil uyku enerji", "setting"),
        app_entry("set-apps", "Yüklü Uygulamalar", "ms-settings:appsfeatures", "Ayarlar", "uygulama kaldir program", "setting"),
        app_entry("set-update", "Windows Update", "ms-settings:windowsupdate", "Ayarlar", "guncelleme windows update", "setting"),
        app_entry("tool-calc", "Hesap Makinesi", "calc.exe", "Araçlar", "hesap makinesi calc", "tool"),
        app_entry("tool-snip", "Ekran Alıntısı", "ms-screenclip:", "Araçlar", "ekran goruntusu snipping", "tool"),
        app_entry("tool-wt", "Windows Terminal", "wt.exe", "Geliştirme", "terminal powershell cmd", "tool"),
        app_entry("tool-taskmgr", "Görev Yöneticisi", "taskmgr.exe", "Sistem", "gorev yoneticisi cpu ram", "tool"),
        app_entry("tool-explorer", "Dosya Gezgini", "explorer.exe", "Sistem", "dosya klasor explorer", "tool"),
        app_entry("tool-notepad", "Not Defteri", "notepad.exe", "Araçlar", "not defteri notepad", "tool"),
    ];

    let mut seen = std::collections::HashSet::new();
    for item in &list {
        seen.insert(item.name.to_ascii_lowercase());
    }

    let mut roots = Vec::new();
    if let Some(pd) = std::env::var_os("ProgramData") {
        roots.push(PathBuf::from(pd).join(r"Microsoft\Windows\Start Menu\Programs"));
    }
    if let Some(ad) = std::env::var_os("APPDATA") {
        roots.push(PathBuf::from(ad).join(r"Microsoft\Windows\Start Menu\Programs"));
    }

    for root in roots {
        scan_lnk_recursive(&root, 0, &mut seen, &mut list);
    }
    list.sort_by_key(|a| a.name.to_ascii_lowercase());
    list
}

fn app_entry(id: &str, name: &str, path: &str, cat: &str, kw: &str, kind: &str) -> InstalledAppEntry {
    InstalledAppEntry {
        id: id.into(),
        name: name.into(),
        launch_path: path.into(),
        category: cat.into(),
        keywords: kw.into(),
        kind: kind.into(),
        icon_base64: None,
    }
}

fn scan_lnk_recursive(dir: &Path, depth: usize, seen: &mut std::collections::HashSet<String>, out: &mut Vec<InstalledAppEntry>) {
    if depth > 3 || out.len() >= 320 {
        return;
    }
    let Ok(rd) = std::fs::read_dir(dir) else { return };
    for entry in rd.flatten() {
        let path = entry.path();
        if path.is_dir() {
            scan_lnk_recursive(&path, depth + 1, seen, out);
        } else if path.extension().and_then(|e| e.to_str()).is_some_and(|e| e.eq_ignore_ascii_case("lnk")) {
            let Some(stem) = path.file_stem().and_then(|s| s.to_str()) else { continue };
            let low = stem.to_ascii_lowercase();
            if low.contains("uninstall") || low.contains("kaldır") || low.contains("readme") || low.contains("help") {
                continue;
            }
            if !seen.insert(low.clone()) {
                continue;
            }
            let cat = aurora_core::classify_tidy_category(stem).label_tr().to_string();
            out.push(InstalledAppEntry {
                id: format!("lnk-{}", out.len()),
                name: stem.to_string(),
                launch_path: path.to_string_lossy().to_string(),
                category: cat,
                keywords: low,
                kind: "app".into(),
                icon_base64: None,
            });
        }
    }
}

/// Fast local user file index (Desktop, Documents, Downloads, Pictures).
pub fn search_local_files(query: &str, max_results: usize) -> Vec<(String, String)> {
    let q = aurora_core::normalize_tr(query);
    if q.len() < 2 {
        return vec![];
    }
    let Some(profile) = std::env::var_os("USERPROFILE").map(PathBuf::from) else {
        return vec![];
    };
    let mut hits = Vec::new();
    for folder in ["Desktop", "Documents", "Downloads", "Pictures"] {
        collect_files_matching(&profile.join(folder), &q, 0, &mut hits, max_results);
        if hits.len() >= max_results {
            break;
        }
    }
    hits
}

fn collect_files_matching(dir: &Path, q: &str, depth: usize, hits: &mut Vec<(String, String)>, max: usize) {
    if depth > 2 || hits.len() >= max {
        return;
    }
    let Ok(rd) = std::fs::read_dir(dir) else { return };
    for e in rd.flatten() {
        if hits.len() >= max {
            break;
        }
        let p = e.path();
        if let Some(name) = p.file_name().and_then(|n| n.to_str()) {
            if !name.starts_with('.') && aurora_core::normalize_tr(name).contains(q) {
                hits.push((name.to_string(), p.to_string_lossy().to_string()));
            }
        }
        if p.is_dir() && depth < 2 {
            collect_files_matching(&p, q, depth + 1, hits, max);
        }
    }
}

/// Captures a screen rectangle directly as BGRA8 pixels via GDI `BitBlt`.
pub fn capture_screen_region_bgra(rect: PhysicalRect) -> Option<Vec<u8>> {
    if rect.width <= 0 || rect.height <= 0 {
        return None;
    }
    let w = rect.width;
    let h = rect.height;
    unsafe {
        let h_screen = GetDC(None);
        let h_mem = CreateCompatibleDC(h_screen);
        let h_bmp = CreateCompatibleBitmap(h_screen, w, h);
        let old = SelectObject(h_mem, h_bmp);
        let ok = BitBlt(h_mem, 0, 0, w, h, h_screen, rect.x, rect.y, SRCCOPY).is_ok();
        let mut bgra = vec![0u8; (w * h * 4) as usize];
        if ok {
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
            let _ = GetDIBits(h_mem, h_bmp, 0, h as u32, Some(bgra.as_mut_ptr() as *mut _), &mut bmi, DIB_RGB_COLORS);
            for px in bgra.as_chunks_mut::<4>().0 {
                px[3] = 255;
            }
        }
        SelectObject(h_mem, old);
        let _ = DeleteObject(h_bmp);
        let _ = DeleteDC(h_mem);
        ReleaseDC(None, h_screen);
        if ok { Some(bgra) } else { None }
    }
}

/// Samples exact RGB hex at physical cursor coordinate for Eyedropper tool.
pub fn sample_screen_pixel_hex(x: i32, y: i32) -> (u8, u8, u8, String) {
    unsafe {
        let hdc = GetDC(None);
        let c = GetPixel(hdc, x, y).0;
        ReleaseDC(None, hdc);
        let r = (c & 0xFF) as u8;
        let g = ((c >> 8) & 0xFF) as u8;
        let b = ((c >> 16) & 0xFF) as u8;
        (r, g, b, format!("#{r:02X}{g:02X}{b:02X}"))
    }
}

/// Native Offline Windows 11 WinRT OCR (`Windows.Media.Ocr`) on any screen region!
pub fn recognize_screen_region_ocr(rect: PhysicalRect) -> Option<String> {
    let bgra = capture_screen_region_bgra(rect)?;
    let engine = OcrEngine::TryCreateFromUserProfileLanguages().ok()?;
    let ibuf = CryptographicBuffer::CreateFromByteArray(&bgra).ok()?;
    let sb = SoftwareBitmap::CreateCopyFromBuffer(&ibuf, BitmapPixelFormat::Bgra8, rect.width, rect.height).ok()?;
    let res = engine.RecognizeAsync(&sb).ok()?.get().ok()?;
    let text = res.Text().ok()?.to_string();
    let trimmed = text.trim();
    if trimmed.is_empty() { None } else { Some(trimmed.to_string()) }
}

/// Native 10 FPS Bayer Animated GIF Recorder (zero external ffmpeg/deps!).
pub fn record_gif_region(rect: PhysicalRect, max_seconds: u32, out_path: &Path, stop_flag: Arc<AtomicBool>) -> anyhow::Result<(u32, u64)> {
    let w = (rect.width as u16).min(1920);
    let h = (rect.height as u16).min(1080);
    let mut file = std::fs::File::create(out_path)?;
    let mut encoder = gif::Encoder::new(&mut file, w, h, &[])?;
    encoder.set_repeat(gif::Repeat::Infinite)?;

    let mut frames = 0u32;
    let max_frames = max_seconds.clamp(1, 30) * 10;
    while frames < max_frames && !stop_flag.load(Ordering::Relaxed) {
        let t0 = std::time::Instant::now();
        if let Some(bgra) = capture_screen_region_bgra(PhysicalRect::new(rect.x, rect.y, w as i32, h as i32)) {
            let mut rgba = Vec::with_capacity((w as usize) * (h as usize) * 4);
            for px in bgra.as_chunks::<4>().0 {
                rgba.extend_from_slice(&[px[2], px[1], px[0], 255]);
            }
            let mut frame = gif::Frame::from_rgba_speed(w, h, &mut rgba, 12);
            frame.delay = 10; // 100ms = 10 FPS
            encoder.write_frame(&frame)?;
            frames += 1;
        }
        let elapsed = t0.elapsed();
        if elapsed < std::time::Duration::from_millis(100) {
            std::thread::sleep(std::time::Duration::from_millis(100) - elapsed);
        }
    }
    drop(encoder);
    let bytes = std::fs::metadata(out_path).map(|m| m.len()).unwrap_or(0);
    Ok((frames / 10, bytes))
}

/// Zero-UAC Local LAN QR AirDrop Server (`tiny_http` + vector QR matrix).
pub struct LanQrShareServer {
    stop: Arc<AtomicBool>,
    pub active: Option<LanQrShareInfo>,
}

impl Default for LanQrShareServer {
    fn default() -> Self {
        Self::new()
    }
}

impl LanQrShareServer {
    pub fn new() -> Self {
        Self {
            stop: Arc::new(AtomicBool::new(false)),
            active: None,
        }
    }

    pub fn stop(&mut self) {
        self.stop.store(true, Ordering::Relaxed);
        self.active = None;
    }

    pub fn start_sharing(&mut self, file_path: &Path) -> anyhow::Result<LanQrShareInfo> {
        self.stop();
        let meta = std::fs::metadata(file_path)?;
        let fname = file_path
            .file_name()
            .and_then(|n| n.to_str())
            .unwrap_or("dosya")
            .to_string();
        let size_label = format_size(meta.len());
        let lan_ip = detect_lan_ipv4().unwrap_or_else(|| "127.0.0.1".into());
        let server = tiny_http::Server::http("0.0.0.0:0").map_err(|e| anyhow::anyhow!("{e}"))?;
        let port = server.server_addr().to_ip().map(|a| a.port()).unwrap_or(8787);
        let token = format!("{:x}", std::time::SystemTime::now().duration_since(std::time::UNIX_EPOCH).unwrap_or_default().as_millis() & 0xFFFF_FFFF);
        let url = format!("http://{lan_ip}:{port}/dl/{token}");

        let qr = QrCode::new(url.as_bytes())?;
        let matrix_size = qr.width();
        let modules: Vec<bool> = qr
            .to_colors()
            .into_iter()
            .map(|c| matches!(c, qrcode::Color::Dark))
            .collect();

        let stop = Arc::new(AtomicBool::new(false));
        self.stop = Arc::clone(&stop);
        let path_owned = file_path.to_path_buf();
        let fname_clone = fname.clone();
        let token_path = format!("/dl/{token}");

        std::thread::Builder::new()
            .name("aurora-qr-airdrop".into())
            .spawn(move || {
                let deadline = std::time::Instant::now() + std::time::Duration::from_secs(300);
                while !stop.load(Ordering::Relaxed) && std::time::Instant::now() < deadline {
                    if let Ok(Some(req)) = server.recv_timeout(std::time::Duration::from_millis(250)) {
                        if req.url() == token_path {
                            if let Ok(data) = std::fs::read(&path_owned) {
                                let hdr = tiny_http::Header::from_bytes(
                                    b"Content-Disposition",
                                    format!("attachment; filename=\"{fname_clone}\""),
                                )
                                .unwrap();
                                let resp = tiny_http::Response::from_data(data).with_header(hdr);
                                let _ = req.respond(resp);
                            }
                        } else {
                            let _ = req.respond(tiny_http::Response::from_string("Aurora QR AirDrop"));
                        }
                    }
                }
            })
            .ok();

        let info = LanQrShareInfo {
            url,
            file_name: fname,
            size_label,
            matrix_size,
            modules,
        };
        self.active = Some(info.clone());
        Ok(info)
    }
}

pub fn zip_shelf_files(paths: &[String], out_zip: &Path) -> anyhow::Result<u64> {
    let file = std::fs::File::create(out_zip)?;
    let mut zip = zip::ZipWriter::new(file);
    let opts = zip::write::SimpleFileOptions::default().compression_method(zip::CompressionMethod::Deflated);
    for p in paths {
        let path = Path::new(p);
        if path.is_file() {
            if let Ok(bytes) = std::fs::read(path) {
                let name = path.file_name().and_then(|n| n.to_str()).unwrap_or("dosya");
                zip.start_file(name, opts)?;
                zip.write_all(&bytes)?;
            }
        }
    }
    zip.finish()?;
    Ok(std::fs::metadata(out_zip).map(|m| m.len()).unwrap_or(0))
}

pub fn set_clipboard_text(text: &str) -> bool {
    let wide: Vec<u16> = text.encode_utf16().chain(std::iter::once(0)).collect();
    let byte_len = wide.len() * 2;
    unsafe {
        if OpenClipboard(None).is_err() {
            return false;
        }
        let _ = EmptyClipboard();
        let Ok(hmem) = GlobalAlloc(GMEM_MOVEABLE, byte_len) else {
            let _ = CloseClipboard();
            return false;
        };
        let ptr = GlobalLock(hmem) as *mut u16;
        if !ptr.is_null() {
            std::ptr::copy_nonoverlapping(wide.as_ptr(), ptr, wide.len());
            let _ = GlobalUnlock(hmem);
            let _ = SetClipboardData(13 /* CF_UNICODETEXT */, windows::Win32::Foundation::HANDLE(hmem.0));
        }
        let _ = CloseClipboard();
        true
    }
}

pub fn get_clipboard_text() -> Option<String> {
    unsafe {
        if OpenClipboard(None).is_err() {
            return None;
        }
        let res = GetClipboardData(13 /* CF_UNICODETEXT */).ok().and_then(|h| {
            let ptr = GlobalLock(windows::Win32::Foundation::HGLOBAL(h.0)) as *const u16;
            if ptr.is_null() {
                return None;
            }
            let mut len = 0usize;
            while len < 64_000 && *ptr.add(len) != 0 {
                len += 1;
            }
            let s = String::from_utf16_lossy(std::slice::from_raw_parts(ptr, len));
            let _ = GlobalUnlock(windows::Win32::Foundation::HGLOBAL(h.0));
            Some(s)
        });
        let _ = CloseClipboard();
        res
    }
}

pub fn fetch_live_weather() -> WeatherSnapshot {
    let url = "https://api.open-meteo.com/v1/forecast?latitude=41.01&longitude=28.97&current=temperature_2m,weather_code";
    let Ok(resp) = ureq::get(url).timeout(std::time::Duration::from_secs(6)).call() else {
        return WeatherSnapshot::default();
    };
    let Ok(json) = resp.into_json::<serde_json::Value>() else {
        return WeatherSnapshot::default();
    };
    let temp = json["current"]["temperature_2m"].as_f64().unwrap_or(18.0) as f32;
    let code = json["current"]["weather_code"].as_i64().unwrap_or(0);
    let cond = match code {
        0 => "Açık",
        1..=3 => "Parçalı Bulutlu",
        45 | 48 => "Sisli",
        51..=67 => "Yağmurlu",
        71..=77 => "Karlı",
        80..=82 => "Sağanak",
        95..=99 => "Fırtına",
        _ => "Bulutlu",
    };
    WeatherSnapshot {
        city: "İstanbul".into(),
        temp_c: temp,
        condition_tr: cond.into(),
        valid: true,
    }
}

fn detect_lan_ipv4() -> Option<String> {
    let sock = UdpSocket::bind("0.0.0.0:0").ok()?;
    sock.connect("8.8.8.8:80").ok()?;
    Some(sock.local_addr().ok()?.ip().to_string())
}

pub fn format_size(bytes: u64) -> String {
    if bytes >= 1024 * 1024 {
        format!("{:.1} MB", bytes as f64 / (1024.0 * 1024.0))
    } else if bytes >= 1024 {
        format!("{:.1} KB", bytes as f64 / 1024.0)
    } else {
        format!("{bytes} B")
    }
}
