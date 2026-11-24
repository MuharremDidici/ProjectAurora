use std::f32::consts::PI;
use aurora_core::Color;
use windows::Win32::Graphics::Direct2D::Common::{
    D2D1_COLOR_F, D2D1_FIGURE_BEGIN_FILLED, D2D1_FIGURE_BEGIN_HOLLOW, D2D1_FIGURE_END_CLOSED,
    D2D1_FIGURE_END_OPEN, D2D1_GRADIENT_STOP, D2D_POINT_2F, D2D_RECT_F,
};
use windows::Win32::Graphics::Direct2D::{
    ID2D1DeviceContext, ID2D1RenderTarget, ID2D1SolidColorBrush, D2D1_ANTIALIAS_MODE_PER_PRIMITIVE,
    D2D1_BITMAP_INTERPOLATION_MODE_LINEAR, D2D1_DRAW_TEXT_OPTIONS_CLIP,
    D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT, D2D1_ELLIPSE, D2D1_EXTEND_MODE_CLAMP,
    D2D1_GAMMA_2_2, D2D1_LAYER_OPTIONS1_NONE, D2D1_LAYER_PARAMETERS1,
    D2D1_LINEAR_GRADIENT_BRUSH_PROPERTIES, D2D1_RADIAL_GRADIENT_BRUSH_PROPERTIES,
    D2D1_ROUNDED_RECT,
};
use windows::Win32::Graphics::DirectWrite::DWRITE_TEXT_METRICS;
use windows::Foundation::Numerics::Matrix3x2;

use crate::{FontFamily, GpuContext, IconKind, TextAlign};

#[derive(Debug, Clone, Copy, Default)]
pub struct RectF {
    pub x: f32,
    pub y: f32,
    pub w: f32,
    pub h: f32,
}

impl RectF {
    pub const fn new(x: f32, y: f32, w: f32, h: f32) -> Self {
        Self { x, y, w, h }
    }
    pub fn right(self) -> f32 { self.x + self.w }
    pub fn bottom(self) -> f32 { self.y + self.h }
    pub fn center_x(self) -> f32 { self.x + self.w * 0.5 }
    pub fn center_y(self) -> f32 { self.y + self.h * 0.5 }
    pub fn inset(self, dx: f32, dy: f32) -> Self {
        Self::new(self.x + dx, self.y + dy, (self.w - dx * 2.0).max(0.0), (self.h - dy * 2.0).max(0.0))
    }
    pub fn contains(self, px: f32, py: f32) -> bool {
        px >= self.x && px <= self.right() && py >= self.y && py <= self.bottom()
    }
    fn to_d2d(self) -> D2D_RECT_F {
        D2D_RECT_F {
            left: self.x,
            top: self.y,
            right: self.x + self.w,
            bottom: self.y + self.h,
        }
    }
}

fn to_d2d_color(c: Color) -> D2D1_COLOR_F {
    D2D1_COLOR_F { r: c.r, g: c.g, b: c.b, a: c.a }
}

/// High-craft Direct2D 1.3 + DirectWrite hardware painter for Aurora v3.0 surfaces.
pub struct Painter<'a> {
    pub dc: &'a ID2D1DeviceContext,
    pub gpu: &'a GpuContext,
}

impl<'a> Painter<'a> {
    pub fn new(dc: &'a ID2D1DeviceContext, gpu: &'a GpuContext) -> Self {
        unsafe {
            dc.SetAntialiasMode(D2D1_ANTIALIAS_MODE_PER_PRIMITIVE);
        }
        Self { dc, gpu }
    }

    fn solid_brush(&self, color: Color) -> Option<ID2D1SolidColorBrush> {
        unsafe { self.dc.CreateSolidColorBrush(&to_d2d_color(color), None).ok() }
    }

    pub fn fill_rect(&self, r: RectF, color: Color) {
        if color.a <= 0.001 || r.w <= 0.0 || r.h <= 0.0 { return; }
        if let Some(b) = self.solid_brush(color) {
            unsafe { self.dc.FillRectangle(&r.to_d2d(), &b); }
        }
    }

    pub fn fill_rounded_rect(&self, r: RectF, radius: f32, color: Color) {
        if color.a <= 0.001 || r.w <= 0.0 || r.h <= 0.0 { return; }
        let rad = radius.min(r.w * 0.5).min(r.h * 0.5).max(0.0);
        if let Some(b) = self.solid_brush(color) {
            let rr = D2D1_ROUNDED_RECT { rect: r.to_d2d(), radiusX: rad, radiusY: rad };
            unsafe { self.dc.FillRoundedRectangle(&rr, &b); }
        }
    }

    pub fn stroke_rounded_rect(&self, r: RectF, radius: f32, color: Color, width: f32, dashed: bool) {
        if color.a <= 0.001 || r.w <= 0.0 || r.h <= 0.0 || width <= 0.0 { return; }
        let half = width * 0.5;
        let inset = r.inset(half, half);
        let rad = (radius - half).min(inset.w * 0.5).min(inset.h * 0.5).max(0.0);
        if let Some(b) = self.solid_brush(color) {
            let rr = D2D1_ROUNDED_RECT { rect: inset.to_d2d(), radiusX: rad, radiusY: rad };
            unsafe {
                let style = if dashed { Some(&self.gpu.dashed_stroke) } else { None };
                self.dc.DrawRoundedRectangle(&rr, &b, width, style);
            }
        }
    }

    pub fn fill_linear_gradient_rounded(&self, r: RectF, radius: f32, c1: Color, c2: Color, vertical: bool) {
        if r.w <= 0.0 || r.h <= 0.0 { return; }
        let rad = radius.min(r.w * 0.5).min(r.h * 0.5).max(0.0);
        let stops = [
            D2D1_GRADIENT_STOP { position: 0.0, color: to_d2d_color(c1) },
            D2D1_GRADIENT_STOP { position: 1.0, color: to_d2d_color(c2) },
        ];
        unsafe {
            let Ok(coll) = ID2D1RenderTarget::CreateGradientStopCollection(self.dc, &stops, D2D1_GAMMA_2_2, D2D1_EXTEND_MODE_CLAMP) else {
                return;
            };
            let props = D2D1_LINEAR_GRADIENT_BRUSH_PROPERTIES {
                startPoint: D2D_POINT_2F { x: r.x, y: r.y },
                endPoint: if vertical {
                    D2D_POINT_2F { x: r.x, y: r.bottom() }
                } else {
                    D2D_POINT_2F { x: r.right(), y: r.y }
                },
            };
            if let Ok(brush) = self.dc.CreateLinearGradientBrush(&props, None, &coll) {
                let rr = D2D1_ROUNDED_RECT { rect: r.to_d2d(), radiusX: rad, radiusY: rad };
                self.dc.FillRoundedRectangle(&rr, &brush);
            }
        }
    }

    pub fn fill_radial_glow(&self, cx: f32, cy: f32, rx: f32, ry: f32, center_color: Color) {
        if center_color.a <= 0.001 || rx <= 0.5 || ry <= 0.5 { return; }
        let stops = [
            D2D1_GRADIENT_STOP { position: 0.0, color: to_d2d_color(center_color) },
            D2D1_GRADIENT_STOP { position: 1.0, color: to_d2d_color(center_color.with_alpha(0.0)) },
        ];
        unsafe {
            let Ok(coll) = ID2D1RenderTarget::CreateGradientStopCollection(self.dc, &stops, D2D1_GAMMA_2_2, D2D1_EXTEND_MODE_CLAMP) else {
                return;
            };
            let props = D2D1_RADIAL_GRADIENT_BRUSH_PROPERTIES {
                center: D2D_POINT_2F { x: cx, y: cy },
                gradientOriginOffset: D2D_POINT_2F { x: 0.0, y: 0.0 },
                radiusX: rx,
                radiusY: ry,
            };
            if let Ok(brush) = self.dc.CreateRadialGradientBrush(&props, None, &coll) {
                let ell = D2D1_ELLIPSE {
                    point: D2D_POINT_2F { x: cx, y: cy },
                    radiusX: rx,
                    radiusY: ry,
                };
                self.dc.FillEllipse(&ell, &brush);
            }
        }
    }

    pub fn fill_circle(&self, cx: f32, cy: f32, r: f32, color: Color) {
        if color.a <= 0.001 || r <= 0.0 { return; }
        if let Some(b) = self.solid_brush(color) {
            let ell = D2D1_ELLIPSE { point: D2D_POINT_2F { x: cx, y: cy }, radiusX: r, radiusY: r };
            unsafe { self.dc.FillEllipse(&ell, &b); }
        }
    }

    pub fn stroke_circle(&self, cx: f32, cy: f32, r: f32, color: Color, width: f32, dashed: bool) {
        if color.a <= 0.001 || r <= 0.0 || width <= 0.0 { return; }
        if let Some(b) = self.solid_brush(color) {
            let ell = D2D1_ELLIPSE { point: D2D_POINT_2F { x: cx, y: cy }, radiusX: r, radiusY: r };
            unsafe {
                let style = if dashed { Some(&self.gpu.dashed_stroke) } else { None };
                self.dc.DrawEllipse(&ell, &b, width, style);
            }
        }
    }

    #[allow(clippy::too_many_arguments)]
    pub fn draw_line(&self, x1: f32, y1: f32, x2: f32, y2: f32, color: Color, width: f32, dashed: bool) {
        if color.a <= 0.001 || width <= 0.0 { return; }
        if let Some(b) = self.solid_brush(color) {
            unsafe {
                let style = if dashed {
                    Some(&self.gpu.dashed_stroke)
                } else {
                    Some(&self.gpu.round_stroke)
                };
                self.dc.DrawLine(
                    D2D_POINT_2F { x: x1, y: y1 },
                    D2D_POINT_2F { x: x2, y: y2 },
                    &b,
                    width,
                    style,
                );
            }
        }
    }

    /// Multi-ring smooth GPU elevation shadow without offscreen pass stalls.
    pub fn draw_soft_shadow(&self, r: RectF, radius: f32, intensity: f32, y_offset: f32, spread: f32) {
        if intensity <= 0.01 { return; }
        let rings = 5;
        for i in (1..=rings).rev() {
            let frac = i as f32 / rings as f32;
            let expand = spread * frac;
            let sr = RectF::new(
                r.x - expand,
                r.y + y_offset * frac - expand * 0.35,
                r.w + expand * 2.0,
                r.h + expand * 2.0,
            );
            let alpha = intensity * 0.24 * (1.0 - frac * 0.72);
            self.fill_rounded_rect(sr, radius + expand * 0.55, Color::BLACK.with_alpha(alpha));
        }
    }

    /// Cyber HUD chamfered octagon / trapezoid with genuine UNCLIPPED outer neon glow!
    pub fn draw_chamfered_shell(
        &self,
        pts: &[(f32, f32)],
        fill_top: Color,
        fill_bot: Color,
        border_color: Color,
        glow_color: Color,
        border_width: f32,
    ) {
        if pts.len() < 3 { return; }
        unsafe {
            let Ok(geo) = self.gpu.d2d_factory.CreatePathGeometry() else { return };
            let Ok(sink) = geo.Open() else { return };
            sink.BeginFigure(D2D_POINT_2F { x: pts[0].0, y: pts[0].1 }, D2D1_FIGURE_BEGIN_FILLED);
            for &(px, py) in &pts[1..] {
                sink.AddLine(D2D_POINT_2F { x: px, y: py });
            }
            sink.EndFigure(D2D1_FIGURE_END_CLOSED);
            let _ = sink.Close();

            // Unclipped multi-pass outer neon glow (fixes v2.5.1 CSS clip-path shadow clipping!)
            if glow_color.a > 0.01 {
                for &(w, a_mul) in &[(10.0f32, 0.14f32), (5.0, 0.26)] {
                    if let Some(gb) = self.solid_brush(glow_color.with_alpha(glow_color.a * a_mul)) {
                        self.dc.DrawGeometry(&geo, &gb, w, Some(&self.gpu.round_stroke));
                    }
                }
            }

            let min_y = pts.iter().map(|p| p.1).fold(f32::MAX, f32::min);
            let max_y = pts.iter().map(|p| p.1).fold(f32::MIN, f32::max);
            let stops = [
                D2D1_GRADIENT_STOP { position: 0.0, color: to_d2d_color(fill_top) },
                D2D1_GRADIENT_STOP { position: 1.0, color: to_d2d_color(fill_bot) },
            ];
            if let Ok(coll) = ID2D1RenderTarget::CreateGradientStopCollection(self.dc, &stops, D2D1_GAMMA_2_2, D2D1_EXTEND_MODE_CLAMP) {
                let props = D2D1_LINEAR_GRADIENT_BRUSH_PROPERTIES {
                    startPoint: D2D_POINT_2F { x: pts[0].0, y: min_y },
                    endPoint: D2D_POINT_2F { x: pts[0].0, y: max_y },
                };
                if let Ok(brush) = self.dc.CreateLinearGradientBrush(&props, None, &coll) {
                    self.dc.FillGeometry(&geo, &brush, None);
                }
            }

            if let Some(bb) = self.solid_brush(border_color) {
                self.dc.DrawGeometry(&geo, &bb, border_width, None);
            }
        }
    }

    /// Procedural Mono Matrix dot-matrix texture inside a clipped region.
    pub fn draw_dot_matrix(&self, r: RectF, spacing: f32, dot_color: Color) {
        if dot_color.a <= 0.001 || spacing < 4.0 { return; }
        let Some(b) = self.solid_brush(dot_color) else { return };
        let mut y = r.y + spacing * 0.5;
        unsafe {
            while y < r.bottom() - 2.0 {
                let mut x = r.x + spacing * 0.5;
                while x < r.right() - 2.0 {
                    let ell = D2D1_ELLIPSE { point: D2D_POINT_2F { x, y }, radiusX: 0.85, radiusY: 0.85 };
                    self.dc.FillEllipse(&ell, &b);
                    x += spacing;
                }
                y += spacing;
            }
        }
    }

    /// Procedural Cyber HUD blueprint grid + corner targeting reticles.
    pub fn draw_cyber_blueprint(&self, r: RectF, grid_color: Color, accent1: Color, accent2: Color) {
        if let Some(gb) = self.solid_brush(grid_color) {
            unsafe {
                let mut x = r.x + 16.0;
                while x < r.right() {
                    self.dc.DrawLine(D2D_POINT_2F { x, y: r.y }, D2D_POINT_2F { x, y: r.bottom() }, &gb, 0.75, None);
                    x += 16.0;
                }
                let mut y = r.y + 16.0;
                while y < r.bottom() {
                    self.dc.DrawLine(D2D_POINT_2F { x: r.x, y }, D2D_POINT_2F { x: r.right(), y }, &gb, 0.75, None);
                    y += 16.0;
                }
            }
        }
        // Top-left & bottom-right tactical corner reticles
        let len = 10.0f32.min(r.w * 0.25).min(r.h * 0.25);
        self.draw_line(r.x, r.y + 1.0, r.x + len, r.y + 1.0, accent1, 2.0, false);
        self.draw_line(r.x + 1.0, r.y, r.x + 1.0, r.y + len, accent1, 2.0, false);
        self.draw_line(r.right() - len, r.bottom() - 1.0, r.right(), r.bottom() - 1.0, accent2, 2.0, false);
        self.draw_line(r.right() - 1.0, r.bottom() - len, r.right() - 1.0, r.bottom(), accent2, 2.0, false);
    }

    pub fn push_clip(&self, r: RectF) {
        unsafe {
            self.dc.PushAxisAlignedClip(&r.to_d2d(), D2D1_ANTIALIAS_MODE_PER_PRIMITIVE);
        }
    }

    pub fn pop_clip(&self) {
        unsafe {
            self.dc.PopAxisAlignedClip();
        }
    }

    pub fn push_rounded_clip(&self, r: RectF, radius: f32) -> bool {
        let rad = radius.min(r.w * 0.5).min(r.h * 0.5).max(0.0);
        unsafe {
            let rr = D2D1_ROUNDED_RECT { rect: r.to_d2d(), radiusX: rad, radiusY: rad };
            let Ok(geo) = self.gpu.d2d_factory.CreateRoundedRectangleGeometry(&rr) else {
                return false;
            };
            let params = D2D1_LAYER_PARAMETERS1 {
                contentBounds: r.to_d2d(),
                geometricMask: std::mem::ManuallyDrop::new(Some(geo.into())),
                maskAntialiasMode: D2D1_ANTIALIAS_MODE_PER_PRIMITIVE,
                maskTransform: Matrix3x2::identity(),
                opacity: 1.0,
                opacityBrush: std::mem::ManuallyDrop::new(None),
                layerOptions: D2D1_LAYER_OPTIONS1_NONE,
            };
            self.dc.PushLayer(&params, None);
            true
        }
    }

    pub fn pop_rounded_clip(&self) {
        unsafe {
            self.dc.PopLayer();
        }
    }

    #[allow(clippy::too_many_arguments)]
    pub fn draw_text(
        &self,
        text: &str,
        r: RectF,
        size: f32,
        weight: u16,
        color: Color,
        family: FontFamily,
        align: TextAlign,
        v_center: bool,
        wrap: bool,
    ) {
        if text.is_empty() || color.a <= 0.001 || r.w <= 1.0 || r.h <= 1.0 {
            return;
        }
        let Ok(fmt) = self.gpu.text_format(family, size, weight, align, v_center, wrap) else {
            return;
        };
        let Some(brush) = self.solid_brush(color) else { return };
        let wide: Vec<u16> = text.encode_utf16().collect();
        unsafe {
            self.dc.DrawText(
                &wide,
                &fmt,
                &r.to_d2d(),
                &brush,
                D2D1_DRAW_TEXT_OPTIONS_CLIP | D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT,
                windows::Win32::Graphics::DirectWrite::DWRITE_MEASURING_MODE_NATURAL,
            );
        }
    }

    pub fn measure_text_width(&self, text: &str, size: f32, weight: u16, family: FontFamily) -> f32 {
        if text.is_empty() {
            return 0.0;
        }
        let Ok(fmt) = self.gpu.text_format(family, size, weight, TextAlign::Left, false, false) else {
            return text.chars().count() as f32 * size * 0.56;
        };
        let wide: Vec<u16> = text.encode_utf16().collect();
        unsafe {
            let Ok(layout) = self.gpu.dwrite_factory.CreateTextLayout(&wide, &fmt, 2000.0, 200.0) else {
                return text.chars().count() as f32 * size * 0.56;
            };
            let mut metrics = DWRITE_TEXT_METRICS::default();
            if layout.GetMetrics(&mut metrics).is_ok() {
                metrics.widthIncludingTrailingWhitespace.ceil()
            } else {
                text.chars().count() as f32 * size * 0.56
            }
        }
    }

    pub fn draw_cached_icon(&self, icon_key: &str, r: RectF, radius: f32) -> bool {
        let Some(bmp) = self.gpu.create_bitmap_for_key(self.dc, icon_key) else {
            return false;
        };
        let clipped = if radius > 0.5 { self.push_rounded_clip(r, radius) } else { false };
        unsafe {
            ID2D1RenderTarget::DrawBitmap(
                self.dc,
                &bmp,
                Some(&r.to_d2d()),
                1.0,
                D2D1_BITMAP_INTERPOLATION_MODE_LINEAR,
                None,
            );
        }
        if clipped {
            self.pop_rounded_clip();
        }
        true
    }

    /// Crisp filled Direct2D telemetry sparkline curve.
    pub fn draw_sparkline(&self, samples: &[f32], r: RectF, stroke_color: Color, fill_alpha: f32) {
        if samples.len() < 2 || r.w <= 4.0 || r.h <= 4.0 {
            return;
        }
        let max_v = samples.iter().copied().fold(10.0f32, f32::max).max(1.0);
        let step_x = r.w / (samples.len() - 1) as f32;
        let pts: Vec<D2D_POINT_2F> = samples
            .iter()
            .enumerate()
            .map(|(i, &v)| {
                let norm = (v / max_v).clamp(0.0, 1.0);
                D2D_POINT_2F {
                    x: r.x + i as f32 * step_x,
                    y: r.bottom() - norm * (r.h - 4.0) - 2.0,
                }
            })
            .collect();

        unsafe {
            // Area fill
            if let Ok(area_geo) = self.gpu.d2d_factory.CreatePathGeometry() {
                if let Ok(sink) = area_geo.Open() {
                    sink.BeginFigure(D2D_POINT_2F { x: r.x, y: r.bottom() }, D2D1_FIGURE_BEGIN_FILLED);
                    for &pt in &pts {
                        sink.AddLine(pt);
                    }
                    sink.AddLine(D2D_POINT_2F { x: r.right(), y: r.bottom() });
                    sink.EndFigure(D2D1_FIGURE_END_CLOSED);
                    let _ = sink.Close();
                    let stops = [
                        D2D1_GRADIENT_STOP { position: 0.0, color: to_d2d_color(stroke_color.with_alpha(fill_alpha)) },
                        D2D1_GRADIENT_STOP { position: 1.0, color: to_d2d_color(stroke_color.with_alpha(0.02)) },
                    ];
                    if let Ok(coll) = ID2D1RenderTarget::CreateGradientStopCollection(self.dc, &stops, D2D1_GAMMA_2_2, D2D1_EXTEND_MODE_CLAMP) {
                        let props = D2D1_LINEAR_GRADIENT_BRUSH_PROPERTIES {
                            startPoint: D2D_POINT_2F { x: r.x, y: r.y },
                            endPoint: D2D_POINT_2F { x: r.x, y: r.bottom() },
                        };
                        if let Ok(brush) = self.dc.CreateLinearGradientBrush(&props, None, &coll) {
                            self.dc.FillGeometry(&area_geo, &brush, None);
                        }
                    }
                }
            }
            // Crisp stroke line
            if let Ok(line_geo) = self.gpu.d2d_factory.CreatePathGeometry() {
                if let Ok(sink) = line_geo.Open() {
                    sink.BeginFigure(pts[0], D2D1_FIGURE_BEGIN_HOLLOW);
                    for &pt in &pts[1..] {
                        sink.AddLine(pt);
                    }
                    sink.EndFigure(D2D1_FIGURE_END_OPEN);
                    let _ = sink.Close();
                    if let Some(sb) = self.solid_brush(stroke_color) {
                        self.dc.DrawGeometry(&line_geo, &sb, 1.8, Some(&self.gpu.round_stroke));
                    }
                }
            }
        }
    }

    /// Pixel-exact scannable QR code renderer directly on GPU.
    pub fn draw_qr_matrix(&self, modules: &[bool], size: usize, r: RectF, fg: Color, bg: Color) {
        if size == 0 || modules.len() < size * size { return; }
        self.fill_rounded_rect(r, 10.0, bg);
        let pad = 8.0;
        let inner = r.inset(pad, pad);
        let cell = (inner.w.min(inner.h) / size as f32).floor().max(1.0);
        let ox = inner.x + (inner.w - cell * size as f32) * 0.5;
        let oy = inner.y + (inner.h - cell * size as f32) * 0.5;
        let Some(brush) = self.solid_brush(fg) else { return };
        unsafe {
            for row in 0..size {
                for col in 0..size {
                    if modules[row * size + col] {
                        let cr = D2D_RECT_F {
                            left: ox + col as f32 * cell,
                            top: oy + row as f32 * cell,
                            right: ox + (col + 1) as f32 * cell,
                            bottom: oy + (row + 1) as f32 * cell,
                        };
                        self.dc.FillRectangle(&cr, &brush);
                    }
                }
            }
        }
    }

    /// Crisp 24x24 normalized vector icon engine rendered natively on Direct2D.
    pub fn draw_icon(&self, kind: IconKind, cx: f32, cy: f32, size: f32, color: Color) {
        if color.a <= 0.001 || size <= 2.0 { return; }
        let s = size / 24.0;
        let ox = cx - size * 0.5;
        let oy = cy - size * 0.5;
        let sw = (1.85 * s).max(1.2);
        let p = |x: f32, y: f32| (ox + x * s, oy + y * s);
        let ln = |x1: f32, y1: f32, x2: f32, y2: f32| {
            let (a, b) = p(x1, y1);
            let (c, d) = p(x2, y2);
            self.draw_line(a, b, c, d, color, sw, false);
        };

        match kind {
            IconKind::Search => {
                let (x, y) = p(11.0, 11.0);
                self.stroke_circle(x, y, 6.8 * s, color, sw, false);
                ln(16.2, 16.2, 20.5, 20.5);
            }
            IconKind::Bolt => {
                self.stroke_polyline(&[p(13.0, 2.5), p(4.5, 13.5), p(11.5, 13.5), p(10.5, 21.5), p(19.5, 10.5), p(12.5, 10.5), p(13.0, 2.5)], color, sw);
            }
            IconKind::Wifi => {
                let (x, y) = p(12.0, 19.2);
                self.fill_circle(x, y, 1.5 * s, color);
                self.stroke_arc(ox + 12.0 * s, oy + 19.0 * s, 6.0 * s, -PI * 0.78, -PI * 0.22, color, sw);
                self.stroke_arc(ox + 12.0 * s, oy + 19.0 * s, 10.5 * s, -PI * 0.78, -PI * 0.22, color, sw);
            }
            IconKind::Bluetooth => {
                self.stroke_polyline(&[p(7.5, 7.5), p(16.5, 16.5), p(12.0, 21.0), p(12.0, 3.0), p(16.5, 7.5), p(7.5, 16.5)], color, sw);
            }
            IconKind::Sun => {
                let (x, y) = p(12.0, 12.0);
                self.stroke_circle(x, y, 4.2 * s, color, sw, false);
                for deg in (0..360).step_by(45) {
                    let rad = (deg as f32).to_radians();
                    let (r1, r2) = (7.2 * s, 9.6 * s);
                    self.draw_line(x + rad.cos() * r1, y + rad.sin() * r1, x + rad.cos() * r2, y + rad.sin() * r2, color, sw, false);
                }
            }
            IconKind::Moon | IconKind::Sleep => {
                self.stroke_arc(ox + 12.5 * s, oy + 12.0 * s, 7.5 * s, 0.35, PI * 1.65, color, sw);
                self.stroke_arc(ox + 15.8 * s, oy + 9.2 * s, 5.8 * s, PI * 0.6, PI * 1.42, color, sw);
            }
            IconKind::Volume => {
                self.stroke_polyline(&[p(11.0, 5.5), p(6.5, 9.0), p(3.0, 9.0), p(3.0, 15.0), p(6.5, 15.0), p(11.0, 18.5), p(11.0, 5.5)], color, sw);
                self.stroke_arc(ox + 12.0 * s, oy + 12.0 * s, 4.2 * s, -0.75, 0.75, color, sw);
                self.stroke_arc(ox + 12.0 * s, oy + 12.0 * s, 7.8 * s, -0.82, 0.82, color, sw);
            }
            IconKind::Mute => {
                self.stroke_polyline(&[p(11.0, 5.5), p(6.5, 9.0), p(3.0, 9.0), p(3.0, 15.0), p(6.5, 15.0), p(11.0, 18.5), p(11.0, 5.5)], color, sw);
                ln(15.5, 9.0, 21.0, 14.5);
                ln(21.0, 9.0, 15.5, 14.5);
            }
            IconKind::Mic => {
                self.stroke_rounded_rect(RectF::new(ox + 9.0 * s, oy + 3.0 * s, 6.0 * s, 11.0 * s), 3.0 * s, color, sw, false);
                self.stroke_arc(ox + 12.0 * s, oy + 11.5 * s, 6.5 * s, 0.05, PI - 0.05, color, sw);
                ln(12.0, 18.0, 12.0, 21.5);
                ln(8.5, 21.5, 15.5, 21.5);
            }
            IconKind::MicOff => {
                self.stroke_rounded_rect(RectF::new(ox + 9.0 * s, oy + 3.0 * s, 6.0 * s, 11.0 * s), 3.0 * s, color, sw, false);
                ln(4.0, 4.0, 20.0, 20.0);
                ln(12.0, 18.0, 12.0, 21.5);
            }
            IconKind::Headphones => {
                self.stroke_arc(ox + 12.0 * s, oy + 13.0 * s, 8.5 * s, PI, PI * 2.0, color, sw);
                self.stroke_rounded_rect(RectF::new(ox + 3.0 * s, oy + 13.0 * s, 4.2 * s, 7.0 * s), 1.8 * s, color, sw, false);
                self.stroke_rounded_rect(RectF::new(ox + 16.8 * s, oy + 13.0 * s, 4.2 * s, 7.0 * s), 1.8 * s, color, sw, false);
            }
            IconKind::Bell => {
                self.stroke_arc(ox + 12.0 * s, oy + 9.5 * s, 5.8 * s, PI, PI * 2.0, color, sw);
                ln(6.2, 9.5, 4.5, 17.0);
                ln(17.8, 9.5, 19.5, 17.0);
                ln(4.0, 17.0, 20.0, 17.0);
                let (x, y) = p(12.0, 20.2);
                self.fill_circle(x, y, 1.5 * s, color);
            }
            IconKind::Pin => {
                self.stroke_polyline(&[p(8.5, 4.0), p(15.5, 4.0), p(14.5, 11.0), p(18.0, 14.5), p(6.0, 14.5), p(9.5, 11.0), p(8.5, 4.0)], color, sw);
                ln(12.0, 14.5, 12.0, 21.5);
            }
            IconKind::Close => {
                ln(6.0, 6.0, 18.0, 18.0);
                ln(18.0, 6.0, 6.0, 18.0);
            }
            IconKind::Minimize => { ln(5.0, 12.0, 19.0, 12.0); }
            IconKind::Maximize => {
                self.stroke_rounded_rect(RectF::new(ox + 4.5 * s, oy + 4.5 * s, 15.0 * s, 15.0 * s), 2.5 * s, color, sw, false);
            }
            IconKind::SnapLeft => {
                self.stroke_rounded_rect(RectF::new(ox + 3.0 * s, oy + 4.5 * s, 18.0 * s, 15.0 * s), 2.5 * s, color, sw, false);
                self.fill_rounded_rect(RectF::new(ox + 4.5 * s, oy + 6.0 * s, 7.2 * s, 12.0 * s), 1.2 * s, color.with_alpha(color.a * 0.45));
                ln(12.0, 4.5, 12.0, 19.5);
            }
            IconKind::SnapRight => {
                self.stroke_rounded_rect(RectF::new(ox + 3.0 * s, oy + 4.5 * s, 18.0 * s, 15.0 * s), 2.5 * s, color, sw, false);
                self.fill_rounded_rect(RectF::new(ox + 12.3 * s, oy + 6.0 * s, 7.2 * s, 12.0 * s), 1.2 * s, color.with_alpha(color.a * 0.45));
                ln(12.0, 4.5, 12.0, 19.5);
            }
            IconKind::SnapCenter => {
                self.stroke_rounded_rect(RectF::new(ox + 3.0 * s, oy + 4.5 * s, 18.0 * s, 15.0 * s), 2.5 * s, color, sw, false);
                self.fill_rounded_rect(RectF::new(ox + 7.2 * s, oy + 7.5 * s, 9.6 * s, 9.0 * s), 1.5 * s, color.with_alpha(color.a * 0.45));
            }
            IconKind::Play => {
                self.fill_triangle(p(7.0, 4.5), p(19.5, 12.0), p(7.0, 19.5), color);
            }
            IconKind::Pause => {
                self.fill_rounded_rect(RectF::new(ox + 6.0 * s, oy + 4.5 * s, 4.0 * s, 15.0 * s), 1.2 * s, color);
                self.fill_rounded_rect(RectF::new(ox + 14.0 * s, oy + 4.5 * s, 4.0 * s, 15.0 * s), 1.2 * s, color);
            }
            IconKind::Prev => {
                self.fill_triangle(p(19.0, 5.0), p(9.5, 12.0), p(19.0, 19.0), color);
                ln(5.5, 5.5, 5.5, 18.5);
            }
            IconKind::Next => {
                self.fill_triangle(p(5.0, 5.0), p(14.5, 12.0), p(5.0, 19.0), color);
                ln(18.5, 5.5, 18.5, 18.5);
            }
            IconKind::Folder => {
                self.stroke_polyline(&[p(3.0, 6.0), p(9.5, 6.0), p(11.5, 8.5), p(21.0, 8.5), p(21.0, 19.0), p(3.0, 19.0), p(3.0, 6.0)], color, sw);
            }
            IconKind::Copy => {
                self.stroke_rounded_rect(RectF::new(ox + 9.0 * s, oy + 9.0 * s, 11.5 * s, 11.5 * s), 2.0 * s, color, sw, false);
                self.stroke_polyline(&[p(6.0, 15.0), p(4.0, 15.0), p(4.0, 4.0), p(15.0, 4.0), p(15.0, 6.0)], color, sw);
            }
            IconKind::Trash => {
                ln(4.0, 6.5, 20.0, 6.5);
                ln(9.0, 3.5, 15.0, 3.5);
                self.stroke_polyline(&[p(6.5, 6.5), p(7.5, 20.0), p(16.5, 20.0), p(17.5, 6.5)], color, sw);
            }
            IconKind::Settings | IconKind::Palette => {
                let (x, y) = p(12.0, 12.0);
                self.stroke_circle(x, y, 7.5 * s, color, sw, false);
                self.stroke_circle(x, y, 2.8 * s, color, sw, false);
            }
            IconKind::Sliders => {
                ln(4.0, 7.0, 20.0, 7.0);
                ln(4.0, 12.0, 20.0, 12.0);
                ln(4.0, 17.0, 20.0, 17.0);
                let (a, b) = p(9.0, 7.0); self.fill_circle(a, b, 2.0 * s, color);
                let (a, b) = p(15.0, 12.0); self.fill_circle(a, b, 2.0 * s, color);
                let (a, b) = p(11.0, 17.0); self.fill_circle(a, b, 2.0 * s, color);
            }
            IconKind::Layers => {
                self.stroke_polyline(&[p(12.0, 3.5), p(21.0, 8.0), p(12.0, 12.5), p(3.0, 8.0), p(12.0, 3.5)], color, sw);
                self.stroke_polyline(&[p(3.0, 12.5), p(12.0, 17.0), p(21.0, 12.5)], color, sw);
                self.stroke_polyline(&[p(3.0, 16.5), p(12.0, 21.0), p(21.0, 16.5)], color, sw);
            }
            IconKind::Activity => {
                self.stroke_polyline(&[p(2.5, 12.0), p(6.5, 12.0), p(9.5, 4.5), p(14.5, 19.5), p(17.5, 12.0), p(21.5, 12.0)], color, sw);
            }
            IconKind::Power => {
                self.stroke_arc(ox + 12.0 * s, oy + 13.0 * s, 7.2 * s, -0.85, PI + 0.85, color, sw);
                ln(12.0, 3.0, 12.0, 11.5);
            }
            IconKind::Lock => {
                self.stroke_rounded_rect(RectF::new(ox + 5.0 * s, oy + 11.0 * s, 14.0 * s, 9.5 * s), 2.5 * s, color, sw, false);
                self.stroke_arc(ox + 12.0 * s, oy + 11.0 * s, 4.2 * s, PI, PI * 2.0, color, sw);
            }
            IconKind::Restart | IconKind::Refresh => {
                self.stroke_arc(ox + 12.0 * s, oy + 12.0 * s, 7.5 * s, 0.3, PI * 1.75, color, sw);
                self.stroke_polyline(&[p(16.0, 4.0), p(19.5, 7.5), p(15.0, 8.5)], color, sw);
            }
            IconKind::Calendar => {
                self.stroke_rounded_rect(RectF::new(ox + 3.5 * s, oy + 5.0 * s, 17.0 * s, 15.5 * s), 2.5 * s, color, sw, false);
                ln(3.5, 10.0, 20.5, 10.0);
                ln(8.0, 3.0, 8.0, 6.5);
                ln(16.0, 3.0, 16.0, 6.5);
            }
            IconKind::Globe => {
                let (x, y) = p(12.0, 12.0);
                self.stroke_circle(x, y, 8.5 * s, color, sw, false);
                ln(3.5, 12.0, 20.5, 12.0);
                ln(12.0, 3.5, 12.0, 20.5);
            }
            IconKind::Inbox | IconKind::Zip => {
                self.stroke_rounded_rect(RectF::new(ox + 3.5 * s, oy + 5.0 * s, 17.0 * s, 14.5 * s), 2.5 * s, color, sw, false);
                self.stroke_polyline(&[p(3.5, 12.0), p(9.0, 12.0), p(10.5, 14.5), p(13.5, 14.5), p(15.0, 12.0), p(20.5, 12.0)], color, sw);
            }
            IconKind::Sparkles => {
                self.stroke_polyline(&[p(12.0, 3.0), p(14.0, 10.0), p(21.0, 12.0), p(14.0, 14.0), p(12.0, 21.0), p(10.0, 14.0), p(3.0, 12.0), p(10.0, 10.0), p(12.0, 3.0)], color, sw);
            }
            IconKind::Coffee => {
                self.stroke_rounded_rect(RectF::new(ox + 4.0 * s, oy + 8.0 * s, 12.0 * s, 10.5 * s), 3.0 * s, color, sw, false);
                self.stroke_arc(ox + 16.0 * s, oy + 12.5 * s, 3.2 * s, -PI * 0.5, PI * 0.5, color, sw);
                ln(7.5, 3.5, 7.5, 5.5);
                ln(12.0, 3.5, 12.0, 5.5);
            }
            IconKind::Scissors => {
                let (a, b) = p(6.5, 7.0); self.stroke_circle(a, b, 2.6 * s, color, sw, false);
                let (a, b) = p(6.5, 17.0); self.stroke_circle(a, b, 2.6 * s, color, sw, false);
                ln(8.8, 8.5, 19.5, 18.0);
                ln(8.8, 15.5, 19.5, 6.0);
            }
            IconKind::Pipette => {
                ln(4.0, 20.0, 16.0, 8.0);
                let (x, y) = p(17.5, 6.5);
                self.stroke_circle(x, y, 2.8 * s, color, sw, false);
            }
            IconKind::Record => {
                let (x, y) = p(12.0, 12.0);
                self.stroke_circle(x, y, 8.0 * s, color, sw, false);
                self.fill_circle(x, y, 3.8 * s, color);
            }
            IconKind::Qr => {
                self.stroke_rounded_rect(RectF::new(ox + 3.5 * s, oy + 3.5 * s, 6.5 * s, 6.5 * s), 1.0 * s, color, sw, false);
                self.stroke_rounded_rect(RectF::new(ox + 14.0 * s, oy + 3.5 * s, 6.5 * s, 6.5 * s), 1.0 * s, color, sw, false);
                self.stroke_rounded_rect(RectF::new(ox + 3.5 * s, oy + 14.0 * s, 6.5 * s, 6.5 * s), 1.0 * s, color, sw, false);
                self.fill_rect(RectF::new(ox + 14.0 * s, oy + 14.0 * s, 3.0 * s, 3.0 * s), color);
                self.fill_rect(RectF::new(ox + 18.0 * s, oy + 18.0 * s, 3.0 * s, 3.0 * s), color);
            }
            IconKind::Camera => {
                self.stroke_rounded_rect(RectF::new(ox + 3.0 * s, oy + 7.5 * s, 18.0 * s, 12.5 * s), 2.5 * s, color, sw, false);
                let (x, y) = p(12.0, 13.8);
                self.stroke_circle(x, y, 3.5 * s, color, sw, false);
                ln(8.5, 7.5, 10.0, 4.8);
                ln(10.0, 4.8, 14.0, 4.8);
                ln(14.0, 4.8, 15.5, 7.5);
            }
            IconKind::Battery => {
                self.stroke_rounded_rect(RectF::new(ox + 2.5 * s, oy + 7.0 * s, 17.0 * s, 10.0 * s), 2.0 * s, color, sw, false);
                ln(21.5, 10.0, 21.5, 14.0);
                self.fill_rounded_rect(RectF::new(ox + 5.0 * s, oy + 9.5 * s, 9.0 * s, 5.0 * s), 1.0 * s, color);
            }
            IconKind::Mouse => {
                self.stroke_rounded_rect(RectF::new(ox + 6.5 * s, oy + 3.5 * s, 11.0 * s, 17.0 * s), 5.5 * s, color, sw, false);
                ln(12.0, 7.0, 12.0, 10.5);
            }
            IconKind::Keyboard => {
                self.stroke_rounded_rect(RectF::new(ox + 2.5 * s, oy + 6.0 * s, 19.0 * s, 12.0 * s), 2.2 * s, color, sw, false);
                ln(7.5, 14.5, 16.5, 14.5);
            }
            IconKind::ExtLink => {
                self.stroke_polyline(&[p(14.0, 4.0), p(20.0, 4.0), p(20.0, 10.0)], color, sw);
                ln(10.5, 13.5, 20.0, 4.0);
                self.stroke_polyline(&[p(11.0, 6.0), p(5.0, 6.0), p(5.0, 19.0), p(18.0, 19.0), p(18.0, 13.0)], color, sw);
            }
            IconKind::Monitor | IconKind::Desktop => {
                self.stroke_rounded_rect(RectF::new(ox + 2.5 * s, oy + 3.5 * s, 19.0 * s, 13.5 * s), 2.2 * s, color, sw, false);
                ln(12.0, 17.0, 12.0, 20.5);
                ln(8.0, 20.5, 16.0, 20.5);
            }
            IconKind::ChevronUp => self.stroke_polyline(&[p(6.5, 15.0), p(12.0, 9.0), p(17.5, 15.0)], color, sw),
            IconKind::ChevronDown => self.stroke_polyline(&[p(6.5, 9.0), p(12.0, 15.0), p(17.5, 9.0)], color, sw),
            IconKind::ChevronLeft => self.stroke_polyline(&[p(15.0, 6.5), p(9.0, 12.0), p(15.0, 17.5)], color, sw),
            IconKind::ChevronRight => self.stroke_polyline(&[p(9.0, 6.5), p(15.0, 12.0), p(9.0, 17.5)], color, sw),
            IconKind::Plus => {
                ln(12.0, 5.0, 12.0, 19.0);
                ln(5.0, 12.0, 19.0, 12.0);
            }
            IconKind::Grid => {
                self.stroke_rounded_rect(RectF::new(ox + 3.5 * s, oy + 3.5 * s, 7.0 * s, 7.0 * s), 1.5 * s, color, sw, false);
                self.stroke_rounded_rect(RectF::new(ox + 13.5 * s, oy + 3.5 * s, 7.0 * s, 7.0 * s), 1.5 * s, color, sw, false);
                self.stroke_rounded_rect(RectF::new(ox + 3.5 * s, oy + 13.5 * s, 7.0 * s, 7.0 * s), 1.5 * s, color, sw, false);
                self.stroke_rounded_rect(RectF::new(ox + 13.5 * s, oy + 13.5 * s, 7.0 * s, 7.0 * s), 1.5 * s, color, sw, false);
            }
            IconKind::Clock => {
                let (x, y) = p(12.0, 12.0);
                self.stroke_circle(x, y, 8.2 * s, color, sw, false);
                ln(12.0, 7.5, 12.0, 12.0);
                ln(12.0, 12.0, 15.5, 14.0);
            }
            IconKind::Cloud => {
                self.stroke_rounded_rect(RectF::new(ox + 4.0 * s, oy + 10.0 * s, 16.0 * s, 8.0 * s), 4.0 * s, color, sw, false);
                self.stroke_arc(ox + 11.5 * s, oy + 10.0 * s, 4.5 * s, PI, PI * 2.0, color, sw);
            }
            IconKind::Clip => {
                self.stroke_rounded_rect(RectF::new(ox + 5.0 * s, oy + 5.0 * s, 14.0 * s, 16.0 * s), 2.2 * s, color, sw, false);
                self.stroke_rounded_rect(RectF::new(ox + 8.5 * s, oy + 2.5 * s, 7.0 * s, 4.0 * s), 1.0 * s, color, sw, false);
            }
            IconKind::Note => {
                ln(12.0, 20.0, 20.5, 20.0);
                self.stroke_polyline(&[p(16.0, 4.0), p(20.0, 8.0), p(8.0, 20.0), p(4.0, 20.0), p(4.0, 16.0), p(16.0, 4.0)], color, sw);
            }
            IconKind::Check => {
                self.stroke_polyline(&[p(5.0, 12.5), p(10.0, 17.5), p(19.5, 6.5)], color, sw);
            }
        }
    }

    fn stroke_polyline(&self, pts: &[(f32, f32)], color: Color, width: f32) {
        if pts.len() < 2 { return; }
        unsafe {
            let Ok(geo) = self.gpu.d2d_factory.CreatePathGeometry() else { return };
            let Ok(sink) = geo.Open() else { return };
            sink.BeginFigure(D2D_POINT_2F { x: pts[0].0, y: pts[0].1 }, D2D1_FIGURE_BEGIN_HOLLOW);
            for &(x, y) in &pts[1..] {
                sink.AddLine(D2D_POINT_2F { x, y });
            }
            sink.EndFigure(D2D1_FIGURE_END_OPEN);
            let _ = sink.Close();
            if let Some(b) = self.solid_brush(color) {
                self.dc.DrawGeometry(&geo, &b, width, Some(&self.gpu.round_stroke));
            }
        }
    }

    fn fill_triangle(&self, a: (f32, f32), b: (f32, f32), c: (f32, f32), color: Color) {
        unsafe {
            let Ok(geo) = self.gpu.d2d_factory.CreatePathGeometry() else { return };
            let Ok(sink) = geo.Open() else { return };
            sink.BeginFigure(D2D_POINT_2F { x: a.0, y: a.1 }, D2D1_FIGURE_BEGIN_FILLED);
            sink.AddLine(D2D_POINT_2F { x: b.0, y: b.1 });
            sink.AddLine(D2D_POINT_2F { x: c.0, y: c.1 });
            sink.EndFigure(D2D1_FIGURE_END_CLOSED);
            let _ = sink.Close();
            if let Some(br) = self.solid_brush(color) {
                self.dc.FillGeometry(&geo, &br, None);
            }
        }
    }

    #[allow(clippy::too_many_arguments)]
    fn stroke_arc(&self, cx: f32, cy: f32, r: f32, start_rad: f32, end_rad: f32, color: Color, width: f32) {
        let steps = 16;
        let mut pts = Vec::with_capacity(steps + 1);
        for i in 0..=steps {
            let t = i as f32 / steps as f32;
            let ang = start_rad + (end_rad - start_rad) * t;
            pts.push((cx + ang.cos() * r, cy + ang.sin() * r));
        }
        self.stroke_polyline(&pts, color, width);
    }
}
