pub mod crown;
pub mod dock;
pub mod popups;
pub mod state;
pub mod tab_control;
pub mod tab_metrics;
pub mod tab_shelf;
pub mod tab_studio;

pub use crown::*;
pub use dock::*;
pub use popups::*;
pub use state::*;
pub use tab_control::*;
pub use tab_metrics::*;
pub use tab_shelf::*;
pub use tab_studio::*;

use aurora_render::Painter;
use aurora_ui::HitTree;

/// Renders the entire Aurora v3.0.0 shell frame for a monitor into Direct2D hardware context
/// and rebuilds the zero-polling `HitTree` (used directly by `WM_NCHITTEST`!).
pub fn paint_full_shell_frame(
    p: &Painter<'_>,
    hits: &mut HitTree,
    state: &ShellState,
    canvas_w: f32,
    canvas_h: f32,
    paint_crown: bool,
) {
    hits.clear();

    // 1. Crystal Dock / Edge-to-Edge Pinned Fixed Bar
    paint_dock(p, hits, state, canvas_w, canvas_h);

    // 2. Top Resting Crown Droplet + Expanded Bento Command Deck (primary monitor)
    if paint_crown {
        paint_crown_and_deck(p, hits, state, canvas_w);
    }

    // 3. Popups, OmniSearch, Radial Window Orb Hub, Calendar, Tour, Clickable Toasts & Overlays
    if paint_crown || matches!(state.open_popup, OverlayPopup::SnapOverlay | OverlayPopup::RegionSnip(_)) {
        paint_popups_and_overlays(p, hits, state, canvas_w, canvas_h);
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use aurora_core::built_in_themes;

    #[test]
    fn demo_fixture_builds_cleanly_for_all_seven_themes() {
        for th in built_in_themes() {
            let st = ShellState::demo_for_capture(&th.id);
            assert_eq!(st.theme_id, th.id);
            assert!(!st.dock_groups.is_empty());
            assert!(!st.omni_results.is_empty());
            assert!(st.theme.text_contrast_ratio >= 7.0);
        }
    }
}
