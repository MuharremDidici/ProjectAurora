param(
    [string]$Version = "",
    [string]$CertThumbprint = "",
    [string]$PfxPath = "",
    [string]$PfxPassword = "",
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [string]$ReleaseDownloadBaseUrl = "https://github.com/MuharremDidici/ProjectAurora/releases/latest/download",
    [string]$ReleaseNotes = "Project Aurora v3.0.0: Saf Rust Win32 motoru + tek paylasimli WebView2 renderer (sifir .NET 10, ~3.4 MB tek dosya UAC'siz kurulum), Dairesel Dallanan Pencere Kuresi, canli laptop & Bluetooth pil gostergesi, olay-tabanli sifir-sorgulu pano, endustriyel kilitlenmez girdi/odak mimarisi ve 7 modern 2026 gorsel tema."
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$cargoBin = Join-Path $env:USERPROFILE ".cargo\bin"
if (Test-Path $cargoBin) {
    $env:PATH = "$cargoBin;$env:PATH"
}
$manifestCargo = Join-Path $root "aurora-rs\Cargo.toml"

# Read version from aurora-rs/Cargo.toml if -Version was not explicitly provided
if ([string]::IsNullOrWhiteSpace($Version) -and (Test-Path $manifestCargo)) {
    $cargoText = Get-Content $manifestCargo -Raw
    if ($cargoText -match '(?m)^version\s*=\s*"([^"]+)"') {
        $Version = $Matches[1].Trim()
    }
}
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = "3.0.0" }

$semver = $Version.Trim().TrimStart('v', 'V')
$parts  = $semver.Split('.')
if ($parts.Length -eq 2) { $semver = "$semver.0"; $parts = $semver.Split('.') }
$setupBaseName = "Aurora-Kurulum-v$semver-x64"

$distDir = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $distDir | Out-Null
Get-ChildItem $distDir -Filter "Aurora-Kurulum-*.exe" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
Get-ChildItem $distDir -Filter "Project-Aurora-TekTik-Kurulum-*.zip" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

function Invoke-OptionalAuthenticodeSign([string]$TargetPath) {
    if ([string]::IsNullOrWhiteSpace($CertThumbprint) -and [string]::IsNullOrWhiteSpace($PfxPath)) {
        return
    }
    $signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
    if (-not $signtool) {
        $signtool = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $signtool) {
        Write-Warning "signtool.exe bulunamadi; dijital imzalama atlandi."
        return
    }

    $argsList = @("sign", "/fd", "SHA256", "/tr", $TimestampUrl, "/td", "SHA256")
    if (-not [string]::IsNullOrWhiteSpace($PfxPath)) {
        $argsList += @("/f", $PfxPath)
        if (-not [string]::IsNullOrWhiteSpace($PfxPassword)) {
            $argsList += @("/p", $PfxPassword)
        }
    } else {
        $argsList += @("/sha1", $CertThumbprint)
    }
    $argsList += $TargetPath

    Write-Host "  -> Authenticode imzalaniyor: $(Split-Path -Leaf $TargetPath)" -ForegroundColor Yellow
    & $signtool @argsList
    if ($LASTEXITCODE -ne 0) { throw "HATA: Authenticode imzalama basarisiz ($TargetPath)." }
}

Write-Host "[1/4] Saf Rust Hibrit aurora.exe (v$semver, LTO Release x64) derleniyor..." -ForegroundColor Cyan
& cargo build --manifest-path $manifestCargo --release -p aurora-app
if ($LASTEXITCODE -ne 0) { throw "HATA: aurora-app release derlemesi basarisiz." }

$auroraBin = Join-Path $root "aurora-rs\target\release\aurora.exe"
if (!(Test-Path $auroraBin)) { throw "HATA: aurora.exe bulunamadi ($auroraBin)." }
Invoke-OptionalAuthenticodeSign $auroraBin

Write-Host "[2/4] Tek Dosya zstd-19 Kurulum Sihirbazi ($setupBaseName.exe) derleniyor..." -ForegroundColor Cyan
$env:AURORA_PAYLOAD_BIN = $auroraBin
& cargo build --manifest-path $manifestCargo --release -p aurora-setup
if ($LASTEXITCODE -ne 0) { throw "HATA: aurora-setup release derlemesi basarisiz." }

$setupBuilt = Join-Path $root "aurora-rs\target\release\aurora-setup.exe"
$setupExe   = Join-Path $distDir "$setupBaseName.exe"
Copy-Item -Path $setupBuilt -Destination $setupExe -Force
Invoke-OptionalAuthenticodeSign $setupExe

Write-Host "[3/4] SHA-256 & Otomatik Guncelleme Manifesti (release-manifest.json) uretiliyor..." -ForegroundColor Cyan
$setupHash = (Get-FileHash -Path $setupExe -Algorithm SHA256).Hash
$downloadUrl = if ([string]::IsNullOrWhiteSpace($ReleaseDownloadBaseUrl)) {
    "$setupBaseName.exe"
} else {
    "$($ReleaseDownloadBaseUrl.TrimEnd('/'))/$setupBaseName.exe"
}
$manifestObj = [ordered]@{
    version        = $semver
    publishedAtUtc = (Get-Date).ToUniversalTime().ToString("O")
    downloadUrl    = $downloadUrl
    sha256         = $setupHash
    releaseNotes   = $ReleaseNotes
}
$manifestPath = Join-Path $distDir "release-manifest.json"
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($manifestPath, ($manifestObj | ConvertTo-Json -Depth 4), $utf8NoBom)

Write-Host "[4/4] Paylasim paketi (.zip) ve BENIOKU hazirlaniyor..." -ForegroundColor Cyan
$readmeTxt = Join-Path $distDir "BENIOKU-Kurulum.txt"
@"
PROJECT AURORA v$semver — SAF RUST KURULUM KILAVUZU (Windows 11 x64)
====================================================================

1. "$setupBaseName.exe" dosyasina cift tiklayin (~3.4 MB tek dosya, sifir .NET 10, sifir harici DLL!).
   (Not: Imzasiz yerel derlemelerde SmartScreen uyarisi cikarsa "Ek bilgi" -> "Yine de calistir" secin.)
2. "Kurulumu Baslat" butonuna basin.
   Yonetici izni (UAC) gerekmez; onceki v2.x ayarlariniz, pano gecmisiniz ve notlariniz %100 korunur.
3. Tum guncellemeler GitHub uzerinden SHA-256 dogrulamasiyla uygulama icinden tek tikla kurulabilir.

GitHub Deposu:
https://github.com/MuharremDidici/ProjectAurora

SHA-256 Dogrulama Ozeti:
$setupHash

KALDIRMAK ICIN:
- Windows Ayarlari -> Uygulamalar -> Yuklu Uygulamalar -> "Project Aurora" -> Kaldir
  (Windows gorev cubugu otomatik olarak geri yuklenir.)
"@ | Set-Content -Path $readmeTxt -Encoding UTF8

$shareZip = Join-Path $distDir "Project-Aurora-TekTik-Kurulum-v$semver.zip"
Compress-Archive -Path $setupExe, $readmeTxt, $manifestPath -DestinationPath $shareZip -CompressionLevel Optimal

Write-Host ""
Write-Host "HAZIR! Paylasima hazir v$semver (Saf Rust Motoru) kurulum dosyalari:" -ForegroundColor Green
Get-ChildItem $distDir | Select-Object Name, @{Name="Boyut_MB";Expression={[math]::Round($_.Length / 1MB, 2)}} | Format-Table -AutoSize
