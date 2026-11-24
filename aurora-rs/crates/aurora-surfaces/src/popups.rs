use aurora_core::{Color, PhysicalRect};
use aurora_render::{FontFamily, IconKind, Painter, RectF, TextAlign};
use aurora_ui::{
    draw_micro_button, draw_pill_button, draw_text_input, radial_orb_positions, HitTree,
    ThemePainter, UiAction,
};
use aurora_win32::compute_snap_zones;
use chrono::{Datelike, NaiveDate};

use crate::{draw_app_icon, OverlayPopup, ShellState, SnipMode};

/// Paints active floating popups, clickable toast banner, OmniSearch, Radial Window Orb Hub,
/// Calendar & Agenda, Notification Center, System Tray Drawer, 6-Step Tour, and Screen Overlays.
pub fn paint_popups_and_overlays(
    p: &Painter<'_>,
    hits: &mut HitTree,
    state: &ShellState,
    canvas_w: f32,
    canvas_h: f32,
) {
    // 1. Live Clickable Floating Toast Banner (top-right, above everything except full-screen snip)
    if let Some(toast) = &state.active_toast {
        if !matches!(state.open_popup, OverlayPopup::RegionSnip(_)) {
            paint_floating_toast(p, hits, state, &toast.notification, canvas_w);
        }
    }

    // 2. Active Modal / Overlay Surface
    match &state.open_popup {
        OverlayPopup::None => {}
        OverlayPopup::OmniSearch => paint_omnisearch(p, hits, state, canvas_w, canvas_h),
        OverlayPopup::Calendar => paint_calendar_popup(p, hits, state, canvas_w, canvas_h),
        OverlayPopup::Notifications => paint_notifications_popup(p, hits, state, canvas_w, canvas_h),
        OverlayPopup::Tray => paint_tray_drawer(p, hits, state, canvas_w, canvas_h),
        OverlayPopup::RadialHub(group_key) => paint_radial_hub(p, hits, state, group_key, canvas_w, canvas_h),
        OverlayPopup::FolderStack(name) => paint_folder_stack(p, hits, state, name, canvas_w, canvas_h),
        OverlayPopup::Tour(step) => paint_tour_modal(p, hits, state, *step, canvas_w, canvas_h),
        OverlayPopup::SnapOverlay => paint_snap_zones_overlay(p, state, canvas_w, canvas_h),
        OverlayPopup::RegionSnip(mode) => paint_region_snip_overlay(p, state, mode, canvas_w, canvas_h),
    }

    // 3. Subtle transient status banner (e.g., "Panoya kopyalandı", "ZIP oluşturuldu")
    if let Some(msg) = &state.status_banner {
        let bw = (p.measure_text_width(msg, 11.5, 600, FontFamily::Ui) + 36.0).clamp(160.0, 440.0);
        let by = if canvas_h < 180.0 { 48.0 } else { canvas_h - 94.0 };
        let br = RectF::new((canvas_w - bw) * 0.5, by, bw, 32.0);
        ThemePainter::draw_shell(p, br, &state.theme, false);
        p.draw_text(msg, br, 11.2, 600, state.theme.text_primary, FontFamily::Ui, TextAlign::Center, true, false);
    }
}

fn paint_floating_toast(
    p: &Painter<'_>,
    hits: &mut HitTree,
    state: &ShellState,
    n: &aurora_core::SystemToastNotification,
    canvas_w: f32,
) {
    let t = &state.theme;
    let hovered = state.hovered_id.as_deref();
    let w = 368.0;
    let h = 88.0;
    let r = RectF::new(canvas_w - w - 18.0, 48.0, w, h);
    ThemePainter::draw_shell(p, r, t, false);
    hits.push_shell(r);
    hits.push("toast:body", RectF::new(r.x, r.y, r.w - 88.0, r.h), UiAction::ActivateNotification(n.notification_id.to_string()));

    // Accent indicator stripe
    p.fill_rounded_rect(RectF::new(r.x + 8.0, r.y + 12.0, 4.0, r.h - 24.0), 2.0, t.accent1);

    p.draw_text(&format!("{}  •  {}", n.app_name, n.arrived_at_utc), RectF::new(r.x + 20.0, r.y + 10.0, r.w - 110.0, 15.0), 9.5, 700, t.accent1, FontFamily::Ui, TextAlign::Left, true, false);
    p.draw_text(&n.title, RectF::new(r.x + 20.0, r.y + 27.0, r.w - 110.0, 18.0), 12.0, 700, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
    p.draw_text(&n.body, RectF::new(r.x + 20.0, r.y + 47.0, r.w - 110.0, 32.0), 10.5, 500, t.text_secondary, FontFamily::Ui, TextAlign::Left, false, true);

    draw_pill_button(
        p, hits, "toast:open", RectF::new(r.right() - 82.0, r.y + 14.0, 70.0, 26.0),
        "Aç ↗", None, t, true, hovered == Some("toast:open"),
        UiAction::ActivateNotification(n.notification_id.to_string()),
    );
    draw_pill_button(
        p, hits, "toast:close", RectF::new(r.right() - 82.0, r.y + 46.0, 70.0, 24.0),
        "Kapat", None, t, false, hovered == Some("toast:close"),
        UiAction::DismissNotification(n.notification_id.to_string()),
    );
}

fn paint_omnisearch(
    p: &Painter<'_>,
    hits: &mut HitTree,
    state: &ShellState,
    canvas_w: f32,
    canvas_h: f32,
) {
    let t = &state.theme;
    let hovered = state.hovered_id.as_deref();
    let w = 640.0f32.min(canvas_w - 40.0);
    let h = 448.0f32.min(canvas_h - 120.0);
    let r = RectF::new((canvas_w - w) * 0.5, (canvas_h * 0.16).max(58.0), w, h);

    ThemePainter::draw_shell(p, r, t, false);
    hits.push_shell(r);

    // Search Input Row
    let in_r = RectF::new(r.x + 16.0, r.y + 16.0, r.w - 66.0, 42.0);
    draw_text_input(p, hits, "input:omni", in_r, &state.omni_input, t, Some(IconKind::Search));
    draw_micro_button(p, hits, "omni:close", RectF::new(r.right() - 44.0, r.y + 22.0, 30.0, 30.0), IconKind::Close, t, false, true, hovered == Some("omni:close"), UiAction::CloseOverlays, Some("Kapat (Esc)"));

    // Filter Chips Row
    let mut fx = r.x + 16.0;
    let fy = in_r.bottom() + 10.0;
    for (fid, flbl) in [("all", "Tümü"), ("app", "Uygulamalar"), ("file", "Dosyalar"), ("setting", "Ayarlar & Araçlar")] {
        let fw = (p.measure_text_width(flbl, 10.2, 600, FontFamily::Ui) + 22.0).max(58.0);
        let fr = RectF::new(fx, fy, fw, 24.0);
        let hid = format!("omni:flt:{fid}");
        draw_pill_button(p, hits, &hid, fr, flbl, None, t, state.omni_filter == fid, hovered == Some(hid.as_str()), UiAction::OmniFilter(fid.into()));
        fx += fw + 6.0;
    }

    // Results List
    let mut ry = fy + 34.0;
    let max_y = r.bottom() - 34.0;
    if state.omni_results.is_empty() {
        p.draw_text("Eşleşen uygulama, dosya veya hesaplama bulunamadı.", RectF::new(r.x + 16.0, ry + 30.0, r.w - 32.0, 24.0), 12.0, 500, t.text_secondary, FontFamily::Ui, TextAlign::Center, true, false);
    } else {
        for (idx, res) in state.omni_results.iter().enumerate() {
            if ry + 42.0 > max_y {
                break;
            }
            let row = RectF::new(r.x + 16.0, ry, r.w - 32.0, 40.0);
            let hid = format!("omni:res:{idx}");
            let sel = idx == state.omni_selected || hovered == Some(hid.as_str());
            p.fill_rounded_rect(row, 10.0, if sel { t.accent_soft } else { t.card_bg });
            p.stroke_rounded_rect(row, 10.0, if sel { t.accent_ring } else { t.border_subtle }, 1.0, false);

            let badge_r = RectF::new(row.x + 10.0, row.center_y() - 9.0, 66.0, 18.0);
            p.fill_rounded_rect(badge_r, 6.0, if res.is_copy_value { t.success.with_alpha(0.22) } else { t.accent_tint });
            p.draw_text(&res.badge, badge_r, 8.5, 700, if res.is_copy_value { t.success } else { t.accent1 }, FontFamily::Mono, TextAlign::Center, true, false);

            p.draw_text(&res.title, RectF::new(badge_r.right() + 10.0, row.y + 4.0, row.w - 110.0, 18.0), 12.0, 700, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
            p.draw_text(&res.subtitle, RectF::new(badge_r.right() + 10.0, row.y + 22.0, row.w - 110.0, 14.0), 9.6, 500, t.text_secondary, FontFamily::Ui, TextAlign::Left, true, false);

            hits.push(hid, row, UiAction::OmniExecute(idx));
            ry += 45.0;
        }
    }

    // Keyboard shortcuts footer
    p.draw_line(r.x + 16.0, r.bottom() - 28.0, r.right() - 16.0, r.bottom() - 28.0, t.border_subtle, 1.0, false);
    p.draw_text("↑/↓ Seç   •   Enter Çalıştır / Kopyala   •   =24*15 Hesapla   •   120 usd Çevir   •   Esc Kapat", RectF::new(r.x + 16.0, r.bottom() - 24.0, r.w - 32.0, 18.0), 9.5, 500, t.text_secondary, FontFamily::Mono, TextAlign::Center, true, false);
}

fn paint_radial_hub(
    p: &Painter<'_>,
    hits: &mut HitTree,
    state: &ShellState,
    group_key: &str,
    canvas_w: f32,
    canvas_h: f32,
) {
    let t = &state.theme;
    let hovered = state.hovered_id.as_deref();
    let Some(group) = state.dock_groups.iter().find(|g| g.group_key == group_key) else {
        return;
    };

    let hub_w = 460.0;
    let hub_h = 310.0;
    let r = RectF::new((canvas_w - hub_w) * 0.5, canvas_h - hub_h - 86.0, hub_w, hub_h);
    ThemePainter::draw_shell(p, r, t, false);
    hits.push_shell(r);

    // Header
    draw_app_icon(p, &group.group_key, &group.display_name, RectF::new(r.x + 16.0, r.y + 12.0, 26.0, 26.0), 7.0);
    p.draw_text(&format!("{} — Açık Pencereler ({})", group.display_name, group.windows.len()), RectF::new(r.x + 50.0, r.y + 12.0, r.w - 96.0, 26.0), 12.5, 700, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
    draw_micro_button(p, hits, "rad:close", RectF::new(r.right() - 38.0, r.y + 11.0, 26.0, 26.0), IconKind::Close, t, false, true, hovered == Some("rad:close"), UiAction::CloseOverlays, Some("Kapat"));

    // Orbital Window Orbs + Direct Window Action Ring (Minimize / SnapLeft / SnapRight / Maximize / Close)
    let cx = r.center_x();
    let cy = r.y + 168.0;
    let count = group.windows.len().min(6);
    if count > 1 {
        p.stroke_circle(cx, cy, 88.0, t.border_subtle, 1.0, true);
    }
    let positions = if count == 2 {
        vec![(cx - 106.0, cy), (cx + 106.0, cy)]
    } else {
        radial_orb_positions(cx, cy, count, 88.0)
    };
    let card_w = if count <= 2 { 196.0 } else { 168.0 };

    for (i, win) in group.windows.iter().take(6).enumerate() {
        let (ox, oy) = positions[i];
        let card = RectF::new(ox - card_w * 0.5, oy - 38.0, card_w, 76.0);
        let hid = format!("rad:win:{}", win.hwnd);
        let is_hov = hovered == Some(hid.as_str());
        p.fill_rounded_rect(card, 12.0, if win.is_active { t.accent_soft } else if is_hov { t.card_hover } else { t.inset_bg });
        p.stroke_rounded_rect(card, 12.0, if win.is_active { t.accent1 } else { t.border_rim }, 1.2, false);

        p.draw_text(&win.title, RectF::new(card.x + 10.0, card.y + 6.0, card.w - 20.0, 30.0), 10.5, 600, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
        hits.push(hid, RectF::new(card.x, card.y, card.w, 38.0), UiAction::ClickRadialWindow(win.hwnd));

        // 5 Direct Window Actions inside the orb card
        let acts = [
            (IconKind::Minimize, "minimize", "Küçült", false),
            (IconKind::SnapLeft, "snap_left", "Sola Yasla", false),
            (IconKind::SnapRight, "snap_right", "Sağa Yasla", false),
            (IconKind::Maximize, "maximize", "Büyüt", false),
            (IconKind::Close, "close", "Kapat", true),
        ];
        for (ai, (ic, cmd, tip, danger)) in acts.into_iter().enumerate() {
            let aid = format!("rad:act:{}:{cmd}", win.hwnd);
            let ar = RectF::new(card.x + 10.0 + ai as f32 * 30.0, card.bottom() - 28.0, 24.0, 22.0);
            draw_micro_button(
                p, hits, &aid, ar, ic, t, false, danger, hovered == Some(aid.as_str()),
                UiAction::RadialAction { hwnd: win.hwnd, cmd: cmd.into() }, Some(tip),
            );
        }
    }
}

fn paint_folder_stack(
    p: &Painter<'_>,
    hits: &mut HitTree,
    state: &ShellState,
    name: &str,
    canvas_w: f32,
    canvas_h: f32,
) {
    let t = &state.theme;
    let hovered = state.hovered_id.as_deref();
    let w = 340.0;
    let h = 348.0;
    let r = RectF::new(canvas_w - w - 36.0, canvas_h - h - 72.0, w, h);
    ThemePainter::draw_shell(p, r, t, false);
    hits.push_shell(r);

    p.draw_icon(IconKind::Folder, r.x + 22.0, r.y + 24.0, 15.0, t.accent1);
    p.draw_text(&format!("Klasör Yığını — {name}"), RectF::new(r.x + 36.0, r.y + 12.0, r.w - 80.0, 24.0), 12.0, 700, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
    draw_micro_button(p, hits, "fld:close", RectF::new(r.right() - 36.0, r.y + 11.0, 24.0, 24.0), IconKind::Close, t, false, true, hovered == Some("fld:close"), UiAction::CloseOverlays, Some("Kapat"));

    // Quick folder switch tabs
    let mut fx = r.x + 12.0;
    for (key, lbl) in [("Desktop", "Masaüstü"), ("Downloads", "İndirilenler"), ("Documents", "Belgeler")] {
        let fr = RectF::new(fx, r.y + 42.0, 98.0, 24.0);
        let hid = format!("fld:tab:{key}");
        draw_pill_button(p, hits, &hid, fr, lbl, None, t, name == key, hovered == Some(hid.as_str()), UiAction::ToggleFolderStack(key.into()));
        fx += 104.0;
    }

    let mut y = r.y + 76.0;
    if state.folder_stack_entries.is_empty() {
        p.draw_text("Bu klasörde gösterilecek öğe yok.", RectF::new(r.x + 14.0, y + 40.0, r.w - 28.0, 24.0), 11.0, 500, t.text_secondary, FontFamily::Ui, TextAlign::Center, true, false);
    } else {
        for (i, item) in state.folder_stack_entries.iter().take(7).enumerate() {
            let row = RectF::new(r.x + 12.0, y, r.w - 24.0, 34.0);
            let hid = format!("fld:it:{i}");
            let is_hov = hovered == Some(hid.as_str());
            p.fill_rounded_rect(row, 8.0, if is_hov { t.card_hover } else { t.inset_bg });
            p.draw_icon(if item.is_dir { IconKind::Folder } else { IconKind::Note }, row.x + 14.0, row.center_y(), 13.0, if item.is_dir { t.warning } else { t.accent1 });
            p.draw_text(&item.name, RectF::new(row.x + 28.0, row.y, row.w - 94.0, row.h), 10.8, 600, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
            p.draw_text(&item.size_label, RectF::new(row.right() - 62.0, row.y, 54.0, row.h), 9.2, 500, t.text_secondary, FontFamily::Mono, TextAlign::Right, true, false);
            hits.push(hid, row, UiAction::OpenFolderEntry(item.path.clone()));
            y += 38.0;
        }
    }
}

fn paint_calendar_popup(
    p: &Painter<'_>,
    hits: &mut HitTree,
    state: &ShellState,
    canvas_w: f32,
    canvas_h: f32,
) {
    let t = &state.theme;
    let hovered = state.hovered_id.as_deref();
    let w = 344.0;
    let h = 408.0;
    let r = RectF::new(canvas_w - w - 16.0, canvas_h - h - 68.0, w, h);
    ThemePainter::draw_shell(p, r, t, false);
    hits.push_shell(r);

    let months = ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];
    let m_name = months[(state.calendar_month.saturating_sub(1) as usize).min(11)];
    p.draw_text(&format!("{m_name} {}", state.calendar_year), RectF::new(r.x + 16.0, r.y + 14.0, 180.0, 24.0), 14.0, 700, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);

    draw_micro_button(p, hits, "cal:prev", RectF::new(r.right() - 96.0, r.y + 13.0, 24.0, 24.0), IconKind::ChevronLeft, t, false, false, hovered == Some("cal:prev"), UiAction::CalendarPrevMonth, Some("Önceki Ay"));
    draw_micro_button(p, hits, "cal:next", RectF::new(r.right() - 66.0, r.y + 13.0, 24.0, 24.0), IconKind::ChevronRight, t, false, false, hovered == Some("cal:next"), UiAction::CalendarNextMonth, Some("Sonraki Ay"));
    draw_micro_button(p, hits, "cal:close", RectF::new(r.right() - 36.0, r.y + 13.0, 24.0, 24.0), IconKind::Close, t, false, true, hovered == Some("cal:close"), UiAction::CloseOverlays, Some("Kapat"));

    // Day-of-week header
    let cw = (r.w - 32.0) / 7.0;
    for (i, d) in ["Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz"].into_iter().enumerate() {
        p.draw_text(d, RectF::new(r.x + 16.0 + i as f32 * cw, r.y + 46.0, cw, 18.0), 9.5, 700, t.text_secondary, FontFamily::Ui, TextAlign::Center, true, false);
    }

    // Calendar day cells
    let first = NaiveDate::from_ymd_opt(state.calendar_year, state.calendar_month, 1).unwrap_or_else(|| NaiveDate::from_ymd_opt(2026, 1, 1).unwrap());
    let start_col = first.weekday().num_days_from_monday() as usize;
    let next_m = if state.calendar_month == 12 {
        NaiveDate::from_ymd_opt(state.calendar_year + 1, 1, 1)
    } else {
        NaiveDate::from_ymd_opt(state.calendar_year, state.calendar_month + 1, 1)
    }
    .unwrap_or(first);
    let days_in_month = (next_m - first).num_days().clamp(28, 31) as u32;
    let today_iso = format!("{:04}-{:02}-{:02}", state.now_local.year(), state.now_local.month(), state.now_local.day());

    for d in 1..=days_in_month {
        let cell = start_col + (d as usize - 1);
        let col = cell % 7;
        let row = cell / 7;
        let dr = RectF::new(r.x + 16.0 + col as f32 * cw + 2.0, r.y + 68.0 + row as f32 * 34.0, cw - 4.0, 30.0);
        let iso = format!("{:04}-{:02}-{:02}", state.calendar_year, state.calendar_month, d);
        let is_today = iso == today_iso;
        let is_sel = iso == state.selected_day_iso;
        let has_note = state.agenda_notes.get(&iso).is_some_and(|s| !s.trim().is_empty());
        let hid = format!("cal:day:{iso}");

        if is_today {
            p.fill_rounded_rect(dr, 8.0, t.accent1);
        } else if is_sel {
            p.fill_rounded_rect(dr, 8.0, t.accent_soft);
            p.stroke_rounded_rect(dr, 8.0, t.accent1, 1.2, false);
        } else if hovered == Some(hid.as_str()) {
            p.fill_rounded_rect(dr, 8.0, t.card_hover);
        }
        p.draw_text(&d.to_string(), dr, 11.0, if is_today || is_sel { 700 } else { 500 }, if is_today { t.accent_ink } else { t.text_primary }, FontFamily::Mono, TextAlign::Center, true, false);
        if has_note {
            p.fill_circle(dr.center_x(), dr.bottom() - 4.0, 2.0, if is_today { t.accent_ink } else { t.accent1 });
        }
        hits.push(hid, dr, UiAction::CalendarSelectDay(iso));
    }

    // Selected Day Agenda Note Box
    let note_y = r.bottom() - 108.0;
    p.draw_line(r.x + 16.0, note_y - 6.0, r.right() - 16.0, note_y - 6.0, t.border_subtle, 1.0, false);
    p.draw_text(&format!("AJANDA NOTU — {}", state.selected_day_iso), RectF::new(r.x + 16.0, note_y, r.w - 32.0, 16.0), 9.5, 700, t.accent1, FontFamily::Mono, TextAlign::Left, true, false);
    draw_text_input(p, hits, "input:agenda", RectF::new(r.x + 16.0, note_y + 22.0, r.w - 32.0, 72.0), &state.agenda_input, t, None);
}

fn paint_notifications_popup(
    p: &Painter<'_>,
    hits: &mut HitTree,
    state: &ShellState,
    canvas_w: f32,
    canvas_h: f32,
) {
    let t = &state.theme;
    let hovered = state.hovered_id.as_deref();
    let w = 368.0;
    let h = 420.0;
    let r = RectF::new(canvas_w - w - 16.0, canvas_h - h - 68.0, w, h);
    ThemePainter::draw_shell(p, r, t, false);
    hits.push_shell(r);

    p.draw_icon(IconKind::Bell, r.x + 22.0, r.y + 24.0, 15.0, t.accent1);
    p.draw_text("Bildirim Merkezi", RectF::new(r.x + 36.0, r.y + 12.0, 160.0, 24.0), 13.0, 700, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
    draw_pill_button(p, hits, "np:clr", RectF::new(r.right() - 114.0, r.y + 12.0, 70.0, 24.0), "Temizle", None, t, false, hovered == Some("np:clr"), UiAction::ClearAllNotifications);
    draw_micro_button(p, hits, "np:close", RectF::new(r.right() - 36.0, r.y + 12.0, 24.0, 24.0), IconKind::Close, t, false, true, hovered == Some("np:close"), UiAction::CloseOverlays, Some("Kapat"));

    let mut ny = r.y + 48.0;
    if state.notifications.is_empty() {
        p.draw_text("Tüm bildirimler okundu ✓", RectF::new(r.x + 16.0, r.center_y() - 12.0, r.w - 32.0, 24.0), 12.0, 600, t.text_secondary, FontFamily::Ui, TextAlign::Center, true, false);
    } else {
        for n in state.notifications.iter().take(5) {
            let nr = RectF::new(r.x + 14.0, ny, r.w - 28.0, 66.0);
            let nid = format!("np:act:{}", n.notification_id);
            let is_hov = hovered == Some(nid.as_str());
            p.fill_rounded_rect(nr, 10.0, if is_hov { t.card_hover } else { t.inset_bg });
            p.stroke_rounded_rect(nr, 10.0, if is_hov { t.accent_ring } else { t.border_subtle }, 1.0, false);

            p.draw_text(&format!("{}  •  {}", n.app_name, n.arrived_at_utc), RectF::new(nr.x + 10.0, nr.y + 6.0, nr.w - 72.0, 14.0), 9.2, 700, t.accent1, FontFamily::Ui, TextAlign::Left, true, false);
            p.draw_text(&n.title, RectF::new(nr.x + 10.0, nr.y + 22.0, nr.w - 72.0, 16.0), 11.2, 700, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
            p.draw_text(&n.body, RectF::new(nr.x + 10.0, nr.y + 40.0, nr.w - 72.0, 18.0), 10.0, 500, t.text_secondary, FontFamily::Ui, TextAlign::Left, true, false);
            hits.push_with_tip(nid, RectF::new(nr.x, nr.y, nr.w - 36.0, nr.h), UiAction::ActivateNotification(n.notification_id.to_string()), "Uygulamada Aç ↗");

            let did = format!("np:dis:{}", n.notification_id);
            draw_micro_button(p, hits, &did, RectF::new(nr.right() - 30.0, nr.y + 20.0, 24.0, 24.0), IconKind::Close, t, false, true, hovered == Some(did.as_str()), UiAction::DismissNotification(n.notification_id.to_string()), Some("Kapat"));
            ny += 72.0;
        }
    }
}

fn paint_tray_drawer(
    p: &Painter<'_>,
    hits: &mut HitTree,
    state: &ShellState,
    canvas_w: f32,
    canvas_h: f32,
) {
    let t = &state.theme;
    let hovered = state.hovered_id.as_deref();
    let w = 312.0;
    let h = 250.0;
    let r = RectF::new(canvas_w - w - 36.0, canvas_h - h - 70.0, w, h);
    ThemePainter::draw_shell(p, r, t, false);
    hits.push_shell(r);

    p.draw_text("Arka Plan Uygulamaları", RectF::new(r.x + 14.0, r.y + 12.0, 210.0, 24.0), 12.0, 700, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
    draw_micro_button(p, hits, "tray:close", RectF::new(r.right() - 36.0, r.y + 11.0, 24.0, 24.0), IconKind::Close, t, false, true, hovered == Some("tray:close"), UiAction::CloseOverlays, Some("Kapat"));

    let mut y = r.y + 44.0;
    for (i, app) in state.tray_apps.iter().take(4).enumerate() {
        let row = RectF::new(r.x + 12.0, y, r.w - 24.0, 34.0);
        let hid = format!("tray:app:{i}");
        p.fill_rounded_rect(row, 8.0, if hovered == Some(hid.as_str()) { t.card_hover } else { t.inset_bg });
        draw_app_icon(p, &app.id, &app.name, RectF::new(row.x + 8.0, row.center_y() - 10.0, 20.0, 20.0), 5.0);
        p.draw_text(&app.name, RectF::new(row.x + 36.0, row.y, row.w - 46.0, row.h), 11.0, 600, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
        hits.push(hid, row, UiAction::LaunchPath(app.executable_path.clone()));
        y += 38.0;
    }

    draw_pill_button(
        p, hits, "tray:peek", RectF::new(r.x + 12.0, r.bottom() - 40.0, r.w - 24.0, 28.0),
        "Klasik Tepsiye Bak (10 sn)", Some(IconKind::ExtLink), t, true, hovered == Some("tray:peek"),
        UiAction::PeekNativeTray,
    );
}

/// Complete 6-Step Interactive Feature Tour Modal (v3.0.0 Native GPU Edition!)
fn paint_tour_modal(
    p: &Painter<'_>,
    hits: &mut HitTree,
    state: &ShellState,
    step: usize,
    canvas_w: f32,
    canvas_h: f32,
) {
    let t = &state.theme;
    let hovered = state.hovered_id.as_deref();
    let step = step.min(5);
    let w = 740.0f32.min(canvas_w - 40.0);
    let h = 472.0;
    let r = RectF::new((canvas_w - w) * 0.5, (canvas_h - h) * 0.5, w, h);

    ThemePainter::draw_shell(p, r, t, false);
    hits.push_shell(r);

    // Header
    p.fill_circle(r.x + 24.0, r.y + 28.0, 8.0, t.accent1);
    p.fill_radial_glow(r.x + 24.0, r.y + 28.0, 16.0, 16.0, t.accent_glow);
    p.draw_text("Project Aurora v3.0 — Kapsamlı Özellik Turu", RectF::new(r.x + 40.0, r.y + 12.0, 440.0, 22.0), 14.0, 800, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
    p.draw_text("Saf Rust + Direct2D/DirectComposition • Sıfır WebView2 • Sıfır Özellik Ödünü", RectF::new(r.x + 40.0, r.y + 32.0, 480.0, 16.0), 10.2, 500, t.text_secondary, FontFamily::Ui, TextAlign::Left, true, false);

    let pg = RectF::new(r.right() - 88.0, r.y + 16.0, 40.0, 24.0);
    p.fill_rounded_rect(pg, 12.0, t.inset_bg);
    p.draw_text(&format!("{}/6", step + 1), pg, 10.5, 700, t.accent1, FontFamily::Mono, TextAlign::Center, true, false);
    draw_micro_button(p, hits, "tour:close", RectF::new(r.right() - 40.0, r.y + 15.0, 26.0, 26.0), IconKind::Close, t, false, true, hovered == Some("tour:close"), UiAction::CloseTour, Some("Kapat"));

    // 6 Step Pills
    let step_tabs = ["1 Motor", "2 Damla & Donanım", "3 Dock & Pencere", "4 Raf & Pano", "5 OCR & Telemetri", "6 Stüdyo & Arama"];
    let stw = (r.w - 32.0 - 30.0) / 6.0;
    for (i, lbl) in step_tabs.into_iter().enumerate() {
        let sr = RectF::new(r.x + 16.0 + i as f32 * (stw + 6.0), r.y + 56.0, stw, 26.0);
        let hid = format!("tour:step:{i}");
        draw_pill_button(p, hits, &hid, sr, lbl, None, t, i == step, hovered == Some(hid.as_str()), UiAction::OpenTour(i));
    }

    // Step Content Definitions (All 6 rich chapters!)
    struct TourFeature { icon: IconKind, title: &'static str, badge: &'static str, desc: &'static str }
    let (heading, subtitle, cards): (&str, &str, [TourFeature; 6]) = match step {
        0 => (
            "1. Saf Rust & Doğal GPU Mimarisi (Sıfır WebView2)",
            "Chromium / Edge süreçlerini tamamen ortadan kaldıran, 10× daha hafif yeni nesil Windows 11 kabuğu.",
            [
                TourFeature { icon: IconKind::Bolt, title: "Sıfır WebView2 Motoru", badge: "10× HAFİF", desc: "Direct3D 11 + Direct2D 1.3 + DirectComposition ile doğrudan GPU üzerinde <50 MB RAM ve %0.05 CPU." },
                TourFeature { icon: IconKind::Sparkles, title: "Gerçek DWM Akrilik Cam", badge: "W11 NATIVE", desc: "Yapay blur yerine Windows 11 DWM gerçek cam kırılması ve 120 Hz yay fiziği animasyonları." },
                TourFeature { icon: IconKind::Monitor, title: "Çoklu Monitör & Karma DPI", badge: "PMv2 DPI", desc: "%100, %125 ve %150 karışık ekranlarda piksel keskinliğinde ClearType Türkçe tipografi." },
                TourFeature { icon: IconKind::Clock, title: "7/24 Akıcı Saniyeli Saat", badge: "0% YÜK", desc: "Kısmi GPU yenileme (Present1) ile saniyeli saat her zaman açıkken bile işlemciye neredeyse sıfır yük." },
                TourFeature { icon: IconKind::Lock, title: "Güvenli & Sıfır UAC", badge: "ASINVOKER", desc: "explorer.exe asla öldürülmez; çökme korumalı Watchdog eski görev çubuğunu 100 ms içinde geri yükler." },
                TourFeature { icon: IconKind::Refresh, title: "Kesintisiz Veri Geçişi", badge: "v2 → v3", desc: "Önceki ayarlarınız, pano geçmişiniz, notlarınız ve ajandanız tek bir veri kaybı olmadan aktarılır." },
            ],
        ),
        1 => (
            "2. Üst Damla (Aurora Crown) & Canlı Donanım Kontrolü",
            "Ekranın üst ortasındaki dinamik damladan tüm ses, parlaklık, medya, Bluetooth ve gizlilik verilerini yönetin.",
            [
                TourFeature { icon: IconKind::Volume, title: "Anında Donanım OSD HUD", badge: "SIFIR GECİKME", desc: "Ses, sessiz, parlaklık, mikrofon ve Caps/Num Lock tuşlarında Windows kutusunu bastırıp üst damlada gösterir." },
                TourFeature { icon: IconKind::Sun, title: "Harici DDC/CI Parlaklık", badge: "EDID + I2C", desc: "Dahili laptop ekranı yanında harici monitörleri gerçek model adıyla algılayıp takılmadan ayarlar." },
                TourFeature { icon: IconKind::Bluetooth, title: "Bluetooth Pil Yüzdeleri", badge: "PNP + BLE", desc: "Kulaklık, fare, klavye ve gamepad şarj yüzdelerini anlık gösterir; %15 altında kırmızı uyarı verir." },
                TourFeature { icon: IconKind::Camera, title: "Kamera & Mikrofon LED'i", badge: "CANLI GÜVENLİK", desc: "Hangi uygulamanın kamera veya mikrofon kullandığını hayalet süreç korumasıyla anlık bildirir." },
                TourFeature { icon: IconKind::Headphones, title: "Medya & Uygulama Mikseri", badge: "COREAUDIO", desc: "Spotify, YouTube ve Chrome sesini ayrı ayrı kısın, çıkış kulaklığını tek tıkla değiştirin." },
                TourFeature { icon: IconKind::Coffee, title: "Uyanık Tut & Pomodoro", badge: "1 TIK", desc: "Sunum veya indirme sırasında ekran kararmasını engelleyin; dahili Pomodoro sayacı ile odaklanın." },
            ],
        ),
        2 => (
            "3. Sabit Çubuk, Yüzen Dock & Akıllı Pencere Yönetimi",
            "Giriş alanlarını kapatmayan pürüzsüz çalışma alanı koruması ve süper hızlı pencere hareketleri.",
            [
                TourFeature { icon: IconKind::Pin, title: "Çalışma Alanı Korumalı Sabit Bar", badge: "44/52/60 PX", desc: "AppBar WorkArea rezervasyonu sayesinde WhatsApp, VS Code ve Antigravity alt yazı alanları asla kapanmaz." },
                TourFeature { icon: IconKind::Sparkles, title: "Akıllı Derle & Klasör Yığını", badge: "SMART TIDY", desc: "Açık uygulamaları Geliştirme, Web, İletişim akordeonlarında toplar; Masaüstü ve İndirilenler'e anında erişir." },
                TourFeature { icon: IconKind::Layers, title: "Radyal Pencere Küresi", badge: "DWM CANLI", desc: "Çok pencereli uygulamalarda dairesel yörünge menüsüyle tek tıkla sola/sağa yasla, büyüt veya kapat." },
                TourFeature { icon: IconKind::Grid, title: "Shift+Sürükle Snap Zones", badge: "4 DÜZEN", desc: "Pencere sürüklerken Shift'e basılı tutun: 65/35 Kod, 3 Sütun, Stüdyo veya 2×2 ızgaraya milimetrik oturtun." },
                TourFeature { icon: IconKind::Mouse, title: "Alt+Sürükle Taşı & Boyutlandır", badge: "PRO UX", desc: "Başlık çubuğunu aramadan Alt+Sol Tık ile her pencereyi taşıyın, Alt+Sağ Tık ile boyutlandırın." },
                TourFeature { icon: IconKind::Bell, title: "Tıklanabilir Canlı Bildirimler", badge: "4-KATMAN", desc: "WhatsApp, Discord ve sistem bildirimlerine tıkladığınızda ilgili uygulama ve sohbet doğrudan öne gelir." },
            ],
        ),
        3 => (
            "4. Süper Raf, Kalıcı Pano Kasası & LAN QR AirDrop",
            "Dosya aktarımı, kalıcı pano geçmişi ve karalama defteri tek bir üretkenlik merkezinde.",
            [
                TourFeature { icon: IconKind::Inbox, title: "Sürükle-Bırak Dosya Rafı", badge: "DROP SHELF", desc: "Dosyaları Dock'a veya rafa bırakın, tek tıkla tek bir ZIP arşivine paketleyin veya başka pencereye taşıyın." },
                TourFeature { icon: IconKind::Qr, title: "Yerel Wi-Fi QR AirDrop", badge: "SIFIR BULUT", desc: "Dosyalarınızı internet olmadan aynı ağdaki telefonunuza anında aktarmak için yerel QR kod oluşturur." },
                TourFeature { icon: IconKind::Clip, title: "SQLite Kalıcı Pano Kasası", badge: "METİN + PNG", desc: "Kopyalanan metin ve ekran görüntülerini veritabanında saklar; arama, sabitleme ve oto-yapıştırma sunar." },
                TourFeature { icon: IconKind::Note, title: "Karalama Defteri", badge: "OTO KAYIT", desc: "Toplantı notlarınızı ve komutları anında yazın; uygulama yeniden açıldığında kaldığınız yerden devam edin." },
                TourFeature { icon: IconKind::Calendar, title: "Takvim & Günlük Ajanda", badge: "TÜRKÇE", desc: "Saate tıklayıp Türkçe takvimi açın, her gün için ayrı hatırlatma notları kaydedin." },
                TourFeature { icon: IconKind::Zip, title: "Klasik Tepsi Çekmecesi", badge: "10 SN BAKIŞ", desc: "Steam, NVIDIA, Discord gibi arka plan simgelerine ve 10 saniyelik klasik tepsi görünümüne ulaşın." },
            ],
        ),
        4 => (
            "5. Ekran Yakalama Araçları & Canlı Telemetri",
            "Ekstra üçüncü parti araçlara gerek kalmadan dahili OCR, Renk Pipeti, GIF Kaydedici ve Sistem İzleyici.",
            [
                TourFeature { icon: IconKind::Scissors, title: "Çevrimdışı WinRT OCR", badge: "ALT+SHIFT+S", desc: "Ekranın herhangi bir bölgesindeki resim veya videodan Türkçe/İngilizce metni anında panoya kopyalar." },
                TourFeature { icon: IconKind::Pipette, title: "Canlı Renk Pipeti", badge: "ALT+SHIFT+C", desc: "İmleç altındaki pikselin tam #HEX ve RGB değerini gerçek zamanlı yakalayıp panoya kopyalar." },
                TourFeature { icon: IconKind::Record, title: "10 FPS GIF Kaydedici", badge: "ALT+SHIFT+R", desc: "Harici FFmpeg gerekmeden seçili ekran bölgesini akıcı animasyonlu GIF olarak kaydeder." },
                TourFeature { icon: IconKind::Activity, title: "Gerçek Zamanlı Grafikler", badge: "DIRECT2D", desc: "CPU, RAM, Ağ (Mb/s) ve batarya durumunu pürüzsüz GPU kıvılcım grafikleriyle canlı takip edin." },
                TourFeature { icon: IconKind::Trash, title: "Güvenli İşlem Sonlandırıcı", badge: "2-ADIM ONAY", desc: "En çok RAM tüketen Top-3 işlemi görün; kritik Windows süreçlerini koruyan güvenli kalkanla sonlandırın." },
                TourFeature { icon: IconKind::Power, title: "Çift Onaylı Güç Satırı", badge: "GÜVENLİ", desc: "Kilitle, Uyku, Yeniden Başlat ve Kapat işlemleri kazara tıklamayı önleyen 2 adımlı onay ile çalışır." },
            ],
        ),
        _ => (
            "6. 7 Görsel Mimari, Canlı Renk Stüdyosu & OmniSearch",
            "Sadece renk değiştirmeyen; form, kenarlık, doku ve tipografiyi baştan kuran 7 özgün tasarım dili.",
            [
                TourFeature { icon: IconKind::Palette, title: "7 Özgün Mimari Stil", badge: "WCAG AA+", desc: "Liquid Glass 26, Bento Silk, Linear Swiss, Mono Matrix, Spatial Vision, Cyber HUD ve Yüksek Kontrast." },
                TourFeature { icon: IconKind::Sliders, title: "Canlı 2D HSV Renk Stüdyosu", badge: "HER TEMA ÖZEL", desc: "Vurgu, yüzey, metin rengini ve cam opaklığını canlı WCAG kontrast göstergesiyle anında kişiselleştirin." },
                TourFeature { icon: IconKind::Search, title: "Anında OmniSearch", badge: "ALT+SPACE", desc: "250+ uygulama, dosya, Windows ayarı, anlık hesap makinesi (=24*15) ve döviz/birim çevirici." },
                TourFeature { icon: IconKind::Layers, title: "Çalışma Profilleri", badge: "4 PROFİL", desc: "Kişisel, İş, Odak ve Oyun profilleri arasında tema ve bildirim ayarlarını tek tıkla değiştirin." },
                TourFeature { icon: IconKind::Folder, title: "Tek Tık Yedek & Geri Yükle", badge: "JSON PAKET", desc: "Tüm ayar, kısayol, not ve tema özelleştirmelerinizi JSON olarak dışa aktarın veya geri yükleyin." },
                TourFeature { icon: IconKind::Refresh, title: "SHA-256 Otomatik Güncelleme", badge: "GÜVENLİ", desc: "Yeni GitHub sürümlerini SHA-256 kriptografik doğrulama ile kontrol edip güvenle kurar." },
            ],
        ),
    };

    // Hero subheader box
    let hero = RectF::new(r.x + 16.0, r.y + 92.0, r.w - 32.0, 52.0);
    p.fill_rounded_rect(hero, 12.0, t.accent_soft);
    p.stroke_rounded_rect(hero, 12.0, t.accent_ring, 1.0, false);
    p.draw_text(heading, RectF::new(hero.x + 14.0, hero.y + 6.0, hero.w - 28.0, 20.0), 12.8, 700, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
    p.draw_text(subtitle, RectF::new(hero.x + 14.0, hero.y + 28.0, hero.w - 28.0, 18.0), 10.2, 500, t.text_secondary, FontFamily::Ui, TextAlign::Left, true, false);

    // 2×3 Feature Cards Grid
    let cw = (r.w - 42.0) * 0.5;
    let ch = 82.0;
    for (idx, f) in cards.iter().enumerate() {
        let col = (idx % 2) as f32;
        let row = (idx / 2) as f32;
        let cr = RectF::new(r.x + 16.0 + col * (cw + 10.0), hero.bottom() + 10.0 + row * (ch + 8.0), cw, ch);
        p.fill_rounded_rect(cr, 11.0, t.inset_bg);
        p.stroke_rounded_rect(cr, 11.0, t.border_subtle, 1.0, false);

        let ibox = RectF::new(cr.x + 10.0, cr.y + 10.0, 26.0, 26.0);
        p.fill_rounded_rect(ibox, 8.0, t.accent_tint);
        p.draw_icon(f.icon, ibox.center_x(), ibox.center_y(), 13.0, t.accent1);

        p.draw_text(f.title, RectF::new(ibox.right() + 8.0, cr.y + 10.0, cr.w - 122.0, 18.0), 11.0, 700, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
        let bdg = RectF::new(cr.right() - 80.0, cr.y + 10.0, 72.0, 16.0);
        p.fill_rounded_rect(bdg, 6.0, t.accent_soft);
        p.draw_text(f.badge, bdg, 8.0, 700, t.accent1, FontFamily::Mono, TextAlign::Center, true, false);

        p.draw_text(f.desc, RectF::new(ibox.right() + 8.0, cr.y + 32.0, cr.w - 52.0, 44.0), 9.8, 500, t.text_secondary, FontFamily::Ui, TextAlign::Left, false, true);
    }

    // Bottom Footer: Progress bar + Prev / Next-Finish buttons
    let fy = r.bottom() - 40.0;
    let prog_track = RectF::new(r.x + 16.0, fy + 11.0, 140.0, 6.0);
    p.fill_rounded_rect(prog_track, 3.0, t.inset_bg);
    p.fill_linear_gradient_rounded(RectF::new(prog_track.x, prog_track.y, prog_track.w * ((step + 1) as f32 / 6.0), 6.0), 3.0, t.accent1, t.accent2, false);
    p.draw_text("← / → Adım Değiştir  •  ESC Kapat", RectF::new(prog_track.right() + 14.0, fy + 4.0, 220.0, 20.0), 9.8, 500, t.text_secondary, FontFamily::Mono, TextAlign::Left, true, false);

    if step > 0 {
        draw_pill_button(p, hits, "tour:prev", RectF::new(r.right() - 216.0, fy, 92.0, 28.0), "‹ Önceki", None, t, false, hovered == Some("tour:prev"), UiAction::OpenTour(step - 1));
    }
    if step < 5 {
        draw_pill_button(p, hits, "tour:next", RectF::new(r.right() - 116.0, fy, 100.0, 28.0), "Sonraki ›", None, t, true, hovered == Some("tour:next"), UiAction::OpenTour(step + 1));
    } else {
        draw_pill_button(p, hits, "tour:done", RectF::new(r.right() - 122.0, fy, 106.0, 28.0), "Başlayalım ✓", None, t, true, hovered == Some("tour:done"), UiAction::CloseTour);
    }
}

fn paint_snap_zones_overlay(p: &Painter<'_>, state: &ShellState, canvas_w: f32, canvas_h: f32) {
    let t = &state.theme;
    let zones = compute_snap_zones(
        PhysicalRect::new(16, 46, (canvas_w - 32.0) as i32, (canvas_h - 110.0) as i32),
        state.prefs.effective_snap_preset(),
        12,
    );
    for (idx, &(label, zr)) in zones.iter().enumerate() {
        let rr = RectF::new(zr.x as f32, zr.y as f32, zr.width as f32, zr.height as f32);
        let active = state.active_snap_hover_idx == Some(idx);
        p.fill_rounded_rect(rr, 16.0, if active { t.accent1.with_alpha(0.28) } else { t.surface1.with_alpha(0.45) });
        p.stroke_rounded_rect(rr, 16.0, if active { t.accent1 } else { t.border_rim }, if active { 2.5 } else { 1.4 }, !active);
        p.draw_text(label, rr, 16.0, 700, if active { Color::WHITE } else { t.text_primary }, FontFamily::Ui, TextAlign::Center, true, false);
    }
}

fn paint_region_snip_overlay(p: &Painter<'_>, state: &ShellState, mode: &SnipMode, canvas_w: f32, canvas_h: f32) {
    let t = &state.theme;
    p.fill_rect(RectF::new(0.0, 0.0, canvas_w, canvas_h), Color::BLACK.with_alpha(0.32));

    let banner = match mode {
        SnipMode::Ocr => "OCR Metin Yakalama: Metni kopyalamak için bir alan sürükleyin (ESC iptal)",
        SnipMode::Eyedropper => "Renk Pipeti: Kopyalamak için herhangi bir piksele tıklayın (ESC iptal)",
        SnipMode::GifRegion => "GIF Kaydedici (10 FPS): Kaydedilecek bölgeyi sürükleyin (ESC iptal)",
    };
    let br = RectF::new((canvas_w - 480.0) * 0.5, 18.0, 480.0, 34.0);
    ThemePainter::draw_shell(p, br, t, false);
    p.draw_text(banner, br, 11.2, 600, t.text_primary, FontFamily::Ui, TextAlign::Center, true, false);

    match mode {
        SnipMode::Eyedropper => {
            let (mx, my) = state.eyedropper_cursor;
            let swatch = Color::from_hex(&state.eyedropper_hex);
            let pill = RectF::new((mx + 18.0).min(canvas_w - 130.0), (my + 18.0).min(canvas_h - 46.0), 116.0, 34.0);
            ThemePainter::draw_shell(p, pill, t, false);
            p.fill_circle(pill.x + 17.0, pill.center_y(), 9.0, swatch);
            p.stroke_circle(pill.x + 17.0, pill.center_y(), 9.0, Color::WHITE, 1.5, false);
            p.draw_text(&state.eyedropper_hex, RectF::new(pill.x + 32.0, pill.y, 76.0, pill.h), 12.0, 700, t.text_primary, FontFamily::Mono, TextAlign::Left, true, false);
        }
        SnipMode::Ocr | SnipMode::GifRegion => {
            if let (Some((x0, y0)), Some((x1, y1))) = (state.snip_drag_start, state.snip_drag_end) {
                let rx = x0.min(x1);
                let ry = y0.min(y1);
                let rw = (x1 - x0).abs();
                let rh = (y1 - y0).abs();
                if rw > 4.0 && rh > 4.0 {
                    let sr = RectF::new(rx, ry, rw, rh);
                    p.fill_rect(sr, t.accent1.with_alpha(0.12));
                    p.stroke_rounded_rect(sr, 4.0, t.accent1, 2.0, false);
                    let dim_txt = format!("{} × {} px", rw.round() as i32, rh.round() as i32);
                    p.draw_text(&dim_txt, RectF::new(rx, (ry - 22.0).max(4.0), 110.0, 18.0), 10.5, 700, Color::WHITE, FontFamily::Mono, TextAlign::Left, true, false);
                }
            }
        }
    }
}
