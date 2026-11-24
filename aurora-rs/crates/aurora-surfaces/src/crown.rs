use aurora_core::VisualStyle;
use aurora_render::{FontFamily, IconKind, Painter, RectF, TextAlign};
use aurora_ui::{draw_micro_button, draw_tab_bar, HitTree, ThemePainter, UiAction};

use crate::{paint_tab_control, paint_tab_metrics, paint_tab_shelf, paint_tab_studio, ShellState};

/// Renders the Top Resting Crown Droplet (with live clock/seconds, weather, media pulse, privacy LEDs,
/// low Bluetooth battery alert, notification badge, and live Hardware OSD Morph) AND the Expanded
/// 900×528 DIP Bento Command Deck when open.
pub fn paint_crown_and_deck(
    p: &Painter<'_>,
    hits: &mut HitTree,
    state: &ShellState,
    canvas_w: f32,
) {
    if state.is_fullscreen_on_monitor {
        return;
    }

    paint_top_droplet(p, hits, state, canvas_w);

    if state.crown_expanded {
        paint_bento_deck(p, hits, state, canvas_w);
    }
}

fn paint_top_droplet(p: &Painter<'_>, hits: &mut HitTree, state: &ShellState, canvas_w: f32) {
    let t = &state.theme;
    let hovered = state.hovered_id.as_deref();

    // Active Hardware OSD takes priority in the top Droplet (Zero-Latency Volume/Brightness/Mic/Caps HUD!)
    if let Some(osd) = &state.active_osd {
        let w = 290.0;
        let h = 36.0;
        let y = if matches!(t.style, VisualStyle::LiquidGlass | VisualStyle::HighContrast) { 0.0 } else { 8.0 };
        let r = RectF::new((canvas_w - w) * 0.5, y, w, h);
        let osd_color = match osd.event.kind.as_str() {
            "mute" => t.danger,
            "mic" => if osd.event.active { t.danger } else { t.success },
            "brightness" => t.warning,
            _ => t.accent1,
        };
        ThemePainter::draw_crown_droplet(p, r, t, Some(osd_color));
        hits.push_shell(r);

        let ic = match osd.event.kind.as_str() {
            "mute" => IconKind::Mute,
            "volume" => IconKind::Volume,
            "brightness" => IconKind::Sun,
            "mic" => if osd.event.active { IconKind::MicOff } else { IconKind::Mic },
            _ => IconKind::Keyboard,
        };
        p.draw_icon(ic, r.x + 22.0, r.center_y(), 15.0, osd_color);
        p.draw_text(&osd.event.label, RectF::new(r.x + 36.0, r.y, 62.0, r.h), 11.0, 700, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);

        if let Some(pct) = osd.event.percent {
            let track = RectF::new(r.x + 88.0, r.center_y() - 3.5, 144.0, 7.0);
            p.fill_rounded_rect(track, 3.5, t.inset_bg);
            let fill_w = (track.w * (pct as f32 / 100.0)).max(6.0);
            p.fill_linear_gradient_rounded(RectF::new(track.x, track.y, fill_w, track.h), 3.5, osd_color, t.accent2, false);
        }

        p.draw_text(
            &osd.event.value_text,
            RectF::new(r.right() - 56.0, r.y, 44.0, r.h),
            11.0,
            700,
            osd_color,
            FontFamily::Mono,
            TextAlign::Right,
            true,
            false,
        );
        return;
    }

    // Normal resting Crown Droplet (auto-expands cleanly to fit active media / weather / privacy / BT low / notifs)
    let has_media = state.media.has_session && !state.media.title.is_empty();
    let has_privacy = state.privacy.camera_active || state.privacy.mic_active;
    let low_bt = state.bluetooth_devices.iter().find(|d| d.is_low);
    let has_notif = !state.notifications.is_empty();

    let mut w: f32 = 228.0;
    if state.prefs.show_clock_seconds { w += 20.0; }
    if state.weather.valid { w += 64.0; }
    if has_media { w += 148.0; }
    if has_privacy { w += 42.0; }
    if low_bt.is_some() { w += 56.0; }
    if has_notif { w += 42.0; }
    if state.gif_recording { w += 76.0; }
    w = w.min(canvas_w - 40.0);

    let h = 34.0;
    let y = match t.style {
        VisualStyle::LiquidGlass | VisualStyle::CyberHud | VisualStyle::HighContrast => 0.0,
        _ => 7.0,
    };
    let r = RectF::new((canvas_w - w) * 0.5, y, w, h);

    ThemePainter::draw_crown_droplet(p, r, t, None);
    hits.push_shell(r);
    // Fallback click anywhere on droplet body opens Deck (pushed FIRST so specific buttons override it!)
    hits.push("crown:body", r, UiAction::ToggleDeck(state.active_deck_tab.clone()));

    let mut x = r.x + 14.0;
    let cy = r.center_y();

    // Aurora orb / active profile indicator
    p.fill_circle(x + 5.0, cy, 4.5, t.accent1);
    p.fill_radial_glow(x + 5.0, cy, 10.0, 10.0, t.accent_glow);
    x += 16.0;

    // Live Clock (with optional 7/24 seconds in crisp tabular monospace!)
    let clk = state.formatted_clock();
    let clk_w = if state.prefs.show_clock_seconds { 66.0 } else { 46.0 };
    p.draw_text(&clk, RectF::new(x, r.y, clk_w, r.h), 12.0, 700, t.text_primary, FontFamily::Mono, TextAlign::Left, true, false);
    x += clk_w + 6.0;

    // Date pill
    let dt = state.formatted_date_tr();
    p.draw_text(&dt, RectF::new(x, r.y, 44.0, r.h), 10.5, 500, t.text_secondary, FontFamily::Ui, TextAlign::Left, true, false);
    hits.push_with_tip("crown:cal", RectF::new(r.x + 26.0, r.y, clk_w + 52.0, r.h), UiAction::ToggleCalendar, "Takvim & Ajanda");
    x += 48.0;

    // Weather
    if state.weather.valid {
        p.draw_line(x, r.y + 9.0, x, r.bottom() - 9.0, t.border_subtle, 1.0, false);
        x += 8.0;
        p.draw_icon(IconKind::Cloud, x + 7.0, cy, 13.0, t.accent1);
        let temp = format!("{}°", state.weather.temp_c.round() as i32);
        p.draw_text(&temp, RectF::new(x + 16.0, r.y, 34.0, r.h), 11.0, 600, t.text_primary, FontFamily::Mono, TextAlign::Left, true, false);
        x += 52.0;
    }

    // Live Playing Media pill + GPU-animated 4-bar equalizer
    if has_media {
        p.draw_line(x, r.y + 9.0, x, r.bottom() - 9.0, t.border_subtle, 1.0, false);
        x += 9.0;
        let eq_x = x;
        for i in 0..4 {
            let phase = state.anim.eq_phase + i as f32 * 1.15;
            let bar_h = if state.media.is_playing {
                4.0 + ((phase.sin() * 0.5 + 0.5) * 9.0)
            } else {
                3.0
            };
            p.fill_rounded_rect(
                RectF::new(eq_x + i as f32 * 4.0, cy + 6.0 - bar_h, 2.5, bar_h),
                1.2,
                t.accent1,
            );
        }
        x += 20.0;
        let title_r = RectF::new(x, r.y, 118.0, r.h);
        p.draw_text(&state.media.title, title_r, 10.5, 600, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
        hits.push_with_tip("crown:media", RectF::new(eq_x - 2.0, r.y, 140.0, r.h), UiAction::ToggleDeck("control".into()), "Medya & Kontrol Merkezi");
        x += 122.0;
    }

    // GIF Recording pulsing badge
    if state.gif_recording {
        let rec_r = RectF::new(x, cy - 10.0, 68.0, 20.0);
        p.fill_rounded_rect(rec_r, 10.0, t.danger.with_alpha(0.22));
        p.stroke_rounded_rect(rec_r, 10.0, t.danger, 1.0, false);
        p.fill_circle(rec_r.x + 10.0, cy, 3.5, t.danger);
        let lbl = format!("REC {}s", state.gif_elapsed_secs);
        p.draw_text(&lbl, RectF::new(rec_r.x + 16.0, rec_r.y, 48.0, rec_r.h), 9.5, 700, t.danger, FontFamily::Mono, TextAlign::Left, true, false);
        hits.push("crown:stop-gif", rec_r, UiAction::ToggleGifRecorder);
        x += 74.0;
    }

    // Live Camera / Microphone Privacy LEDs
    if has_privacy {
        if state.privacy.camera_active {
            p.fill_circle(x + 6.0, cy, 4.0, t.success);
            p.fill_radial_glow(x + 6.0, cy, 9.0, 9.0, t.success.with_alpha(0.5));
            x += 16.0;
        }
        if state.privacy.mic_active {
            p.fill_circle(x + 6.0, cy, 4.0, t.warning);
            p.fill_radial_glow(x + 6.0, cy, 9.0, 9.0, t.warning.with_alpha(0.5));
            x += 16.0;
        }
        x += 4.0;
    }

    // Low Bluetooth Battery warning chip
    if let Some(bt) = low_bt {
        let btr = RectF::new(x, cy - 10.0, 48.0, 20.0);
        p.fill_rounded_rect(btr, 10.0, t.danger.with_alpha(0.18));
        p.draw_icon(IconKind::Bluetooth, btr.x + 9.0, cy, 11.0, t.danger);
        p.draw_text(&format!("{}%", bt.battery_percent), RectF::new(btr.x + 15.0, btr.y, 30.0, btr.h), 9.5, 700, t.danger, FontFamily::Mono, TextAlign::Center, true, false);
        hits.push_with_tip("crown:bt", btr, UiAction::ToggleDeck("control".into()), format!("Düşük Pil: {}", bt.name));
        x += 54.0;
    }

    // Notification counter chip
    if has_notif {
        let nr = RectF::new(x, cy - 10.0, 34.0, 20.0);
        p.fill_rounded_rect(nr, 10.0, t.accent_tint);
        p.draw_icon(IconKind::Bell, nr.x + 10.0, cy, 10.5, t.accent1);
        p.draw_text(&state.notifications.len().min(99).to_string(), RectF::new(nr.x + 16.0, nr.y, 16.0, nr.h), 9.5, 700, t.accent1, FontFamily::Mono, TextAlign::Center, true, false);
        hits.push_with_tip("crown:notif", nr, UiAction::ToggleNotif, "Bildirim Merkezi");
    }

    // Right expand chevron button
    let exp_r = RectF::new(r.right() - 28.0, cy - 11.0, 22.0, 22.0);
    if hovered == Some("crown:toggle") {
        p.fill_rounded_rect(exp_r, 8.0, t.card_hover);
    }
    p.draw_icon(if state.crown_expanded { IconKind::ChevronUp } else { IconKind::ChevronDown }, exp_r.center_x(), cy, 12.0, t.text_secondary);
    hits.push("crown:toggle", RectF::new(r.right() - 34.0, r.y, 34.0, r.h), UiAction::ToggleDeck(state.active_deck_tab.clone()));
}

fn paint_bento_deck(p: &Painter<'_>, hits: &mut HitTree, state: &ShellState, canvas_w: f32) {
    let t = &state.theme;
    let hovered = state.hovered_id.as_deref();

    let deck_w = 900.0f32.min(canvas_w - 36.0);
    let deck_h = 548.0;
    let deck_x = (canvas_w - deck_w) * 0.5;
    let deck_y = 46.0;
    let deck_r = RectF::new(deck_x, deck_y, deck_w, deck_h);

    ThemePainter::draw_shell(p, deck_r, t, false);
    hits.push_shell(deck_r);

    // ── Top Header Row: Brand + Workspace Profiles + Action Buttons ──
    let hdr_y = deck_r.y + 12.0;
    let brand_x = deck_r.x + 16.0;
    p.fill_circle(brand_x + 7.0, hdr_y + 14.0, 6.0, t.accent1);
    p.fill_radial_glow(brand_x + 7.0, hdr_y + 14.0, 14.0, 14.0, t.accent_glow);
    p.draw_text(
        "PROJECT AURORA",
        RectF::new(brand_x + 18.0, hdr_y, 132.0, 28.0),
        12.0,
        800,
        t.text_primary,
        if t.style.uses_monospace_headers() { FontFamily::Mono } else { FontFamily::Ui },
        TextAlign::Left,
        true,
        false,
    );
    let ver_r = RectF::new(brand_x + 144.0, hdr_y + 5.0, 46.0, 18.0);
    p.fill_rounded_rect(ver_r, 9.0, t.accent_soft);
    p.draw_text("v3.0", ver_r, 9.5, 700, t.accent1, FontFamily::Mono, TextAlign::Center, true, false);

    // Workspace Profile quick-switch pills (Kişisel / İş / Odak / Oyun)
    let mut px = brand_x + 206.0;
    for prof in &state.profiles {
        let pw = (p.measure_text_width(&prof.name, 10.5, 600, FontFamily::Ui) + 20.0).clamp(52.0, 82.0);
        let pr = RectF::new(px, hdr_y + 2.0, pw, 24.0);
        let active = prof.id == state.active_profile_id;
        let hid = format!("prof:{}", prof.id);
        let is_hov = hovered == Some(hid.as_str());
        p.fill_rounded_rect(
            pr,
            t.radius_btn.min(12.0),
            if active { t.accent_tint } else if is_hov { t.card_hover } else { t.card_bg },
        );
        p.stroke_rounded_rect(pr, t.radius_btn.min(12.0), if active { t.accent_ring } else { t.border_subtle }, 1.0, false);
        p.draw_text(
            &prof.name,
            pr,
            10.5,
            if active { 700 } else { 500 },
            if active { t.accent1 } else { t.text_secondary },
            FontFamily::Ui,
            TextAlign::Center,
            true,
            false,
        );
        hits.push(hid, pr, UiAction::SelectProfile(prof.id.clone()));
        px += pw + 6.0;
    }

    // Right header quick tools: OmniSearch, Tour, Pin Deck, Close
    let mut rx = deck_r.right() - 16.0;
    draw_micro_button(
        p, hits, "deck:close", RectF::new(rx - 28.0, hdr_y, 28.0, 28.0),
        IconKind::Close, t, false, true, hovered == Some("deck:close"),
        UiAction::CloseOverlays, Some("Kapat (Esc)"),
    );
    rx -= 34.0;

    draw_micro_button(
        p, hits, "deck:pin", RectF::new(rx - 28.0, hdr_y, 28.0, 28.0),
        IconKind::Pin, t, state.prefs.crown_pinned, false, hovered == Some("deck:pin"),
        UiAction::ToggleCrownPin, Some("Paneli Sabitle"),
    );
    rx -= 34.0;

    draw_micro_button(
        p, hits, "deck:tour", RectF::new(rx - 28.0, hdr_y, 28.0, 28.0),
        IconKind::Sparkles, t, false, false, hovered == Some("deck:tour"),
        UiAction::OpenTour(0), Some("6 Adımlı Özellik Turu"),
    );
    rx -= 34.0;

    draw_micro_button(
        p, hits, "deck:omni", RectF::new(rx - 28.0, hdr_y, 28.0, 28.0),
        IconKind::Search, t, false, false, hovered == Some("deck:omni"),
        UiAction::ToggleOmni, Some("OmniSearch (Alt+Space)"),
    );

    // ── Theme-Aware 4-Tab Bar ──
    let tab_bar_r = RectF::new(deck_r.x + 16.0, hdr_y + 36.0, deck_r.w - 32.0, 34.0);
    let tabs = [
        ("control", "Kontrol & Donanım", IconKind::Sliders),
        ("shelf", "Süper Raf & Pano", IconKind::Layers),
        ("metrics", "Canlı Telemetri", IconKind::Activity),
        ("studio", "Stüdyo & Ayarlar", IconKind::Palette),
    ];
    draw_tab_bar(p, hits, tab_bar_r, &tabs, &state.active_deck_tab, hovered, t);

    // ── Main Content Viewport ──
    let body_r = RectF::new(deck_r.x + 16.0, tab_bar_r.bottom() + 12.0, deck_r.w - 32.0, deck_r.bottom() - tab_bar_r.bottom() - 26.0);
    match state.active_deck_tab.as_str() {
        "shelf" => paint_tab_shelf(p, hits, state, body_r),
        "metrics" => paint_tab_metrics(p, hits, state, body_r),
        "studio" => paint_tab_studio(p, hits, state, body_r),
        _ => paint_tab_control(p, hits, state, body_r),
    }
}
