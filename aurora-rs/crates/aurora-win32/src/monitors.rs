use aurora_core::{MonitorDescriptor, PhysicalRect};
use windows::Win32::Foundation::{BOOL, LPARAM, RECT};
use windows::Win32::Graphics::Gdi::{
    EnumDisplayMonitors, GetMonitorInfoW, HDC, HMONITOR, MONITORINFOEXW,
};
use windows::Win32::UI::HiDpi::{
    GetDpiForMonitor, SetProcessDpiAwarenessContext, DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2,
    MDT_EFFECTIVE_DPI,
};

const MONITORINFOF_PRIMARY: u32 = 1;

pub fn enable_per_monitor_dpi_v2() {
    unsafe {
        let _ = SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    }
}

#[derive(Debug, Clone)]
pub struct LiveMonitorHandle {
    pub hmonitor: HMONITOR,
    pub descriptor: MonitorDescriptor,
}

pub fn enumerate_monitors() -> Vec<LiveMonitorHandle> {
    let mut results: Vec<LiveMonitorHandle> = Vec::with_capacity(4);
    unsafe {
        let ptr = &mut results as *mut Vec<LiveMonitorHandle>;
        let _ = EnumDisplayMonitors(
            HDC::default(),
            None,
            Some(enum_mon_proc),
            LPARAM(ptr as isize),
        );
    }
    if results.is_empty() {
        results.push(LiveMonitorHandle {
            hmonitor: HMONITOR::default(),
            descriptor: MonitorDescriptor {
                device_id: "PRIMARY_FALLBACK".into(),
                friendly_name: "Birincil Ekran".into(),
                is_primary: true,
                effective_dpi: 96,
                bounds: PhysicalRect::new(0, 0, 1920, 1080),
                work_area: PhysicalRect::new(0, 0, 1920, 1032),
            },
        });
    }
    results.sort_by_key(|a| std::cmp::Reverse(a.descriptor.is_primary));
    results
}

unsafe extern "system" fn enum_mon_proc(
    hmon: HMONITOR,
    _hdc: HDC,
    _rc: *mut RECT,
    lparam: LPARAM,
) -> BOOL {
    let list = &mut *(lparam.0 as *mut Vec<LiveMonitorHandle>);
    let mut mi = MONITORINFOEXW::default();
    mi.monitorInfo.cbSize = std::mem::size_of::<MONITORINFOEXW>() as u32;
    if GetMonitorInfoW(hmon, &mut mi.monitorInfo as *mut _ as *mut _).as_bool() {
        let mut dpi_x = 96u32;
        let mut dpi_y = 96u32;
        let _ = GetDpiForMonitor(hmon, MDT_EFFECTIVE_DPI, &mut dpi_x, &mut dpi_y);
        let is_primary = (mi.monitorInfo.dwFlags & MONITORINFOF_PRIMARY) != 0;
        let nul_pos = mi.szDevice.iter().position(|&c| c == 0).unwrap_or(mi.szDevice.len());
        let dev = String::from_utf16_lossy(&mi.szDevice[..nul_pos]).trim().to_string();
        let idx = list.len() + 1;
        let device_id = if dev.is_empty() { format!("DISPLAY_{idx}") } else { dev };
        let mr = mi.monitorInfo.rcMonitor;
        let wr = mi.monitorInfo.rcWork;
        let mw = mr.right - mr.left;
        let mh = mr.bottom - mr.top;
        let friendly = if is_primary {
            format!("Birincil Ekran ({mw}×{mh})")
        } else {
            format!("Ekran {idx} ({mw}×{mh})")
        };
        list.push(LiveMonitorHandle {
            hmonitor: hmon,
            descriptor: MonitorDescriptor {
                device_id,
                friendly_name: friendly,
                is_primary,
                effective_dpi: dpi_x.max(96),
                bounds: PhysicalRect::new(mr.left, mr.top, mw, mh),
                work_area: PhysicalRect::new(wr.left, wr.top, wr.right - wr.left, wr.bottom - wr.top),
            },
        });
    }
    BOOL(1)
}
