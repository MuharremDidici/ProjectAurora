use std::cell::RefCell;
use std::path::Path;
use std::rc::Rc;

use anyhow::{anyhow, Context, Result};
use aurora_core::PhysicalRect;
use aurora_win32::{force_foreground_window, post_wake_ui, WM_AURORA_WAKE};
use crossbeam_channel::Sender;
use serde_json::Value;
use webview2_com::Microsoft::Web::WebView2::Win32::{
    CreateCoreWebView2EnvironmentWithOptions, ICoreWebView2, ICoreWebView2Controller,
    ICoreWebView2Controller2, ICoreWebView2Environment, ICoreWebView2EnvironmentOptions,
    ICoreWebView2File, ICoreWebView2Settings3, ICoreWebView2WebMessageReceivedEventArgs2,
    ICoreWebView2_19, ICoreWebView2_3, COREWEBVIEW2_COLOR,
    COREWEBVIEW2_HOST_RESOURCE_ACCESS_KIND_ALLOW, COREWEBVIEW2_MEMORY_USAGE_TARGET_LEVEL_LOW,
    COREWEBVIEW2_MOVE_FOCUS_REASON_PROGRAMMATIC,
};
use webview2_com::{
    CreateCoreWebView2ControllerCompletedHandler, CreateCoreWebView2EnvironmentCompletedHandler,
    CoreWebView2EnvironmentOptions, NavigationCompletedEventHandler, WebMessageReceivedEventHandler,
};
use windows::core::{w, Interface, HSTRING, PCWSTR, PWSTR};
use windows::Win32::Foundation::{BOOL, HWND, LPARAM, LRESULT, RECT, WPARAM};
use windows::Win32::Graphics::Dwm::{
    DwmEnableBlurBehindWindow, DwmExtendFrameIntoClientArea, DwmSetWindowAttribute,
    DWMNCRP_DISABLED, DWMWA_BORDER_COLOR, DWMWA_NCRENDERING_POLICY, DWM_BB_BLURREGION,
    DWM_BB_ENABLE, DWM_BLURBEHIND,
};
use windows::Win32::Graphics::Gdi::{
    CombineRgn, CreateRectRgn, DeleteObject, SetWindowRgn, HRGN, RGN_OR,
};
use windows::Win32::System::Com::CoTaskMemFree;
use windows::Win32::System::DataExchange::{
    AddClipboardFormatListener, RemoveClipboardFormatListener,
};
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::System::WinRT::EventRegistrationToken;
use windows::Win32::UI::Controls::MARGINS;
use windows::Win32::UI::WindowsAndMessaging::{
    CreateWindowExW, DefWindowProcW, DestroyWindow, GetWindowLongPtrW, LoadCursorW,
    RegisterClassW, RegisterWindowMessageW, SetWindowLongPtrW, SetWindowPos, ShowWindow,
    GWLP_USERDATA, GWL_EXSTYLE, HWND_TOPMOST, IDC_ARROW, MA_NOACTIVATE, SWP_FRAMECHANGED,
    SWP_NOACTIVATE, SWP_NOMOVE, SWP_NOSIZE, SWP_NOZORDER, SWP_SHOWWINDOW, SW_HIDE,
    SW_SHOWNOACTIVATE, WA_INACTIVE, WM_ACTIVATE, WM_DISPLAYCHANGE, WM_DPICHANGED, WM_ERASEBKGND,
    WM_HOTKEY, WM_MOUSEACTIVATE, WNDCLASSW, WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW, WS_EX_TOPMOST,
    WS_POPUP,
};

#[derive(Debug, Clone)]
pub enum SurfaceMessage {
    Ready {
        surface_id: usize,
        source: &'static str,
    },
    Diag {
        surface_id: usize,
        detail: String,
    },
    BridgeAction {
        monitor_id: String,
        payload: Value,
        dropped_files: Vec<String>,
    },
    Hotkey {
        id: i32,
    },
    Deactivated {
        surface_id: usize,
    },
    DisplayChanged,
    PowerChanged,
    ClipboardChanged,
    TaskbarRecreated,
}

#[derive(Debug, Clone, Copy)]
struct DipRect {
    x: f64,
    y: f64,
    w: f64,
    h: f64,
}

struct HitState {
    bounds_px: PhysicalRect,
    dpi_scale: f32,
    is_crown: bool,
    force_interactive: bool,
    viewport_w: f64,
    viewport_h: f64,
    rects: Vec<DipRect>,
    last_rgn: Vec<(i32, i32, i32, i32)>,
}

impl HitState {
    fn sync_rgn(&mut self, hwnd: HWND) {
        let max_w = self.bounds_px.width.max(1);
        let max_h = self.bounds_px.height.max(1);
        // INDUSTRIAL CLICK-THROUGH INVARIANT:
        // NEVER expand SetWindowRgn to the full (0,0,max_w,max_h) invisible window even when
        // `force_interactive` is true! Transparent pixels around open popups MUST pass straight
        // through the OS kernel to underlying apps so clicking outside NEVER feels blocked and
        // immediately triggers WM_ACTIVATE(WA_INACTIVE) to close the popup cleanly!
        let target: Vec<(i32, i32, i32, i32)> = if self.rects.is_empty() {
            // Keep a minimal 1x1 non-intrusive region until first DOM hitRects arrives
            vec![(0, 0, 1, 1)]
        } else {
            let sx = if self.viewport_w > 10.0 {
                max_w as f64 / self.viewport_w
            } else {
                self.dpi_scale.max(1.0) as f64
            };
            let sy = if self.viewport_h > 10.0 {
                max_h as f64 / self.viewport_h
            } else {
                self.dpi_scale.max(1.0) as f64
            };
            let vw = if self.viewport_w > 10.0 {
                self.viewport_w
            } else {
                max_w as f64 / self.dpi_scale.max(1.0) as f64
            };
            let vh = if self.viewport_h > 10.0 {
                self.viewport_h
            } else {
                max_h as f64 / self.dpi_scale.max(1.0) as f64
            };

            self.rects
                .iter()
                .filter_map(|r| {
                    if r.w <= 0.5 || r.h <= 0.5 {
                        return None;
                    }
                    // Edge-to-edge Fixed Bar gets 0px extra top/side padding so WhatsApp / Antigravity
                    // typing boxes right above the 44/52/60px bar are NEVER covered!
                    let is_edge_bar = !self.is_crown && (r.w >= vw - 4.0 || r.h >= vh - 4.0);
                    let (pad_l, pad_t, pad_r, pad_b) = if is_edge_bar {
                        (0.0, 0.0, 0.0, 0.0)
                    } else {
                        // Generous 24 DIP shadow halo around floating pills/cards keeps CSS drop-shadows unclipped!
                        (24.0 * sx, 20.0 * sy, 24.0 * sx, 26.0 * sy)
                    };
                    let x0 = ((r.x * sx - pad_l).floor() as i32).clamp(0, max_w);
                    let y0 = ((r.y * sy - pad_t).floor() as i32).clamp(0, max_h);
                    let x1 = (((r.x + r.w) * sx + pad_r).ceil() as i32).clamp(0, max_w);
                    let y1 = (((r.y + r.h) * sy + pad_b).ceil() as i32).clamp(0, max_h);
                    if x1 > x0 && y1 > y0 {
                        Some((x0, y0, x1, y1))
                    } else {
                        None
                    }
                })
                .collect()
        };

        if target == self.last_rgn {
            return;
        }
        self.last_rgn = target;
        unsafe {
            let combined: HRGN = if self.last_rgn.is_empty() {
                CreateRectRgn(0, 0, 0, 0)
            } else {
                let (x0, y0, x1, y1) = self.last_rgn[0];
                let base = CreateRectRgn(x0, y0, x1, y1);
                for &(ax0, ay0, ax1, ay1) in &self.last_rgn[1..] {
                    let next = CreateRectRgn(ax0, ay0, ax1, ay1);
                    let _ = CombineRgn(base, base, next, RGN_OR);
                    let _ = DeleteObject(next);
                }
                base
            };
            let _ = SetWindowRgn(hwnd, combined, true);
        }
    }
}

struct HostBridge {
    surface_id: usize,
    allow_activate: bool,
    taskbar_created_msg: u32,
    tx: Sender<SurfaceMessage>,
    wake_hwnd: isize,
}

/// Creates ONE shared ultra-lean `CoreWebView2Environment` using `--process-per-site` + stripped
/// Chromium features so ALL monitors/surfaces share ONE single Chromium renderer process!
pub fn create_shared_webview_env(user_data_dir: &Path) -> Result<ICoreWebView2Environment> {
    let _ = std::fs::create_dir_all(user_data_dir);
    let data_dir_w = HSTRING::from(user_data_dir.to_string_lossy().as_ref());

    let opts = CoreWebView2EnvironmentOptions::default();
    unsafe {
        opts.set_additional_browser_arguments(
            "--process-per-site --renderer-process-limit=1 --disable-extensions --disable-sync --disable-background-networking --disable-component-update --disable-back-forward-cache --disable-gpu-shader-disk-cache --gpu-program-cache-size-kb=2048 --disable-features=Translate,OptimizationHints,MediaRouter,AutofillServerCommunication,BackForwardCache --js-flags=--max-old-space-size=48".into(),
        );
    }
    let opts_iface: ICoreWebView2EnvironmentOptions = opts.into();

    let env_cell: Rc<RefCell<Option<ICoreWebView2Environment>>> = Rc::new(RefCell::new(None));
    let env_out = Rc::clone(&env_cell);
    CreateCoreWebView2EnvironmentCompletedHandler::wait_for_async_operation(
        Box::new(move |handler| unsafe {
            CreateCoreWebView2EnvironmentWithOptions(
                PCWSTR::null(),
                PCWSTR(data_dir_w.as_ptr()),
                &opts_iface,
                &handler,
            )
            .map_err(webview2_com::Error::WindowsError)
        }),
        Box::new(move |hr, env| {
            hr?;
            *env_out.borrow_mut() = env;
            Ok(())
        }),
    )
    .map_err(|e| anyhow!("CreateCoreWebView2EnvironmentWithOptions failed: {e:?}"))?;

    let env = env_cell.borrow_mut().take();
    env.context("WebView2 environment returned None (Edge WebView2 Runtime missing?)")
}

/// Ultra-lean per-surface Rust Win32 + `CoreWebView2` host with DWM per-pixel transparency,
/// kernel-level `SetWindowRgn` click-through (0% idle polling!), and `https://aurora.local/` virtual origin.
pub struct WebViewOverlaySurface {
    pub surface_id: usize,
    pub monitor_id: String,
    pub hwnd: HWND,
    pub bounds_px: PhysicalRect,
    pub dpi_scale: f32,
    pub suppressed_fullscreen: bool,
    target_url: String,
    controller: ICoreWebView2Controller,
    webview: ICoreWebView2,
    hit_state: Rc<RefCell<HitState>>,
    bridge_ptr: *mut HostBridge,
}

impl WebViewOverlaySurface {
    #[allow(clippy::too_many_arguments)]
    pub fn create(
        surface_id: usize,
        monitor_id: String,
        is_crown: bool,
        env: &ICoreWebView2Environment,
        ui_dir: &Path,
        page_file: &str,
        bounds_px: PhysicalRect,
        dpi_scale: f32,
        tx: Sender<SurfaceMessage>,
        wake_hwnd_override: Option<isize>,
        defer_navigate: bool,
    ) -> Result<Self> {
        let hwnd = create_transparent_host_hwnd(bounds_px)?;
        let wake_hwnd = wake_hwnd_override.unwrap_or(hwnd.0 as isize);
        let tb_msg = unsafe { RegisterWindowMessageW(w!("TaskbarCreated")) };
        let bridge = Box::new(HostBridge {
            surface_id,
            allow_activate: false,
            taskbar_created_msg: tb_msg,
            tx: tx.clone(),
            wake_hwnd,
        });
        let bridge_ptr = Box::into_raw(bridge);
        unsafe {
            SetWindowLongPtrW(hwnd, GWLP_USERDATA, bridge_ptr as isize);
            if surface_id == 0 {
                // Zero-polling OS clipboard change notifications (WM_CLIPBOARDUPDATE)!
                let _ = AddClipboardFormatListener(hwnd);
            }
        }

        // Create CoreWebView2Controller synchronously via message-pumping handler
        let env_clone = env.clone();
        let ctrl_cell: Rc<RefCell<Option<ICoreWebView2Controller>>> = Rc::new(RefCell::new(None));
        let ctrl_out = Rc::clone(&ctrl_cell);
        CreateCoreWebView2ControllerCompletedHandler::wait_for_async_operation(
            Box::new(move |handler| unsafe {
                env_clone
                    .CreateCoreWebView2Controller(hwnd, &handler)
                    .map_err(webview2_com::Error::WindowsError)
            }),
            Box::new(move |hr, ctrl| {
                hr?;
                *ctrl_out.borrow_mut() = ctrl;
                Ok(())
            }),
        )
        .map_err(|e| anyhow!("CreateCoreWebView2Controller failed: {e:?}"))?;

        let controller = ctrl_cell
            .borrow_mut()
            .take()
            .context("WebView2 controller returned None")?;
        let webview = unsafe { controller.CoreWebView2()? };

        // Per-pixel transparent WebView2 background + Low Memory Target + Clean UX settings
        unsafe {
            if let Ok(ctrl2) = controller.cast::<ICoreWebView2Controller2>() {
                let _ = ctrl2.SetDefaultBackgroundColor(COREWEBVIEW2_COLOR { A: 0, R: 0, G: 0, B: 0 });
            }
            if let Ok(settings) = webview.Settings() {
                let _ = settings.SetAreDefaultContextMenusEnabled(false);
                let _ = settings.SetAreDevToolsEnabled(false);
                let _ = settings.SetIsStatusBarEnabled(false);
                let _ = settings.SetIsZoomControlEnabled(false);
                if let Ok(s3) = settings.cast::<ICoreWebView2Settings3>() {
                    let _ = s3.SetAreBrowserAcceleratorKeysEnabled(false);
                }
            }
            if let Ok(wv19) = webview.cast::<ICoreWebView2_19>() {
                let _ = wv19.SetMemoryUsageTargetLevel(COREWEBVIEW2_MEMORY_USAGE_TARGET_LEVEL_LOW);
            }

            let rc = RECT {
                left: 0,
                top: 0,
                right: bounds_px.width.max(4),
                bottom: bounds_px.height.max(4),
            };
            let _ = controller.SetBounds(rc);
            let _ = controller.SetIsVisible(true);

            // Map shared virtual host `https://aurora.local/` so `--process-per-site` shares 1 renderer!
            if let Ok(wv3) = webview.cast::<ICoreWebView2_3>() {
                let host = HSTRING::from("aurora.local");
                let folder = HSTRING::from(ui_dir.to_string_lossy().as_ref());
                let _ = wv3.SetVirtualHostNameToFolderMapping(
                    &host,
                    &folder,
                    COREWEBVIEW2_HOST_RESOURCE_ACCESS_KIND_ALLOW,
                );
            }
        }

        let hit_state = Rc::new(RefCell::new(HitState {
            bounds_px,
            dpi_scale: dpi_scale.max(1.0),
            is_crown,
            force_interactive: false,
            viewport_w: 0.0,
            viewport_h: 0.0,
            rects: Vec::new(),
            last_rgn: Vec::new(),
        }));

        // Wire JS -> Rust message handler (+ native WebView2 File Drag-and-Drop!)
        let hs_cb = Rc::clone(&hit_state);
        let mon_cb = monitor_id.clone();
        let tx_cb = tx.clone();
        let mut token = EventRegistrationToken::default();
        let handler = WebMessageReceivedEventHandler::create(Box::new(move |_wv, args| {
            let Some(args) = args else {
                return Ok(());
            };
            let mut dropped_files = Vec::new();
            unsafe {
                if let Ok(args2) = args.cast::<ICoreWebView2WebMessageReceivedEventArgs2>() {
                    if let Ok(objs) = args2.AdditionalObjects() {
                        let mut count = 0u32;
                        if objs.Count(&mut count).is_ok() && count > 0 {
                            for i in 0..count {
                                if let Ok(item) = objs.GetValueAtIndex(i) {
                                    if let Ok(wf) = item.cast::<ICoreWebView2File>() {
                                        let mut pw = PWSTR::null();
                                        if wf.Path(&mut pw).is_ok() && !pw.0.is_null() {
                                            if let Ok(p) = pw.to_string() {
                                                if !p.is_empty() {
                                                    dropped_files.push(p);
                                                }
                                            }
                                            CoTaskMemFree(Some(pw.0 as *const _));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            let mut json_pw = PWSTR::null();
            let raw_json = unsafe {
                if args.WebMessageAsJson(&mut json_pw).is_ok() && !json_pw.0.is_null() {
                    let s = json_pw.to_string().unwrap_or_default();
                    CoTaskMemFree(Some(json_pw.0 as *const _));
                    s
                } else {
                    String::new()
                }
            };
            if raw_json.is_empty() && dropped_files.is_empty() {
                return Ok(());
            }

            let val: Value = serde_json::from_str(&raw_json).unwrap_or(Value::Null);
            if val.get("type").and_then(|v| v.as_str()) == Some("uiReady") {
                let _ = tx_cb.try_send(SurfaceMessage::Ready {
                    surface_id,
                    source: "uiReady",
                });
                post_wake_ui(wake_hwnd);
                return Ok(());
            }
            if val.get("type").and_then(|v| v.as_str()) == Some("uiDiag") {
                let detail = val
                    .get("detail")
                    .and_then(|v| v.as_str())
                    .unwrap_or("")
                    .to_string();
                let _ = tx_cb.try_send(SurfaceMessage::Diag { surface_id, detail });
                post_wake_ui(wake_hwnd);
                return Ok(());
            }
            if val.get("type").and_then(|v| v.as_str()) == Some("hitRects") {
                let mut hs = hs_cb.borrow_mut();
                let first_hit = hs.last_rgn.is_empty() || hs.last_rgn == [(0, 0, 1, 1)];
                if let Some(vw) = val.get("vw").and_then(|v| v.as_f64()) {
                    hs.viewport_w = vw;
                }
                if let Some(vh) = val.get("vh").and_then(|v| v.as_f64()) {
                    hs.viewport_h = vh;
                }
                if let Some(arr) = val.get("rects").and_then(|v| v.as_array()) {
                    hs.rects.clear();
                    for item in arr {
                        let x = item.get("x").and_then(|v| v.as_f64()).unwrap_or(0.0);
                        let y = item.get("y").and_then(|v| v.as_f64()).unwrap_or(0.0);
                        let w = item.get("w").and_then(|v| v.as_f64()).unwrap_or(0.0);
                        let h = item.get("h").and_then(|v| v.as_f64()).unwrap_or(0.0);
                        hs.rects.push(DipRect { x, y, w, h });
                    }
                    hs.sync_rgn(hwnd);
                    if first_hit {
                        let _ = tx_cb.try_send(SurfaceMessage::Diag {
                            surface_id,
                            detail: format!(
                                "first hitRects vw={:.0} vh={:.0} bounds=({},{},{}x{}) rgn={:?}",
                                hs.viewport_w,
                                hs.viewport_h,
                                hs.bounds_px.x,
                                hs.bounds_px.y,
                                hs.bounds_px.width,
                                hs.bounds_px.height,
                                hs.last_rgn
                            ),
                        });
                        post_wake_ui(wake_hwnd);
                    }
                }
                return Ok(());
            }

            let _ = tx_cb.try_send(SurfaceMessage::BridgeAction {
                monitor_id: mon_cb.clone(),
                payload: val,
                dropped_files,
            });
            post_wake_ui(wake_hwnd);
            Ok(())
        }));
        unsafe {
            let _ = webview.add_WebMessageReceived(&handler, &mut token);
            let tx_nav = tx.clone();
            let mut nav_token = EventRegistrationToken::default();
            let nav_handler = NavigationCompletedEventHandler::create(Box::new(move |wv, args| {
                let mut ok = true;
                let mut err_code = 0i32;
                if let Some(a) = args {
                    let mut b = BOOL(1);
                    if a.IsSuccess(&mut b).is_ok() {
                        ok = b.as_bool();
                    }
                    let mut st = webview2_com::Microsoft::Web::WebView2::Win32::COREWEBVIEW2_WEB_ERROR_STATUS(0);
                    if a.WebErrorStatus(&mut st).is_ok() {
                        err_code = st.0;
                    }
                }
                let mut src_str = String::new();
                if let Some(w) = wv {
                    let mut pw = PWSTR::null();
                    if w.Source(&mut pw).is_ok() && !pw.0.is_null() {
                        src_str = pw.to_string().unwrap_or_default();
                        CoTaskMemFree(Some(pw.0 as *const _));
                    }
                }
                if !ok {
                    let _ = tx_nav.try_send(SurfaceMessage::Diag {
                        surface_id,
                        detail: format!("navComplete FAILED ok={ok} err={err_code} url={src_str}"),
                    });
                }
                let _ = tx_nav.try_send(SurfaceMessage::Ready {
                    surface_id,
                    source: "nav",
                });
                post_wake_ui(wake_hwnd);
                Ok(())
            }));
            let _ = webview.add_NavigationCompleted(&nav_handler, &mut nav_token);
            let target_url = format!("https://aurora.local/{page_file}");
            if !defer_navigate {
                let url = HSTRING::from(target_url.as_str());
                let _ = webview.Navigate(&url);
            }
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
                surface_id,
                monitor_id,
                hwnd,
                bounds_px,
                dpi_scale: dpi_scale.max(1.0),
                suppressed_fullscreen: false,
                target_url,
                controller,
                webview,
                hit_state,
                bridge_ptr,
            })
        }
    }

    pub fn navigate_to_page(&self) {
        let url = HSTRING::from(self.target_url.as_str());
        unsafe {
            let _ = self.webview.Navigate(&url);
        }
    }

    pub fn post_json(&self, json: &str) {
        let wide = HSTRING::from(json);
        unsafe {
            let _ = self.webview.PostWebMessageAsJson(&wide);
        }
    }

    pub fn set_bounds_px(&mut self, new_bounds: PhysicalRect, dpi_scale: f32) {
        let scale = dpi_scale.max(1.0);
        if self.bounds_px == new_bounds && (self.dpi_scale - scale).abs() < 0.01 {
            return;
        }
        self.bounds_px = new_bounds;
        self.dpi_scale = scale;
        unsafe {
            let _ = SetWindowPos(
                self.hwnd,
                HWND_TOPMOST,
                new_bounds.x,
                new_bounds.y,
                new_bounds.width.max(4),
                new_bounds.height.max(4),
                SWP_NOACTIVATE | SWP_FRAMECHANGED,
            );
            let rc = RECT {
                left: 0,
                top: 0,
                right: new_bounds.width.max(4),
                bottom: new_bounds.height.max(4),
            };
            let _ = self.controller.SetBounds(rc);
            let _ = self.controller.NotifyParentWindowPositionChanged();
        }
        {
            let mut hs = self.hit_state.borrow_mut();
            hs.bounds_px = new_bounds;
            hs.dpi_scale = scale;
            hs.last_rgn.clear();
            hs.sync_rgn(self.hwnd);
        }
        self.post_json(r#"{"type":"syncLayout"}"#);
    }

    pub fn set_suppressed_fullscreen(&mut self, suppressed: bool) {
        if self.suppressed_fullscreen == suppressed {
            return;
        }
        if suppressed && self.hit_state.borrow().force_interactive {
            return;
        }
        self.suppressed_fullscreen = suppressed;
        unsafe {
            if suppressed {
                let _ = ShowWindow(self.hwnd, SW_HIDE);
            } else {
                let _ = ShowWindow(self.hwnd, SW_SHOWNOACTIVATE);
                let _ = SetWindowPos(
                    self.hwnd,
                    HWND_TOPMOST,
                    0,
                    0,
                    0,
                    0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE,
                );
            }
        }
    }

    /// Gives full reliable keyboard focus to OmniSearch / Bento Deck / Calendar / Tour!
    pub fn focus_and_activate(&mut self) {
        if self.suppressed_fullscreen {
            self.suppressed_fullscreen = false;
            unsafe {
                let _ = ShowWindow(self.hwnd, SW_SHOWNOACTIVATE);
            }
        }
        {
            let mut hs = self.hit_state.borrow_mut();
            hs.force_interactive = true;
            hs.sync_rgn(self.hwnd);
        }
        self.set_allow_activate(true);
        unsafe {
            let _ = SetWindowPos(self.hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        }
        force_foreground_window(self.hwnd);
        unsafe {
            let _ = self.controller.MoveFocus(COREWEBVIEW2_MOVE_FOCUS_REASON_PROGRAMMATIC);
        }
    }

    pub fn release_interactive(&mut self) {
        self.set_allow_activate(false);
        let mut hs = self.hit_state.borrow_mut();
        hs.force_interactive = false;
        hs.sync_rgn(self.hwnd);
    }

    pub fn is_interactive(&self) -> bool {
        self.hit_state.borrow().force_interactive
    }

    /// Forces immediate `SetWindowRgn` recomputation + DOM layout sync (e.g. after monitor hot-plug or sleep resume).
    pub fn force_resync_region(&mut self) {
        {
            let mut hs = self.hit_state.borrow_mut();
            hs.last_rgn.clear();
            hs.sync_rgn(self.hwnd);
        }
        self.post_json(r#"{"type":"syncLayout"}"#);
    }

    fn set_allow_activate(&self, allow: bool) {
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
}

impl Drop for WebViewOverlaySurface {
    fn drop(&mut self) {
        unsafe {
            if self.surface_id == 0 {
                let _ = RemoveClipboardFormatListener(self.hwnd);
            }
            let _ = self.controller.Close();
            SetWindowLongPtrW(self.hwnd, GWLP_USERDATA, 0);
            let _ = DestroyWindow(self.hwnd);
            if !self.bridge_ptr.is_null() {
                drop(Box::from_raw(self.bridge_ptr));
                self.bridge_ptr = std::ptr::null_mut();
            }
        }
    }
}

fn create_transparent_host_hwnd(bounds_px: PhysicalRect) -> Result<HWND> {
    unsafe {
        let hinst = GetModuleHandleW(None)?;
        let cls = w!("ProjectAuroraWebViewHostV3");
        let wc = WNDCLASSW {
            lpfnWndProc: Some(host_wnd_proc),
            hInstance: hinst.into(),
            hCursor: LoadCursorW(None, IDC_ARROW).unwrap_or_default(),
            lpszClassName: cls,
            ..Default::default()
        };
        let _ = RegisterClassW(&wc);

        let hwnd = CreateWindowExW(
            WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
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

        // Exact DWM per-pixel transparency (zero border, zero white flash, crisp CSS alpha!)
        let nc = DWMNCRP_DISABLED.0;
        let _ = DwmSetWindowAttribute(
            hwnd,
            DWMWA_NCRENDERING_POLICY,
            &nc as *const _ as *const _,
            std::mem::size_of_val(&nc) as u32,
        );
        let no_border: u32 = 0xFFFF_FFFE;
        let _ = DwmSetWindowAttribute(
            hwnd,
            DWMWA_BORDER_COLOR,
            &no_border as *const _ as *const _,
            std::mem::size_of_val(&no_border) as u32,
        );
        let margins = MARGINS {
            cxLeftWidth: -1,
            cxRightWidth: -1,
            cyTopHeight: -1,
            cyBottomHeight: -1,
        };
        let _ = DwmExtendFrameIntoClientArea(hwnd, &margins);
        let empty_rgn = CreateRectRgn(0, 0, -1, -1);
        let bb = DWM_BLURBEHIND {
            dwFlags: DWM_BB_ENABLE | DWM_BB_BLURREGION,
            fEnable: BOOL(1),
            hRgnBlur: empty_rgn,
            fTransitionOnMaximized: BOOL(0),
        };
        let _ = DwmEnableBlurBehindWindow(hwnd, &bb);
        let _ = DeleteObject(empty_rgn);

        Ok(hwnd)
    }
}

unsafe extern "system" fn host_wnd_proc(
    hwnd: HWND,
    msg: u32,
    wparam: WPARAM,
    lparam: LPARAM,
) -> LRESULT {
    let ptr = GetWindowLongPtrW(hwnd, GWLP_USERDATA) as *mut HostBridge;
    if ptr.is_null() {
        return DefWindowProcW(hwnd, msg, wparam, lparam);
    }
    let bridge = &mut *ptr;

    if msg == bridge.taskbar_created_msg && bridge.taskbar_created_msg != 0 {
        let _ = bridge.tx.try_send(SurfaceMessage::TaskbarRecreated);
        post_wake_ui(bridge.wake_hwnd);
        return LRESULT(0);
    }

    match msg {
        WM_AURORA_WAKE => return LRESULT(0),
        WM_ERASEBKGND => return LRESULT(1),
        WM_MOUSEACTIVATE => {
            if !bridge.allow_activate {
                return LRESULT(MA_NOACTIVATE as isize);
            }
        }
        WM_ACTIVATE => {
            let state = (wparam.0 & 0xFFFF) as u32;
            if state == WA_INACTIVE {
                let _ = bridge.tx.try_send(SurfaceMessage::Deactivated {
                    surface_id: bridge.surface_id,
                });
                post_wake_ui(bridge.wake_hwnd);
            }
        }
        WM_HOTKEY => {
            let _ = bridge.tx.try_send(SurfaceMessage::Hotkey { id: wparam.0 as i32 });
            post_wake_ui(bridge.wake_hwnd);
            return LRESULT(0);
        }
        WM_DISPLAYCHANGE | WM_DPICHANGED => {
            let _ = bridge.tx.try_send(SurfaceMessage::DisplayChanged);
            post_wake_ui(bridge.wake_hwnd);
            if msg == WM_DPICHANGED {
                return LRESULT(0);
            }
        }
        0x031D /* WM_CLIPBOARDUPDATE */ => {
            let _ = bridge.tx.try_send(SurfaceMessage::ClipboardChanged);
            post_wake_ui(bridge.wake_hwnd);
            return LRESULT(0);
        }
        0x0218 /* WM_POWERBROADCAST */ => {
            let evt = wparam.0;
            if evt == 0x0A /* PBT_APMPOWERSTATUSCHANGE */ {
                let _ = bridge.tx.try_send(SurfaceMessage::PowerChanged);
                post_wake_ui(bridge.wake_hwnd);
            } else if evt == 0x07 /* PBT_APMRESUMESUSPEND */ || evt == 0x12 /* PBT_APMRESUMEAUTOMATIC */ {
                let _ = bridge.tx.try_send(SurfaceMessage::PowerChanged);
                let _ = bridge.tx.try_send(SurfaceMessage::DisplayChanged);
                post_wake_ui(bridge.wake_hwnd);
            }
        }
        _ => {}
    }

    DefWindowProcW(hwnd, msg, wparam, lparam)
}
