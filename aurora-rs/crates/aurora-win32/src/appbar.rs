use std::collections::HashMap;
use std::sync::Once;
use aurora_core::{
    compute_appbar_strip_rect_for_arch, compute_dock_reserved_work_area_for_arch, PhysicalRect,
};
use windows::core::{w, HSTRING, PCWSTR};
use windows::Win32::Foundation::{BOOL, HWND, LPARAM, LRESULT, RECT, WPARAM};
use windows::Win32::Graphics::Dwm::{DwmGetWindowAttribute, DWMWA_CLOAKED, DWMWA_EXTENDED_FRAME_BOUNDS};
use windows::Win32::Graphics::Gdi::{
    GetMonitorInfoW, MonitorFromWindow, HMONITOR, MONITORINFOEXW, MONITOR_DEFAULTTONEAREST,
};
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::UI::Input::KeyboardAndMouse::{EnableWindow, GetAsyncKeyState, VK_MENU};
use windows::Win32::UI::Shell::{
    SHAppBarMessage, ABE_BOTTOM, ABE_LEFT, ABE_RIGHT, ABM_NEW, ABM_QUERYPOS, ABM_REMOVE,
    ABM_SETPOS, ABM_SETSTATE, ABS_AUTOHIDE, APPBARDATA,
};
use windows::Win32::UI::WindowsAndMessaging::{
    CreateWindowExW, DefWindowProcW, DestroyWindow, EnumWindows, FindWindowExW, FindWindowW,
    GetClassNameW, GetWindowLongPtrW, GetWindowRect, GetWindowThreadProcessId, IsIconic, IsWindow,
    IsWindowVisible, IsZoomed, RegisterClassW, RegisterWindowMessageW, SendNotifyMessageW,
    SetLayeredWindowAttributes, SetWindowPos, ShowWindow, ShowWindowAsync, SystemParametersInfoW,
    GWL_EXSTYLE,
    HWND_BROADCAST, HWND_BOTTOM, LWA_ALPHA, SPI_SETWORKAREA, SWP_ASYNCWINDOWPOS, SWP_HIDEWINDOW,
    SWP_NOACTIVATE, SWP_NOMOVE, SWP_NOSENDCHANGING, SWP_NOSIZE, SWP_NOZORDER, SWP_SHOWWINDOW,
    SW_HIDE, SW_SHOW, SW_SHOWNOACTIVATE, SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS, WM_SETTINGCHANGE,
    WNDCLASSW, WS_EX_LAYERED, WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW, WS_EX_TRANSPARENT, WS_POPUP,
};

use crate::monitors::enumerate_monitors;

static APPBAR_CLASS_INIT: Once = Once::new();
const APPBAR_SLOT_CLASS: PCWSTR = w!("ProjectAurora_NativeAppBarSlot");

struct DockAppBarSlot {
    hwnd: HWND,
    #[allow(dead_code)]
    callback_msg: u32,
}

/// Strictly UI-thread-owned Native Win32 Multi-Monitor AppBar & Zero-Peek Taskbar Guard.
/// Solves v2.5.1's ThreadPool HWND creation risk by keeping all AppBar slot HWNDs strictly on
/// the Win32 UI thread with a real custom WndProc handling ABN_POSCHANGED / ABN_FULLSCREENAPP!
pub struct NativeAppBarManager {
    slots: HashMap<String, DockAppBarSlot>,
    saved_work_areas: HashMap<String, RECT>,
    last_signature: Option<String>,
    native_suppressed: bool,
    original_state: i32,
}

impl Default for NativeAppBarManager {
    fn default() -> Self {
        Self::new()
    }
}

impl NativeAppBarManager {
    pub fn new() -> Self {
        Self {
            slots: HashMap::new(),
            saved_work_areas: HashMap::new(),
            last_signature: None,
            native_suppressed: false,
            original_state: 0,
        }
    }

    pub fn is_native_taskbar_suppressed(&self) -> bool {
        self.native_suppressed
    }

    pub fn hide_native_taskbar(&mut self) {
        self.capture_original_work_areas_if_empty();
        unsafe {
            let primary = FindWindowW(w!("Shell_TrayWnd"), None).unwrap_or_default();
            if !primary.0.is_null() {
                let _ = EnableWindow(primary, true);
                let mut abd = APPBARDATA {
                    cbSize: std::mem::size_of::<APPBARDATA>() as u32,
                    hWnd: primary,
                    lParam: LPARAM(ABS_AUTOHIDE as isize),
                    ..Default::default()
                };
                SHAppBarMessage(ABM_SETSTATE, &mut abd);
            }
            suppress_all_tray_windows();
        }
        self.native_suppressed = true;
        self.last_signature = None;
    }

    pub fn enforce_native_taskbar_suppressed(&self, changed_hwnd: Option<HWND>) {
        if !self.native_suppressed {
            return;
        }
        unsafe {
            // Do not send cross-process SW_HIDE to explorer.exe while Alt is held (Alt+Tab switch).
            if (GetAsyncKeyState(VK_MENU.0 as i32) as u16 & 0x8000) != 0 {
                return;
            }
            if let Some(h) = changed_hwnd {
                if !h.0.is_null() && is_native_taskbar_window(h) && IsWindowVisible(h).as_bool() {
                    hide_tray_window(h);
                }
                return;
            }
            for h in enumerate_all_tray_windows() {
                if IsWindowVisible(h).as_bool() {
                    hide_tray_window(h);
                }
            }
        }
    }

    pub fn restore_native_taskbar(&mut self) {
        self.native_suppressed = false;
        self.last_signature = None;
        self.release_all_slots();

        unsafe {
            let trays = enumerate_all_tray_windows();
            for h in &trays {
                let _ = EnableWindow(*h, true);
                let _ = ShowWindow(*h, SW_SHOW);
                let _ = SetWindowPos(
                    *h,
                    None,
                    0,
                    0,
                    0,
                    0,
                    SWP_SHOWWINDOW | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE,
                );
            }
            let primary = FindWindowW(w!("Shell_TrayWnd"), None).unwrap_or_default();
            let mut abd = APPBARDATA {
                cbSize: std::mem::size_of::<APPBARDATA>() as u32,
                hWnd: primary,
                lParam: LPARAM(self.original_state as isize),
                ..Default::default()
            };
            SHAppBarMessage(ABM_SETSTATE, &mut abd);

            if (self.original_state as u32 & ABS_AUTOHIDE) == 0 {
                self.restore_saved_work_areas();
            }
            let _ = SendNotifyMessageW(HWND_BROADCAST, WM_SETTINGCHANGE, WPARAM(0), LPARAM(0));
        }
    }

    /// Applies or releases per-monitor native Win32 AppBar WorkArea slots strictly on UI thread.
    /// Returns `true` when an actual state transition occurred.
    pub fn apply_dock_reservation(
        &mut self,
        dock_edge: &str,
        dock_scale_percent: i32,
        reserve: bool,
        dock_density: &str,
        shell_architecture: &str,
        force_reapply: bool,
    ) -> bool {
        if !self.native_suppressed {
            return false;
        }
        self.capture_original_work_areas_if_empty();
        let monitors = enumerate_monitors();
        if monitors.is_empty() {
            return false;
        }

        let mon_sig: String = monitors
            .iter()
            .map(|m| {
                let b = m.descriptor.bounds;
                format!(
                    "{}:{},{},{}x{}@{}",
                    m.descriptor.device_id, b.x, b.y, b.width, b.height, m.descriptor.effective_dpi
                )
            })
            .collect::<Vec<_>>()
            .join(";");
        let sig = format!("{reserve}|{dock_edge}|{dock_scale_percent}|{dock_density}|{shell_architecture}|{mon_sig}");

        if !force_reapply && self.last_signature.as_deref() == Some(sig.as_str()) {
            let healthy = if !reserve {
                self.slots.is_empty()
                    && monitors.iter().all(|m| {
                        let b = m.descriptor.bounds;
                        try_get_monitor_work_area(m.hmonitor).is_some_and(|live| {
                            live.left == b.x
                                && live.top == b.y
                                && live.right == b.right()
                                && live.bottom == b.bottom()
                        })
                    })
            } else {
                monitors.iter().all(|m| {
                    let slot_ok = self
                        .slots
                        .get(&m.descriptor.device_id)
                        .is_some_and(|s| unsafe { IsWindow(s.hwnd).as_bool() });
                    let desired_wa = compute_dock_reserved_work_area_for_arch(
                        m.descriptor.bounds,
                        m.descriptor.scale_factor(),
                        dock_edge,
                        dock_scale_percent,
                        true,
                        dock_density,
                        shell_architecture,
                    );
                    let wa_ok = try_get_monitor_work_area(m.hmonitor).is_some_and(|live| {
                        let live_pr = PhysicalRect::new(
                            live.left,
                            live.top,
                            live.right - live.left,
                            live.bottom - live.top,
                        );
                        !is_work_area_edge_mismatched(live_pr, desired_wa, dock_edge)
                    });
                    slot_ok && wa_ok
                })
            };
            if healthy {
                return false;
            }
        }
        self.last_signature = Some(sig);

        if !reserve {
            self.release_all_slots();
            unsafe { suppress_all_tray_windows(); }
            for m in &monitors {
                let b = m.descriptor.bounds;
                if let Some(live) = try_get_monitor_work_area(m.hmonitor) {
                    if live.left != b.x || live.top != b.y || live.right != b.right() || live.bottom != b.bottom() {
                        let mut full = RECT { left: b.x, top: b.y, right: b.right(), bottom: b.bottom() };
                        unsafe {
                            let _ = SystemParametersInfoW(
                                SPI_SETWORKAREA,
                                0,
                                Some(&mut full as *mut _ as *mut _),
                                SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS(0),
                            );
                        }
                    }
                }
            }
            return true;
        }

        // Remove unplugged monitors
        let stale: Vec<String> = self
            .slots
            .keys()
            .filter(|k| !monitors.iter().any(|m| m.descriptor.device_id.eq_ignore_ascii_case(k)))
            .cloned()
            .collect();
        for id in stale {
            self.release_slot(&id);
        }

        let edge_u32 = if dock_edge.eq_ignore_ascii_case("Left") {
            ABE_LEFT
        } else if dock_edge.eq_ignore_ascii_case("Right") {
            ABE_RIGHT
        } else {
            ABE_BOTTOM
        };

        for m in &monitors {
            let strip = compute_appbar_strip_rect_for_arch(
                m.descriptor.bounds,
                m.descriptor.scale_factor(),
                dock_edge,
                dock_scale_percent,
                dock_density,
                shell_architecture,
            );
            let desired_wa = compute_dock_reserved_work_area_for_arch(
                m.descriptor.bounds,
                m.descriptor.scale_factor(),
                dock_edge,
                dock_scale_percent,
                true,
                dock_density,
                shell_architecture,
            );
            // If an external bar or prior buggy reservation over-clipped this monitor's work area,
            // reset its work area cleanly to full monitor bounds before re-applying exact strip!
            if let Some(live_before) = try_get_monitor_work_area(m.hmonitor) {
                let over_reserved = match edge_u32 {
                    ABE_LEFT => live_before.left > desired_wa.x + 2,
                    ABE_RIGHT => live_before.right < desired_wa.right() - 2,
                    _ => live_before.bottom < desired_wa.bottom() - 2,
                };
                if over_reserved {
                    let b = m.descriptor.bounds;
                    let mut full = RECT {
                        left: b.x,
                        top: b.y,
                        right: b.right(),
                        bottom: b.bottom(),
                    };
                    unsafe {
                        let _ = SystemParametersInfoW(
                            SPI_SETWORKAREA,
                            0,
                            Some(&mut full as *mut _ as *mut _),
                            SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS(0),
                        );
                    }
                }
            }

            let registered = self.ensure_and_position_slot(&m.descriptor.device_id, m.descriptor.bounds, edge_u32, strip);
            let needs_fallback = if !registered {
                true
            } else if let Some(live) = try_get_monitor_work_area(m.hmonitor) {
                let live_pr = PhysicalRect::new(
                    live.left,
                    live.top,
                    live.right - live.left,
                    live.bottom - live.top,
                );
                is_work_area_edge_mismatched(live_pr, desired_wa, dock_edge)
            } else {
                false
            };
            if needs_fallback {
                let mut rc = RECT {
                    left: desired_wa.x,
                    top: desired_wa.y,
                    right: desired_wa.right(),
                    bottom: desired_wa.bottom(),
                };
                unsafe {
                    let _ = SystemParametersInfoW(
                        SPI_SETWORKAREA,
                        0,
                        Some(&mut rc as *mut _ as *mut _),
                        SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS(0),
                    );
                }
            }
        }

        unsafe { suppress_all_tray_windows(); }
        true
    }

    fn ensure_and_position_slot(&mut self, device_id: &str, mon_bounds: PhysicalRect, u_edge: u32, strip: PhysicalRect) -> bool {
        ensure_appbar_window_class();
        let valid_existing = self
            .slots
            .get(device_id)
            .is_some_and(|s| unsafe { IsWindow(s.hwnd).as_bool() });
        if !valid_existing {
            self.release_slot(device_id);
            unsafe {
                let hinst = GetModuleHandleW(None).unwrap_or_default();
                let title = HSTRING::from(format!("ProjectAurora_DockSlot_{device_id}"));
                let Ok(hwnd) = CreateWindowExW(
                    WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_LAYERED,
                    APPBAR_SLOT_CLASS,
                    &title,
                    WS_POPUP,
                    strip.x,
                    strip.y,
                    strip.width,
                    strip.height,
                    None,
                    None,
                    hinst,
                    None,
                ) else {
                    return false;
                };
                let _ = SetLayeredWindowAttributes(hwnd, windows::Win32::Foundation::COLORREF(0), 0, LWA_ALPHA);
                let _ = ShowWindow(hwnd, SW_SHOWNOACTIVATE);

                let msg_name = HSTRING::from(format!("ProjectAurora_AppBarCb_{:x}", hwnd.0 as usize));
                let cb_msg = RegisterWindowMessageW(&msg_name);
                let mut new_abd = APPBARDATA {
                    cbSize: std::mem::size_of::<APPBARDATA>() as u32,
                    hWnd: hwnd,
                    uCallbackMessage: cb_msg,
                    ..Default::default()
                };
                if SHAppBarMessage(ABM_NEW, &mut new_abd) == 0 {
                    let _ = DestroyWindow(hwnd);
                    return false;
                }
                self.slots.insert(device_id.to_string(), DockAppBarSlot { hwnd, callback_msg: cb_msg });
            }
        }

        let Some(slot) = self.slots.get(device_id) else {
            return false;
        };
        unsafe {
            let mut abd = APPBARDATA {
                cbSize: std::mem::size_of::<APPBARDATA>() as u32,
                hWnd: slot.hwnd,
                uEdge: u_edge,
                rc: RECT {
                    left: mon_bounds.x,
                    top: mon_bounds.y,
                    right: mon_bounds.right(),
                    bottom: mon_bounds.bottom(),
                },
                ..Default::default()
            };
            SHAppBarMessage(ABM_QUERYPOS, &mut abd);
            // Pin directly to the monitor-edge strip so an external taskbar or prior reservation
            // can NEVER cause ABM_QUERYPOS -> ABM_SETPOS to double-stack (e.g. 104px bottom gap)!
            abd.rc = RECT {
                left: strip.x,
                top: strip.y,
                right: strip.right(),
                bottom: strip.bottom(),
            };
            SHAppBarMessage(ABM_SETPOS, &mut abd);
            let _ = SetWindowPos(
                slot.hwnd,
                HWND_BOTTOM,
                abd.rc.left,
                abd.rc.top,
                (abd.rc.right - abd.rc.left).max(1),
                (abd.rc.bottom - abd.rc.top).max(1),
                SWP_NOACTIVATE | SWP_NOSENDCHANGING,
            );
        }
        true
    }

    fn release_slot(&mut self, device_id: &str) {
        if let Some(slot) = self.slots.remove(device_id) {
            unsafe {
                let mut abd = APPBARDATA {
                    cbSize: std::mem::size_of::<APPBARDATA>() as u32,
                    hWnd: slot.hwnd,
                    ..Default::default()
                };
                SHAppBarMessage(ABM_REMOVE, &mut abd);
                if IsWindow(slot.hwnd).as_bool() {
                    let _ = DestroyWindow(slot.hwnd);
                }
            }
        }
    }

    pub fn release_all_slots(&mut self) {
        let keys: Vec<String> = self.slots.keys().cloned().collect();
        for k in keys {
            self.release_slot(&k);
        }
    }

    fn capture_original_work_areas_if_empty(&mut self) {
        if !self.saved_work_areas.is_empty() {
            return;
        }
        for m in enumerate_monitors() {
            let b = m.descriptor.bounds;
            let w = m.descriptor.work_area;
            if w != b {
                self.saved_work_areas.insert(
                    m.descriptor.device_id,
                    RECT { left: w.x, top: w.y, right: w.right(), bottom: w.bottom() },
                );
            }
        }
    }

    fn restore_saved_work_areas(&mut self) {
        for (_, mut rc) in self.saved_work_areas.drain() {
            unsafe {
                let _ = SystemParametersInfoW(
                    SPI_SETWORKAREA,
                    0,
                    Some(&mut rc as *mut _ as *mut _),
                    SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS(0),
                );
            }
        }
    }

    /// One-shot post-transition check using DWMWA_EXTENDED_FRAME_BOUNDS so Electron/WhatsApp/Antigravity
    /// windows snap cleanly ONCE above the Fixed Bar without ever oscillating!
    pub fn refit_mismatched_maximized_windows_once(&self) {
        if !self.native_suppressed {
            return;
        }
        unsafe {
            let own_pid = std::process::id();
            let _ = EnumWindows(Some(refit_enum_proc), LPARAM(own_pid as isize));
        }
    }
}

impl Drop for NativeAppBarManager {
    fn drop(&mut self) {
        if self.native_suppressed {
            self.restore_native_taskbar();
        } else {
            self.release_all_slots();
        }
    }
}

unsafe extern "system" fn refit_enum_proc(hwnd: HWND, lparam: LPARAM) -> BOOL {
    let own_pid = lparam.0 as u32;
    if !IsWindowVisible(hwnd).as_bool() || IsIconic(hwnd).as_bool() || !IsZoomed(hwnd).as_bool() {
        return BOOL(1);
    }
    let mut pid = 0u32;
    GetWindowThreadProcessId(hwnd, Some(&mut pid));
    if pid == 0 || pid == own_pid || is_native_taskbar_window(hwnd) {
        return BOOL(1);
    }
    let ex = GetWindowLongPtrW(hwnd, GWL_EXSTYLE) as u32;
    if (ex & WS_EX_TOOLWINDOW.0) != 0 {
        return BOOL(1);
    }
    let mut cloaked = 0i32;
    if DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, &mut cloaked as *mut _ as *mut _, 4).is_ok() && cloaked != 0 {
        return BOOL(1);
    }
    let hmon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
    let Some(wa) = try_get_monitor_work_area(hmon) else {
        return BOOL(1);
    };
    let mut win_rc = RECT::default();
    if GetWindowRect(hwnd, &mut win_rc).is_err() {
        return BOOL(1);
    }
    let mut ext_rc = RECT::default();
    let has_dwm = DwmGetWindowAttribute(
        hwnd,
        DWMWA_EXTENDED_FRAME_BOUNDS,
        &mut ext_rc as *mut _ as *mut _,
        std::mem::size_of::<RECT>() as u32,
    )
    .is_ok()
        && ext_rc.right > ext_rc.left
        && ext_rc.bottom > ext_rc.top;
    let vis = if has_dwm { ext_rc } else { win_rc };

    let aligned = (vis.left - wa.left).abs() <= 4
        && (vis.top - wa.top).abs() <= 4
        && (vis.right - wa.right).abs() <= 4
        && (vis.bottom - wa.bottom).abs() <= 4;
    if aligned {
        return BOOL(1);
    }

    let pad_l = if has_dwm { (vis.left - win_rc.left).clamp(0, 20) } else { 0 };
    let pad_t = if has_dwm { (vis.top - win_rc.top).clamp(0, 20) } else { 0 };
    let pad_r = if has_dwm { (win_rc.right - vis.right).clamp(0, 20) } else { 0 };
    let pad_b = if has_dwm { (win_rc.bottom - vis.bottom).clamp(0, 20) } else { 0 };

    let _ = SendNotifyMessageW(hwnd, WM_SETTINGCHANGE, WPARAM(SPI_SETWORKAREA.0 as usize), LPARAM(0));
    let _ = SetWindowPos(
        hwnd,
        None,
        wa.left - pad_l,
        wa.top - pad_t,
        (wa.right - wa.left) + pad_l + pad_r,
        (wa.bottom - wa.top) + pad_t + pad_b,
        SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS,
    );
    BOOL(1)
}

fn ensure_appbar_window_class() {
    APPBAR_CLASS_INIT.call_once(|| unsafe {
        let hinst = GetModuleHandleW(None).unwrap_or_default();
        let wc = WNDCLASSW {
            lpfnWndProc: Some(appbar_slot_wndproc),
            hInstance: hinst.into(),
            lpszClassName: APPBAR_SLOT_CLASS,
            ..Default::default()
        };
        RegisterClassW(&wc);
    });
}

unsafe extern "system" fn appbar_slot_wndproc(hwnd: HWND, msg: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    DefWindowProcW(hwnd, msg, wparam, lparam)
}

fn try_get_monitor_work_area(hmon: HMONITOR) -> Option<RECT> {
    if hmon.0.is_null() {
        return None;
    }
    unsafe {
        let mut mi = MONITORINFOEXW::default();
        mi.monitorInfo.cbSize = std::mem::size_of::<MONITORINFOEXW>() as u32;
        if GetMonitorInfoW(hmon, &mut mi.monitorInfo as *mut _ as *mut _).as_bool() {
            Some(mi.monitorInfo.rcWork)
        } else {
            None
        }
    }
}

/// Enumerates primary and secondary Windows taskbar handles.
///
/// # Safety
/// Calls Win32 window enumeration APIs (`FindWindowW`, `FindWindowExW`).
pub unsafe fn enumerate_all_tray_windows() -> Vec<HWND> {
    let mut list = Vec::with_capacity(4);
    if let Ok(primary) = FindWindowW(w!("Shell_TrayWnd"), None) {
        if !primary.0.is_null() {
            list.push(primary);
        }
    }
    let mut sec = HWND::default();
    while let Ok(next) = FindWindowExW(HWND::default(), sec, w!("Shell_SecondaryTrayWnd"), None) {
        if next.0.is_null() {
            break;
        }
        list.push(next);
        sec = next;
    }
    list
}

/// Checks whether a given HWND belongs to the Windows native taskbar.
///
/// # Safety
/// Caller must pass a valid or null `HWND`.
pub unsafe fn is_native_taskbar_window(hwnd: HWND) -> bool {
    let mut buf = [0u16; 64];
    let len = GetClassNameW(hwnd, &mut buf) as usize;
    if len == 0 {
        return false;
    }
    let cls = String::from_utf16_lossy(&buf[..len]);
    cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd"
}

unsafe fn hide_tray_window(hwnd: HWND) {
    if hwnd.0.is_null() {
        return;
    }
    let _ = ShowWindowAsync(hwnd, SW_HIDE);
    let _ = SetWindowPos(
        hwnd,
        HWND_BOTTOM,
        0,
        0,
        0,
        0,
        SWP_HIDEWINDOW | SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS,
    );
}

unsafe fn suppress_all_tray_windows() {
    for h in enumerate_all_tray_windows() {
        hide_tray_window(h);
    }
}

/// Checks whether a live monitor WorkArea deviates (either over-reserved OR under-reserved)
/// from Aurora's desired pixel-flush dock WorkArea by more than 2 pixels.
pub fn is_work_area_edge_mismatched(
    live: PhysicalRect,
    desired: PhysicalRect,
    dock_edge: &str,
) -> bool {
    if dock_edge.eq_ignore_ascii_case("Left") {
        (live.x - desired.x).abs() > 2
    } else if dock_edge.eq_ignore_ascii_case("Right") {
        (live.right() - desired.right()).abs() > 2
    } else {
        (live.bottom() - desired.bottom()).abs() > 2
    }
}

/// Standalone `--restore-taskbar` recovery helper (invoked by detached crash watchdog or user shortcut).
pub fn restore_taskbar_standalone() {
    let mut mgr = NativeAppBarManager::new();
    mgr.restore_native_taskbar();
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn detects_both_over_and_under_reserved_work_area() {
        let desired_bottom = PhysicalRect::new(0, 0, 2560, 1388); // 52px bottom dock
        let over_reserved = PhysicalRect::new(0, 0, 2560, 1336);  // 104px gap bug!
        let under_reserved = PhysicalRect::new(0, 0, 2560, 1440); // 0px reserved
        let exact = PhysicalRect::new(0, 0, 2560, 1388);

        assert!(is_work_area_edge_mismatched(over_reserved, desired_bottom, "Bottom"));
        assert!(is_work_area_edge_mismatched(under_reserved, desired_bottom, "Bottom"));
        assert!(!is_work_area_edge_mismatched(exact, desired_bottom, "Bottom"));

        let desired_left = PhysicalRect::new(58, 0, 2502, 1440);
        let over_left = PhysicalRect::new(116, 0, 2444, 1440);
        assert!(is_work_area_edge_mismatched(over_left, desired_left, "Left"));
        assert!(!is_work_area_edge_mismatched(desired_left, desired_left, "Left"));

        let desired_right = PhysicalRect::new(0, 0, 2502, 1440);
        let over_right = PhysicalRect::new(0, 0, 2444, 1440);
        assert!(is_work_area_edge_mismatched(over_right, desired_right, "Right"));
        assert!(!is_work_area_edge_mismatched(desired_right, desired_right, "Right"));
    }
}
