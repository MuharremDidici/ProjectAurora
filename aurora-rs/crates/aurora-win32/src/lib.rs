pub mod appbar;
pub mod audio;
pub mod bluetooth;
pub mod brightness;
pub mod hardware_osd;
pub mod hooks_and_overlays;
pub mod media;
pub mod monitors;
pub mod notifications;
pub mod privacy_and_metrics;
pub mod productivity;
pub mod shell_extras;
pub mod windows_catalog;

pub use appbar::*;
pub use audio::*;
pub use bluetooth::*;
pub use brightness::*;
pub use hardware_osd::*;
pub use hooks_and_overlays::*;
pub use media::*;
pub use monitors::*;
pub use notifications::*;
pub use privacy_and_metrics::*;
pub use productivity::*;
pub use shell_extras::*;
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
}
