fn main() {
    println!("cargo:rerun-if-changed=../../../assets/aurora.ico");
    let mut res = winresource::WindowsResource::new();
    let ico = std::path::Path::new("../../../assets/aurora.ico");
    if ico.exists() {
        res.set_icon(ico.to_str().unwrap());
    }
    res.set("ProductName", "Project Aurora");
    res.set("FileDescription", "Project Aurora v3.0 — Native Rust GPU Windows 11 Desktop Shell");
    res.set("CompanyName", "Muharrem Didici");
    res.set("LegalCopyright", "MIT License • Project Aurora");
    res.set("ProductVersion", "3.0.0.0");
    res.set("FileVersion", "3.0.0.0");
    res.set_manifest(
        r#"<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
  <assemblyIdentity version="3.0.0.0" processorArchitecture="amd64" name="ProjectAurora.Shell" type="win32"/>
  <description>Project Aurora Native Windows 11 Shell</description>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v3">
    <security>
      <requestedPrivileges>
        <requestedExecutionLevel level="asInvoker" uiAccess="false"/>
      </requestedPrivileges>
    </security>
  </trustInfo>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/PM</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/>
    </application>
  </compatibility>
</assembly>"#,
    );
    if let Err(e) = res.compile() {
        println!("cargo:warning=winresource compile skipped: {e}");
    }
}
