use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum ColorMode {
    Dark,
    Light,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum VisualStyle {
    #[serde(rename = "liquid-glass")]
    LiquidGlass,
    #[serde(rename = "bento-silk")]
    BentoSilk,
    #[serde(rename = "linear-precision")]
    LinearPrecision,
    #[serde(rename = "mono-matrix")]
    MonoMatrix,
    #[serde(rename = "spatial-vision")]
    SpatialVision,
    #[serde(rename = "cyber-prism")]
    CyberHud,
    #[serde(rename = "high-contrast")]
    HighContrast,
}

impl VisualStyle {
    pub fn as_str(self) -> &'static str {
        match self {
            Self::LiquidGlass => "liquid-glass",
            Self::BentoSilk => "bento-silk",
            Self::LinearPrecision => "linear-precision",
            Self::MonoMatrix => "mono-matrix",
            Self::SpatialVision => "spatial-vision",
            Self::CyberHud => "cyber-prism",
            Self::HighContrast => "high-contrast",
        }
    }

    pub fn style_tag_tr(self) -> &'static str {
        match self {
            Self::LiquidGlass => "Optik Cam",
            Self::BentoSilk => "Porselen",
            Self::LinearPrecision => "İsviçre Mat",
            Self::MonoMatrix => "Nokta Matris",
            Self::SpatialVision => "Uzamsal Cam",
            Self::CyberHud => "Taktik HUD",
            Self::HighContrast => "WCAG AAA",
        }
    }

    pub fn uses_acrylic_backdrop(self) -> bool {
        matches!(self, Self::LiquidGlass | Self::SpatialVision)
    }

    pub fn uses_monospace_headers(self) -> bool {
        matches!(self, Self::MonoMatrix | Self::CyberHud)
    }
}

/// Premultiplied-ready sRGB color with floating-point channels in `[0.0, 1.0]`.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Color {
    pub r: f32,
    pub g: f32,
    pub b: f32,
    pub a: f32,
}

impl Color {
    pub const TRANSPARENT: Self = Self { r: 0.0, g: 0.0, b: 0.0, a: 0.0 };
    pub const WHITE: Self = Self { r: 1.0, g: 1.0, b: 1.0, a: 1.0 };
    pub const BLACK: Self = Self { r: 0.0, g: 0.0, b: 0.0, a: 1.0 };

    pub const fn rgba(r: u8, g: u8, b: u8, a: f32) -> Self {
        Self {
            r: r as f32 / 255.0,
            g: g as f32 / 255.0,
            b: b as f32 / 255.0,
            a,
        }
    }

    pub fn with_alpha(self, alpha: f32) -> Self {
        Self { a: alpha.clamp(0.0, 1.0), ..self }
    }

    pub fn lerp(self, other: Self, t: f32) -> Self {
        let t = t.clamp(0.0, 1.0);
        Self {
            r: self.r + (other.r - self.r) * t,
            g: self.g + (other.g - self.g) * t,
            b: self.b + (other.b - self.b) * t,
            a: self.a + (other.a - self.a) * t,
        }
    }

    pub fn from_hex(hex: &str) -> Self {
        let (a, r, g, b) = parse_hex_argb(hex);
        Self {
            r: r as f32 / 255.0,
            g: g as f32 / 255.0,
            b: b as f32 / 255.0,
            a: a as f32 / 255.0,
        }
    }

    pub fn to_hex_rgb(self) -> String {
        let r = (self.r.clamp(0.0, 1.0) * 255.0).round() as u8;
        let g = (self.g.clamp(0.0, 1.0) * 255.0).round() as u8;
        let b = (self.b.clamp(0.0, 1.0) * 255.0).round() as u8;
        format!("#{r:02X}{g:02X}{b:02X}")
    }

    /// Packs color into `0xAABBGGRR` for Win32 AccentPolicy / COLORREF interop.
    pub fn to_abgr_u32(self) -> u32 {
        let r = (self.r.clamp(0.0, 1.0) * 255.0).round() as u32;
        let g = (self.g.clamp(0.0, 1.0) * 255.0).round() as u32;
        let b = (self.b.clamp(0.0, 1.0) * 255.0).round() as u32;
        let a = (self.a.clamp(0.0, 1.0) * 255.0).round() as u32;
        (a << 24) | (b << 16) | (g << 8) | r
    }
}

pub fn parse_hex_argb(hex: &str) -> (u8, u8, u8, u8) {
    let trimmed = hex.trim().trim_start_matches('#');
    if trimmed.len() == 6 {
        if let Ok(rgb) = u32::from_str_radix(trimmed, 16) {
            return (255, ((rgb >> 16) & 0xFF) as u8, ((rgb >> 8) & 0xFF) as u8, (rgb & 0xFF) as u8);
        }
    } else if trimmed.len() == 8 {
        if let Ok(argb) = u32::from_str_radix(trimmed, 16) {
            return (
                ((argb >> 24) & 0xFF) as u8,
                ((argb >> 16) & 0xFF) as u8,
                ((argb >> 8) & 0xFF) as u8,
                (argb & 0xFF) as u8,
            );
        }
    }
    (255, 15, 23, 42)
}

pub fn is_valid_hex_color(hex: &str) -> bool {
    let trimmed = hex.trim();
    if !trimmed.starts_with('#') {
        return false;
    }
    let body = &trimmed[1..];
    (body.len() == 6 || body.len() == 8) && u32::from_str_radix(body, 16).is_ok()
}

pub fn relative_luminance(hex: &str) -> f64 {
    let (_, r, g, b) = parse_hex_argb(hex);
    let ch = |c: u8| {
        let v = c as f64 / 255.0;
        if v <= 0.03928 {
            v / 12.92
        } else {
            ((v + 0.055) / 1.055).powf(2.4)
        }
    };
    0.2126 * ch(r) + 0.7152 * ch(g) + 0.0722 * ch(b)
}

pub fn contrast_ratio(fg_hex: &str, bg_hex: &str) -> f64 {
    let l1 = relative_luminance(fg_hex);
    let l2 = relative_luminance(bg_hex);
    let (hi, lo) = if l1 >= l2 { (l1, l2) } else { (l2, l1) };
    (((hi + 0.05) / (lo + 0.05)) * 100.0).round() / 100.0
}

pub fn meets_wcag_aa(fg_hex: &str, bg_hex: &str, large_text: bool) -> bool {
    contrast_ratio(fg_hex, bg_hex) >= if large_text { 3.0 } else { 4.5 }
}

pub fn pick_readable_ink(bg_hex: &str) -> &'static str {
    if relative_luminance(bg_hex) > 0.36 {
        "#050B16"
    } else {
        "#FFFFFF"
    }
}

#[derive(Debug, Clone, Default, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ThemeColorCustomization {
    #[serde(default)]
    pub accent_primary: Option<String>,
    #[serde(default)]
    pub accent_secondary: Option<String>,
    #[serde(default)]
    pub surface1: Option<String>,
    #[serde(default)]
    pub surface2: Option<String>,
    #[serde(default)]
    pub text_primary: Option<String>,
    #[serde(default)]
    pub surface_opacity: Option<f64>,
}

impl ThemeColorCustomization {
    pub fn has_any_override(&self) -> bool {
        self.accent_primary.as_deref().is_some_and(|s| !s.is_empty())
            || self.accent_secondary.as_deref().is_some_and(|s| !s.is_empty())
            || self.surface1.as_deref().is_some_and(|s| !s.is_empty())
            || self.surface2.as_deref().is_some_and(|s| !s.is_empty())
            || self.text_primary.as_deref().is_some_and(|s| !s.is_empty())
            || self.surface_opacity.is_some()
    }
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ThemePalette {
    pub canvas: String,
    pub surface1: String,
    pub surface2: String,
    pub surface3: String,
    pub surface_elevated: String,
    pub text_primary: String,
    pub text_secondary: String,
    pub text_disabled: String,
    pub border_subtle: String,
    pub border_strong: String,
    pub accent_primary: String,
    pub accent_secondary: String,
    pub state_success: String,
    pub state_warning: String,
    pub state_danger: String,
    pub state_info: String,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ThemeTokens {
    pub id: String,
    pub display_name: String,
    pub description: String,
    pub color_mode: ColorMode,
    pub visual_style: VisualStyle,
    pub colors: ThemePalette,
    pub surface_opacity: f32,
    pub radius_shell: f32,
    pub radius_card: f32,
    pub radius_item: f32,
    pub radius_btn: f32,
    pub shadow_intensity: f32,
}

impl ThemeTokens {
    pub fn with_customization(&self, custom: Option<&ThemeColorCustomization>) -> Self {
        let Some(c) = custom else {
            return self.clone();
        };
        let mut out = self.clone();
        if let Some(a1) = c.accent_primary.as_deref().filter(|s| is_valid_hex_color(s)) {
            out.colors.accent_primary = a1.to_uppercase();
        }
        if let Some(a2) = c.accent_secondary.as_deref().filter(|s| is_valid_hex_color(s)) {
            out.colors.accent_secondary = a2.to_uppercase();
        }
        if let Some(s1) = c.surface1.as_deref().filter(|s| is_valid_hex_color(s)) {
            out.colors.surface1 = s1.to_uppercase();
            out.color_mode = if relative_luminance(s1) > 0.42 {
                ColorMode::Light
            } else {
                ColorMode::Dark
            };
        }
        if let Some(s2) = c.surface2.as_deref().filter(|s| is_valid_hex_color(s)) {
            out.colors.surface2 = s2.to_uppercase();
        }
        if let Some(t1) = c.text_primary.as_deref().filter(|s| is_valid_hex_color(s)) {
            out.colors.text_primary = t1.to_uppercase();
        } else if c.surface1.is_some() {
            // Auto-adapt primary/secondary text for guaranteed WCAG contrast when user switches dark <-> light surface
            if out.color_mode == ColorMode::Light {
                out.colors.text_primary = "#14161A".into();
                out.colors.text_secondary = "#4A515E".into();
            } else {
                out.colors.text_primary = "#F8FAFC".into();
                out.colors.text_secondary = "#B6C4D8".into();
            }
        }
        if let Some(op) = c.surface_opacity {
            out.surface_opacity = (op as f32).clamp(0.55, 1.0);
        }
        out
    }

    /// Pre-resolved GPU colors ready for immediate Direct2D rendering with zero string parsing per frame.
    pub fn resolved(&self) -> ResolvedTheme {
        let is_light = self.color_mode == ColorMode::Light;
        let op1 = self.surface_opacity.clamp(0.55, 1.0);
        let op2 = (op1 + 0.04).min(1.0);
        let s1_solid = Color::from_hex(&self.colors.surface1);
        let s2_solid = Color::from_hex(&self.colors.surface2);
        let a1 = Color::from_hex(&self.colors.accent_primary);
        let a2 = Color::from_hex(&self.colors.accent_secondary);
        let t1 = Color::from_hex(&self.colors.text_primary);
        let t2 = Color::from_hex(&self.colors.text_secondary);
        let accent_ink = Color::from_hex(pick_readable_ink(&self.colors.accent_primary));

        ResolvedTheme {
            style: self.visual_style,
            is_light,
            surface1: s1_solid.with_alpha(op1),
            surface2: s2_solid.with_alpha(op2),
            surface1_solid: s1_solid,
            surface_elevated: Color::from_hex(&self.colors.surface_elevated)
                .with_alpha(if is_light { 0.96 } else { (op2 + 0.02).min(1.0) }),
            card_bg: if is_light {
                Color::rgba(20, 22, 26, 0.05)
            } else {
                Color::rgba(255, 255, 255, 0.05)
            },
            card_hover: if is_light {
                Color::rgba(20, 22, 26, 0.095)
            } else {
                Color::rgba(255, 255, 255, 0.105)
            },
            inset_bg: if is_light {
                Color::rgba(20, 22, 26, 0.07)
            } else {
                Color::rgba(0, 0, 0, 0.30)
            },
            border_rim: if is_light {
                Color::rgba(20, 22, 26, 0.14)
            } else {
                Color::rgba(255, 255, 255, 0.17)
            },
            border_subtle: if is_light {
                Color::rgba(20, 22, 26, 0.085)
            } else {
                Color::rgba(255, 255, 255, 0.085)
            },
            text_primary: t1,
            text_secondary: t2,
            accent1: a1,
            accent2: a2,
            accent_soft: a1.with_alpha(if is_light { 0.12 } else { 0.15 }),
            accent_tint: a1.with_alpha(if is_light { 0.20 } else { 0.24 }),
            accent_ring: a1.with_alpha(0.50),
            accent_glow: a1.with_alpha(if is_light { 0.24 } else { 0.36 }),
            accent_ink,
            success: Color::from_hex(&self.colors.state_success),
            warning: Color::from_hex(&self.colors.state_warning),
            danger: Color::from_hex(&self.colors.state_danger),
            radius_shell: self.radius_shell,
            radius_card: self.radius_card,
            radius_item: self.radius_item,
            radius_btn: self.radius_btn,
            shadow_intensity: self.shadow_intensity,
            text_contrast_ratio: contrast_ratio(&self.colors.text_primary, &self.colors.surface1),
        }
    }
}

#[derive(Debug, Clone, Copy)]
pub struct ResolvedTheme {
    pub style: VisualStyle,
    pub is_light: bool,
    pub surface1: Color,
    pub surface2: Color,
    pub surface1_solid: Color,
    pub surface_elevated: Color,
    pub card_bg: Color,
    pub card_hover: Color,
    pub inset_bg: Color,
    pub border_rim: Color,
    pub border_subtle: Color,
    pub text_primary: Color,
    pub text_secondary: Color,
    pub accent1: Color,
    pub accent2: Color,
    pub accent_soft: Color,
    pub accent_tint: Color,
    pub accent_ring: Color,
    pub accent_glow: Color,
    pub accent_ink: Color,
    pub success: Color,
    pub warning: Color,
    pub danger: Color,
    pub radius_shell: f32,
    pub radius_card: f32,
    pub radius_item: f32,
    pub radius_btn: f32,
    pub shadow_intensity: f32,
    pub text_contrast_ratio: f64,
}

#[derive(Debug, Clone, Copy)]
pub struct AccentPreset {
    pub name: &'static str,
    pub accent1: &'static str,
    pub accent2: &'static str,
}

pub const CURATED_ACCENT_PRESETS: &[AccentPreset] = &[
    AccentPreset { name: "Buz Mavisi", accent1: "#38BDF8", accent2: "#818CF8" },
    AccentPreset { name: "Zümrüt", accent1: "#10B981", accent2: "#34D399" },
    AccentPreset { name: "Sinyal Kırmızı", accent1: "#FF453A", accent2: "#FF9F0A" },
    AccentPreset { name: "Kehribar Altın", accent1: "#F59E0B", accent2: "#FB923C" },
    AccentPreset { name: "Uzamsal İris", accent1: "#A78BFA", accent2: "#2DD4BF" },
    AccentPreset { name: "Kobalt", accent1: "#2563EB", accent2: "#0EA5E9" },
    AccentPreset { name: "Siber Mercan", accent1: "#F43F5E", accent2: "#EC4899" },
    AccentPreset { name: "Asit Yeşil", accent1: "#84CC16", accent2: "#22C55E" },
];

pub fn built_in_themes() -> Vec<ThemeTokens> {
    vec![
        // 1. Liquid Glass 26 — Apple macOS Tahoe / iOS 26 Refractive Optical Crystal
        ThemeTokens {
            id: "aurora-glass-dark".into(),
            display_name: "Liquid Glass 26".into(),
            description: "Gerçek DWM optik cam bulanıklığı, üst kırılma yansıması ve akıcı simge ışığı.".into(),
            color_mode: ColorMode::Dark,
            visual_style: VisualStyle::LiquidGlass,
            colors: ThemePalette {
                canvas: "#0A0F1D".into(),
                surface1: "#0F172A".into(),
                surface2: "#162038".into(),
                surface3: "#1E2C4A".into(),
                surface_elevated: "#1A2744".into(),
                text_primary: "#F8FAFC".into(),
                text_secondary: "#B6C4D8".into(),
                text_disabled: "#64748B".into(),
                border_subtle: "#2A3A5C".into(),
                border_strong: "#47608E".into(),
                accent_primary: "#38BDF8".into(),
                accent_secondary: "#818CF8".into(),
                state_success: "#34D399".into(),
                state_warning: "#FBBF24".into(),
                state_danger: "#FB7185".into(),
                state_info: "#60A5FA".into(),
            },
            surface_opacity: 0.84,
            radius_shell: 24.0,
            radius_card: 17.0,
            radius_item: 13.0,
            radius_btn: 999.0,
            shadow_intensity: 0.48,
        },
        // 2. Bento Silk — Scandinavian Porcelain Ceramic (Light)
        ThemeTokens {
            id: "aurora-glass-light".into(),
            display_name: "Bento Silk".into(),
            description: "Katmanlı beyaz porselen kartlar, doğal gün ışığı derinliği ve sıfır neon yalınlık.".into(),
            color_mode: ColorMode::Light,
            visual_style: VisualStyle::BentoSilk,
            colors: ThemePalette {
                canvas: "#ECE9E2".into(),
                surface1: "#F7F5F0".into(),
                surface2: "#ECE8E0".into(),
                surface3: "#E2DDD3".into(),
                surface_elevated: "#FFFFFF".into(),
                text_primary: "#14161A".into(),
                text_secondary: "#4A515E".into(),
                text_disabled: "#9098A6".into(),
                border_subtle: "#D6D2C8".into(),
                border_strong: "#B5AFA3".into(),
                accent_primary: "#2563EB".into(),
                accent_secondary: "#0EA5E9".into(),
                state_success: "#059669".into(),
                state_warning: "#D97706".into(),
                state_danger: "#E11D48".into(),
                state_info: "#2563EB".into(),
            },
            surface_opacity: 0.96,
            radius_shell: 22.0,
            radius_card: 16.0,
            radius_item: 13.0,
            radius_btn: 11.0,
            shadow_intensity: 0.24,
        },
        // 3. Linear Swiss — Matte Carbon, Razor 1px Hairlines, Zero Glow
        ThemeTokens {
            id: "obsidian-pro-dark".into(),
            display_name: "Linear Swiss".into(),
            description: "Mat karbon yüzey, altı çizgili sekmeler, 1px İsviçre çizgileri ve sıfır parlama.".into(),
            color_mode: ColorMode::Dark,
            visual_style: VisualStyle::LinearPrecision,
            colors: ThemePalette {
                canvas: "#090A0D".into(),
                surface1: "#101217".into(),
                surface2: "#161920".into(),
                surface3: "#1E232D".into(),
                surface_elevated: "#181C24".into(),
                text_primary: "#F4F6F9".into(),
                text_secondary: "#A3AEBE".into(),
                text_disabled: "#636D7E".into(),
                border_subtle: "#262C38".into(),
                border_strong: "#3F495C".into(),
                accent_primary: "#10B981".into(),
                accent_secondary: "#34D399".into(),
                state_success: "#22C55E".into(),
                state_warning: "#F59E0B".into(),
                state_danger: "#EF4444".into(),
                state_info: "#3B82F6".into(),
            },
            surface_opacity: 0.97,
            radius_shell: 10.0,
            radius_card: 7.0,
            radius_item: 7.0,
            radius_btn: 6.0,
            shadow_intensity: 0.30,
        },
        // 4. Mono Matrix — Nothing OS / Teenage Engineering Industrial Dot-Matrix
        ThemeTokens {
            id: "mono-matrix-dark".into(),
            display_name: "Mono Matrix".into(),
            description: "Nokta matris doku, dairesel donanım soketleri, 3'lü LED göstergeler ve mono tipografi.".into(),
            color_mode: ColorMode::Dark,
            visual_style: VisualStyle::MonoMatrix,
            colors: ThemePalette {
                canvas: "#08080A".into(),
                surface1: "#0E0E11".into(),
                surface2: "#16161A".into(),
                surface3: "#202026".into(),
                surface_elevated: "#1A1A20".into(),
                text_primary: "#F5F5F7".into(),
                text_secondary: "#A8A8B3".into(),
                text_disabled: "#63636E".into(),
                border_subtle: "#282830".into(),
                border_strong: "#484854".into(),
                accent_primary: "#FF453A".into(),
                accent_secondary: "#FF9F0A".into(),
                state_success: "#30D158".into(),
                state_warning: "#FF9F0A".into(),
                state_danger: "#FF453A".into(),
                state_info: "#64D2FF".into(),
            },
            surface_opacity: 0.97,
            radius_shell: 18.0,
            radius_card: 12.0,
            radius_item: 999.0,
            radius_btn: 999.0,
            shadow_intensity: 0.35,
        },
        // 5. Spatial Vision — Apple visionOS 26 Floating Frosted Glass Capsules (WCAG AA+ #A78BFA!)
        ThemeTokens {
            id: "aurora-flow-dark".into(),
            display_name: "Spatial Vision".into(),
            description: "Tam kapsül uzamsal form, gerçek cam bulanıklığı, çift kenarlık ve dairesel odak haleleri.".into(),
            color_mode: ColorMode::Dark,
            visual_style: VisualStyle::SpatialVision,
            colors: ThemePalette {
                canvas: "#0D0E1B".into(),
                surface1: "#14162A".into(),
                surface2: "#1D203C".into(),
                surface3: "#272B4E".into(),
                surface_elevated: "#212545".into(),
                text_primary: "#FAF9FF".into(),
                text_secondary: "#C5C2E2".into(),
                text_disabled: "#76729E".into(),
                border_subtle: "#343866".into(),
                border_strong: "#565C9E".into(),
                accent_primary: "#A78BFA".into(),
                accent_secondary: "#2DD4BF".into(),
                state_success: "#34D399".into(),
                state_warning: "#FBBF24".into(),
                state_danger: "#FB7185".into(),
                state_info: "#60A5FA".into(),
            },
            surface_opacity: 0.82,
            radius_shell: 30.0,
            radius_card: 22.0,
            radius_item: 999.0,
            radius_btn: 999.0,
            shadow_intensity: 0.48,
        },
        // 6. Cyber HUD — Chamfered Tactical HUD with Unclipped D2D Neon Glow
        ThemeTokens {
            id: "nebula-dark".into(),
            display_name: "Cyber HUD".into(),
            description: "Açılı trapez üst çentik, köşe nişangah çerçeveleri, blueprint ızgara ve kırpılmayan lazer parlaması.".into(),
            color_mode: ColorMode::Dark,
            visual_style: VisualStyle::CyberHud,
            colors: ThemePalette {
                canvas: "#05070E".into(),
                surface1: "#090E1C".into(),
                surface2: "#10182E".into(),
                surface3: "#182444".into(),
                surface_elevated: "#131D38".into(),
                text_primary: "#F0F9FF".into(),
                text_secondary: "#A5CBE2".into(),
                text_disabled: "#526D8A".into(),
                border_subtle: "#1D3259".into(),
                border_strong: "#06B6D4".into(),
                accent_primary: "#06B6D4".into(),
                accent_secondary: "#F43F5E".into(),
                state_success: "#4ADE80".into(),
                state_warning: "#FACC15".into(),
                state_danger: "#F43F5E".into(),
                state_info: "#38BDF8".into(),
            },
            surface_opacity: 0.94,
            radius_shell: 3.0,
            radius_card: 3.0,
            radius_item: 3.0,
            radius_btn: 3.0,
            shadow_intensity: 0.54,
        },
        // 7. High Contrast — WCAG AAA Safe
        ThemeTokens {
            id: "aurora-high-contrast".into(),
            display_name: "Yüksek Kontrast".into(),
            description: "Sıfır bulanıklık, maksimum WCAG AAA metin ve odak kontrastı.".into(),
            color_mode: ColorMode::Dark,
            visual_style: VisualStyle::HighContrast,
            colors: ThemePalette {
                canvas: "#000000".into(),
                surface1: "#0A0A0A".into(),
                surface2: "#141414".into(),
                surface3: "#1F1F1F".into(),
                surface_elevated: "#141414".into(),
                text_primary: "#FFFFFF".into(),
                text_secondary: "#EFEFEF".into(),
                text_disabled: "#8C8C8C".into(),
                border_subtle: "#A0A0A0".into(),
                border_strong: "#FFFFFF".into(),
                accent_primary: "#00E5FF".into(),
                accent_secondary: "#FFFF00".into(),
                state_success: "#00FF7F".into(),
                state_warning: "#FFD700".into(),
                state_danger: "#FF5252".into(),
                state_info: "#40C4FF".into(),
            },
            surface_opacity: 1.0,
            radius_shell: 6.0,
            radius_card: 5.0,
            radius_item: 5.0,
            radius_btn: 5.0,
            shadow_intensity: 0.0,
        },
    ]
}

pub fn theme_by_id(id: &str) -> ThemeTokens {
    let all = built_in_themes();
    all.iter()
        .find(|t| t.id.eq_ignore_ascii_case(id))
        .cloned()
        .unwrap_or_else(|| all.into_iter().next().unwrap())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn all_seven_themes_exceed_wcag_aa_on_primary_secondary_and_accent() {
        for theme in built_in_themes() {
            let r_primary = contrast_ratio(&theme.colors.text_primary, &theme.colors.surface1);
            let r_secondary = contrast_ratio(&theme.colors.text_secondary, &theme.colors.surface2);
            let r_accent = contrast_ratio(&theme.colors.accent_primary, &theme.colors.surface1);
            assert!(
                r_primary >= 7.0,
                "{} primary text contrast {r_primary}:1 < 7.0 (AAA)",
                theme.display_name
            );
            assert!(
                r_secondary >= 4.5,
                "{} secondary text contrast {r_secondary}:1 < 4.5 (AA)",
                theme.display_name
            );
            assert!(
                r_accent >= 4.5,
                "{} accent contrast {r_accent}:1 < 4.5 (AA)",
                theme.display_name
            );
        }
    }

    #[test]
    fn customization_adapts_color_mode_and_preserves_contrast() {
        let base = theme_by_id("aurora-glass-dark");
        let custom = ThemeColorCustomization {
            surface1: Some("#F8FAFC".into()),
            accent_primary: Some("#2563EB".into()),
            surface_opacity: Some(0.40),
            ..Default::default()
        };
        let applied = base.with_customization(Some(&custom));
        assert_eq!(applied.color_mode, ColorMode::Light);
        assert!((applied.surface_opacity - 0.55).abs() < 1e-4);
        assert!(meets_wcag_aa(&applied.colors.text_primary, &applied.colors.surface1, false));
    }
}
