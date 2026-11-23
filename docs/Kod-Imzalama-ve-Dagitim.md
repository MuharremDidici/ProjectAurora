# Project Aurora v2.0 — Dijital Kod İmzalama (Authenticode) ve Otomatik Güncelleme Dağıtım Kılavuzu

Bu kılavuz, `Build-Installer.ps1` üzerinden **Windows SmartScreen uyarısı olmadan** imzalı kurulum paketi üretmeyi ve opt-in güncelleme manifestini yayınlamayı açıklar.

---

## 1. İmzalı Kurulum Paketi Üretme

`Build-Installer.ps1` hem içteki `Aurora.App.exe` dosyasını hem de tek tık kurulum sihirbazını (`Aurora-Kurulum-v2.0-x64.exe`) SHA-256 + RFC 3161 zaman damgasıyla otomatik imzalar:

### Seçenek A: `.pfx` Sertifika Dosyası ile
```powershell
.\Build-Installer.ps1 `
    -PfxPath "C:\Certs\aurora-codesign.pfx" `
    -PfxPassword "GIZLI_PAROLA" `
    -ReleaseDownloadBaseUrl "https://github.com/KULLANICI/project-aurora/releases/download/v2.0.0"
```

### Seçenek B: Windows Sertifika Deposu / Donanım Token (EV / OV USB Token) ile
```powershell
.\Build-Installer.ps1 `
    -CertThumbprint "A1B2C3D4E5F6..." `
    -ReleaseDownloadBaseUrl "https://github.com/KULLANICI/project-aurora/releases/download/v2.0.0"
```

> **Gereksinim:** Windows 10/11 SDK içindeki `signtool.exe` otomatik bulunur (`C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe`).

---

## 2. Otomatik Üretilen Dağıtım Çıktıları (`dist\`)

Her derlemede şu dosyalar otomatik oluşturulur:
- `Aurora-Kurulum-v2.0-x64.exe` — Tek tık self-contained kurulum sihirbazı (`asInvoker`, UAC gerektirmez)
- `Project-Aurora-TekTik-Kurulum-v2.0.zip` — WhatsApp / Drive paylaşım paketi
- `release-manifest.json` — Sürüm, indirme URL'si, sürüm notu ve **SHA-256** doğrulama özeti

---

## 3. Güncelleme Denetleyicisi (`AuroraUpdateService`)

Aurora varsayılan olarak **tamamen çevrimdışı** çalışır (arka planda gizli ağ isteği yapmaz). Bir dağıtım adresi tanımlamak için `%LOCALAPPDATA%\ProjectAurora\data\shell_settings.json` içinde:

```json
"updateManifestUrl": "https://github.com/KULLANICI/project-aurora/releases/latest/download/release-manifest.json"
```

veya doğrudan GitHub Releases API adresi (`https://api.github.com/repos/KULLANICI/project-aurora/releases/latest`) tanımlanabilir. Kullanıcı **Stüdyo → v2.0.0** düğmesine tıkladığında sürüm karşılaştırması yapılır ve yeni sürüm varsa tek tık indirme düğmesi görünür.
