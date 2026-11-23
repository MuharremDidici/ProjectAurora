# Project Aurora — Sürüm Notları

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
