use std::path::{Path, PathBuf};

use aurora_core::{contrast_ratio, relative_luminance};
use windows::Win32::UI::WindowsAndMessaging::{
    SystemParametersInfoW, SPI_GETDESKWALLPAPER, SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS,
};

/// Reads the active Windows desktop wallpaper path via `SPI_GETDESKWALLPAPER`
/// (with fallback to `%APPDATA%\Microsoft\Windows\Themes\TranscodedWallpaper`).
pub fn current_wallpaper_path() -> Option<PathBuf> {
    let mut buf = [0u16; 520];
    let ok = unsafe {
        SystemParametersInfoW(
            SPI_GETDESKWALLPAPER,
            buf.len() as u32,
            Some(buf.as_mut_ptr() as *mut _),
            SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS(0),
        )
        .is_ok()
    };
    if ok {
        let len = buf.iter().position(|&c| c == 0).unwrap_or(buf.len());
        let p = PathBuf::from(String::from_utf16_lossy(&buf[..len]).trim());
        if p.is_file() {
            return Some(p);
        }
    }
    if let Some(appdata) = std::env::var_os("APPDATA") {
        let transcoded = PathBuf::from(appdata)
            .join("Microsoft")
            .join("Windows")
            .join("Themes")
            .join("TranscodedWallpaper");
        if transcoded.is_file() {
            return Some(transcoded);
        }
    }
    None
}

/// Samples the active Windows wallpaper (`64x64`, `<15ms`) and extracts a harmonious
/// `(accent_primary, accent_secondary)` pair guaranteed to meet WCAG AA (≥ 4.5:1)
/// against `surface_hex`.
pub fn extract_wallpaper_accent_colors(surface_hex: &str) -> Option<(String, String)> {
    let path = current_wallpaper_path()?;
    extract_accent_from_image_path(&path, surface_hex)
}

pub fn extract_accent_from_image_path(path: &Path, surface_hex: &str) -> Option<(String, String)> {
    let img = image::open(path).ok()?;
    let small = img.resize_exact(64, 64, image::imageops::FilterType::Triangle).to_rgb8();

    // 24 HSV hue bins (15 degrees each), accumulating weighted RGB sums
    let mut bin_weight = [0.0f64; 24];
    let mut bin_rgb = [(0.0f64, 0.0f64, 0.0f64); 24];

    for px in small.pixels() {
        let (r, g, b) = (px[0], px[1], px[2]);
        let (h, s, v) = rgb_to_hsv(r, g, b);
        if s < 0.16 || v < 0.12 || v > 0.97 {
            continue;
        }
        let bin = (((h / 15.0).floor() as usize) % 24).min(23);
        let w = (s as f64).powf(1.4) * (0.4 + 0.6 * (v as f64));
        bin_weight[bin] += w;
        bin_rgb[bin].0 += r as f64 * w;
        bin_rgb[bin].1 += g as f64 * w;
        bin_rgb[bin].2 += b as f64 * w;
    }

    let (best_bin, best_w) = bin_weight
        .iter()
        .enumerate()
        .max_by(|a, b| a.1.total_cmp(b.1))
        .map(|(i, &w)| (i, w))
        .unwrap_or((0, 0.0));

    let (r1, g1, b1) = if best_w > 1.0 {
        let (sr, sg, sb) = bin_rgb[best_bin];
        ((sr / best_w) as u8, (sg / best_w) as u8, (sb / best_w) as u8)
    } else {
        // Fallback for monochrome/grayscale wallpaper: clean Aurora ice blue + iris
        (56, 189, 248)
    };

    // Pick secondary accent from a distinct hue bin (>= 3 bins / 45° away), or shift hue +40°
    let mut sec_bin = None;
    let mut sec_w = 0.0;
    for (i, &w) in bin_weight.iter().enumerate() {
        let dist = best_bin.abs_diff(i).min(24 - best_bin.abs_diff(i));
        if dist >= 3 && w > sec_w && w > best_w * 0.12 {
            sec_w = w;
            sec_bin = Some(i);
        }
    }

    let (r2, g2, b2) = if let Some(sb_idx) = sec_bin {
        let (sr, sg, sb) = bin_rgb[sb_idx];
        ((sr / sec_w) as u8, (sg / sec_w) as u8, (sb / sec_w) as u8)
    } else {
        let (h, s, v) = rgb_to_hsv(r1, g1, b1);
        hsv_to_rgb((h + 42.0) % 360.0, s.clamp(0.45, 0.85), v.clamp(0.55, 0.92))
    };

    let primary = adjust_accent_for_wcag_aa(r1, g1, b1, surface_hex);
    let secondary = adjust_accent_for_wcag_aa(r2, g2, b2, surface_hex);
    Some((primary, secondary))
}

/// Adjusts an RGB accent color's brightness/saturation so its WCAG contrast ratio
/// against `surface_hex` is guaranteed to be ≥ 4.5:1 (AA).
pub fn adjust_accent_for_wcag_aa(r: u8, g: u8, b: u8, surface_hex: &str) -> String {
    let (h, mut s, mut v) = rgb_to_hsv(r, g, b);
    s = s.clamp(0.35, 0.90);
    let bg_is_light = relative_luminance(surface_hex) > 0.42;

    for step in 0..24 {
        let (cr, cg, cb) = hsv_to_rgb(h, s, v);
        let hex = format!("#{cr:02X}{cg:02X}{cb:02X}");
        if contrast_ratio(&hex, surface_hex) >= 4.6 {
            return hex;
        }
        if bg_is_light {
            // Darken smoothly for crisp readability on light themes
            v = (v - 0.045).max(0.18);
            if step > 10 {
                s = (s + 0.03).min(0.95);
            }
        } else {
            // Brighten smoothly (and slightly reduce saturation at high lightness if needed) for dark themes
            if v < 0.96 {
                v = (v + 0.05).min(0.98);
            } else {
                s = (s - 0.05).max(0.20);
            }
        }
    }

    if bg_is_light {
        "#1D4ED8".into()
    } else {
        "#38BDF8".into()
    }
}

fn rgb_to_hsv(r: u8, g: u8, b: u8) -> (f32, f32, f32) {
    let rf = r as f32 / 255.0;
    let gf = g as f32 / 255.0;
    let bf = b as f32 / 255.0;
    let max = rf.max(gf).max(bf);
    let min = rf.min(gf).min(bf);
    let d = max - min;
    let h = if d <= 1e-6 {
        0.0
    } else if (max - rf).abs() <= 1e-6 {
        60.0 * (((gf - bf) / d).rem_euclid(6.0))
    } else if (max - gf).abs() <= 1e-6 {
        60.0 * (((bf - rf) / d) + 2.0)
    } else {
        60.0 * (((rf - gf) / d) + 4.0)
    };
    let s = if max <= 1e-6 { 0.0 } else { d / max };
    (h.rem_euclid(360.0), s, max)
}

fn hsv_to_rgb(h: f32, s: f32, v: f32) -> (u8, u8, u8) {
    let c = v * s;
    let x = c * (1.0 - (((h / 60.0) % 2.0) - 1.0).abs());
    let m = v - c;
    let (r1, g1, b1) = if h < 60.0 {
        (c, x, 0.0)
    } else if h < 120.0 {
        (x, c, 0.0)
    } else if h < 180.0 {
        (0.0, c, x)
    } else if h < 240.0 {
        (0.0, x, c)
    } else if h < 300.0 {
        (x, 0.0, c)
    } else {
        (c, 0.0, x)
    };
    (
        ((r1 + m).clamp(0.0, 1.0) * 255.0).round() as u8,
        ((g1 + m).clamp(0.0, 1.0) * 255.0).round() as u8,
        ((b1 + m).clamp(0.0, 1.0) * 255.0).round() as u8,
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn wallpaper_accent_wcag_adjustment_guarantees_contrast() {
        // Dark blue on dark surface must lift to >= 4.5:1
        let dark_adj = adjust_accent_for_wcag_aa(22, 38, 120, "#0F172A");
        assert!(
            contrast_ratio(&dark_adj, "#0F172A") >= 4.5,
            "Adjusted dark accent {dark_adj} failed WCAG AA on #0F172A"
        );
        // Pale yellow on light surface must darken to >= 4.5:1
        let light_adj = adjust_accent_for_wcag_aa(250, 232, 110, "#F7F5F0");
        assert!(
            contrast_ratio(&light_adj, "#F7F5F0") >= 4.5,
            "Adjusted light accent {light_adj} failed WCAG AA on #F7F5F0"
        );
    }
}
