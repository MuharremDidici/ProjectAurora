@echo off
chcp 65001 >nul
title Project Aurora Studio
set "AURORA_REL=%~dp0aurora-rs\target\release\aurora.exe"
set "AURORA_INST=%LOCALAPPDATA%\Programs\ProjectAurora\aurora.exe"

if exist "%AURORA_REL%" (
    start "" "%AURORA_REL%" --studio %*
    exit /b 0
)
if exist "%AURORA_INST%" (
    start "" "%AURORA_INST%" --studio %*
    exit /b 0
)

echo aurora.exe bulunamadı. Önce Start-Aurora.cmd çalıştırın veya kurulum sihirbazını kullanın.
pause
exit /b 1
