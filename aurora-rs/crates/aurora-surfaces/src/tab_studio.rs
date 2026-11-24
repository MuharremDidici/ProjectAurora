use aurora_core::{built_in_themes, Color, CURATED_ACCENT_PRESETS};
use aurora_render::{FontFamily, IconKind, Painter, RectF, TextAlign};
use aurora_ui::{draw_hsv_color_picker, draw_pill_button, draw_slider, HitTree, ThemePainter, UiAction};

use crate::ShellState;

/// Tab 4: Stüdyo & Ayarlar — All 7 Visual Style Architecture cards (with live mini previews),
/// Live 2D HSV Per-Theme Color Customizer + Opacity Slider + WCAG AA+ Ratio Pill,
/// Complete Dock/Bar/WorkArea/Taskbar/OSD/AltDrag/SnapZones/Seconds Controls,
/// Backup/Restore, SHA-256 GitHub Auto-Updater, and Interactive Tour launcher.
pub fn paint_tab_studio(p: &Painter<'_>, hits: &mut HitTree, state: &ShellState, r: RectF) {
    let t = &state.theme;
    let hovered = state.hovered_id.as_deref();
    let gap = 12.0;
    let left_w = (r.w - gap) * 0.56;
    let right_w = r.w - gap - left_w;

    // ═══════════ LEFT COLUMN: 7 VISUAL STYLES GALLERY + LIVE COLOR STUDIO ═══════════
    let gal_h = 188.0;
    let gal_r = RectF::new(r.x, r.y, left_w, gal_h);
    ThemePainter::draw_card(p, gal_r, "7 Görsel Stil Mimarisi (2026)", t);

    let themes = built_in_themes();
    let cols = 4;
    let card_gap = 7.0;
    let cw = (gal_r.w - 24.0 - card_gap * (cols as f32 - 1.0)) / cols as f32;
    let ch = 68.0;
    for (idx, th) in themes.iter().enumerate() {
        let col = idx % cols;
        let row = idx / cols;
        let cr = RectF::new(
            gal_r.x + 12.0 + col as f32 * (cw + card_gap),
            gal_r.y + 30.0 + row as f32 * (ch + card_gap),
            cw,
            ch,
        );
        let active = th.id.eq_ignore_ascii_case(&state.theme_id);
        let hid = format!("th:{}", th.id);
        let is_hov = hovered == Some(hid.as_str());

        let th_s1 = Color::from_hex(&th.colors.surface1);
        let th_s2 = Color::from_hex(&th.colors.surface2);
        let th_a1 = Color::from_hex(&th.colors.accent_primary);
        let th_a2 = Color::from_hex(&th.colors.accent_secondary);
        let th_txt = Color::from_hex(&th.colors.text_primary);

        let rad = th.radius_card.min(12.0);
        p.fill_linear_gradient_rounded(cr, rad, th_s1, th_s2, true);
        p.stroke_rounded_rect(
            cr,
            rad,
            if active { t.accent1 } else if is_hov { th_a1 } else { Color::from_hex(&th.colors.border_strong).with_alpha(0.55) },
            if active { 2.0 } else { 1.0 },
            false,
        );

        // Mini preview bar inside the theme tile
        let mini = RectF::new(cr.x + 8.0, cr.y + 8.0, cr.w - 16.0, 12.0);
        p.fill_linear_gradient_rounded(mini, 4.0, th_a1, th_a2, false);
        if active {
            p.fill_circle(cr.right() - 12.0, cr.y + 14.0, 6.0, Color::WHITE);
            p.draw_icon(IconKind::Check, cr.right() - 12.0, cr.y + 14.0, 8.5, Color::BLACK);
        }

        p.draw_text(&th.display_name, RectF::new(cr.x + 7.0, cr.y + 26.0, cr.w - 14.0, 16.0), 10.0, 700, th_txt, FontFamily::Ui, TextAlign::Left, true, false);
        p.draw_text(th.visual_style.style_tag_tr(), RectF::new(cr.x + 7.0, cr.y + 44.0, cr.w - 14.0, 14.0), 8.8, 600, th_a1, FontFamily::Mono, TextAlign::Left, true, false);

        hits.push(hid, cr, UiAction::SelectTheme(th.id.clone()));
    }

    // ── Live 2D HSV Color Studio + Opacity + WCAG Contrast Badge ──
    let col_r = RectF::new(r.x, gal_r.bottom() + gap, left_w, r.bottom() - gal_r.bottom() - gap);
    ThemePainter::draw_card(p, col_r, "Canlı Tema Renk Stüdyosu (HSV + Opaklık)", t);

    // Live WCAG AA/AAA badge in top-right of card
    let ratio = t.text_contrast_ratio;
    let wcag_ok = ratio >= 4.5;
    let wcag_r = RectF::new(col_r.right() - 118.0, col_r.y + 5.0, 108.0, 19.0);
    p.fill_rounded_rect(wcag_r, 9.5, if wcag_ok { t.success.with_alpha(0.18) } else { t.warning.with_alpha(0.20) });
    p.draw_text(
        &format!("WCAG {:.1}:1 {}", ratio, if ratio >= 7.0 { "AAA" } else if wcag_ok { "AA" } else { "Düşük" }),
        wcag_r,
        9.0,
        700,
        if wcag_ok { t.success } else { t.warning },
        FontFamily::Mono,
        TextAlign::Center,
        true,
        false,
    );

    // Left sub-pane inside Color Studio: 2D HSV Live Color Picker
    let picker_r = RectF::new(col_r.x + 12.0, col_r.y + 32.0, 178.0, col_r.h - 46.0);
    draw_hsv_color_picker(
        p,
        hits,
        picker_r,
        &state.studio_color_slot,
        state.studio_hue,
        state.studio_sat,
        state.studio_val,
        t,
    );

    // Right sub-pane inside Color Studio: Slot selector, 8 Curated Accent Presets, Opacity Slider, Reset
    let sx = picker_r.right() + 12.0;
    let sw = col_r.right() - sx - 12.0;
    let mut sy = col_r.y + 32.0;

    let slots = [("accent1", "Vurgu 1"), ("accent2", "Vurgu 2"), ("surface1", "Yüzey 1"), ("text", "Metin")];
    let slot_w = (sw - 12.0) / 4.0;
    for (i, (sid, slbl)) in slots.into_iter().enumerate() {
        let sr = RectF::new(sx + i as f32 * (slot_w + 4.0), sy, slot_w, 22.0);
        let hid = format!("slot:{sid}");
        draw_pill_button(
            p, hits, &hid, sr, slbl, None, t, state.studio_color_slot == sid, hovered == Some(hid.as_str()),
            UiAction::SetCustomColorSlot { slot: sid.into(), hex: String::new() },
        );
    }
    sy += 30.0;

    // 8 Curated Accent Swatches
    p.draw_text("HAZIR VURGU PALETLERİ", RectF::new(sx, sy, sw, 14.0), 9.0, 700, t.text_secondary, FontFamily::Ui, TextAlign::Left, true, false);
    sy += 17.0;
    let sw_sz = 24.0;
    let sw_gap = ((sw - 8.0 * sw_sz) / 7.0).clamp(4.0, 10.0);
    for (i, preset) in CURATED_ACCENT_PRESETS.iter().enumerate() {
        let pr = RectF::new(sx + i as f32 * (sw_sz + sw_gap), sy, sw_sz, sw_sz);
        let hid = format!("preset:{i}");
        p.fill_linear_gradient_rounded(pr, 12.0, Color::from_hex(preset.accent1), Color::from_hex(preset.accent2), false);
        p.stroke_rounded_rect(pr, 12.0, if hovered == Some(hid.as_str()) { Color::WHITE } else { t.border_rim }, 1.2, false);
        hits.push_with_tip(hid, pr, UiAction::ApplyAccentPreset(i), preset.name);
    }
    sy += 34.0;

    // Surface Opacity Slider (55%..100%)
    let op_pct = (state.theme_tokens.surface_opacity * 100.0).round() as u8;
    let op_norm = ((state.theme_tokens.surface_opacity - 0.55) / 0.45).clamp(0.0, 1.0);
    draw_slider(
        p,
        hits,
        "sl:opacity",
        RectF::new(sx, sy, sw, 32.0),
        "Cam Opaklığı",
        &format!("{op_pct}%"),
        op_norm,
        None,
        t,
        hovered == Some("sl:opacity"),
        "surface-opacity",
    );
    sy += 40.0;

    // Reset theme colors & Open Tour buttons
    let half_btn = (sw - 6.0) * 0.5;
    draw_pill_button(p, hits, "th:reset", RectF::new(sx, sy, half_btn, 26.0), "Sıfırla", Some(IconKind::Refresh), t, false, hovered == Some("th:reset"), UiAction::ResetThemeColors);
    draw_pill_button(p, hits, "studio:tour", RectF::new(sx + half_btn + 6.0, sy, half_btn, 26.0), "Hızlı Tur", Some(IconKind::Sparkles), t, true, hovered == Some("studio:tour"), UiAction::OpenTour(0));

    // ═══════════ RIGHT COLUMN: DOCK / WORKAREA / BEHAVIOR MATRIX + BACKUP + UPDATER ═══════════
    let cfg_r = RectF::new(r.x + left_w + gap, r.y, right_w, r.h - 102.0);
    ThemePainter::draw_card(p, cfg_r, "Dock, Sabit Bar & Kabuk Ayarları", t);

    let cx = cfg_r.x + 12.0;
    let cw2 = cfg_r.w - 24.0;
    let mut cy = cfg_r.y + 30.0;

    // Row 1: Dock Visibility Mode
    let mode = state.prefs.effective_dock_mode();
    p.draw_text("Dock Görünümü", RectF::new(cx, cy, 108.0, 24.0), 10.5, 600, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
    let m_btns = [("AlwaysVisible", "Sabit"), ("SmartDodge", "Kaçış"), ("AutoHide", "Gizle")];
    let mbw = (cw2 - 110.0) / 3.0;
    for (i, (mv, ml)) in m_btns.into_iter().enumerate() {
        let hid = format!("dmode:{mv}");
        let mr = RectF::new(cx + 108.0 + i as f32 * (mbw + 2.0), cy, mbw - 2.0, 24.0);
        draw_pill_button(p, hits, &hid, mr, ml, None, t, mode == mv, hovered == Some(hid.as_str()), UiAction::SetDockMode(mv.into()));
    }
    cy += 30.0;

    // Row 2: Dock Edge (Alt / Sol / Sağ)
    let edge = state.prefs.effective_dock_edge();
    p.draw_text("Dock Kenarı", RectF::new(cx, cy, 108.0, 24.0), 10.5, 600, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
    for (i, (ev, el)) in [("Bottom", "Alt"), ("Left", "Sol"), ("Right", "Sağ")].into_iter().enumerate() {
        let hid = format!("dedge:{ev}");
        let mr = RectF::new(cx + 108.0 + i as f32 * (mbw + 2.0), cy, mbw - 2.0, 24.0);
        draw_pill_button(p, hits, &hid, mr, el, None, t, edge == ev, hovered == Some(hid.as_str()), UiAction::SetDockEdge(ev.into()));
    }
    cy += 30.0;

    // Row 3: Dock Density (44px / 52px / 60px)
    let den = state.prefs.effective_dock_density();
    p.draw_text("Bar Boyutu", RectF::new(cx, cy, 108.0, 24.0), 10.5, 600, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
    for (i, (dv, dl)) in [("Compact", "44px"), ("Standard", "52px"), ("Cozy", "60px")].into_iter().enumerate() {
        let hid = format!("dden:{dv}");
        let mr = RectF::new(cx + 108.0 + i as f32 * (mbw + 2.0), cy, mbw - 2.0, 24.0);
        draw_pill_button(p, hits, &hid, mr, dl, None, t, den == dv, hovered == Some(hid.as_str()), UiAction::SetDockDensity(dv.into()));
    }
    cy += 30.0;

    // Row 4: Fixed Bar Layout (Ortalanmış / 3 Bölge)
    let lay = state.prefs.effective_dock_bar_layout();
    p.draw_text("Sabit Bar Düzeni", RectF::new(cx, cy, 108.0, 24.0), 10.5, 600, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
    let lbw = (cw2 - 110.0) * 0.5;
    draw_pill_button(p, hits, "dlay:Centered", RectF::new(cx + 108.0, cy, lbw - 2.0, 24.0), "Ortalanmış", None, t, lay == "Centered", hovered == Some("dlay:Centered"), UiAction::SetDockBarLayout("Centered".into()));
    draw_pill_button(p, hits, "dlay:Split3Zone", RectF::new(cx + 108.0 + lbw + 2.0, cy, lbw - 2.0, 24.0), "3 Bölge", None, t, lay == "Split3Zone", hovered == Some("dlay:Split3Zone"), UiAction::SetDockBarLayout("Split3Zone".into()));
    cy += 34.0;

    // 2-Column Crisp Feature Switch Grid (100% Parity!)
    let switches = [
        ("sw:wa", "Çalışma Alanı Ayır", state.prefs.effective_reserve_pref(), UiAction::ToggleReserveWorkArea),
        ("sw:tb", "Win Barı Gizle", state.prefs.hide_native_taskbar_while_active, UiAction::ToggleNativeTaskbar),
        ("sw:sec", "Saat Saniyesi (7/24)", state.prefs.show_clock_seconds, UiAction::ToggleClockSeconds),
        ("sw:osd", "Donanım OSD HUD", state.prefs.hardware_osd_enabled, UiAction::ToggleHardwareOsd),
        ("sw:alt", "Alt+Sürükle Pencere", state.prefs.alt_drag_enabled, UiAction::ToggleAltDrag),
        ("sw:snap", "Shift+Sürükle Snap", state.prefs.snap_zones_enabled, UiAction::ToggleSnapZones),
        ("sw:win", "Win Tuşu OmniSearch", state.prefs.win_key_opens_omni_search, UiAction::ToggleWinKeyOmni),
        ("sw:dwm", "DWM Canlı Önizleme", state.prefs.dwm_live_preview_enabled, UiAction::ToggleDwmPreview),
        ("sw:nb", "Bildirim Köprüsü", state.prefs.system_notification_bridge_enabled, UiAction::ToggleNotifBridge),
        ("sw:ap", "Pano Oto-Yapıştır", state.prefs.clipboard_auto_paste_enabled, UiAction::ToggleClipboardAutoPaste),
    ];

    let sw_w = (cw2 - 6.0) * 0.5;
    for (idx, (id, lbl, on, act)) in switches.into_iter().enumerate() {
        let col = (idx % 2) as f32;
        let row = (idx / 2) as f32;
        let sr = RectF::new(cx + col * (sw_w + 6.0), cy + row * 29.0, sw_w, 25.0);
        if sr.bottom() > cfg_r.bottom() - 6.0 {
            break;
        }
        let is_hov = hovered == Some(id);
        p.fill_rounded_rect(sr, 7.0, if on { t.accent_soft } else if is_hov { t.card_hover } else { t.inset_bg });
        p.stroke_rounded_rect(sr, 7.0, if on { t.accent_ring } else { t.border_subtle }, 1.0, false);
        p.draw_text(lbl, RectF::new(sr.x + 8.0, sr.y, sr.w - 34.0, sr.h), 9.8, 600, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
        // Toggle pill indicator
        let ind = RectF::new(sr.right() - 28.0, sr.center_y() - 6.0, 22.0, 12.0);
        p.fill_rounded_rect(ind, 6.0, if on { t.accent1 } else { t.border_rim });
        let knob_x = if on { ind.right() - 6.0 } else { ind.x + 6.0 };
        p.fill_circle(knob_x, ind.center_y(), 4.5, Color::WHITE);
        hits.push(id, sr, act);
    }

    // ── Bottom-Right Card: Backup / Restore + SHA-256 GitHub Auto-Updater + Exit ──
    let upd_r = RectF::new(r.x + left_w + gap, cfg_r.bottom() + gap, right_w, 90.0);
    ThemePainter::draw_card(p, upd_r, "Yedekleme & Güvenli Güncelleme (SHA-256)", t);

    p.draw_text(&state.update_status, RectF::new(upd_r.x + 12.0, upd_r.y + 28.0, upd_r.w - 24.0, 16.0), 10.0, 600, t.accent1, FontFamily::Mono, TextAlign::Left, true, false);
    let act_y = upd_r.y + 52.0;
    let b3 = (upd_r.w - 36.0) / 3.0;
    draw_pill_button(p, hits, "sys:exp", RectF::new(upd_r.x + 12.0, act_y, b3, 26.0), "Yedekle", None, t, false, hovered == Some("sys:exp"), UiAction::ExportBackup);
    draw_pill_button(p, hits, "sys:imp", RectF::new(upd_r.x + 18.0 + b3, act_y, b3, 26.0), "Geri Yükle", None, t, false, hovered == Some("sys:imp"), UiAction::ImportBackup);
    if state.latest_update.is_some() {
        draw_pill_button(p, hits, "sys:inst", RectF::new(upd_r.x + 24.0 + b3 * 2.0, act_y, b3, 26.0), "Güncelle", Some(IconKind::Refresh), t, true, hovered == Some("sys:inst"), UiAction::InstallUpdate);
    } else {
        draw_pill_button(p, hits, "sys:chk", RectF::new(upd_r.x + 24.0 + b3 * 2.0, act_y, b3, 26.0), "Denetle", Some(IconKind::Refresh), t, true, hovered == Some("sys:chk"), UiAction::CheckUpdates);
    }
}
