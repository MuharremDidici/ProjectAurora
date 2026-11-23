using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Aurora.Platform.Windows.DisplayAndTaskbar;

namespace Aurora.Platform.Windows.Apps;

public sealed record InstalledAppEntry(
    string Id,
    string Name,
    string LaunchPath,
    string Category,
    byte[]? IconPngBytes,
    string Keywords = "",
    string Kind = "app");

public sealed class InstalledAppCatalogService
{
    private static readonly Guid FolderIdAppsFolder = new("1E87508D-89C2-42F0-8A7E-645A0F50CA58");
    private static readonly Guid BhidEnumItems = new("94F60519-2850-4924-AA5A-D15E84868039");
    private static readonly Guid IidIShellItem = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");
    private static readonly Guid IidIEnumShellItems = new("70629033-E363-4A28-A567-0DB78006E6D7");

    private const uint SigdnNormalDisplay = 0x00000000;
    private const uint SigdnParentRelativeParsing = 0x80018001;
    private const uint SiigbfResizeToFit = 0x00000000;
    private const uint SiigbfBiggerSizeOk = 0x00000001;
    private const uint SiigbfIconOnly = 0x00000004;

    private static readonly string[] NoiseKeywords =
    [
        "uninstall", "kaldır", "readme", "benioku", "help", "yardım",
        "website", "license", "lisans", "release notes", "manual",
        "about ", "check for updates", "documentation", "dokümantasyon",
        "migration", "debuggable package", "application verifier"
    ];

    private static readonly string[] PriorityAppKeywords =
    [
        "Dosya Gezgini", "Brave", "Chrome", "Opera", "Firefox", "Edge", "ChatGPT", "Claude",
        "Visual Studio", "Code", "Antigravity", "Android Studio", "Cursor", "Discord", "Spotify",
        "Steam", "Teams", "WhatsApp", "Telegram", "Ayarlar", "Görev Yöneticisi",
        "Ekran Alıntısı", "Hesap Makinesi", "Not Defteri", "PowerShell", "Terminal",
        "Fotoğraflar", "Microsoft Store", "Word", "Excel", "PowerPoint", "Paint"
    ];

    private static readonly (string Name, string Target, string Category, string Keywords, string Kind)[] BuiltInSettingsAndTools =
    [
        // Core Windows 11 Tools
        ("Dosya Gezgini", "explorer.exe", "Sistem", "file explorer klasor dosyalar bilgisayarim this pc", "app"),
        ("Windows Ayarları", "ms-settings:", "Ayarlar", "settings sistem ayarlar denetim", "setting"),
        ("Görev Yöneticisi", "taskmgr.exe", "Sistem", "task manager performans cpu ram islemler", "tool"),
        ("Ekran Alıntısı Al", "ms-screenclip:", "Araçlar", "snipping tool screenshot ekran goruntusu kirp", "tool"),
        ("Hesap Makinesi", "calc.exe", "Araçlar", "calculator hesapla matematik", "app"),
        ("Not Defteri", "notepad.exe", "Araçlar", "notepad metin not text", "app"),
        ("Paint", "mspaint.exe", "Araçlar", "resim cizim boya", "app"),
        ("Denetim Masası", "control.exe", "Sistem", "control panel klasik ayarlar", "tool"),
        ("Komut İstemi (CMD)", "cmd.exe", "Geliştirme", "command prompt terminal konsol", "tool"),
        ("PowerShell", "powershell.exe", "Geliştirme", "terminal ps komut kabuk", "tool"),

        // Deep Windows 11 Settings Pages (ms-settings:)
        ("Ekran & Çözünürlük Ayarları", "ms-settings:display", "Ayarlar", "monitor parlaklik cozunurluk olcek gece isigi hdr hz", "setting"),
        ("Ses & Mikrofon Ayarları", "ms-settings:sound", "Ayarlar", "audio hoparlor kulaklik mikrofon volume ses", "setting"),
        ("Wi-Fi & Kablosuz Ağ", "ms-settings:network-wifi", "Ayarlar", "internet wifi kablosuz ag baglanti", "setting"),
        ("Ağ ve İnternet Ayarları", "ms-settings:network-status", "Ayarlar", "ethernet lan dns ip network", "setting"),
        ("Bluetooth ve Cihazlar", "ms-settings:bluetooth", "Ayarlar", "kulaklik fare klavye eslestir bluetooth", "setting"),
        ("Yüklü Uygulamalar & Kaldır", "ms-settings:appsfeatures", "Ayarlar", "program kaldir apps features uninstall", "setting"),
        ("Başlangıç Uygulamaları", "ms-settings:startupapps", "Ayarlar", "startup acilis otomatik baslat", "setting"),
        ("Depolama & Disk Alanı", "ms-settings:storagesense", "Ayarlar", "storage disk temizle alan ssd", "setting"),
        ("Güç, Uyku ve Pil", "ms-settings:powersleep", "Ayarlar", "battery power uyku ekran suresi enerji", "setting"),
        ("Windows Update (Güncelleme)", "ms-settings:windowsupdate", "Ayarlar", "guncelleme update yama", "setting"),
        ("Kişiselleştirme & Duvar Kağıdı", "ms-settings:personalization", "Ayarlar", "wallpaper arka plan tema personalization", "setting"),
        ("Renkler & Koyu/Açık Mod", "ms-settings:colors", "Ayarlar", "dark light tema renk accent", "setting"),
        ("Saat, Tarih ve Dil", "ms-settings:dateandtime", "Ayarlar", "clock time saat tarih bolge klavye dil", "setting"),
        ("Fare & Dokunmatik Yüzey", "ms-settings:mousetouchpad", "Ayarlar", "mouse imlec hiz touchpad", "setting"),
        ("Gizlilik ve Güvenlik", "ms-settings:privacy", "Ayarlar", "privacy kamera mikrofon konum izin", "setting"),
        ("Windows Güvenliği (Defender)", "windowsdefender:", "Ayarlar", "antivirus virus guvenlik duvari firewall", "setting"),
        ("Sistem Hakkında (Özellikler)", "ms-settings:about", "Ayarlar", "about bilgisayar adi ram islemci sistem ozellikleri", "setting"),
        ("Bildirim Ayarları", "ms-settings:notifications", "Ayarlar", "notifications odak rahatsiz etme", "setting"),
        ("VPN Ayarları", "ms-settings:network-vpn", "Ayarlar", "vpn baglanti ozel ag", "setting"),
        ("Proxy Ayarları", "ms-settings:network-proxy", "Ayarlar", "proxy vekil sunucu", "setting"),
        ("Oyun Modu", "ms-settings:gaming-gamemode", "Ayarlar", "game mode oyun performans", "setting"),
        ("Geliştirici Ayarları", "ms-settings:developers", "Ayarlar", "developer mode gelistirici sudo", "setting"),
        ("Varsayılan Uygulamalar", "ms-settings:defaultapps", "Ayarlar", "default browser varsayilan tarayici", "setting"),
        ("Yazıcılar ve Tarayıcılar", "ms-settings:printers", "Ayarlar", "printer yazici tarayici", "setting"),
        ("Hesaplar ve Oturum", "ms-settings:yourinfo", "Ayarlar", "account hesap parola pin", "setting"),

        // Windows Administrative / Run Power Tools
        ("Cihaz Yöneticisi", "devmgmt.msc", "Sistem", "device manager surucu driver donanim", "tool"),
        ("Hizmetler (Services)", "services.msc", "Sistem", "services servisler arka plan", "tool"),
        ("Disk Yönetimi", "diskmgmt.msc", "Sistem", "disk management bolum partition surucu", "tool"),
        ("Kayıt Defteri (Regedit)", "regedit.exe", "Sistem", "registry editor kayit defteri", "tool"),
        ("Sistem Bilgisi (msinfo32)", "msinfo32.exe", "Sistem", "system information bios anakart donanim", "tool"),
        ("DirectX Tanılama (dxdiag)", "dxdiag.exe", "Sistem", "gpu ekran karti directx", "tool"),
        ("Program Ekle veya Kaldır", "appwiz.cpl", "Sistem", "klasik program kaldir", "tool"),
        ("Ağ Bağlantıları (ncpa.cpl)", "ncpa.cpl", "Sistem", "adapter ethernet wifi dns", "tool"),
        ("Uzak Masaüstü (RDP)", "mstsc.exe", "Araçlar", "remote desktop uzak baglanti rdp", "tool"),
        ("Kaynak İzleyici", "resmon.exe", "Sistem", "resource monitor cpu disk ag izle", "tool"),
        ("Windows Sürümü (winver)", "winver.exe", "Sistem", "build version surum", "tool"),
        ("Ortam Değişkenleri", "rundll32.exe|sysdm.cpl,EditEnvironmentVariables", "Geliştirme", "path environment variables env", "tool")
    ];

    private List<InstalledAppEntry>? _cachedApps;
    private bool _cachedWithIcons;
    private DateTimeOffset _lastScanUtc = DateTimeOffset.MinValue;
    private readonly object _scanLock = new();

    public IReadOnlyList<InstalledAppEntry> GetInstalledApps(bool forceRefresh = false, bool includeIcons = true)
    {
        lock (_scanLock)
        {
            if (!forceRefresh && _cachedApps is not null &&
                (!includeIcons || _cachedWithIcons) &&
                (DateTimeOffset.UtcNow - _lastScanUtc).TotalMinutes < 10)
            {
                return _cachedApps;
            }

            var byName = new Dictionary<string, InstalledAppEntry>(StringComparer.OrdinalIgnoreCase);

            // 1. Enumerate ALL Windows 11 Apps via native shell:AppsFolder COM (UWP, MSIX, Store, Electron, Win32!)
            foreach (var app in EnumerateShellAppsFolder(includeIcons))
            {
                if (string.IsNullOrWhiteSpace(app.Name) || app.Name.Length < 2)
                    continue;
                if (NoiseKeywords.Any(k => app.Name.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    continue;

                byName.TryAdd(app.Name, app);
            }

            // 2. Merge Start Menu & Desktop shortcuts (.lnk / .url) for anything not in AppsFolder (games, portable tools, user links)
            MergeShortcutsFromRoots(byName, includeIcons);

            // 3. Add curated Windows 11 Settings pages & System/Admin Power Tools (prefer existing icon if already discovered)
            var settingsIcon = includeIcons ? Win32IconExtractor.ResolveShortcutIconPng("ms-settings:") : null;
            foreach (var item in BuiltInSettingsAndTools)
            {
                if (byName.TryGetValue(item.Name, out var existing))
                {
                    // Enrich keywords on existing app entry
                    byName[item.Name] = existing with
                    {
                        Keywords = string.IsNullOrWhiteSpace(existing.Keywords) ? item.Keywords : $"{existing.Keywords} {item.Keywords}"
                    };
                    continue;
                }

                byte[]? icon = !includeIcons
                    ? null
                    : (item.Kind == "setting"
                        ? settingsIcon
                        : Win32IconExtractor.ResolveShortcutIconPng(item.Target.Split('|')[0]));

                byName[item.Name] = new InstalledAppEntry(
                    Id: $"{item.Kind}-{byName.Count}",
                    Name: item.Name,
                    LaunchPath: item.Target,
                    Category: item.Category,
                    IconPngBytes: icon,
                    Keywords: item.Keywords,
                    Kind: item.Kind);
            }

            _cachedApps = byName.Values
                .OrderByDescending(a => a.Kind == "app")
                .ThenByDescending(a => PriorityAppKeywords.Any(p => a.Name.Contains(p, StringComparison.OrdinalIgnoreCase)))
                .ThenByDescending(a => a.IconPngBytes is { Length: > 0 })
                .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            _cachedWithIcons = includeIcons;
            _lastScanUtc = DateTimeOffset.UtcNow;
            return _cachedApps;
        }
    }

    public static bool LaunchTarget(string rawTarget, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(rawTarget))
        {
            errorMessage = "Hedef yol boş.";
            return false;
        }

        string target = rawTarget.Trim();
        try
        {
            // 1. UWP / MSIX / Store / AppsFolder AUMID launch
            if (target.StartsWith("appsfolder:", StringComparison.OrdinalIgnoreCase))
            {
                string appId = target["appsfolder:".Length..].Trim();
                if (string.IsNullOrWhiteSpace(appId))
                {
                    errorMessage = "Geçersiz uygulama kimliği.";
                    return false;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"shell:AppsFolder\\{appId}",
                    UseShellExecute = false
                });
                return true;
            }

            // 2. Shell command execution mode ("> ipconfig", "> ping ...", etc.)
            if (target.StartsWith("cmdrun:", StringComparison.OrdinalIgnoreCase))
            {
                string cmd = target["cmdrun:".Length..].Trim();
                if (string.IsNullOrWhiteSpace(cmd))
                    return false;

                string escaped = cmd.Replace("\"", "\\\"");
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoExit -Command \"{escaped}\"",
                    UseShellExecute = true
                });
                return true;
            }

            // 3. Explicit "exe|arguments" compound launch (e.g., rundll32.exe|sysdm.cpl,EditEnvironmentVariables)
            if (target.Contains('|'))
            {
                var parts = target.Split('|', 2);
                Process.Start(new ProcessStartInfo
                {
                    FileName = parts[0].Trim(),
                    Arguments = parts[1].Trim(),
                    UseShellExecute = true
                });
                return true;
            }

            // 4. Reveal file in Explorer ("reveal:<path>")
            if (target.StartsWith("reveal:", StringComparison.OrdinalIgnoreCase))
            {
                string path = target["reveal:".Length..].Trim();
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
                return true;
            }

            // 5. Standard executable, .lnk, .url, folder, document, ms-settings:, windowsdefender:, https://
            var psi = new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            };

            if (File.Exists(target))
            {
                string? dir = Path.GetDirectoryName(target);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    psi.WorkingDirectory = dir;
            }

            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static List<InstalledAppEntry> EnumerateShellAppsFolder(bool includeIcons)
    {
        var results = new List<InstalledAppEntry>(256);
        nint folderPtr = 0;
        nint enumPtr = 0;

        try
        {
            int hr = SHGetKnownFolderItem(FolderIdAppsFolder, 0, 0, IidIShellItem, out folderPtr);
            if (hr != 0 || folderPtr == 0)
                return results;

            var folderItem = (IShellItem)Marshal.GetObjectForIUnknown(folderPtr);
            folderItem.BindToHandler(0, BhidEnumItems, IidIEnumShellItems, out enumPtr);
            if (enumPtr == 0)
                return results;

            var enumerator = (IEnumShellItems)Marshal.GetObjectForIUnknown(enumPtr);
            var batch = new IShellItem[1];
            int idx = 0;

            while (enumerator.Next(1, batch, out uint fetched) == 0 && fetched == 1)
            {
                var item = batch[0];
                if (item is null) continue;

                try
                {
                    item.GetDisplayName(SigdnNormalDisplay, out string? displayName);
                    item.GetDisplayName(SigdnParentRelativeParsing, out string? appId);

                    if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(appId))
                        continue;

                    displayName = displayName.Trim();
                    appId = appId.Trim();

                    // Extract crisp 32x32 transparent ARGB PNG icon directly from Windows Shell when requested
                    byte[]? iconPng = includeIcons ? ExtractShellItemIconPng(item, 32) : null;

                    bool isStoreOrUwp = appId.Contains('!');
                    string category = InferAppCategory(displayName, appId, isStoreOrUwp);
                    string keywords = BuildKeywords(displayName, appId);

                    results.Add(new InstalledAppEntry(
                        Id: $"appsfolder-{idx++}",
                        Name: displayName,
                        LaunchPath: $"appsfolder:{appId}",
                        Category: category,
                        IconPngBytes: iconPng,
                        Keywords: keywords,
                        Kind: "app"));
                }
                catch
                {
                    // Skip broken shell item gracefully
                }
                finally
                {
                    Marshal.ReleaseComObject(item);
                }
            }

            Marshal.ReleaseComObject(enumerator);
            Marshal.ReleaseComObject(folderItem);
        }
        catch
        {
            // Fallback gracefully to Start Menu scan if COM fails
        }
        finally
        {
            if (enumPtr != 0) Marshal.Release(enumPtr);
            if (folderPtr != 0) Marshal.Release(folderPtr);
        }

        return results;
    }

    private static void MergeShortcutsFromRoots(Dictionary<string, InstalledAppEntry> byName, bool includeIcons)
    {
        (string Path, string DefaultCat)[] roots =
        [
            (Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Masaüstü"),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "Masaüstü"),
            (Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Uygulama"),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Uygulama")
        ];

        foreach (var (root, defaultCat) in roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                continue;

            var searchOption = defaultCat == "Masaüstü" ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*.*", searchOption))
                {
                    string ext = Path.GetExtension(file);
                    if (!ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) &&
                        !ext.Equals(".url", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (file.Contains("Administrative Tools", StringComparison.OrdinalIgnoreCase) ||
                        file.Contains("Windows Kits", StringComparison.OrdinalIgnoreCase) ||
                        file.Contains("Maintenance", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var name = Path.GetFileNameWithoutExtension(file).Trim();
                    if (string.IsNullOrWhiteSpace(name) || name.Length < 2 || name.StartsWith('$'))
                        continue;

                    if (NoiseKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    if (byName.ContainsKey(name))
                        continue;

                    var parentFolder = Path.GetFileName(Path.GetDirectoryName(file) ?? string.Empty);
                    string category = defaultCat == "Masaüstü"
                        ? "Masaüstü"
                        : (string.IsNullOrWhiteSpace(parentFolder) || parentFolder.Equals("Programs", StringComparison.OrdinalIgnoreCase)
                            ? "Uygulama"
                            : parentFolder);

                    var icon = includeIcons ? Win32IconExtractor.ResolveShortcutIconPng(file) : null;
                    byName[name] = new InstalledAppEntry(
                        Id: $"lnk-{byName.Count}",
                        Name: name,
                        LaunchPath: file,
                        Category: category,
                        IconPngBytes: icon,
                        Keywords: category,
                        Kind: "app");
                }
            }
            catch
            {
                // Skip protected directories
            }
        }
    }

    private static string InferAppCategory(string name, string appId, bool isStoreOrUwp)
    {
        string lower = $"{name} {appId}".ToLowerInvariant();
        if (lower.Contains("chrome") || lower.Contains("brave") || lower.Contains("opera") || lower.Contains("edge") || lower.Contains("firefox"))
            return "Tarayıcı";
        if (lower.Contains("code") || lower.Contains("studio") || lower.Contains("terminal") || lower.Contains("powershell") || lower.Contains("git") || lower.Contains("python") || lower.Contains("antigravity") || lower.Contains("cursor") || lower.Contains("netbeans"))
            return "Geliştirme";
        if (lower.Contains("teams") || lower.Contains("discord") || lower.Contains("whatsapp") || lower.Contains("telegram") || lower.Contains("slack") || lower.Contains("zoom"))
            return "İletişim";
        if (lower.Contains("spotify") || lower.Contains("vlc") || lower.Contains("music") || lower.Contains("photo") || lower.Contains("fotoğraf") || lower.Contains("kamera") || lower.Contains("paint"))
            return "Medya";
        if (lower.Contains("steam") || lower.Contains("xbox") || lower.Contains("riot") || lower.Contains("epic") || lower.Contains("blitz") || lower.Contains("game"))
            return "Oyun";
        if (lower.Contains("word") || lower.Contains("excel") || lower.Contains("powerpoint") || lower.Contains("outlook") || lower.Contains("onenote") || lower.Contains("office") || lower.Contains("pdf"))
            return "Ofis";
        return isStoreOrUwp ? "Microsoft Store" : "Uygulama";
    }

    private static string BuildKeywords(string name, string appId)
    {
        var tokens = new List<string>();
        string cleanId = appId.Replace('{', ' ').Replace('}', ' ');
        int slash = cleanId.LastIndexOf('\\');
        if (slash >= 0 && slash + 1 < cleanId.Length)
        {
            string exeName = Path.GetFileNameWithoutExtension(cleanId[(slash + 1)..]);
            if (!string.IsNullOrWhiteSpace(exeName) && !exeName.Equals(name, StringComparison.OrdinalIgnoreCase))
                tokens.Add(exeName);
        }
        else if (appId.Contains('!'))
        {
            string pkg = appId.Split('_')[0];
            foreach (var seg in pkg.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (seg.Length >= 3 && !seg.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) && !seg.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                    tokens.Add(seg);
            }
        }
        return string.Join(' ', tokens.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    public static byte[]? ExtractIconForAppsFolderId(string appId, int sizePx = 32)
    {
        if (string.IsNullOrWhiteSpace(appId))
            return null;

        nint itemPtr = 0;
        try
        {
            int hr = SHCreateItemFromParsingName($"shell:AppsFolder\\{appId.Trim()}", 0, IidIShellItem, out itemPtr);
            if (hr != 0 || itemPtr == 0)
                return null;

            var item = (IShellItem)Marshal.GetObjectForIUnknown(itemPtr);
            try
            {
                return ExtractShellItemIconPng(item, sizePx);
            }
            finally
            {
                Marshal.ReleaseComObject(item);
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            if (itemPtr != 0)
                Marshal.Release(itemPtr);
        }
    }

    private static byte[]? ExtractShellItemIconPng(IShellItem item, int sizePx)
    {
        if (item is not IShellItemImageFactory factory)
            return null;

        nint hBitmap = 0;
        try
        {
            int hr = factory.GetImage(new SIZE(sizePx, sizePx), SiigbfIconOnly | SiigbfBiggerSizeOk | SiigbfResizeToFit, out hBitmap);
            if (hr != 0 || hBitmap == 0)
            {
                hr = factory.GetImage(new SIZE(sizePx, sizePx), SiigbfResizeToFit, out hBitmap);
                if (hr != 0 || hBitmap == 0)
                    return null;
            }

            return ConvertHBitmapToTransparentPng(hBitmap);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hBitmap != 0)
                DeleteObject(hBitmap);
        }
    }

    private static byte[]? ConvertHBitmapToTransparentPng(nint hBitmap)
    {
        var bm = new BITMAP();
        if (GetObjectW(hBitmap, Marshal.SizeOf<BITMAP>(), ref bm) == 0 || bm.bmWidth <= 0 || bm.bmHeight <= 0)
            return null;

        int width = bm.bmWidth;
        int height = bm.bmHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];

        var bmi = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height, // top-down DIB
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0 // BI_RGB
            }
        };

        nint screenDc = GetDC(0);
        try
        {
            int scanLines = GetDIBits(screenDc, hBitmap, 0, (uint)height, pixels, ref bmi, 0);
            if (scanLines == 0)
                return null;
        }
        finally
        {
            ReleaseDC(0, screenDc);
        }

        // If bitmap had 0 alpha across all non-black pixels (legacy 24-bit icon), make non-empty opaque
        bool hasAlpha = false;
        for (int i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 0)
            {
                hasAlpha = true;
                break;
            }
        }
        if (!hasAlpha)
        {
            for (int i = 3; i < pixels.Length; i += 4)
                pixels[i] = 255;
        }

        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
        }
        finally
        {
            bmp.UnlockBits(bmpData);
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct SIZE(int cx, int cy)
    {
        public readonly int cx = cx;
        public readonly int cy = cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public nint bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(nint pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out nint ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string? ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport]
    [Guid("70629033-E363-4A28-A567-0DB78006E6D7")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumShellItems
    {
        [PreserveSig]
        int Next(uint celt, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IShellItem[] rgelt, out uint pceltFetched);
        [PreserveSig]
        int Skip(uint celt);
        [PreserveSig]
        int Reset();
        [PreserveSig]
        int Clone(out IEnumShellItems ppenum);
    }

    [ComImport]
    [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, uint flags, out nint phbm);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderItem(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
        uint dwFlags,
        nint hToken,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        out nint ppv);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        string pszPath,
        nint pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        out nint ppv);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint hObject);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetObjectW(nint hObject, int cbBuffer, ref BITMAP lpvObject);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(nint hdc, nint hbm, uint start, uint cLines, [Out] byte[] lpvBits, ref BITMAPINFO lpbmi, uint usage);

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hWnd, nint hDc);
}
