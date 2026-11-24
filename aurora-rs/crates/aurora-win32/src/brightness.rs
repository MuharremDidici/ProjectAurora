use std::collections::HashMap;
use std::sync::Arc;
use aurora_core::ExternalMonitorBrightness;
use crossbeam_channel::{unbounded, Sender};
use parking_lot::Mutex;
use windows::core::PCWSTR;
use windows::Win32::Devices::Display::{
    DestroyPhysicalMonitors, GetMonitorBrightness, GetNumberOfPhysicalMonitorsFromHMONITOR,
    GetPhysicalMonitorsFromHMONITOR, GetVCPFeatureAndVCPFeatureReply, SetMonitorBrightness,
    SetVCPFeature, PHYSICAL_MONITOR,
};
use windows::Win32::Graphics::Gdi::{EnumDisplayDevicesW, DISPLAY_DEVICEW};
use windows::Win32::System::Registry::{
    RegCloseKey, RegEnumKeyExW, RegOpenKeyExW, RegQueryValueExW, HKEY, HKEY_LOCAL_MACHINE, KEY_READ,
};

use crate::monitors::enumerate_monitors;

struct MonEntry {
    id: String,
    name: String,
    hphys: windows::Win32::Foundation::HANDLE,
    cur_pct: u8,
    min_v: u32,
    max_v: u32,
    use_vcp: bool,
}

unsafe impl Send for MonEntry {}
unsafe impl Sync for MonEntry {}

/// Native EDID + DXVA2 DDC/CI External Monitor Brightness Controller with last-value-wins async
/// I2C write coalescing (zero UI stall while dragging brightness sliders!) + `hPhysicalMonitor == 0` safe!
pub struct BrightnessController {
    monitors: Arc<Mutex<Vec<MonEntry>>>,
    write_tx: Sender<(String, u8)>,
}

impl Default for BrightnessController {
    fn default() -> Self {
        Self::new()
    }
}

impl BrightnessController {
    pub fn new() -> Self {
        let monitors: Arc<Mutex<Vec<MonEntry>>> = Arc::new(Mutex::new(Vec::new()));
        let (tx, rx) = unbounded::<(String, u8)>();
        let mons_bg = Arc::clone(&monitors);

        std::thread::Builder::new()
            .name("aurora-ddcci".into())
            .spawn(move || {
                while let Ok((mut id, mut pct)) = rx.recv() {
                    // Last-value-wins coalescing while user drags slider rapidly
                    let mut latest: HashMap<String, u8> = HashMap::new();
                    latest.insert(id, pct);
                    while let Ok((nid, npct)) = rx.try_recv() {
                        id = nid;
                        pct = npct;
                        latest.insert(id, pct);
                    }
                    let mut guard = mons_bg.lock();
                    for (mid, target_pct) in latest {
                        if let Some(entry) = guard.iter_mut().find(|m| m.id == mid) {
                            let span = entry.max_v.saturating_sub(entry.min_v).max(1);
                            let raw = entry.min_v + ((target_pct as u32 * span) + 50) / 100;
                            unsafe {
                                if entry.use_vcp || SetMonitorBrightness(entry.hphys, raw) == 0 {
                                    let _ = SetVCPFeature(entry.hphys, 0x10, raw);
                                }
                            }
                            entry.cur_pct = target_pct;
                        }
                    }
                }
            })
            .ok();

        Self {
            monitors,
            write_tx: tx,
        }
    }

    pub fn discover(&self) -> Vec<ExternalMonitorBrightness> {
        let edid_map = read_edid_friendly_names();
        let live_mons = enumerate_monitors();
        let mut discovered = Vec::new();

        unsafe {
            for (idx, lm) in live_mons.iter().enumerate() {
                if lm.hmonitor.0.is_null() {
                    continue;
                }
                let mut count = 0u32;
                if GetNumberOfPhysicalMonitorsFromHMONITOR(lm.hmonitor, &mut count).is_err() || count == 0 {
                    continue;
                }
                let mut phys = vec![PHYSICAL_MONITOR::default(); count as usize];
                if GetPhysicalMonitorsFromHMONITOR(lm.hmonitor, &mut phys).is_err() {
                    continue;
                }
                let hw_code = query_monitor_hw_code(&lm.descriptor.device_id);
                for (sub, pm) in phys.into_iter().enumerate() {
                    // NOTE: On Windows dxva2.dll, hPhysicalMonitor == 0 IS a valid 0-based index handle!
                    let mut min_b = 0u32;
                    let mut cur_b = 0u32;
                    let mut max_b = 100u32;
                    let mut use_vcp = false;
                    let ok_hl = GetMonitorBrightness(pm.hPhysicalMonitor, &mut min_b, &mut cur_b, &mut max_b) != 0 && max_b > min_b;
                    let ok_vcp = if !ok_hl {
                        use_vcp = true;
                        let mut vcp_cur = 0u32;
                        let mut vcp_max = 100u32;
                        let ok = GetVCPFeatureAndVCPFeatureReply(pm.hPhysicalMonitor, 0x10, None, &mut vcp_cur, Some(&mut vcp_max)) != 0
                            && vcp_max > 0;
                        if ok {
                            min_b = 0;
                            cur_b = vcp_cur;
                            max_b = vcp_max;
                        }
                        ok
                    } else {
                        true
                    };

                    if !ok_hl && !ok_vcp {
                        let _ = DestroyPhysicalMonitors(&[pm]);
                        continue;
                    }

                    let pct = (((cur_b.saturating_sub(min_b)) * 100) / (max_b - min_b).max(1)).min(100) as u8;
                    let desc_arr = pm.szPhysicalMonitorDescription;
                    let raw_desc_len = desc_arr.iter().position(|&c| c == 0).unwrap_or(desc_arr.len());
                    let dxva_desc = String::from_utf16_lossy(&desc_arr[..raw_desc_len]).trim().to_string();

                    let edid_name = hw_code.as_deref().and_then(|c| edid_map.get(c)).cloned();
                    let friendly = edid_name.unwrap_or_else(|| {
                        if !dxva_desc.is_empty() && !dxva_desc.to_ascii_lowercase().contains("generic") {
                            dxva_desc
                        } else {
                            format!("Ekran {}", idx + 1)
                        }
                    });
                    let clean_dev = lm.descriptor.device_id.replace(['\\', '.'], "");
                    discovered.push(MonEntry {
                        id: format!("ddc-{clean_dev}-{sub}"),
                        name: friendly,
                        hphys: pm.hPhysicalMonitor,
                        cur_pct: pct,
                        min_v: min_b,
                        max_v: max_b,
                        use_vcp,
                    });
                }
            }
        }

        let mut guard = self.monitors.lock();
        // Cleanly destroy old handles before swapping
        for old in guard.drain(..) {
            unsafe {
                let pm = PHYSICAL_MONITOR {
                    hPhysicalMonitor: old.hphys,
                    szPhysicalMonitorDescription: [0u16; 128],
                };
                let _ = DestroyPhysicalMonitors(&[pm]);
            }
        }
        *guard = discovered;
        guard
            .iter()
            .map(|m| ExternalMonitorBrightness {
                id: m.id.clone(),
                name: m.name.clone(),
                brightness_percent: m.cur_pct,
                is_internal: false,
            })
            .collect()
    }

    pub fn snapshot(&self) -> Vec<ExternalMonitorBrightness> {
        self.monitors
            .lock()
            .iter()
            .map(|m| ExternalMonitorBrightness {
                id: m.id.clone(),
                name: m.name.clone(),
                brightness_percent: m.cur_pct,
                is_internal: false,
            })
            .collect()
    }

    pub fn set_brightness(&self, id: &str, pct: u8) {
        let clamped = pct.min(100);
        if let Some(m) = self.monitors.lock().iter_mut().find(|m| m.id == id) {
            m.cur_pct = clamped;
        }
        let _ = self.write_tx.try_send((id.to_string(), clamped));
    }
}

impl Drop for BrightnessController {
    fn drop(&mut self) {
        let mut guard = self.monitors.lock();
        for old in guard.drain(..) {
            unsafe {
                let pm = PHYSICAL_MONITOR {
                    hPhysicalMonitor: old.hphys,
                    szPhysicalMonitorDescription: [0u16; 128],
                };
                let _ = DestroyPhysicalMonitors(&[pm]);
            }
        }
    }
}

fn query_monitor_hw_code(device_name: &str) -> Option<String> {
    let wide: Vec<u16> = device_name.encode_utf16().chain(std::iter::once(0)).collect();
    unsafe {
        let mut dd = DISPLAY_DEVICEW {
            cb: std::mem::size_of::<DISPLAY_DEVICEW>() as u32,
            ..Default::default()
        };
        if EnumDisplayDevicesW(PCWSTR(wide.as_ptr()), 0, &mut dd, 0).as_bool() {
            let n = dd.DeviceID.iter().position(|&c| c == 0).unwrap_or(dd.DeviceID.len());
            let dev_id = String::from_utf16_lossy(&dd.DeviceID[..n]);
            return extract_hw_code(&dev_id);
        }
    }
    None
}

pub fn extract_hw_code(device_id: &str) -> Option<String> {
    let parts: Vec<&str> = device_id.split('\\').collect();
    if parts.len() >= 2 && !parts[1].is_empty() {
        Some(parts[1].to_ascii_uppercase())
    } else {
        None
    }
}

/// Parses real EDID 18-byte descriptor blocks (tag `0xFC` monitor name) directly from HKLM\SYSTEM\CurrentControlSet\Enum\DISPLAY!
fn read_edid_friendly_names() -> HashMap<String, String> {
    let mut map = HashMap::new();
    unsafe {
        let base = windows::core::w!(r"SYSTEM\CurrentControlSet\Enum\DISPLAY");
        let mut h_disp = HKEY::default();
        if RegOpenKeyExW(HKEY_LOCAL_MACHINE, base, 0, KEY_READ, &mut h_disp).is_err() {
            return map;
        }
        let mut idx = 0u32;
        loop {
            let mut name_buf = [0u16; 128];
            let mut name_len = name_buf.len() as u32;
            if RegEnumKeyExW(h_disp, idx, windows::core::PWSTR(name_buf.as_mut_ptr()), &mut name_len, None, windows::core::PWSTR::null(), None, None).is_err() {
                break;
            }
            idx += 1;
            let hw = String::from_utf16_lossy(&name_buf[..name_len as usize]).to_ascii_uppercase();
            let mut h_hw = HKEY::default();
            if RegOpenKeyExW(h_disp, PCWSTR(name_buf.as_ptr()), 0, KEY_READ, &mut h_hw).is_ok() {
                let mut i2 = 0u32;
                loop {
                    let mut inst_buf = [0u16; 128];
                    let mut inst_len = inst_buf.len() as u32;
                    if RegEnumKeyExW(h_hw, i2, windows::core::PWSTR(inst_buf.as_mut_ptr()), &mut inst_len, None, windows::core::PWSTR::null(), None, None).is_err() {
                        break;
                    }
                    i2 += 1;
                    let inst = String::from_utf16_lossy(&inst_buf[..inst_len as usize]);
                    let dp: Vec<u16> = format!(r"{inst}\Device Parameters")
                        .encode_utf16()
                        .chain(std::iter::once(0))
                        .collect();
                    let mut h_dp = HKEY::default();
                    if RegOpenKeyExW(h_hw, PCWSTR(dp.as_ptr()), 0, KEY_READ, &mut h_dp).is_ok() {
                        let mut edid = [0u8; 256];
                        let mut sz = edid.len() as u32;
                        if RegQueryValueExW(h_dp, windows::core::w!("EDID"), None, None, Some(edid.as_mut_ptr()), Some(&mut sz)).is_ok() && sz >= 128 {
                            if let Some(model) = parse_edid_model_name(&edid[..sz as usize]) {
                                map.insert(hw.clone(), model);
                            }
                        }
                        let _ = RegCloseKey(h_dp);
                    }
                }
                let _ = RegCloseKey(h_hw);
            }
        }
        let _ = RegCloseKey(h_disp);
    }
    map
}

pub fn parse_edid_model_name(edid: &[u8]) -> Option<String> {
    if edid.len() < 128 {
        return None;
    }
    for offset in [54usize, 72, 90, 108] {
        if edid[offset] == 0 && edid[offset + 1] == 0 && edid[offset + 3] == 0xFC {
            let raw = &edid[offset + 5..offset + 18];
            let s: String = raw
                .iter()
                .take_while(|&&b| b != 0x0A && b != 0x00)
                .filter(|&&b| (0x20..=0x7E).contains(&b))
                .map(|&b| b as char)
                .collect();
            let trimmed = s.trim();
            if !trimmed.is_empty() {
                return Some(trimmed.to_string());
            }
        }
    }
    None
}
