# Project Aurora — Başka Bilgisayarda Tek Tık Kurulum ve Paylaşım Kılavuzu

Bu kılavuz, Project Aurora'yı ekstra hiçbir yazılım (`.NET SDK`, `Visual Studio` vb.) kurulu olmayan başka bir Windows 11 (64-bit) bilgisayarda **tek tıkla kurup denemek** için hazırlanmıştır.

## 1. Hazır Kurulum Dosyaları Nerede?

Proje kök dizinindeki `dist\` klasöründe iki hazır paket üretilmiştir:

1. **`dist\Aurora-Kurulum-v0.5-x64.exe` (Tek Dosyalık Kurulum Sihirbazı):**
   * USB bellek, yerel ağ veya doğrudan dosya paylaşımı için tek `.exe` dosyasıdır.
   * İçinde `.NET 10` çalışma zamanı ve tüm Aurora bileşenleri gömülüdür.
2. **`dist\Project-Aurora-TekTik-Kurulum-v0.5.zip` (İnternet Üzerinden Gönderim Paketi):**
   * WhatsApp, Telegram, Discord, Google Drive, WeTransfer veya E-posta çoğu zaman doğrudan `.exe` uzantılı dosyaları engellediği için `.zip` paketi hazırlanmıştır. Arkadaşınıza internetten gönderecekseniz **bu `.zip` dosyasını** gönderin.

---

## 2. Arkadaşınızın Bilgisayarında Kurulum Adımları (3 Saniye)

1. Gönderdiğiniz `.zip` dosyasını klasöre çıkartıp **`Aurora-Kurulum-v0.5-x64.exe`** dosyasına çift tıklasın.
2. **Windows SmartScreen Uyarısı Çıkarsa:** Uygulama ticari bir EV kod imzalama sertifikasıyla imzalanmadığı için Windows mavi bir bilgi penceresi gösterebilir. Penceredeki **"Ek bilgi" (More info)** yazısına tıklayıp **"Yine de çalıştır" (Run anyway)** butonuna basması yeterlidir.
3. Açılan koyu temalı **Project Aurora Kurulum** penceresinde **`🚀 Tek Tıkla Kur ve Başlat`** butonuna tıklasın:
   * Yönetici (UAC) şifresi istemez; kullanıcı klasörüne (`%LOCALAPPDATA%\Programs\ProjectAurora`) kurulur.
   * Masaüstüne ve Başlat Menüsüne **Project Aurora** kısayolunu ekler.
   * WebView2 eksikse Microsoft'tan otomatik tamamlar ve uygulamayı anında başlatır.

---

## 3. Arkadaşınız Uygulamayı Kaldırmak İsterse

Hiçbir kalıntı bırakmadan kaldırmak için üç kolay yöntem vardır:
* **Yöntem 1 (Windows Ayarları):** `Windows Ayarları -> Uygulamalar -> Yüklü Uygulamalar` listesinden **Project Aurora** yanındaki üç noktaya tıklayıp **Kaldır** seçilebilir.
* **Yöntem 2 (Kurulum Sihirbazı):** `Aurora-Kurulum-v0.5-x64.exe` tekrar çalıştırılıp sol alttaki **Mevcut Kurulumu Kaldır** butonuna tıklanabilir.
* **Otomatik Görev Çubuğu Güvencesi:** Kaldırma işlemi sırasında Windows'un kendi görev çubuğu otomatik olarak görünür hale getirilir.

---

## 4. Kodda Değişiklik Yaptığınızda Paketi Yeniden Üretmek

İleride kodda yeni bir değişiklik yaptığınızda tek tık kurulum paketini güncellemek için PowerShell'de şu tek komutu çalıştırmanız yeterlidir:

```powershell
powershell -ExecutionPolicy Bypass -File .\Build-Installer.ps1
```
