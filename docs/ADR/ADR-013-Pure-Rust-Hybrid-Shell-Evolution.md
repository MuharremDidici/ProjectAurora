# ADR-013: Saf Rust Hibrit Kabuk Mimarisine Geçiş (`v3.0.0` → `v7.2.0`)

- **Durum:** Kabul Edildi ve Canlıda (Supersedes `ADR-001` & `ADR-002`)
- **Tarih:** 2026-11-26

## Bağlam
İlk sürümler (`v0.x–v2.5.1`) `.NET 10` (`Aurora.App.exe`, 220+ harici DLL, ~92 MB kurulum paketi) üzerinde çalışıyordu (`ADR-001`, `ADR-002`). Boşta ~490 MB RAM tüketimi, soğuk açılış gecikmesi ve çoklu WebView2 bellek maliyetini düşürmek amacıyla çekirdek motor `v3.0.0` itibarıyla tamamen **Saf Rust (`aurora-rs/`)** çalışma alanına taşındı; `v4.0–v7.2.0` sürümlerinde ise **10 kabuk mimarisi**, **6 Dinamik Ada stili**, **10 masaüstü widget'ı**, **gerçek Windows 11 sistem tepsisi keşfi**, **Alt+Tab odak bütünlüğü** ve **evrensel tek tık dış kapanma** ile olgunlaştırıldı.

## Alınan Karar
1. **Çalışma Zamanı & İkili:** 8 crate'li `aurora-rs/` workspace'i tek statik `aurora.exe` + Direct2D/zstd-19 gömülü `aurora-setup` üretir (`.NET 10` tamamen kaldırıldı).
2. **Arayüz Katmanı:** `aurora-app` içinde Crown, tüm monitör Dock'ları ve Masaüstü Widget'ları tek paylaşımlı WebView2 (`--process-per-site`, `https://aurora.local/`) üzerinden beslenir; geçici tam ekran araçlar (OCR, Pipet, GIF Bölgesi, Snap Zones) isteğe bağlı Direct2D/DirectComposition (`aurora-render`) kullanır.
3. **Veri & Yükseltme Sürekliliği:** `aurora-storage`, mevcut `v2.x` kullanıcılarının `%LOCALAPPDATA%\ProjectAurora\data\` SQLite (`aurora.db`, `clipboard_vault.db`) ve `shell_settings.json` dosyalarını %100 okuyup yükseltir; `aurora-setup` eski `v2.x` `--update --wait-pid` çağrılarını da destekler.
4. **Kod Deposu Temizliği (`v7.2.0`):** Eski C#/`.NET 10` kaynak klasörleri (`src/`, `tests/`, `installer/`, `Aurora.sln`, `Directory.Build.props`) ana daldan kaldırılmıştır; geçmiş sürümlere Git etiketleri (`v2.0.0`..`v3.0.0`) üzerinden her zaman erişilebilir.
