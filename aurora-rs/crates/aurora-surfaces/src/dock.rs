use aurora_core::{classify_tidy_category, fitts_hover_scale, Color, DockAppGroup};
use aurora_render::{FontFamily, IconKind, Painter, RectF, TextAlign};
use aurora_ui::{draw_micro_button, draw_pill_button, HitTree, ThemePainter, UiAction};

use crate::ShellState;

/// Computes a deterministic, harmonious per-app fallback accent color when no native HICON bitmap is cached yet.
pub fn app_monogram_color(name: &str) -> Color {
    let low = name.to_ascii_lowercase();
    if low.contains("code") || low.contains("visual") {
        Color::from_hex("#007ACC")
    } else if low.contains("whatsapp") || low.contains("spotify") {
        Color::from_hex("#1DB954")
    } else if low.contains("chrome") || low.contains("edge") {
        Color::from_hex("#38BDF8")
    } else if low.contains("discord") || low.contains("teams") {
        Color::from_hex("#5865F2")
    } else if low.contains("terminal") || low.contains("powershell") || low.contains("cmd") {
        Color::from_hex("#334155")
    } else if low.contains("explorer") || low.contains("gezgin") {
        Color::from_hex("#F59E0B")
    } else {
        let hash: u32 = low.bytes().fold(2166136261u32, |acc, b| acc.wrapping_mul(16777619) ^ b as u32);
        const PAL: &[&str] = &["#38BDF8", "#818CF8", "#10B981", "#F59E0B", "#EC4899", "#06B6D4"];
        Color::from_hex(PAL[(hash as usize) % PAL.len()])
    }
}

/// Draws either the real GPU-uploaded native Windows HICON or a crisp 2026 vector monogram badge.
pub fn draw_app_icon(p: &Painter<'_>, icon_key: &str, display_name: &str, r: RectF, radius: f32) {
    if p.draw_cached_icon(icon_key, r, radius) {
        return;
    }
    let bg = app_monogram_color(display_name);
    let bg2 = bg.lerp(Color::BLACK, 0.25);
    p.fill_linear_gradient_rounded(r, radius, bg, bg2, true);
    p.stroke_rounded_rect(r, radius, Color::WHITE.with_alpha(0.28), 1.0, false);

    let low = display_name.to_ascii_lowercase();
    if low.contains("gezgin") || low.contains("explorer") {
        p.draw_icon(IconKind::Folder, r.center_x(), r.center_y(), r.w * 0.52, Color::WHITE);
    } else if low.contains("terminal") || low.contains("powershell") {
        p.draw_text(">_", r, (r.h * 0.42).clamp(11.0, 16.0), 800, Color::WHITE, FontFamily::Mono, TextAlign::Center, true, false);
    } else {
        let initial: String = display_name
            .chars()
            .filter(|c| c.is_alphanumeric())
            .take(2)
            .collect::<String>()
            .to_uppercase();
        let lbl = if initial.is_empty() { "A".into() } else { initial };
        p.draw_text(&lbl, r, (r.h * 0.40).clamp(11.0, 15.0), 800, Color::WHITE, FontFamily::Ui, TextAlign::Center, true, false);
    }
}

/// Renders either the Edge-to-Edge Pinned Fixed Bar (`44/52/60 px` flush!) OR the Floating Crystal Dock
/// (including Zero-Drift Fitts hover wave, Smart Tidy category accordions, Folder Stacks, and Tucked AutoHide strip).
pub fn paint_dock(
    p: &Painter<'_>,
    hits: &mut HitTree,
    state: &ShellState,
    canvas_w: f32,
    canvas_h: f32,
) {
    if state.is_fullscreen_on_monitor {
        return;
    }

    let edge = state.prefs.effective_dock_edge();
    let mode = state.prefs.effective_dock_mode();
    let is_pinned_bar = state.prefs.effective_reserve_dock_work_area();
    let density = state.prefs.effective_dock_density();
    let scale_mul = (state.prefs.dock_scale_percent.clamp(75, 135) as f32) / 100.0;
    let is_vertical = edge == "Left" || edge == "Right";

    // Tucked AutoHide / SmartDodge reveal handle + generous edge hover sensor
    if !is_pinned_bar && state.dock_tucked && mode != "AlwaysVisible" {
        let (handle, sensor) = if edge == "Left" {
            (
                RectF::new(2.0, canvas_h * 0.5 - 72.0, 5.0, 144.0),
                RectF::new(0.0, canvas_h * 0.2, 14.0, canvas_h * 0.6),
            )
        } else if edge == "Right" {
            (
                RectF::new(canvas_w - 7.0, canvas_h * 0.5 - 72.0, 5.0, 144.0),
                RectF::new(canvas_w - 14.0, canvas_h * 0.2, 14.0, canvas_h * 0.6),
            )
        } else {
            (
                RectF::new(canvas_w * 0.5 - 92.0, canvas_h - 7.0, 184.0, 5.0),
                RectF::new(canvas_w * 0.2, canvas_h - 14.0, canvas_w * 0.6, 14.0),
            )
        };
        p.fill_radial_glow(handle.center_x(), handle.center_y(), handle.w * 0.65 + 8.0, handle.h * 0.65 + 6.0, state.theme.accent_glow);
        p.fill_rounded_rect(handle, 2.5, state.theme.accent1.with_alpha(0.92));
        hits.push_shell(sensor);
        return;
    }

    if is_pinned_bar {
        paint_pinned_fixed_bar(p, hits, state, canvas_w, canvas_h, edge, density, scale_mul, is_vertical);
    } else {
        paint_floating_crystal_dock(p, hits, state, canvas_w, canvas_h, edge, density, scale_mul, is_vertical);
    }
}

#[allow(clippy::too_many_arguments)]
fn paint_pinned_fixed_bar(
    p: &Painter<'_>,
    hits: &mut HitTree,
    state: &ShellState,
    canvas_w: f32,
    canvas_h: f32,
    edge: &str,
    density: &str,
    scale_mul: f32,
    is_vertical: bool,
) {
    let t = &state.theme;
    let bar_thickness = if is_vertical {
        let base = match density {
            "Compact" => 50.0,
            "Cozy" => 66.0,
            _ => 58.0,
        };
        (base * scale_mul).clamp(42.0, 92.0)
    } else {
        let base = match density {
            "Compact" => 44.0,
            "Cozy" => 60.0,
            _ => 52.0,
        };
        (base * scale_mul).clamp(38.0, 84.0)
    };

    let bar_rect = match edge {
        "Left" => RectF::new(0.0, 0.0, bar_thickness, canvas_h),
        "Right" => RectF::new(canvas_w - bar_thickness, 0.0, bar_thickness, canvas_h),
        _ => RectF::new(0.0, canvas_h - bar_thickness, canvas_w, bar_thickness),
    };

    ThemePainter::draw_shell(p, bar_rect, t, true);
    hits.push_shell(bar_rect);

    // Subtle crisp inner edge separator line facing the work area
    match edge {
        "Left" => p.draw_line(bar_rect.right() - 0.5, 0.0, bar_rect.right() - 0.5, canvas_h, t.border_rim, 1.0, false),
        "Right" => p.draw_line(bar_rect.x + 0.5, 0.0, bar_rect.x + 0.5, canvas_h, t.border_rim, 1.0, false),
        _ => p.draw_line(0.0, bar_rect.y + 0.5, canvas_w, bar_rect.y + 0.5, t.border_rim, 1.0, false),
    }

    if state.is_dragging_file {
        p.stroke_rounded_rect(bar_rect.inset(2.0, 2.0), 4.0, t.accent1, 2.0, true);
    }

    if is_vertical {
        paint_vertical_dock_items(p, hits, state, bar_rect, edge, bar_thickness);
        return;
    }

    let split_layout = state.prefs.effective_dock_bar_layout() == "Split3Zone";
    let item_sz = (bar_thickness - 12.0).clamp(30.0, 46.0);
    let cy = bar_rect.center_y();
    let hovered = state.hovered_id.as_deref();

    // LEFT CLUSTER: OmniSearch + Control Deck + Smart Tidy + Pin Toggle
    let mut lx = 10.0;
    let search_w = if split_layout { 92.0 } else { 76.0 };
    draw_pill_button(
        p,
        hits,
        "dock:search",
        RectF::new(lx, cy - 15.0, search_w, 30.0),
        "Ara",
        Some(IconKind::Search),
        t,
        false,
        hovered == Some("dock:search"),
        UiAction::ToggleOmni,
    );
    lx += search_w + 6.0;

    draw_micro_button(
        p,
        hits,
        "dock:deck",
        RectF::new(lx, cy - 15.0, 30.0, 30.0),
        IconKind::Sliders,
        t,
        state.crown_expanded,
        false,
        hovered == Some("dock:deck"),
        UiAction::ToggleDeck("control".into()),
        Some("Kontrol Merkezi (Alt+Shift+A)"),
    );
    lx += 36.0;

    if split_layout {
        draw_micro_button(
            p,
            hits,
            "dock:tidy",
            RectF::new(lx, cy - 15.0, 30.0, 30.0),
            IconKind::Sparkles,
            t,
            state.prefs.smart_tidy_enabled,
            false,
            hovered == Some("dock:tidy"),
            UiAction::ToggleSmartTidy,
            Some("Akıllı Derle"),
        );
        lx += 36.0;
    }

    // RIGHT CLUSTER: Folder Stack + Tray + Notifications + Clock/Calendar + Pin + Show Desktop
    let mut rx = canvas_w - 8.0;
    // Show desktop thin edge button (classic Windows convenience!)
    let desk_r = RectF::new(rx - 8.0, bar_rect.y + 4.0, 8.0, bar_rect.h - 8.0);
    p.fill_rounded_rect(desk_r, 3.0, if hovered == Some("dock:desktop") { t.accent_tint } else { t.border_subtle });
    hits.push_with_tip("dock:desktop", desk_r, UiAction::ShowDesktop, "Masaüstünü Göster");
    rx -= 14.0;

    // Clock & Date pill (opens Calendar & Agenda!)
    let clock_str = state.formatted_clock();
    let date_str = state.formatted_date_tr();
    let clock_w = if state.prefs.show_clock_seconds { 94.0 } else { 78.0 };
    let clock_r = RectF::new(rx - clock_w, cy - 16.0, clock_w, 32.0);
    let clk_hov = hovered == Some("dock:clock");
    if clk_hov {
        p.fill_rounded_rect(clock_r, 8.0, t.card_hover);
    }
    p.draw_text(&clock_str, RectF::new(clock_r.x, clock_r.y + 1.0, clock_r.w, 16.0), 11.5, 700, t.text_primary, FontFamily::Mono, TextAlign::Center, true, false);
    p.draw_text(&date_str, RectF::new(clock_r.x, clock_r.y + 16.0, clock_r.w, 14.0), 9.5, 500, t.text_secondary, FontFamily::Ui, TextAlign::Center, true, false);
    hits.push_with_tip("dock:clock", clock_r, UiAction::ToggleCalendar, "Takvim & Ajanda Notları");
    rx -= clock_w + 6.0;

    // Notifications button with live badge
    let notif_r = RectF::new(rx - 30.0, cy - 15.0, 30.0, 30.0);
    draw_micro_button(
        p,
        hits,
        "dock:notif",
        notif_r,
        IconKind::Bell,
        t,
        !state.notifications.is_empty(),
        false,
        hovered == Some("dock:notif"),
        UiAction::ToggleNotif,
        Some("Bildirimler"),
    );
    if !state.notifications.is_empty() {
        let badge = RectF::new(notif_r.right() - 12.0, notif_r.y - 2.0, 14.0, 14.0);
        p.fill_circle(badge.center_x(), badge.center_y(), 7.0, t.accent1);
        let cnt = state.notifications.len().min(9).to_string();
        p.draw_text(&cnt, badge, 8.5, 800, t.accent_ink, FontFamily::Mono, TextAlign::Center, true, false);
    }
    rx -= 36.0;

    // System Tray drawer button
    let tray_r = RectF::new(rx - 30.0, cy - 15.0, 30.0, 30.0);
    draw_micro_button(
        p,
        hits,
        "dock:tray",
        tray_r,
        IconKind::ChevronUp,
        t,
        false,
        false,
        hovered == Some("dock:tray"),
        UiAction::ToggleTray,
        Some("Sistem Tepsisi & Arka Plan Uygulamaları"),
    );
    rx -= 36.0;

    // Keyboard layout pill (TR / EN)
    let kb_r = RectF::new(rx - 32.0, cy - 13.0, 32.0, 26.0);
    p.fill_rounded_rect(kb_r, 7.0, if hovered == Some("dock:kb") { t.card_hover } else { t.card_bg });
    p.stroke_rounded_rect(kb_r, 7.0, t.border_subtle, 1.0, false);
    p.draw_text(&state.keyboard_code, kb_r, 10.0, 700, t.text_primary, FontFamily::Mono, TextAlign::Center, true, false);
    hits.push_with_tip("dock:kb", kb_r, UiAction::CycleKeyboard, "Klavye Düzenini Değiştir");
    rx -= 38.0;

    // Pin / Detach Floating Dock quick toggle
    draw_micro_button(
        p,
        hits,
        "dock:pin-mode",
        RectF::new(rx - 28.0, cy - 14.0, 28.0, 28.0),
        IconKind::Pin,
        t,
        true,
        false,
        hovered == Some("dock:pin-mode"),
        UiAction::ToggleDockPinMode,
        Some("Yüzen Dock / Sabit Bar Geçişi"),
    );
    rx -= 34.0;

    // Folder Stack quick button
    draw_micro_button(
        p,
        hits,
        "dock:folder",
        RectF::new(rx - 28.0, cy - 14.0, 28.0, 28.0),
        IconKind::Folder,
        t,
        false,
        false,
        hovered == Some("dock:folder"),
        UiAction::ToggleFolderStack("Downloads".into()),
        Some("Klasör Yığını (İndirilenler)"),
    );
    rx -= 36.0;

    // CENTER APP STRIP (Zero-Drift fixed pitch icons + Smart Tidy categories)
    let avail_w = (rx - lx - 12.0).max(80.0);
    let gap = 6.0;
    let total_w = compute_app_strip_width(state, item_sz, gap);
    let start_x = if split_layout {
        lx + 8.0
    } else {
        (canvas_w * 0.5 - total_w * 0.5).clamp(lx + 6.0, (rx - total_w - 6.0).max(lx + 6.0))
    };

    paint_horizontal_app_groups(p, hits, state, start_x, cy, item_sz, gap, avail_w, false);
}

#[allow(clippy::too_many_arguments)]
fn paint_floating_crystal_dock(
    p: &Painter<'_>,
    hits: &mut HitTree,
    state: &ShellState,
    canvas_w: f32,
    canvas_h: f32,
    edge: &str,
    density: &str,
    scale_mul: f32,
    is_vertical: bool,
) {
    let t = &state.theme;
    let item_base = match density {
        "Compact" => 36.0,
        "Cozy" => 48.0,
        _ => 42.0,
    } * scale_mul;
    let item_sz = item_base.clamp(32.0, 56.0);
    let gap = 7.0;
    let hovered = state.hovered_id.as_deref();

    if is_vertical {
        let count = state.dock_groups.len().max(1) as f32;
        let dock_w = item_sz + 18.0;
        let dock_h = (count * (item_sz + gap) + 128.0).min(canvas_h - 32.0);
        let x = if edge == "Left" { 10.0 } else { canvas_w - dock_w - 10.0 };
        let y = (canvas_h - dock_h) * 0.5;
        let shell = RectF::new(x, y, dock_w, dock_h);
        ThemePainter::draw_shell(p, shell, t, false);
        hits.push_shell(shell);
        paint_vertical_dock_items(p, hits, state, shell, edge, dock_w);
        return;
    }

    let app_w = compute_app_strip_width(state, item_sz, gap);
    let chrome_w = 80.0 + 12.0 + 36.0 + 36.0 + 74.0 + 34.0; // Search + Divider + Folder + Tray + SmartTidy + Pin
    let dock_w = (app_w + chrome_w + 26.0).min(canvas_w - 32.0);
    let dock_h = item_sz + 18.0;
    let offset_px = (state.prefs.dock_offset_percent as f32 / 100.0) * (canvas_w - dock_w) * 0.45;
    let x = ((canvas_w - dock_w) * 0.5 + offset_px).clamp(14.0, (canvas_w - dock_w - 14.0).max(14.0));
    let y = canvas_h - dock_h - 10.0;
    let shell = RectF::new(x, y, dock_w, dock_h);

    ThemePainter::draw_shell(p, shell, t, false);
    hits.push_shell(shell);
    if state.is_dragging_file {
        p.stroke_rounded_rect(shell.inset(-2.0, -2.0), t.radius_shell + 2.0, t.accent1, 2.0, true);
    }

    let cy = shell.center_y();
    let mut cx = shell.x + 12.0;

    // OmniSearch pill
    draw_pill_button(
        p,
        hits,
        "dock:search",
        RectF::new(cx, cy - 15.0, 74.0, 30.0),
        "Ara",
        Some(IconKind::Search),
        t,
        false,
        hovered == Some("dock:search"),
        UiAction::ToggleOmni,
    );
    cx += 82.0;

    // Vertical hairline divider
    p.draw_line(cx, shell.y + 12.0, cx, shell.bottom() - 12.0, t.border_subtle, 1.0, false);
    cx += 10.0;

    // App Icons with Zero-Drift Fitts Magnification Wave!
    let used_w = paint_horizontal_app_groups(p, hits, state, cx, cy, item_sz, gap, app_w + 4.0, true);
    cx += used_w + 6.0;

    p.draw_line(cx, shell.y + 12.0, cx, shell.bottom() - 12.0, t.border_subtle, 1.0, false);
    cx += 10.0;

    // Folder Stack
    draw_micro_button(
        p,
        hits,
        "dock:folder",
        RectF::new(cx, cy - 15.0, 30.0, 30.0),
        IconKind::Folder,
        t,
        false,
        false,
        hovered == Some("dock:folder"),
        UiAction::ToggleFolderStack("Downloads".into()),
        Some("Klasör Yığını"),
    );
    cx += 36.0;

    // System Tray drawer
    draw_micro_button(
        p,
        hits,
        "dock:tray",
        RectF::new(cx, cy - 15.0, 30.0, 30.0),
        IconKind::ChevronUp,
        t,
        false,
        false,
        hovered == Some("dock:tray"),
        UiAction::ToggleTray,
        Some("Sistem Tepsisi"),
    );
    cx += 36.0;

    // Smart Tidy toggle pill
    draw_pill_button(
        p,
        hits,
        "dock:tidy",
        RectF::new(cx, cy - 15.0, 70.0, 30.0),
        "Derle",
        Some(IconKind::Sparkles),
        t,
        state.prefs.smart_tidy_enabled,
        hovered == Some("dock:tidy"),
        UiAction::ToggleSmartTidy,
    );
    cx += 76.0;

    // Pin to Fixed Bar toggle
    draw_micro_button(
        p,
        hits,
        "dock:pin-mode",
        RectF::new(cx, cy - 15.0, 30.0, 30.0),
        IconKind::Pin,
        t,
        false,
        false,
        hovered == Some("dock:pin-mode"),
        UiAction::ToggleDockPinMode,
        Some("Sabit Çubuğa Dönüştür"),
    );
}

fn compute_app_strip_width(state: &ShellState, item_sz: f32, gap: f32) -> f32 {
    let groups = &state.dock_groups;
    if !state.prefs.smart_tidy_enabled {
        return groups.len() as f32 * (item_sz + gap);
    }
    let (pinned, unpinned): (Vec<&DockAppGroup>, Vec<&DockAppGroup>) =
        groups.iter().partition(|g| g.is_pinned);
    let mut w = pinned.len() as f32 * (item_sz + gap);
    if !unpinned.is_empty() {
        w += 10.0;
        let mut cats: Vec<&'static str> = Vec::new();
        for g in &unpinned {
            let cat = classify_tidy_category(&g.group_key).label_tr();
            if !cats.contains(&cat) {
                cats.push(cat);
            }
        }
        for cat in cats {
            let is_open = state.expanded_accordion.as_deref() == Some(cat);
            let cnt = unpinned
                .iter()
                .filter(|g| classify_tidy_category(&g.group_key).label_tr() == cat)
                .count();
            w += if is_open { 86.0 + cnt as f32 * (item_sz + 4.0) } else { 96.0 } + gap;
        }
    }
    w
}

#[allow(clippy::too_many_arguments)]
fn paint_horizontal_app_groups(
    p: &Painter<'_>,
    hits: &mut HitTree,
    state: &ShellState,
    start_x: f32,
    cy: f32,
    item_sz: f32,
    gap: f32,
    max_w: f32,
    allow_fitts_wave: bool,
) -> f32 {
    let t = &state.theme;
    let hovered = state.hovered_id.as_deref();
    let max_boost = if allow_fitts_wave {
        (state.prefs.dock_magnification_percent as f32 / 100.0) * 0.42
    } else {
        0.0
    };
    let cursor = state.dock_hover_cursor;

    let mut x = start_x;
    let limit_x = start_x + max_w;

    let (pinned, unpinned): (Vec<&DockAppGroup>, Vec<&DockAppGroup>) = if state.prefs.smart_tidy_enabled {
        state.dock_groups.iter().partition(|g| g.is_pinned)
    } else {
        (state.dock_groups.iter().collect(), Vec::new())
    };

    for g in pinned {
        if x + item_sz > limit_x {
            break;
        }
        draw_single_dock_icon(p, hits, g, x, cy, item_sz, cursor, max_boost, hovered, t, None);
        x += item_sz + gap;
    }

    if !unpinned.is_empty() && x + 60.0 < limit_x {
        p.draw_line(x + 2.0, cy - 12.0, x + 2.0, cy + 12.0, t.border_subtle, 1.0, false);
        x += 10.0;

        let mut cats: Vec<&'static str> = Vec::new();
        for g in &unpinned {
            let cat = classify_tidy_category(&g.group_key).label_tr();
            if !cats.contains(&cat) {
                cats.push(cat);
            }
        }
        for cat in cats {
            let items: Vec<&DockAppGroup> = unpinned
                .iter()
                .copied()
                .filter(|g| classify_tidy_category(&g.group_key).label_tr() == cat)
                .collect();
            let is_open = state.expanded_accordion.as_deref() == Some(cat);
            let pill_w = if is_open { 86.0 + items.len() as f32 * (item_sz + 4.0) } else { 96.0 };
            if x + pill_w > limit_x {
                break;
            }
            let pr = RectF::new(x, cy - item_sz * 0.44, pill_w, item_sz * 0.88);
            let hid = format!("acc:{cat}");
            let is_hov = hovered == Some(hid.as_str());
            let has_attn = items.iter().any(|i| i.needs_attention);
            p.fill_rounded_rect(pr, t.radius_item, if has_attn { t.warning.with_alpha(0.16) } else if is_hov { t.card_hover } else { t.card_bg });
            p.stroke_rounded_rect(pr, t.radius_item, if has_attn { t.warning } else if is_open { t.accent_ring } else { t.border_subtle }, 1.0, false);

            // Mini stacked icon + Category name + Count pill
            if let Some(first) = items.first() {
                draw_app_icon(p, &first.group_key, &first.display_name, RectF::new(pr.x + 6.0, pr.center_y() - 10.0, 20.0, 20.0), 5.0);
            }
            p.draw_text(cat, RectF::new(pr.x + 30.0, pr.y, 48.0, pr.h), 10.2, 600, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
            let cnt_r = RectF::new(pr.x + 74.0, pr.center_y() - 8.0, 16.0, 16.0);
            p.fill_circle(cnt_r.center_x(), cnt_r.center_y(), 8.0, t.accent_tint);
            p.draw_text(&items.len().to_string(), cnt_r, 9.0, 700, t.accent1, FontFamily::Mono, TextAlign::Center, true, false);
            hits.push(hid, RectF::new(pr.x, pr.y, 92.0, pr.h), UiAction::ToggleAccordion(cat.to_string()));

            if is_open {
                let mut ix = pr.x + 92.0;
                for item in items {
                    draw_single_dock_icon(p, hits, item, ix, cy, item_sz - 6.0, cursor, 0.0, hovered, t, None);
                    ix += item_sz;
                }
            }
            x += pill_w + gap;
        }
    }

    x - start_x
}

#[allow(clippy::too_many_arguments)]
fn draw_single_dock_icon(
    p: &Painter<'_>,
    hits: &mut HitTree,
    g: &DockAppGroup,
    x: f32,
    cy: f32,
    base_sz: f32,
    cursor: Option<(f32, f32)>,
    max_boost: f32,
    hovered: Option<&str>,
    t: &aurora_core::ResolvedTheme,
    vertical_edge: Option<&str>,
) {
    let slot_r = RectF::new(x, cy - base_sz * 0.5, base_sz, base_sz);
    let hid = format!("app:{}", g.group_key);
    let is_hov = hovered == Some(hid.as_str());

    // Zero-Drift Fitts Magnification: expands visually upward while preserving exact hit slot pitch!
    let scale = if let Some((mx, my)) = cursor {
        if (my - slot_r.center_y()).abs() <= base_sz * 1.6 {
            fitts_hover_scale(mx, slot_r.center_x(), base_sz * 2.5, max_boost, false)
        } else {
            1.0
        }
    } else if is_hov && max_boost > 0.01 {
        1.0 + max_boost * 0.35
    } else {
        1.0
    };

    let vis_sz = base_sz * scale;
    let lift_y = (vis_sz - base_sz) * 0.62;
    let vis_r = RectF::new(
        slot_r.center_x() - vis_sz * 0.5,
        slot_r.center_y() - vis_sz * 0.5 - lift_y,
        vis_sz,
        vis_sz,
    );

    let running = !g.windows.is_empty();
    ThemePainter::draw_dock_item_chrome(p, vis_r, t, is_hov, g.has_active, running, g.needs_attention, vertical_edge);

    let ic_pad = (vis_sz * 0.16).clamp(4.0, 9.0);
    draw_app_icon(p, &g.group_key, &g.display_name, vis_r.inset(ic_pad, ic_pad), (t.radius_item - 3.0).max(4.0));

    // Multi-window count pill badge
    if g.windows.len() > 1 {
        let br = RectF::new(vis_r.right() - 13.0, vis_r.y - 2.0, 15.0, 15.0);
        p.fill_circle(br.center_x(), br.center_y(), 7.5, t.accent1);
        p.draw_text(&g.windows.len().min(9).to_string(), br, 8.5, 800, t.accent_ink, FontFamily::Mono, TextAlign::Center, true, false);
    }

    // Download / copy progress bar overlay
    if let Some(pct) = g.progress_percent {
        let bar = RectF::new(vis_r.x + 4.0, vis_r.bottom() - 5.0, (vis_r.w - 8.0) * (pct as f32 / 100.0), 3.0);
        p.fill_rounded_rect(bar, 1.5, t.success);
    }

    hits.push_with_tip(hid, slot_r, UiAction::ClickDockGroup(g.group_key.clone()), g.display_name.clone());
}

fn paint_vertical_dock_items(
    p: &Painter<'_>,
    hits: &mut HitTree,
    state: &ShellState,
    bar: RectF,
    edge: &str,
    thickness: f32,
) {
    let t = &state.theme;
    let hovered = state.hovered_id.as_deref();
    let item_sz = (thickness - 12.0).clamp(32.0, 48.0);
    let x = bar.center_x() - item_sz * 0.5;
    let mut y = bar.y + 10.0;

    draw_micro_button(
        p,
        hits,
        "dock:search",
        RectF::new(bar.center_x() - 16.0, y, 32.0, 32.0),
        IconKind::Search,
        t,
        false,
        false,
        hovered == Some("dock:search"),
        UiAction::ToggleOmni,
        Some("OmniSearch (Alt+Space)"),
    );
    y += 40.0;

    let max_y = bar.bottom() - 88.0;
    for g in &state.dock_groups {
        if y + item_sz > max_y {
            break;
        }
        draw_single_dock_icon(p, hits, g, x, y + item_sz * 0.5, item_sz, None, 0.0, hovered, t, Some(edge));
        y += item_sz + 8.0;
    }

    // Bottom utility buttons on vertical dock
    let by = bar.bottom() - 78.0;
    draw_micro_button(
        p,
        hits,
        "dock:tray",
        RectF::new(bar.center_x() - 15.0, by, 30.0, 30.0),
        IconKind::ChevronUp,
        t,
        false,
        false,
        hovered == Some("dock:tray"),
        UiAction::ToggleTray,
        Some("Sistem Tepsisi"),
    );
    draw_micro_button(
        p,
        hits,
        "dock:pin-mode",
        RectF::new(bar.center_x() - 15.0, by + 36.0, 30.0, 30.0),
        IconKind::Pin,
        t,
        state.prefs.effective_reserve_dock_work_area(),
        false,
        hovered == Some("dock:pin-mode"),
        UiAction::ToggleDockPinMode,
        Some("Sabit Bar / Yüzen Dock"),
    );
}
