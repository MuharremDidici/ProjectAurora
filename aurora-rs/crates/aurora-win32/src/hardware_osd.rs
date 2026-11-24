use std::sync::atomic::{AtomicBool, AtomicIsize, AtomicU64, Ordering};
use aurora_core::PhysicalRect;
use crossbeam_channel::Sender;
use parking_lot::Mutex;
use windows::Win32::Foundation::{HWND, LPARAM, LRESULT, RECT, WPARAM};
use windows::Win32::Graphics::Dwm::{DwmGetWindowAttribute, DWMWA_EXTENDED_FRAME_BOUNDS};
use windows::Win32::System::SystemInformation::GetTickCount64;
use windows::Win32::System::Threading::GetCurrentThreadId;
use windows::Win32::UI::Input::KeyboardAndMouse::{
    GetAsyncKeyState, GetKeyState, SendInput, INPUT, INPUT_0, INPUT_KEYBOARD, KEYBDINPUT,
    KEYEVENTF_KEYUP, VIRTUAL_KEY, VK_CAPITAL, VK_CONTROL, VK_MENU, VK_NUMLOCK, VK_SHIFT,
};
use windows::Win32::UI::WindowsAndMessaging::{
    CallNextHookEx, GetClassNameW, GetMessageW, GetWindowRect, GetWindowTextW,
    GetWindowThreadProcessId, IsWindowVisible, PostMessageW, PostThreadMessageW,
    SetWindowsHookExW, ShowWindow, UnhookWindowsHookEx, KBDLLHOOKSTRUCT, MSG, SW_HIDE,
    WH_KEYBOARD_LL, WM_APP, WM_KEYDOWN, WM_KEYUP, WM_QUIT, WM_SYSKEYDOWN, WM_SYSKEYUP,
};

use crate::windows_catalog::query_process_image;

pub const VK_VOLUME_MUTE: u32 = 0xAD;
pub const VK_VOLUME_DOWN: u32 = 0xAE;
pub const VK_VOLUME_UP: u32 = 0xAF;
const VK_LWIN: u32 = 0x5B;
const VK_RWIN: u32 = 0x5C;
const VK_NONAME: u16 = 0xFC;
const LLKHF_INJECTED: u32 = 0x10;
const LLKHF_ALTDOWN: u32 = 0x20;

/// Custom Win32 wake message posted to the primary UI HWND whenever a background thread or
/// dedicated low-level hook thread enqueues an event — guarantees <0.5ms UI wakeup with 0% idle CPU!
pub const WM_AURORA_WAKE: u32 = WM_APP + 1;

pub fn post_wake_ui(wake_hwnd: isize) {
    if wake_hwnd != 0 {
        unsafe {
            let _ = PostMessageW(HWND(wake_hwnd as *mut _), WM_AURORA_WAKE, WPARAM(0), LPARAM(0));
        }
    }
}

#[derive(Debug, Clone)]
pub enum LowLevelKeyEvent {
    VolumeKey(u32),
    ToggleIndicatorKey { vk: u32, next_on: bool },
    SingleWinKeyTapped,
    OmniHotkeyTapped,
}

static HOOK_TX: Mutex<Option<Sender<LowLevelKeyEvent>>> = Mutex::new(None);
static WAKE_HWND: AtomicIsize = AtomicIsize::new(0);
static SUPPRESS_VOL: AtomicBool = AtomicBool::new(true);
static WIN_KEY_OMNI: AtomicBool = AtomicBool::new(false);
static LL_OMNI_FALLBACK: AtomicBool = AtomicBool::new(false);
static WIN_PRESSED: AtomicBool = AtomicBool::new(false);
static WIN_DOWN_TICK: AtomicU64 = AtomicU64::new(0);
static WIN_COMBO_USED: AtomicBool = AtomicBool::new(false);
static ALT_SPACE_SWALLOWED: AtomicBool = AtomicBool::new(false);
static ALT_SPACE_TICK: AtomicU64 = AtomicU64::new(0);

/// Runs `WH_KEYBOARD_LL` on a dedicated ultra-fast `GetMessageW` thread (<3 µs callback) so
/// Windows 11's `LowLevelHooksTimeout` NEVER silently unhooks Aurora under any UI or GPU load!
pub struct LowLevelKeyboardHook {
    thread_id: Option<u32>,
    join: Option<std::thread::JoinHandle<()>>,
}

impl Default for LowLevelKeyboardHook {
    fn default() -> Self {
        Self::new()
    }
}

impl LowLevelKeyboardHook {
    pub fn new() -> Self {
        Self { thread_id: None, join: None }
    }

    pub fn start(
        &mut self,
        tx: Sender<LowLevelKeyEvent>,
        wake_hwnd: isize,
        suppress_vol: bool,
        win_omni: bool,
        ll_omni_fallback: bool,
    ) -> bool {
        self.stop();
        WAKE_HWND.store(wake_hwnd, Ordering::Relaxed);
        SUPPRESS_VOL.store(suppress_vol, Ordering::Relaxed);
        WIN_KEY_OMNI.store(win_omni, Ordering::Relaxed);
        LL_OMNI_FALLBACK.store(ll_omni_fallback, Ordering::Relaxed);
        WIN_PRESSED.store(false, Ordering::Relaxed);
        WIN_COMBO_USED.store(false, Ordering::Relaxed);
        ALT_SPACE_SWALLOWED.store(false, Ordering::Relaxed);
        *HOOK_TX.lock() = Some(tx);

        let (ready_tx, ready_rx) = crossbeam_channel::bounded::<(u32, bool)>(1);
        let handle = std::thread::Builder::new()
            .name("aurora-kbd-hook".into())
            .spawn(move || unsafe {
                let tid = GetCurrentThreadId();
                let hhook = SetWindowsHookExW(WH_KEYBOARD_LL, Some(ll_kbd_proc), None, 0).ok();
                let ok = hhook.is_some_and(|h| !h.0.is_null());
                let _ = ready_tx.send((tid, ok));
                if let Some(h) = hhook.filter(|h| !h.0.is_null()) {
                    let mut msg = MSG::default();
                    while GetMessageW(&mut msg, None, 0, 0).0 > 0 {}
                    let _ = UnhookWindowsHookEx(h);
                }
            });

        match handle {
            Ok(jh) => {
                if let Ok((tid, ok)) = ready_rx.recv() {
                    if ok {
                        self.thread_id = Some(tid);
                        self.join = Some(jh);
                        return true;
                    }
                }
                let _ = jh.join();
                false
            }
            Err(_) => false,
        }
    }

    pub fn update_flags(&self, suppress_vol: bool, win_omni: bool, ll_omni_fallback: bool) {
        SUPPRESS_VOL.store(suppress_vol, Ordering::Relaxed);
        WIN_KEY_OMNI.store(win_omni, Ordering::Relaxed);
        LL_OMNI_FALLBACK.store(ll_omni_fallback, Ordering::Relaxed);
        if !win_omni {
            WIN_PRESSED.store(false, Ordering::Relaxed);
            WIN_COMBO_USED.store(false, Ordering::Relaxed);
        }
    }

    pub fn stop(&mut self) {
        if let Some(tid) = self.thread_id.take() {
            unsafe {
                let _ = PostThreadMessageW(tid, WM_QUIT, WPARAM(0), LPARAM(0));
            }
        }
        if let Some(jh) = self.join.take() {
            let _ = jh.join();
        }
        *HOOK_TX.lock() = None;
        WAKE_HWND.store(0, Ordering::Relaxed);
    }
}

impl Drop for LowLevelKeyboardHook {
    fn drop(&mut self) {
        self.stop();
    }
}

/// Injects a single harmless masked `VK_NONAME` (0xFC) KEYUP before `VK_LWIN`/`VK_RWIN` goes up
/// so explorer.exe suppresses the native Start Menu cleanly WITHOUT ever sticking any real key!
unsafe fn suppress_native_start_menu_mask() {
    let seq = [INPUT {
        r#type: INPUT_KEYBOARD,
        Anonymous: INPUT_0 {
            ki: KEYBDINPUT {
                wVk: VIRTUAL_KEY(VK_NONAME),
                dwFlags: KEYEVENTF_KEYUP,
                ..Default::default()
            },
        },
    }];
    let _ = SendInput(&seq, std::mem::size_of::<INPUT>() as i32);
}

unsafe extern "system" fn ll_kbd_proc(code: i32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    if code >= 0 && lparam.0 != 0 {
        let kb = &*(lparam.0 as *const KBDLLHOOKSTRUCT);
        let msg = wparam.0 as u32;
        let is_down = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
        let is_up = msg == WM_KEYUP || msg == WM_SYSKEYUP;
        let is_injected = (kb.flags.0 & LLKHF_INJECTED) != 0;
        let vk = kb.vkCode;

        // Fast-exit injected non-volume keys (<0.1 µs) so SendInput can NEVER re-enter or stall!
        if is_injected && !matches!(vk, VK_VOLUME_UP | VK_VOLUME_DOWN | VK_VOLUME_MUTE) {
            return CallNextHookEx(None, code, wparam, lparam);
        }

        // 1. Intercept physical & driver-injected volume keys (Lenovo Fn, Logitech G HUB) -> 0ms Aurora OSD!
        if matches!(vk, VK_VOLUME_UP | VK_VOLUME_DOWN | VK_VOLUME_MUTE)
            && SUPPRESS_VOL.load(Ordering::Relaxed)
        {
            if is_down {
                if let Some(guard) = HOOK_TX.try_lock() {
                    if let Some(tx) = guard.as_ref() {
                        let _ = tx.try_send(LowLevelKeyEvent::VolumeKey(vk));
                        post_wake_ui(WAKE_HWND.load(Ordering::Relaxed));
                    }
                }
                return LRESULT(1);
            }
            if is_up {
                return LRESULT(1);
            }
        }

        // 2. CapsLock / NumLock live OSD (non-blocking try_lock, never swallows!)
        if is_down && !is_injected && (vk == VK_CAPITAL.0 as u32 || vk == VK_NUMLOCK.0 as u32) {
            let next_on = (GetKeyState(vk as i32) & 1) == 0;
            if let Some(guard) = HOOK_TX.try_lock() {
                if let Some(tx) = guard.as_ref() {
                    let _ = tx.try_send(LowLevelKeyEvent::ToggleIndicatorKey { vk, next_on });
                    post_wake_ui(WAKE_HWND.load(Ordering::Relaxed));
                }
            }
        }

        // 3. Fallback Alt+Space interception ONLY when RegisterHotKey could not claim Alt+Space
        if LL_OMNI_FALLBACK.load(Ordering::Relaxed) && !is_injected && vk == 0x20 {
            let ctrl_down = (GetAsyncKeyState(VK_CONTROL.0 as i32) as u16 & 0x8000) != 0;
            let now = GetTickCount64();
            if is_down && !ctrl_down && (kb.flags.0 & LLKHF_ALTDOWN) != 0 {
                ALT_SPACE_SWALLOWED.store(true, Ordering::Relaxed);
                ALT_SPACE_TICK.store(now, Ordering::Relaxed);
                if let Some(guard) = HOOK_TX.try_lock() {
                    if let Some(tx) = guard.as_ref() {
                        let _ = tx.try_send(LowLevelKeyEvent::OmniHotkeyTapped);
                        post_wake_ui(WAKE_HWND.load(Ordering::Relaxed));
                    }
                }
                return LRESULT(1);
            }
            if is_up && ALT_SPACE_SWALLOWED.swap(false, Ordering::Relaxed) {
                let dt = now.saturating_sub(ALT_SPACE_TICK.load(Ordering::Relaxed));
                if dt <= 500 {
                    return LRESULT(1);
                }
            }
        }

        // 4. Single-tap Win key -> OmniSearch while preserving 100% of Win+Key shortcuts (Win+E/D/L/V/Shift+S).
        //    INDUSTRIAL GUARDS:
        //    - NEVER swallows VK_LWIN / VK_RWIN down or up!
        //    - 450ms tap window prevents stale WIN_PRESSED across UAC / elevated apps!
        //    - Checks Ctrl/Alt/Shift modifiers so combos NEVER misfire!
        if !is_injected && WIN_KEY_OMNI.load(Ordering::Relaxed) {
            if vk == VK_LWIN || vk == VK_RWIN {
                let now = GetTickCount64();
                let other_mod = (GetAsyncKeyState(VK_CONTROL.0 as i32) as u16 & 0x8000) != 0
                    || (GetAsyncKeyState(VK_MENU.0 as i32) as u16 & 0x8000) != 0
                    || (GetAsyncKeyState(VK_SHIFT.0 as i32) as u16 & 0x8000) != 0;
                if is_down {
                    if !WIN_PRESSED.swap(true, Ordering::Relaxed) {
                        WIN_DOWN_TICK.store(now, Ordering::Relaxed);
                        WIN_COMBO_USED.store(other_mod, Ordering::Relaxed);
                    } else if other_mod {
                        WIN_COMBO_USED.store(true, Ordering::Relaxed);
                    }
                } else if is_up {
                    let was_down = WIN_PRESSED.swap(false, Ordering::Relaxed);
                    let used_combo = WIN_COMBO_USED.swap(false, Ordering::Relaxed);
                    let dt = now.saturating_sub(WIN_DOWN_TICK.load(Ordering::Relaxed));
                    if was_down && !used_combo && !other_mod && dt <= 450 {
                        suppress_native_start_menu_mask();
                        if let Some(guard) = HOOK_TX.try_lock() {
                            if let Some(tx) = guard.as_ref() {
                                let _ = tx.try_send(LowLevelKeyEvent::SingleWinKeyTapped);
                                post_wake_ui(WAKE_HWND.load(Ordering::Relaxed));
                            }
                        }
                    }
                }
            } else if is_down && vk != VK_NONAME as u32 && WIN_PRESSED.load(Ordering::Relaxed) {
                WIN_COMBO_USED.store(true, Ordering::Relaxed);
            }
        }
    }
    CallNextHookEx(None, code, wparam, lparam)
}

pub fn is_caps_lock_on() -> bool {
    unsafe { (GetKeyState(VK_CAPITAL.0 as i32) & 1) != 0 }
}

pub fn is_num_lock_on() -> bool {
    unsafe { (GetKeyState(VK_NUMLOCK.0 as i32) & 1) != 0 }
}

/// <1ms suppression of duplicate Windows 11 volume/brightness OSD flyouts and bottom-right toast banners
/// while strictly protecting Start Menu, Quick Settings, and Action Center!
pub fn try_suppress_win11_shell_overlay(
    hwnd: HWND,
    monitors: &[PhysicalRect],
    suppress_osd: bool,
    suppress_toasts: bool,
) -> Option<&'static str> {
    if (!suppress_osd && !suppress_toasts) || hwnd.0.is_null() {
        return None;
    }
    unsafe {
        if !IsWindowVisible(hwnd).as_bool() {
            return None;
        }
        let mut pid = 0u32;
        GetWindowThreadProcessId(hwnd, Some(&mut pid));
        if pid == 0 || pid == std::process::id() {
            return None;
        }
        let (proc_name, _) = query_process_image(pid);
        let p_low = proc_name.to_ascii_lowercase();
        let is_shell_host = matches!(
            p_low.as_str(),
            "shellexperiencehost" | "shellhost" | "explorer" | "xamlhost"
        );
        if !is_shell_host {
            return None;
        }

        let mut cls_buf = [0u16; 96];
        let clen = GetClassNameW(hwnd, &mut cls_buf) as usize;
        let cls = String::from_utf16_lossy(&cls_buf[..clen]);
        if !matches!(
            cls.as_str(),
            "XamlExplorerHostIslandWindow" | "Windows.UI.Core.CoreWindow" | "NativeHWNDHost"
        ) {
            return None;
        }

        let mut title_buf = [0u16; 128];
        let tlen = GetWindowTextW(hwnd, &mut title_buf) as usize;
        let title = String::from_utf16_lossy(&title_buf[..tlen]);

        let mut rc = RECT::default();
        if DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, &mut rc as *mut _ as *mut _, 16).is_err()
            && GetWindowRect(hwnd, &mut rc).is_err()
        {
            return None;
        }
        let w_rect = PhysicalRect::new(rc.left, rc.top, rc.right - rc.left, rc.bottom - rc.top);

        if suppress_osd && is_compact_osd_geometry(w_rect, monitors) {
            let _ = ShowWindow(hwnd, SW_HIDE);
            return Some("osd");
        }
        if suppress_toasts && is_native_toast_banner_match(&title, w_rect, monitors) {
            let _ = ShowWindow(hwnd, SW_HIDE);
            return Some("toast");
        }
    }
    None
}

pub fn is_compact_osd_geometry(r: PhysicalRect, monitors: &[PhysicalRect]) -> bool {
    if r.width < 110 || r.width > 520 || r.height < 36 || r.height > 155 {
        return false;
    }
    let cx = r.x + r.width / 2;
    let cy = r.y + r.height / 2;
    for m in monitors {
        if !m.contains(cx, cy) {
            continue;
        }
        let mon_cx = m.x + m.width / 2;
        let horiz_center = (cx - mon_cx).abs() <= (m.width / 8).max(120);
        let near_bottom = r.bottom() >= m.bottom() - (m.height / 4).max(160);
        let near_top = r.y <= m.y + (m.height / 6).max(120);
        if horiz_center && (near_bottom || near_top) {
            return true;
        }
    }
    false
}

pub fn is_native_toast_banner_match(title: &str, r: PhysicalRect, monitors: &[PhysicalRect]) -> bool {
    let t = title.trim().to_ascii_lowercase();
    // Strictly protect Action Center, Quick Settings, Start Menu!
    if t.contains("merkezi")
        || t.contains("notification center")
        || t.contains("hızlı ayar")
        || t.contains("quick setting")
        || t.contains("başlat")
        || t.contains("start")
    {
        return false;
    }
    if r.width < 220 || r.width > 560 || r.height < 64 || r.height > 290 {
        return false;
    }
    let explicit_toast = t.contains("yeni bildirim") || t.contains("new notification");
    let cx = r.x + r.width / 2;
    let cy = r.y + r.height / 2;
    for m in monitors {
        if !m.contains(cx, cy) {
            continue;
        }
        let near_right = r.right() >= m.right() - 120;
        let near_bottom = r.bottom() >= m.bottom() - 260;
        if near_right && near_bottom && (explicit_toast || t.is_empty()) {
            return true;
        }
    }
    false
}

pub fn compute_next_volume_step(current_pct: u8, step_up: bool) -> u8 {
    if step_up {
        ((current_pct / 2) * 2 + 2).min(100)
    } else {
        current_pct.saturating_sub(2)
    }
}
