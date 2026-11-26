use std::collections::{HashMap, HashSet};
use std::path::Path;
use std::sync::atomic::{AtomicIsize, Ordering};
use aurora_core::{AppWindowEntry, MonitorDescriptor, PhysicalRect};
use windows::core::{PCWSTR, PWSTR};
use windows::Win32::Foundation::{BOOL, ERROR_SUCCESS, HWND, LPARAM, RECT, WPARAM};
use windows::Win32::Graphics::Dwm::{DwmGetWindowAttribute, DWMWA_CLOAKED, DWMWA_EXTENDED_FRAME_BOUNDS};
use windows::Win32::Graphics::Gdi::{
    CreateCompatibleDC, DeleteDC, DeleteObject, GetDIBits, GetMonitorInfoW, MonitorFromWindow,
    SelectObject, BITMAPINFO, BITMAPINFOHEADER, BI_RGB, DIB_RGB_COLORS, MONITORINFOEXW,
    MONITOR_DEFAULTTONEAREST,
};
use windows::Win32::Storage::Packaging::Appx::GetApplicationUserModelId;
use windows::Win32::System::Threading::{
    OpenProcess, QueryFullProcessImageNameW, PROCESS_NAME_WIN32, PROCESS_QUERY_LIMITED_INFORMATION,
};
use windows::Win32::UI::Shell::{SHGetFileInfoW, SHFILEINFOW, SHGFI_ICON, SHGFI_LARGEICON};
use windows::Win32::UI::WindowsAndMessaging::{
    DestroyIcon, EnumWindows, GetAncestor, GetClassNameW, GetForegroundWindow, GetIconInfo,
    GetLastActivePopup, GetWindowLongPtrW, GetWindowRect, GetWindowTextW, GetWindowThreadProcessId,
    IsIconic, IsWindowVisible, IsZoomed, PostMessageW, SendMessageTimeoutW,
    SetWindowPos, ShowWindowAsync, GA_ROOTOWNER, GCLP_HICON, GCLP_HICONSM, GWL_EXSTYLE,
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
        pid_exe_cache: HashMap<u32, (String, String, Option<String>)>,
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

        // Standard Alt-Tab root owner check — hardened for Java Swing/AWT (`SunAwtFrame` + `SunAwtDialog`):
        // Only skip `hwnd` in favor of `last_popup` when `last_popup` is ALSO a real, titled, non-tool, non-cloaked window!
        let root = GetAncestor(hwnd, GA_ROOTOWNER);
        let last_popup = GetLastActivePopup(root);
        if last_popup != hwnd && IsWindowVisible(last_popup).as_bool() {
            let lp_ex = GetWindowLongPtrW(last_popup, GWL_EXSTYLE) as u32;
            let lp_is_tool = (lp_ex & WS_EX_TOOLWINDOW.0) != 0 && (lp_ex & WS_EX_APPWINDOW.0) == 0;
            let lp_title_len = GetWindowTextW(last_popup, &mut [0u16; 4]);
            let mut lp_cloaked = 0i32;
            let lp_is_cloaked = DwmGetWindowAttribute(
                last_popup,
                DWMWA_CLOAKED,
                &mut lp_cloaked as *mut _ as *mut _,
                4,
            )
            .is_ok()
                && lp_cloaked != 0;
            if !lp_is_tool && !lp_is_cloaked && lp_title_len > 0 {
                return BOOL(1);
            }
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
        if matches!(
            cls.as_str(),
            "Progman"
                | "WorkerW"
                | "Windows.UI.Core.CoreWindow"
                | "MultitaskingViewFrame"
                | "XamlExplorerHostIslandWindow"
                | "TaskListThumbnailWnd"
                | "ForegroundStaging"
                | "ApplicationManager_DesktopShellWindow"
                | "Shell_InputSwitchTopLevelWindow"
        ) {
            return BOOL(1);
        }

        let mut pid = 0u32;
        GetWindowThreadProcessId(hwnd, Some(&mut pid));
        if pid == 0 || pid == ctx.own_pid {
            return BOOL(1);
        }

        let (mut proc_name, mut exe_path, mut aumid) = ctx
            .pid_exe_cache
            .entry(pid)
            .or_insert_with(|| query_process_meta(pid))
            .clone();
        // Resolve real UWP / Store app process hosted inside ApplicationFrameHost.exe (WhatsApp, Settings, Teams, etc.)
        if proc_name.eq_ignore_ascii_case("ApplicationFrameHost") {
            if let Some(real_pid) = find_uwp_child_pid(hwnd, pid) {
                let (rp, re, ra) = ctx
                    .pid_exe_cache
                    .entry(real_pid)
                    .or_insert_with(|| query_process_meta(real_pid))
                    .clone();
                if !rp.is_empty() && !rp.eq_ignore_ascii_case("ApplicationFrameHost") {
                    pid = real_pid;
                    proc_name = rp;
                    exe_path = re;
                    aumid = ra;
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

        let is_active = raw_h == ctx.fg_hwnd
            || (ctx.fg_hwnd != 0
                && !root.0.is_null()
                && GetAncestor(HWND(ctx.fg_hwnd as *mut _), GA_ROOTOWNER) == root);
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
            aumid,
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
    let (stem, path, _) = query_process_meta(pid);
    (stem, path)
}

/// Queries executable stem, full image path, AND native MSIX/UWP `AUMID` via `GetApplicationUserModelId`.
/// Resolves Store apps like `WhatsApp.Root.exe` (`5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App`),
/// `ms-teams.exe`, Spotify, Terminal, Calculator, and Microsoft Store cleanly!
pub fn query_process_meta(pid: u32) -> (String, String, Option<String>) {
    unsafe {
        let Ok(hproc) = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid) else {
            return (String::new(), String::new(), None);
        };
        let mut buf = [0u16; 520];
        let mut size = buf.len() as u32;
        let ok = QueryFullProcessImageNameW(hproc, PROCESS_NAME_WIN32, PWSTR(buf.as_mut_ptr()), &mut size).is_ok();

        let mut aumid_buf = [0u16; 260];
        let mut aumid_len = aumid_buf.len() as u32;
        let aumid = if GetApplicationUserModelId(hproc, &mut aumid_len, PWSTR(aumid_buf.as_mut_ptr())) == ERROR_SUCCESS
            && aumid_len > 1
        {
            let slice_len = (aumid_len as usize).saturating_sub(1).min(aumid_buf.len());
            let s = String::from_utf16_lossy(&aumid_buf[..slice_len])
                .trim_end_matches('\0')
                .trim()
                .to_string();
            if s.is_empty() { None } else { Some(s) }
        } else {
            None
        };

        let _ = windows::Win32::Foundation::CloseHandle(hproc);
        if !ok || size == 0 {
            return (String::new(), String::new(), aumid);
        }
        let path = String::from_utf16_lossy(&buf[..size as usize]);
        let stem = Path::new(&path)
            .file_stem()
            .and_then(|s| s.to_str())
            .unwrap_or("")
            .to_string();
        (stem, path, aumid)
    }
}

static LAST_DOCK_ACTIVATED_HWND: AtomicIsize = AtomicIsize::new(0);
static LAST_DOCK_ACTIVATED_FG: AtomicIsize = AtomicIsize::new(0);

/// Returns true ONLY if another genuine, titled, opaque, non-topmost, non-tool, non-shell
/// user application window sits above `hwnd` in Z-order and meaningfully overlaps it.
/// Filters out Windows 11 invisible helper/shadow/noactivate/transparent/CoreWindow frames
/// that previously caused intermittent click-to-minimize failures!
unsafe fn is_window_occluded_by_normal_window(hwnd: HWND) -> bool {
    let mut target_rc = RECT::default();
    if DwmGetWindowAttribute(
        hwnd,
        DWMWA_EXTENDED_FRAME_BOUNDS,
        &mut target_rc as *mut _ as *mut _,
        std::mem::size_of::<RECT>() as u32,
    )
    .is_err()
        && GetWindowRect(hwnd, &mut target_rc).is_err()
    {
        return false;
    }
    let mut target_pid = 0u32;
    GetWindowThreadProcessId(hwnd, Some(&mut target_pid));
    let target_root = GetAncestor(hwnd, GA_ROOTOWNER);
    let own_pid = std::process::id();
    let mut cur = windows::Win32::UI::WindowsAndMessaging::GetWindow(
        hwnd,
        windows::Win32::UI::WindowsAndMessaging::GW_HWNDPREV,
    )
    .unwrap_or_default();
    let mut steps = 0usize;
    while !cur.0.is_null() && steps < 64 {
        steps += 1;
        if IsWindowVisible(cur).as_bool() && !IsIconic(cur).as_bool() && !is_native_taskbar_window(cur) {
            let ex = GetWindowLongPtrW(cur, GWL_EXSTYLE) as u32;
            let is_topmost = (ex & WS_EX_TOPMOST.0) != 0;
            let is_tool = (ex & WS_EX_TOOLWINDOW.0) != 0 && (ex & WS_EX_APPWINDOW.0) == 0;
            let is_noactivate = (ex & WS_EX_NOACTIVATE.0) != 0 && (ex & WS_EX_APPWINDOW.0) == 0;
            let is_transparent = (ex & 0x0000_0020 /* WS_EX_TRANSPARENT */) != 0;
            if !is_topmost && !is_tool && !is_noactivate && !is_transparent {
                let mut cpid = 0u32;
                GetWindowThreadProcessId(cur, Some(&mut cpid));
                let cur_root = GetAncestor(cur, GA_ROOTOWNER);
                let same_app = cpid == target_pid || (!target_root.0.is_null() && cur_root == target_root);
                if cpid != 0 && cpid != own_pid && !same_app {
                    // Must have a real non-empty title & must not be a shell/system/IME helper class
                    let title_len = GetWindowTextW(cur, &mut [0u16; 4]);
                    if title_len > 0 {
                        let mut cls_buf = [0u16; 64];
                        let clen = GetClassNameW(cur, &mut cls_buf) as usize;
                        let cls = String::from_utf16_lossy(&cls_buf[..clen]);
                        let is_shell_helper = matches!(
                            cls.as_str(),
                            "Progman"
                                | "WorkerW"
                                | "Windows.UI.Core.CoreWindow"
                                | "Shell_TrayWnd"
                                | "Shell_SecondaryTrayWnd"
                                | "EdgeUiInputTopWndClass"
                                | "ForegroundStaging"
                                | "MultitaskingViewFrame"
                                | "XamlExplorerHostIslandWindow"
                                | "TaskListThumbnailWnd"
                                | "Shell_InputSwitchTopLevelWindow"
                                | "ApplicationManager_DesktopShellWindow"
                        );
                        if !is_shell_helper {
                            let mut cloaked = 0i32;
                            let is_cloaked = DwmGetWindowAttribute(
                                cur,
                                DWMWA_CLOAKED,
                                &mut cloaked as *mut _ as *mut _,
                                4,
                            )
                            .is_ok()
                                && cloaked != 0;
                            if !is_cloaked {
                                let mut rc = RECT::default();
                                let got_rc = DwmGetWindowAttribute(
                                    cur,
                                    DWMWA_EXTENDED_FRAME_BOUNDS,
                                    &mut rc as *mut _ as *mut _,
                                    std::mem::size_of::<RECT>() as u32,
                                )
                                .is_ok()
                                    || GetWindowRect(cur, &mut rc).is_ok();
                                if got_rc && rc.right > rc.left + 120 && rc.bottom > rc.top + 100 {
                                    let overlap_w =
                                        rc.right.min(target_rc.right) - rc.left.max(target_rc.left);
                                    let overlap_h =
                                        rc.bottom.min(target_rc.bottom) - rc.top.max(target_rc.top);
                                    if overlap_w > 48 && overlap_h > 48 {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        cur = windows::Win32::UI::WindowsAndMessaging::GetWindow(
            cur,
            windows::Win32::UI::WindowsAndMessaging::GW_HWNDPREV,
        )
        .unwrap_or_default();
    }
    false
}

pub fn focus_window(hwnd_raw: isize) {
    let hwnd = HWND(hwnd_raw as *mut _);
    crate::hooks_and_overlays::force_foreground_window(hwnd);
    unsafe {
        LAST_DOCK_ACTIVATED_HWND.store(hwnd_raw, Ordering::Relaxed);
        LAST_DOCK_ACTIVATED_FG.store(GetForegroundWindow().0 as isize, Ordering::Relaxed);
    }
}

pub fn activate_or_toggle_window(hwnd_raw: isize) {
    let hwnd = HWND(hwnd_raw as *mut _);
    unsafe {
        let target = crate::hooks_and_overlays::resolve_active_popup_target(hwnd);
        let fg = GetForegroundWindow();
        let fg_raw = fg.0 as isize;
        let root_h = GetAncestor(hwnd, GA_ROOTOWNER);
        let fg_root = if !fg.0.is_null() {
            GetAncestor(fg, GA_ROOTOWNER)
        } else {
            HWND::default()
        };
        let is_fg = fg == hwnd
            || fg == target
            || (!root_h.0.is_null() && fg_root == root_h);

        // Also handle the case where Windows Foreground Lock resisted SetForegroundWindow when we
        // raised `hwnd` on the previous click, yet `hwnd` is physically on top & unobstructed right now!
        let was_last_raised_unchanged = LAST_DOCK_ACTIVATED_HWND.load(Ordering::Relaxed) == hwnd_raw
            && LAST_DOCK_ACTIVATED_FG.load(Ordering::Relaxed) == fg_raw;

        let not_minimized = !IsIconic(hwnd).as_bool() && !IsIconic(target).as_bool();
        if not_minimized
            && (is_fg || was_last_raised_unchanged)
            && !is_window_occluded_by_normal_window(target)
        {
            LAST_DOCK_ACTIVATED_HWND.store(0, Ordering::Relaxed);
            LAST_DOCK_ACTIVATED_FG.store(0, Ordering::Relaxed);
            if target != hwnd {
                let _ = ShowWindowAsync(target, SW_MINIMIZE);
            }
            let _ = ShowWindowAsync(hwnd, SW_MINIMIZE);
        } else {
            crate::hooks_and_overlays::force_foreground_window(hwnd);
            LAST_DOCK_ACTIVATED_HWND.store(hwnd_raw, Ordering::Relaxed);
            LAST_DOCK_ACTIVATED_FG.store(GetForegroundWindow().0 as isize, Ordering::Relaxed);
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

/// Pure, deterministic fullscreen predicate distinguishing true fullscreen videos/games
/// (Chromium/Opera/Chrome/Edge/Firefox HTML5 & F11 fullscreen, VLC, UWP video, D3D/borderless games)
/// from normal maximized apps across both Fixed-Bar (work-area reserved) and Floating/AutoHide modes!
pub fn is_window_fullscreen_on_monitor(
    win_rect: PhysicalRect,
    mon_bounds: PhysicalRect,
    mon_work_area: PhysicalRect,
    is_zoomed: bool,
    d3d_exclusive_fs: bool,
) -> bool {
    let covers_mon = win_rect.x <= mon_bounds.x + 2
        && win_rect.y <= mon_bounds.y + 2
        && win_rect.right() >= mon_bounds.right() - 2
        && win_rect.bottom() >= mon_bounds.bottom() - 2;
    if !covers_mon {
        return false;
    }
    // When Dock or taskbar reserves work area (>=16px inset on any edge), a normal maximized window
    // stops at `mon_work_area` and NEVER covers 100% of `mon_bounds`.
    let work_area_inset = (mon_work_area.x - mon_bounds.x) >= 16
        || (mon_work_area.y - mon_bounds.y) >= 16
        || (mon_bounds.right() - mon_work_area.right()) >= 16
        || (mon_bounds.bottom() - mon_work_area.bottom()) >= 16;
    d3d_exclusive_fs || !is_zoomed || work_area_inset
}

/// Detects per-monitor fullscreen videos/games (including Chrome/Opera/Edge/Firefox HTML5 & F11
/// fullscreen on ANY monitor!) AND SmartDodge dock-zone overlap!
pub fn analyze_fullscreen_and_dodge(
    monitors: &[MonitorDescriptor],
    owned_hwnds: &HashSet<isize>,
    dock_edge: &str,
) -> (HashSet<String>, HashSet<String>) {
    let mut fullscreen_ids = HashSet::new();
    let mut overlapped_ids = HashSet::new();
    let mut seen_top_mon = HashSet::new();

    let fg_hwnd = get_foreground_hwnd();
    let d3d_exclusive_fs = unsafe {
        windows::Win32::UI::Shell::SHQueryUserNotificationState()
            .map(|s| s == windows::Win32::UI::Shell::QUNS_RUNNING_D3D_FULL_SCREEN)
            .unwrap_or(false)
    };

    // Walk all visible top-level windows in strict front-to-back Z-order:
    // - The FIRST normal visible window on each monitor decides whether that monitor is currently
    //   covered by a true fullscreen video/game (works across multi-monitor setups even when the
    //   user clicks another monitor, and NEVER falsely triggers on normal maximized apps!).
    // - Also computes SmartDodge edge overlap for floating/dodge dock mode.
    struct Ctx<'a> {
        own_pid: u32,
        fg_hwnd: isize,
        d3d_exclusive_fs: bool,
        owned: &'a HashSet<isize>,
        monitors: &'a [MonitorDescriptor],
        dock_edge: &'a str,
        fs: &'a mut HashSet<String>,
        seen_top_mon: &'a mut HashSet<String>,
        over: &'a mut HashSet<String>,
    }

    let mut ctx = Ctx {
        own_pid: std::process::id(),
        fg_hwnd,
        d3d_exclusive_fs,
        owned: owned_hwnds,
        monitors,
        dock_edge,
        fs: &mut fullscreen_ids,
        seen_top_mon: &mut seen_top_mon,
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
        if matches!(
            cls.as_str(),
            "Progman"
                | "WorkerW"
                | "Windows.UI.Core.CoreWindow"
                | "Shell_TrayWnd"
                | "Shell_SecondaryTrayWnd"
                | "XamlExplorerHostIslandWindow"
                | "MultitaskingViewFrame"
                | "TaskListThumbnailWnd"
                | "ForegroundStaging"
                | "ApplicationManager_DesktopShellWindow"
                | "Shell_InputSwitchTopLevelWindow"
        ) {
            return BOOL(1);
        }
        let ex_style = GetWindowLongPtrW(hwnd, GWL_EXSTYLE) as u32;
        if (ex_style & WS_EX_TOOLWINDOW.0) != 0
            || (ex_style & WS_EX_NOACTIVATE.0) != 0
            || (ex_style & 0x0000_0020 /* WS_EX_TRANSPARENT */) != 0
        {
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

        // Per-monitor fullscreen check on the topmost normal user window on this monitor!
        // Skip small WS_EX_TOPMOST overlays (PiP, widgets) so they never mask a fullscreen video/game behind them.
        let win_rect = PhysicalRect::new(rc.left, rc.top, rw, rh);
        let is_zoomed = IsZoomed(hwnd).as_bool();
        let is_d3d_fg = raw == ctx.fg_hwnd && ctx.d3d_exclusive_fs;
        let is_fs = is_window_fullscreen_on_monitor(win_rect, mb, mon.work_area, is_zoomed, is_d3d_fg);
        let is_small_topmost_overlay = (ex_style & WS_EX_TOPMOST.0) != 0 && !is_fs;
        if !is_small_topmost_overlay && ctx.seen_top_mon.insert(mon.device_id.clone()) && is_fs {
            ctx.fs.insert(mon.device_id.clone());
        }

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
        (bm.bmWidth.clamp(16, 128), bm.bmHeight.saturating_abs().clamp(16, 128))
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
