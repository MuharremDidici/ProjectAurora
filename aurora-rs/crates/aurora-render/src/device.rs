use std::collections::HashMap;
use anyhow::{Context, Result};
use parking_lot::Mutex;
use windows::core::{Interface, HSTRING};
use windows::Win32::Graphics::Direct2D::{
    D2D1CreateFactory, ID2D1Bitmap1, ID2D1Device, ID2D1DeviceContext, ID2D1Factory, ID2D1Factory1,
    ID2D1StrokeStyle, D2D1_BITMAP_OPTIONS_NONE, D2D1_BITMAP_PROPERTIES1, D2D1_CAP_STYLE_ROUND,
    D2D1_DASH_STYLE_CUSTOM, D2D1_FACTORY_OPTIONS, D2D1_FACTORY_TYPE_SINGLE_THREADED,
    D2D1_LINE_JOIN_ROUND, D2D1_STROKE_STYLE_PROPERTIES,
};
use windows::Win32::Graphics::Direct2D::Common::{D2D1_ALPHA_MODE_PREMULTIPLIED, D2D1_PIXEL_FORMAT};
use windows::Win32::Graphics::Direct3D::{
    D3D_DRIVER_TYPE_HARDWARE, D3D_DRIVER_TYPE_WARP, D3D_FEATURE_LEVEL_11_0, D3D_FEATURE_LEVEL_11_1,
};
use windows::Win32::Graphics::Direct3D11::{
    D3D11CreateDevice, ID3D11Device, D3D11_CREATE_DEVICE_BGRA_SUPPORT, D3D11_SDK_VERSION,
};
use windows::Win32::Graphics::DirectComposition::{DCompositionCreateDevice, IDCompositionDevice};
use windows::Win32::Graphics::DirectWrite::{
    DWriteCreateFactory, IDWriteFactory, IDWriteTextFormat, DWRITE_FACTORY_TYPE_SHARED,
    DWRITE_FONT_STRETCH_NORMAL, DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_WEIGHT,
    DWRITE_PARAGRAPH_ALIGNMENT_CENTER, DWRITE_PARAGRAPH_ALIGNMENT_NEAR, DWRITE_TEXT_ALIGNMENT_CENTER,
    DWRITE_TEXT_ALIGNMENT_LEADING, DWRITE_TEXT_ALIGNMENT_TRAILING, DWRITE_TRIMMING,
    DWRITE_TRIMMING_GRANULARITY_CHARACTER, DWRITE_WORD_WRAPPING_NO_WRAP, DWRITE_WORD_WRAPPING_WRAP,
};
use windows::Win32::Graphics::Dxgi::Common::DXGI_FORMAT_B8G8R8A8_UNORM;
use windows::Win32::Graphics::Dxgi::{IDXGIAdapter, IDXGIDevice, IDXGIFactory2};
use windows::Win32::Graphics::Imaging::{CLSID_WICImagingFactory, IWICImagingFactory};
use windows::Win32::System::Com::{CoCreateInstance, CLSCTX_INPROC_SERVER};

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum TextAlign {
    Left,
    Center,
    Right,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum FontFamily {
    Ui,
    Mono,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
struct FontKey {
    family: FontFamily,
    size_tenths: u16,
    weight: u16,
    align: TextAlign,
    v_center: bool,
    wrap: bool,
}

/// Single shared D3D11 + Direct2D 1.3 + DirectWrite + DirectComposition + WIC GPU context
/// across ALL monitors and ALL Aurora surfaces (zero Chromium/WebView2 overhead!).
pub struct GpuContext {
    pub d3d_device: ID3D11Device,
    pub d3d_ctx: windows::Win32::Graphics::Direct3D11::ID3D11DeviceContext,
    pub dxgi_device: IDXGIDevice,
    pub dxgi_factory: IDXGIFactory2,
    pub d2d_factory: ID2D1Factory1,
    pub d2d_device: ID2D1Device,
    pub dwrite_factory: IDWriteFactory,
    pub dcomp_device: IDCompositionDevice,
    pub wic_factory: IWICImagingFactory,
    pub round_stroke: ID2D1StrokeStyle,
    pub dashed_stroke: ID2D1StrokeStyle,
    font_cache: Mutex<HashMap<FontKey, IDWriteTextFormat>>,
    /// Cached premultiplied BGRA raw icon pixel buffers (uploaded per device context lazily).
    icon_pixels: Mutex<HashMap<String, CachedPixelImage>>,
}

#[derive(Debug, Clone)]
pub struct CachedPixelImage {
    pub width: u32,
    pub height: u32,
    pub bgra_premultiplied: Vec<u8>,
}

impl GpuContext {
    pub fn new() -> Result<Self> {
        unsafe {
            let feature_levels = [D3D_FEATURE_LEVEL_11_1, D3D_FEATURE_LEVEL_11_0];
            let mut d3d_device: Option<ID3D11Device> = None;
            let mut d3d_ctx: Option<windows::Win32::Graphics::Direct3D11::ID3D11DeviceContext> = None;

            let hr = D3D11CreateDevice(
                None,
                D3D_DRIVER_TYPE_HARDWARE,
                None,
                D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                Some(&feature_levels),
                D3D11_SDK_VERSION,
                Some(&mut d3d_device),
                None,
                Some(&mut d3d_ctx),
            );
            if hr.is_err() {
                D3D11CreateDevice(
                    None,
                    D3D_DRIVER_TYPE_WARP,
                    None,
                    D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                    Some(&feature_levels),
                    D3D11_SDK_VERSION,
                    Some(&mut d3d_device),
                    None,
                    Some(&mut d3d_ctx),
                )
                .context("D3D11CreateDevice (Hardware & WARP fallback) failed")?;
            }
            let d3d_device = d3d_device.context("D3D11 device handle missing")?;
            let d3d_ctx = d3d_ctx.context("D3D11 device context missing")?;
            let dxgi_device: IDXGIDevice = d3d_device.cast()?;
            let dxgi_adapter: IDXGIAdapter = dxgi_device.GetAdapter()?;
            let dxgi_factory: IDXGIFactory2 = dxgi_adapter.GetParent()?;

            let d2d_opts = D2D1_FACTORY_OPTIONS::default();
            let d2d_factory: ID2D1Factory1 =
                D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, Some(&d2d_opts))?;
            let d2d_device = d2d_factory.CreateDevice(&dxgi_device)?;

            let dwrite_factory: IDWriteFactory = DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED)?;
            let dcomp_device: IDCompositionDevice = DCompositionCreateDevice(&dxgi_device)?;
            let wic_factory: IWICImagingFactory =
                CoCreateInstance(&CLSID_WICImagingFactory, None, CLSCTX_INPROC_SERVER)?;

            let round_props = D2D1_STROKE_STYLE_PROPERTIES {
                startCap: D2D1_CAP_STYLE_ROUND,
                endCap: D2D1_CAP_STYLE_ROUND,
                lineJoin: D2D1_LINE_JOIN_ROUND,
                ..Default::default()
            };
            let round_stroke = ID2D1Factory::CreateStrokeStyle(&d2d_factory, &round_props, None)?;

            let dash_props = D2D1_STROKE_STYLE_PROPERTIES {
                startCap: D2D1_CAP_STYLE_ROUND,
                endCap: D2D1_CAP_STYLE_ROUND,
                lineJoin: D2D1_LINE_JOIN_ROUND,
                dashStyle: D2D1_DASH_STYLE_CUSTOM,
                ..Default::default()
            };
            let dashed_stroke = ID2D1Factory::CreateStrokeStyle(&d2d_factory, &dash_props, Some(&[3.5, 3.0]))?;

            Ok(Self {
                d3d_device,
                d3d_ctx,
                dxgi_device,
                dxgi_factory,
                d2d_factory,
                d2d_device,
                dwrite_factory,
                dcomp_device,
                wic_factory,
                round_stroke,
                dashed_stroke,
                font_cache: Mutex::new(HashMap::new()),
                icon_pixels: Mutex::new(HashMap::new()),
            })
        }
    }

    pub fn text_format(
        &self,
        family: FontFamily,
        size_dip: f32,
        weight: u16,
        align: TextAlign,
        v_center: bool,
        wrap: bool,
    ) -> Result<IDWriteTextFormat> {
        let key = FontKey {
            family,
            size_tenths: (size_dip.clamp(6.0, 96.0) * 10.0).round() as u16,
            weight,
            align,
            v_center,
            wrap,
        };
        let mut cache = self.font_cache.lock();
        if let Some(fmt) = cache.get(&key) {
            return Ok(fmt.clone());
        }
        let family_name = match family {
            FontFamily::Ui => HSTRING::from("Segoe UI Variable Display"),
            FontFamily::Mono => HSTRING::from("Cascadia Mono"),
        };
        let locale = HSTRING::from("tr-TR");
        unsafe {
            let fmt = self.dwrite_factory.CreateTextFormat(
                &family_name,
                None,
                DWRITE_FONT_WEIGHT(weight as i32),
                DWRITE_FONT_STYLE_NORMAL,
                DWRITE_FONT_STRETCH_NORMAL,
                size_dip.clamp(6.0, 96.0),
                &locale,
            )?;
            let ta = match align {
                TextAlign::Left => DWRITE_TEXT_ALIGNMENT_LEADING,
                TextAlign::Center => DWRITE_TEXT_ALIGNMENT_CENTER,
                TextAlign::Right => DWRITE_TEXT_ALIGNMENT_TRAILING,
            };
            let _ = fmt.SetTextAlignment(ta);
            let _ = fmt.SetParagraphAlignment(if v_center {
                DWRITE_PARAGRAPH_ALIGNMENT_CENTER
            } else {
                DWRITE_PARAGRAPH_ALIGNMENT_NEAR
            });
            let _ = fmt.SetWordWrapping(if wrap {
                DWRITE_WORD_WRAPPING_WRAP
            } else {
                DWRITE_WORD_WRAPPING_NO_WRAP
            });
            if !wrap {
                let ellipsis = self.dwrite_factory.CreateEllipsisTrimmingSign(&fmt)?;
                let trim = DWRITE_TRIMMING {
                    granularity: DWRITE_TRIMMING_GRANULARITY_CHARACTER,
                    delimiter: 0,
                    delimiterCount: 0,
                };
                let _ = fmt.SetTrimming(&trim, &ellipsis);
            }
            cache.insert(key, fmt.clone());
            Ok(fmt)
        }
    }

    pub fn put_icon_pixels(&self, key: impl Into<String>, width: u32, height: u32, bgra_premultiplied: Vec<u8>) {
        if width == 0 || height == 0 || bgra_premultiplied.len() < (width * height * 4) as usize {
            return;
        }
        self.icon_pixels.lock().insert(
            key.into(),
            CachedPixelImage {
                width,
                height,
                bgra_premultiplied,
            },
        );
    }

    pub fn has_icon(&self, key: &str) -> bool {
        self.icon_pixels.lock().contains_key(key)
    }

    pub fn create_bitmap_for_key(&self, dc: &ID2D1DeviceContext, key: &str) -> Option<ID2D1Bitmap1> {
        let guard = self.icon_pixels.lock();
        let img = guard.get(key)?;
        let props = D2D1_BITMAP_PROPERTIES1 {
            pixelFormat: D2D1_PIXEL_FORMAT {
                format: DXGI_FORMAT_B8G8R8A8_UNORM,
                alphaMode: D2D1_ALPHA_MODE_PREMULTIPLIED,
            },
            dpiX: 96.0,
            dpiY: 96.0,
            bitmapOptions: D2D1_BITMAP_OPTIONS_NONE,
            colorContext: std::mem::ManuallyDrop::new(None),
        };
        unsafe {
            dc.CreateBitmap(
                windows::Win32::Graphics::Direct2D::Common::D2D_SIZE_U {
                    width: img.width,
                    height: img.height,
                },
                Some(img.bgra_premultiplied.as_ptr() as *const _),
                img.width * 4,
                &props,
            )
            .ok()
        }
    }
}
