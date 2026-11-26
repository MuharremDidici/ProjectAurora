@echo off
chcp 65001 >nul
title Windows Görev Çubuğunu Geri Yükle
set "AURORA_REL=%~dp0aurora-rs\target\release\aurora.exe"
set "AURORA_INST=%LOCALAPPDATA%\Programs\ProjectAurora\aurora.exe"

if exist "%AURORA_INST%" (
    "%AURORA_INST%" --restore-taskbar
    echo Windows görev çubuğu aurora.exe üzerinden geri yüklendi.
    exit /b 0
)
if exist "%AURORA_REL%" (
    "%AURORA_REL%" --restore-taskbar
    echo Windows görev çubuğu aurora.exe üzerinden geri yüklendi.
    exit /b 0
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public class TbRestore { [StructLayout(LayoutKind.Sequential)] public struct RECT { public int l,t,r,b; } [StructLayout(LayoutKind.Sequential)] public struct ABD { public uint cbSize; public IntPtr hWnd; public uint msg; public uint edge; public RECT rc; public IntPtr lParam; } [DllImport(\"user32.dll\", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowW(string c, string w); [DllImport(\"user32.dll\", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowExW(IntPtr p, IntPtr a, string c, string w); [DllImport(\"user32.dll\")] public static extern bool ShowWindow(IntPtr h, int s); [DllImport(\"shell32.dll\")] public static extern UIntPtr SHAppBarMessage(uint m, ref ABD d); public static void Run() { var h = FindWindowW(\"Shell_TrayWnd\", null); if (h != IntPtr.Zero) ShowWindow(h, 5); var s = IntPtr.Zero; while ((s = FindWindowExW(IntPtr.Zero, s, \"Shell_SecondaryTrayWnd\", null)) != IntPtr.Zero) ShowWindow(s, 5); var d = new ABD { cbSize = (uint)Marshal.SizeOf(typeof(ABD)), hWnd = h, lParam = IntPtr.Zero }; SHAppBarMessage(0x0000000A, ref d); } }'; [TbRestore]::Run()"
echo Windows görev çubuğu görünür hale getirildi.
