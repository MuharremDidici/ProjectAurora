# ADR-012: Çoklu Monitör Kabuk Mimarisi, Tam Ekran Otomatik Gizlenme, DPI-Bağımsız Tıklama ve Gerçek Windows 11 Sistem Tepsisi Keşfi

- **Durum:** Kabul Edildi ve Uygulandı (`v1.0`)
- **Tarih:** 2026-11-19
- **İlişkili Kararlar:** `ADR-002`, `ADR-005`, `ADR-011`

## 1. Bağlam ve Çözülen Gerçek Kullanım Sorunları
Üç monitörlü karma çözünürlük/DPI kurulumunda (`2560x1440`, `2048x1152 @ +2560,-95`, `1280x1024 @ -1280,+200`) yapılan canlı saha testlerinde beş temel gereksinim belirlenmiştir:

1. **Tam Ekran Video / Oyun Çakışması:** Sabit `HWND_TOPMOST` yüzeyler tam ekran video (YouTube/Netflix/VLC) ve oyunların üstünde kalıyordu.
2. **Eksik Sistem Tepsisi İkonları:** Sabit listeye dayalı arka plan tarayıcısı Windows 11 bildirim alanındaki dinamik uygulamaları (`GlobalProtect`, `RustWTM`, `MuMuPlayer`, `Secret Server RDP`, `Lenovo Battery Widget` vb.) kaçırıyordu.
3. **Karma DPI ve Farklı Ekran Boyutlarında Tıklama Kayması:** `Form.DeviceDpi / 96.0` tahmini karma DPI monitörlerde CSS koordinatlarıyla sapma oluşturabiliyordu.
4. **Üst Dinamik Ada Görsel İşgali:** Tarayıcı sekmelerinin üstünü kapatmayan, yalnızca üzerine gelince genişleyen sıvı **damla (droplet)** formuna ihtiyaç duyuldu.
5. **Çoklu Monitör Desteği:** İkincil ve üçüncül ekranlarda Aurora yüzeyleri görünmüyordu.

## 2. Alınan Mimari Kararlar
1. **Monitör Bazlı Bağımsız Yüzey Çiftleri (`MonitorSurfaceHost`):** `MonitorTopologyService.GetMonitors()` tarafından dönen her monitör için tek paylaşımlı `CoreWebView2Environment` altında bağımsız `(CrownForm, DockForm)` çifti oluşturulur ve `SystemEvents.DisplaySettingsChanged` ile sıcak tak/çıkar desteklenir. Kısayollar (`Alt+Space`) farenin bulunduğu aktif monitörde açılır.
2. **Monitör Bazlı Tam Ekran Algılama (`FullscreenMonitorDetector`):** `250 ms` aralıkla Z-sırasında her monitörün en üst penceresi (`DWMWA_EXTENDED_FRAME_BOUNDS` + `WS_CAPTION`/`WS_THICKFRAME`/`IsZoomed` + `SHQueryUserNotificationState`) incelenir. Normal büyütülmüş pencereler (`WS_CAPTION + WS_THICKFRAME + IsZoomed`) etkilenmezken tam ekran video/oyun/sunum çalışan monitörde yalnızca o monitörün Aurora yüzeyleri `SW_HIDE` ile gizlenir.
3. **DPI-Bağımsız Fiziksel-CSS Oransal Hit-Test (`TransparentSurfaceForm`):** `60 Hz` (`16 ms`) döngüde Win32 `GetCursorPos` + `GetWindowRect` ile JS tarafından bildirilen `window.innerWidth / window.innerHeight` oranlanarak sıfır sapmalı tıklama sağlanır; pencere konumu WinForms ölçeklemesi yerine doğrudan `SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE)` ile kilitlenir.
4. **Gerçek Windows 11 Sistem Tepsisi Keşfi (`BackgroundTrayAppsService`):** Standart kullanıcı yetkisiyle okunabilen `HKCU\Control Panel\NotifyIconSettings` kayıt anahtarından `ExecutablePath` (KnownFolder GUID çözümlemesi + `SHGetKnownFolderPath`), `InitialTooltip` ve `IsPromoted` alanları okunur, aktif kullanıcı oturumundaki süreçlerle eşleştirilir ve gerçek ikon + canlı durum alt başlığı sunulur.
5. **Vektörel SVG Tasarım Sistemi ve Sıvı Damla Dinamik Ada:** Ham emoji chrome yerine tekdüze `1.85px` çizgi kalınlıklı özel SVG ikon kütüphanesi ve üst kenarda yalnızca `25 px` yüksekliğinde dinlenen, üzerine gelince yay animasyonuyla açılan sıvı damla tasarımına geçildi.
