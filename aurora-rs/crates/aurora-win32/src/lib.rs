pub mod ai_quota;
pub mod appbar;
pub mod audio;
pub mod bluetooth;
pub mod brightness;
pub mod hardware_osd;
pub mod hooks_and_overlays;
pub mod market_and_local_ai;
pub mod media;
pub mod monitors;
pub mod notifications;
pub mod privacy_and_metrics;
pub mod productivity;
pub mod shell_extras;
pub mod wallpaper_accent;
pub mod windows_catalog;

pub use ai_quota::*;
pub use appbar::*;
pub use audio::*;
pub use bluetooth::*;
pub use brightness::*;
pub use hardware_osd::*;
pub use hooks_and_overlays::*;
pub use market_and_local_ai::*;
pub use media::*;
pub use monitors::*;
pub use notifications::*;
pub use privacy_and_metrics::*;
pub use productivity::*;
pub use shell_extras::*;
pub use wallpaper_accent::*;
pub use windows_catalog::*;

#[cfg(test)]
mod tests {
    use super::*;
    use aurora_core::PhysicalRect;

    #[test]
    fn snap_zone_layouts_cover_work_area_cleanly() {
        let wa = PhysicalRect::new(0, 0, 2560, 1388);
        for preset in ["code6535", "studio3", "cols3", "grid2x2"] {
            let zones = compute_snap_zones(wa, preset, 8);
            assert!(zones.len() >= 2, "preset {preset} must define >= 2 zones");
            for &(_, z) in &zones {
                assert!(z.width > 100 && z.height > 100);
                assert!(z.x >= wa.x && z.right() <= wa.right());
                assert!(z.y >= wa.y && z.bottom() <= wa.bottom());
            }
        }
    }

    #[test]
    fn safe_process_terminator_blocks_critical_system_processes() {
        for critical in ["explorer", "csrss", "wininit", "lsass", "dwm", "aurora", "System"] {
            assert!(!is_safe_to_terminate(critical), "{critical} must never be terminatable");
        }
        assert!(is_safe_to_terminate("notepad"));
    }

    #[test]
    fn enumerates_live_monitors_with_valid_dpi() {
        let mons = enumerate_monitors();
        assert!(!mons.is_empty());
        assert!(mons.iter().any(|m| m.descriptor.is_primary));
        for m in &mons {
            assert!(m.descriptor.bounds.width >= 640 && m.descriptor.bounds.height >= 480);
            assert!(m.descriptor.effective_dpi >= 96);
        }
    }

    #[test]
    fn qr_airdrop_matrix_and_http_roundtrip() {
        let tmp = std::env::temp_dir().join("aurora_v3_qr_test.txt");
        std::fs::write(&tmp, b"Aurora v3 QR payload").unwrap();
        let mut srv = LanQrShareServer::default();
        let info = srv.start_sharing(&tmp).expect("QR share server should start");
        assert!(info.url.starts_with("http://"));
        assert!(info.matrix_size >= 21);
        assert_eq!(info.modules.len(), info.matrix_size * info.matrix_size);
        srv.stop();
        let _ = std::fs::remove_file(tmp);
    }

    #[test]
    fn dedicated_low_level_keyboard_hook_starts_and_stops_cleanly() {
        let (tx, _rx) = crossbeam_channel::bounded(8);
        let mut hook = LowLevelKeyboardHook::new();
        assert!(hook.start(tx, 0, true, true, false), "WH_KEYBOARD_LL thread must install cleanly");
        hook.update_flags(true, false, true);
        hook.stop();
    }

    #[test]
    fn volume_step_matches_windows_2_percent_alignment() {
        assert_eq!(compute_next_volume_step(50, true), 52);
        assert_eq!(compute_next_volume_step(51, true), 52);
        assert_eq!(compute_next_volume_step(100, true), 100);
        assert_eq!(compute_next_volume_step(50, false), 48);
        assert_eq!(compute_next_volume_step(1, false), 0);
    }

    #[test]
    fn alt_drag_strictly_excludes_turkish_altgr_and_shell_switchers() {
        // Pure Left-Alt only!
        assert!(is_pure_left_alt_drag(true, false, false));
        // Turkish Q AltGr sends VK_RMENU (often with synthetic VK_LCONTROL) -> MUST NEVER hijack clicks!
        assert!(!is_pure_left_alt_drag(false, true, false));
        assert!(!is_pure_left_alt_drag(false, true, true));
        assert!(!is_pure_left_alt_drag(true, true, false));
        assert!(!is_pure_left_alt_drag(true, false, true));
        assert!(!is_pure_left_alt_drag(false, false, false));

        // Shell Alt+Tab / Desktop / Language switcher must NEVER be dragged!
        for cls in [
            "Progman",
            "WorkerW",
            "Shell_TrayWnd",
            "XamlExplorerHostIslandWindow",
            "MultitaskingViewFrame",
            "Shell_InputSwitchTopLevelWindow",
        ] {
            assert!(is_forbidden_alt_drag_class(cls), "{cls} must be excluded from Alt+Drag");
        }
        assert!(!is_forbidden_alt_drag_class("Chrome_WidgetWin_1"));
    }

    #[test]
    fn windowsapps_msix_aumid_and_manifest_icon_extraction_succeeds() {
        // Always verify real Win32 executable icon extraction works on every Windows desktop/server host
        let notepad_icon = extract_icon_png_base64(None, r"C:\Windows\System32\notepad.exe", None);
        assert!(
            notepad_icon.as_deref().is_some_and(|s| s.len() > 200),
            "System notepad.exe icon must extract cleanly"
        );

        // Verify built-in Windows Store AUMIDs extract crisp PNG base64 icons on interactive Windows desktops
        // (Headless Windows Server GitHub Actions runners may lack Client Store ImmersiveControlPanel provisioning)
        let settings_icon = extract_icon_png_base64(
            None,
            "",
            Some("windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel"),
        );
        if std::env::var_os("GITHUB_ACTIONS").is_none() {
            assert!(
                settings_icon.as_deref().is_some_and(|s| s.len() > 200),
                "Store Settings AUMID icon must extract cleanly via shell:AppsFolder"
            );
        } else if let Some(s) = settings_icon.as_deref() {
            assert!(s.len() > 200, "Store Settings AUMID icon returned invalid PNG");
        }

        // Verify nonexistent WindowsApps exe never returns a bogus generic white placeholder
        let bogus = extract_icon_png_base64(
            None,
            r"C:\Program Files\WindowsApps\Fake.MissingPkg_1.0\WhatsApp.Root.exe",
            None,
        );
        assert!(bogus.is_none(), "Unreadable WindowsApps exe without AUMID must never cache generic exe icon");
    }

    #[test]
    fn windowsapps_sandbox_detector_and_backslash_path_healer_work() {
        assert!(is_windows_apps_sandbox_path(
            r"C:\Program Files\WindowsApps\5319275A.WhatsAppDesktop_2.24\WhatsApp.Root.exe"
        ));
        assert!(!is_windows_apps_sandbox_path(
            r"C:\Users\Lenovo\AppData\Local\Programs\antigravity\Antigravity.exe"
        ));

        // Create a deterministic temporary nested file, strip all backslashes from its path,
        // and verify `repair_corrupted_windows_path` reconstructs the exact original path!
        let base = std::env::temp_dir().join("aurora_v5_path_heal_test");
        let nested = base.join("NestedFolder").join("DemoApp.exe");
        let _ = std::fs::create_dir_all(nested.parent().unwrap());
        std::fs::write(&nested, b"test").unwrap();
        let canonical = nested.to_string_lossy().to_string();
        let stripped = canonical.replace('\\', "");
        let healed = repair_corrupted_windows_path(&stripped);
        let _ = std::fs::remove_dir_all(&base);
        assert!(
            healed.as_deref().is_some_and(|h| h.eq_ignore_ascii_case(&canonical)),
            "expected '{canonical}', got '{healed:?}' from '{stripped}'"
        );
    }

    #[test]
    fn media_session_query_from_sta_thread_never_deadlocks_or_fails() {
        let handle = std::thread::spawn(|| {
            unsafe {
                let _ = windows::Win32::System::Com::CoInitializeEx(
                    None,
                    windows::Win32::System::Com::COINIT_APARTMENTTHREADED,
                );
            }
            query_media_session()
        });
        let snap = handle.join().expect("STA caller thread must never panic or deadlock");
        eprintln!(
            "LIVE_MEDIA_SNAP: has_session={} playing={} app='{}' title='{}' artist='{}' has_art={}",
            snap.has_session,
            snap.is_playing,
            snap.app_name,
            snap.title,
            snap.artist,
            snap.thumbnail_bytes.is_some()
        );
    }

    #[test]
    fn fullscreen_video_detection_hides_dock_without_false_positives_on_maximized_apps() {
        let mon = PhysicalRect::new(0, 0, 2560, 1440);
        let work_reserved = PhysicalRect::new(0, 0, 2560, 1388); // 52px fixed Dock reserved

        // 1. Normal maximized browser/IDE in Fixed-Bar mode -> MUST NOT hide Dock!
        assert!(!is_window_fullscreen_on_monitor(work_reserved, mon, work_reserved, true, false));
        // 2. Normal maximized browser/IDE in Floating/AutoHide mode (work_area == bounds) -> MUST NOT hide Dock!
        assert!(!is_window_fullscreen_on_monitor(mon, mon, mon, true, false));
        // 3. Fullscreen YouTube in Opera/Chrome/Edge/Firefox (Fixed-Bar mode) -> MUST hide Dock + Crown!
        assert!(is_window_fullscreen_on_monitor(mon, mon, work_reserved, false, false));
        // 4. Fullscreen YouTube in Opera/Chrome/Edge/Firefox (Floating mode) -> MUST hide Dock + Crown!
        assert!(is_window_fullscreen_on_monitor(mon, mon, mon, false, false));
        // 5. Fullscreen video/app covering reserved Dock even if WS_MAXIMIZE lingered -> MUST hide Dock + Crown!
        assert!(is_window_fullscreen_on_monitor(mon, mon, work_reserved, true, false));
        // 6. D3D exclusive fullscreen game -> MUST hide Dock + Crown!
        assert!(is_window_fullscreen_on_monitor(mon, mon, mon, true, true));
    }
}
