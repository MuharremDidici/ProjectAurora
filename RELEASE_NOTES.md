# Project Aurora — Sürüm Notları

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
