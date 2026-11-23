using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Aurora.Platform.Windows.DisplayAndTaskbar;

public static class Win32IconExtractor
{
    private const uint WmGetIcon = 0x007F;
    private const nint IconBig = 1;
    private const nint IconSmall2 = 2;
    private const int GclpHIcon = -14;
    private const uint SmtoAbortIfHung = 0x0002;
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;

    private static readonly ConcurrentDictionary<string, byte[]?> PathIconCache = new(StringComparer.OrdinalIgnoreCase);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFOW
    {
        public nint hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SHGetFileInfoW(string pszPath, uint dwFileAttributes, ref SHFILEINFOW psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessageTimeoutW(nint hWnd, uint msg, nint wParam, nint lParam, uint fuFlags, uint uTimeout, out nint lpdwResult);

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
    private static extern nint GetClassLongPtr64(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint hIcon);

    public static byte[]? GetWindowOrProcessIconPng(nint hwnd, string? executablePath, string? processName)
    {
        bool isFrameHost = string.Equals(processName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase);
        var cacheKey = (!isFrameHost && !string.IsNullOrWhiteSpace(executablePath))
            ? executablePath
            : $"hwnd:{hwnd}";

        if (PathIconCache.TryGetValue(cacheKey, out var cached) && cached is not null)
            return cached;

        byte[]? bytes = null;

        // 1. Try file-based high-DPI shell icon first for standard desktop apps
        if (!isFrameHost && !string.IsNullOrWhiteSpace(executablePath))
        {
            bytes = ExtractFromPath(executablePath);
        }

        // 2. Fallback to live HWND icon (handles UWP ApplicationFrameHost & custom window icons)
        if (bytes is null && hwnd != 0)
        {
            bytes = ExtractFromWindowHandle(hwnd);
        }

        if (bytes is not null)
            PathIconCache[cacheKey] = bytes;

        return bytes;
    }

    public static byte[]? ResolveShortcutIconPng(string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            return null;

        if (PathIconCache.TryGetValue(targetPath, out var cached))
            return cached;

        if (targetPath.StartsWith("appsfolder:", StringComparison.OrdinalIgnoreCase))
        {
            var appId = targetPath["appsfolder:".Length..].Trim();
            var shellIcon = Aurora.Platform.Windows.Apps.InstalledAppCatalogService.ExtractIconForAppsFolderId(appId, 32);
            PathIconCache[targetPath] = shellIcon;
            return shellIcon;
        }

        var resolved = ResolveWellKnownExecutable(targetPath);
        if (resolved is null)
            return null;

        var bytes = ExtractFromPath(resolved);
        PathIconCache[targetPath] = bytes;
        return bytes;
    }

    private static string? ResolveWellKnownExecutable(string target)
    {
        if (target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase))
        {
            var sysSettings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "ImmersiveControlPanel", "SystemSettings.exe");
            return File.Exists(sysSettings) ? sysSettings : null;
        }

        if (File.Exists(target))
            return target;

        var fileName = target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? target : $"{target}.exe";
        string[] probeDirs =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.SystemDirectory,
            Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0")
        ];

        foreach (var dir in probeDirs)
        {
            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static byte[]? ExtractFromPath(string path)
    {
        try
        {
            var resolved = ResolveWellKnownExecutable(path);
            if (resolved is null || !File.Exists(resolved))
                return null;

            var shfi = new SHFILEINFOW();
            var res = SHGetFileInfoW(resolved, 0, ref shfi, (uint)Marshal.SizeOf<SHFILEINFOW>(), ShgfiIcon | ShgfiLargeIcon);
            if (res != 0 && shfi.hIcon != 0)
            {
                try
                {
                    return ConvertHIconToPng(shfi.hIcon);
                }
                finally
                {
                    DestroyIcon(shfi.hIcon);
                }
            }

            using var assoc = Icon.ExtractAssociatedIcon(resolved);
            if (assoc is not null)
            {
                using var bmp = assoc.ToBitmap();
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
        catch
        {
            // Ignore icon extraction errors
        }

        return null;
    }

    private static byte[]? ExtractFromWindowHandle(nint hwnd)
    {
        try
        {
            nint hIcon = 0;
            SendMessageTimeoutW(hwnd, WmGetIcon, IconBig, 0, SmtoAbortIfHung, 45, out hIcon);
            if (hIcon == 0)
                SendMessageTimeoutW(hwnd, WmGetIcon, IconSmall2, 0, SmtoAbortIfHung, 45, out hIcon);
            if (hIcon == 0)
                hIcon = GetClassLongPtr64(hwnd, GclpHIcon);

            if (hIcon != 0)
                return ConvertHIconToPng(hIcon);
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    private static byte[]? ConvertHIconToPng(nint hIcon)
    {
        using var borrowed = Icon.FromHandle(hIcon);
        using var cloned = (Icon)borrowed.Clone();
        using var bmp = cloned.ToBitmap();
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
