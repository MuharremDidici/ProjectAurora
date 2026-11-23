param(
    [string]$Version = "",
    [string]$CertThumbprint = "",
    [string]$PfxPath = "",
    [string]$PfxPassword = "",
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [string]$ReleaseDownloadBaseUrl = "https://github.com/MuharremDidici/ProjectAurora/releases/latest/download",
    [string]$ReleaseNotes = "Project Aurora: Dinamik Damla Canli Donanim OSD, Sol/Sag/Alt Dikey & Yatay Dock, Alt+Surukle Pencere ve Dock Yonetimi, Kalici SQLite Pano Kasasi, Shift+Surukle Snap Zones ve GitHub Otomatik Guncelleme."
)

$ErrorActionPreference = "Stop"

$root   = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnet = if (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") { "$env:USERPROFILE\.dotnet\dotnet.exe" } else { "dotnet" }

# Read version from Directory.Build.props if -Version was not explicitly provided
if ([string]::IsNullOrWhiteSpace($Version)) {
    $propsPath = Join-Path $root "Directory.Build.props"
    if (Test-Path $propsPath) {
        [xml]$propsXml = Get-Content $propsPath
        $propVer = $propsXml.Project.PropertyGroup.Version
        if (-not [string]::IsNullOrWhiteSpace($propVer)) {
            $Version = $propVer.Trim()
        }
    }
}
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = "2.0.0" }

$semver = $Version.Trim().TrimStart('v', 'V')
$parts  = $semver.Split('.')
if ($parts.Length -eq 2) { $semver = "$semver.0"; $parts = $semver.Split('.') }
$shortVer = if ($parts.Length -ge 3 -and $parts[2] -eq "0") { "$($parts[0]).$($parts[1])" } else { $semver }
$setupBaseName = "Aurora-Kurulum-v$shortVer-x64"

$stageAppDir = Join-Path $root "installer\stage\app"
$payloadDir  = Join-Path $root "installer\Aurora.Setup\Payload"
$payloadZip  = Join-Path $payloadDir "Aurora-Payload.zip"
$distDir     = Join-Path $root "dist"

function Invoke-OptionalAuthenticodeSign([string]$TargetPath) {
    if ([string]::IsNullOrWhiteSpace($CertThumbprint) -and [string]::IsNullOrWhiteSpace($PfxPath)) {
        return
    }
    $signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
    if (-not $signtool) {
        $sdkBin = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
        $signtool = $sdkBin
    }
    if (-not $signtool) {
        Write-Warning "signtool.exe bulunamadi; dijital imzalama atlandi. (Windows 10/11 SDK gerekir.)"
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

Write-Host "[1/5] Aurora.App v$semver (win-x64, Self-Contained) yayinlaniyor..." -ForegroundColor Cyan
$runningProcs = @(Get-Process -Name "Aurora.App" -ErrorAction SilentlyContinue)
if ($runningProcs.Count -gt 0) {
    Stop-Process -Name "Aurora.App" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 600
}

if (Test-Path $stageAppDir) { Remove-Item $stageAppDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stageAppDir | Out-Null
New-Item -ItemType Directory -Force -Path $payloadDir | Out-Null
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

Get-ChildItem $distDir -Filter "Aurora-Kurulum-*.exe" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
Get-ChildItem $distDir -Filter "Project-Aurora-TekTik-Kurulum-*.zip" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

& $dotnet publish (Join-Path $root "src\Aurora.App\Aurora.App.csproj") `
    -c Release -r win-x64 -p:Platform=x64 --self-contained true `
    -p:Version=$semver -p:InformationalVersion=$semver `
    -p:DebugType=none -p:DebugSymbols=false `
    -o $stageAppDir --nologo

if ($LASTEXITCODE -ne 0 -or !(Test-Path (Join-Path $stageAppDir "Aurora.App.exe"))) {
    throw "HATA: Aurora.App yayinlanamadi (dosya kilidi veya derleme hatasi)."
}

Invoke-OptionalAuthenticodeSign (Join-Path $stageAppDir "Aurora.App.exe")

# Add standalone taskbar recovery batch into installed directory
$stageRestoreCmd = Join-Path $stageAppDir "Windows-Gorev-Cubugunu-Geri-Getir.cmd"
@"
@echo off
chcp 65001 >nul
"%~dp0Aurora.App.exe" --restore-taskbar
echo Windows gorev cubugu gorunur hale getirildi.
"@ | Set-Content -Path $stageRestoreCmd -Encoding UTF8

Get-ChildItem $stageAppDir -Include *.pdb, *.xml -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "[2/5] Kurulum arsivi (Aurora-Payload.zip) sikistiriliyor..." -ForegroundColor Cyan
if (Test-Path $payloadZip) { Remove-Item $payloadZip -Force }
Compress-Archive -Path "$stageAppDir\*" -DestinationPath $payloadZip -CompressionLevel Optimal

Write-Host "[3/5] Tek Tik Kurulum Sihirbazi ($setupBaseName.exe) derleniyor..." -ForegroundColor Cyan
& $dotnet publish (Join-Path $root "installer\Aurora.Setup\Aurora.Setup.csproj") `
    -c Release -r win-x64 --self-contained true `
    -p:Version=$semver -p:InformationalVersion=$semver `
    -p:SetupAssemblyName=$setupBaseName `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -p:DebugSymbols=false `
    -o $distDir --nologo

if ($LASTEXITCODE -ne 0) { throw "HATA: Aurora.Setup derlenemedi." }

Get-ChildItem $distDir -Include *.pdb, *.xml | Remove-Item -Force -ErrorAction SilentlyContinue

$setupExe = Join-Path $distDir "$setupBaseName.exe"
Invoke-OptionalAuthenticodeSign $setupExe

Write-Host "[4/5] SHA-256 & Otomatik Guncelleme Manifesti (release-manifest.json) uretiliyor..." -ForegroundColor Cyan
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
$manifestObj | ConvertTo-Json -Depth 4 | Set-Content -Path $manifestPath -Encoding UTF8

Write-Host "[5/5] Paylasim paketi (.zip) hazirlaniyor..." -ForegroundColor Cyan
$readmeTxt = Join-Path $distDir "BENIOKU-Kurulum.txt"
@"
PROJECT AURORA v$semver — TEK TIK KURULUM KILAVUZU (Windows 11 64-Bit)
====================================================================

1. "$setupBaseName.exe" dosyasina cift tiklayin.
   (Not: Imzasiz yerel derlemelerde Windows SmartScreen uyarisi cikarsa "Ek bilgi" -> "Yine de calistir" secenegine tiklayin.)
2. Acilan pencerede "Tek Tikla Kur ve Baslat" butonuna basin.
   Yonetici izni veya ekstra .NET kurulumu gerekmez; 3 saniye icinde kurulup otomatik acilir.
3. Bundan sonraki tum guncellemeler GitHub uzerinden otomatik bildirilir ve uygulama icinden (Studyo & Ayarlar -> Guncellemeleri Kontrol Et) tek tikla kurulabilir.

GitHub Deposu:
https://github.com/MuharremDidici/ProjectAurora

SHA-256 Dogrulama Ozeti:
$setupHash

KALDIRMAK ICIN:
- Windows Ayarlari -> Uygulamalar -> Yuklu Uygulamalar -> "Project Aurora" -> Kaldir
  veya kurulum sihirbazini tekrar acip "Mevcut Kurulumu Kaldir" butonuna tiklayabilirsiniz.
  Kaldirildiginda Windows'un kendi gorev cubugu otomatik olarak geri gelir.
"@ | Set-Content -Path $readmeTxt -Encoding UTF8

$shareZip = Join-Path $distDir "Project-Aurora-TekTik-Kurulum-v$shortVer.zip"
if (Test-Path $shareZip) { Remove-Item $shareZip -Force }
Compress-Archive -Path $setupExe, $readmeTxt, $manifestPath -DestinationPath $shareZip -CompressionLevel Optimal

if (Test-Path (Join-Path $root "installer\stage")) {
    Remove-Item (Join-Path $root "installer\stage") -Recurse -Force
}

Write-Host ""
Write-Host "HAZIR! Paylasima hazir v$semver kurulum dosyalari:" -ForegroundColor Green
Get-ChildItem $distDir | Select-Object Name, @{Name="Boyut_MB";Expression={[math]::Round($_.Length / 1MB, 1)}} | Format-Table -AutoSize
