use aurora_core::Color;
use aurora_render::{FontFamily, IconKind, Painter, RectF, TextAlign};
use aurora_ui::{draw_micro_button, draw_pill_button, draw_text_input, HitTree, ThemePainter, UiAction};

use crate::ShellState;

/// Tab 2: Süper Raf & Pano — 3-Zone Power Productivity Hub:
/// 1. Drag-Drop File Shelf + ZIP Packager + Live Direct2D Local LAN QR AirDrop
/// 2. Persistent SQLite Clipboard Vault (Search + Filter Pills + Pin / Copy-Paste / Delete)
/// 3. Scratchpad + 1-Click Native Productivity Tools (WinRT OCR, Eyedropper, 10 FPS GIF Recorder)
pub fn paint_tab_shelf(p: &Painter<'_>, hits: &mut HitTree, state: &ShellState, r: RectF) {
    let t = &state.theme;
    let hovered = state.hovered_id.as_deref();
    let gap = 12.0;
    let left_w = (r.w - gap * 2.0) * 0.34;
    let mid_w = (r.w - gap * 2.0) * 0.40;
    let right_w = r.w - gap * 2.0 - left_w - mid_w;

    // ═══════════ ZONE 1: DROP SHELF & LOCAL LAN QR AIRDROP ═══════════
    let z1 = RectF::new(r.x, r.y, left_w, r.h);
    ThemePainter::draw_card(p, z1, "Dosya Rafı & LAN QR Paylaş", t);

    let mut y = z1.y + 30.0;
    // Active QR Share Card (pixel-exact Direct2D QR matrix!)
    if let Some(qr) = &state.qr_share {
        let qr_box = RectF::new(z1.x + 12.0, y, z1.w - 24.0, 146.0);
        p.fill_rounded_rect(qr_box, 10.0, t.inset_bg);
        p.stroke_rounded_rect(qr_box, 10.0, t.accent_ring, 1.2, false);

        // Crisp white quiet-zone card with pixel-exact QR modules
        let mat_r = RectF::new(qr_box.x + 10.0, qr_box.y + 10.0, 118.0, 118.0);
        p.fill_rounded_rect(mat_r, 8.0, Color::WHITE);
        p.draw_qr_matrix(&qr.modules, qr.matrix_size, mat_r.inset(7.0, 7.0), Color::BLACK, Color::WHITE);

        let tx = mat_r.right() + 10.0;
        let tw = (qr_box.right() - tx - 8.0).max(40.0);
        p.draw_text("QR AirDrop Aktif", RectF::new(tx, qr_box.y + 12.0, tw, 16.0), 11.0, 700, t.accent1, FontFamily::Ui, TextAlign::Left, true, false);
        p.draw_text(&qr.file_name, RectF::new(tx, qr_box.y + 30.0, tw, 16.0), 10.5, 600, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
        p.draw_text(&qr.size_label, RectF::new(tx, qr_box.y + 46.0, tw, 14.0), 9.5, 500, t.text_secondary, FontFamily::Mono, TextAlign::Left, true, false);
        p.draw_text(&qr.url, RectF::new(tx, qr_box.y + 64.0, tw, 30.0), 8.8, 500, t.text_secondary, FontFamily::Mono, TextAlign::Left, false, true);

        draw_pill_button(
            p, hits, "qr:stop", RectF::new(tx, qr_box.bottom() - 32.0, tw, 24.0),
            "Durdur", Some(IconKind::Close), t, false, hovered == Some("qr:stop"),
            UiAction::StopQrShare,
        );
        y = qr_box.bottom() + 10.0;
    }

    // Shelf file items list
    let list_bottom = z1.bottom() - 46.0;
    if state.drop_shelf_paths.is_empty() {
        let drop_zone = RectF::new(z1.x + 12.0, y, z1.w - 24.0, (list_bottom - y).max(80.0));
        p.fill_rounded_rect(drop_zone, 10.0, t.inset_bg);
        p.stroke_rounded_rect(drop_zone, 10.0, t.border_rim, 1.2, true);
        p.draw_icon(IconKind::Inbox, drop_zone.center_x(), drop_zone.center_y() - 14.0, 24.0, t.text_secondary);
        p.draw_text("Dosyaları buraya veya Dock'a sürükleyin", RectF::new(drop_zone.x + 10.0, drop_zone.center_y() + 6.0, drop_zone.w - 20.0, 32.0), 10.5, 500, t.text_secondary, FontFamily::Ui, TextAlign::Center, true, true);
    } else {
        for (idx, path) in state.drop_shelf_paths.iter().enumerate() {
            if y + 40.0 > list_bottom {
                break;
            }
            let file_name = std::path::Path::new(path)
                .file_name()
                .and_then(|s| s.to_str())
                .unwrap_or(path);
            let row = RectF::new(z1.x + 12.0, y, z1.w - 24.0, 36.0);
            let hid = format!("shelf:open:{idx}");
            let is_hov = hovered == Some(hid.as_str());
            p.fill_rounded_rect(row, 9.0, if is_hov { t.card_hover } else { t.inset_bg });
            p.stroke_rounded_rect(row, 9.0, t.border_subtle, 1.0, false);
            p.draw_icon(IconKind::Folder, row.x + 14.0, row.center_y(), 13.0, t.accent1);
            p.draw_text(file_name, RectF::new(row.x + 26.0, row.y, row.w - 92.0, row.h), 10.5, 600, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
            hits.push(hid, RectF::new(row.x, row.y, row.w - 64.0, row.h), UiAction::OpenShelfItem(path.clone()));

            let qrid = format!("shelf:qr:{idx}");
            draw_micro_button(p, hits, &qrid, RectF::new(row.right() - 58.0, row.y + 5.0, 26.0, 26.0), IconKind::Qr, t, false, false, hovered == Some(qrid.as_str()), UiAction::StartQrShare(path.clone()), Some("Telefona QR Paylaş"));
            let rmid = format!("shelf:rm:{idx}");
            draw_micro_button(p, hits, &rmid, RectF::new(row.right() - 29.0, row.y + 5.0, 26.0, 26.0), IconKind::Trash, t, false, true, hovered == Some(rmid.as_str()), UiAction::RemoveShelfItem(path.clone()), Some("Raftan Kaldır"));
            y += 42.0;
        }
    }

    // Bottom shelf actions: ZIP + Clear
    let bw = (z1.w - 30.0) * 0.5;
    draw_pill_button(p, hits, "shelf:zip", RectF::new(z1.x + 12.0, z1.bottom() - 38.0, bw, 28.0), "ZIP Yap", Some(IconKind::Zip), t, true, hovered == Some("shelf:zip"), UiAction::ZipShelf);
    draw_pill_button(p, hits, "shelf:clr", RectF::new(z1.x + 18.0 + bw, z1.bottom() - 38.0, bw, 28.0), "Temizle", Some(IconKind::Trash), t, false, hovered == Some("shelf:clr"), UiAction::ClearShelf);

    // ═══════════ ZONE 2: SQLITE CLIPBOARD VAULT ═══════════
    let z2 = RectF::new(z1.right() + gap, r.y, mid_w, r.h);
    ThemePainter::draw_card(p, z2, "Kalıcı Pano Kasası (SQLite)", t);

    // Search input
    let search_r = RectF::new(z2.x + 12.0, z2.y + 30.0, z2.w - 24.0, 30.0);
    draw_text_input(p, hits, "input:clip-search", search_r, &state.clipboard_search, t, Some(IconKind::Search));

    // Filter pills (Tümü / Sabit / Metin / Görsel) + Clear Unpinned
    let fy = search_r.bottom() + 8.0;
    let filters = [("all", "Tümü"), ("pinned", "Sabit"), ("text", "Metin"), ("image", "Görsel")];
    let mut fx = z2.x + 12.0;
    for (fid, flbl) in filters {
        let fw = 52.0;
        let fr = RectF::new(fx, fy, fw, 22.0);
        let hid = format!("clip:flt:{fid}");
        let on = state.clipboard_filter == fid;
        draw_pill_button(p, hits, &hid, fr, flbl, None, t, on, hovered == Some(hid.as_str()), UiAction::ClipboardFilter(fid.into()));
        fx += fw + 5.0;
    }
    draw_micro_button(
        p, hits, "clip:clr-unpinned", RectF::new(z2.right() - 36.0, fy - 1.0, 24.0, 24.0),
        IconKind::Trash, t, false, true, hovered == Some("clip:clr-unpinned"),
        UiAction::ClipboardClearUnpinned, Some("Sabitlenmemişleri Temizle"),
    );

    // Clipboard items list
    let mut cy = fy + 30.0;
    let q = state.clipboard_search.text.trim().to_ascii_lowercase();
    let filtered: Vec<_> = state
        .clipboard_items
        .iter()
        .filter(|it| match state.clipboard_filter.as_str() {
            "pinned" => it.is_pinned,
            "text" => it.kind == "text",
            "image" => it.kind == "image",
            _ => true,
        })
        .filter(|it| q.is_empty() || it.preview_text.to_ascii_lowercase().contains(&q) || it.source_app.to_ascii_lowercase().contains(&q))
        .collect();

    if filtered.is_empty() {
        p.draw_text("Pano kaydı bulunamadı.", RectF::new(z2.x + 12.0, cy + 20.0, z2.w - 24.0, 24.0), 11.0, 500, t.text_secondary, FontFamily::Ui, TextAlign::Center, true, false);
    } else {
        for item in filtered.into_iter().take(6) {
            if cy + 52.0 > z2.bottom() - 8.0 {
                break;
            }
            let row = RectF::new(z2.x + 12.0, cy, z2.w - 24.0, 48.0);
            let use_id = format!("clip:use:{}", item.id);
            let is_hov = hovered == Some(use_id.as_str());
            p.fill_rounded_rect(row, 9.0, if is_hov { t.card_hover } else { t.inset_bg });
            p.stroke_rounded_rect(row, 9.0, if item.is_pinned { t.accent_ring } else { t.border_subtle }, 1.0, false);

            let ic = if item.kind == "image" { IconKind::Camera } else { IconKind::Clip };
            p.draw_icon(ic, row.x + 14.0, row.center_y(), 13.0, if item.is_pinned { t.accent1 } else { t.text_secondary });

            p.draw_text(&item.preview_text, RectF::new(row.x + 28.0, row.y + 6.0, row.w - 92.0, 18.0), 10.8, 600, t.text_primary, FontFamily::Ui, TextAlign::Left, true, false);
            let meta = format!("{} • {}", item.source_app, item.last_copied_utc);
            p.draw_text(&meta, RectF::new(row.x + 28.0, row.y + 25.0, row.w - 92.0, 15.0), 9.2, 500, t.text_secondary, FontFamily::Ui, TextAlign::Left, true, false);

            hits.push_with_tip(use_id, RectF::new(row.x, row.y, row.w - 64.0, row.h), UiAction::ClipboardUse(item.id), "Panoya Kopyala / Yapıştır");

            let pin_id = format!("clip:pin:{}", item.id);
            draw_micro_button(p, hits, &pin_id, RectF::new(row.right() - 58.0, row.y + 11.0, 26.0, 26.0), IconKind::Pin, t, item.is_pinned, false, hovered == Some(pin_id.as_str()), UiAction::ClipboardPin(item.id), Some("Sabitle"));
            let del_id = format!("clip:del:{}", item.id);
            draw_micro_button(p, hits, &del_id, RectF::new(row.right() - 29.0, row.y + 11.0, 26.0, 26.0), IconKind::Trash, t, false, true, hovered == Some(del_id.as_str()), UiAction::ClipboardDelete(item.id), Some("Sil"));
            cy += 54.0;
        }
    }

    // ═══════════ ZONE 3: QUICK TOOLS (OCR / EYEDROPPER / GIF) + SCRATCHPAD ═══════════
    let z3_top = RectF::new(z2.right() + gap, r.y, right_w, 168.0);
    ThemePainter::draw_card(p, z3_top, "Hızlı Yakalama Araçları", t);

    let tools = [
        ("tool:ocr", "Ekran OCR (Alt+Shift+S)", IconKind::Scissors, false, UiAction::TriggerOcr),
        ("tool:eye", "Renk Pipeti (Alt+Shift+C)", IconKind::Pipette, false, UiAction::TriggerEyedropper),
        ("tool:gif", if state.gif_recording { "GIF Kaydı Durdur" } else { "GIF Kaydedici (10 FPS)" }, IconKind::Record, state.gif_recording, UiAction::ToggleGifRecorder),
    ];
    for (i, (id, lbl, ic, act_on, action)) in tools.into_iter().enumerate() {
        let tr = RectF::new(z3_top.x + 12.0, z3_top.y + 32.0 + i as f32 * 42.0, z3_top.w - 24.0, 34.0);
        draw_pill_button(p, hits, id, tr, lbl, Some(ic), t, act_on, hovered == Some(id), action);
    }

    // Scratchpad Card
    let z3_bot = RectF::new(z2.right() + gap, z3_top.bottom() + gap, right_w, r.bottom() - z3_top.bottom() - gap);
    ThemePainter::draw_card(p, z3_bot, "Karalama Defteri", t);
    let pad_r = RectF::new(z3_bot.x + 12.0, z3_bot.y + 30.0, z3_bot.w - 24.0, z3_bot.h - 42.0);
    p.fill_rounded_rect(pad_r, 9.0, t.inset_bg);
    p.stroke_rounded_rect(pad_r, 9.0, if state.scratchpad.focused { t.accent1 } else { t.border_subtle }, if state.scratchpad.focused { 1.4 } else { 1.0 }, false);
    let pad_txt = if state.scratchpad.text.is_empty() {
        &state.scratchpad.placeholder
    } else {
        &state.scratchpad.text
    };
    p.draw_text(
        pad_txt,
        pad_r.inset(10.0, 8.0),
        11.0,
        500,
        if state.scratchpad.text.is_empty() { t.text_secondary.with_alpha(0.75) } else { t.text_primary },
        FontFamily::Ui,
        TextAlign::Left,
        false,
        true,
    );
    hits.push("input:scratchpad", pad_r, UiAction::FocusTextInput("input:scratchpad".into()));
}
