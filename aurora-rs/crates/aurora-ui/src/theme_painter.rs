use aurora_core::{Color, ResolvedTheme, VisualStyle};
use aurora_render::{FontFamily, Painter, RectF, TextAlign};

/// Unified architectural surface renderer for ALL 7 built-in 2026 visual styles.
/// Zero CSS cascade collisions, true unclipped Cyber HUD glow, opaque Mono Matrix tabs,
/// and WCAG AA+ verified contrast in every single theme!
pub struct ThemePainter;

impl ThemePainter {
    /// Renders a primary shell container (Expanded Bento Deck, Floating Dock, Popup Card, OmniBar).
    pub fn draw_shell(p: &Painter<'_>, r: RectF, t: &ResolvedTheme, is_pinned_bar: bool) {
        let radius = if is_pinned_bar { 0.0 } else { t.radius_shell };

        if !is_pinned_bar && t.shadow_intensity > 0.01 {
            p.draw_soft_shadow(r, radius, t.shadow_intensity, 8.0, 14.0);
        }

        match t.style {
            VisualStyle::LiquidGlass => {
                p.fill_linear_gradient_rounded(r, radius, t.surface1, t.surface2, true);
                let clipped = p.push_rounded_clip(r, radius);
                // Optical top refractive specular highlight + ambient accent blooms
                p.fill_linear_gradient_rounded(
                    RectF::new(r.x, r.y, r.w, (r.h * 0.42).min(110.0)),
                    radius,
                    Color::WHITE.with_alpha(if t.is_light { 0.40 } else { 0.12 }),
                    Color::TRANSPARENT,
                    true,
                );
                p.fill_radial_glow(r.x + r.w * 0.22, r.y, r.w * 0.42, r.h * 0.45, t.accent1.with_alpha(0.14));
                p.fill_radial_glow(r.x + r.w * 0.84, r.bottom(), r.w * 0.36, r.h * 0.42, t.accent2.with_alpha(0.10));
                if clipped { p.pop_rounded_clip(); }

                // Top specular glass rim line
                if !is_pinned_bar {
                    p.draw_line(
                        r.x + r.w * 0.14,
                        r.y + 1.0,
                        r.right() - r.w * 0.14,
                        r.y + 1.0,
                        Color::WHITE.with_alpha(if t.is_light { 0.85 } else { 0.36 }),
                        1.4,
                        false,
                    );
                }
                p.stroke_rounded_rect(r, radius, Color::WHITE.with_alpha(if t.is_light { 0.55 } else { 0.22 }), 1.0, false);
            }
            VisualStyle::BentoSilk => {
                p.fill_linear_gradient_rounded(r, radius, t.surface1, t.surface2, true);
                // Crisp porcelain top inner edge highlight (zero neon glow)
                p.draw_line(
                    r.x + radius * 0.5,
                    r.y + 1.0,
                    r.right() - radius * 0.5,
                    r.y + 1.0,
                    Color::WHITE.with_alpha(if t.is_light { 0.92 } else { 0.14 }),
                    1.0,
                    false,
                );
                let border = if t.is_light { Color::rgba(20, 22, 26, 0.14) } else { Color::rgba(255, 255, 255, 0.15) };
                p.stroke_rounded_rect(r, radius, border, 1.0, false);
            }
            VisualStyle::LinearPrecision => {
                // Matte anodized carbon with razor 1px hairline border, zero glow
                p.fill_rounded_rect(r, radius, t.surface1);
                p.draw_line(
                    r.x + 1.0,
                    r.y + 0.75,
                    r.right() - 1.0,
                    r.y + 0.75,
                    Color::WHITE.with_alpha(if t.is_light { 0.50 } else { 0.07 }),
                    1.0,
                    false,
                );
                p.stroke_rounded_rect(r, radius, t.border_rim, 1.0, false);
            }
            VisualStyle::MonoMatrix => {
                p.fill_linear_gradient_rounded(r, radius, t.surface1, t.surface2, true);
                let clipped = p.push_rounded_clip(r, radius);
                let dot_c = if t.is_light { Color::rgba(15, 23, 42, 0.11) } else { Color::rgba(255, 255, 255, 0.09) };
                p.draw_dot_matrix(r, 9.0, dot_c);
                if clipped { p.pop_rounded_clip(); }
                let border = if t.is_light { Color::rgba(15, 23, 42, 0.24) } else { Color::rgba(255, 255, 255, 0.22) };
                p.stroke_rounded_rect(r, radius, border, 1.4, is_pinned_bar);
            }
            VisualStyle::SpatialVision => {
                p.fill_linear_gradient_rounded(r, radius, t.surface1, t.surface2, true);
                let clipped = p.push_rounded_clip(r, radius);
                p.fill_radial_glow(r.center_x(), r.y, r.w * 0.55, r.h * 0.45, Color::WHITE.with_alpha(0.16));
                p.fill_radial_glow(r.x + r.w * 0.18, r.bottom(), r.w * 0.42, r.h * 0.45, t.accent1.with_alpha(0.15));
                p.fill_radial_glow(r.x + r.w * 0.85, r.bottom(), r.w * 0.38, r.h * 0.45, t.accent2.with_alpha(0.14));
                if clipped { p.pop_rounded_clip(); }
                // Double visionOS frosted glass rim
                p.stroke_rounded_rect(r, radius, Color::WHITE.with_alpha(0.32), 1.5, false);
                p.stroke_rounded_rect(r.inset(1.5, 1.5), (radius - 1.5).max(0.0), Color::WHITE.with_alpha(0.12), 0.75, false);
            }
            VisualStyle::CyberHud => {
                if is_pinned_bar {
                    p.fill_linear_gradient_rounded(r, 0.0, t.surface1, t.surface2, true);
                    p.push_clip(r);
                    p.draw_cyber_blueprint(r, t.accent1.with_alpha(0.06), t.accent1, t.accent2);
                    p.pop_clip();
                    // Laser top rail
                    p.draw_line(r.x, r.y + 1.0, r.right(), r.y + 1.0, t.accent1, 2.0, false);
                } else {
                    let cut = 8.0f32.min(r.h * 0.28);
                    let pts = [
                        (r.x + cut, r.y),
                        (r.right() - cut, r.y),
                        (r.right(), r.y + cut),
                        (r.right(), r.bottom() - cut),
                        (r.right() - cut, r.bottom()),
                        (r.x + cut, r.bottom()),
                        (r.x, r.bottom() - cut),
                        (r.x, r.y + cut),
                    ];
                    p.draw_chamfered_shell(&pts, t.surface1, t.surface2, t.accent1.with_alpha(0.55), t.accent1.with_alpha(0.55), 1.2);
                    p.push_clip(r.inset(2.0, 2.0));
                    p.draw_cyber_blueprint(r.inset(2.0, 2.0), t.accent1.with_alpha(0.055), t.accent1, t.accent2);
                    p.pop_clip();
                    p.draw_line(r.x + cut, r.y + 1.0, r.right() - cut, r.y + 1.0, t.accent1, 2.0, false);
                }
            }
            VisualStyle::HighContrast => {
                p.fill_rounded_rect(r, radius, t.surface1_solid);
                p.stroke_rounded_rect(r, radius, Color::WHITE, 2.0, false);
            }
        }
    }

    /// Renders the unmistakable top resting Crown Droplet silhouette per theme!
    pub fn draw_crown_droplet(p: &Painter<'_>, r: RectF, t: &ResolvedTheme, osd_accent: Option<Color>) {
        let accent = osd_accent.unwrap_or(t.accent1);
        match t.style {
            VisualStyle::CyberHud => {
                let inset = 11.0f32.min(r.w * 0.12);
                let pts = [
                    (r.x, r.y),
                    (r.right(), r.y),
                    (r.right() - inset, r.bottom()),
                    (r.x + inset, r.bottom()),
                ];
                p.draw_chamfered_shell(&pts, t.surface1, t.surface2, accent.with_alpha(0.85), accent.with_alpha(0.60), 1.5);
                p.draw_line(r.x + inset + 2.0, r.bottom() - 1.0, r.right() - inset - 2.0, r.bottom() - 1.0, accent, 2.0, false);
            }
            VisualStyle::BentoSilk | VisualStyle::MonoMatrix | VisualStyle::SpatialVision => {
                // Floating top capsule pill
                let rad = r.h * 0.5;
                if t.shadow_intensity > 0.01 {
                    p.draw_soft_shadow(r, rad, t.shadow_intensity * 0.75, 4.0, 8.0);
                }
                p.fill_linear_gradient_rounded(r, rad, t.surface1, t.surface2, true);
                if t.style == VisualStyle::MonoMatrix {
                    let clipped = p.push_rounded_clip(r, rad);
                    p.draw_dot_matrix(r, 7.0, Color::WHITE.with_alpha(if t.is_light { 0.12 } else { 0.09 }));
                    if clipped { p.pop_rounded_clip(); }
                }
                let rim = if t.style == VisualStyle::SpatialVision {
                    Color::WHITE.with_alpha(0.36)
                } else {
                    t.border_rim
                };
                p.stroke_rounded_rect(r, rad, rim, if t.style == VisualStyle::BentoSilk { 1.0 } else { 1.4 }, false);
            }
            VisualStyle::LinearPrecision => {
                p.draw_soft_shadow(r, 6.0, 0.32, 3.0, 6.0);
                p.fill_rounded_rect(r, 6.0, t.surface1);
                p.stroke_rounded_rect(r, 6.0, t.border_rim, 1.0, false);
                p.fill_rect(RectF::new(r.x + 1.0, r.y + 3.0, 2.5, r.h - 6.0), accent);
            }
            VisualStyle::LiquidGlass | VisualStyle::HighContrast => {
                let rad = if t.style == VisualStyle::HighContrast { 6.0 } else { 16.0 };
                if t.style == VisualStyle::LiquidGlass {
                    p.draw_soft_shadow(r, rad, 0.42, 4.0, 9.0);
                }
                // Flat top flush with monitor edge, curved droplet bottom
                p.fill_linear_gradient_rounded(
                    RectF::new(r.x, r.y - rad * 0.5, r.w, r.h + rad * 0.5),
                    rad,
                    t.surface1,
                    t.surface2,
                    true,
                );
                p.stroke_rounded_rect(
                    RectF::new(r.x, r.y - rad * 0.5, r.w, r.h + rad * 0.5),
                    rad,
                    if t.style == VisualStyle::HighContrast { Color::WHITE } else { Color::WHITE.with_alpha(0.22) },
                    if t.style == VisualStyle::HighContrast { 2.0 } else { 1.0 },
                    false,
                );
                // Bottom luminous droplet arc
                p.draw_line(r.x + r.w * 0.22, r.bottom() - 1.5, r.right() - r.w * 0.22, r.bottom() - 1.5, accent.with_alpha(0.85), 2.0, false);
            }
        }
    }

    /// Renders an inner Bento Card with theme-specific borders, title header, and elevation.
    pub fn draw_card(p: &Painter<'_>, r: RectF, title: &str, t: &ResolvedTheme) {
        let rad = t.radius_card;
        match t.style {
            VisualStyle::BentoSilk if t.is_light => {
                p.draw_soft_shadow(r, rad, 0.14, 2.0, 5.0);
                p.fill_rounded_rect(r, rad, Color::WHITE.with_alpha(0.95));
                p.stroke_rounded_rect(r, rad, Color::rgba(20, 22, 26, 0.10), 1.0, false);
            }
            VisualStyle::MonoMatrix => {
                p.fill_rounded_rect(r, rad, t.card_bg);
                p.stroke_rounded_rect(r, rad, t.border_rim, 1.0, true);
            }
            VisualStyle::CyberHud => {
                p.fill_rounded_rect(r, rad, t.accent1.with_alpha(0.04));
                p.stroke_rounded_rect(r, rad, t.accent1.with_alpha(0.26), 1.0, false);
                p.draw_line(r.x, r.y + 1.0, r.x + 8.0, r.y + 1.0, t.accent1, 1.8, false);
                p.draw_line(r.x + 1.0, r.y, r.x + 1.0, r.y + 8.0, t.accent1, 1.8, false);
            }
            VisualStyle::SpatialVision => {
                p.fill_rounded_rect(r, rad, Color::WHITE.with_alpha(0.065));
                p.stroke_rounded_rect(r, rad, Color::WHITE.with_alpha(0.20), 1.0, false);
            }
            VisualStyle::HighContrast => {
                p.fill_rounded_rect(r, rad, Color::from_hex("#121212"));
                p.stroke_rounded_rect(r, rad, Color::WHITE.with_alpha(0.80), 1.5, false);
            }
            _ => {
                p.fill_rounded_rect(r, rad, t.card_bg);
                p.stroke_rounded_rect(r, rad, t.border_subtle, 1.0, false);
            }
        }

        if !title.is_empty() {
            let fam = if t.style.uses_monospace_headers() { FontFamily::Mono } else { FontFamily::Ui };
            let formatted = match t.style {
                VisualStyle::CyberHud => format!("// {}", title.to_uppercase()),
                VisualStyle::MonoMatrix => format!("▪ {}", title.to_uppercase()),
                _ => title.to_uppercase(),
            };
            p.draw_text(
                &formatted,
                RectF::new(r.x + 12.0, r.y + 8.0, r.w - 24.0, 16.0),
                10.0,
                700,
                if t.style == VisualStyle::CyberHud { t.accent1 } else { t.text_secondary },
                fam,
                TextAlign::Left,
                true,
                false,
            );
            if t.style == VisualStyle::LinearPrecision {
                p.draw_line(r.x + 10.0, r.y + 26.0, r.right() - 10.0, r.y + 26.0, t.border_subtle, 1.0, false);
            }
        }
    }

    /// Renders a Dock App Icon frame + active/running indicator according to the active 2026 visual style.
    #[allow(clippy::too_many_arguments)]
    pub fn draw_dock_item_chrome(
        p: &Painter<'_>,
        r: RectF,
        t: &ResolvedTheme,
        hovered: bool,
        active: bool,
        running: bool,
        attention: bool,
        vertical_edge: Option<&str>, // Some("Left") | Some("Right") | None (Bottom)
    ) {
        let rad = match t.style {
            VisualStyle::MonoMatrix | VisualStyle::SpatialVision => r.w * 0.5,
            _ => t.radius_item,
        };

        // Frame background & border
        if attention {
            p.fill_rounded_rect(r, rad, t.warning.with_alpha(0.18));
            p.stroke_rounded_rect(r, rad, t.warning, 1.5, false);
        } else if active {
            match t.style {
                VisualStyle::BentoSilk if t.is_light => {
                    p.draw_soft_shadow(r, rad, 0.16, 2.0, 4.0);
                    p.fill_rounded_rect(r, rad, Color::WHITE);
                    p.stroke_rounded_rect(r, rad, Color::rgba(20, 22, 26, 0.14), 1.0, false);
                }
                VisualStyle::SpatialVision => {
                    p.fill_radial_glow(r.center_x(), r.center_y(), r.w * 0.72, r.h * 0.72, t.accent_glow);
                    p.fill_rounded_rect(r, rad, t.accent1.with_alpha(0.24));
                    p.stroke_rounded_rect(r, rad, Color::WHITE.with_alpha(0.56), 1.5, false);
                }
                VisualStyle::MonoMatrix => {
                    p.fill_rounded_rect(r, rad, t.accent_soft);
                    p.stroke_rounded_rect(r, rad, t.accent1, 1.5, false);
                }
                VisualStyle::CyberHud => {
                    p.fill_linear_gradient_rounded(r, rad, t.accent1.with_alpha(0.22), t.accent2.with_alpha(0.10), true);
                    p.stroke_rounded_rect(r, rad, t.accent1, 1.2, false);
                }
                _ => {
                    p.fill_rounded_rect(r, rad, t.accent_soft);
                    p.stroke_rounded_rect(r, rad, t.accent_ring, 1.0, false);
                }
            }
        } else if hovered {
            let bg = match t.style {
                VisualStyle::BentoSilk if t.is_light => Color::WHITE.with_alpha(0.92),
                VisualStyle::SpatialVision => Color::WHITE.with_alpha(0.20),
                _ => t.card_hover,
            };
            p.fill_rounded_rect(r, rad, bg);
            p.stroke_rounded_rect(
                r,
                rad,
                if t.style == VisualStyle::MonoMatrix || t.style == VisualStyle::CyberHud { t.accent1 } else { t.border_rim },
                1.0,
                t.style == VisualStyle::MonoMatrix,
            );
        } else if matches!(t.style, VisualStyle::LinearPrecision | VisualStyle::MonoMatrix | VisualStyle::CyberHud) {
            p.fill_rounded_rect(r, rad, t.card_bg.with_alpha(0.035));
            p.stroke_rounded_rect(r, rad, t.border_subtle, 1.0, false);
        }

        // Active / Running indicator — 100% consistent bottom/left/right geometry (fixes Cyber HUD v2.5.1 cascade bug!)
        if !active && !running {
            return;
        }
        let dot_color = if active { t.accent1 } else { t.text_secondary.with_alpha(0.75) };

        match (t.style, vertical_edge) {
            (VisualStyle::MonoMatrix, None) if active => {
                // 3-LED horizontal signal matrix!
                let cy = r.bottom() + 2.0;
                for dx in [-5.5f32, 0.0, 5.5] {
                    p.fill_circle(r.center_x() + dx, cy, 1.75, t.accent1);
                }
            }
            (VisualStyle::MonoMatrix, Some(edge)) if active => {
                let cx = if edge == "Left" { r.x - 3.0 } else { r.right() + 3.0 };
                for dy in [-5.5f32, 0.0, 5.5] {
                    p.fill_circle(cx, r.center_y() + dy, 1.75, t.accent1);
                }
            }
            (VisualStyle::BentoSilk | VisualStyle::SpatialVision, None) => {
                let rad_d = if active { 2.6 } else { 1.9 };
                let col = if t.style == VisualStyle::SpatialVision && active { Color::WHITE } else { dot_color };
                if t.style == VisualStyle::SpatialVision && active {
                    p.fill_radial_glow(r.center_x(), r.bottom() + 2.0, 8.0, 5.0, t.accent1.with_alpha(0.6));
                }
                p.fill_circle(r.center_x(), r.bottom() + 2.0, rad_d, col);
            }
            (VisualStyle::LinearPrecision | VisualStyle::CyberHud, None) => {
                let w = if active { 20.0 } else { 8.0 };
                let h = if t.style == VisualStyle::CyberHud && active { 2.5 } else { 2.0 };
                if t.style == VisualStyle::CyberHud && active {
                    p.fill_radial_glow(r.center_x(), r.bottom(), 14.0, 5.0, t.accent_glow);
                }
                p.fill_rounded_rect(RectF::new(r.center_x() - w * 0.5, r.bottom() - 1.0, w, h), 0.5, dot_color);
            }
            (_, None) => {
                // Liquid Glass 26 luminous pill bar
                let w = if active { 15.0 } else { 5.0 };
                if active {
                    p.fill_radial_glow(r.center_x(), r.bottom() + 1.0, 12.0, 5.0, t.accent_glow);
                    p.fill_linear_gradient_rounded(RectF::new(r.center_x() - w * 0.5, r.bottom() - 0.5, w, 3.0), 1.5, t.accent1, t.accent2, false);
                } else {
                    p.fill_circle(r.center_x(), r.bottom() + 1.0, 2.0, dot_color);
                }
            }
            (_, Some(edge)) => {
                let cx = if edge == "Left" { r.x - 3.5 } else { r.right() + 0.5 };
                let h = if active { 16.0 } else { 5.0 };
                p.fill_rounded_rect(RectF::new(cx, r.center_y() - h * 0.5, 3.0, h), 1.5, dot_color);
            }
        }
    }
}
