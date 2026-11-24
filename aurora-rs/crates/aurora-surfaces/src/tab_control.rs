use aurora_render::{FontFamily, IconKind, Painter, RectF, TextAlign};
use aurora_ui::{draw_micro_button, draw_pill_button, draw_slider, draw_toggle_tile, HitTree, ThemePainter, UiAction};

use crate::ShellState;

/// Tab 1: Kontrol & Donanım — Media Hero + EQ, Master Volume + Output Selector + Per-App Audio Mixer,
/// External DDC/CI & Internal Monitor Brightness, Bluetooth Device Batteries, Privacy LEDs,
/// Quick Toggles (Wi-Fi/BT/Odak/Uyanık Tut/Mikrofon/Pomodoro), and 2-Step Armed Power Row.
pub fn paint_tab_control(p: &Painter<'_>, hits: &mut HitTree, state: &ShellState, r: RectF) {
    let t = &state.theme;
    let hovered = state.hovered_id.as_deref();
    let gap = 12.0;
    let left_w = (r.w - gap) * 0.49;
    let right_w = r.w - gap - left_w;
    let lx = r.x;
    let rx = r.x + left_w + gap;

    // ═══════════ LEFT COLUMN ═══════════
    // 1. Media Hero Card
    let media_r = RectF::new(lx, r.y, left_w, 108.0);
    ThemePainter::draw_card(p, media_r, "Şu An Çalıyor", t);
    let art_r = RectF::new(media_r.x + 12.0, media_r.y + 32.0, 62.0, 62.0);
    if !p.draw_cached_icon("media-art", art_r, 10.0) {
        p.fill_linear_gradient_rounded(art_r, 10.0, t.accent1.with_alpha(0.45), t.accent2.with_alpha(0.35), true);
        p.stroke_rounded_rect(art_r, 10.0, t.border_rim, 1.0, false);
        p.draw_icon(IconKind::Headphones, art_r.center_x(), art_r.center_y(), 24.0, t.text_primary);
    }

    let info_x = art_r.right() + 12.0;
    let info_w = media_r.right() - info_x - 108.0;
    let song_title = if state.media.has_session && !state.media.title.is_empty() {
        state.media.title.as_str()
    } else {
        "Aktif medya oynatılmıyor"
    };
    let song_sub = if state.media.has_session && !state.media.artist.is_empty() {
        format!("{} • {}", state.media.artist, state.media.app_name)
    } else {
        "Spotify, YouTube veya tarayıcı hazır".into()
    };
    p.draw_text(song_title, RectF::new(info_x, media_r.y + 34.0, info_w, 20.0), 13.0, 700, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
    p.draw_text(&song_sub, RectF::new(info_x, media_r.y + 54.0, info_w, 16.0), 10.5, 500, t.text_secondary, FontFamily::Ui, TextAlign::Left, true, false);

    // Live 5-bar GPU Equalizer indicator
    for i in 0..5 {
        let phase = state.anim.eq_phase + i as f32 * 0.9;
        let bh = if state.media.is_playing { 4.0 + ((phase.sin() * 0.5 + 0.5) * 11.0) } else { 3.0 };
        p.fill_rounded_rect(RectF::new(info_x + i as f32 * 6.0, media_r.y + 88.0 - bh, 3.5, bh), 1.5, t.accent1);
    }

    // Transport Controls (Prev / Play-Pause / Next)
    let tr_x = media_r.right() - 104.0;
    let tr_y = media_r.y + 46.0;
    draw_micro_button(p, hits, "med:prev", RectF::new(tr_x, tr_y, 28.0, 28.0), IconKind::Prev, t, false, false, hovered == Some("med:prev"), UiAction::MediaPrev, Some("Önceki"));
    draw_micro_button(p, hits, "med:pp", RectF::new(tr_x + 33.0, tr_y - 2.0, 32.0, 32.0), if state.media.is_playing { IconKind::Pause } else { IconKind::Play }, t, true, false, hovered == Some("med:pp"), UiAction::MediaPlayPause, Some("Oynat / Duraklat"));
    draw_micro_button(p, hits, "med:next", RectF::new(tr_x + 70.0, tr_y, 28.0, 28.0), IconKind::Next, t, false, false, hovered == Some("med:next"), UiAction::MediaNext, Some("Sonraki"));

    // 2. Master Audio + Output Switcher + Per-App Audio Mixer Card
    let audio_r = RectF::new(lx, media_r.bottom() + gap, left_w, r.bottom() - media_r.bottom() - gap);
    ThemePainter::draw_card(p, audio_r, "Ses Kontrolü & Uygulama Mikseri", t);

    let mut ay = audio_r.y + 30.0;
    // Master Volume Slider + Mute + Mic Mute buttons
    let mute_r = RectF::new(audio_r.x + 12.0, ay + 6.0, 28.0, 28.0);
    draw_micro_button(
        p, hits, "aud:mute", mute_r,
        if state.audio.is_master_muted { IconKind::Mute } else { IconKind::Volume },
        t, state.audio.is_master_muted, state.audio.is_master_muted, hovered == Some("aud:mute"),
        UiAction::ToggleMasterMute, Some("Sesi Kapat / Aç"),
    );
    let sl_r = RectF::new(mute_r.right() + 10.0, ay, audio_r.w - 94.0, 34.0);
    draw_slider(
        p,
        hits,
        "sl:master",
        sl_r,
        if state.audio.default_output_name.is_empty() { "Ana Ses Çıkışı" } else { &state.audio.default_output_name },
        &if state.audio.is_master_muted { "SESSİZ".into() } else { format!("{}%", state.audio.master_volume_percent) },
        state.audio.master_volume_percent as f32 / 100.0,
        None,
        t,
        hovered == Some("sl:master"),
        "master-vol",
    );
    let mic_r = RectF::new(audio_r.right() - 40.0, ay + 6.0, 28.0, 28.0);
    draw_micro_button(
        p, hits, "aud:mic", mic_r,
        if state.audio.is_mic_muted { IconKind::MicOff } else { IconKind::Mic },
        t, state.audio.is_mic_muted, state.audio.is_mic_muted, hovered == Some("aud:mic"),
        UiAction::ToggleMicMute, Some("Mikrofonu Kapat / Aç"),
    );
    ay += 42.0;

    // Audio Output device selector chips
    if !state.audio.outputs.is_empty() {
        let mut ox = audio_r.x + 12.0;
        let max_ox = audio_r.right() - 12.0;
        for out in state.audio.outputs.iter().take(3) {
            let short = if out.name.chars().count() > 16 {
                format!("{}…", out.name.chars().take(15).collect::<String>())
            } else {
                out.name.clone()
            };
            let cw = (max_ox - ox).clamp(72.0, 132.0);
            if ox + cw > max_ox + 2.0 {
                break;
            }
            let cr = RectF::new(ox, ay, cw, 24.0);
            let hid = format!("out:{}", out.id);
            draw_pill_button(p, hits, &hid, cr, &short, Some(IconKind::Headphones), t, out.is_default, hovered == Some(hid.as_str()), UiAction::SelectAudioOutput(out.id.clone()));
            ox += cw + 6.0;
        }
        ay += 32.0;
    }

    // Divider before Per-App Audio Mixer
    p.draw_line(audio_r.x + 12.0, ay, audio_r.right() - 12.0, ay, t.border_subtle, 1.0, false);
    ay += 8.0;
    p.draw_text("UYGULAMA SES MİKSERİ", RectF::new(audio_r.x + 12.0, ay, 200.0, 14.0), 9.5, 700, t.text_secondary, FontFamily::Ui, TextAlign::Left, true, false);
    ay += 18.0;

    if state.audio.app_sessions.is_empty() {
        p.draw_text("Şu anda aktif ses çıkışı kullanan uygulama yok.", RectF::new(audio_r.x + 12.0, ay + 12.0, audio_r.w - 24.0, 20.0), 11.0, 500, t.text_secondary, FontFamily::Ui, TextAlign::Left, true, false);
    } else {
        for sess in state.audio.app_sessions.iter().take(4) {
            if ay + 34.0 > audio_r.bottom() - 8.0 {
                break;
            }
            let sid = format!("sl:app:{}", sess.process_id);
            let sr = RectF::new(audio_r.x + 12.0, ay, audio_r.w - 24.0, 32.0);
            draw_slider(
                p,
                hits,
                &sid,
                sr,
                &sess.display_name,
                &format!("{}%", sess.volume_percent),
                sess.volume_percent as f32 / 100.0,
                Some(IconKind::Volume),
                t,
                hovered == Some(sid.as_str()),
                &format!("app-vol:{}", sess.process_id),
            );
            ay += 36.0;
        }
    }

    // ═══════════ RIGHT COLUMN ═══════════
    // 3. External DDC/CI & Internal Monitor Brightness Card
    let mon_count = state.monitors_brightness.len().clamp(1, 3);
    let mon_h = 34.0 + mon_count as f32 * 36.0;
    let mon_r = RectF::new(rx, r.y, right_w, mon_h);
    ThemePainter::draw_card(p, mon_r, "Monitör Parlaklığı (DDC/CI + Dahili)", t);
    let mut my = mon_r.y + 30.0;
    if state.monitors_brightness.is_empty() {
        p.draw_text("Monitörler algılanıyor (EDID + I2C)...", RectF::new(mon_r.x + 12.0, my, mon_r.w - 24.0, 22.0), 11.0, 500, t.text_secondary, FontFamily::Ui, TextAlign::Left, true, false);
    } else {
        for m in state.monitors_brightness.iter().take(3) {
            let sid = format!("sl:mon:{}", m.id);
            draw_slider(
                p,
                hits,
                &sid,
                RectF::new(mon_r.x + 12.0, my, mon_r.w - 24.0, 32.0),
                &m.name,
                &format!("{}%", m.brightness_percent),
                m.brightness_percent as f32 / 100.0,
                Some(if m.is_internal { IconKind::Sun } else { IconKind::Monitor }),
                t,
                hovered == Some(sid.as_str()),
                &format!("mon-bri:{}", m.id),
            );
            my += 35.0;
        }
    }

    // 4. Bluetooth Batteries & Privacy Guardian Strip
    let bt_r = RectF::new(rx, mon_r.bottom() + 10.0, right_w, 72.0);
    ThemePainter::draw_card(p, bt_r, "Bluetooth Piller & Gizlilik Koruması", t);
    let mut bx = bt_r.x + 12.0;
    let by = bt_r.y + 31.0;

    // Privacy camera/mic status chip
    let priv_w = 104.0;
    let priv_r = RectF::new(bx, by, priv_w, 30.0);
    let priv_on = state.privacy.camera_active || state.privacy.mic_active;
    p.fill_rounded_rect(priv_r, 8.0, if priv_on { t.warning.with_alpha(0.16) } else { t.inset_bg });
    p.stroke_rounded_rect(priv_r, 8.0, if priv_on { t.warning } else { t.border_subtle }, 1.0, false);
    p.fill_circle(priv_r.x + 11.0, priv_r.center_y(), 4.0, if state.privacy.camera_active { t.success } else if state.privacy.mic_active { t.warning } else { t.text_secondary.with_alpha(0.4) });
    let priv_lbl = if state.privacy.camera_active {
        "Kamera Aktif"
    } else if let Some(app) = state.privacy.mic_apps.first() {
        app.as_str()
    } else {
        "Gizlilik Güvenli"
    };
    p.draw_text(priv_lbl, RectF::new(priv_r.x + 20.0, priv_r.y, priv_w - 24.0, priv_r.h), 10.0, 600, if priv_on { t.warning } else { t.text_secondary }, FontFamily::Ui, TextAlign::Left, true, false);
    bx += priv_w + 8.0;

    for dev in state.bluetooth_devices.iter().take(2) {
        let remain = bt_r.right() - bx - 12.0;
        let dw = (remain / 2.0 - 4.0).clamp(96.0, 146.0);
        if bx + dw > bt_r.right() - 8.0 {
            break;
        }
        let dr = RectF::new(bx, by, dw, 30.0);
        p.fill_rounded_rect(dr, 8.0, if dev.is_low { t.danger.with_alpha(0.16) } else { t.inset_bg });
        p.stroke_rounded_rect(dr, 8.0, if dev.is_low { t.danger } else { t.border_subtle }, 1.0, false);
        let ic = match dev.category.as_str() {
            "headset" => IconKind::Headphones,
            "mouse" => IconKind::Mouse,
            "keyboard" => IconKind::Keyboard,
            _ => IconKind::Bluetooth,
        };
        let col = if dev.is_low { t.danger } else { t.accent1 };
        p.draw_icon(ic, dr.x + 12.0, dr.center_y(), 12.0, col);
        let short_n = if dev.name.chars().count() > 11 {
            format!("{}…", dev.name.chars().take(10).collect::<String>())
        } else {
            dev.name.clone()
        };
        p.draw_text(&short_n, RectF::new(dr.x + 22.0, dr.y, dw - 52.0, dr.h), 9.8, 600, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
        p.draw_text(&format!("{}%", dev.battery_percent), RectF::new(dr.right() - 32.0, dr.y, 28.0, dr.h), 10.0, 700, col, FontFamily::Mono, TextAlign::Right, true, false);
        bx += dw + 6.0;
    }

    // 5. Quick Toggles 2×3 Grid
    let tog_y = bt_r.bottom() + 10.0;
    let tog_w = (right_w - 8.0) * 0.5;
    let tog_h = 46.0;
    let awake_sub = match state.awake_minutes_remaining {
        None => "Kapalı".into(),
        Some(0) => "Süresiz Açık".into(),
        Some(m) => format!("{m} dk kaldı"),
    };
    let pom_m = state.focus_timer_seconds / 60;
    let pom_s = state.focus_timer_seconds % 60;
    let pom_sub = if state.focus_timer_running {
        format!("{pom_m:02}:{pom_s:02} aktif")
    } else {
        format!("{pom_m:02}:{pom_s:02} hazır")
    };

    let tiles = [
        ("tog:wifi", IconKind::Wifi, "Wi-Fi", if state.wifi_enabled { "Bağlı" } else { "Kapalı" }.to_string(), state.wifi_enabled, UiAction::ToggleWifi),
        ("tog:bt", IconKind::Bluetooth, "Bluetooth", if state.bluetooth_enabled { "Aktif" } else { "Kapalı" }.to_string(), state.bluetooth_enabled, UiAction::ToggleBluetooth),
        ("tog:focus", IconKind::Moon, "Odak Modu", if state.focus_mode_enabled { "Bildirimler Sessiz" } else { "Normal Akış" }.to_string(), state.focus_mode_enabled, UiAction::ToggleFocusMode),
        ("tog:awake", IconKind::Coffee, "Uyanık Tut", awake_sub, state.awake_minutes_remaining.is_some(), UiAction::CycleAwake),
        ("tog:pom", IconKind::Clock, "Pomodoro", pom_sub, state.focus_timer_running, UiAction::ToggleFocusTimer),
        ("tog:vdesk", IconKind::Grid, "Sanal Masaüstü", format!("M{}/{}", state.virtual_desktop_index, state.virtual_desktop_count), false, UiAction::SwitchVirtualDesktop(1)),
    ];

    for (idx, (id, ic, title, sub, on, act)) in tiles.into_iter().enumerate() {
        let col = (idx % 2) as f32;
        let row = (idx / 2) as f32;
        let tr = RectF::new(rx + col * (tog_w + 8.0), tog_y + row * (tog_h + 7.0), tog_w, tog_h);
        if tr.bottom() <= r.bottom() - 42.0 {
            draw_toggle_tile(p, hits, id, tr, ic, title, &sub, on, hovered == Some(id), t, act);
        }
    }

    // 6. Power Action Row (2-Step Armed Safety, proportional widths so monospace never clips!)
    let pow_y = r.bottom() - 34.0;
    let pow_btns = [
        ("pow:lock", "Kilitle", IconKind::Lock, "lock", 0.22f32),
        ("pow:sleep", "Uyku", IconKind::Sleep, "sleep", 0.21f32),
        ("pow:restart", "Yeniden Başlat", IconKind::Restart, "restart", 0.35f32),
        ("pow:shutdown", "Kapat", IconKind::Power, "shutdown", 0.22f32),
    ];
    let avail_pw = right_w - 18.0;
    let mut px = rx;
    for (id, lbl, ic, cmd, frac) in pow_btns {
        let pw = avail_pw * frac;
        let pr = RectF::new(px, pow_y, pw, 30.0);
        let armed = state.armed_power.as_deref() == Some(cmd);
        let text = if armed { "Onayla?" } else { lbl };
        draw_pill_button(p, hits, id, pr, text, Some(ic), t, armed, hovered == Some(id), UiAction::PowerAction(cmd.into()));
        px += pw + 6.0;
    }
}
