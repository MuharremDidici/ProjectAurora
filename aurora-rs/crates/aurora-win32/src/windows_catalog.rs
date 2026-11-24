use std::collections::{HashMap, HashSet};
use std::path::Path;
use aurora_core::{AppWindowEntry, MonitorDescriptor, PhysicalRect};
use windows::core::{PCWSTR, PWSTR};
use windows::Win32::Foundation::{BOOL, HWND, LPARAM, RECT, WPARAM};
use windows::Win32::Graphics::Dwm::{DwmGetWindowAttribute, DWMWA_CLOAKED, DWMWA_EXTENDED_FRAME_BOUNDS};
use windows::Win32::Graphics::Gdi::{
    CreateCompatibleDC, DeleteDC, DeleteObject, GetDIBits, GetMonitorInfoW, MonitorFromWindow,
    SelectObject, BITMAPINFO, BITMAPINFOHEADER, BI_RGB, DIB_RGB_COLORS, MONITORINFOEXW,
    MONITOR_DEFAULTTONEAREST,
};
use windows::Win32::System::Threading::{
    OpenProcess, QueryFullProcessImageNameW, PROCESS_NAME_WIN32, PROCESS_QUERY_LIMITED_INFORMATION,
};
use windows::Win32::UI::Shell::{SHGetFileInfoW, SHFILEINFOW, SHGFI_ICON, SHGFI_LARGEICON};
use windows::Win32::UI::WindowsAndMessaging::{
    DestroyIcon, EnumWindows, GetAncestor, GetClassNameW, GetForegroundWindow, GetIconInfo,
    GetLastActivePopup, GetWindowLongPtrW, GetWindowRect, GetWindowTextW, GetWindowThreadProcessId,
    IsIconic, IsWindowVisible, IsZoomed, PostMessageW, SendMessageTimeoutW,
    SetWindowPos, ShowWindowAsync, GA_ROOTOWNER, GCLP_HICON, GCLP_HICONSM, GWL_EXSTYLE, GWL_STYLE,
    HICON, HWND_NOTOPMOST, HWND_TOPMOST, ICON_BIG, ICON_SMALL, ICON_SMALL2, SMTO_ABORTIFHUNG,
    SWP_ASYNCWINDOWPOS, SWP_NOACTIVATE, SWP_NOMOVE, SWP_NOSIZE, SWP_NOZORDER, SW_MAXIMIZE,
    SW_MINIMIZE, SW_RESTORE, WM_CLOSE, WM_GETICON, WS_EX_APPWINDOW, WS_EX_NOACTIVATE,
    WS_EX_TOOLWINDOW, WS_EX_TOPMOST,
};

use crate::appbar::is_native_taskbar_window;

pub fn get_foreground_hwnd() -> isize {
    unsafe { GetForegroundWindow().0 as isize }
}

pub fn enumerate_open_windows(
    owned_hwnds: &HashSet<isize>,
    attention_procs: &HashSet<String>,
) -> Vec<AppWindowEntry> {
    struct Ctx<'a> {
        own_pid: u32,
        fg_hwnd: isize,
        owned: &'a HashSet<isize>,
        attention: &'a HashSet<String>,
        out: Vec<AppWindowEntry>,
        pid_exe_cache: HashMap<u32, (String, String)>,
    }

    let mut ctx = Ctx {
        own_pid: std::process::id(),
        fg_hwnd: get_foreground_hwnd(),
        owned: owned_hwnds,
        attention: attention_procs,
        out: Vec::with_capacity(24),
        pid_exe_cache: HashMap::new(),
    };

    unsafe extern "system" fn callback(hwnd: HWND, lparam: LPARAM) -> BOOL {
        let ctx = &mut *(lparam.0 as *mut Ctx<'_>);
        let raw_h = hwnd.0 as isize;
        if raw_h == 0 || ctx.owned.contains(&raw_h) || !IsWindowVisible(hwnd).as_bool() {
            return BOOL(1);
        }
        if is_native_taskbar_window(hwnd) {
            return BOOL(1);
        }
        let ex = GetWindowLongPtrW(hwnd, GWL_EXSTYLE) as u32;
        if (ex & WS_EX_TOOLWINDOW.0) != 0 && (ex & WS_EX_APPWINDOW.0) == 0 {
            return BOOL(1);
        }
        if (ex & WS_EX_NOACTIVATE.0) != 0 && (ex & WS_EX_APPWINDOW.0) == 0 {
            return BOOL(1);
        }
        let mut cloaked = 0i32;
        if DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, &mut cloaked as *mut _ as *mut _, 4).is_ok() && cloaked != 0 {
            return BOOL(1);
        }

        // Standard Alt-Tab root owner check
        let root = GetAncestor(hwnd, GA_ROOTOWNER);
        let last_popup = GetLastActivePopup(root);
        if last_popup != hwnd && IsWindowVisible(last_popup).as_bool() {
            return BOOL(1);
        }

        let mut title_buf = [0u16; 256];
        let tlen = GetWindowTextW(hwnd, &mut title_buf) as usize;
        if tlen == 0 {
            return BOOL(1);
        }
        let title = String::from_utf16_lossy(&title_buf[..tlen]).trim().to_string();
        if title.is_empty() || title == "Program Manager" || title == "Windows Input Experience" {
            return BOOL(1);
        }

        let mut cls_buf = [0u16; 64];
        let clen = GetClassNameW(hwnd, &mut cls_buf) as usize;
        let cls = String::from_utf16_lossy(&cls_buf[..clen]);
        if matches!(cls.as_str(), "Progman" | "WorkerW" | "Windows.UI.Core.CoreWindow") {
            return BOOL(1);
        }

        let mut pid = 0u32;
        GetWindowThreadProcessId(hwnd, Some(&mut pid));
        if pid == 0 || pid == ctx.own_pid {
            return BOOL(1);
        }

        let (mut proc_name, mut exe_path) = ctx
            .pid_exe_cache
            .entry(pid)
            .or_insert_with(|| query_process_image(pid))
            .clone();
        // Resolve real UWP / Store app process hosted inside ApplicationFrameHost.exe (WhatsApp, Settings, Teams, etc.)
        if proc_name.eq_ignore_ascii_case("ApplicationFrameHost") {
            if let Some(real_pid) = find_uwp_child_pid(hwnd, pid) {
                let (rp, re) = ctx
                    .pid_exe_cache
                    .entry(real_pid)
                    .or_insert_with(|| query_process_image(real_pid))
                    .clone();
                if !rp.is_empty() && !rp.eq_ignore_ascii_case("ApplicationFrameHost") {
                    pid = real_pid;
                    proc_name = rp;
                    exe_path = re;
                }
            }
        }
        if proc_name.is_empty() {
            return BOOL(1);
        }

        let hmon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        let mut mi = MONITORINFOEXW::default();
        mi.monitorInfo.cbSize = std::mem::size_of::<MONITORINFOEXW>() as u32;
        let monitor_device_id = if GetMonitorInfoW(hmon, &mut mi.monitorInfo as *mut _ as *mut _).as_bool() {
            let n = mi.szDevice.iter().position(|&c| c == 0).unwrap_or(mi.szDevice.len());
            String::from_utf16_lossy(&mi.szDevice[..n]).trim().to_string()
        } else {
            "DISPLAY_1".into()
        };

        let is_active = raw_h == ctx.fg_hwnd;
        let is_minimized = IsIconic(hwnd).as_bool();
        let is_top_most = (ex & WS_EX_TOPMOST.0) != 0;
        let needs_attention = !is_active && ctx.attention.contains(&proc_name.to_ascii_lowercase());

        ctx.out.push(AppWindowEntry {
            hwnd: raw_h,
            process_id: pid,
            process_name: proc_name,
            executable_path: exe_path,
            title,
            monitor_device_id,
            is_active,
            is_minimized,
            is_top_most,
            needs_attention,
            progress_percent: None,
        });
        BOOL(1)
    }

    unsafe {
        let _ = EnumWindows(Some(callback), LPARAM(&mut ctx as *mut Ctx<'_> as isize));
    }
    ctx.out
}

unsafe fn find_uwp_child_pid(parent: HWND, host_pid: u32) -> Option<u32> {
    struct ChildCtx {
        host_pid: u32,
        found: Option<u32>,
    }
    unsafe extern "system" fn child_cb(ch: HWND, lp: LPARAM) -> BOOL {
        let c = &mut *(lp.0 as *mut ChildCtx);
        let mut cpid = 0u32;
        GetWindowThreadProcessId(ch, Some(&mut cpid));
        if cpid != 0 && cpid != c.host_pid {
            c.found = Some(cpid);
            return BOOL(0);
        }
        BOOL(1)
    }
    let mut ctx = ChildCtx { host_pid, found: None };
    let _ = windows::Win32::UI::WindowsAndMessaging::EnumChildWindows(
        parent,
        Some(child_cb),
        LPARAM(&mut ctx as *mut _ as isize),
    );
    ctx.found
}

pub fn query_process_image(pid: u32) -> (String, String) {
    unsafe {
        let Ok(hproc) = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid) else {
            return (String::new(), String::new());
        };
        let mut buf = [0u16; 520];
        let mut size = buf.len() as u32;
        let ok = QueryFullProcessImageNameW(hproc, PROCESS_NAME_WIN32, PWSTR(buf.as_mut_ptr()), &mut size).is_ok();
        let _ = windows::Win32::Foundation::CloseHandle(hproc);
        if !ok || size == 0 {
            return (String::new(), String::new());
        }
        let path = String::from_utf16_lossy(&buf[..size as usize]);
        let stem = Path::new(&path)
            .file_stem()
            .and_then(|s| s.to_str())
            .unwrap_or("")
            .to_string();
        (stem, path)
    }
}

pub fn focus_window(hwnd_raw: isize) {
    let hwnd = HWND(hwnd_raw as *mut _);
    unsafe {
        if IsIconic(hwnd).as_bool() {
            let _ = ShowWindowAsync(hwnd, SW_RESTORE);
        }
    }
    crate::hooks_and_overlays::force_foreground_window(hwnd);
}

pub fn activate_or_toggle_window(hwnd_raw: isize) {
    let hwnd = HWND(hwnd_raw as *mut _);
    unsafe {
        if GetForegroundWindow() == hwnd && !IsIconic(hwnd).as_bool() {
            let _ = ShowWindowAsync(hwnd, SW_MINIMIZE);
        } else {
            if IsIconic(hwnd).as_bool() {
                let _ = ShowWindowAsync(hwnd, SW_RESTORE);
            }
            crate::hooks_and_overlays::force_foreground_window(hwnd);
        }
    }
}

pub fn minimize_window(hwnd_raw: isize) {
    unsafe { let _ = ShowWindowAsync(HWND(hwnd_raw as *mut _), SW_MINIMIZE); }
}

pub fn close_window(hwnd_raw: isize) {
    unsafe { let _ = PostMessageW(HWND(hwnd_raw as *mut _), WM_CLOSE, WPARAM(0), LPARAM(0)); }
}

pub fn toggle_always_on_top(hwnd_raw: isize) -> bool {
    let hwnd = HWND(hwnd_raw as *mut _);
    unsafe {
        let ex = GetWindowLongPtrW(hwnd, GWL_EXSTYLE) as u32;
        let next = (ex & WS_EX_TOPMOST.0) == 0;
        let _ = SetWindowPos(
            hwnd,
            if next { HWND_TOPMOST } else { HWND_NOTOPMOST },
            0,
            0,
            0,
            0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS,
        );
        next
    }
}

pub fn arrange_window(hwnd_raw: isize, mode: &str) {
    let hwnd = HWND(hwnd_raw as *mut _);
    unsafe {
        let hmon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        let mut mi = MONITORINFOEXW::default();
        mi.monitorInfo.cbSize = std::mem::size_of::<MONITORINFOEXW>() as u32;
        if !GetMonitorInfoW(hmon, &mut mi.monitorInfo as *mut _ as *mut _).as_bool() {
            return;
        }
        let wa = mi.monitorInfo.rcWork;
        let w = wa.right - wa.left;
        let h = wa.bottom - wa.top;
        if matches!(mode, "max" | "maximize") {
            let _ = ShowWindowAsync(hwnd, SW_MAXIMIZE);
            crate::hooks_and_overlays::force_foreground_window(hwnd);
            return;
        }
        if IsZoomed(hwnd).as_bool() || IsIconic(hwnd).as_bool() {
            let _ = ShowWindowAsync(hwnd, SW_RESTORE);
        }
        let (x, y, rw, rh) = match mode {
            "left" | "snap_left" => (wa.left, wa.top, w / 2, h),
            "right" | "snap_right" => (wa.left + w / 2, wa.top, w - w / 2, h),
            "center" => (wa.left + w / 8, wa.top + h / 12, (w * 3) / 4, (h * 5) / 6),
            _ => (wa.left, wa.top, w, h),
        };
        let _ = SetWindowPos(hwnd, None, x, y, rw, rh, SWP_NOZORDER | SWP_ASYNCWINDOWPOS);
        crate::hooks_and_overlays::force_foreground_window(hwnd);
    }
}

/// Detects per-monitor exclusive fullscreen apps/games AND SmartDodge dock-zone overlap!
pub fn analyze_fullscreen_and_dodge(
    monitors: &[MonitorDescriptor],
    owned_hwnds: &HashSet<isize>,
    dock_edge: &str,
) -> (HashSet<String>, HashSet<String>) {
    let mut fullscreen_ids = HashSet::new();
    let mut overlapped_ids = HashSet::new();

    let fg_hwnd = get_foreground_hwnd();
    let d3d_exclusive_fs = unsafe {
        windows::Win32::UI::Shell::SHQueryUserNotificationState()
            .map(|s| s == windows::Win32::UI::Shell::QUNS_RUNNING_D3D_FULL_SCREEN)
            .unwrap_or(false)
    };

    // 1. Check ONLY the actual FOREGROUND window for true fullscreen games/videos!
    //    NEVER flags maximized Antigravity/VS Code/Chrome/WhatsApp/Electron apps or background windows!
    if fg_hwnd != 0 && !owned_hwnds.contains(&fg_hwnd) {
        unsafe {
            let fg = HWND(fg_hwnd as *mut _);
            let mut fg_pid = 0u32;
            GetWindowThreadProcessId(fg, Some(&mut fg_pid));
            if fg_pid != 0 && fg_pid != std::process::id() && IsWindowVisible(fg).as_bool() && !IsIconic(fg).as_bool() && !is_native_taskbar_window(fg) {
                let mut cls_buf = [0u16; 64];
                let clen = GetClassNameW(fg, &mut cls_buf) as usize;
                let cls = String::from_utf16_lossy(&cls_buf[..clen]);
                let is_shell_or_desktop = matches!(
                    cls.as_str(),
                    "Progman" | "WorkerW" | "Windows.UI.Core.CoreWindow" | "Shell_TrayWnd" | "Shell_SecondaryTrayWnd" | "ApplicationFrameWindow"
                );
                if !is_shell_or_desktop {
                    let mut rc = RECT::default();
                    let got_rc = DwmGetWindowAttribute(
                        fg,
                        DWMWA_EXTENDED_FRAME_BOUNDS,
                        &mut rc as *mut _ as *mut _,
                        std::mem::size_of::<RECT>() as u32,
                    )
                    .is_ok()
                        || GetWindowRect(fg, &mut rc).is_ok();
                    if got_rc {
                        let cx = (rc.left + rc.right) / 2;
                        let cy = (rc.top + rc.bottom) / 2;
                        if let Some(mon) = monitors.iter().find(|m| m.bounds.contains(cx, cy)) {
                            let mb = mon.bounds;
                            let covers_mon = rc.left <= mb.x + 1
                                && rc.top <= mb.y + 1
                                && rc.right >= mb.right() - 1
                                && rc.bottom >= mb.bottom() - 1;
                            let style = GetWindowLongPtrW(fg, GWL_STYLE) as u32;
                            let is_zoomed = IsZoomed(fg).as_bool();
                            const WS_CAPTION_MASK: u32 = 0x00C0_0000;
                            const WS_THICKFRAME_MASK: u32 = 0x0004_0000;
                            let has_normal_frame = (style & WS_CAPTION_MASK) != 0 || (style & WS_THICKFRAME_MASK) != 0;
                            // Only treat as fullscreen when D3D exclusive fullscreen is active OR
                            // foreground is truly un-zoomed frameless borderless covering 100% of the monitor
                            // (e.g., borderless fullscreen game or F11 fullscreen video — NEVER a maximized app!).
                            if covers_mon && (d3d_exclusive_fs || (!is_zoomed && !has_normal_frame)) {
                                fullscreen_ids.insert(mon.device_id.clone());
                            }
                        }
                    }
                }
            }
        }
    }

    // 2. SmartDodge overlap check across visible non-desktop user windows
    struct Ctx<'a> {
        own_pid: u32,
        owned: &'a HashSet<isize>,
        monitors: &'a [MonitorDescriptor],
        dock_edge: &'a str,
        over: &'a mut HashSet<String>,
    }

    let mut ctx = Ctx {
        own_pid: std::process::id(),
        owned: owned_hwnds,
        monitors,
        dock_edge,
        over: &mut overlapped_ids,
    };

    unsafe extern "system" fn enum_fs(hwnd: HWND, lparam: LPARAM) -> BOOL {
        let ctx = &mut *(lparam.0 as *mut Ctx<'_>);
        let raw = hwnd.0 as isize;
        if raw == 0 || ctx.owned.contains(&raw) || !IsWindowVisible(hwnd).as_bool() || IsIconic(hwnd).as_bool() {
            return BOOL(1);
        }
        if is_native_taskbar_window(hwnd) {
            return BOOL(1);
        }
        let mut pid = 0u32;
        GetWindowThreadProcessId(hwnd, Some(&mut pid));
        if pid == 0 || pid == ctx.own_pid {
            return BOOL(1);
        }
        let mut cls_buf = [0u16; 64];
        let clen = GetClassNameW(hwnd, &mut cls_buf) as usize;
        let cls = String::from_utf16_lossy(&cls_buf[..clen]);
        if matches!(cls.as_str(), "Progman" | "WorkerW" | "Windows.UI.Core.CoreWindow" | "Shell_TrayWnd" | "Shell_SecondaryTrayWnd") {
            return BOOL(1);
        }
        let ex_style = GetWindowLongPtrW(hwnd, GWL_EXSTYLE) as u32;
        if (ex_style & WS_EX_TOOLWINDOW.0) != 0 {
            return BOOL(1);
        }
        let mut cloaked = 0i32;
        if DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, &mut cloaked as *mut _ as *mut _, 4).is_ok() && cloaked != 0 {
            return BOOL(1);
        }

        let mut rc = RECT::default();
        if DwmGetWindowAttribute(
            hwnd,
            DWMWA_EXTENDED_FRAME_BOUNDS,
            &mut rc as *mut _ as *mut _,
            std::mem::size_of::<RECT>() as u32,
        )
        .is_err()
            && GetWindowRect(hwnd, &mut rc).is_err()
        {
            return BOOL(1);
        }
        let rw = rc.right - rc.left;
        let rh = rc.bottom - rc.top;
        if rw < 160 || rh < 100 {
            return BOOL(1);
        }

        let cx = (rc.left + rc.right) / 2;
        let cy = (rc.top + rc.bottom) / 2;
        let Some(mon) = ctx.monitors.iter().find(|m| m.bounds.contains(cx, cy)) else {
            return BOOL(1);
        };
        let mb = mon.bounds;

        // SmartDodge dock-edge overlap check (geometry-based!)
        let scale = mon.scale_factor();
        let zone = (56.0 * scale).round() as i32;
        let overlaps = if ctx.dock_edge.eq_ignore_ascii_case("Left") {
            rc.left < mb.x + zone && rc.right > mb.x
        } else if ctx.dock_edge.eq_ignore_ascii_case("Right") {
            rc.right > mb.right() - zone && rc.left < mb.right()
        } else {
            rc.bottom > mb.bottom() - zone && rc.top < mb.bottom()
        };
        if overlaps {
            ctx.over.insert(mon.device_id.clone());
        }
        BOOL(1)
    }

    unsafe {
        let _ = EnumWindows(Some(enum_fs), LPARAM(&mut ctx as *mut Ctx<'_> as isize));
    }
    (fullscreen_ids, overlapped_ids)
}

/// Extracts a crisp 32-bit premultiplied BGRA bitmap directly from an HWND or executable path!
/// Uploaded straight to Direct2D GPU memory (zero PNG encoding / zero Base64 JSON overhead!).
pub fn extract_icon_bgra(hwnd_opt: Option<isize>, exe_path: &str) -> Option<(u32, u32, Vec<u8>)> {
    unsafe {
        let mut hicon = HICON::default();
        let mut owns_icon = false;

        if let Some(raw) = hwnd_opt.filter(|&h| h != 0) {
            let hwnd = HWND(raw as *mut _);
            let mut res = 0usize;
            for icon_type in [ICON_BIG, ICON_SMALL2, ICON_SMALL] {
                if SendMessageTimeoutW(hwnd, WM_GETICON, WPARAM(icon_type as usize), LPARAM(0), SMTO_ABORTIFHUNG, 40, Some(&mut res)).0 != 0
                    && res != 0
                {
                    hicon = HICON(res as *mut _);
                    break;
                }
            }
            if hicon.0.is_null() {
                let c = windows::Win32::UI::WindowsAndMessaging::GetClassLongPtrW(hwnd, GCLP_HICON);
                if c != 0 {
                    hicon = HICON(c as *mut _);
                } else {
                    let cs = windows::Win32::UI::WindowsAndMessaging::GetClassLongPtrW(hwnd, GCLP_HICONSM);
                    if cs != 0 {
                        hicon = HICON(cs as *mut _);
                    }
                }
            }
        }

        if hicon.0.is_null() && !exe_path.is_empty() {
            let wide: Vec<u16> = exe_path.encode_utf16().chain(std::iter::once(0)).collect();
            let mut shfi = SHFILEINFOW::default();
            let ret = SHGetFileInfoW(
                PCWSTR(wide.as_ptr()),
                windows::Win32::Storage::FileSystem::FILE_FLAGS_AND_ATTRIBUTES(0),
                Some(&mut shfi),
                std::mem::size_of::<SHFILEINFOW>() as u32,
                SHGFI_ICON | SHGFI_LARGEICON,
            );
            if ret != 0 && !shfi.hIcon.0.is_null() {
                hicon = shfi.hIcon;
                owns_icon = true;
            }
        }

        if hicon.0.is_null() {
            return None;
        }
        let result = hicon_to_premultiplied_bgra(hicon);
        if owns_icon {
            let _ = DestroyIcon(hicon);
        }
        result
    }
}

unsafe fn hicon_to_premultiplied_bgra(hicon: HICON) -> Option<(u32, u32, Vec<u8>)> {
    use windows::Win32::Graphics::Gdi::{GetObjectW, BITMAP};

    let mut info = Default::default();
    if GetIconInfo(hicon, &mut info).is_err() {
        return None;
    }
    let hbm_color = info.hbmColor;
    let hbm_mask = info.hbmMask;

    let mut bm = BITMAP::default();
    let (w, h) = if !hbm_color.0.is_null()
        && GetObjectW(
            hbm_color,
            std::mem::size_of::<BITMAP>() as i32,
            Some(&mut bm as *mut _ as *mut _),
        ) != 0
    {
        (bm.bmWidth.clamp(16, 128), bm.bmHeight.abs().clamp(16, 128))
    } else {
        (32, 32)
    };

    let hdc = CreateCompatibleDC(None);
    if hdc.0.is_null() {
        if !hbm_color.0.is_null() { let _ = DeleteObject(hbm_color); }
        if !hbm_mask.0.is_null() { let _ = DeleteObject(hbm_mask); }
        return None;
    }

    let mut bmi = BITMAPINFO {
        bmiHeader: BITMAPINFOHEADER {
            biSize: std::mem::size_of::<BITMAPINFOHEADER>() as u32,
            biWidth: w,
            biHeight: -h, // top-down
            biPlanes: 1,
            biBitCount: 32,
            biCompression: BI_RGB.0,
            ..Default::default()
        },
        ..Default::default()
    };

    let mut pixels = vec![0u8; (w * h * 4) as usize];
    let ok = if !hbm_color.0.is_null() {
        let old = SelectObject(hdc, hbm_color);
        let lines = GetDIBits(
            hdc,
            hbm_color,
            0,
            h as u32,
            Some(pixels.as_mut_ptr() as *mut _),
            &mut bmi,
            DIB_RGB_COLORS,
        );
        SelectObject(hdc, old);
        lines > 0
    } else {
        false
    };

    // Read hbmMask when alpha channel is absent so dark icons keep sharp silhouettes!
    let any_alpha = ok && pixels.as_chunks::<4>().0.iter().any(|px| px[3] != 0);
    let mut mask_pixels = Vec::new();
    if ok && !any_alpha && !hbm_mask.0.is_null() {
        mask_pixels.resize((w * h * 4) as usize, 0u8);
        let mut mask_bmi = bmi;
        let old = SelectObject(hdc, hbm_mask);
        let _ = GetDIBits(
            hdc,
            hbm_mask,
            0,
            h as u32,
            Some(mask_pixels.as_mut_ptr() as *mut _),
            &mut mask_bmi,
            DIB_RGB_COLORS,
        );
        SelectObject(hdc, old);
    }

    let _ = DeleteDC(hdc);
    if !hbm_color.0.is_null() { let _ = DeleteObject(hbm_color); }
    if !hbm_mask.0.is_null() { let _ = DeleteObject(hbm_mask); }

    if !ok {
        return None;
    }

    let mut visible_count = 0usize;
    for (idx, px) in pixels.as_chunks_mut::<4>().0.iter_mut().enumerate() {
        if !any_alpha {
            let opaque = if mask_pixels.len() >= (idx + 1) * 4 {
                // In Win32 AND mask, 0 = opaque pixel, 255 = transparent pixel
                mask_pixels[idx * 4] == 0
            } else {
                px[0] != 0 || px[1] != 0 || px[2] != 0
            };
            px[3] = if opaque { 255 } else { 0 };
            if !opaque {
                px[0] = 0;
                px[1] = 0;
                px[2] = 0;
            }
        } else {
            let a = px[3] as f32 / 255.0;
            px[0] = (px[0] as f32 * a) as u8;
            px[1] = (px[1] as f32 * a) as u8;
            px[2] = (px[2] as f32 * a) as u8;
        }
        if px[3] >= 16 {
            visible_count += 1;
        }
    }
    // Reject completely blank/corrupt HICONs so callers fall through to IShellItemImageFactory / AppsFolder!
    if visible_count < 12 {
        return None;
    }
    Some((w as u32, h as u32, pixels))
}

/// Computes exact Snap Zone rectangles for Shift+Drag window snapping (`code6535`, `studio3`, `cols3`, `grid2x2`).
pub fn compute_snap_zones(work: PhysicalRect, preset: &str, gap: i32) -> Vec<(&'static str, PhysicalRect)> {
    let x = work.x + gap;
    let y = work.y + gap;
    let w = (work.width - gap * 2).max(200);
    let h = (work.height - gap * 2).max(200);

    match preset {
        "studio3" => {
            let lw = (w * 58) / 100 - gap / 2;
            let rw = w - lw - gap;
            let th = h / 2 - gap / 2;
            let bh = h - th - gap;
            vec![
                ("Ana Sahne (58%)", PhysicalRect::new(x, y, lw, h)),
                ("Üst Sağ", PhysicalRect::new(x + lw + gap, y, rw, th)),
                ("Alt Sağ", PhysicalRect::new(x + lw + gap, y + th + gap, rw, bh)),
            ]
        }
        "cols3" => {
            let cw = (w - gap * 2) / 3;
            vec![
                ("Sol Sütun", PhysicalRect::new(x, y, cw, h)),
                ("Orta Sütun", PhysicalRect::new(x + cw + gap, y, cw, h)),
                ("Sağ Sütun", PhysicalRect::new(x + (cw + gap) * 2, y, w - (cw + gap) * 2, h)),
            ]
        }
        "grid2x2" => {
            let hw = (w - gap) / 2;
            let hh = (h - gap) / 2;
            vec![
                ("Sol Üst", PhysicalRect::new(x, y, hw, hh)),
                ("Sağ Üst", PhysicalRect::new(x + hw + gap, y, w - hw - gap, hh)),
                ("Sol Alt", PhysicalRect::new(x, y + hh + gap, hw, h - hh - gap)),
                ("Sağ Alt", PhysicalRect::new(x + hw + gap, y + hh + gap, w - hw - gap, h - hh - gap)),
            ]
        }
        _ => {
            let lw = (w * 65) / 100 - gap / 2;
            let rw = w - lw - gap;
            vec![
                ("Kod / Ana (65%)", PhysicalRect::new(x, y, lw, h)),
                ("Yan Panel (35%)", PhysicalRect::new(x + lw + gap, y, rw, h)),
            ]
        }
    }
}
