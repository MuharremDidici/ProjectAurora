use std::path::{Path, PathBuf};

fn main() {
    println!("cargo:rerun-if-changed=../../../assets/aurora.ico");
    println!("cargo:rerun-if-env-changed=AURORA_PAYLOAD_BIN");

    let out_dir = PathBuf::from(std::env::var("OUT_DIR").unwrap());
    let out_zst = out_dir.join("aurora.exe.zst");

    let payload_path = std::env::var_os("AURORA_PAYLOAD_BIN")
        .map(PathBuf::from)
        .filter(|p| p.exists())
        .or_else(|| {
            let p = Path::new("../../target/release/aurora.exe");
            p.exists().then(|| p.to_path_buf())
        });

    if let Some(bin_path) = payload_path {
        println!("cargo:rerun-if-changed={}", bin_path.display());
        let raw = std::fs::read(&bin_path).expect("failed to read aurora.exe payload");
        let compressed = zstd::bulk::compress(&raw, 19).expect("zstd compress failed");
        std::fs::write(&out_zst, compressed).expect("failed to write aurora.exe.zst");
        println!("cargo:rustc-cfg=has_embedded_payload");
    } else {
        std::fs::write(&out_zst, []).expect("failed to write empty payload placeholder");
    }

    println!("cargo:rustc-check-cfg=cfg(has_embedded_payload)");

    let pkg_ver = env!("CARGO_PKG_VERSION");
    let win_ver = {
        let parts: Vec<&str> = pkg_ver.split('.').collect();
        match parts.len() {
            1 => format!("{pkg_ver}.0.0.0"),
            2 => format!("{pkg_ver}.0.0"),
            3 => format!("{pkg_ver}.0"),
            _ => pkg_ver.to_string(),
        }
    };
    let desc = format!("Project Aurora v{pkg_ver} Kurulum Sihirbazı (Sıfır UAC)");

    println!("cargo:rerun-if-changed=../../Cargo.toml");
    let mut res = winresource::WindowsResource::new();
    let ico = Path::new("../../../assets/aurora.ico");
    if ico.exists() {
        res.set_icon(ico.to_str().unwrap());
    }
    res.set("ProductName", "Project Aurora Kurulum");
    res.set("FileDescription", &desc);
    res.set("CompanyName", "Muharrem Didici");
    res.set("ProductVersion", &win_ver);
    res.set("FileVersion", &win_ver);
    let manifest_xml = format!(
        r#"<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
  <assemblyIdentity version="{win_ver}" processorArchitecture="amd64" name="ProjectAurora.Setup" type="win32"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v3">
    <security>
      <requestedPrivileges>
        <requestedExecutionLevel level="asInvoker" uiAccess="false"/>
      </requestedPrivileges>
    </security>
  </trustInfo>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>"#
    );
    res.set_manifest(&manifest_xml);
    let _ = res.compile();
}
