# Project Aurora — Sürüm Notları

## v7.2.0
> Summary: Alt+Tab & tam ekran odak bütünlüğü, tüm Dock/Crown açılır menüleri için tek tıkla boşluğa basınca kapanma, v2.x (.NET) ve v3.x+ kurulumlarından sorunsuz tek tık otomatik yükseltme, GitHub Releases API yedekli güncelleyici ve dinamik PE sürüm bilgisi.

### Öne Çıkan Yenilikler
- **Alt+Tab Odak Bütünlüğü & Tam Ekran Koruması:** `Alt` (`VK_MENU`) basılıyken Windows 11 kabuk bastırma ve Explorer gizleme çağrıları duraklatılır; `hide_tray_window` içindeki senkron `ShowWindow` blokajı kaldırıldı, `analyze_fullscreen_and_dodge` içinden `XamlExplorerHostIslandWindow`/`MultitaskingViewFrame` dışlandı ve Aurora içi odaklar kapandığında klavye odağı son aktif kullanıcı penceresine (`last_external_hwnd`) geri verilir.
- **Tüm Açılır Menüler İçin Tek Tık Dış Kapanma:** Alt Tepsi, Hızlı Kontroller, Takvim/Bildirim, Widget Seçici, Dairesel Küre, Klasör Yığını ve üst Canlı Sahne için evrensel dış tıklama izleme (`setDockPopoverState`, `setCrownStageState`, `want_watch_clicks`) + Dock içi boş alan `pointerdown` kapatması eklendi.
- **Eski Sürümlerden (`v2.x` & `v3.x+`) Kesintisiz Otomatik Yükseltme:** `aurora-setup` artık eski `v2.x` güncelleyicisinin gönderdiği `--update --wait-pid=<PID>` parametrelerini tam olarak işleyip atomik kurulum ve yeniden başlatma yapar; `aurora-storage::updater` GitHub Releases API JSON yedeği içerir ve tüm PE sürüm kaynakları dinamik olarak `Cargo.toml` sürümüne bağlandı.

## v7.1.0
> Summary: Gerçek Windows 11 sistem tepsisi keşfi (NotifyIconSettings + 32 uygulama + akıllı pencere/AUMID açılışı) ve alt Dock üzerinden üst Dinamik Ada'yı açmadan yukarı doğru açılan OmniSearch.

### Öne Çıkan Yenilikler
- **Gerçek Windows 11 Sistem Tepsisi Keşfi:** Canlı `HKCU\Control Panel\NotifyIconSettings` kayıt defteri + çalışan süreç anlık görüntüsü eşleştirilerek `GlobalProtect`, `MuMuPlayer`, `RustWTM`, `WhatsApp`, `Spotify`, `Teams`, `Docker Desktop` dahil 32 arka plan tepsi uygulaması gerçek ikonlarıyla gösterilir ve önce açık pencereye odaklanır, yoksa AUMID/EXE üzerinden başlatılır.
- **Alt Dock Üzerinde Açılan OmniSearch:** Başlat / Ara düğmesine tıklandığında arama penceresi üst Dinamik Ada'yı tetiklemeden doğrudan alt Dock'un hemen üzerinde açılır.

## v7.0.0
> Summary: 10 özgün kabuk tasarım mimarisi, 6 Dinamik Ada stili + 3 boşta davranışı + 4 sekmeli Canlı Sahne, sadeleştirilmiş yerel Windows / Aurora Yansıtma bildirim seçici, 20 WCAG AA+ tema ve duvar kağıdı vurgu rengi.

### Öne Çıkan Yenilikler
- **10 Kabuk Tasarım Mimarisi:** `Tahoe Cam`, `Nothing Donanım`, `Linear Stüdyo`, `Vision Küre`, `Arc Bölünmüş`, `Braun Atölye`, `Swiss Editoryal`, `Cockpit HUD`, `Fluent Mica` ve `Zen Hayalet` birbirinden bağımsız geometri, ızgara ve yerleşim kurallarıyla çalışır.
- **Yeni Nesil Dinamik Ada & 4 Sekmeli Canlı Sahne:** 6 görsel ada stili (`Sıvı Hap`, `Mac Çentik`, `Ufuk Çizgisi`, `Nothing Kapsül`, `Cam Şerit`, `Yörünge Halka`) + 3 boşta modu (`Sıvı Kapsül`, `Ufuk Çizgisi`, `Akış`) ve `Canlı Sahne · Hızlı Kontrol · Araçlar · Telemetri` sekmeleri; gereksiz ada ses kontrolü kaldırılıp çift Windows bildirimi engellendi.
- **20 WCAG AA+ Tema + Duvar Kağıdı Otomatik Vurgu:** Duvar kağıdından çıkarılan baskın renkler WCAG AA kontrastına göre dengelenir.

## v6.0.0
> Summary: Tam ekran video/oyun algılama iyileştirmeleri, 5 yüzey stiliyle geliştirilmiş masaüstü widget motoru ve yeni nesil görsel kalite güncellemesi.

### Öne Çıkan Yenilikler
- **Tam Ekran Medya Koruması & Widget Stilleri:** Video tam ekran yapıldığında Dock/Crown otomatik gizlenir; saat, müzik, AI kota, pil ve sistem widget'ları 5 yüzey kaplaması ve 3 boyut seçeneğiyle genişletildi.

## v5.0.0
> Summary: Java/Swing/Electron/UWP pencere odaklama & simge durumuna küçültme güvenilirliği, tam ekran gizleme doğruluk düzeltmeleri ve endüstriyel tasarım dili yenilemesi.

### Öne Çıkan Yenilikler
- **Endüstriyel Pencere Aktivasyonu (`focus_window`):** Java/Swing, Electron ve çoklu monitör pencerelerinde Z-sırası ve öne getirme güvenilirliği güçlendirildi; Dock üzerinden açık pencereye tekrar tıklandığında simge durumuna küçültme/geri getirme dengelendi.

## v4.2.0
> Summary: Sabit zıplamayan uygulama sıralaması + elle sürükle-bırak sabitleme, 4 bölgeli 14 Pro Dock widget'ı, uygulama bazlı canlı ses rozeti & Alt+Tekerlek ve 6 dilimli Hızlı Aksiyon Çemberi.

## v4.0.0
> Summary: Aurora OS Studio & Masaüstü Widget Motoru — Windows 11 tanıdık alt görev çubuğu ve sağ alt Hızlı Kontroller/Takvim, 4 seçilebilir çoklu pencere dallanma stili, 12 WCAG AA+ tema + otomatik gündüz/gece geçişi, pencerelerin arkasında çalışan 10 özelleştirilebilir masaüstü widget'ı (Saat, Vinil Müzik, Bluetooth/Laptop Pil Halkaları, Windows DPAPI şifreli ChatGPT/Gemini/Claude AI Kota Takibi), sakin üst Dinamik Ada ve Apple/Linear kenar çubuklu ayarlar.

### Öne Çıkan Yenilikler
- **Windows 11 Tanıdık Alt Çubuk + Sağ Alt Hızlı Kontroller & Takvim:**
  - Solda net `✦` Başlat & Ara + klasörler, ortada sabitli/açık uygulamalar, sağda gizli simgeler, `Wi-Fi · Ses · Pil %` (fare tekerleğiyle ses ayarı + tıklayınca sağ alt Hızlı Kontroller), `Saat · Tarih · 🔔` (tıklayınca sağ alt Bildirimler & Takvim/Ajanda) ve boş çubuğa sağ tık menüsü.
- **4 Seçilebilir Çoklu Pencere Dallanma Stili & 3 İkon Hover Efekti:**
  - `Dairesel Dallanan Küre (Dendritic)`, `3D Yelpaze Kartlar`, `Win11 Modern Kartlar` ve `Yay Çemberi (Prism Arc)` + `Hafif Yükselme / Dalga / Işıltı` ikon efektleri.
- **10 Özelleştirilebilir Masaüstü Widget Motoru (`HWND_BOTTOM`, Tek Paylaşımlı WebView2):**
  - Pencerelerin arkasında yaşar, boş masaüstünde `%100` tıklama geçirgendir (`SetWindowRgn`), tam ekran oyun/videoda animasyonları `%0` CPU için durdurur, kapalıyken `0 MB` yüzey RAM'i harcar.
  - 10 widget: **Saat Stüdyosu** (5 stil), **Müzik Çalar** (Vinil/Cam/Şerit), **Bluetooth & Laptop Pil Halkaları**, **AI Kota Takibi** (ChatGPT/Gemini/Claude — Windows DPAPI şifreli API canlı sorgusu + yerel CLI log tarayıcı + abonelik sayacı), **Hava Durumu**, **Sistem Telemetri**, **Takvim & Ajanda**, **Yapışkan Cam Not**, **Pomodoro Odak**, **Hızlı Başlatıcı** + 16px manyetik ızgaralı Düzenleme Modu.
- **12 WCAG AA+ Tema + 3 Deneyim Şablonu + Kenar Çubuklu Ayarlar:**
  - 5 yeni tema (`Nordic Frost`, `Warm Latte`, `Midnight Emerald`, `Sunset Kyoto`, `True OLED`), 3 köşe yuvarlaklığı (`Keskin / Modern / Kapsül`), otomatik gündüz/gece teması ve Apple/Linear tarzı 6 bölümlü sade Türkçe ayarlar.

## v3.0.0
> Summary: Saf Rust Win32 çekirdek motoru + tek paylaşımlı ultra-hafif WebView2 renderer (sıfır .NET 10, sıfır harici DLL, 3.39 MB tek dosya kurulum), Dairesel Dallanan Pencere Küresi, canlı laptop + bağlı Bluetooth pil göstergesi, olay-tabanlı sıfır-sorgulu pano, endüstriyel kilitlenmez girdi/odak mimarisi ve boşta 53 MB WorkingSet / %0.05 CPU yükü.

### Öne Çıkan Yenilikler
- **Saf Rust Win32 Motoru + Tek Paylaşımlı WebView2 (`--process-per-site`):**
  - `.NET 10` çalışma zamanı ve 220+ harici DLL tamamen kaldırıldı; tek statik `aurora.exe` + `0.7 MB` Watchdog.
  - Tüm monitörlerde yalnızca **1 paylaşımlı Chromium renderer**, `COREWEBVIEW2_MEMORY_USAGE_TARGET_LEVEL_LOW` ve kontrollü bellek kırpma ile gerçek ölçülen boşta yük: **53.0 MB toplam WorkingSet RAM** (`aurora.exe` `8.0 MB`), **%0.052 CPU**, **40 ms Crown / 72 ms Dock soğuk başlatma**.
  - **Sıfır Pil/SSD İsrafı:** Pano izleme Windows `WM_CLIPBOARDUPDATE` olayına taşındı (sıfır periyodik `OpenClipboard` çakışması); Windows bildirim veritabanı (`wpndatabase.db`) yalnızca dosya değişiklik damgası değiştiğinde okunuyor.
- **Endüstriyel Kilitlenmez Girdi, Odak ve Tıklama Geçirgenliği:**
  - `AttachThreadInput` tamamen kaldırıldı; bloklamasız `WH_KEYBOARD_LL` / `WH_MOUSE_LL`, Türkçe Q `AltGr` ve `Alt+Tab` korumalı `Alt+Sürükle`, `WS_EX_TRANSPARENT` + fiziksel tuş emniyetli Snap Zones, `ResizeObserver` senkronlu piksel-kesin `SetWindowRgn`, Windows Foreground Lock'a dayanıklı tek-tık dış-kapanma ve kademeli kendi kendini onaran soğuk başlatma bekçisi.
- **Yeni Dairesel Dallanan Pencere Küresi & Canlı Pil/Bluetooth:**
  - Çoklu pencere grupları için sıfır-kaymalı, organik kavisli dallı dairesel cam küreler + DWM canlı önizlemenin hemen altında tek ince kontrol şeridi (`Sol · Sağ · 50/50 · Üstte · + · ×`).
  - Üst Damla, Üst Çubuk ve Kontrol Güvertesinde anlık (`WM_POWERBROADCAST`) dinamik doluluklu laptop pil göstergesi; Bluetooth tarafında `DEVPKEY_Device_ContainerId` + `DEVPKEY_Device_IsConnected` filtresiyle yalnızca o an aktif bağlı cihazlar ve gerçek pil yüzdeleri.
- **3.39 MB Saf Rust Atomik Kurulum Paketi (`aurora-setup`):**
  - `92.4 MB` .NET kurulumu yerine zstd-19 sıkıştırmalı **`3.39 MB`** sıfır-UAC kurulum sihirbazı; çalışan eski sürümün üzerine dosya kilidine takılmadan 12 denemeli atomik yükseltme yapar ve `%LOCALAPPDATA%\ProjectAurora\data\` içindeki tüm v2.x verilerini %100 korur.
  - `Alt+Shift+R` başka bir sürücü tarafından tutuluyorsa GIF kaydedici için otomatik `Ctrl+Alt+Shift+R` yedek kısayolu kaydedilir (`5/5` kısayol aktif).

## v2.5.1
> Summary: Sabit Bar ↔ Akıllı Kaçınma geçişinde pencerelerin periyodik büyüyüp küçülmesini tamamen bitiren gerçek Win32 AppBar (`SHAppBarMessage`) altyapısı + sadece renk değil silüet, kart, sekme, simge çerçevesi ve gösterge mimarisiyle tamamen ayrışan 6 modern 2026 görsel stili.

### Öne Çıkan Yenilikler
- **Sıfır Titreme / Sıfır Yeniden Boyutlandırma Döngüsü (Native Win32 AppBar):** Sabit Bar modunda Windows Explorer ile çakışan periyodik `SPI_SETWORKAREA` zamanlayıcısı tamamen kaldırıldı; yerine her monitör için görünmez, tıklamayı geçiren gerçek Win32 AppBar (`SHAppBarMessage ABM_NEW / ABM_QUERYPOS / ABM_SETPOS / ABM_REMOVE`) kaydı eklendi. Sabit açıldığında tüm açık pencereler tek seferde barın hemen üstüne kusursuz hizalanır; sabit kapatıldığında ise tek seferde %100 tam ekrana açılır ve bir daha asla büyüyüp küçülmez (`DWMWA_EXTENDED_FRAME_BOUNDS` ile Electron/Chromium/WhatsApp pencereleri dahil). `explorer.exe` yeniden başlarsa `TaskbarCreated` mesajıyla otomatik toparlar.
- **Gerçekten Farklı 6 Modern Görsel Stil (Renk Değişimi Değil, Tam Mimari Fark):**
  - **Liquid Glass 26:** Kavisli optik cam üst kırılma yansıması, akıcı üst damla ve parlak kapsül gösterge.
  - **Bento Silk:** Sıfır neon parlamalı, sıcak taş tepsi üzerinde yükseltilmiş mat beyaz porselen bento kartlar, porselen sekme çubuğu ve net nokta göstergesi.
  - **Linear Swiss:** Mat karbon yüzey, alt çizgili İsviçre sekme çubuğu (`border-bottom`), başlık ayraçlı düz kartlar, sol şeritli aktif anahtarlar ve jilet 2px simge alt çizgisi.
  - **Mono Matrix:** Görünür nokta matris doku, kesikli donanım çerçeveleri, %50 tam dairesel simge soketleri, 3'lü LED matris aktif göstergesi ve büyük harfli mono tipografi.
  - **Spatial Vision:** Havada süzülen çift cam kenarlıklı tam kapsül üst ada ve Dock, %50 dairesel ışık halkalı simge baloncukları ve buzlu beyaz uzamsal odak sekmeleri.
  - **Cyber HUD:** Açılı trapez (`clip-path`) üst çentik, köşe nişangah çerçeveleri, teknik blueprint ızgarası, üst lazer rayı ve köşeli taktik simge bölmeleri.
- **Tema Bazlı Renk Özelleştirme Korundu:** Tüm 6 görsel stilin kendi renkleri (Vurgu 1/2, Yüzey 1/2, Metin, Opaklık) Stüdyo içinden canlı olarak özelleştirilmeye devam eder.

## v2.5.0
> Summary: Tek tıkla Sabit Bar (52 px uçtan uca ince görev çubuğu, 0 px duvar kağıdı sızıntısı) ↔ Yüzen Intellihide geçişi + 6 farklı 2026 mimari tema (Liquid Glass 26, Bento Silk, Linear Precision, Mono Matrix, Spatial Vision, Cyber Prism) ve her tema için ayrı kaydedilen canlı renk özelleştirici.

### Öne Çıkan Yenilikler
- **Sabit Bar ↔ Akıllı Kaçınma (Tek Tık Pin Geçişi):** Dock üzerindeki Sabit (Pin) butonuna basıldığında Dock uçtan uca kenara sıfır oturan ince bir görev çubuğuna (`Kompakt 44 px / Standart 52 px / Geniş 60 px`) dönüşür — duvar kağıdı sızıntısı ve kesik masaüstü ikonları tamamen ortadan kalkar, Antigravity/WhatsApp mesaj kutuları asla kapanmaz. Pin kaldırıldığında doğrudan Akıllı Kaçınma (yüzen Kristal Pill) moduna geçer, çalışma alanı %100'e geri açılır ve en alt kenar tetiklemesi daraltılarak yazı kutularına tıklarken yanlışlıkla açılma engellenir.
- **Sabit Bar Düzen Seçeneği:** Stüdyo'dan tek tıkla `Merkez Odaklı` veya `3 Bölgeli (Sol: Ara · Orta: Uygulamalar · Sağ: Araçlar)` düzen seçilebilir.
- **2026 Çok-Mimarili Tasarım Sistemi (6 Farklı Tema Mimarisi):** Yalnızca renk değişimi değil; her biri farklı geometri, yüzey dokusu, kenarlık, üst ada silüeti ve aktif uygulama göstergesine sahip 6 modern tema: **Liquid Glass 26**, **Bento Silk (Açık)**, **Linear Precision**, **Mono Matrix** *(yeni — dot-matrix + monospace telemetri + sinyal LED)*, **Spatial Vision** ve **Cyber Prism**.
- **Her Tema İçin Canlı Renk Özelleştirici:** Stüdyo sekmesinde her temanın **Vurgu 1, Vurgu 2, Yüzey 1, Yüzey 2, Metin** renkleri ve **Yüzey Opaklığı (%55–%100)** canlı olarak değiştirilebilir, 8 hazır modern palet uygulanabilir, WCAG AA okunabilirlik oranı anlık izlenebilir ve her temanın özel renkleri ayrı ayrı kaydedilir.

## v2.4.1
> Summary: Tıklanabilir canlı bildirimler (WhatsApp, Antigravity, Teams, Ekran Alıntısı derin bağlantı) + açılış sonrası Explorer yarışını engelleyen, yoğunluğa duyarlı (Kompakt/Standart/Rahat) ve kendi kendini iyileştiren Dock Çalışma Alanı (`Alan Ayır`) koruması.

### Öne Çıkan Yenilikler
- **Tıklanabilir Canlı Bildirimler (Native Deep-Link):** Üstte beliren bildirim kartına (`Aç ↗`), Zil açılır penceresine veya Telemetri bildirim satırına tıkladığınızda ilgili uygulama ve doğrudan o sohbet/konuşma açılır (`whatsapp:`, `antigravity-ide:`, `ms-screensketch:`, `msteams:` protokolleri + Store/MSIX uygulamaları için COM `IApplicationActivationManager` AUMID + açık pencere odaklama).
- **Kendi Kendini İyileştiren Dock Çalışma Alanı (`Alan Ayır: Açık`):** Açılışta Windows Explorer'ın `ABM_SETSTATE` sonrası çalışma alanını sıfırlama yarış koşulu giderildi; gecikmeli doğrulama, ekran değişikliği ve pasif 0-CPU kalp atışı koruması eklendi. Artık tüm monitörlerde (100% ve 125%+ DPI dahil) Dock yoğunluğuna (`Kompakt 64 px`, `Standart 72 px`, `Rahat 82 px`) ve `Alt+Sağ Tık` ölçeğine göre birebir temiz boşluk ayrılır; Antigravity, WhatsApp ve Discord yazı kutuları asla kapanmaz.
- **Sürüm Yükseltme Uyumluluğu:** `v2.3.2` ve öncesi ayarlardan yükselten kullanıcılar için `Alan Ayır` varsayılan olarak otomatik etkin gelir; `Otomatik Gizle` modunda alan %100'e geri açılır.

## v2.4.0
> Summary: Tıklanabilir canlı bildirimler (WhatsApp sohbetine, Antigravity konuşmasına, Teams ve Ekran Alıntısı'na doğrudan derin bağlantı) ve tam ekran pencerelerin yazı kutularını asla kapatmayan otomatik Dock Çalışma Alanı (Alan Ayır) rezervasyonu.

### Öne Çıkan Yenilikler
- **Tıklanabilir Canlı Bildirimler (Native Deep-Link):** Üstte beliren bildirim kartına (`Aç ↗`), Zil açılır penceresindeki veya Telemetri listesindeki herhangi bir bildirime tıkladığınızda ilgili uygulama ve doğrudan o mesaj/konuşma açılır (`ms-screensketch:`, `antigravity-ide:`, `whatsapp:`, `msteams:` protokolleri + WhatsApp/Store uygulamaları için COM `IApplicationActivationManager` AUMID + açık pencere odaklama).
- **Dock Çalışma Alanı Ayırma (`Alan Ayır: Açık`):** Aurora artık alt/sol/sağ Dock için Windows `WorkArea` alanında DPI ve ölçeğe tam uyumlu bir boşluk ayırır; Antigravity, WhatsApp, Discord, Opera ve VS Code gibi tam ekran pencereler Dock'un hemen üstünde/yanında biter, yazı yazma kutuları asla kapanmaz! Tam ekran oyun/videolar yine %100 ekranı kaplar; isterseniz *Stüdyo → Alan Ayır* ile tek tıkla açıp kapatabilirsiniz.
- **Akıllı Kaçınma & Mevcut Pencereler:** Açık haldeki tam ekran pencereler bile anında yeni alana hizalanır; `Otomatik Gizle` modunda alan otomatik olarak %100'e geri açılır.

## v2.3.2
> Summary: Windows 11 eski ses/parlaklık çubuğu ve çift çıkan sağ-alt bildirim kutularının anlık (<1 ms) bastırılması + USB/Bluetooth kulaklık ses tekeri için canlı CoreAudio geri çağrımı.

### Öne Çıkan Yenilikler
- **Eski Windows Ses & Parlaklık Çubuğu Tamamen Bastırıldı:** Klavye, Lenovo Fn ve Logitech G HUB/medya tekeri enjekte ses tuşları doğrudan yakalanır; USB/Bluetooth kulaklık tekerinden gelen donanım değişimleri ise canlı CoreAudio `IAudioEndpointVolumeCallback` (< 2 ms) ve `EVENT_OBJECT_UNCLOAKED` (`0x8018`) kancasıyla Windows 11'in alt-orta ses kutusunu göstermeden anında Aurora Üst Damla'ya yönlendirir.
- **Çift Çıkan Windows Sağ-Alt Bildirimleri Bastırıldı:** Aurora'nın Bildirim Köprüsü açıkken Windows 11'in `ShellExperienceHost`/`ShellHost` (`Yeni bildirim` / `New notification`) sağ-alt bildirim penceresi uncloak anında (< 1 ms) gizlenir; `wpndatabase.db` ve İşlem Merkezi bozulmadan tüm bildirimler yalnızca Aurora Üst Damla'da görünür.
- **Anlık Üst Hap Ses Senkronizasyonu:** Ses değiştirildiğinde Üst Damla'daki `%` göstergesi ile canlı OSD çubuğu gecikmesiz aynı karede güncellenir.

## v2.3.1
> Summary: Tüm güncel özellikleri kapsayan 6 adımlı interaktif Özellik Turu (canlı tema/Dock seçici, DDC/CI, Bluetooth Pil, Gizlilik LED, 3 Bölgeli Drop + QR AirDrop, GIF/OCR, Telemetri ve kısayol haritası).

### Öne Çıkan Yenilikler
- **6 Adımlı İnteraktif Özellik Turu (`Hızlı Tur`):** Üstte tıklanabilir 6 bölüm sekmesi, ilerleme çubuğu, klavye (`← / → / ESC`) gezintisi ve her adımda ilgili sekme veya OmniSearch'e tek tıkla geçiş düğmesi.
- **Canlı Kurulum Adımı (Adım 1):** Tema ailesi, Dock konumu (`Alt / Sol / Sağ`) ve Dock davranışı tur açıkken canlı önizlenerek değiştirilebilir.
- **Eksiksiz Özellik Rehberi (Adım 2–6):** Canlı Donanım OSD, Harici Monitör DDC/CI (EDID), Bluetooth Pil yüzdeleri, Kamera/Mikrofon Gizlilik LED'i, sıfır kaymalı Fitts Yasası Dock, `Alt+Sürükle`, `Shift+Sürükle` Snap Zones, 3 Bölgeli Drop + Yerel QR AirDrop, SQLite Pano Kasası & Oto-Yapıştır, 10 FPS Bayer GIF Kaydedici, Çevrimdışı OCR, Renk Damlalığı, Telemetri SVG Eğrileri, OmniSearch kısayol haritası ve SHA-256 GitHub otomatik güncelleme tek rehberde toplandı.

## v2.3.0
> Summary: Tek merkezi uyku destekli hit-test motoru ve tek-renderer düşük bellekli WebView2 ile uzun süreli CPU/RAM optimizasyonu, tam modüler kod mimarisi, 8 Açık Tema yüzeyi piksel doğrulaması ve yeni vitrin dokümantasyonu.

### Öne Çıkan Yenilikler
- **Uzun Süreli CPU & Bellek Optimizasyonu:** 6 ayrı 16 ms yüzey zamanlayıcısı tek merkezi imleç-delta `SurfaceHitTestCoordinator` yapısında birleştirildi; imleç hareketsizken sıfır Win32 yüküyle otomatik 100 ms uyku moduna geçer, ilk piksel hareketinde anında 60 Hz'e uyanır.
- **Düşük Bellekli WebView2 Motoru:** Tek paylaşımlı Chromium renderer (`--renderer-process-limit=1`), kapalı BFCache/SpareRenderer ve açılış ikon çözümlemesi sonrası otomatik bellek kırpma + 6 sn arka plan tepsi önbelleği.
- **8/8 Açık Tema Yüzeyi Tam Doğrulandı:** Kontrol Merkezi, Telemetri, Süper Raf, Stüdyo, Omnibar, Sistem Tepsisi, Yatay Kristal Dock ve Dikey Dock'un tamamı `Aurora Glass (Açık)` temasında piksel piksel doğrulandı.
- **Temiz Modüler Mimari:** `Program.cs` 5 alan modülüne (`Coordinator/`), `AuroraSurfaceTemplates.cs` ise 6 okunabilir şablon parçasına (`Host/Templates/`) ayrıldı.
- **Akıllı Release Notları & Vitrin:** Otomatik güncelleme bildirimleri artık doğrudan bu dosyadaki özet notu kullanır; yeni cam squircle ikonu ve Koyu/Açık tema galerisiyle `README.md` yenilendi.

## v2.2.2
> Summary: Özel 7 boyutlu (16–256 px) Aurora cam squircle uygulama ikonu, kurulum sihirbazı logosu, Masaüstü/Başlat kısayol ikonları ve anlık Shell ikon önbelleği yenileme.

- `Aurora.App.exe`, `Aurora-Kurulum-v*-x64.exe`, Masaüstü & Başlat kısayolları ve Windows Yüklü Uygulamalar kaydı yeni çok boyutlu `aurora.ico` ile güncellendi.

## v2.2.1
> Summary: Gerçek EDID+DXVA2 DDC/CI harici monitör parlaklığı, Bluetooth cihaz kategori ikonları, GIF kayıt çerçevesi + Bayer dithering, Gizlilik LED hayalet koruması ve çift-tık süreç kapatma.

- Windows `dxva2.dll` `hPhysicalMonitor = 0` uyumluluk düzeltmesi, gerçek EDID monitör isimleri, son-değer-kazanır I2C yazma birleştirme ve Açık Tema WCAG AA+ kontrast iyileştirmeleri.
