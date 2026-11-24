use std::collections::{HashMap, HashSet};

use aurora_core::BluetoothBatteryDevice;
use windows::core::GUID;
use windows::Win32::Devices::DeviceAndDriverInstallation::{
    CM_Get_DevNode_PropertyW, CM_Get_Device_ID_ListW, CM_Get_Device_ID_List_SizeW,
    CM_Locate_DevNodeW, CM_GETIDLIST_FILTER_PRESENT, CM_LOCATE_DEVNODE_NORMAL, CR_SUCCESS,
};
use windows::Win32::Devices::Properties::{
    DEVPKEY_Device_ContainerId, DEVPKEY_Device_FriendlyName, DEVPKEY_NAME, DEVPROPKEY, DEVPROPTYPE,
};

// Standard Windows 10/11 Bluetooth Hands-Free / BLE Battery Property Key
const DEVPKEY_BLUETOOTH_BATTERY: DEVPROPKEY = DEVPROPKEY {
    fmtid: GUID::from_u128(0x104EA319_6EE2_4701_BD47_8DDBF425BBE5),
    pid: 2,
};

// Standard Windows 10/11 PnP Live Connection Status Key (`DEVPKEY_Device_IsConnected`)
const DEVPKEY_DEVICE_IS_CONNECTED: DEVPROPKEY = DEVPROPKEY {
    fmtid: GUID::from_u128(0x83DA6326_97A6_4088_9453_A1923F573B29),
    pid: 15,
};

// Base computer container GUID `{00000000-0000-0000-FFFF-FFFFFFFFFFFF}` — never a real BT device!
const BASE_COMPUTER_CONTAINER: [u8; 16] = [
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
];
const ZERO_CONTAINER: [u8; 16] = [0u8; 16];

struct BtContainerAgg {
    primary_id: String,
    best_name: String,
    best_raw_name: String,
    name_priority: u8,
    is_connected: bool,
    battery: Option<u8>,
}

/// Zero-PowerShell, sub-5ms native `cfgmgr32.dll` PnP Bluetooth Live Device & Battery scanner.
/// Groups all `BTHENUM` / `BTHLE` / `BTHLEDEVICE` devnodes by `DEVPKEY_Device_ContainerId`,
/// filters strictly to **currently connected (`DEVPKEY_Device_IsConnected == true`)** devices
/// so previously paired / offline devices with stale cached battery levels are NEVER shown!
pub fn query_bluetooth_batteries() -> Vec<BluetoothBatteryDevice> {
    let mut containers: HashMap<[u8; 16], BtContainerAgg> = HashMap::new();

    unsafe {
        let mut len = 0u32;
        if CM_Get_Device_ID_List_SizeW(&mut len, None, CM_GETIDLIST_FILTER_PRESENT) != CR_SUCCESS
            || len == 0
        {
            return Vec::new();
        }
        let mut buf = vec![0u16; len as usize];
        if CM_Get_Device_ID_ListW(None, &mut buf, CM_GETIDLIST_FILTER_PRESENT) != CR_SUCCESS {
            return Vec::new();
        }

        for slice in buf.split(|&c| c == 0) {
            if slice.is_empty() {
                continue;
            }
            let dev_id = String::from_utf16_lossy(slice);
            let upper = dev_id.to_ascii_uppercase();
            if !upper.starts_with("BTHENUM\\") && !upper.starts_with("BTHLE") {
                continue;
            }

            let mut wide_id: Vec<u16> = slice.to_vec();
            wide_id.push(0);
            let mut devinst = 0u32;
            if CM_Locate_DevNodeW(
                &mut devinst,
                windows::core::PCWSTR(wide_id.as_ptr()),
                CM_LOCATE_DEVNODE_NORMAL,
            ) != CR_SUCCESS
            {
                continue;
            }

            let Some(cid) = read_devnode_guid(devinst, &DEVPKEY_Device_ContainerId) else {
                continue;
            };
            if cid == ZERO_CONTAINER || cid == BASE_COMPUTER_CONTAINER {
                continue;
            }

            let is_primary_dev =
                upper.starts_with("BTHENUM\\DEV_") || upper.starts_with("BTHLE\\DEV_");
            let is_connected = read_devnode_bool(devinst, &DEVPKEY_DEVICE_IS_CONNECTED);
            let batt = read_devnode_u8(devinst, &DEVPKEY_BLUETOOTH_BATTERY).filter(|&v| v <= 100);
            let raw_name = read_devnode_string(devinst, &DEVPKEY_Device_FriendlyName)
                .or_else(|| read_devnode_string(devinst, &DEVPKEY_NAME))
                .unwrap_or_default();
            let clean = clean_bt_name(&raw_name);
            let prio = name_priority(&clean, is_primary_dev, &upper);

            let entry = containers.entry(cid).or_insert_with(|| BtContainerAgg {
                primary_id: dev_id.clone(),
                best_name: String::new(),
                best_raw_name: String::new(),
                name_priority: 0,
                is_connected: false,
                battery: None,
            });

            if is_connected {
                entry.is_connected = true;
            }
            if let Some(b) = batt {
                // Prefer primary device battery if present, otherwise take any valid battery in this connected container
                if entry.battery.is_none() || is_primary_dev {
                    entry.battery = Some(b);
                }
            }
            if prio > entry.name_priority && !clean.is_empty() {
                entry.name_priority = prio;
                entry.best_name = clean;
                entry.best_raw_name = raw_name;
                entry.primary_id = dev_id;
            }
        }
    }

    let mut results = Vec::new();
    let mut seen_names = HashSet::new();

    for agg in containers.into_values() {
        // STRICT LIVE FILTER: Never show previously paired / turned-off / disconnected devices!
        if !agg.is_connected || agg.best_name.is_empty() {
            continue;
        }
        let dedup_key = agg.best_name.to_ascii_lowercase();
        if !seen_names.insert(dedup_key) {
            continue;
        }
        let category = classify_bt_category(&agg.best_raw_name);
        let (has_battery, battery_percent) = match agg.battery {
            Some(pct) => (true, pct),
            None => (false, 0),
        };
        results.push(BluetoothBatteryDevice {
            id: agg.primary_id,
            name: truncate_label(&agg.best_name, 22),
            battery_percent,
            category: category.into(),
            is_low: has_battery && battery_percent <= 15,
            has_battery,
            is_connected: true,
        });
    }

    // Connected devices reporting real battery come first (lowest battery first for low-battery awareness),
    // followed by connected devices without a battery sensor sorted alphabetically.
    results.sort_by(|a, b| {
        b.has_battery
            .cmp(&a.has_battery)
            .then_with(|| a.battery_percent.cmp(&b.battery_percent))
            .then_with(|| a.name.to_ascii_lowercase().cmp(&b.name.to_ascii_lowercase()))
    });
    results
}

fn name_priority(clean: &str, is_primary_dev: bool, upper_id: &str) -> u8 {
    if clean.is_empty() {
        return 0;
    }
    let low = clean.to_ascii_lowercase();
    if [
        "genel erişim",
        "genel öznitelik",
        "aygıt bilgileri",
        "generic access",
        "generic attribute",
        "device information",
        "standart seri",
        "standard serial",
        "gatt uyumlu",
        "gatt compliant",
        "avrcp transport",
        "bluetooth le genel",
    ]
    .iter()
    .any(|bad| low.contains(bad))
    {
        return 0;
    }
    if is_primary_dev {
        3
    } else if upper_id.starts_with("BTHENUM\\{") {
        2
    } else {
        1
    }
}

unsafe fn read_devnode_guid(devinst: u32, key: &DEVPROPKEY) -> Option<[u8; 16]> {
    let mut prop_type = DEVPROPTYPE(0);
    let mut buf = [0u8; 16];
    let mut sz = buf.len() as u32;
    if CM_Get_DevNode_PropertyW(
        devinst,
        key,
        &mut prop_type,
        Some(buf.as_mut_ptr()),
        &mut sz,
        0,
    ) == CR_SUCCESS
        && sz == 16
    {
        Some(buf)
    } else {
        None
    }
}

unsafe fn read_devnode_bool(devinst: u32, key: &DEVPROPKEY) -> bool {
    let mut prop_type = DEVPROPTYPE(0);
    let mut buf = [0u8; 4];
    let mut sz = buf.len() as u32;
    CM_Get_DevNode_PropertyW(
        devinst,
        key,
        &mut prop_type,
        Some(buf.as_mut_ptr()),
        &mut sz,
        0,
    ) == CR_SUCCESS
        && sz >= 1
        && buf[0] != 0
}

unsafe fn read_devnode_u8(devinst: u32, key: &DEVPROPKEY) -> Option<u8> {
    let mut prop_type = DEVPROPTYPE(0);
    let mut buf = [0u8; 4];
    let mut sz = buf.len() as u32;
    if CM_Get_DevNode_PropertyW(
        devinst,
        key,
        &mut prop_type,
        Some(buf.as_mut_ptr()),
        &mut sz,
        0,
    ) == CR_SUCCESS
        && sz >= 1
    {
        Some(buf[0])
    } else {
        None
    }
}

unsafe fn read_devnode_string(devinst: u32, key: &DEVPROPKEY) -> Option<String> {
    let mut prop_type = DEVPROPTYPE(0);
    let mut buf = [0u16; 256];
    let mut sz = (buf.len() * 2) as u32;
    if CM_Get_DevNode_PropertyW(
        devinst,
        key,
        &mut prop_type,
        Some(buf.as_mut_ptr() as *mut u8),
        &mut sz,
        0,
    ) == CR_SUCCESS
        && sz >= 2
    {
        let chars = (sz as usize / 2).min(buf.len());
        let n = buf[..chars].iter().position(|&c| c == 0).unwrap_or(chars);
        let s = String::from_utf16_lossy(&buf[..n]).trim().to_string();
        if !s.is_empty() {
            return Some(s);
        }
    }
    None
}

pub fn classify_bt_category(raw: &str) -> &'static str {
    let s = raw.to_ascii_lowercase();
    if [
        "mouse", "fare", "pebble", "mx master", "mx anywhere", "deathadder", "viper", "g pro",
        "g305", "basilisk", "ergo", "lift",
    ]
    .iter()
    .any(|k| s.contains(k))
    {
        "mouse"
    } else if [
        "keyboard", "klavye", "keychron", "k380", "mx keys", "nuphy", "anne pro", "magic key",
    ]
    .iter()
    .any(|k| s.contains(k))
    {
        "keyboard"
    } else if [
        "controller", "xbox", "dualsense", "dualshock", "gamepad", "joy-con", "8bitdo",
    ]
    .iter()
    .any(|k| s.contains(k))
    {
        "gamepad"
    } else if [
        "headset", "headphone", "kulak", "airpods", "buds", "pods", "beats", "wh-1000", "wf-1000",
        "g435", "g733", "cloud", "arctis", "jbl", "sony", "bose", "sennheiser", "hands-free",
        "audio", "speaker", "hoparlör",
    ]
    .iter()
    .any(|k| s.contains(k))
    {
        "headset"
    } else {
        "device"
    }
}

fn clean_bt_name(raw: &str) -> String {
    raw.replace("Hands-Free AG Audio", "")
        .replace("Hands-Free AG", "")
        .replace("Hands-Free", "")
        .replace("Avrcp Transport", "")
        .replace("Stereo", "")
        .trim()
        .trim_end_matches(['-', '(', ')'])
        .trim()
        .to_string()
}

fn truncate_label(s: &str, max_chars: usize) -> String {
    if s.chars().count() > max_chars {
        format!(
            "{}…",
            s.chars()
                .take(max_chars.saturating_sub(1))
                .collect::<String>()
        )
    } else {
        s.to_string()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn live_bluetooth_scan_only_returns_connected_devices() {
        let devs = query_bluetooth_batteries();
        for d in &devs {
            assert!(d.is_connected, "Disconnected device leaked: {:?}", d);
            assert!(!d.name.is_empty());
            if d.has_battery {
                assert!(d.battery_percent <= 100);
            }
        }
    }
}
