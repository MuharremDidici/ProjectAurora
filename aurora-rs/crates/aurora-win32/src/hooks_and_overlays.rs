use std::sync::atomic::{AtomicBool, AtomicIsize, AtomicU32, Ordering};
use aurora_core::PhysicalRect;
use crossbeam_channel::Sender;
use parking_lot::Mutex;
use windows::core::w;
use windows::Win32::Foundation::{BOOL, HWND, LPARAM, LRESULT, POINT, RECT, SIZE, WPARAM};
use windows::Win32::Graphics::Dwm::{
    DwmQueryThumbnailSourceSize, DwmRegisterThumbnail, DwmSetWindowAttribute,
    DwmUnregisterThumbnail, DwmUpdateThumbnailProperties, DWMWA_WINDOW_CORNER_PREFERENCE,
    DWM_THUMBNAIL_PROPERTIES, DWM_TNP_OPACITY, DWM_TNP_RECTDESTINATION, DWM_TNP_SOURCECLIENTAREAONLY,
    DWM_TNP_VISIBLE, DWMWCP_ROUND,
};
use windows::Win32::Graphics::Gdi::{
    GetMonitorInfoW, MonitorFromWindow, MONITORINFO, MONITOR_DEFAULTTONEAREST,
};
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::System::Threading::GetCurrentThreadId;
use windows::Win32::UI::Accessibility::{SetWinEventHook, UnhookWinEvent, HWINEVENTHOOK};
use windows::Win32::UI::Input::KeyboardAndMouse::{
    GetAsyncKeyState, IsWindowEnabled, RegisterHotKey, UnregisterHotKey, HOT_KEY_MODIFIERS,
    MOD_ALT, MOD_CONTROL, MOD_NOREPEAT, MOD_SHIFT, VK_CONTROL, VK_LBUTTON, VK_LMENU, VK_RBUTTON,
    VK_RMENU, VK_SHIFT,
};
use windows::Win32::UI::Shell::{DragFinish, DragQueryFileW, HDROP};
use windows::Win32::UI::WindowsAndMessaging::{
    AllowSetForegroundWindow, BringWindowToTop, CallNextHookEx, CreateWindowExW, DefWindowProcW,
    DestroyWindow, GetAncestor, GetClassNameW, GetForegroundWindow, GetLastActivePopup,
    GetMessageW, GetWindow, GetWindowLongPtrW, GetWindowRect, GetWindowThreadProcessId,
    IsHungAppWindow, IsIconic, IsWindow, IsWindowVisible, IsZoomed, PostThreadMessageW,
    RegisterClassW, SetForegroundWindow, SetWindowPos, SetWindowsHookExW, ShowWindow,
    ShowWindowAsync, SwitchToThisWindow, UnhookWindowsHookEx, ASFW_ANY, EVENT_OBJECT_CREATE,
    EVENT_OBJECT_DESTROY, EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_NAMECHANGE, EVENT_OBJECT_SHOW,
    EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_MINIMIZEEND, EVENT_SYSTEM_MINIMIZESTART,
    EVENT_SYSTEM_MOVESIZEEND, EVENT_SYSTEM_MOVESIZESTART, GA_ROOT, GA_ROOTOWNER, GWL_EXSTYLE,
    GW_OWNER, HWND_NOTOPMOST, HWND_TOPMOST, MSLLHOOKSTRUCT, MSG, SWP_NOACTIVATE, SWP_NOMOVE,
    SWP_NOSIZE, SWP_SHOWWINDOW, SW_HIDE, SW_RESTORE, SW_SHOW, WH_MOUSE_LL, WINEVENT_OUTOFCONTEXT,
    WINEVENT_SKIPOWNPROCESS, WM_LBUTTONDOWN, WM_LBUTTONUP, WM_MOUSEMOVE, WM_QUIT, WM_RBUTTONDOWN,
    WM_RBUTTONUP, WNDCLASSW, WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW, WS_EX_TOPMOST, WS_EX_TRANSPARENT,
    WS_POPUP,
};

use crate::hardware_osd::post_wake_ui;

#[derive(Debug, Clone)]
pub enum ShellHookEvent {
    WindowCatalogChanged { foreground_hwnd: isize },
    ObjectShown { hwnd: isize },
    MoveSizeStarted { hwnd: isize },
    MoveSizeEnded { hwnd: isize },
    ShiftDragMoved { hwnd: isize, px: i32, py: i32 },
    ShiftDragDropped { hwnd: isize, px: i32, py: i32 },
    AltDragMove { hwnd: isize, x: i32, y: i32, w: i32, h: i32 },
    ExternalMouseClick { px: i32, py: i32 },
}

static EVENT_TX: Mutex<Option<Sender<ShellHookEvent>>> = Mutex::new(None);
static MOUSE_WAKE_HWND: AtomicIsize = AtomicIsize::new(0);
static ALT_DRAG_ENABLED: AtomicBool = AtomicBool::new(true);
static SNAP_ZONES_ENABLED: AtomicBool = AtomicBool::new(true);
static WATCH_EXTERNAL_CLICKS: AtomicBool = AtomicBool::new(false);
static LAST_FG_LOC_TICK: AtomicU32 = AtomicU32::new(0);

#[derive(Default)]
struct AltDragState {
    mode: u8, // 0=idle, 1=move, 2=resize
    hwnd: isize,
    start_x: i32,
    start_y: i32,
    orig_rc: RECT,
}

static ALT_STATE: Mutex<AltDragState> = Mutex::new(AltDragState {
    mode: 0,
    hwnd: 0,
    start_x: 0,
    start_y: 0,
    orig_rc: RECT { left: 0, top: 0, right: 0, bottom: 0 },
});

static SHIFT_DRAG_HWND: AtomicIsize = AtomicIsize::new(0);

pub fn set_alt_drag_enabled(enabled: bool) {
    ALT_DRAG_ENABLED.store(enabled, Ordering::Relaxed);
    if !enabled {
        let mut st = ALT_STATE.lock();
        st.mode = 0;
        st.hwnd = 0;
    }
}

pub fn set_snap_zones_enabled(enabled: bool) {
    SNAP_ZONES_ENABLED.store(enabled, Ordering::Relaxed);
}

/// Enables zero-overhead read-only click observation ONLY while a Crown popup is open, so clicking
/// outside ALWAYS dismisses the popup on click #1 even when Windows Foreground Lock is active!
pub fn set_watch_external_clicks(active: bool) {
    WATCH_EXTERNAL_CLICKS.store(active, Ordering::Relaxed);
}

/// Event-driven WinEventHook listener + dedicated `aurora-mouse-hook` thread for `WH_MOUSE_LL`
/// (zero-polling foreground/create/destroy/title/minimize/movesize & immune to hook timeouts!).
pub struct ShellEventHooks {
    win_hooks: Vec<HWINEVENTHOOK>,
    mouse_tid: Option<u32>,
    mouse_join: Option<std::thread::JoinHandle<()>>,
    pub mouse_hook_ok: bool,
}

impl ShellEventHooks {
    pub fn install(tx: Sender<ShellHookEvent>, wake_hwnd: isize) -> Self {
        MOUSE_WAKE_HWND.store(wake_hwnd, Ordering::Relaxed);
        *EVENT_TX.lock() = Some(tx);
        let mut win_hooks = Vec::new();
        let ranges = [
            (EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND),
            (EVENT_SYSTEM_MOVESIZESTART, EVENT_SYSTEM_MOVESIZEEND),
            (EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZEEND),
            (EVENT_OBJECT_CREATE, EVENT_OBJECT_SHOW),
            (EVENT_OBJECT_NAMECHANGE, EVENT_OBJECT_NAMECHANGE),
            (EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE),
        ];
        unsafe {
            for (lo, hi) in ranges {
                let h = SetWinEventHook(
                    lo,
                    hi,
                    None,
                    Some(win_event_proc),
                    0,
                    0,
                    WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS,
                );
                if !h.0.is_null() {
                    win_hooks.push(h);
                }
            }
        }

        let (ready_tx, ready_rx) = crossbeam_channel::bounded::<(u32, bool)>(1);
        let mut mouse_tid = None;
        let mut mouse_join = None;
        let mut mouse_hook_ok = false;

        if let Ok(jh) = std::thread::Builder::new()
            .name("aurora-mouse-hook".into())
            .spawn(move || unsafe {
                let tid = GetCurrentThreadId();
                let hhook = SetWindowsHookExW(WH_MOUSE_LL, Some(low_level_mouse_proc), None, 0).ok();
                let ok = hhook.is_some_and(|h| !h.0.is_null());
                let _ = ready_tx.send((tid, ok));
                if let Some(h) = hhook.filter(|h| !h.0.is_null()) {
                    let mut msg = MSG::default();
                    while GetMessageW(&mut msg, None, 0, 0).0 > 0 {}
                    let _ = UnhookWindowsHookEx(h);
                }
            })
        {
            if let Ok((tid, ok)) = ready_rx.recv() {
                if ok {
                    mouse_tid = Some(tid);
                    mouse_join = Some(jh);
                    mouse_hook_ok = true;
                } else {
                    let _ = jh.join();
                }
            } else {
                let _ = jh.join();
            }
        }

        Self { win_hooks, mouse_tid, mouse_join, mouse_hook_ok }
    }
}

impl Drop for ShellEventHooks {
    fn drop(&mut self) {
        unsafe {
            for h in self.win_hooks.drain(..) {
                let _ = UnhookWinEvent(h);
            }
            if let Some(tid) = self.mouse_tid.take() {
                let _ = PostThreadMessageW(tid, WM_QUIT, WPARAM(0), LPARAM(0));
            }
        }
        if let Some(jh) = self.mouse_join.take() {
            let _ = jh.join();
        }
        *EVENT_TX.lock() = None;
        MOUSE_WAKE_HWND.store(0, Ordering::Relaxed);
    }
}

unsafe extern "system" fn win_event_proc(
    _hook: HWINEVENTHOOK,
    event: u32,
    hwnd: HWND,
    id_object: i32,
    id_child: i32,
    _thread: u32,
    _time: u32,
) {
    if id_object != 0 || id_child != 0 || hwnd.0.is_null() {
        return;
    }
    let h = hwnd.0 as isize;
    // Fast lock-free filter for high-frequency LOCATIONCHANGE events:
    // - Forward Shift+Drag snap moves immediately.
    // - Also wake fullscreen/dodge detector (throttled to 120ms) when the foreground window resizes
    //   so entering/exiting fullscreen YouTube/Netflix/VLC hides & restores Dock instantaneously!
    if event == EVENT_OBJECT_LOCATIONCHANGE {
        if SNAP_ZONES_ENABLED.load(Ordering::Relaxed)
            && SHIFT_DRAG_HWND.load(Ordering::Relaxed) == h
        {
            let shift_down = (GetAsyncKeyState(VK_SHIFT.0 as i32) as u16 & 0x8000) != 0;
            if shift_down {
                if let Some(guard) = EVENT_TX.try_lock() {
                    if let Some(tx) = guard.as_ref() {
                        let mut pt = POINT::default();
                        let _ = windows::Win32::UI::WindowsAndMessaging::GetCursorPos(&mut pt);
                        let _ = tx.try_send(ShellHookEvent::ShiftDragMoved { hwnd: h, px: pt.x, py: pt.y });
                    }
                }
            }
            return;
        }
        if hwnd == GetForegroundWindow() {
            let prev = LAST_FG_LOC_TICK.load(Ordering::Relaxed);
            if _time.wrapping_sub(prev) >= 120 {
                LAST_FG_LOC_TICK.store(_time, Ordering::Relaxed);
                if let Some(guard) = EVENT_TX.try_lock() {
                    if let Some(tx) = guard.as_ref() {
                        let _ = tx.try_send(ShellHookEvent::WindowCatalogChanged { foreground_hwnd: h });
                        post_wake_ui(MOUSE_WAKE_HWND.load(Ordering::Relaxed));
                    }
                }
            }
        }
        return;
    }
    let Some(guard) = EVENT_TX.try_lock() else { return };
    let Some(tx) = guard.as_ref() else { return };
    match event {
        EVENT_SYSTEM_FOREGROUND => {
            let _ = tx.try_send(ShellHookEvent::WindowCatalogChanged { foreground_hwnd: h });
        }
        EVENT_SYSTEM_MOVESIZESTART => {
            SHIFT_DRAG_HWND.store(h, Ordering::Relaxed);
            let _ = tx.try_send(ShellHookEvent::MoveSizeStarted { hwnd: h });
        }
        EVENT_SYSTEM_MOVESIZEEND => {
            let prev = SHIFT_DRAG_HWND.swap(0, Ordering::Relaxed);
            let _ = tx.try_send(ShellHookEvent::MoveSizeEnded {
                hwnd: if prev != 0 { prev } else { h },
            });
        }
        EVENT_OBJECT_SHOW => {
            let _ = tx.try_send(ShellHookEvent::ObjectShown { hwnd: h });
        }
        EVENT_SYSTEM_MINIMIZESTART
        | EVENT_SYSTEM_MINIMIZEEND
        | EVENT_OBJECT_CREATE
        | EVENT_OBJECT_DESTROY
        | EVENT_OBJECT_NAMECHANGE => {
            let _ = tx.try_send(ShellHookEvent::WindowCatalogChanged { foreground_hwnd: 0 });
        }
        _ => {}
    }
}

unsafe extern "system" fn low_level_mouse_proc(code: i32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    if code >= 0 && lparam.0 != 0 {
        let info = &*(lparam.0 as *const MSLLHOOKSTRUCT);
        let msg = wparam.0 as u32;

        // 0. Read-only click observation while a Crown popup is open (NEVER swallows the click!).
        //    Allows 1-click outside-dismiss even when Windows Foreground Lock blocked SetForegroundWindow!
        if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN)
            && WATCH_EXTERNAL_CLICKS.load(Ordering::Relaxed)
        {
            if let Some(guard) = EVENT_TX.try_lock() {
                if let Some(tx) = guard.as_ref() {
                    let _ = tx.try_send(ShellHookEvent::ExternalMouseClick {
                        px: info.pt.x,
                        py: info.pt.y,
                    });
                    post_wake_ui(MOUSE_WAKE_HWND.load(Ordering::Relaxed));
                }
            }
        }

        // 1. Shift+Drag drop detection for Snap Zones (non-blocking, NEVER swallows WM_LBUTTONUP!)
        if msg == WM_LBUTTONUP {
            let drag_hwnd = SHIFT_DRAG_HWND.swap(0, Ordering::Relaxed);
            if drag_hwnd != 0 && SNAP_ZONES_ENABLED.load(Ordering::Relaxed) {
                let shift_down = (GetAsyncKeyState(VK_SHIFT.0 as i32) as u16 & 0x8000) != 0;
                if let Some(guard) = EVENT_TX.try_lock() {
                    if let Some(tx) = guard.as_ref() {
                        if shift_down {
                            let _ = tx.try_send(ShellHookEvent::ShiftDragDropped {
                                hwnd: drag_hwnd,
                                px: info.pt.x,
                                py: info.pt.y,
                            });
                        } else {
                            let _ = tx.try_send(ShellHookEvent::MoveSizeEnded { hwnd: drag_hwnd });
                        }
                        post_wake_ui(MOUSE_WAKE_HWND.load(Ordering::Relaxed));
                    }
                }
            }
        }

        // 2. Industrial-safe Left-Alt+Drag ONLY (STRICTLY excludes Turkish AltGr / VK_RMENU & Ctrl+Alt!).
        //    ZERO blocking SetWindowPos inside WH_MOUSE_LL; NEVER swallows clicks if AltGr/Ctrl is active!
        if ALT_DRAG_ENABLED.load(Ordering::Relaxed) {
            let lalt_down = (GetAsyncKeyState(VK_LMENU.0 as i32) as u16 & 0x8000) != 0;
            let ralt_down = (GetAsyncKeyState(VK_RMENU.0 as i32) as u16 & 0x8000) != 0;
            let ctrl_down = (GetAsyncKeyState(VK_CONTROL.0 as i32) as u16 & 0x8000) != 0;
            let pure_left_alt = is_pure_left_alt_drag(lalt_down, ralt_down, ctrl_down);

            match msg {
                WM_LBUTTONDOWN | WM_RBUTTONDOWN if pure_left_alt => {
                    let target = windows::Win32::UI::WindowsAndMessaging::WindowFromPoint(info.pt);
                    let root = GetAncestor(target, GA_ROOT);
                    if !root.0.is_null() && !IsZoomed(root).as_bool() {
                        let mut pid = 0u32;
                        GetWindowThreadProcessId(root, Some(&mut pid));
                        if pid != 0 && pid != std::process::id() {
                            let mut cls_buf = [0u16; 64];
                            let clen = GetClassNameW(root, &mut cls_buf) as usize;
                            let cls = String::from_utf16_lossy(&cls_buf[..clen]);
                            if !is_forbidden_alt_drag_class(&cls) {
                                let mut rc = RECT::default();
                                if GetWindowRect(root, &mut rc).is_ok() {
                                    // Never intercept Left-Alt+Click on borderless fullscreen windows/games!
                                    let hmon = MonitorFromWindow(root, MONITOR_DEFAULTTONEAREST);
                                    let mut mi = MONITORINFO {
                                        cbSize: std::mem::size_of::<MONITORINFO>() as u32,
                                        ..Default::default()
                                    };
                                    let is_fullscreen = GetMonitorInfoW(hmon, &mut mi).as_bool()
                                        && rc.left <= mi.rcMonitor.left + 2
                                        && rc.top <= mi.rcMonitor.top + 2
                                        && rc.right >= mi.rcMonitor.right - 2
                                        && rc.bottom >= mi.rcMonitor.bottom - 2;
                                    if !is_fullscreen {
                                        if let Some(mut st) = ALT_STATE.try_lock() {
                                            st.mode = if msg == WM_LBUTTONDOWN { 1 } else { 2 };
                                            st.hwnd = root.0 as isize;
                                            st.start_x = info.pt.x;
                                            st.start_y = info.pt.y;
                                            st.orig_rc = rc;
                                            return LRESULT(1);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                WM_MOUSEMOVE => {
                    if let Some(mut st) = ALT_STATE.try_lock() {
                        if st.mode != 0 && st.hwnd != 0 {
                            let btn_still_down = if st.mode == 1 {
                                (GetAsyncKeyState(VK_LBUTTON.0 as i32) as u16 & 0x8000) != 0
                            } else {
                                (GetAsyncKeyState(VK_RBUTTON.0 as i32) as u16 & 0x8000) != 0
                            };
                            if !pure_left_alt || !btn_still_down {
                                st.mode = 0;
                                st.hwnd = 0;
                            } else {
                                let dx = info.pt.x - st.start_x;
                                let dy = info.pt.y - st.start_y;
                                let (nx, ny, nw, nh) = if st.mode == 1 {
                                    (
                                        st.orig_rc.left + dx,
                                        st.orig_rc.top + dy,
                                        st.orig_rc.right - st.orig_rc.left,
                                        st.orig_rc.bottom - st.orig_rc.top,
                                    )
                                } else {
                                    (
                                        st.orig_rc.left,
                                        st.orig_rc.top,
                                        (st.orig_rc.right - st.orig_rc.left + dx).max(240),
                                        (st.orig_rc.bottom - st.orig_rc.top + dy).max(160),
                                    )
                                };
                                let drag_hwnd = st.hwnd;
                                drop(st);
                                // Dispatch window move off the WH_MOUSE_LL hook thread (<1 µs hook return!)
                                if let Some(guard) = EVENT_TX.try_lock() {
                                    if let Some(tx) = guard.as_ref() {
                                        let _ = tx.try_send(ShellHookEvent::AltDragMove {
                                            hwnd: drag_hwnd,
                                            x: nx,
                                            y: ny,
                                            w: nw,
                                            h: nh,
                                        });
                                        post_wake_ui(MOUSE_WAKE_HWND.load(Ordering::Relaxed));
                                    }
                                }
                            }
                        }
                    }
                }
                WM_LBUTTONUP | WM_RBUTTONUP => {
                    if let Some(mut st) = ALT_STATE.try_lock() {
                        if (msg == WM_LBUTTONUP && st.mode == 1)
                            || (msg == WM_RBUTTONUP && st.mode == 2)
                        {
                            st.mode = 0;
                            st.hwnd = 0;
                            return LRESULT(1);
                        } else if st.mode != 0 && !pure_left_alt {
                            st.mode = 0;
                            st.hwnd = 0;
                        }
                    }
                }
                _ => {}
            }
        }
    }
    CallNextHookEx(None, code, wparam, lparam)
}

/// Global Hotkey IDs registered on Aurora's primary message HWND.
pub const HOTKEY_OMNI_SEARCH: i32 = 0xA001;
pub const HOTKEY_CROWN_DECK: i32 = 0xA002;
pub const HOTKEY_OCR_SNIP: i32 = 0xA003;
pub const HOTKEY_EYEDROPPER: i32 = 0xA004;
pub const HOTKEY_GIF_RECORDER: i32 = 0xA005;

pub fn register_aurora_hotkeys(hwnd: HWND, omni_preset: &str) -> Vec<(&'static str, bool)> {
    unsafe {
        let _ = UnregisterHotKey(hwnd, HOTKEY_OMNI_SEARCH);
        let (mods, vk): (HOT_KEY_MODIFIERS, u32) = match omni_preset {
            "Ctrl+Space" => (MOD_CONTROL | MOD_NOREPEAT, 0x20),
            "Ctrl+Shift+Space" => (MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, 0x20),
            _ => (MOD_ALT | MOD_NOREPEAT, 0x20),
        };
        let r_omni = RegisterHotKey(hwnd, HOTKEY_OMNI_SEARCH, mods, vk).is_ok();
        let r_deck = RegisterHotKey(hwnd, HOTKEY_CROWN_DECK, MOD_ALT | MOD_SHIFT | MOD_NOREPEAT, b'A' as u32).is_ok();
        let r_ocr = RegisterHotKey(hwnd, HOTKEY_OCR_SNIP, MOD_ALT | MOD_SHIFT | MOD_NOREPEAT, b'S' as u32).is_ok();
        let r_eye = RegisterHotKey(hwnd, HOTKEY_EYEDROPPER, MOD_ALT | MOD_SHIFT | MOD_NOREPEAT, b'C' as u32).is_ok();
        let (gif_label, r_gif) =
            if RegisterHotKey(hwnd, HOTKEY_GIF_RECORDER, MOD_ALT | MOD_SHIFT | MOD_NOREPEAT, b'R' as u32).is_ok() {
                ("GifRec", true)
            } else if RegisterHotKey(
                hwnd,
                HOTKEY_GIF_RECORDER,
                MOD_CONTROL | MOD_ALT | MOD_SHIFT | MOD_NOREPEAT,
                b'R' as u32,
            )
            .is_ok()
            {
                ("GifRec(Ctrl+Alt+Shift+R)", true)
            } else {
                ("GifRec", false)
            };
        vec![
            ("OmniSearch", r_omni),
            ("CrownDeck", r_deck),
            ("OCR", r_ocr),
            ("Eyedropper", r_eye),
            (gif_label, r_gif),
        ]
    }
}

/// Returns true ONLY for pure Left-Alt drag — strictly excluding Turkish `AltGr` (`VK_RMENU`)
/// and `Ctrl+Alt` chords so typing `@`, `{`, `}`, `[`, `]`, `\` can NEVER hijack mouse clicks!
pub fn is_pure_left_alt_drag(lalt_down: bool, ralt_down: bool, ctrl_down: bool) -> bool {
    lalt_down && !ralt_down && !ctrl_down
}

/// System desktop, taskbar, Alt+Tab switcher, and language-bar classes that Alt+Drag must NEVER intercept.
pub fn is_forbidden_alt_drag_class(cls: &str) -> bool {
    matches!(
        cls,
        "Progman"
            | "WorkerW"
            | "Shell_TrayWnd"
            | "Shell_SecondaryTrayWnd"
            | "MultitaskingViewFrame"
            | "XamlExplorerHostIslandWindow"
            | "Windows.UI.Core.CoreWindow"
            | "TaskListThumbnailWnd"
            | "ForegroundStaging"
            | "ApplicationManager_DesktopShellWindow"
            | "Shell_InputSwitchTopLevelWindow"
    )
}

/// Returns true if `hwnd_raw` is `owner` itself or an owned popup (such as a WebView2 `<select>` or
/// `<input type="color">` picker spawned from `owner`).
pub fn is_window_owned_by(hwnd_raw: isize, owner: HWND) -> bool {
    if hwnd_raw == 0 || owner.0.is_null() {
        return false;
    }
    let hwnd = HWND(hwnd_raw as *mut _);
    if hwnd == owner {
        return true;
    }
    unsafe {
        if GetAncestor(hwnd, GA_ROOTOWNER) == owner {
            return true;
        }
        GetWindow(hwnd, GW_OWNER).map(|o| o == owner).unwrap_or(false)
    }
}

/// Returns true if screen point `(px, py)` hits `owner` itself or any owned child/popup of `owner`.
pub fn is_point_inside_window_or_owned(px: i32, py: i32, owner: HWND) -> bool {
    if owner.0.is_null() {
        return false;
    }
    unsafe {
        let hit = windows::Win32::UI::WindowsAndMessaging::WindowFromPoint(POINT { x: px, y: py });
        if hit.0.is_null() {
            return false;
        }
        if hit == owner {
            return true;
        }
        let root = GetAncestor(hit, GA_ROOT);
        if root == owner || is_window_owned_by(root.0 as isize, owner) {
            return true;
        }
        // Also cover in-process child windows of owner (e.g. WebView2 Chrome_WidgetWin_0)
        GetAncestor(hit, GA_ROOTOWNER) == owner
    }
}

/// Resolves the actual activatable window target: if `hwnd` has a visible, enabled modal popup
/// (e.g. Java `SunAwtDialog` owned by `SunAwtFrame`), returns that popup instead of the blocked parent.
pub fn resolve_active_popup_target(hwnd: HWND) -> HWND {
    unsafe {
        if hwnd.0.is_null() || !IsWindow(hwnd).as_bool() {
            return hwnd;
        }
        let popup = GetLastActivePopup(hwnd);
        if !popup.0.is_null()
            && popup != hwnd
            && IsWindow(popup).as_bool()
            && IsWindowVisible(popup).as_bool()
            && IsWindowEnabled(popup).as_bool()
        {
            return popup;
        }
        hwnd
    }
}

/// 5-Stage Deadlock-Free Industrial Foreground & Z-Order Engine (v5.0):
/// - NEVER calls `AttachThreadInput`, so foreign Electron/Java/UWP queues can NEVER deadlock!
/// - Resolves active modal popups (`GetLastActivePopup`, fixes Java `SunAwtFrame` + `SunAwtDialog`).
/// - Performs a safe non-hung `HWND_TOPMOST` → `HWND_NOTOPMOST` Z-order elevation bump so stubborn
///   Java AWT/Swing, Qt, and Electron windows physically rise above competing windows even under
///   Windows Foreground Lock (`WS_EX_NOACTIVATE` Dock clicks).
/// - Finishes with verified `SetForegroundWindow` + `SwitchToThisWindow(target, true)` Alt-Tab focus handoff.
pub fn force_foreground_window(hwnd: HWND) {
    unsafe {
        if hwnd.0.is_null() || !IsWindow(hwnd).as_bool() {
            return;
        }

        let target = resolve_active_popup_target(hwnd);

        // Stage 1: Restore iconic windows cleanly (both parent and active popup if distinct)
        if target != hwnd && IsIconic(hwnd).as_bool() {
            let _ = ShowWindowAsync(hwnd, SW_RESTORE);
        }
        if IsIconic(target).as_bool() {
            let _ = ShowWindowAsync(target, SW_RESTORE);
        } else {
            let _ = ShowWindowAsync(target, SW_SHOW);
        }

        // Stage 2: Grant explicit foreground transfer permission to target PID + ASFW_ANY
        let mut target_pid = 0u32;
        GetWindowThreadProcessId(target, Some(&mut target_pid));
        if target_pid != 0 {
            let _ = AllowSetForegroundWindow(target_pid);
        }
        let _ = AllowSetForegroundWindow(ASFW_ANY);

        // Stage 3: Safe physical Z-order elevation bump for healthy non-topmost windows.
        // Guaranteed never to leave a window stuck topmost (synchronous pair, guarded by IsHungAppWindow).
        let lift_z_order = |w: HWND| {
            if w.0.is_null() || !IsWindow(w).as_bool() || IsHungAppWindow(w).as_bool() {
                return;
            }
            let ex = GetWindowLongPtrW(w, GWL_EXSTYLE) as u32;
            let was_topmost = (ex & WS_EX_TOPMOST.0) != 0;
            if !was_topmost {
                let _ = SetWindowPos(
                    w,
                    HWND_TOPMOST,
                    0,
                    0,
                    0,
                    0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW,
                );
                let _ = SetWindowPos(
                    w,
                    HWND_NOTOPMOST,
                    0,
                    0,
                    0,
                    0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW,
                );
            }
        };

        if target != hwnd {
            lift_z_order(hwnd);
        }
        lift_z_order(target);

        // Stage 4: Standard activation & Z-top bring
        let _ = BringWindowToTop(target);
        let _ = SetForegroundWindow(target);

        // Stage 5: Verified fallback via Alt-Tab native focus handoff when Windows Foreground Lock resists
        if GetForegroundWindow() != target {
            SwitchToThisWindow(target, true);
            let _ = BringWindowToTop(target);
            let _ = SetForegroundWindow(target);
        }
    }
}

pub fn unregister_aurora_hotkeys(hwnd: HWND) {
    unsafe {
        for id in [HOTKEY_OMNI_SEARCH, HOTKEY_CROWN_DECK, HOTKEY_OCR_SNIP, HOTKEY_EYEDROPPER, HOTKEY_GIF_RECORDER] {
            let _ = UnregisterHotKey(hwnd, id);
        }
    }
}

/// Extracts dropped file paths from a `WM_DROPFILES` handle cleanly.
pub fn extract_dropped_files(hdrop: HDROP) -> Vec<String> {
    let mut out = Vec::new();
    unsafe {
        let count = DragQueryFileW(hdrop, u32::MAX, None);
        for i in 0..count {
            let len = DragQueryFileW(hdrop, i, None) as usize;
            if len > 0 {
                let mut buf = vec![0u16; len + 1];
                let copied = DragQueryFileW(hdrop, i, Some(&mut buf)) as usize;
                let s = String::from_utf16_lossy(&buf[..copied]);
                if !s.is_empty() {
                    out.push(s);
                }
            }
        }
        DragFinish(hdrop);
    }
    out
}

/// Hardware-composited DWM Live Thumbnail preview popup (`DwmRegisterThumbnail`) for hovering
/// over Dock icons — ZERO CPU capture, 60fps real-time GPU window reflection!
pub struct DwmLivePreviewWindow {
    hwnd: HWND,
    thumb: isize,
    source_hwnd: isize,
}

impl DwmLivePreviewWindow {
    pub fn create() -> Option<Self> {
        unsafe {
            let hinst = GetModuleHandleW(None).ok()?;
            let cls = w!("AuroraDwmLivePreviewV3");
            let wc = WNDCLASSW {
                lpfnWndProc: Some(preview_wnd_proc),
                hInstance: hinst.into(),
                lpszClassName: cls,
                ..Default::default()
            };
            let _ = RegisterClassW(&wc);
            let hwnd = CreateWindowExW(
                WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT,
                cls,
                w!("Aurora Live Preview"),
                WS_POPUP,
                0,
                0,
                260,
                160,
                None,
                None,
                hinst,
                None,
            )
            .ok()?;

            let corner = DWMWCP_ROUND;
            let _ = DwmSetWindowAttribute(
                hwnd,
                DWMWA_WINDOW_CORNER_PREFERENCE,
                &corner as *const _ as *const _,
                std::mem::size_of_val(&corner) as u32,
            );
            Some(Self { hwnd, thumb: 0, source_hwnd: 0 })
        }
    }

    pub fn show(&mut self, source_hwnd: isize, target_rect: PhysicalRect) {
        if source_hwnd == 0 {
            self.hide();
            return;
        }
        unsafe {
            if self.source_hwnd != source_hwnd {
                if self.thumb != 0 {
                    let _ = DwmUnregisterThumbnail(self.thumb);
                    self.thumb = 0;
                }
                if let Ok(t) = DwmRegisterThumbnail(self.hwnd, HWND(source_hwnd as *mut _)) {
                    self.thumb = t;
                    self.source_hwnd = source_hwnd;
                } else {
                    self.hide();
                    return;
                }
            }

            let src_sz = DwmQueryThumbnailSourceSize(self.thumb)
                .unwrap_or(SIZE { cx: target_rect.width, cy: target_rect.height });
            let pad = 6;
            let inner_w = (target_rect.width - pad * 2).max(32);
            let inner_h = (target_rect.height - pad * 2).max(24);
            let (dst_w, dst_h) = if src_sz.cx > 0 && src_sz.cy > 0 {
                let s = (inner_w as f32 / src_sz.cx as f32).min(inner_h as f32 / src_sz.cy as f32);
                ((src_sz.cx as f32 * s) as i32, (src_sz.cy as f32 * s) as i32)
            } else {
                (inner_w, inner_h)
            };
            let ox = (target_rect.width - dst_w) / 2;
            let oy = (target_rect.height - dst_h) / 2;

            let props = DWM_THUMBNAIL_PROPERTIES {
                dwFlags: DWM_TNP_VISIBLE | DWM_TNP_OPACITY | DWM_TNP_RECTDESTINATION | DWM_TNP_SOURCECLIENTAREAONLY,
                rcDestination: RECT { left: ox, top: oy, right: ox + dst_w, bottom: oy + dst_h },
                rcSource: RECT::default(),
                opacity: 245,
                fVisible: BOOL(1),
                fSourceClientAreaOnly: BOOL(0),
            };
            let _ = DwmUpdateThumbnailProperties(self.thumb, &props);
            let _ = SetWindowPos(
                self.hwnd,
                HWND_TOPMOST,
                target_rect.x,
                target_rect.y,
                target_rect.width,
                target_rect.height,
                SWP_NOACTIVATE | SWP_SHOWWINDOW,
            );
        }
    }

    pub fn hide(&mut self) {
        unsafe {
            if self.thumb != 0 {
                let _ = DwmUnregisterThumbnail(self.thumb);
                self.thumb = 0;
            }
            self.source_hwnd = 0;
            let _ = ShowWindow(self.hwnd, SW_HIDE);
        }
    }
}

impl Drop for DwmLivePreviewWindow {
    fn drop(&mut self) {
        self.hide();
        unsafe {
            let _ = DestroyWindow(self.hwnd);
        }
    }
}

unsafe extern "system" fn preview_wnd_proc(hwnd: HWND, msg: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    DefWindowProcW(hwnd, msg, wparam, lparam)
}
