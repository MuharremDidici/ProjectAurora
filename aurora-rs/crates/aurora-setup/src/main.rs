#![windows_subsystem = "windows"]

use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicBool, AtomicU8, Ordering};
use std::sync::Arc;

use aurora_core::{theme_by_id, Color, AURORA_VERSION};
use aurora_render::{CompositionSurface, FontFamily, GpuContext, IconKind, Painter, RectF, TextAlign};
use aurora_win32::{enable_per_monitor_dpi_v2, restore_taskbar_standalone};
use parking_lot::Mutex;
use windows::core::{w, Interface, HSTRING, PCWSTR};
use windows::Win32::Foundation::{CloseHandle, HWND, LPARAM, LRESULT, WPARAM};
use windows::Win32::System::Com::{
    CoCreateInstance, CoInitializeEx, IPersistFile, CLSCTX_INPROC_SERVER, COINIT_APARTMENTTHREADED,
};
use windows::Win32::System::Diagnostics::ToolHelp::{
    CreateToolhelp32Snapshot, Process32FirstW, Process32NextW, PROCESSENTRY32W, TH32CS_SNAPPROCESS,
};
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::System::Registry::{
    RegCreateKeyExW, RegDeleteTreeW, RegDeleteValueW, RegOpenKeyExW, RegSetValueExW, HKEY,
    HKEY_CURRENT_USER, KEY_SET_VALUE, REG_DWORD, REG_OPTION_NON_VOLATILE, REG_SZ,
};
use windows::Win32::System::Threading::{
    OpenProcess, TerminateProcess, WaitForSingleObject, PROCESS_SYNCHRONIZE, PROCESS_TERMINATE,
};
use windows::Win32::UI::Shell::{IShellLinkW, SHChangeNotify, ShellLink, SHCNE_ASSOCCHANGED, SHCNF_IDLIST};
use windows::Win32::UI::WindowsAndMessaging::{
    CreateWindowExW, DefWindowProcW, DispatchMessageW, GetSystemMetrics, LoadCursorW,
    PeekMessageW, PostQuitMessage, RegisterClassW, SetWindowLongPtrW, SetWindowPos,
    TranslateMessage, GWLP_USERDATA, HWND_TOP, IDC_ARROW, MSG, PM_REMOVE, SM_CXSCREEN,
    SM_CYSCREEN, SWP_SHOWWINDOW, WM_DESTROY, WM_LBUTTONDOWN, WM_MOUSEMOVE, WNDCLASSW,
    WS_EX_NOREDIRECTIONBITMAP, WS_POPUP,
};

static EMBEDDED_ZST: &[u8] = include_bytes!(concat!(env!("OUT_DIR"), "/aurora.exe.zst"));

#[derive(Clone, Copy, PartialEq, Eq)]
enum WizardStage {
    Ready,
    Installing,
    Done,
    Error,
}

struct WizardState {
    stage: WizardStage,
    auto_start: bool,
    desktop_shortcut: bool,
    progress_pct: u8,
    status_line: String,
    hover_btn: Option<&'static str>,
    close_requested: bool,
}

fn parse_wait_pid(args: &[String]) -> Option<u32> {
    for (i, arg) in args.iter().enumerate() {
        let lower = arg.to_ascii_lowercase();
        if let Some(rest) = lower.strip_prefix("--wait-pid=") {
            if let Ok(pid) = rest.trim().parse::<u32>() {
                return Some(pid);
            }
        }
        if lower == "--wait-pid" {
            if let Some(next) = args.get(i + 1) {
                if let Ok(pid) = next.trim().parse::<u32>() {
                    return Some(pid);
                }
            }
        }
    }
    None
}

fn wait_for_pid_exit(pid: u32, timeout_ms: u32) {
    if pid == 0 || pid == std::process::id() {
        return;
    }
    unsafe {
        if let Ok(hp) = OpenProcess(PROCESS_SYNCHRONIZE, false, pid) {
            let _ = WaitForSingleObject(hp, timeout_ms);
            let _ = CloseHandle(hp);
        }
    }
}

fn is_silent_or_update_mode(args: &[String]) -> bool {
    args.iter().any(|a| {
        a.eq_ignore_ascii_case("--silent")
            || a.eq_ignore_ascii_case("/s")
            || a.eq_ignore_ascii_case("--update")
            || a.eq_ignore_ascii_case("/update")
    })
}

fn main() {
    enable_per_monitor_dpi_v2();
    let _ = unsafe { CoInitializeEx(None, COINIT_APARTMENTTHREADED) };

    let args: Vec<String> = std::env::args().collect();
    if let Some(pid) = parse_wait_pid(&args) {
        wait_for_pid_exit(pid, 5000);
    }
    if args.iter().any(|a| a.eq_ignore_ascii_case("--uninstall")) {
        let _ = perform_uninstall();
        return;
    }
    if is_silent_or_update_mode(&args) {
        if perform_install(true, true, |_| {}).is_err() {
            std::process::exit(1);
        }
        if !args.iter().any(|a| a.eq_ignore_ascii_case("--no-launch")) {
            launch_installed_aurora();
        }
        return;
    }

    let _ = run_installer_gui();
}

fn install_dir() -> PathBuf {
    let local = std::env::var_os("LOCALAPPDATA")
        .map(PathBuf::from)
        .unwrap_or_else(|| PathBuf::from("."));
    local.join("Programs").join("ProjectAurora")
}

fn desktop_shortcut_path() -> PathBuf {
    let prof = std::env::var_os("USERPROFILE")
        .map(PathBuf::from)
        .unwrap_or_else(|| PathBuf::from("."));
    prof.join("Desktop").join("Project Aurora.lnk")
}

fn start_menu_shortcut_path() -> PathBuf {
    let appdata = std::env::var_os("APPDATA")
        .map(PathBuf::from)
        .unwrap_or_else(|| PathBuf::from("."));
    appdata
        .join("Microsoft")
        .join("Windows")
        .join("Start Menu")
        .join("Programs")
        .join("Project Aurora.lnk")
}

fn stop_running_aurora_processes() {
    let own_pid = std::process::id();
    unsafe {
        let Ok(snap) = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0) else { return };
        let mut pe = PROCESSENTRY32W {
            dwSize: std::mem::size_of::<PROCESSENTRY32W>() as u32,
            ..Default::default()
        };
        if Process32FirstW(snap, &mut pe).is_ok() {
            loop {
                let len = pe.szExeFile.iter().position(|&c| c == 0).unwrap_or(pe.szExeFile.len());
                let name = String::from_utf16_lossy(&pe.szExeFile[..len]).to_ascii_lowercase();
                if pe.th32ProcessID != own_pid
                    && (name == "aurora.exe" || name == "aurora.app.exe")
                {
                    if let Ok(hp) = OpenProcess(PROCESS_TERMINATE, false, pe.th32ProcessID) {
                        let _ = TerminateProcess(hp, 0);
                        let _ = windows::Win32::Foundation::CloseHandle(hp);
                    }
                }
                if Process32NextW(snap, &mut pe).is_err() {
                    break;
                }
            }
        }
        let _ = windows::Win32::Foundation::CloseHandle(snap);
    }
    std::thread::sleep(std::time::Duration::from_millis(350));
}

fn write_binary_with_retry(target_exe: &Path, bytes: &[u8]) -> anyhow::Result<()> {
    let mut last_err = None;
    for attempt in 0..12 {
        match std::fs::write(target_exe, bytes) {
            Ok(()) => return Ok(()),
            Err(e) => {
                last_err = Some(e);
                if attempt == 2 || attempt == 6 {
                    stop_running_aurora_processes();
                }
                // Try rename-aside strategy for locked running binaries on Windows
                let old_tmp = target_exe.with_extension("exe.old");
                let _ = std::fs::remove_file(&old_tmp);
                if std::fs::rename(target_exe, &old_tmp).is_ok()
                    && std::fs::write(target_exe, bytes).is_ok()
                {
                    let _ = std::fs::remove_file(&old_tmp);
                    return Ok(());
                }
                std::thread::sleep(std::time::Duration::from_millis(250));
            }
        }
    }
    Err(anyhow::anyhow!(
        "aurora.exe yazılamadı (dosya kilitli): {:?}",
        last_err
    ))
}

fn perform_install<F: Fn(u8)>(auto_start: bool, desktop_lnk: bool, on_progress: F) -> anyhow::Result<PathBuf> {
    on_progress(10);
    stop_running_aurora_processes();
    restore_taskbar_standalone();
    on_progress(25);

    let dir = install_dir();
    std::fs::create_dir_all(&dir)?;

    // Clean legacy v2.x .NET/WebView2 binaries in Programs\ProjectAurora (user data in LocalAppData\ProjectAurora\data is untouched!)
    if let Ok(entries) = std::fs::read_dir(&dir) {
        for ent in entries.flatten() {
            let p = ent.path();
            let name = ent.file_name().to_string_lossy().to_ascii_lowercase();
            if name == "aurora.exe" || name == "uninstall.exe" {
                continue;
            }
            if p.is_dir() {
                let _ = std::fs::remove_dir_all(&p);
            } else {
                let _ = std::fs::remove_file(&p);
            }
        }
    }

    let target_exe = dir.join("aurora.exe");

    if !EMBEDDED_ZST.is_empty() {
        let decompressed = zstd::stream::decode_all(EMBEDDED_ZST)?;
        write_binary_with_retry(&target_exe, &decompressed)?;
    } else {
        // Fallback when running dev installer beside target/release/aurora.exe
        let beside = std::env::current_exe()
            .ok()
            .and_then(|p| p.parent().map(|d| d.join("aurora.exe")));
        if let Some(src) = beside.filter(|p| p.exists()) {
            let raw = std::fs::read(src)?;
            write_binary_with_retry(&target_exe, &raw)?;
        } else {
            anyhow::bail!("Gömülü aurora.exe paketi bulunamadı.");
        }
    }
    on_progress(62);

    // Copy uninstaller itself
    if let Ok(self_exe) = std::env::current_exe() {
        let uninst = dir.join("uninstall.exe");
        if self_exe != uninst {
            let _ = std::fs::copy(&self_exe, &uninst);
        }
    }
    on_progress(75);

    // Write standalone emergency taskbar restore helper cmd
    let restore_cmd = dir.join("Windows-Gorev-Cubugunu-Geri-Getir.cmd");
    let _ = std::fs::write(
        &restore_cmd,
        "@echo off\r\nchcp 65001 >nul\r\n\"%~dp0aurora.exe\" --restore-taskbar\r\necho Windows gorev cubugu geri yuklendi.\r\n",
    );

    // Create Start Menu & optional Desktop shortcuts via native COM IShellLinkW
    let _ = create_shell_shortcut(&start_menu_shortcut_path(), &target_exe, "Project Aurora — Native Windows 11 Kabuğu");
    if desktop_lnk {
        let _ = create_shell_shortcut(&desktop_shortcut_path(), &target_exe, "Project Aurora");
    }
    on_progress(88);

    // Write HKCU Uninstall + Auto-Start registry entries (ZERO UAC!)
    write_hkcu_registry(&dir, &target_exe, auto_start);
    unsafe {
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, None, None);
    }
    on_progress(100);
    Ok(target_exe)
}

fn perform_uninstall() -> anyhow::Result<()> {
    stop_running_aurora_processes();
    restore_taskbar_standalone();
    let _ = std::fs::remove_file(desktop_shortcut_path());
    let _ = std::fs::remove_file(start_menu_shortcut_path());

    unsafe {
        let mut hkey = HKEY::default();
        if RegOpenKeyExW(
            HKEY_CURRENT_USER,
            w!("Software\\Microsoft\\Windows\\CurrentVersion\\Run"),
            0,
            KEY_SET_VALUE,
            &mut hkey,
        )
        .is_ok()
        {
            let _ = RegDeleteValueW(hkey, w!("ProjectAurora"));
        }
        let _ = RegDeleteTreeW(
            HKEY_CURRENT_USER,
            w!("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\ProjectAurora"),
        );
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, None, None);
    }

    let dir = install_dir();
    let _ = std::fs::remove_file(dir.join("aurora.exe"));
    Ok(())
}

fn create_shell_shortcut(lnk_path: &Path, target_exe: &Path, desc: &str) -> anyhow::Result<()> {
    if let Some(p) = lnk_path.parent() {
        std::fs::create_dir_all(p)?;
    }
    unsafe {
        let link: IShellLinkW = CoCreateInstance(&ShellLink, None, CLSCTX_INPROC_SERVER)?;
        let exe_h = HSTRING::from(target_exe.to_string_lossy().as_ref());
        link.SetPath(&exe_h)?;
        if let Some(dir) = target_exe.parent() {
            let wd_h = HSTRING::from(dir.to_string_lossy().as_ref());
            link.SetWorkingDirectory(&wd_h)?;
        }
        let desc_h = HSTRING::from(desc);
        link.SetDescription(&desc_h)?;
        link.SetIconLocation(&exe_h, 0)?;

        let pf: IPersistFile = link.cast()?;
        let lnk_h = HSTRING::from(lnk_path.to_string_lossy().as_ref());
        pf.Save(PCWSTR(lnk_h.as_ptr()), true)?;
    }
    Ok(())
}

fn set_reg_sz(hkey: HKEY, name: &str, value: &str) {
    let n_w: Vec<u16> = name.encode_utf16().chain(std::iter::once(0)).collect();
    let v_w: Vec<u16> = value.encode_utf16().chain(std::iter::once(0)).collect();
    let bytes: &[u8] = unsafe { std::slice::from_raw_parts(v_w.as_ptr() as *const u8, v_w.len() * 2) };
    unsafe {
        let _ = RegSetValueExW(hkey, PCWSTR(n_w.as_ptr()), 0, REG_SZ, Some(bytes));
    }
}

fn write_hkcu_registry(dir: &Path, target_exe: &Path, auto_start: bool) {
    let exe_str = target_exe.to_string_lossy().to_string();
    let uninst_cmd = format!("\"{}\" --uninstall", dir.join("uninstall.exe").to_string_lossy());
    unsafe {
        let mut hkey = HKEY::default();
        if RegCreateKeyExW(
            HKEY_CURRENT_USER,
            w!("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\ProjectAurora"),
            0,
            None,
            REG_OPTION_NON_VOLATILE,
            KEY_SET_VALUE,
            None,
            &mut hkey,
            None,
        )
        .is_ok()
        {
            set_reg_sz(hkey, "DisplayName", "Project Aurora");
            set_reg_sz(hkey, "DisplayVersion", AURORA_VERSION);
            set_reg_sz(hkey, "Publisher", "Muharrem Didici");
            set_reg_sz(hkey, "InstallLocation", &dir.to_string_lossy());
            set_reg_sz(hkey, "DisplayIcon", &format!("{exe_str},0"));
            set_reg_sz(hkey, "UninstallString", &uninst_cmd);
            let kb: u32 = 8192;
            let _ = RegSetValueExW(hkey, w!("EstimatedSize"), 0, REG_DWORD, Some(&kb.to_le_bytes()));
            let one: u32 = 1;
            let _ = RegSetValueExW(hkey, w!("NoModify"), 0, REG_DWORD, Some(&one.to_le_bytes()));
            let _ = RegSetValueExW(hkey, w!("NoRepair"), 0, REG_DWORD, Some(&one.to_le_bytes()));
        }

        let mut run_key = HKEY::default();
        if RegCreateKeyExW(
            HKEY_CURRENT_USER,
            w!("Software\\Microsoft\\Windows\\CurrentVersion\\Run"),
            0,
            None,
            REG_OPTION_NON_VOLATILE,
            KEY_SET_VALUE,
            None,
            &mut run_key,
            None,
        )
        .is_ok()
        {
            if auto_start {
                set_reg_sz(run_key, "ProjectAurora", &format!("\"{exe_str}\""));
            } else {
                let _ = RegDeleteValueW(run_key, w!("ProjectAurora"));
            }
        }
    }
}

fn launch_installed_aurora() {
    use std::os::windows::process::CommandExt;
    use std::process::Stdio;
    const DETACHED_PROCESS: u32 = 0x0000_0008;
    const CREATE_NEW_PROCESS_GROUP: u32 = 0x0000_0200;
    let exe = install_dir().join("aurora.exe");
    if exe.exists() {
        let _ = std::process::Command::new(exe)
            .stdin(Stdio::null())
            .stdout(Stdio::null())
            .stderr(Stdio::null())
            .creation_flags(DETACHED_PROCESS | CREATE_NEW_PROCESS_GROUP)
            .spawn();
    }
}

struct GuiClickEvent {
    mx: f32,
    my: f32,
    clicked: bool,
}

fn run_installer_gui() -> anyhow::Result<()> {
    let gpu = GpuContext::new()?;
    let w = 540i32;
    let h = 360i32;
    let (sx, sy) = unsafe { (GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN)) };
    let x = (sx - w) / 2;
    let y = (sy - h) / 2;

    let events = Arc::new(Mutex::new(Vec::<GuiClickEvent>::new()));
    let ev_box = Box::into_raw(Box::new(events.clone()));

    let hwnd = unsafe {
        let hinst = GetModuleHandleW(None)?;
        let cls = w!("AuroraSetupWizardV3");
        let wc = WNDCLASSW {
            lpfnWndProc: Some(setup_wnd_proc),
            hInstance: hinst.into(),
            hCursor: LoadCursorW(None, IDC_ARROW).unwrap_or_default(),
            lpszClassName: cls,
            ..Default::default()
        };
        let _ = RegisterClassW(&wc);
        let title_h = HSTRING::from(format!("Project Aurora v{AURORA_VERSION} Kurulum"));
        let hwnd = CreateWindowExW(
            WS_EX_NOREDIRECTIONBITMAP,
            cls,
            PCWSTR(title_h.as_ptr()),
            WS_POPUP,
            x,
            y,
            w,
            h,
            None,
            None,
            hinst,
            None,
        )?;
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, ev_box as isize);
        let _ = SetWindowPos(hwnd, HWND_TOP, x, y, w, h, SWP_SHOWWINDOW);
        hwnd
    };

    let mut surface = CompositionSurface::new(&gpu, hwnd, w as u32, h as u32, 96.0)?;
    let theme = theme_by_id("aurora-glass-dark").resolved();

    let mut state = WizardState {
        stage: WizardStage::Ready,
        auto_start: true,
        desktop_shortcut: true,
        progress_pct: 0,
        status_line: "Saf Rust Motoru • Paylaşımlı Ultra-Hafif Arayüz • Sıfır UAC".into(),
        hover_btn: None,
        close_requested: false,
    };

    let progress_atomic = Arc::new(AtomicU8::new(0));
    let done_atomic = Arc::new(AtomicBool::new(false));
    let err_msg: Arc<Mutex<Option<String>>> = Arc::new(Mutex::new(None));

    let btn_close = RectF::new(w as f32 - 42.0, 14.0, 28.0, 28.0);
    let chk_start = RectF::new(28.0, 186.0, 484.0, 32.0);
    let chk_desk = RectF::new(28.0, 224.0, 484.0, 32.0);
    let btn_primary = RectF::new(w as f32 - 196.0, h as f32 - 58.0, 168.0, 38.0);

    let mut dirty = true;
    while !state.close_requested {
        unsafe {
            let mut msg = MSG::default();
            while PeekMessageW(&mut msg, None, 0, 0, PM_REMOVE).as_bool() {
                if msg.message == 0x0012 {
                    state.close_requested = true;
                }
                let _ = TranslateMessage(&msg);
                DispatchMessageW(&msg);
            }
        }

        let drained: Vec<GuiClickEvent> = {
            let mut g = events.lock();
            std::mem::take(&mut *g)
        };
        for ev in drained {
            let next_hov = if btn_close.contains(ev.mx, ev.my) {
                Some("close")
            } else if btn_primary.contains(ev.mx, ev.my) {
                Some("primary")
            } else if state.stage == WizardStage::Ready && chk_start.contains(ev.mx, ev.my) {
                Some("start")
            } else if state.stage == WizardStage::Ready && chk_desk.contains(ev.mx, ev.my) {
                Some("desk")
            } else {
                None
            };
            if next_hov != state.hover_btn {
                state.hover_btn = next_hov;
                dirty = true;
            }
            if ev.clicked {
                match next_hov {
                    Some("close") => state.close_requested = true,
                    Some("start") => { state.auto_start = !state.auto_start; dirty = true; }
                    Some("desk") => { state.desktop_shortcut = !state.desktop_shortcut; dirty = true; }
                    Some("primary") => match state.stage {
                        WizardStage::Ready => {
                            state.stage = WizardStage::Installing;
                            state.status_line = "Project Aurora kuruluyor ve eski veriler korunuyor...".into();
                            let p_a = progress_atomic.clone();
                            let d_a = done_atomic.clone();
                            let e_m = err_msg.clone();
                            let a_s = state.auto_start;
                            let d_s = state.desktop_shortcut;
                            std::thread::spawn(move || {
                                if let Err(e) = perform_install(a_s, d_s, |pct| p_a.store(pct, Ordering::Relaxed)) {
                                    *e_m.lock() = Some(e.to_string());
                                }
                                d_a.store(true, Ordering::Relaxed);
                            });
                            dirty = true;
                        }
                        WizardStage::Done => {
                            launch_installed_aurora();
                            state.close_requested = true;
                        }
                        WizardStage::Error => state.close_requested = true,
                        WizardStage::Installing => {}
                    },
                    _ => {}
                }
            }
        }

        if state.stage == WizardStage::Installing {
            let p = progress_atomic.load(Ordering::Relaxed);
            if p != state.progress_pct {
                state.progress_pct = p;
                dirty = true;
            }
            if done_atomic.load(Ordering::Relaxed) {
                if let Some(e) = err_msg.lock().clone() {
                    state.stage = WizardStage::Error;
                    state.status_line = format!("Kurulum hatası: {e}");
                } else {
                    state.stage = WizardStage::Done;
                    state.progress_pct = 100;
                    state.status_line = "Kurulum tamamlandı! Önceki tüm verileriniz korundu.".into();
                }
                dirty = true;
            }
        }

        if dirty {
            dirty = false;
            surface.begin_frame();
            {
                let p = Painter::new(&surface.d2d_dc, &gpu);
                let full = RectF::new(0.0, 0.0, w as f32, h as f32);
                p.fill_linear_gradient_rounded(full, 20.0, theme.surface1_solid, Color::from_hex("#162038"), true);
                p.fill_radial_glow(110.0, 40.0, 220.0, 150.0, theme.accent1.with_alpha(0.18));
                p.stroke_rounded_rect(full, 20.0, Color::WHITE.with_alpha(0.24), 1.2, false);

                // Brand Header
                p.fill_circle(42.0, 44.0, 12.0, theme.accent1);
                p.fill_radial_glow(42.0, 44.0, 24.0, 24.0, theme.accent_glow);
                p.draw_text(&format!("Project Aurora v{AURORA_VERSION}"), RectF::new(66.0, 24.0, 340.0, 24.0), 18.0, 800, theme.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
                p.draw_text("Saf Rust Motorlu Modern Windows 11 Kabuğu", RectF::new(66.0, 48.0, 380.0, 18.0), 11.0, 500, theme.text_secondary, FontFamily::Ui, TextAlign::Left, true, false);

                // Close button
                p.fill_rounded_rect(btn_close, 9.0, if state.hover_btn == Some("close") { theme.danger.with_alpha(0.25) } else { theme.card_bg });
                p.draw_icon(IconKind::Close, btn_close.center_x(), btn_close.center_y(), 12.0, theme.text_primary);

                // Highlights box
                let info = RectF::new(28.0, 86.0, w as f32 - 56.0, 86.0);
                p.fill_rounded_rect(info, 12.0, theme.inset_bg);
                p.stroke_rounded_rect(info, 12.0, theme.border_subtle, 1.0, false);
                p.draw_text("• Saf Rust + Tek Paylaşımlı WebView2: Sıfır .NET 10, Sıfır Harici DLL", RectF::new(info.x + 14.0, info.y + 10.0, info.w - 28.0, 20.0), 11.0, 600, theme.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
                p.draw_text("• 10 Kabuk Mimarisi, Dinamik Ada, 20 Tema, 10 Masaüstü & 14 Dock Widget'ı", RectF::new(info.x + 14.0, info.y + 34.0, info.w - 28.0, 20.0), 11.0, 500, theme.text_secondary, FontFamily::Ui, TextAlign::Left, true, false);
                p.draw_text("• Yönetici izni (UAC) gerekmez • v2.x / v3.x+ verileriniz %100 korunur", RectF::new(info.x + 14.0, info.y + 56.0, info.w - 28.0, 20.0), 11.0, 500, theme.success, FontFamily::Ui, TextAlign::Left, true, false);

                if state.stage == WizardStage::Ready {
                    for (r, checked, lbl) in [
                        (chk_start, state.auto_start, "Windows açılışında Project Aurora'yı otomatik başlat"),
                        (chk_desk, state.desktop_shortcut, "Masaüstüne kısayol ekle"),
                    ] {
                        p.fill_rounded_rect(r, 9.0, theme.card_bg);
                        let box_r = RectF::new(r.x + 10.0, r.center_y() - 8.0, 16.0, 16.0);
                        p.fill_rounded_rect(box_r, 4.0, if checked { theme.accent1 } else { theme.inset_bg });
                        p.stroke_rounded_rect(box_r, 4.0, if checked { theme.accent1 } else { theme.border_rim }, 1.2, false);
                        if checked {
                            p.draw_icon(IconKind::Check, box_r.center_x(), box_r.center_y(), 10.0, theme.accent_ink);
                        }
                        p.draw_text(lbl, RectF::new(box_r.right() + 10.0, r.y, r.w - 44.0, r.h), 11.5, 600, theme.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
                    }
                } else {
                    let bar = RectF::new(28.0, 212.0, w as f32 - 56.0, 10.0);
                    p.fill_rounded_rect(bar, 5.0, theme.inset_bg);
                    let fw = (bar.w * (state.progress_pct as f32 / 100.0)).max(10.0);
                    p.fill_linear_gradient_rounded(RectF::new(bar.x, bar.y, fw, bar.h), 5.0, theme.accent1, theme.accent2, false);
                    p.draw_text(&format!("{}%", state.progress_pct), RectF::new(28.0, 228.0, w as f32 - 56.0, 20.0), 12.0, 700, theme.accent1, FontFamily::Mono, TextAlign::Right, true, false);
                }

                // Bottom Status + Action Button
                p.draw_text(&state.status_line, RectF::new(28.0, h as f32 - 52.0, w as f32 - 236.0, 28.0), 10.5, 500, theme.text_secondary, FontFamily::Ui, TextAlign::Left, true, true);
                let btn_lbl = match state.stage {
                    WizardStage::Ready => "Kurulumu Başlat",
                    WizardStage::Installing => "Kuruluyor...",
                    WizardStage::Done => "Aurora'yı Başlat ✓",
                    WizardStage::Error => "Kapat",
                };
                p.fill_linear_gradient_rounded(btn_primary, 19.0, theme.accent1, theme.accent2, false);
                p.draw_text(btn_lbl, btn_primary, 12.5, 700, theme.accent_ink, FontFamily::Ui, TextAlign::Center, true, false);
            }
            let _ = surface.end_frame_and_present(&gpu, None);
        }

        std::thread::sleep(std::time::Duration::from_millis(16));
    }

    unsafe {
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, 0);
        drop(Box::from_raw(ev_box));
    }
    Ok(())
}

unsafe extern "system" fn setup_wnd_proc(hwnd: HWND, msg: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    let ptr = windows::Win32::UI::WindowsAndMessaging::GetWindowLongPtrW(hwnd, GWLP_USERDATA) as *const Arc<Mutex<Vec<GuiClickEvent>>>;
    match msg {
        WM_DESTROY => {
            PostQuitMessage(0);
            return LRESULT(0);
        }
        WM_MOUSEMOVE | WM_LBUTTONDOWN if !ptr.is_null() => {
            let raw = lparam.0 as u32;
            let mx = (raw & 0xFFFF) as i16 as f32;
            let my = ((raw >> 16) & 0xFFFF) as i16 as f32;
            (*ptr).lock().push(GuiClickEvent { mx, my, clicked: msg == WM_LBUTTONDOWN });
            return LRESULT(0);
        }
        _ => {}
    }
    DefWindowProcW(hwnd, msg, wparam, lparam)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_legacy_and_modern_updater_flags() {
        let legacy_args = vec![
            "Aurora-Kurulum.exe".to_string(),
            "--update".to_string(),
            "--wait-pid=4242".to_string(),
        ];
        assert!(is_silent_or_update_mode(&legacy_args));
        assert_eq!(parse_wait_pid(&legacy_args), Some(4242));

        let modern_args = vec![
            "Aurora-Kurulum.exe".to_string(),
            "--silent".to_string(),
            "--relaunch".to_string(),
            "--wait-pid".to_string(),
            "9876".to_string(),
        ];
        assert!(is_silent_or_update_mode(&modern_args));
        assert_eq!(parse_wait_pid(&modern_args), Some(9876));

        let gui_args = vec!["Aurora-Kurulum.exe".to_string()];
        assert!(!is_silent_or_update_mode(&gui_args));
        assert_eq!(parse_wait_pid(&gui_args), None);
    }
}
