# Project Aurora v7.2 — Dijital Kod İmzalama (Authenticode) ve Otomatik Güncelleme Dağıtım Kılavuzu

Bu kılavuz, `Build-Installer.ps1` üzerinden Authenticode imzalı Saf Rust kurulum paketi üretmeyi ve `aurora-storage` güncelleyicisiyle uyumlu `release-manifest.json` yayınlamayı açıklar.

---

## 1. İmzalı Kurulum Paketi Üretme

`Build-Installer.ps1` hem derlenen `aurora-rs\target\release\aurora.exe` dosyasını hem de tek dosya kurulum sihirbazını (`Aurora-Kurulum-v7.2.0-x64.exe`) SHA-256 + RFC 3161 zaman damgasıyla imzalayabilir:

### Seçenek A: `.pfx` Sertifika Dosyası ile
```powershell
.\Build-Installer.ps1 `
    -Version "7.2.0" `
    -PfxPath "C:\Certs\aurora-codesign.pfx" `
    -PfxPassword "GIZLI_PAROLA" `
    -ReleaseDownloadBaseUrl "https://github.com/MuharremDidici/ProjectAurora/releases/download/v7.2.0"
```

### Seçenek B: Windows Sertifika Deposu / USB Token (EV / OV) ile
```powershell
.\Build-Installer.ps1 `
    -Version "7.2.0" `
    -CertThumbprint "A1B2C3D4E5F6..." `
    -ReleaseDownloadBaseUrl "https://github.com/MuharremDidici/ProjectAurora/releases/download/v7.2.0"
```

> **Gereksinim:** Windows 10/11 SDK içindeki `signtool.exe` otomatik bulunur (`C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe`).

---

## 2. Otomatik Üretilen Dağıtım Çıktıları (`dist\`)

- `Aurora-Kurulum-v7.2.0-x64.exe` — Tek dosya `zstd-19` Saf Rust kurulum sihirbazı (`asInvoker`, sıfır UAC)
- `Project-Aurora-TekTik-Kurulum-v7.2.0.zip` — Paylaşım arşivi
- `release-manifest.json` — SemVer `version`, UTC yayın tarihi, HTTPS `downloadUrl`, `sha256` ve `releaseNotes`
- `BENIOKU-Kurulum.txt` — Hızlı kurulum ve SHA-256 özeti

---

## 3. Güncelleme Denetleyicisi ve Geriye Dönük Uyumluluk (`aurora-storage::updater`)

- Varsayılan adres: `https://github.com/MuharremDidici/ProjectAurora/releases/latest/download/release-manifest.json`
- CDN manifesti bulunamaz veya parse edilemezse güncelleyici otomatik olarak `https://api.github.com/repos/MuharremDidici/ProjectAurora/releases/latest` API yanıtına düşer ve `Aurora-Kurulum-*.exe` varlığını seçer.
- İndirilen paket `%LOCALAPPDATA%\ProjectAurora\updates\` içine kaydedilir, `SHA-256` doğrulamasından geçtikten sonra çalıştırılır ve hem eski `v2.x` (`--update --wait-pid=<PID>`) hem yeni `v3.x+` (`--silent --relaunch --wait-pid=<PID>`) parametreleriyle sorunsuz yükseltme yapılır.
