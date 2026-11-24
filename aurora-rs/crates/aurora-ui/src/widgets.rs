use std::f32::consts::PI;
use aurora_core::{Color, ResolvedTheme, VisualStyle};
use aurora_render::{FontFamily, IconKind, Painter, RectF, TextAlign};

use crate::{HitTree, TextInputState, UiAction};

/// Tactile pill button obeying the active 2026 theme's geometry, border, and hover contrast.
#[allow(clippy::too_many_arguments)]
pub fn draw_pill_button(
    p: &Painter<'_>,
    hits: &mut HitTree,
    id: &str,
    r: RectF,
    label: &str,
    icon: Option<IconKind>,
    t: &ResolvedTheme,
    accent: bool,
    hovered: bool,
    action: UiAction,
) {
    let rad = t.radius_btn.min(r.h * 0.5);
    if accent {
        if matches!(t.style, VisualStyle::BentoSilk | VisualStyle::LinearPrecision | VisualStyle::MonoMatrix) {
            p.fill_rounded_rect(r, rad, t.accent1);
        } else if t.style == VisualStyle::SpatialVision {
            p.fill_linear_gradient_rounded(r, rad, Color::WHITE.with_alpha(0.32), t.accent1.with_alpha(0.55), false);
            p.stroke_rounded_rect(r, rad, Color::WHITE.with_alpha(0.50), 1.0, false);
        } else {
            p.fill_linear_gradient_rounded(r, rad, t.accent1, t.accent2, false);
        }
    } else {
        let bg = if hovered {
            t.card_hover
        } else if t.style == VisualStyle::BentoSilk && t.is_light {
            Color::WHITE.with_alpha(0.88)
        } else {
            t.card_bg
        };
        p.fill_rounded_rect(r, rad, bg);
        p.stroke_rounded_rect(
            r,
            rad,
            if hovered { t.accent_ring } else { t.border_subtle },
            1.0,
            t.style == VisualStyle::MonoMatrix && hovered,
        );
    }

    let fg = if accent {
        if t.style == VisualStyle::SpatialVision { t.text_primary } else { t.accent_ink }
    } else if hovered {
        t.text_primary
    } else {
        t.text_primary.with_alpha(0.92)
    };

    let fam = if t.style.uses_monospace_headers() { FontFamily::Mono } else { FontFamily::Ui };
    if let Some(ic) = icon {
        let ic_sz = (r.h * 0.48).clamp(11.0, 15.0);
        if label.is_empty() {
            p.draw_icon(ic, r.center_x(), r.center_y(), ic_sz, fg);
        } else {
            p.draw_icon(ic, r.x + 12.0, r.center_y(), ic_sz, fg);
            p.draw_text(
                label,
                RectF::new(r.x + 22.0, r.y, r.w - 28.0, r.h),
                11.0,
                600,
                fg,
                fam,
                TextAlign::Left,
                true,
                false,
            );
        }
    } else {
        p.draw_text(label, r.inset(6.0, 0.0), 11.0, 600, fg, fam, TextAlign::Center, true, false);
    }

    hits.push(id, r, action);
}

#[allow(clippy::too_many_arguments)]
pub fn draw_micro_button(
    p: &Painter<'_>,
    hits: &mut HitTree,
    id: &str,
    r: RectF,
    icon: IconKind,
    t: &ResolvedTheme,
    active: bool,
    danger: bool,
    hovered: bool,
    action: UiAction,
    tooltip: Option<&str>,
) {
    let rad = match t.style {
        VisualStyle::MonoMatrix | VisualStyle::SpatialVision => r.w * 0.5,
        VisualStyle::CyberHud => 3.0,
        VisualStyle::LinearPrecision => 6.0,
        _ => 9.0,
    };
    let (bg, border, fg) = if danger && hovered {
        (t.danger.with_alpha(0.22), t.danger, t.danger)
    } else if active {
        (t.accent_tint, t.accent_ring, t.accent1)
    } else if hovered {
        (t.card_hover, t.accent_ring, t.text_primary)
    } else {
        (t.card_bg, t.border_subtle, t.text_secondary)
    };

    p.fill_rounded_rect(r, rad, bg);
    p.stroke_rounded_rect(r, rad, border, 1.0, t.style == VisualStyle::MonoMatrix && hovered);
    p.draw_icon(icon, r.center_x(), r.center_y(), (r.w * 0.50).clamp(12.0, 15.0), fg);

    if let Some(tip) = tooltip {
        hits.push_with_tip(id, r, action, tip);
    } else {
        hits.push(id, r, action);
    }
}

/// Control Center toggle tile (Wi-Fi, Bluetooth, Odak, Uyanık Tut, Mikrofon, etc.) with theme architecture!
#[allow(clippy::too_many_arguments)]
pub fn draw_toggle_tile(
    p: &Painter<'_>,
    hits: &mut HitTree,
    id: &str,
    r: RectF,
    icon: IconKind,
    title: &str,
    subtitle: &str,
    on: bool,
    hovered: bool,
    t: &ResolvedTheme,
    action: UiAction,
) {
    let rad = (t.radius_card - 2.0).max(3.0);
    let bg = if on {
        t.accent_soft
    } else if hovered {
        t.card_hover
    } else if t.style == VisualStyle::BentoSilk && t.is_light {
        Color::WHITE.with_alpha(0.92)
    } else {
        t.card_bg
    };
    let border = if on { t.accent_ring } else if hovered { t.border_rim } else { t.border_subtle };

    p.fill_rounded_rect(r, rad, bg);
    p.stroke_rounded_rect(r, rad, border, if on && t.style == VisualStyle::MonoMatrix { 1.4 } else { 1.0 }, t.style == VisualStyle::MonoMatrix && hovered && !on);

    // Linear Swiss & Cyber HUD left active rail
    if on && matches!(t.style, VisualStyle::LinearPrecision | VisualStyle::CyberHud) {
        p.fill_rect(RectF::new(r.x + 1.0, r.y + 3.0, 3.0, r.h - 6.0), t.accent1);
    }

    // Icon badge
    let ic_box = RectF::new(r.x + 10.0, r.center_y() - 14.0, 28.0, 28.0);
    let ic_rad = match t.style {
        VisualStyle::MonoMatrix | VisualStyle::SpatialVision => 14.0,
        VisualStyle::CyberHud => 3.0,
        VisualStyle::LinearPrecision => 5.0,
        _ => 9.0,
    };
    p.fill_rounded_rect(ic_box, ic_rad, if on { t.accent1 } else { t.inset_bg });
    p.draw_icon(icon, ic_box.center_x(), ic_box.center_y(), 14.0, if on { t.accent_ink } else { t.text_secondary });

    // Labels
    p.draw_text(
        title,
        RectF::new(r.x + 45.0, r.y + 8.0, r.w - 53.0, 16.0),
        11.5,
        700,
        t.text_primary,
        FontFamily::Ui,
        TextAlign::Left,
        true,
        false,
    );
    p.draw_text(
        subtitle,
        RectF::new(r.x + 45.0, r.y + 24.0, r.w - 53.0, 14.0),
        10.0,
        500,
        if on { t.accent1 } else { t.text_secondary },
        FontFamily::Ui,
        TextAlign::Left,
        true,
        false,
    );

    hits.push(id, r, action);
}

/// Interactive horizontal hardware slider (Master Volume, Per-App Volume, Monitor DDC/CI Brightness, Opacity).
#[allow(clippy::too_many_arguments)]
pub fn draw_slider(
    p: &Painter<'_>,
    hits: &mut HitTree,
    id: &str,
    r: RectF,
    label: &str,
    value_text: &str,
    percent: f32, // 0.0..=1.0
    icon: Option<IconKind>,
    t: &ResolvedTheme,
    hovered: bool,
    slider_track_id: &str,
) {
    let pct = percent.clamp(0.0, 1.0);
    // Label row
    let lbl_x = if let Some(ic) = icon {
        p.draw_icon(ic, r.x + 8.0, r.y + 9.0, 13.0, t.accent1);
        r.x + 21.0
    } else {
        r.x
    };
    p.draw_text(
        label,
        RectF::new(lbl_x, r.y, r.w - 56.0, 18.0),
        11.0,
        600,
        t.text_primary,
        FontFamily::Ui,
        TextAlign::Left,
        true,
        false,
    );
    p.draw_text(
        value_text,
        RectF::new(r.right() - 44.0, r.y, 44.0, 18.0),
        11.0,
        700,
        t.accent1,
        FontFamily::Mono,
        TextAlign::Right,
        true,
        false,
    );

    // Track
    let track = RectF::new(r.x, r.y + 22.0, r.w, 6.0);
    p.fill_rounded_rect(track, 3.0, t.inset_bg);
    let fill_w = (track.w * pct).max(6.0);
    p.fill_linear_gradient_rounded(RectF::new(track.x, track.y, fill_w, track.h), 3.0, t.accent1, t.accent2, false);

    // Thumb knob
    let thumb_x = track.x + (track.w - 10.0) * pct + 5.0;
    let thumb_r = if hovered { 7.0 } else { 5.8 };
    if hovered {
        p.fill_radial_glow(thumb_x, track.center_y(), 12.0, 12.0, t.accent_glow);
    }
    p.fill_circle(thumb_x, track.center_y(), thumb_r, Color::WHITE);
    p.stroke_circle(thumb_x, track.center_y(), thumb_r, t.accent1, 1.8, false);

    let hit_r = RectF::new(track.x - 4.0, track.y - 8.0, track.w + 8.0, 22.0);
    hits.push(
        id,
        hit_r,
        UiAction::SliderTrack {
            id: slider_track_id.to_string(),
            rect_x: track.x.round() as i32,
            rect_w: track.w.round().max(1.0) as i32,
        },
    );
}

/// Theme-aware Tab Bar (Liquid Glass pill, Bento Silk porcelain bar, Linear Swiss underline,
/// Mono Matrix opaque inverted pill, Spatial Vision frosted lens, Cyber HUD top-laser!).
pub fn draw_tab_bar(
    p: &Painter<'_>,
    hits: &mut HitTree,
    r: RectF,
    tabs: &[(&str, &str, IconKind)],
    active_id: &str,
    hovered_id: Option<&str>,
    t: &ResolvedTheme,
) {
    let rad = match t.style {
        VisualStyle::LinearPrecision => 0.0,
        VisualStyle::CyberHud => 3.0,
        VisualStyle::BentoSilk => 12.0,
        _ => r.h * 0.5,
    };

    if t.style == VisualStyle::LinearPrecision {
        p.draw_line(r.x, r.bottom() - 0.5, r.right(), r.bottom() - 0.5, t.border_subtle, 1.0, false);
    } else {
        p.fill_rounded_rect(r, rad, t.inset_bg);
        p.stroke_rounded_rect(r, rad, t.border_subtle, 1.0, t.style == VisualStyle::MonoMatrix);
    }

    let pad = if t.style == VisualStyle::LinearPrecision { 0.0 } else { 3.0 };
    let inner = r.inset(pad, pad);
    let tab_w = inner.w / tabs.len().max(1) as f32;
    let fam = if t.style.uses_monospace_headers() { FontFamily::Mono } else { FontFamily::Ui };

    for (i, &(tid, label, icon)) in tabs.iter().enumerate() {
        let tr = RectF::new(inner.x + i as f32 * tab_w, inner.y, tab_w - 2.0, inner.h);
        let is_act = tid == active_id;
        let hit_id = format!("tab:{tid}");
        let is_hov = hovered_id == Some(hit_id.as_str());
        let trad = (rad - 2.0).max(2.0);

        let fg = if is_act {
            match t.style {
                VisualStyle::LinearPrecision => {
                    p.fill_rounded_rect(tr, 5.0, t.accent_soft);
                    p.fill_rect(RectF::new(tr.x + 2.0, tr.bottom() - 2.0, tr.w - 4.0, 2.0), t.accent1);
                    t.text_primary
                }
                VisualStyle::MonoMatrix => {
                    // Opaque inverted surface1_solid text (fixes v2.5.1 semi-transparent tab text bug!)
                    p.fill_rounded_rect(tr, trad, t.text_primary);
                    t.surface1_solid
                }
                VisualStyle::SpatialVision => {
                    p.fill_rounded_rect(tr, trad, if t.is_light { Color::WHITE } else { Color::WHITE.with_alpha(0.24) });
                    p.stroke_rounded_rect(tr, trad, Color::WHITE.with_alpha(0.45), 1.0, false);
                    t.text_primary
                }
                VisualStyle::CyberHud => {
                    p.fill_rounded_rect(tr, 2.0, t.accent_tint);
                    p.stroke_rounded_rect(tr, 2.0, t.accent1.with_alpha(0.65), 1.0, false);
                    p.fill_rect(RectF::new(tr.x, tr.y, tr.w, 2.0), t.accent1);
                    t.accent1
                }
                VisualStyle::BentoSilk if t.is_light => {
                    p.draw_soft_shadow(tr, 9.0, 0.12, 1.0, 3.0);
                    p.fill_rounded_rect(tr, 9.0, Color::WHITE);
                    t.text_primary
                }
                _ => {
                    p.fill_linear_gradient_rounded(tr, trad, t.accent1, t.accent2, false);
                    t.accent_ink
                }
            }
        } else {
            if is_hov {
                p.fill_rounded_rect(tr, trad, t.card_hover);
            }
            if is_hov { t.text_primary } else { t.text_secondary }
        };

        let text_lbl = if t.style.uses_monospace_headers() { label.to_uppercase() } else { label.to_string() };
        let ic_x = tr.x + 14.0;
        p.draw_icon(icon, ic_x, tr.center_y(), 12.5, fg);
        p.draw_text(
            &text_lbl,
            RectF::new(tr.x + 24.0, tr.y, tr.w - 28.0, tr.h),
            10.8,
            if is_act { 700 } else { 600 },
            fg,
            fam,
            TextAlign::Left,
            true,
            false,
        );
        hits.push(hit_id, tr, UiAction::SwitchDeckTab(tid.to_string()));
    }
}

/// Native DirectWrite text input box with Turkish IME, placeholder, and crisp caret.
pub fn draw_text_input(
    p: &Painter<'_>,
    hits: &mut HitTree,
    id: &str,
    r: RectF,
    input: &TextInputState,
    t: &ResolvedTheme,
    leading_icon: Option<IconKind>,
) {
    let rad = match t.style {
        VisualStyle::SpatialVision | VisualStyle::MonoMatrix => r.h * 0.5,
        VisualStyle::CyberHud => 3.0,
        VisualStyle::LinearPrecision => 7.0,
        _ => 12.0,
    };
    let bg = if t.style == VisualStyle::BentoSilk && t.is_light {
        Color::WHITE
    } else {
        t.inset_bg
    };
    p.fill_rounded_rect(r, rad, bg);
    p.stroke_rounded_rect(
        r,
        rad,
        if input.focused { t.accent1 } else { t.border_rim },
        if input.focused { 1.5 } else { 1.0 },
        false,
    );
    if t.style == VisualStyle::CyberHud {
        p.fill_rect(RectF::new(r.x, r.y + 2.0, 3.0, r.h - 4.0), t.accent1);
    }

    let text_x = if let Some(ic) = leading_icon {
        p.draw_icon(ic, r.x + 16.0, r.center_y(), 14.0, if input.focused { t.accent1 } else { t.text_secondary });
        r.x + 30.0
    } else {
        r.x + 12.0
    };
    let text_r = RectF::new(text_x, r.y, (r.right() - text_x - 10.0).max(10.0), r.h);

    if input.text.is_empty() {
        p.draw_text(&input.placeholder, text_r, 12.0, 500, t.text_secondary.with_alpha(0.75), FontFamily::Ui, TextAlign::Left, true, false);
    } else {
        p.draw_text(&input.text, text_r, 12.0, 600, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
    }

    if input.focused {
        let prefix_w = p.measure_text_width(input.text_before_cursor(), 12.0, 600, FontFamily::Ui);
        let cx = (text_x + prefix_w).min(text_r.right());
        p.draw_line(cx, r.y + 7.0, cx, r.bottom() - 7.0, t.accent1, 1.8, false);
    }

    hits.push(id, r, UiAction::FocusTextInput(id.to_string()));
}

/// Converts HSV (`h: 0..360, s: 0..1, v: 0..1`) to sRGB `Color`.
pub fn hsv_to_color(h: f32, s: f32, v: f32) -> Color {
    let c = v * s;
    let x = c * (1.0 - (((h / 60.0) % 2.0) - 1.0).abs());
    let m = v - c;
    let (r, g, b) = match (h as i32).rem_euclid(360) {
        0..=59 => (c, x, 0.0),
        60..=119 => (x, c, 0.0),
        120..=179 => (0.0, c, x),
        180..=239 => (0.0, x, c),
        240..=299 => (x, 0.0, c),
        _ => (c, 0.0, x),
    };
    Color { r: r + m, g: g + m, b: b + m, a: 1.0 }
}

/// 2D HSV Live Color Picker rendered natively on GPU (no `<input type="color">` OS dialog stutter!).
#[allow(clippy::too_many_arguments)]
pub fn draw_hsv_color_picker(
    p: &Painter<'_>,
    hits: &mut HitTree,
    r: RectF,
    slot: &str,
    hue: f32,
    sat: f32,
    val: f32,
    t: &ResolvedTheme,
) {
    let sv_r = RectF::new(r.x, r.y, r.w, (r.h - 20.0).max(40.0));
    // Silky-smooth continuous Direct2D HSV gradient (2 GPU draw calls instead of coarse blocks!)
    let pure_hue = hsv_to_color(hue, 1.0, 1.0);
    p.fill_linear_gradient_rounded(sv_r, 8.0, Color::WHITE, pure_hue, false);
    p.fill_linear_gradient_rounded(sv_r, 8.0, Color::TRANSPARENT, Color::BLACK, true);
    p.stroke_rounded_rect(sv_r, 8.0, t.border_rim, 1.0, false);

    // Reticle at (sat, 1-val)
    let rx = sv_r.x + sat.clamp(0.0, 1.0) * sv_r.w;
    let ry = sv_r.y + (1.0 - val.clamp(0.0, 1.0)) * sv_r.h;
    p.stroke_circle(rx, ry, 6.0, Color::WHITE, 2.0, false);
    p.stroke_circle(rx, ry, 7.2, Color::BLACK.with_alpha(0.55), 1.0, false);

    hits.push(
        format!("cp-sv:{slot}"),
        sv_r,
        UiAction::ColorPickerArea {
            slot: slot.to_string(),
            rect_x: sv_r.x.round() as i32,
            rect_y: sv_r.y.round() as i32,
            rect_w: sv_r.w.round().max(1.0) as i32,
            rect_h: sv_r.h.round().max(1.0) as i32,
        },
    );

    // Rainbow Hue Bar
    let hue_r = RectF::new(r.x, sv_r.bottom() + 6.0, r.w, 12.0);
    let hclip = p.push_rounded_clip(hue_r, 6.0);
    let segs = 18;
    let sw = hue_r.w / segs as f32;
    for i in 0..segs {
        let h0 = (i as f32 / segs as f32) * 360.0;
        let h1 = ((i + 1) as f32 / segs as f32) * 360.0;
        p.fill_linear_gradient_rounded(
            RectF::new(hue_r.x + i as f32 * sw, hue_r.y, sw + 0.8, hue_r.h),
            0.0,
            hsv_to_color(h0, 1.0, 1.0),
            hsv_to_color(h1, 1.0, 1.0),
            false,
        );
    }
    if hclip { p.pop_rounded_clip(); }
    p.stroke_rounded_rect(hue_r, 6.0, t.border_rim, 1.0, false);
    let hx = hue_r.x + (hue.rem_euclid(360.0) / 360.0) * hue_r.w;
    p.fill_circle(hx, hue_r.center_y(), 6.5, Color::WHITE);
    p.stroke_circle(hx, hue_r.center_y(), 6.5, Color::BLACK.with_alpha(0.5), 1.2, false);

    hits.push(
        format!("cp-hue:{slot}"),
        hue_r.inset(-2.0, -3.0),
        UiAction::ColorHueBar {
            slot: slot.to_string(),
            rect_x: hue_r.x.round() as i32,
            rect_w: hue_r.w.round().max(1.0) as i32,
        },
    );
}

/// Computes circular orb coordinates inside the Radial Window Orb Hub.
pub fn radial_orb_positions(cx: f32, cy: f32, count: usize, ring_radius: f32) -> Vec<(f32, f32)> {
    if count == 0 { return vec![]; }
    if count == 1 { return vec![(cx, cy)]; }
    (0..count)
        .map(|i| {
            let ang = -PI * 0.5 + (2.0 * PI * i as f32) / count as f32;
            (cx + ang.cos() * ring_radius, cy + ang.sin() * ring_radius)
        })
        .collect()
}
