use std::path::Path;
use anyhow::Result;
use windows::Win32::Graphics::Direct2D::Common::{D2D1_ALPHA_MODE_PREMULTIPLIED, D2D1_PIXEL_FORMAT};
use windows::Win32::Graphics::Direct2D::{
    D2D1_BITMAP_OPTIONS_CANNOT_DRAW, D2D1_BITMAP_OPTIONS_CPU_READ, D2D1_BITMAP_OPTIONS_TARGET,
    D2D1_BITMAP_PROPERTIES1, D2D1_DEVICE_CONTEXT_OPTIONS_NONE, D2D1_MAP_OPTIONS_READ,
};
use windows::Win32::Graphics::Dxgi::Common::DXGI_FORMAT_B8G8R8A8_UNORM;

use crate::{GpuContext, Painter};

/// Headless GPU Direct2D offscreen PNG renderer for automated pixel-by-pixel verification of ALL 7 themes!
pub fn render_offscreen_to_png<F>(
    gpu: &GpuContext,
    width: u32,
    height: u32,
    dpi: f32,
    out_path: &Path,
    draw_fn: F,
) -> Result<()>
where
    F: FnOnce(&Painter<'_>, f32, f32),
{
    if let Some(parent) = out_path.parent() {
        std::fs::create_dir_all(parent)?;
    }
    unsafe {
        let dc = gpu.d2d_device.CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE)?;
        dc.SetDpi(dpi, dpi);

        let target_props = D2D1_BITMAP_PROPERTIES1 {
            pixelFormat: D2D1_PIXEL_FORMAT {
                format: DXGI_FORMAT_B8G8R8A8_UNORM,
                alphaMode: D2D1_ALPHA_MODE_PREMULTIPLIED,
            },
            dpiX: dpi,
            dpiY: dpi,
            bitmapOptions: D2D1_BITMAP_OPTIONS_TARGET,
            colorContext: std::mem::ManuallyDrop::new(None),
        };
        let gpu_bmp = dc.CreateBitmap(
            windows::Win32::Graphics::Direct2D::Common::D2D_SIZE_U { width, height },
            None,
            0,
            &target_props,
        )?;
        dc.SetTarget(&gpu_bmp);

        let scale = dpi / 96.0;
        let dip_w = width as f32 / scale;
        let dip_h = height as f32 / scale;

        dc.BeginDraw();
        dc.Clear(Some(&windows::Win32::Graphics::Direct2D::Common::D2D1_COLOR_F {
            r: 0.0,
            g: 0.0,
            b: 0.0,
            a: 0.0,
        }));
        let painter = Painter::new(&dc, gpu);
        draw_fn(&painter, dip_w, dip_h);
        dc.EndDraw(None, None)?;

        // Staging CPU-read bitmap
        let cpu_props = D2D1_BITMAP_PROPERTIES1 {
            pixelFormat: D2D1_PIXEL_FORMAT {
                format: DXGI_FORMAT_B8G8R8A8_UNORM,
                alphaMode: D2D1_ALPHA_MODE_PREMULTIPLIED,
            },
            dpiX: dpi,
            dpiY: dpi,
            bitmapOptions: D2D1_BITMAP_OPTIONS_CPU_READ | D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
            colorContext: std::mem::ManuallyDrop::new(None),
        };
        let cpu_bmp = dc.CreateBitmap(
            windows::Win32::Graphics::Direct2D::Common::D2D_SIZE_U { width, height },
            None,
            0,
            &cpu_props,
        )?;
        cpu_bmp.CopyFromBitmap(None, &gpu_bmp, None)?;

        let mapped = cpu_bmp.Map(D2D1_MAP_OPTIONS_READ)?;
        let mut rgba = vec![0u8; (width * height * 4) as usize];
        let pitch = mapped.pitch as usize;
        let src_slice = std::slice::from_raw_parts(mapped.bits, pitch * height as usize);
        for y in 0..height as usize {
            let row = &src_slice[y * pitch..y * pitch + (width as usize * 4)];
            for x in 0..width as usize {
                let b = row[x * 4];
                let g = row[x * 4 + 1];
                let r = row[x * 4 + 2];
                let a = row[x * 4 + 3];
                // Un-premultiply for standard PNG RGBA
                let (ur, ug, ub) = if a > 0 && a < 255 {
                    let af = a as f32 / 255.0;
                    (
                        ((r as f32 / af).min(255.0)) as u8,
                        ((g as f32 / af).min(255.0)) as u8,
                        ((b as f32 / af).min(255.0)) as u8,
                    )
                } else {
                    (r, g, b)
                };
                let dst = (y * width as usize + x) * 4;
                rgba[dst] = ur;
                rgba[dst + 1] = ug;
                rgba[dst + 2] = ub;
                rgba[dst + 3] = a;
            }
        }
        cpu_bmp.Unmap()?;

        let img = image::RgbaImage::from_raw(width, height, rgba)
            .ok_or_else(|| anyhow::anyhow!("Invalid RGBA buffer dimensions"))?;
        img.save(out_path)?;
        Ok(())
    }
}
