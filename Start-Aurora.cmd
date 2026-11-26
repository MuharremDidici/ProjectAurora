@echo off
chcp 65001 >nul
title Project Aurora — Saf Rust Başlatıcı
set "AURORA_REL=%~dp0aurora-rs\target\release\aurora.exe"
set "AURORA_INST=%LOCALAPPDATA%\Programs\ProjectAurora\aurora.exe"

if exist "%AURORA_REL%" (
    start "" "%AURORA_REL%" %*
    exit /b 0
)
if exist "%AURORA_INST%" (
    start "" "%AURORA_INST%" %*
    exit /b 0
)

where cargo >nul 2>&1
if errorlevel 1 (
    set "PATH=%USERPROFILE%\.cargo\bin;%PATH%"
)

echo Derlenmiş aurora.exe bulunamadı; Saf Rust release derlemesi başlatılıyor...
cargo build --manifest-path "%~dp0aurora-rs\Cargo.toml" --release -p aurora-app
if errorlevel 1 (
    echo HATA: Project Aurora derlemesi başarısız oldu.
    pause
    exit /b 1
)
start "" "%AURORA_REL%" %*
