use std::path::Path;
use aurora_core::{built_in_themes, Color, HardwareOsdEvent};
use aurora_render::{render_offscreen_to_png, GpuContext, Painter, RectF};
use aurora_surfaces::{paint_full_shell_frame, ActiveOsd, ActiveToast, OverlayPopup, ShellState};
use aurora_ui::HitTree;

/// Paints a realistic wallpaper gradient behind offscreen PNG captures so glass opacity,
/// specular rims, soft shadows, and WCAG contrast can be audited accurately pixel-by-pixel!
fn paint_audit_wallpaper(p: &Painter<'_>, w: f32, h: f32, is_light: bool) {
    let r = RectF::new(0.0, 0.0, w, h);
    if is_light {
        p.fill_linear_gradient_rounded(r, 0.0, Color::from_hex("#DFE7F2"), Color::from_hex("#C9D6E8"), true);
        p.fill_radial_glow(w * 0.25, h * 0.28, w * 0.35, h * 0.40, Color::from_hex("#93C5FD").with_alpha(0.32));
        p.fill_radial_glow(w * 0.78, h * 0.72, w * 0.35, h * 0.38, Color::from_hex("#C4B5FD").with_alpha(0.28));
    } else {
        p.fill_linear_gradient_rounded(r, 0.0, Color::from_hex("#070B16"), Color::from_hex("#111B33"), true);
        p.fill_radial_glow(w * 0.22, h * 0.24, w * 0.36, h * 0.42, Color::from_hex("#1D4ED8").with_alpha(0.24));
        p.fill_radial_glow(w * 0.76, h * 0.68, w * 0.38, h * 0.42, Color::from_hex("#7C3AED").with_alpha(0.20));
    }
}

fn capture_state(gpu: &GpuContext, out_path: &Path, st: &ShellState) -> anyhow::Result<()> {
    let is_light = st.theme.is_light;
    render_offscreen_to_png(gpu, 1280, 720, 96.0, out_path, |p, w, h| {
        paint_audit_wallpaper(p, w, h, is_light);
        let mut hits = HitTree::default();
        paint_full_shell_frame(p, &mut hits, st, w, h, true);
    })
}

/// Renders a comprehensive visual verification suite of ALL 7 built-in 2026 visual themes +
/// all 4 Bento tabs + Floating Dock + Radial Hub + OmniSearch + Calendar + Toast + 6-Step Tour!
pub fn run_visual_capture_suite(out_dir: &Path) -> anyhow::Result<()> {
    std::fs::create_dir_all(out_dir)?;
    let gpu = GpuContext::new()?;

    // 1. ALL 7 BUILT-IN THEMES (Crown Droplet + Expanded Control Deck + Pinned Fixed Bar)
    for th in built_in_themes() {
        let mut st = ShellState::demo_for_capture(&th.id);
        st.crown_expanded = true;
        st.active_deck_tab = "control".into();
        let path = out_dir.join(format!("theme_{}.png", th.visual_style.as_str()));
        capture_state(&gpu, &path, &st)?;
    }

    // 2. All 4 Bento Deck Tabs on Liquid Glass 26
    for tab in ["shelf", "metrics", "studio"] {
        let mut st = ShellState::demo_for_capture("aurora-glass-dark");
        st.crown_expanded = true;
        st.active_deck_tab = tab.into();
        capture_state(&gpu, &out_dir.join(format!("deck_{tab}.png")), &st)?;
    }

    // 3. Studio Tab on Bento Silk (Light theme verification!)
    {
        let mut st = ShellState::demo_for_capture("aurora-glass-light");
        st.crown_expanded = true;
        st.active_deck_tab = "studio".into();
        capture_state(&gpu, &out_dir.join("deck_studio_light.png"), &st)?;
    }

    // 4. Floating Crystal Dock + Hardware Volume OSD + Floating Clickable Toast
    {
        let mut st = ShellState::demo_for_capture("aurora-glass-dark");
        st.prefs.reserve_dock_work_area = Some(false);
        st.prefs.dock_visibility_mode = Some("SmartDodge".into());
        st.active_osd = Some(ActiveOsd {
            event: HardwareOsdEvent {
                kind: "volume".into(),
                label: "Ses".into(),
                value_text: "74%".into(),
                percent: Some(74),
                active: true,
                duration_ms: 1500,
            },
            expires_at_ms: u64::MAX,
        });
        st.active_toast = st.notifications.first().cloned().map(|n| ActiveToast {
            notification: n,
            expires_at_ms: u64::MAX,
        });
        st.dock_hover_cursor = Some((1280.0 * 0.45, 720.0 - 34.0));
        capture_state(&gpu, &out_dir.join("floating_dock_osd_toast.png"), &st)?;
    }

    // 5. OmniSearch + Calculator
    {
        let mut st = ShellState::demo_for_capture("aurora-glass-dark");
        st.open_popup = OverlayPopup::OmniSearch;
        capture_state(&gpu, &out_dir.join("popup_omnisearch.png"), &st)?;
    }

    // 6. Radial Window Orb Hub
    {
        let mut st = ShellState::demo_for_capture("aurora-flow-dark");
        st.open_popup = OverlayPopup::RadialHub("code".into());
        capture_state(&gpu, &out_dir.join("popup_radial_hub.png"), &st)?;
    }

    // 7. Calendar & Agenda + Pinned Fixed Bar (3-Zone Split)
    {
        let mut st = ShellState::demo_for_capture("aurora-glass-dark");
        st.prefs.dock_bar_layout = "Split3Zone".into();
        st.open_popup = OverlayPopup::Calendar;
        capture_state(&gpu, &out_dir.join("popup_calendar_3zone.png"), &st)?;
    }

    // 8. 6-Step Interactive Tour (Step 1 Dark & Step 2 Light)
    {
        let mut st = ShellState::demo_for_capture("aurora-glass-dark");
        st.open_popup = OverlayPopup::Tour(0);
        capture_state(&gpu, &out_dir.join("tour_step1_dark.png"), &st)?;
    }
    {
        let mut st = ShellState::demo_for_capture("aurora-glass-light");
        st.open_popup = OverlayPopup::Tour(1);
        capture_state(&gpu, &out_dir.join("tour_step2_light.png"), &st)?;
    }

    Ok(())
}
