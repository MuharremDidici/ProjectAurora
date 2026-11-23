@echo off
title Project Aurora Studio
set "AURORA_EXE=%~dp0src\Aurora.App\bin\x64\Release\net10.0-windows10.0.22621.0\win-x64\Aurora.App.exe"
if exist "%AURORA_EXE%" (
    start "" "%AURORA_EXE%" --studio
    exit /b 0
)
echo Aurora.App.exe bulunamadi. Once Start-Aurora.cmd calistirin.
pause
