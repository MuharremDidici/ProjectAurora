# Project Aurora — Saf Rust Hibrit Mimari Özeti (`v7.2.0`)

## 1. Workspace Yapısı (`aurora-rs/`, 8 Crate)

```text
aurora-rs/ (Rust Stable + Win32 Native + Direct2D/DirectComposition + Tek Paylaşımlı WebView2)
├── crates/aurora-core/      # Modeller, 20 WCAG AA+ tema, 10 kabuk mimarisi, WorkArea hesabı, omni calc/fuzzy
├── crates/aurora-storage/   # Atomik JSON tercihleri, WAL SQLite (aurora.db, clipboard_vault.db), SHA-256 + GitHub API güncelleyici
├── crates/aurora-render/    # Direct3D 11 / Direct2D 1.3 / DirectComposition motoru (OCR/GIF/Snap + aurora-setup)
├── crates/aurora-ui/        # Taffy Flexbox/Grid yerleşimi, HitTree ve Türkçe UTF-8 giriş yardımcıları
├── crates/aurora-win32/     # Native AppBar, CoreAudio, DDC/CI EDID, Live Bluetooth, GSMTC, Tray/NotifyIconSettings, OCR/GIF/QR
├── crates/aurora-surfaces/  # Yüzey durum modelleri ve görsel captures fixture'ları
├── crates/aurora-app/       # Ana kabuk konağı (`aurora.exe`), tek paylaşımlı WebView2 (`https://aurora.local/`), Watchdog & HTML UI'lar
└── crates/aurora-setup/     # Tek dosya (~3.7 MB) zstd-19 gömülü sıfır-UAC kurulum & kaldırma sihirbazı
```

## 2. Temel Mimari İlkeler

- **Sıfır .NET & Sıfır Harici DLL:** Tek statik `aurora.exe` ve `--watchdog-pid` modunda çalışan sıfır-CPU bekçi süreci; kullanıcı makinelerinde `.NET Runtime` kurulumu gerekmez.
- **Tek Paylaşımlı WebView2 Renderer:** Crown (`surface=0`), çoklu monitör Dock (`dock.html`) ve `HWND_BOTTOM` Masaüstü Widget yüzeyi (`widgets.html`) tek WebView2 ortamını paylaşır (`--process-per-site` / `https://aurora.local/`).
- **Piksel Kesin Tıklama Geçirgenliği (`SetWindowRgn`):** Her WebView2 yüzeyi `ResizeObserver` üzerinden aktif DOM dikdörtgenlerini `aurora-app` tarafına raporlar; boş pikseller `%100` alttaki pencerelere geçer.
- **Kilitlenmez Girdi & Explorer Güvenliği:** `AttachThreadInput` kullanılmaz; `WH_KEYBOARD_LL` / `WH_MOUSE_LL` bloklama yapmaz; `Alt+Tab` (`VK_MENU`) sırasında kabuk bastırma ve `Shell_TrayWnd` gizleme duraklatılır. `explorer.exe` yamalanmaz veya sonlandırılmaz.
- **Acil Durum Kurtarma:** `aurora.exe --restore-taskbar`, `aurora.exe --safe-mode` veya Watchdog (<100 ms) sayesinde Windows görev çubuğu her senaryoda güvendedir.
