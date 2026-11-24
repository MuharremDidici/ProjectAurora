use std::cell::RefCell;
use std::rc::Rc;
use aurora_core::PhysicalRect;
use aurora_render::{CompositionSurface, GpuContext, Painter};
use aurora_ui::{HitTree, UiAction};
use aurora_win32::WM_AURORA_WAKE;
use crossbeam_channel::Sender;
use windows::core::w;
use windows::Win32::Foundation::{HWND, LPARAM, LRESULT, POINT, WPARAM};
use windows::Win32::Graphics::Gdi::{
    CombineRgn, CreateRectRgn, DeleteObject, ScreenToClient, SetWindowRgn, HRGN, RGN_OR,
};
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::UI::Input::KeyboardAndMouse::{
    GetKeyState, TrackMouseEvent, TME_LEAVE, TRACKMOUSEEVENT, VK_CONTROL,
};
use windows::Win32::UI::Shell::{DragAcceptFiles, HDROP};
use windows::Win32::UI::WindowsAndMessaging::{
    CreateWindowExW, DefWindowProcW, DestroyWindow, GetWindowLongPtrW, LoadCursorW,
    RegisterClassW, RegisterWindowMessageW, SetWindowLongPtrW, SetWindowPos, ShowWindow,
    GWLP_USERDATA, GWL_EXSTYLE, HTCLIENT, HTTRANSPARENT, HWND_TOPMOST, IDC_ARROW, MA_NOACTIVATE,
    SWP_FRAMECHANGED, SWP_NOACTIVATE, SWP_NOMOVE, SWP_NOSIZE, SWP_NOZORDER, SWP_SHOWWINDOW,
    SW_HIDE, SW_SHOWNOACTIVATE, WA_INACTIVE, WM_ACTIVATE, WM_CHAR, WM_DISPLAYCHANGE, WM_DPICHANGED,
    WM_DROPFILES, WM_ERASEBKGND, WM_HOTKEY, WM_KEYDOWN, WM_LBUTTONDOWN, WM_LBUTTONUP,
    WM_MOUSEACTIVATE, WM_MOUSEMOVE, WM_MOUSEWHEEL, WM_NCHITTEST, WM_RBUTTONDOWN, WNDCLASSW,
    WS_EX_ACCEPTFILES, WS_EX_NOACTIVATE, WS_EX_NOREDIRECTIONBITMAP, WS_EX_TOOLWINDOW,
    WS_EX_TOPMOST, WS_POPUP,
};

const WM_MOUSELEAVE: u32 = 0x02A3;

#[allow(dead_code)]
#[derive(Debug, Clone)]
pub enum WindowEvent {
    MouseMove { win_id: usize, dip_x: f32, dip_y: f32, button_down: bool },
    MouseLeave { win_id: usize },
    LeftDown { win_id: usize, dip_x: f32, dip_y: f32 },
    LeftUp { dip_x: f32, dip_y: f32 },
    RightDown { win_id: usize, dip_x: f32, dip_y: f32 },
    MouseWheel { win_id: usize, delta: i16 },
    KeyDown { vk: u16, ctrl: bool },
    CharInput { ch: char },
    Hotkey { id: i32 },
    FilesDropped { paths: Vec<String> },
    Deactivated { win_id: usize },
    DisplayChanged,
    TaskbarRecreated,
}

struct WndBridge {
    win_id: usize,
    dpi_scale: f32,
    allow_activate: bool,
    mouse_down: bool,
    tracking_leave: bool,
    taskbar_created_msg: u32,
    hits: Rc<RefCell<HitTree>>,
    tx: Sender<WindowEvent>,
}

/// Hardware-accelerated DirectComposition overlay HWND (per-monitor Dock band or Primary Crown/Popup band).
/// Uses kernel-level `SetWindowRgn` built from `HitTree::opaque_shells` so 100% of transparent areas
/// pass mouse clicks cross-process to underlying apps (Antigravity, Chrome, WhatsApp, Desktop) with 0% CPU!
pub struct ShellOverlayWindow {
    pub hwnd: HWND,
    pub surface: CompositionSurface,
    pub hits: Rc<RefCell<HitTree>>,
    pub bounds_px: PhysicalRect,
    pub dpi_scale: f32,
    pub visible: bool,
    last_rgn_rects: Option<Vec<(i32, i32, i32, i32)>>,
    bridge_ptr: *mut WndBridge,
}

impl ShellOverlayWindow {
    pub fn create(
        win_id: usize,
        gpu: &GpuContext,
        bounds_px: PhysicalRect,
        dpi_scale: f32,
        tx: Sender<WindowEvent>,
    ) -> anyhow::Result<Self> {
        unsafe {
            let hinst = GetModuleHandleW(None)?;
            let cls = w!("ProjectAuroraDirectCompV3");
            let wc = WNDCLASSW {
                lpfnWndProc: Some(overlay_wnd_proc),
                hInstance: hinst.into(),
                hCursor: LoadCursorW(None, IDC_ARROW).unwrap_or_default(),
                lpszClassName: cls,
                ..Default::default()
            };
            let _ = RegisterClassW(&wc);

            let hwnd = CreateWindowExW(
                WS_EX_TOPMOST
                    | WS_EX_TOOLWINDOW
                    | WS_EX_NOACTIVATE
                    | WS_EX_NOREDIRECTIONBITMAP
                    | WS_EX_ACCEPTFILES,
                cls,
                w!("Project Aurora"),
                WS_POPUP,
                bounds_px.x,
                bounds_px.y,
                bounds_px.width.max(4),
                bounds_px.height.max(4),
                None,
                None,
                hinst,
                None,
            )?;

            DragAcceptFiles(hwnd, true);

            let hits = Rc::new(RefCell::new(HitTree::default()));
            let tb_msg = RegisterWindowMessageW(w!("TaskbarCreated"));
            let bridge = Box::new(WndBridge {
                win_id,
                dpi_scale: dpi_scale.max(1.0),
                allow_activate: false,
                mouse_down: false,
                tracking_leave: false,
                taskbar_created_msg: tb_msg,
                hits: hits.clone(),
                tx,
            });
            let bridge_ptr = Box::into_raw(bridge);
            SetWindowLongPtrW(hwnd, GWLP_USERDATA, bridge_ptr as isize);

            let surface = CompositionSurface::new(
                gpu,
                hwnd,
                bounds_px.width.max(4) as u32,
                bounds_px.height.max(4) as u32,
                dpi_scale.max(1.0) * 96.0,
            )?;

            let _ = ShowWindow(hwnd, SW_SHOWNOACTIVATE);
            let _ = SetWindowPos(
                hwnd,
                HWND_TOPMOST,
                bounds_px.x,
                bounds_px.y,
                bounds_px.width.max(4),
                bounds_px.height.max(4),
                SWP_NOACTIVATE | SWP_SHOWWINDOW,
            );

            Ok(Self {
                hwnd,
                surface,
                hits,
                bounds_px,
                dpi_scale: dpi_scale.max(1.0),
                visible: true,
                last_rgn_rects: None,
                bridge_ptr,
            })
        }
    }

    /// Toggles `WS_EX_TRANSPARENT` so visual-only overlays (Shift+Drag Snap Zones) can NEVER block mouse clicks!
    pub fn set_click_through(&self, transparent: bool) {
        use windows::Win32::UI::WindowsAndMessaging::WS_EX_TRANSPARENT;
        unsafe {
            let cur = GetWindowLongPtrW(self.hwnd, GWL_EXSTYLE) as u32;
            let next = if transparent {
                cur | WS_EX_TRANSPARENT.0
            } else {
                cur & !WS_EX_TRANSPARENT.0
            };
            if next != cur {
                SetWindowLongPtrW(self.hwnd, GWL_EXSTYLE, next as isize);
                let _ = SetWindowPos(
                    self.hwnd,
                    None,
                    0,
                    0,
                    0,
                    0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED,
                );
            }
        }
    }

    /// Dynamically toggles `WS_EX_NOACTIVATE` so OmniSearch/Deck text inputs receive full
    /// Turkish IME keyboard input while resting Crown & Dock NEVER steal focus!
    pub fn set_allow_activate(&self, allow: bool) {
        unsafe {
            if !self.bridge_ptr.is_null() {
                if (*self.bridge_ptr).allow_activate == allow {
                    return;
                }
                (*self.bridge_ptr).allow_activate = allow;
                let cur = GetWindowLongPtrW(self.hwnd, GWL_EXSTYLE) as u32;
                let next = if allow {
                    cur & !WS_EX_NOACTIVATE.0
                } else {
                    cur | WS_EX_NOACTIVATE.0
                };
                if next != cur {
                    SetWindowLongPtrW(self.hwnd, GWL_EXSTYLE, next as isize);
                    let _ = SetWindowPos(
                        self.hwnd,
                        None,
                        0,
                        0,
                        0,
                        0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED,
                    );
                }
            }
        }
    }

    #[allow(dead_code)]
    pub fn set_bounds_px(&mut self, gpu: &GpuContext, new_bounds: PhysicalRect, dpi_scale: f32) -> anyhow::Result<()> {
        let scale = dpi_scale.max(1.0);
        if self.bounds_px == new_bounds && (self.dpi_scale - scale).abs() < 0.01 && self.visible {
            return Ok(());
        }
        self.bounds_px = new_bounds;
        self.dpi_scale = scale;
        self.visible = true;
        self.last_rgn_rects = None;
        unsafe {
            if !self.bridge_ptr.is_null() {
                (*self.bridge_ptr).dpi_scale = scale;
            }
            let _ = ShowWindow(self.hwnd, SW_SHOWNOACTIVATE);
            let _ = SetWindowPos(
                self.hwnd,
                HWND_TOPMOST,
                new_bounds.x,
                new_bounds.y,
                new_bounds.width.max(4),
                new_bounds.height.max(4),
                SWP_NOACTIVATE | SWP_SHOWWINDOW,
            );
        }
        self.surface.set_dpi(scale * 96.0);
        let _ = self.surface.ensure_size(
            gpu,
            new_bounds.width.max(4) as u32,
            new_bounds.height.max(4) as u32,
        )?;
        Ok(())
    }

    #[allow(dead_code)]
    pub fn hide(&mut self) {
        if self.visible {
            self.visible = false;
            unsafe {
                let _ = ShowWindow(self.hwnd, SW_HIDE);
            }
        }
    }

    pub fn dip_size(&self) -> (f32, f32) {
        (
            self.bounds_px.width as f32 / self.dpi_scale.max(1.0),
            self.bounds_px.height as f32 / self.dpi_scale.max(1.0),
        )
    }

    /// Renders a Direct2D frame into DirectComposition and updates the kernel-level `HRGN`
    /// click-through shape ONLY when shell rects actually change.
    pub fn render<F>(&mut self, gpu: &GpuContext, full_capture: bool, draw: F) -> anyhow::Result<()>
    where
        F: FnOnce(&Painter<'_>, &mut HitTree, f32, f32),
    {
        if !self.visible {
            return Ok(());
        }
        let (w_dip, h_dip) = self.dip_size();
        let mut hits = self.hits.borrow_mut();
        self.surface.begin_frame();
        {
            let painter = Painter::new(&self.surface.d2d_dc, gpu);
            draw(&painter, &mut hits, w_dip, h_dip);
        }
        let present_res = self.surface.end_frame_and_present(gpu, None);
        let rgn_rects = self.collect_rgn_rects(&hits, full_capture);
        drop(hits);
        self.sync_window_region(rgn_rects);
        present_res
    }

    fn collect_rgn_rects(&self, hits: &HitTree, full_capture: bool) -> Vec<(i32, i32, i32, i32)> {
        let max_w = self.bounds_px.width.max(1);
        let max_h = self.bounds_px.height.max(1);
        if full_capture {
            return vec![(0, 0, max_w, max_h)];
        }
        let s = self.dpi_scale.max(1.0);
        let (w_dip, h_dip) = self.dip_size();
        hits.opaque_shells
            .iter()
            .map(|r| {
                // Edge-to-edge fixed bar gets 0px extra top/side padding so app input boxes are NEVER blocked!
                let is_edge_bar = r.w >= w_dip - 2.0 || r.h >= h_dip - 2.0;
                let pad = if is_edge_bar { 0 } else { (12.0 * s).round() as i32 };
                let x0 = ((r.x * s).floor() as i32 - pad).clamp(0, max_w);
                let y0 = ((r.y * s).floor() as i32 - pad).clamp(0, max_h);
                let x1 = ((r.right() * s).ceil() as i32 + pad).clamp(0, max_w);
                let y1 = ((r.bottom() * s).ceil() as i32 + pad).clamp(0, max_h);
                (x0, y0, x1, y1)
            })
            .filter(|&(x0, y0, x1, y1)| x1 > x0 && y1 > y0)
            .collect()
    }

    fn sync_window_region(&mut self, rects: Vec<(i32, i32, i32, i32)>) {
        if self.last_rgn_rects.as_ref() == Some(&rects) {
            return;
        }
        unsafe {
            let combined: HRGN = if rects.is_empty() {
                CreateRectRgn(0, 0, 0, 0)
            } else {
                let (x0, y0, x1, y1) = rects[0];
                let base = CreateRectRgn(x0, y0, x1, y1);
                for &(ax0, ay0, ax1, ay1) in &rects[1..] {
                    let next = CreateRectRgn(ax0, ay0, ax1, ay1);
                    let _ = CombineRgn(base, base, next, RGN_OR);
                    let _ = DeleteObject(next);
                }
                base
            };
            // Windows takes ownership of `combined` upon SetWindowRgn!
            let _ = SetWindowRgn(self.hwnd, combined, true);
        }
        self.last_rgn_rects = Some(rects);
    }

    #[allow(dead_code)]
    pub fn hit_test_action(&self, dip_x: f32, dip_y: f32) -> Option<(String, UiAction)> {
        self.hits
            .borrow()
            .hit_test(dip_x, dip_y)
            .map(|h| (h.id.clone(), h.action.clone()))
    }
}

impl Drop for ShellOverlayWindow {
    fn drop(&mut self) {
        unsafe {
            SetWindowLongPtrW(self.hwnd, GWLP_USERDATA, 0);
            let _ = DestroyWindow(self.hwnd);
            if !self.bridge_ptr.is_null() {
                drop(Box::from_raw(self.bridge_ptr));
                self.bridge_ptr = std::ptr::null_mut();
            }
        }
    }
}

fn unpack_xy(lparam: LPARAM) -> (i16, i16) {
    let raw = lparam.0 as u32;
    ((raw & 0xFFFF) as i16, ((raw >> 16) & 0xFFFF) as i16)
}

unsafe extern "system" fn overlay_wnd_proc(
    hwnd: HWND,
    msg: u32,
    wparam: WPARAM,
    lparam: LPARAM,
) -> LRESULT {
    let ptr = GetWindowLongPtrW(hwnd, GWLP_USERDATA) as *mut WndBridge;
    if ptr.is_null() {
        return DefWindowProcW(hwnd, msg, wparam, lparam);
    }
    let bridge = &mut *ptr;

    if msg == bridge.taskbar_created_msg && bridge.taskbar_created_msg != 0 {
        let _ = bridge.tx.try_send(WindowEvent::TaskbarRecreated);
        return LRESULT(0);
    }

    match msg {
        WM_AURORA_WAKE => return LRESULT(0),
        WM_ERASEBKGND => return LRESULT(1),
        WM_NCHITTEST => {
            let (sx, sy) = unpack_xy(lparam);
            let mut pt = POINT { x: sx as i32, y: sy as i32 };
            let _ = ScreenToClient(hwnd, &mut pt);
            let dip_x = pt.x as f32 / bridge.dpi_scale.max(1.0);
            let dip_y = pt.y as f32 / bridge.dpi_scale.max(1.0);
            if bridge.hits.borrow().is_opaque_at(dip_x, dip_y) {
                return LRESULT(HTCLIENT as isize);
            }
            return LRESULT(HTTRANSPARENT as isize);
        }
        WM_MOUSEACTIVATE => {
            if !bridge.allow_activate {
                return LRESULT(MA_NOACTIVATE as isize);
            }
        }
        WM_ACTIVATE => {
            let state = (wparam.0 & 0xFFFF) as u32;
            if state == WA_INACTIVE {
                let _ = bridge.tx.try_send(WindowEvent::Deactivated { win_id: bridge.win_id });
            }
        }
        WM_MOUSEMOVE => {
            if !bridge.tracking_leave {
                let mut tme = TRACKMOUSEEVENT {
                    cbSize: std::mem::size_of::<TRACKMOUSEEVENT>() as u32,
                    dwFlags: TME_LEAVE,
                    hwndTrack: hwnd,
                    dwHoverTime: 0,
                };
                let _ = TrackMouseEvent(&mut tme);
                bridge.tracking_leave = true;
            }
            let (x, y) = unpack_xy(lparam);
            let dip_x = x as f32 / bridge.dpi_scale.max(1.0);
            let dip_y = y as f32 / bridge.dpi_scale.max(1.0);
            let _ = bridge.tx.try_send(WindowEvent::MouseMove {
                win_id: bridge.win_id,
                dip_x,
                dip_y,
                button_down: bridge.mouse_down,
            });
            return LRESULT(0);
        }
        WM_MOUSELEAVE => {
            bridge.tracking_leave = false;
            bridge.mouse_down = false;
            let _ = bridge.tx.try_send(WindowEvent::MouseLeave { win_id: bridge.win_id });
            return LRESULT(0);
        }
        WM_LBUTTONDOWN => {
            bridge.mouse_down = true;
            let (x, y) = unpack_xy(lparam);
            let _ = bridge.tx.try_send(WindowEvent::LeftDown {
                win_id: bridge.win_id,
                dip_x: x as f32 / bridge.dpi_scale.max(1.0),
                dip_y: y as f32 / bridge.dpi_scale.max(1.0),
            });
            return LRESULT(0);
        }
        WM_LBUTTONUP => {
            bridge.mouse_down = false;
            let (x, y) = unpack_xy(lparam);
            let _ = bridge.tx.try_send(WindowEvent::LeftUp {
                dip_x: x as f32 / bridge.dpi_scale.max(1.0),
                dip_y: y as f32 / bridge.dpi_scale.max(1.0),
            });
            return LRESULT(0);
        }
        WM_RBUTTONDOWN => {
            let (x, y) = unpack_xy(lparam);
            let _ = bridge.tx.try_send(WindowEvent::RightDown {
                win_id: bridge.win_id,
                dip_x: x as f32 / bridge.dpi_scale.max(1.0),
                dip_y: y as f32 / bridge.dpi_scale.max(1.0),
            });
            return LRESULT(0);
        }
        WM_MOUSEWHEEL => {
            let delta = ((wparam.0 >> 16) & 0xFFFF) as i16;
            let _ = bridge.tx.try_send(WindowEvent::MouseWheel { win_id: bridge.win_id, delta });
            return LRESULT(0);
        }
        WM_KEYDOWN => {
            let ctrl = (GetKeyState(VK_CONTROL.0 as i32) as u16 & 0x8000) != 0;
            let _ = bridge.tx.try_send(WindowEvent::KeyDown { vk: (wparam.0 & 0xFFFF) as u16, ctrl });
            return LRESULT(0);
        }
        WM_CHAR => {
            if let Some(ch) = char::from_u32((wparam.0 & 0xFFFF) as u32) {
                let _ = bridge.tx.try_send(WindowEvent::CharInput { ch });
            }
            return LRESULT(0);
        }
        WM_HOTKEY => {
            let _ = bridge.tx.try_send(WindowEvent::Hotkey { id: wparam.0 as i32 });
            return LRESULT(0);
        }
        WM_DROPFILES => {
            let paths = aurora_win32::extract_dropped_files(HDROP(wparam.0 as *mut _));
            if !paths.is_empty() {
                let _ = bridge.tx.try_send(WindowEvent::FilesDropped { paths });
            }
            return LRESULT(0);
        }
        WM_DISPLAYCHANGE | WM_DPICHANGED => {
            let _ = bridge.tx.try_send(WindowEvent::DisplayChanged);
        }
        _ => {}
    }

    DefWindowProcW(hwnd, msg, wparam, lparam)
}
