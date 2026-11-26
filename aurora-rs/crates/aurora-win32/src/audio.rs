use aurora_core::{AudioDeviceInfo, AudioStateSnapshot, PerAppAudioSession};
use crossbeam_channel::Sender;
use windows::core::{implement, Interface, PCWSTR};
use windows::Win32::Devices::FunctionDiscovery::PKEY_Device_FriendlyName;
use windows::Win32::Media::Audio::Endpoints::{
    IAudioEndpointVolume, IAudioEndpointVolumeCallback, IAudioEndpointVolumeCallback_Impl,
};
use windows::Win32::Media::Audio::{
    eCapture, eConsole, eMultimedia, eRender, IAudioSessionControl2, IAudioSessionManager2,
    IMMDevice, IMMDeviceEnumerator, ISimpleAudioVolume, MMDeviceEnumerator,
    AUDIO_VOLUME_NOTIFICATION_DATA, DEVICE_STATE_ACTIVE,
};
use windows::Win32::System::Com::{
    CoCreateInstance, CLSCTX_ALL, CLSCTX_INPROC_SERVER, STGM_READ,
};

use crate::hardware_osd::post_wake_ui;
use crate::windows_catalog::query_process_image;

#[implement(IAudioEndpointVolumeCallback)]
struct VolumeNotifyCallback {
    tx: Sender<(u8, bool)>,
    wake_hwnd: isize,
}

impl IAudioEndpointVolumeCallback_Impl for VolumeNotifyCallback_Impl {
    fn OnNotify(&self, pnotify: *mut AUDIO_VOLUME_NOTIFICATION_DATA) -> windows::core::Result<()> {
        if !pnotify.is_null() {
            unsafe {
                let d = &*pnotify;
                let pct = (d.fMasterVolume.clamp(0.0, 1.0) * 100.0).round() as u8;
                let muted = d.bMuted.as_bool();
                let _ = self.tx.try_send((pct, muted));
                post_wake_ui(self.wake_hwnd);
            }
        }
        Ok(())
    }
}

/// Event-driven CoreAudio service with <2ms live `IAudioEndpointVolumeCallback` (catches USB/BT
/// headset wheels instantly without polling!) + Per-App Mixer + Mic Mute + Device Selection.
pub struct CoreAudioController {
    registered_vol: Option<IAudioEndpointVolume>,
    registered_cb: Option<IAudioEndpointVolumeCallback>,
}

impl Default for CoreAudioController {
    fn default() -> Self {
        Self::new()
    }
}

impl CoreAudioController {
    pub fn new() -> Self {
        Self {
            registered_vol: None,
            registered_cb: None,
        }
    }

    pub fn start_volume_callback(&mut self, tx: Sender<(u8, bool)>, wake_hwnd: isize) {
        self.stop_volume_callback();
        unsafe {
            let Ok(enumerator): windows::core::Result<IMMDeviceEnumerator> =
                CoCreateInstance(&MMDeviceEnumerator, None, CLSCTX_INPROC_SERVER)
            else {
                return;
            };
            let Ok(dev) = enumerator.GetDefaultAudioEndpoint(eRender, eMultimedia) else {
                return;
            };
            let Ok(ep_vol): windows::core::Result<IAudioEndpointVolume> = dev.Activate(CLSCTX_ALL, None) else {
                return;
            };
            let cb: IAudioEndpointVolumeCallback = VolumeNotifyCallback { tx, wake_hwnd }.into();
            if ep_vol.RegisterControlChangeNotify(&cb).is_ok() {
                self.registered_vol = Some(ep_vol);
                self.registered_cb = Some(cb);
            }
        }
    }

    pub fn stop_volume_callback(&mut self) {
        if let (Some(vol), Some(cb)) = (self.registered_vol.take(), self.registered_cb.take()) {
            unsafe {
                let _ = vol.UnregisterControlChangeNotify(&cb);
            }
        }
    }

    pub fn get_snapshot(&self) -> AudioStateSnapshot {
        let mut snap = AudioStateSnapshot::default();
        unsafe {
            let Ok(enumerator): windows::core::Result<IMMDeviceEnumerator> =
                CoCreateInstance(&MMDeviceEnumerator, None, CLSCTX_INPROC_SERVER)
            else {
                return snap;
            };

            // Default render device
            if let Ok(def_dev) = enumerator.GetDefaultAudioEndpoint(eRender, eMultimedia) {
                if let Ok(id_pw) = def_dev.GetId() {
                    snap.default_output_id = id_pw.to_string().unwrap_or_default();
                    windows::Win32::System::Com::CoTaskMemFree(Some(id_pw.0 as *const _));
                }
                snap.default_output_name = read_device_friendly_name(&def_dev);

                if let Ok(ep_vol) = def_dev.Activate::<IAudioEndpointVolume>(CLSCTX_ALL, None) {
                    if let Ok(scalar) = ep_vol.GetMasterVolumeLevelScalar() {
                        snap.master_volume_percent = (scalar.clamp(0.0, 1.0) * 100.0).round() as u8;
                    }
                    if let Ok(muted) = ep_vol.GetMute() {
                        snap.is_master_muted = muted.as_bool();
                    }
                }

                // Per-app audio sessions
                if let Ok(mgr) = def_dev.Activate::<IAudioSessionManager2>(CLSCTX_ALL, None) {
                    if let Ok(sess_enum) = mgr.GetSessionEnumerator() {
                        let count = sess_enum.GetCount().unwrap_or(0);
                        let mut seen_pids = std::collections::HashSet::new();
                        for i in 0..count {
                            let Ok(ctrl) = sess_enum.GetSession(i) else { continue };
                            let Ok(ctrl2) = ctrl.cast::<IAudioSessionControl2>() else { continue };
                            let pid = ctrl2.GetProcessId().unwrap_or(0);
                            if pid == 0 || !seen_pids.insert(pid) {
                                continue;
                            }
                            let Ok(sav) = ctrl.cast::<ISimpleAudioVolume>() else { continue };
                            let vol = (sav.GetMasterVolume().unwrap_or(1.0).clamp(0.0, 1.0) * 100.0).round() as u8;
                            let muted = sav.GetMute().map(|m| m.as_bool()).unwrap_or(false);
                            let (pname, _) = query_process_image(pid);
                            if pname.is_empty() || pname.eq_ignore_ascii_case("aurora") {
                                continue;
                            }
                            snap.app_sessions.push(PerAppAudioSession {
                                process_id: pid,
                                display_name: capitalize(&pname),
                                process_name: pname,
                                volume_percent: vol,
                                is_muted: muted,
                                routed_device_id: None,
                            });
                        }
                    }
                }
            }

            // Enumerate all active render outputs
            if let Ok(coll) = enumerator.EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE) {
                let count = coll.GetCount().unwrap_or(0);
                for i in 0..count {
                    let Ok(dev) = coll.Item(i) else { continue };
                    let id = dev
                        .GetId()
                        .map(|p| {
                            let s = p.to_string().unwrap_or_default();
                            windows::Win32::System::Com::CoTaskMemFree(Some(p.0 as *const _));
                            s
                        })
                        .unwrap_or_default();
                    let name = read_device_friendly_name(&dev);
                    let is_default = id.eq_ignore_ascii_case(&snap.default_output_id);
                    snap.outputs.push(AudioDeviceInfo { id, name, is_default });
                }
            }

            // Default capture (microphone)
            if let Ok(mic_dev) = enumerator.GetDefaultAudioEndpoint(eCapture, eConsole) {
                if let Ok(mic_vol) = mic_dev.Activate::<IAudioEndpointVolume>(CLSCTX_ALL, None) {
                    snap.mic_supported = true;
                    snap.is_mic_muted = mic_vol.GetMute().map(|m| m.as_bool()).unwrap_or(false);
                }
            }
        }
        snap
    }

    pub fn set_master_volume(&self, pct: u8) {
        unsafe {
            if let Ok(enumerator) = CoCreateInstance::<_, IMMDeviceEnumerator>(&MMDeviceEnumerator, None, CLSCTX_INPROC_SERVER) {
                if let Ok(dev) = enumerator.GetDefaultAudioEndpoint(eRender, eMultimedia) {
                    if let Ok(ep) = dev.Activate::<IAudioEndpointVolume>(CLSCTX_ALL, None) {
                        let scalar = (pct.min(100) as f32) / 100.0;
                        let _ = ep.SetMasterVolumeLevelScalar(scalar, std::ptr::null());
                        if pct > 0 {
                            let _ = ep.SetMute(false, std::ptr::null());
                        }
                    }
                }
            }
        }
    }

    pub fn toggle_master_mute(&self) -> bool {
        unsafe {
            if let Ok(enumerator) = CoCreateInstance::<_, IMMDeviceEnumerator>(&MMDeviceEnumerator, None, CLSCTX_INPROC_SERVER) {
                if let Ok(dev) = enumerator.GetDefaultAudioEndpoint(eRender, eMultimedia) {
                    if let Ok(ep) = dev.Activate::<IAudioEndpointVolume>(CLSCTX_ALL, None) {
                        let next = !ep.GetMute().map(|m| m.as_bool()).unwrap_or(false);
                        let _ = ep.SetMute(next, std::ptr::null());
                        return next;
                    }
                }
            }
        }
        false
    }

    pub fn toggle_mic_mute(&self) -> Option<bool> {
        unsafe {
            let enumerator = CoCreateInstance::<_, IMMDeviceEnumerator>(&MMDeviceEnumerator, None, CLSCTX_INPROC_SERVER).ok()?;
            let dev = enumerator.GetDefaultAudioEndpoint(eCapture, eConsole).ok()?;
            let ep = dev.Activate::<IAudioEndpointVolume>(CLSCTX_ALL, None).ok()?;
            let next = !ep.GetMute().map(|m| m.as_bool()).unwrap_or(false);
            let _ = ep.SetMute(next, std::ptr::null());
            Some(next)
        }
    }

    pub fn set_app_volume(&self, target_pid: u32, pct: u8) {
        unsafe {
            let Ok(enumerator) = CoCreateInstance::<_, IMMDeviceEnumerator>(&MMDeviceEnumerator, None, CLSCTX_INPROC_SERVER) else {
                return;
            };
            let Ok(dev) = enumerator.GetDefaultAudioEndpoint(eRender, eMultimedia) else { return };
            let Ok(mgr) = dev.Activate::<IAudioSessionManager2>(CLSCTX_ALL, None) else { return };
            let Ok(sess_enum) = mgr.GetSessionEnumerator() else { return };
            let count = sess_enum.GetCount().unwrap_or(0);
            for i in 0..count {
                let Ok(ctrl) = sess_enum.GetSession(i) else { continue };
                let Ok(ctrl2) = ctrl.cast::<IAudioSessionControl2>() else { continue };
                if ctrl2.GetProcessId().unwrap_or(0) == target_pid {
                    if let Ok(sav) = ctrl.cast::<ISimpleAudioVolume>() {
                        let _ = sav.SetMasterVolume((pct.min(100) as f32) / 100.0, std::ptr::null());
                        if pct > 0 {
                            let _ = sav.SetMute(false, std::ptr::null());
                        }
                    }
                }
            }
        }
    }

    pub fn toggle_app_mute(&self, target_pid: u32) -> Option<bool> {
        unsafe {
            let enumerator: IMMDeviceEnumerator =
                CoCreateInstance(&MMDeviceEnumerator, None, CLSCTX_INPROC_SERVER).ok()?;
            let dev = enumerator.GetDefaultAudioEndpoint(eRender, eMultimedia).ok()?;
            let mgr = dev.Activate::<IAudioSessionManager2>(CLSCTX_ALL, None).ok()?;
            let sess_enum = mgr.GetSessionEnumerator().ok()?;
            let count = sess_enum.GetCount().unwrap_or(0);
            let mut result = None;
            for i in 0..count {
                let Ok(ctrl) = sess_enum.GetSession(i) else { continue };
                let Ok(ctrl2) = ctrl.cast::<IAudioSessionControl2>() else { continue };
                if ctrl2.GetProcessId().unwrap_or(0) == target_pid {
                    if let Ok(sav) = ctrl.cast::<ISimpleAudioVolume>() {
                        let next = !sav.GetMute().map(|m| m.as_bool()).unwrap_or(false);
                        let _ = sav.SetMute(next, std::ptr::null());
                        result = Some(next);
                    }
                }
            }
            result
        }
    }

    pub fn adjust_app_volume(&self, target_pid: u32, delta: i8) -> Option<(u8, bool)> {
        unsafe {
            let enumerator: IMMDeviceEnumerator =
                CoCreateInstance(&MMDeviceEnumerator, None, CLSCTX_INPROC_SERVER).ok()?;
            let dev = enumerator.GetDefaultAudioEndpoint(eRender, eMultimedia).ok()?;
            let mgr = dev.Activate::<IAudioSessionManager2>(CLSCTX_ALL, None).ok()?;
            let sess_enum = mgr.GetSessionEnumerator().ok()?;
            let count = sess_enum.GetCount().unwrap_or(0);
            let mut result = None;
            for i in 0..count {
                let Ok(ctrl) = sess_enum.GetSession(i) else { continue };
                let Ok(ctrl2) = ctrl.cast::<IAudioSessionControl2>() else { continue };
                if ctrl2.GetProcessId().unwrap_or(0) == target_pid {
                    if let Ok(sav) = ctrl.cast::<ISimpleAudioVolume>() {
                        let cur = (sav.GetMasterVolume().unwrap_or(1.0).clamp(0.0, 1.0) * 100.0).round() as i16;
                        let next = (cur + delta as i16).clamp(0, 100) as u8;
                        let _ = sav.SetMasterVolume(next as f32 / 100.0, std::ptr::null());
                        let muted = if next > 0 {
                            let _ = sav.SetMute(false, std::ptr::null());
                            false
                        } else {
                            sav.GetMute().map(|m| m.as_bool()).unwrap_or(false)
                        };
                        result = Some((next, muted));
                    }
                }
            }
            result
        }
    }

    pub fn cycle_default_output_device(&self) -> Option<String> {
        let snap = self.get_snapshot();
        if snap.outputs.len() < 2 {
            return None;
        }
        let cur = snap.outputs.iter().position(|o| o.is_default).unwrap_or(0);
        let next = &snap.outputs[(cur + 1) % snap.outputs.len()];
        if self.set_default_output_device(&next.id) {
            Some(next.name.clone())
        } else {
            None
        }
    }

    /// Sets system default audio output device via undocumented standard Windows `IPolicyConfig` COM interface.
    pub fn set_default_output_device(&self, device_id: &str) -> bool {
        if device_id.is_empty() {
            return false;
        }
        // Standard IPolicyConfigVista / IPolicyConfig CLSID ({870af99c-171d-4f9e-af0d-e63df40c2bc9})
        // IID: {f8679f50-850a-41cf-9c72-430f290290c8}
        #[repr(C)]
        struct IPolicyConfigVtbl {
            // IUnknown (3) + 10 methods before SetDefaultEndpoint (slot 13)
            query_interface: usize,
            add_ref: usize,
            release: unsafe extern "system" fn(*mut std::ffi::c_void) -> u32,
            _pad: [usize; 10],
            set_default_endpoint: unsafe extern "system" fn(*mut std::ffi::c_void, PCWSTR, u32) -> i32,
        }
        let clsid = windows::core::GUID::from_u128(0x870af99c_171d_4f9e_af0d_e63df40c2bc9);
        let iid = windows::core::GUID::from_u128(0xf8679f50_850a_41cf_9c72_430f290290c8);
        let wide: Vec<u16> = device_id.encode_utf16().chain(std::iter::once(0)).collect();
        unsafe {
            let mut raw_ptr: *mut std::ffi::c_void = std::ptr::null_mut();
            let hr = windows::Win32::System::Com::CoCreateInstance(
                &clsid,
                None,
                CLSCTX_ALL,
            );
            // Fallback raw CoCreateInstance via ole32 if needed
            let _ = hr as windows::core::Result<windows::core::IUnknown>;
            let ok = raw_co_create_policy_config(&clsid, &iid, &mut raw_ptr);
            if ok && !raw_ptr.is_null() {
                let vtbl = *(raw_ptr as *const *const IPolicyConfigVtbl);
                let mut success = false;
                for role in [0u32, 1u32, 2u32] {
                    if ((*vtbl).set_default_endpoint)(raw_ptr, PCWSTR(wide.as_ptr()), role) == 0 {
                        success = true;
                    }
                }
                ((*vtbl).release)(raw_ptr);
                return success;
            }
        }
        false
    }
}

unsafe fn raw_co_create_policy_config(
    clsid: &windows::core::GUID,
    iid: &windows::core::GUID,
    out: &mut *mut std::ffi::c_void,
) -> bool {
    type CoCreateInstanceFn = unsafe extern "system" fn(
        *const windows::core::GUID,
        *mut std::ffi::c_void,
        u32,
        *const windows::core::GUID,
        *mut *mut std::ffi::c_void,
    ) -> i32;
    let Ok(hmod) = windows::Win32::System::LibraryLoader::GetModuleHandleW(windows::core::w!("ole32.dll")) else {
        return false;
    };
    let Some(proc) = windows::Win32::System::LibraryLoader::GetProcAddress(hmod, windows::core::s!("CoCreateInstance")) else {
        return false;
    };
    let func: CoCreateInstanceFn = std::mem::transmute(proc);
    func(clsid, std::ptr::null_mut(), CLSCTX_ALL.0, iid, out) == 0
}

impl Drop for CoreAudioController {
    fn drop(&mut self) {
        self.stop_volume_callback();
    }
}

unsafe fn read_device_friendly_name(dev: &IMMDevice) -> String {
    let Ok(store) = dev.OpenPropertyStore(STGM_READ) else {
        return "Hoparlör".into();
    };
    let Ok(prop) = store.GetValue(&PKEY_Device_FriendlyName) else {
        return "Hoparlör".into();
    };
    let raw = prop.to_string();
    short_audio_name(&raw)
}

pub fn short_audio_name(raw: &str) -> String {
    let trimmed = raw.trim();
    if trimmed.is_empty() {
        return "Hoparlör".into();
    }
    if let (Some(open), Some(close)) = (trimmed.find('('), trimmed.rfind(')')) {
        if open >= 2 && close > open + 1 {
            let inner = trimmed[open + 1..close]
                .trim()
                .replace("(R)", "")
                .replace("(TM)", "")
                .replace("High Definition Audio", "HD Audio");
            if inner.len() >= 2 {
                let s = inner.trim();
                return if s.chars().count() > 22 {
                    format!("{}…", s.chars().take(21).collect::<String>())
                } else {
                    s.to_string()
                };
            }
        }
    }
    if trimmed.chars().count() > 22 {
        format!("{}…", trimmed.chars().take(21).collect::<String>())
    } else {
        trimmed.to_string()
    }
}

fn capitalize(s: &str) -> String {
    let mut c = s.chars();
    match c.next() {
        None => String::new(),
        Some(f) => f.to_uppercase().collect::<String>() + c.as_str(),
    }
}
