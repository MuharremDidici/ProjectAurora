# Project Aurora — Tek Tık Kurulum ve Dağıtım Kılavuzu (`v7.2.0`)

Bu kılavuz, Project Aurora'yı ekstra hiçbir yazılım (`.NET SDK`, `Visual Studio` vb.) kurulu olmayan herhangi bir Windows 11 (64-bit) bilgisayarda **tek tıkla kurmak veya eski sürümlerden yükseltmek** için hazırlanmıştır.

## 1. Hazır Kurulum Dosyaları (`dist/`)

`.\Build-Installer.ps1 -Version "7.2.0"` çalıştırıldığında (veya GitHub Releases üzerinden indirildiğinde) `dist\` klasöründe şu dosyalar yer alır:

1. **`Aurora-Kurulum-v7.2.0-x64.exe` (~3.7 MB Tek Dosyalık Kurulum Sihirbazı):**
   - Saf Rust `aurora.exe` ikilisini `zstd -19` ile gömülü olarak taşır; sıfır `.NET 10`, sıfır harici DLL ve sıfır yönetici izni (UAC / `asInvoker`).
2. **`Project-Aurora-TekTik-Kurulum-v7.2.0.zip` (Paylaşım Paketi):**
   - Kurulum sihirbazı, `release-manifest.json` ve `BENIOKU-Kurulum.txt` dosyalarını içerir.

## 2. Kurulum & Otomatik Yükseltme (`v2.x` ve `v3.x+` Dahil)

- **Yeni Kurulum:** `Aurora-Kurulum-v7.2.0-x64.exe` dosyasına çift tıklayıp **"Kurulumu Başlat"** butonuna basın (~2 saniyede `%LOCALAPPDATA%\Programs\ProjectAurora\aurora.exe` dizinine kurulur).
- **Mevcut Sürümlerden Yükseltme:**
  - `v2.x` (`.NET 10`) ve `v3.x–v7.x` (Saf Rust) sürümleri içindeki **Güncelle** butonu çalıştırıldığında `aurora-setup` hem `--update --wait-pid=<PID>` hem `--silent --relaunch` parametrelerini tanır, çalışan eski sürümü güvenle kapatıp eski `.NET` dosyalarını temizler, atomik olarak `v7.2.0`'ı kurar ve yeniden başlatır.
  - `%LOCALAPPDATA%\ProjectAurora\data\` altındaki tüm ayarlarınız, sabitlenmiş uygulamalarınız, widget yerleşiminiz, pano kasası ve ajanda notlarınız **%100 korunur**.
- **SmartScreen Bilgilendirmesi:** Ticari EV kod imzalama sertifikası olmayan yerel derlemelerde Windows SmartScreen çıkarsa **"Ek bilgi" -> "Yine de çalıştır"** seçilebilir.

## 3. Kaldırma & Görev Çubuğu Güvencesi

- `Windows Ayarları -> Uygulamalar -> Yüklü Uygulamalar -> Project Aurora -> Kaldır`
- Veya komut satırından `"%LOCALAPPDATA%\Programs\ProjectAurora\uninstall.exe" --uninstall`
- Kaldırma sırasında Windows'un özgün görev çubuğu otomatik olarak görünür hale getirilir.

## 4. Geliştirici Paketleme Komutu

```powershell
powershell -ExecutionPolicy Bypass -File .\Build-Installer.ps1 -Version "7.2.0"
```
