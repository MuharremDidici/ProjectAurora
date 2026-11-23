# ✦ Project Aurora — Windows 11 Dinamik Ada (Crown) & Kristal Dock

Project Aurora, Windows 11 için `.NET 10` + `WebView2` + `DirectComposition` mimarisiyle geliştirilmiş, yönetici izni (UAC) gerektirmeyen ve `explorer.exe`'ye dokunmadan çalışan **çoklu monitör destekli** yeni nesil masaüstü deneyimi katmanıdır.

## Öne Çıkan Özellikler
- **Üst Merkez Akışkan Damla (Aurora Crown):**
  - **Canlı Donanım OSD HUD'u:** Ses, sessiz, parlaklık, mikrofon, Caps Lock ve Num Lock değişimlerinde Windows'un ses kutusunu bastırıp Üst Damla'da akıcı doluluk çubuğu gösterir.
  - **4 Sekmeli Süper Güverte:** Kontrol & Medya (GSMTC + uygulama bazlı ses çıkış yönlendirme + Uyanık Tut/Awake + Çevrimdışı OCR & Renk Damlalığı), Süper Raf & Kalıcı Görselli SQLite Pano Kasası, Canlı Telemetri & Hava Durumu, Stüdyo & Temalar.
  - **`Alt + Space` OmniSearch:** Uygulama/dosya arama, canlı hesap makinesi ve tek tıkla sistem komutları.
- **Kristal Dock (Alt Yatay / Sol Dikey / Sağ Dikey):**
  - 60 FPS yaylı parabolik büyüme dalgası, **Akıllı Kaçınma (`SmartDodge`)**, **Radyal Pencere Hub'ı**, **Akıllı Derle** kategori yığınları ve **Klasör Yığınları**.
  - **`Alt + Sürükle` Pencere & Dock Yönetimi:** Pencereleri herhangi bir yerinden `Alt + Sol Tık` ile taşıyıp `Alt + Sağ Tık` ile boyutlandırabilir; aynı hareketle Dock'un kendisini `Alt / Sol / Sağ` kenara yaslayıp `%75–%135` canlı ölçekleyebilirsin.
  - **`Shift + Sürükle` Snap Zones:** Cam pencere yerleşim bölgeleri.
- **GitHub Üzerinden Otomatik Güncelleme:**
  - Yeni sürüm yayınlandığında Üst Damla'da otomatik bildirim gösterir.
  - **Stüdyo & Ayarlar → Güncellemeleri Kontrol Et** (veya `Alt + Space` → *Güncellemeleri Kontrol Et*) üzerinden **SHA-256** doğrulamalı tek tıkla indirme, mevcut ayarları koruyarak sessiz kurulum ve otomatik yeniden başlatma.

## Tek Tık Kurulum (Kullanıcılar İçin)
1. [Releases (Son Sürüm)](https://github.com/MuharremDidici/ProjectAurora/releases/latest) sayfasından **`Aurora-Kurulum-v*-x64.exe`** dosyasını indirin.
2. Çift tıklayıp **"Tek Tıkla Kur ve Başlat"** butonuna basın (ekstra .NET veya Yönetici izni gerekmez).
3. Bundan sonraki tüm güncellemeler uygulama içinden otomatik bildirilir ve tek tıkla kurulur.

## Yeni Sürüm Yayınlama (Geliştirici İçin — 1 Komut)
GitHub Actions iş akışı ([.github/workflows/release.yml](.github/workflows/release.yml)) her `v*` etiketinde tüm testleri çalıştırır, `.exe` kurulum paketini + SHA-256 `release-manifest.json` dosyasını derler ve otomatik **GitHub Release** olarak yayınlar:

```powershell
# 1. Directory.Build.props içindeki <Version> değerini ör. 2.1.0 yap ve commit at
git add .
git commit -m "Release v2.1.0"
git push origin main

# 2. Sürüm etiketini gönder -> GitHub Actions otomatik Release yayınlar, tüm kullanıcılar bildirim alır!
git tag v2.1.0
git push origin v2.1.0
```

Yerel derleme ve paketleme için:
```powershell
.\Build-Installer.ps1 -Version 2.0.0
```
