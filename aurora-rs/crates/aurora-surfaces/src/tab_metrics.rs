use aurora_render::{FontFamily, IconKind, Painter, RectF, TextAlign};
use aurora_ui::{draw_micro_button, draw_pill_button, HitTree, ThemePainter, UiAction};

use crate::ShellState;

/// Tab 3: Canlı Telemetri — Live CPU/RAM/Network/Battery gauges, Direct2D filled Sparklines,
/// Top-3 RAM Processes with 2-click armed safe terminate, and Clickable Live Notifications History!
pub fn paint_tab_metrics(p: &Painter<'_>, hits: &mut HitTree, state: &ShellState, r: RectF) {
    let t = &state.theme;
    let hovered = state.hovered_id.as_deref();
    let gap = 12.0;

    // ── Top 4 KPI Stat Cards ──
    let kpi_h = 84.0;
    let kw = (r.w - gap * 3.0) / 4.0;
    let kpis = [
        ("İŞLEMCİ (CPU)", format!("{:.1}%", state.metrics.cpu_percent), "Düşük gecikme • Rust Native".to_string(), t.accent1, IconKind::Activity),
        ("BELLEK (RAM)", format!("{:.1}%", state.metrics.ram_percent), format!("{:.1} / {:.0} GB", state.metrics.ram_used_gb, state.metrics.ram_total_gb), t.accent2, IconKind::Layers),
        ("AĞ AKIŞI", format!("{:.1} Mb/s", state.metrics.net_down_mbps), format!("↑ {:.1} Mb/s gönderim", state.metrics.net_up_mbps), t.success, IconKind::Globe),
        (
            "BATARYA",
            if state.metrics.battery_present { format!("{}%", state.metrics.battery_percent) } else { "AC Güç".into() },
            if state.metrics.is_charging { "Şarj Oluyor ⚡".into() } else if state.metrics.battery_present { "Pil Modu".into() } else { "Masaüstü".into() },
            if state.metrics.battery_present && state.metrics.battery_percent <= 20 { t.danger } else { t.warning },
            IconKind::Battery,
        ),
    ];

    for (i, (lbl, val, sub, accent, ic)) in kpis.into_iter().enumerate() {
        let kr = RectF::new(r.x + i as f32 * (kw + gap), r.y, kw, kpi_h);
        ThemePainter::draw_card(p, kr, "", t);
        p.draw_icon(ic, kr.x + 18.0, kr.y + 20.0, 14.0, accent);
        p.draw_text(lbl, RectF::new(kr.x + 30.0, kr.y + 12.0, kr.w - 40.0, 16.0), 9.5, 700, t.text_secondary, FontFamily::Ui, TextAlign::Left, true, false);
        p.draw_text(&val, RectF::new(kr.x + 14.0, kr.y + 32.0, kr.w - 28.0, 26.0), 21.0, 800, t.text_primary, FontFamily::Mono, TextAlign::Left, true, false);
        p.draw_text(&sub, RectF::new(kr.x + 14.0, kr.y + 60.0, kr.w - 28.0, 14.0), 9.5, 500, t.text_secondary, FontFamily::Ui, TextAlign::Left, true, false);
    }

    // ── Bottom Split: Left (Sparklines + Top-3 Processes), Right (Clickable Live Notifications) ──
    let bot_y = r.y + kpi_h + gap;
    let bot_h = r.bottom() - bot_y;
    let left_w = (r.w - gap) * 0.54;
    let right_w = r.w - gap - left_w;

    // 1. CPU & RAM Real-Time Direct2D Sparklines
    let spark_h = 148.0;
    let spark_r = RectF::new(r.x, bot_y, left_w, spark_h);
    ThemePainter::draw_card(p, spark_r, "Gerçek Zamanlı CPU & RAM Grafiği", t);

    let half_w = (spark_r.w - 36.0) * 0.5;
    let cpu_box = RectF::new(spark_r.x + 12.0, spark_r.y + 32.0, half_w, spark_h - 44.0);
    p.fill_rounded_rect(cpu_box, 8.0, t.inset_bg);
    p.draw_sparkline(&state.cpu_history, cpu_box.inset(6.0, 18.0), t.accent1, 0.30);
    p.draw_text(&format!("CPU  {:.1}%", state.metrics.cpu_percent), RectF::new(cpu_box.x + 8.0, cpu_box.y + 5.0, cpu_box.w - 16.0, 15.0), 9.8, 700, t.accent1, FontFamily::Mono, TextAlign::Left, true, false);

    let ram_box = RectF::new(cpu_box.right() + 12.0, spark_r.y + 32.0, half_w, spark_h - 44.0);
    p.fill_rounded_rect(ram_box, 8.0, t.inset_bg);
    p.draw_sparkline(&state.ram_history, ram_box.inset(6.0, 18.0), t.accent2, 0.28);
    p.draw_text(&format!("RAM  {:.1}%", state.metrics.ram_percent), RectF::new(ram_box.x + 8.0, ram_box.y + 5.0, ram_box.w - 16.0, 15.0), 9.8, 700, t.accent2, FontFamily::Mono, TextAlign::Left, true, false);

    // 2. Top-3 RAM Processes with 2-click armed safe terminate
    let proc_r = RectF::new(r.x, spark_r.bottom() + gap, left_w, bot_h - spark_h - gap);
    ThemePainter::draw_card(p, proc_r, "En Çok Bellek Kullanan İşlemler (Güvenli Sonlandır)", t);
    let mut py = proc_r.y + 32.0;
    for proc_info in state.metrics.top_processes.iter().take(3) {
        if py + 36.0 > proc_r.bottom() - 4.0 {
            break;
        }
        let row = RectF::new(proc_r.x + 12.0, py, proc_r.w - 24.0, 34.0);
        p.fill_rounded_rect(row, 8.0, t.inset_bg);
        p.stroke_rounded_rect(row, 8.0, t.border_subtle, 1.0, false);
        p.draw_text(&proc_info.name, RectF::new(row.x + 10.0, row.y, row.w - 178.0, row.h), 11.0, 600, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
        p.draw_text(&format!("{} MB", proc_info.memory_mb), RectF::new(row.right() - 166.0, row.y, 70.0, row.h), 10.5, 700, t.accent1, FontFamily::Mono, TextAlign::Right, true, false);

        let armed = state.armed_kill_pid == Some(proc_info.pid);
        let kid = format!("kill:{}", proc_info.pid);
        draw_pill_button(
            p, hits, &kid, RectF::new(row.right() - 88.0, row.y + 4.0, 82.0, 26.0),
            if armed { "Emin misin?" } else { "Sonlandır" }, None, t, armed, hovered == Some(kid.as_str()),
            UiAction::KillTopProcess { pid: proc_info.pid, name: proc_info.name.clone() },
        );
        py += 40.0;
    }

    // 3. Clickable Live Notifications History
    let notif_r = RectF::new(r.x + left_w + gap, bot_y, right_w, bot_h);
    ThemePainter::draw_card(p, notif_r, "Canlı Bildirim Geçmişi (Tıkla & Aç)", t);
    if !state.notifications.is_empty() {
        draw_pill_button(
            p, hits, "notif:clear-all", RectF::new(notif_r.right() - 82.0, notif_r.y + 5.0, 72.0, 20.0),
            "Temizle", None, t, false, hovered == Some("notif:clear-all"),
            UiAction::ClearAllNotifications,
        );
    }

    let mut ny = notif_r.y + 32.0;
    if state.notifications.is_empty() {
        p.draw_icon(IconKind::Bell, notif_r.center_x(), notif_r.center_y() - 10.0, 26.0, t.text_secondary.with_alpha(0.5));
        p.draw_text("Yeni sistem bildirimi yok", RectF::new(notif_r.x + 12.0, notif_r.center_y() + 12.0, notif_r.w - 24.0, 20.0), 11.0, 500, t.text_secondary, FontFamily::Ui, TextAlign::Center, true, false);
    } else {
        for n in state.notifications.iter().take(4) {
            if ny + 64.0 > notif_r.bottom() - 8.0 {
                break;
            }
            let nr = RectF::new(notif_r.x + 12.0, ny, notif_r.w - 24.0, 62.0);
            let nid = format!("notif:act:{}", n.notification_id);
            let is_hov = hovered == Some(nid.as_str());
            p.fill_rounded_rect(nr, 10.0, if is_hov { t.card_hover } else { t.inset_bg });
            p.stroke_rounded_rect(nr, 10.0, if is_hov { t.accent_ring } else { t.border_subtle }, 1.0, false);

            let hdr = format!("{}  •  {}", n.app_name, n.arrived_at_utc);
            p.draw_text(&hdr, RectF::new(nr.x + 10.0, nr.y + 6.0, nr.w - 76.0, 14.0), 9.2, 700, t.accent1, FontFamily::Ui, TextAlign::Left, true, false);
            p.draw_text(&n.title, RectF::new(nr.x + 10.0, nr.y + 21.0, nr.w - 76.0, 16.0), 11.0, 700, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
            p.draw_text(&n.body, RectF::new(nr.x + 10.0, nr.y + 39.0, nr.w - 76.0, 16.0), 10.0, 500, t.text_secondary, FontFamily::Ui, TextAlign::Left, true, false);

            hits.push_with_tip(nid, RectF::new(nr.x, nr.y, nr.w - 34.0, nr.h), UiAction::ActivateNotification(n.notification_id.to_string()), "Uygulamada Aç ↗");

            let did = format!("notif:dis:{}", n.notification_id);
            draw_micro_button(p, hits, &did, RectF::new(nr.right() - 30.0, nr.y + 18.0, 24.0, 24.0), IconKind::Close, t, false, true, hovered == Some(did.as_str()), UiAction::DismissNotification(n.notification_id.to_string()), Some("Kapat"));
            ny += 68.0;
        }
    }
}
