use aurora_render::RectF;

/// Semantic action identifier attached to interactive hit regions.
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub enum UiAction {
    None,
    HoverOnly,
    // Crown & Deck navigation
    ToggleDeck(String),
    SwitchDeckTab(String),
    ToggleCrownPin,
    ToggleOmni,
    ToggleTray,
    ToggleCalendar,
    ToggleNotif,
    ToggleDrop,
    OpenTour(usize),
    CloseTour,
    CloseOverlays,
    // Dock actions
    ClickDockGroup(String),
    RightClickDockGroup(String),
    ClickPinnedShortcut(String),
    ClickRadialWindow(isize),
    RadialAction { hwnd: isize, cmd: String },
    ToggleAccordion(String),
    ToggleFolderStack(String),
    OpenFolderEntry(String),
    ToggleSmartTidy,
    ToggleDockPinMode,
    CycleDockMode,
    SetDockMode(String),
    // Control Center & Hardware
    SetMasterVolume(u8),
    ToggleMasterMute,
    ToggleMicMute,
    SelectAudioOutput(String),
    SetAppVolume { pid: u32, pct: u8 },
    RouteAppOutput { pid: u32, device_id: String },
    SetMonitorBrightness { id: String, pct: u8 },
    MediaPrev,
    MediaPlayPause,
    MediaNext,
    ToggleWifi,
    ToggleBluetooth,
    ToggleFocusMode,
    CycleAwake,
    ToggleFocusTimer,
    ResetFocusTimer,
    CycleKeyboard,
    SwitchVirtualDesktop(i32),
    PeekNativeTray,
    ShowDesktop,
    PowerAction(String),
    // Productivity & Tools
    TriggerOcr,
    TriggerEyedropper,
    ToggleGifRecorder,
    StartQrShare(String),
    StopQrShare,
    ZipShelf,
    ClearShelf,
    OpenShelfItem(String),
    RemoveShelfItem(String),
    ClipboardUse(i64),
    ClipboardPin(i64),
    ClipboardDelete(i64),
    ClipboardClearUnpinned,
    ClipboardFilter(String),
    FocusTextInput(String),
    // Notifications & Calendar
    ActivateNotification(String),
    DismissNotification(String),
    ClearAllNotifications,
    CalendarPrevMonth,
    CalendarNextMonth,
    CalendarSelectDay(String),
    // Telemetry
    KillTopProcess { pid: u32, name: String },
    // Studio & Personalization
    SelectTheme(String),
    SelectProfile(String),
    ApplyAccentPreset(usize),
    SetCustomColorSlot { slot: String, hex: String },
    SetSurfaceOpacity(u8),
    ResetThemeColors,
    SetDockEdge(String),
    SetDockDensity(String),
    SetDockBarLayout(String),
    ToggleReserveWorkArea,
    ToggleNativeTaskbar,
    ToggleClockSeconds,
    ToggleHardwareOsd,
    ToggleAltDrag,
    ToggleSnapZones,
    SetSnapPreset(String),
    ToggleWinKeyOmni,
    TogglePerMonitorFilter,
    ToggleClipboardAutoPaste,
    ToggleNotifBridge,
    ToggleDwmPreview,
    ExportBackup,
    ImportBackup,
    CheckUpdates,
    InstallUpdate,
    ExitAurora,
    // OmniSearch
    OmniFilter(String),
    OmniExecute(usize),
    // Slider drag region
    SliderTrack { id: String, rect_x: i32, rect_w: i32 },
    // 2D Color picker drag region
    ColorPickerArea { slot: String, rect_x: i32, rect_y: i32, rect_w: i32, rect_h: i32 },
    ColorHueBar { slot: String, rect_x: i32, rect_w: i32 },
    // Tray app launch
    LaunchPath(String),
    OpenUri(String),
}

#[derive(Debug, Clone)]
pub struct HitRegion {
    pub id: String,
    pub rect: RectF,
    pub action: UiAction,
    pub tooltip: Option<String>,
}

/// Zero-polling hit-test ledger rebuilt cleanly on each paint pass (used directly by `WM_NCHITTEST`!).
#[derive(Debug, Default, Clone)]
pub struct HitTree {
    /// Non-transparent visual shell rects that should receive mouse events (vs `HTTRANSPARENT` passthrough).
    pub opaque_shells: Vec<RectF>,
    /// Interactive controls in paint order (top-most last).
    pub regions: Vec<HitRegion>,
}

impl HitTree {
    pub fn clear(&mut self) {
        self.opaque_shells.clear();
        self.regions.clear();
    }

    pub fn push_shell(&mut self, rect: RectF) {
        if rect.w > 0.0 && rect.h > 0.0 {
            self.opaque_shells.push(rect);
        }
    }

    pub fn push(&mut self, id: impl Into<String>, rect: RectF, action: UiAction) {
        if rect.w > 0.0 && rect.h > 0.0 {
            self.regions.push(HitRegion {
                id: id.into(),
                rect,
                action,
                tooltip: None,
            });
        }
    }

    pub fn push_with_tip(&mut self, id: impl Into<String>, rect: RectF, action: UiAction, tip: impl Into<String>) {
        if rect.w > 0.0 && rect.h > 0.0 {
            self.regions.push(HitRegion {
                id: id.into(),
                rect,
                action,
                tooltip: Some(tip.into()),
            });
        }
    }

    /// Evaluated directly inside Win32 `WM_NCHITTEST` in < 2 microseconds!
    pub fn is_opaque_at(&self, dip_x: f32, dip_y: f32) -> bool {
        self.opaque_shells.iter().any(|r| r.contains(dip_x, dip_y))
            || self.regions.iter().any(|h| h.rect.contains(dip_x, dip_y))
    }

    /// Top-most interactive region under cursor.
    pub fn hit_test(&self, dip_x: f32, dip_y: f32) -> Option<&HitRegion> {
        self.regions.iter().rev().find(|h| h.rect.contains(dip_x, dip_y))
    }
}
