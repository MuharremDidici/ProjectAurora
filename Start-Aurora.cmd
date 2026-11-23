@echo off
title Project Aurora Launcher
set "AURORA_EXE=%~dp0src\Aurora.App\bin\x64\Release\net10.0-windows10.0.22621.0\win-x64\Aurora.App.exe"
if exist "%AURORA_EXE%" (
    start "" "%AURORA_EXE%"
    exit /b 0
)
set "AURORA_DEBUG=%~dp0src\Aurora.App\bin\x64\Debug\net10.0-windows10.0.22621.0\win-x64\Aurora.App.exe"
if exist "%AURORA_DEBUG%" (
    start "" "%AURORA_DEBUG%"
    exit /b 0
)
echo Building Project Aurora...
"%USERPROFILE%\.dotnet\dotnet.exe" build "%~dp0src\Aurora.App\Aurora.App.csproj" -c Release /p:Platform=x64
start "" "%AURORA_EXE%"
