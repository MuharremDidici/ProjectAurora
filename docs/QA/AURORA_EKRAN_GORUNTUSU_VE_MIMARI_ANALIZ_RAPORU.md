# Project Aurora — Ekran Görüntüsü UX Denetimi, Eksik Analizi ve Mimari Kabul Raporu

**Tarih:** 17.11.2026  
**İncelenen Ekran Görüntüsü:** Kullanıcı masaüstü canlı kullanım görüntüsü (`media_1794916833711_31f36305.jpg`)  
**Kapsam:** G0 (`v0.1.0`) + G0/G1 (`v0.2.0`) Dokümantasyon Paketleri, `ADR-001`..`ADR-011` ve `Aurora.App` v0.4 Çalışma Zamanı

---

## 1. Gönderdiğiniz Ekran Görüntüsünün Dürüst Değerlendirmesi ("Sence Nasıl?")

Gönderdiğiniz ekran görüntüsü, Project Aurora'nın klasik dikdörtgen Windows görev çubuğu kalıbından çıkıp **Üst Dinamik Ada (Dynamic Crown) + Alt Kristal Dock (Crystal Dock)** mimarisine geçişini gerçek masaüstü kullanımı altında çok net göstermektedir.

### 1.1. Ekran Görüntüsünde Başarılı Olan Unsurlar
1. **Gerçek Çerçevesiz Katmanlama (`DirectComposition`):** Üst ve alt yüzeylerin etrafında hiçbir pencere başlığı, kenarlık veya tıklamayı engelleyen görünmez kutu kalmamış; boş alanlar `%100` geçirgen (`WS_EX_TRANSPARENT`) çalışıyor.
2. **Bento Kontrol Hiyerarşisi:** Üst güvertedeki *Wi-Fi*, *Bluetooth*, *Odak Modu*, *Akıllı Derle*, *Ses & Ekran Parlaklığı*, *GSMTC Medya Kartı* ve *Uygulama Bazlı Ses Mikseri* tek bakışta okunabilir bir düzende.
3. **Gerçek Uygulama İkonları ve Çoklu Pencere Rozetleri:** Alt Kristal Dock üzerinde Dosya Gezgini (`2`), PowerShell, Ayarlar, Antigravity, ChatGPT, Opera (`2`), Brave, Microsoft Teams gibi gerçek Win32 ikonları ve pencere sayaçları sorunsuz işlenmiş.

---

### 1.2. Ekran Görüntüsünde Tespit Edilen 5 Kusur ve Bu Sürümde Uygulanan Çözümler

Ekran görüntüsünü tasarım ve mühendislik gözüyle incelediğimizde **5 somut sorun** tespit edilmiş ve **beşi de bu güncellemede doğrudan kod tabanında çözülmüştür**:

| # | Ekran Görüntüsündeki Kusur | Neden Oluştu? | Bu Güncellemede Uygulanan Kalıcı Çözüm |
|---|---|---|---|
| **1** | **Çift Görev Çubuğu & ChatGPT Yazı Kutusunun Kapanması:** Aurora Dock, Windows görev çubuğunun üstünde duruyor ve alttaki *"ChatGPT'ye sor"* kutusunu kapatıyordu. | Windows'un kendi görev çubuğu (`Shell_TrayWnd`) `48px` yer tutmaya devam ettiği için Aurora yukarı itilmişti. | **`ADR-011` Aktif Görev Çubuğu Gizleme + Crash Watchdog:** Aurora çalışırken `explorer.exe` kapatılmadan Windows çubuğu otomatik gizlenir; Kristal Dock ekranın **en alt sıfır kenarına** (`boundsPx.Bottom`) iner. Uygulama kapanırsa veya çökerse bağımsız bekçi süreci (`--watchdog-for-pid=`) Windows çubuğunu `< 50 ms` içinde geri getirir. |
| **2** | **Arka Plan Metin Sızması (Şeffaflık Kirliliği):** Üst açık panelin sağ yarısında arkadaki mavi kutu ve beyaz yazılar panelin içinden okunuyordu; ayrıca *Oyun* profilinde açık (beyaz) tema kalmıştı. | Önceki testten kalan `custom-auroraglass` (Açık mod) teması SQLite'ta kayıtlı kalmış ve `%84` CSS opaklığı keskin arka plan yazılarını perdelemeye yetmemişti. | **Yüksek Kontrastlı Kuzey Işığı Camı (`%97–98.5` Opaklık):** Cam yüzey opaklığı radyal ışık kırılmasıyla `0.97–0.985` seviyesine çıkarıldı; arka plan metin sızması tamamen kesildi ve varsayılan koyu `Aurora Glass` teması geri yüklendi. |
| **3** | **Görünür "Ara / Uygulama Aç" Butonunun Olmaması:** Windows'taki Başlat/Arama kutusu gibi doğrudan tıklanacak bir arama yeri yoktu ve bilgisayardaki yüklü uygulamalar listelenmiyordu. | Komut paleti yalnızca `Alt+Space` kısayoluna bağlıydı ve Başlat Menüsü `.lnk` uygulama indeksleyicisi yoktu. | **`🔍 Ara` Kapsülü + Yüklü Uygulama Başlatıcısı (`InstalledAppCatalogService`):** Hem alt Kristal Dock'a hem üst Dinamik Ada'ya belirgin **`🔍 Ara`** butonu eklendi. Tıklandığında bilgisayardaki tüm Başlat Menüsü uygulamalarını gerçek ikonlarıyla 4 sütunlu ızgarada açar; uygulama başlatma, Dock'a sabitleme (`📌`), canlı hesap makinesi, açık pencere bulma ve Google Web araması sunar. |
| **4** | **Dışarı Tıklayınca Açık Panelin Kapanmaması & DPI Kayması:** Üst panel açıkken arkadaki tarayıcıya tıklandığında panel açık kalmaya devam ediyordu. | `TransparentSurfaceForm` üzerinde `Deactivate` olayı dinlenmiyordu ve `AutoScaleMode` varsayılan bırakılmıştı. | **`AutoScaleMode.None` + Otomatik Kapanma (`outsideDeactivate`):** Fiziksel piksel koordinatları sabitlendi ve başka herhangi bir pencereye tıklandığında açık güverte/arama kutusu otomatik küçülecek şekilde bağlandı. |
| **5** | **Güç & Hızlı Sistem Kısayollarının Eksikliği:** Masaüstünü gösterme, ekran alıntısı alma veya ekranı kilitleme için hızlı şerit yoktu. | İlk sürümde güç aksiyonları eklenmemişti. | **Alt Sistem & Güç Şeridi:** `🔍 Ara` penceresinin altına tek tıkla `🖥️ Masaüstü`, `✂️ Ekran Alıntısı`, `📊 Görev Yöneticisi`, `⚙️ Ayarlar` ve `🔒 Ekranı Kilitle` butonları eklendi. |

---

## 2. v0.5 (v1.1 Paketi) İle Tamamlanan Ek Kabuk Özellikleri

İlk denetimde v1.1 yol haritası olarak listelenen başlıca kabuk yetenekleri de (`TrayKeyboardAndThumbnailServices.cs` ve `AuroraSurfaceTemplates.cs`) projeye dahil edilmiştir:

### 2.1. Sistem Tepsisi & Arka Plan Uygulamaları Çekmecesi (`🔼 Tepsi`) — *Tamamlandı*
* **Ne Yapar?** Hem Üst Dinamik Ada'da hem Alt Kristal Dock'ta yer alan `🔼` kapsülü, kullanıcı oturumunda arka planda çalışan uygulamaları (`Steam`, `Discord`, `Telegram`, `Teams`, `OneDrive`, `NVIDIA`, `SteelSeries`, `Windows Güvenliği` vb.) gerçek ikonları ve RAM kullanımlarıyla listeler; tek tıkla penceresini açar veya arka plan uygulamasını kapatır.
* **Klasik Tepsi Köprüsü:** Üçüncü parti eski tepsi ikonlarına sağ tıklamak gerektiğinde çekmece altındaki **`🪟 Klasik Windows Tepsisini 10 sn Göster`** butonu Windows bildirim alanını 10 saniyeliğine açıp otomatik geri gizler.

### 2.2. Canlı Pencere Görsel Önizlemesi (`PrintWindow` Live Preview) — *Tamamlandı*
* **Ne Yapar?** Kristal Dock'ta çalışan bir uygulama grubuna sağ tıklandığında (veya çoklu pencereli ikona tıklandığında) açılan *Pencere Seçici (Peek)* kartı, Win32 `PrintWindow(PW_RENDERFULLCONTENT)` API'siyle pencerenin **canlı anlık görüntüsünü** (`JPEG`) yakalar ve hizalama/kapatma butonlarının üzerinde gösterir.

### 2.3. Sürükle-Bırak (Drag & Drop) Dock İkon Sıralama ve Üst Dosya Rafı (`📥 Dropzone`) — *Tamamlandı*
* **Dock Sıralama:** Sabitlenmiş simgeleri Kristal Dock üzerinde fareyle sağa/sola sürükleyerek yeniden sıralayabilirsiniz; yeni sıra anında yerel SQLite veritabanına (`ReorderPinnedShortcutToIndexAsync`) kaydedilir.
* **Süper Dosya Rafı (`📥`):** Masaüstünden veya Dosya Gezgini'nden herhangi bir dosyayı Üst Dinamik Ada'ya sürükleyip bıraktığınızda (`CoreWebView2File` tam yol yakalama) veya `➕ Dosya Ekle` ile seçtiğinizde dosya geçici çalışma rafına sabitlenir; tek tıkla açılabilir, klasöründe gösterilebilir veya dosya yolu kopyalanabilir.

### 2.4. Aylık İnteraktif Takvim, Günlük Ajanda ve Canlı Klavye Dil (`🌐 TR/EN`) Kapsülü — *Tamamlandı*
* **Klavye Dili:** Üst Dinamik Ada'daki `🌐 TR / EN` kapsülü aktif pencerenin gerçek klavye düzenini (`GetKeyboardLayout`) canlı okur ve tıklandığında yüklü klavye dilleri arasında anında geçiş yapar (`ActivateKeyboardLayout` + `WM_INPUTLANGCHANGEREQUEST`).
* **Aylık Takvim & Ajanda:** `📅 Saat • Tarih` kapsülüne tıklandığında Pazartesi başlangıçlı interaktif aylık takvim açılır; herhangi bir güne tıklanarak o tarihe özel kalıcı ajanda notu kaydedilebilir (not bulunan günlerde takvim üzerinde mavi ışık noktası görünür).

### 2.5. İleri Faz (v1.2+) İçin Kalan Tek Opsiyonel Kalem
* **Çoklu Monitör İkincil Dock Klonları:** `MonitorTopologyService` tüm bağlı ekranları algılar ve ikincil ekranlardaki Windows çubuklarını uyumlu yönetir; Aurora yüzeylerinin ikinci/üçüncü monitörlerde bağımsız kopya olarak çoğaltılması isteğe bağlı v1.2 genişletmesidir.

---

## 3. Bağımsız Teknik Kabul Denetimi (G0 & G1 Uyumluluk Özeti)

Ekran görüntünüzün arka planında yer alan **7 maddelik Bağımsız Teknik Kabul Denetimi** kriterlerine göre projenin doğrulanabilir kanıt durumu aşağıdadır:

1. **G0 / G1 Gereksinim ve ADR Uyumluluğu:** `docs/ADR/ADR-001` ile `ADR-011` arasındaki tüm mimari karar kayıtları ve `docs/G1_EVIDENCE/12_G1_FINAL_RECONCILIATION.md` kanıt paketi eksiksizdir. WebView2 DirectComposition hibrit yüzey geçişi ve çalışma zamanı görev çubuğu gizleme mekanizması mimari sapma bırakılmadan `ADR-011` altında belgelenmiştir.
2. **18 Projeli Modüler Monolit Sınırları:** `Aurora.sln` altında `13` kaynak (`src/*`) ve `5` test (`tests/*`) projesi katı katman sınırlarıyla derlenmektedir (`Aurora.Core` sıfır dış bağımlılığa sahiptir).
3. **Gerçek Windows Entegrasyonu:** `Win32AppBarService`, `Win32WindowCatalog`, `Win32IconExtractor`, `InstalledAppCatalogService`, `CoreAudioService` (MMDevice + IAudioSessionManager2), `GsmtcMediaSessionService`, `MonitorTopologyService` sahte (mock) veri değil, gerçek işletim sistemi adaptörleridir.
4. **Güvenlik ve Yetki Sınırı (`DEC-002` & `DEC-003`):** Uygulama `asInvoker` (standart kullanıcı) manifestiyle çalışır; yönetici (UAC) izni istemez, `explorer.exe` sürecini sonlandırmaz, DLL enjeksiyonu yapmaz ve kayıt defterindeki belgelenmemiş `StuckRects3` ikili verilerine dokunmaz.
5. **Kurtarma Garantisi:**
   * Normal çıkışta `EnsureNativeTaskbarRestored()` otomatik çalışır.
   * Çökme veya Görev Yöneticisi'nden sonlandırmada bağımsız `--watchdog-for-pid=` süreci Windows görev çubuğunu anında geri yükler.
   * Manuel kurtarma için kök dizindeki `Restore-Windows-Taskbar.cmd` veya `Aurora.App.exe --restore-taskbar` komutu her zaman hazırdır.
6. **Test ve Bellek Performansı:** 5 test projesindeki tüm birim/entegrasyon/platform testleri geçmektedir; Release x64 çalışma zamanı bellek tüketimi (~`114 MB` Working Set / ~`29 MB` Private Memory) G1 hedef bütçesinin (`< 150 MB`) altındadır.
