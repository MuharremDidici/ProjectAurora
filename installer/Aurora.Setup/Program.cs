using System.Diagnostics;
using System.Drawing;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Win32;

namespace Aurora.Setup;

internal static class Program
{
    public static string DefaultInstallDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ProjectAurora");

    public static string AppVersion { get; } = ResolveVersion();

    private static string ResolveVersion()
    {
        var raw = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(raw))
            return raw.Split('+')[0].Trim();
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v is not null ? $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}" : "2.0.0";
    }

    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (args.Any(a => string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            SetupEngine.UninstallCleanly(DefaultInstallDir);
            if (!args.Any(a => string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(
                    "Project Aurora bilgisayarınızdan kaldırıldı ve Windows görev çubuğu varsayılan haline döndürüldü.",
                    "Project Aurora Kaldırıldı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            return 0;
        }

        int? waitPid = ParseWaitPid(args);

        if (args.Any(a => string.Equals(a, "--update", StringComparison.OrdinalIgnoreCase)))
        {
            if (args.Any(a => string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase)))
            {
                SetupEngine.InstallAsync(
                    installDir: DefaultInstallDir,
                    createDesktopShortcut: false,
                    createStartMenuShortcuts: true,
                    enableStartup: false,
                    preserveExistingStartup: true,
                    waitForPid: waitPid,
                    launchAfterInstall: true,
                    progress: null).GetAwaiter().GetResult();
                return 0;
            }

            using var updateForm = new AuroraAutoUpdateForm(waitPid);
            Application.Run(updateForm);
            return 0;
        }

        if (args.Any(a => string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase)))
        {
            bool launch = !args.Any(a => string.Equals(a, "--no-launch", StringComparison.OrdinalIgnoreCase));
            SetupEngine.InstallAsync(
                installDir: DefaultInstallDir,
                createDesktopShortcut: true,
                createStartMenuShortcuts: true,
                enableStartup: false,
                preserveExistingStartup: true,
                waitForPid: waitPid,
                launchAfterInstall: launch,
                progress: null).GetAwaiter().GetResult();
            return 0;
        }

        using var form = new AuroraSetupForm();
        Application.Run(form);
        return 0;
    }

    private static int? ParseWaitPid(string[] args)
    {
        var arg = args.FirstOrDefault(a => a.StartsWith("--wait-pid=", StringComparison.OrdinalIgnoreCase));
        if (arg is not null && int.TryParse(arg["--wait-pid=".Length..], out int pid) && pid > 0)
            return pid;
        return null;
    }
}

internal sealed class AuroraAutoUpdateForm : Form
{
    private readonly int? _waitPid;
    private readonly ProgressBar _bar;
    private readonly Label _lblStatus;

    public AuroraAutoUpdateForm(int? waitPid)
    {
        _waitPid = waitPid;
        Text = $"Project Aurora v{Program.AppVersion} Güncelleniyor";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ShowInTaskbar = true;
        ClientSize = new Size(410, 118);
        BackColor = Color.FromArgb(11, 16, 32);
        ForeColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9.5f);

        var border = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 18, 22, 18),
            BackColor = Color.FromArgb(13, 20, 38)
        };
        border.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(52, 211, 153), 1.5f);
            e.Graphics.DrawRectangle(pen, 1, 1, border.Width - 3, border.Height - 3);
        };

        var lblTitle = new Label
        {
            Text = $"✦ Project Aurora v{Program.AppVersion} Güncelleniyor",
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 11.5f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(22, 18)
        };

        _lblStatus = new Label
        {
            Text = "Mevcut ayarlarınız korunarak yeni sürüm kuruluyor...",
            ForeColor = Color.FromArgb(186, 230, 253),
            Font = new Font("Segoe UI", 9f),
            Size = new Size(366, 20),
            Location = new Point(22, 46)
        };

        _bar = new ProgressBar
        {
            Location = new Point(22, 76),
            Size = new Size(366, 14),
            Style = ProgressBarStyle.Continuous,
            Minimum = 0,
            Maximum = 100,
            Value = 8
        };

        border.Controls.AddRange([lblTitle, _lblStatus, _bar]);
        Controls.Add(border);

        Shown += async (_, _) => await RunUpdateAsync();
    }

    private async Task RunUpdateAsync()
    {
        try
        {
            var prog = new Progress<(int Percent, string Message)>(step =>
            {
                if (IsDisposed) return;
                _bar.Value = Math.Clamp(step.Percent, 0, 100);
                _lblStatus.Text = step.Message;
            });

            await SetupEngine.InstallAsync(
                installDir: Program.DefaultInstallDir,
                createDesktopShortcut: File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Project Aurora.lnk")),
                createStartMenuShortcuts: true,
                enableStartup: false,
                preserveExistingStartup: true,
                waitForPid: _waitPid,
                launchAfterInstall: true,
                progress: prog);

            await Task.Delay(350);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Otomatik güncelleme sırasında hata oluştu:\n{ex.Message}",
                "Güncelleme Hatası",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Close();
        }
    }
}

internal sealed class AuroraSetupForm : Form
{
    private readonly CheckBox _chkDesktop;
    private readonly CheckBox _chkStartMenu;
    private readonly CheckBox _chkStartup;
    private readonly CheckBox _chkLaunchNow;
    private readonly ProgressBar _progressBar;
    private readonly Label _lblStatus;
    private readonly Button _btnInstall;
    private readonly Button _btnUninstall;

    public AuroraSetupForm()
    {
        Text = $"Project Aurora v{Program.AppVersion} — Tek Tık Kurulum";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(540, 405);
        BackColor = Color.FromArgb(11, 16, 32);
        ForeColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9.75f, FontStyle.Regular);

        var lblBadge = new Label
        {
            Text = $"✦ PROJECT AURORA v{Program.AppVersion} • ÇOKLU EKRAN • WIN11 X64",
            ForeColor = Color.FromArgb(56, 189, 248),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(28, 24)
        };

        var lblTitle = new Label
        {
            Text = "Yeni Nesil Dinamik Ada & Kristal Dock",
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 15.5f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(26, 46)
        };

        var lblSub = new Label
        {
            Text = "Ekstra .NET kurulumu veya Yönetici (UAC) izni gerektirmez.\nKurulum klasörü: " + Program.DefaultInstallDir,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 9f),
            Size = new Size(485, 40),
            Location = new Point(28, 82)
        };

        var cardPanel = new Panel
        {
            Location = new Point(28, 130),
            Size = new Size(484, 142),
            BackColor = Color.FromArgb(18, 26, 48)
        };

        _chkDesktop = CreateOptionCheck("Masaüstüne \"Project Aurora\" kısayolu oluştur", true, 14);
        _chkStartMenu = CreateOptionCheck("Başlat Menüsü kısayolu ve Görev Çubuğu Kurtarma aracını ekle", true, 44);
        _chkStartup = CreateOptionCheck("Windows açıldığında Project Aurora otomatik başlasın", false, 74);
        _chkLaunchNow = CreateOptionCheck("Kurulum tamamlanınca uygulamayı hemen çalıştır", true, 104);

        cardPanel.Controls.AddRange([_chkDesktop, _chkStartMenu, _chkStartup, _chkLaunchNow]);

        _lblStatus = new Label
        {
            Text = "Kuruluma hazır.",
            ForeColor = Color.FromArgb(186, 230, 253),
            Font = new Font("Segoe UI", 9f),
            Size = new Size(484, 20),
            Location = new Point(28, 284)
        };

        _progressBar = new ProgressBar
        {
            Location = new Point(28, 308),
            Size = new Size(484, 12),
            Style = ProgressBarStyle.Continuous,
            Minimum = 0,
            Maximum = 100,
            Value = 0
        };

        _btnInstall = new Button
        {
            Text = "🚀 Tek Tıkla Kur ve Başlat",
            Location = new Point(272, 340),
            Size = new Size(240, 44),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(56, 189, 248),
            ForeColor = Color.FromArgb(6, 16, 31),
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnInstall.FlatAppearance.BorderSize = 0;
        _btnInstall.Click += async (_, _) => await RunInstallFlowAsync();

        _btnUninstall = new Button
        {
            Text = "Mevcut Kurulumu Kaldır",
            Location = new Point(28, 344),
            Size = new Size(185, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(24, 34, 58),
            ForeColor = Color.FromArgb(251, 113, 133),
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            Cursor = Cursors.Hand,
            Visible = Directory.Exists(Program.DefaultInstallDir)
        };
        _btnUninstall.FlatAppearance.BorderColor = Color.FromArgb(80, 251, 113, 133);
        _btnUninstall.Click += (_, _) =>
        {
            SetupEngine.UninstallCleanly(Program.DefaultInstallDir);
            _btnUninstall.Visible = false;
            _lblStatus.Text = "Mevcut kurulum temizlendi ve Windows görev çubuğu geri yüklendi.";
        };

        Controls.AddRange([lblBadge, lblTitle, lblSub, cardPanel, _lblStatus, _progressBar, _btnInstall, _btnUninstall]);
    }

    private static CheckBox CreateOptionCheck(string text, bool isChecked, int y) => new()
    {
        Text = text,
        Checked = isChecked,
        ForeColor = Color.FromArgb(241, 245, 249),
        AutoSize = true,
        Location = new Point(16, y),
        Cursor = Cursors.Hand
    };

    private async Task RunInstallFlowAsync()
    {
        _btnInstall.Enabled = false;
        _btnUninstall.Enabled = false;

        try
        {
            var prog = new Progress<(int Percent, string Message)>( step =>
            {
                _progressBar.Value = Math.Clamp(step.Percent, 0, 100);
                _lblStatus.Text = step.Message;
            });

            await SetupEngine.InstallAsync(
                installDir: Program.DefaultInstallDir,
                createDesktopShortcut: _chkDesktop.Checked,
                createStartMenuShortcuts: _chkStartMenu.Checked,
                enableStartup: _chkStartup.Checked,
                preserveExistingStartup: false,
                waitForPid: null,
                launchAfterInstall: _chkLaunchNow.Checked,
                progress: prog);

            await Task.Delay(550);
            Close();
        }
        catch (Exception ex)
        {
            _btnInstall.Enabled = true;
            _btnUninstall.Enabled = true;
            MessageBox.Show(
                $"Kurulum sırasında bir hata oluştu:\n{ex.Message}",
                "Kurulum Hatası",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}

internal static class SetupEngine
{
    private const string UninstallRegPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ProjectAurora";
    private const string RunRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "ProjectAurora";

    public static async Task InstallAsync(
        string installDir,
        bool createDesktopShortcut,
        bool createStartMenuShortcuts,
        bool enableStartup,
        bool preserveExistingStartup,
        int? waitForPid,
        bool launchAfterInstall,
        IProgress<(int Percent, string Message)>? progress)
    {
        progress?.Report((8, "Çalışan eski Aurora süreçleri kapatılıyor..."));
        await Task.Run(() => StopRunningAuroraProcesses(waitForPid));

        progress?.Report((20, "Microsoft WebView2 çalışma zamanı doğrulanıyor..."));
        await EnsureWebView2RuntimeAsync(progress);

        progress?.Report((35, "Uygulama paketleri çıkartılıyor..."));
        await Task.Run(() =>
        {
            Directory.CreateDirectory(installDir);
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AuroraPayload.zip")
                ?? throw new InvalidOperationException("Gömülü kurulum paketi (AuroraPayload.zip) bulunamadı.");

            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                var fullDest = Path.GetFullPath(Path.Combine(installDir, entry.FullName));
                if (!fullDest.StartsWith(Path.GetFullPath(installDir), StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(fullDest);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(fullDest)!);
                ExtractEntryWithRetry(entry, fullDest);
            }
        });

        string mainExe = Path.Combine(installDir, "Aurora.App.exe");
        string uninstallCmd = Path.Combine(installDir, "Uninstall-Aurora.cmd");

        progress?.Report((75, "Kaldırma betiği ve kısayollar hazırlanıyor..."));
        WriteUninstallScript(installDir, mainExe, uninstallCmd);
        RegisterWindowsUninstaller(installDir, mainExe, uninstallCmd);

        if (createDesktopShortcut)
        {
            string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Project Aurora.lnk");
            ShortcutCreator.CreateShortcut(desktopLnk, mainExe, "", installDir, "Project Aurora — Akıllı Dinamik Ada ve Kristal Dock", mainExe);
        }

        if (createStartMenuShortcuts)
        {
            string startDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Project Aurora");
            Directory.CreateDirectory(startDir);
            ShortcutCreator.CreateShortcut(
                Path.Combine(startDir, "Project Aurora.lnk"),
                mainExe,
                "",
                installDir,
                "Project Aurora — Akıllı Dinamik Ada ve Kristal Dock",
                mainExe);
            ShortcutCreator.CreateShortcut(
                Path.Combine(startDir, "Windows Görev Çubuğunu Geri Getir.lnk"),
                mainExe,
                "--restore-taskbar",
                installDir,
                "Windows yerel görev çubuğunu anında görünür yapar",
                mainExe);
        }

        if (!preserveExistingStartup)
        {
            using var runKey = Registry.CurrentUser.CreateSubKey(RunRegPath, writable: true);
            if (enableStartup)
                runKey.SetValue(RunValueName, $"\"{mainExe}\"");
            else
                runKey.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
        else
        {
            // Keep existing HKCU Run startup preference intact while ensuring path stays updated if enabled
            using var runKey = Registry.CurrentUser.OpenSubKey(RunRegPath, writable: true);
            if (runKey?.GetValue(RunValueName) is not null)
                runKey.SetValue(RunValueName, $"\"{mainExe}\"");
        }

        progress?.Report((100, "Kurulum başarıyla tamamlandı!"));

        if (launchAfterInstall && File.Exists(mainExe))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = mainExe,
                WorkingDirectory = installDir,
                UseShellExecute = true
            });
        }
    }

    public static void UninstallCleanly(string installDir)
    {
        string mainExe = Path.Combine(installDir, "Aurora.App.exe");
        if (File.Exists(mainExe))
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = mainExe,
                    Arguments = "--restore-taskbar",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                p?.WaitForExit(2000);
            }
            catch { }
        }

        StopRunningAuroraProcesses();

        try
        {
            if (File.Exists(mainExe))
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = mainExe,
                    Arguments = "--restore-taskbar",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                p?.WaitForExit(2000);
            }
        }
        catch { }

        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunRegPath, writable: true);
            runKey?.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
        catch { }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(UninstallRegPath, throwOnMissingSubKey: false);
        }
        catch { }

        try
        {
            string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Project Aurora.lnk");
            if (File.Exists(desktopLnk)) File.Delete(desktopLnk);

            string startDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Project Aurora");
            if (Directory.Exists(startDir)) Directory.Delete(startDir, recursive: true);
        }
        catch { }

        try
        {
            if (Directory.Exists(installDir))
                Directory.Delete(installDir, recursive: true);
        }
        catch { }
    }

    private static void ExtractEntryWithRetry(ZipArchiveEntry entry, string destinationPath)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                entry.ExtractToFile(destinationPath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 7)
            {
                Thread.Sleep(250);
            }
            catch (UnauthorizedAccessException) when (attempt < 7)
            {
                Thread.Sleep(250);
            }
        }
    }

    private static void StopRunningAuroraProcesses(int? waitForPid = null)
    {
        if (waitForPid is { } pid && pid > 0)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                p.WaitForExit(5000);
            }
            catch { }
        }

        foreach (var proc in Process.GetProcessesByName("Aurora.App"))
        {
            using (proc)
            {
                try { proc.Kill(); proc.WaitForExit(2500); } catch { }
            }
        }
        Thread.Sleep(350);
    }

    private static bool IsWebView2Installed()
    {
        const string clientGuid = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
        string[] paths =
        [
            $@"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{clientGuid}",
            $@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{clientGuid}"
        ];

        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var sub in paths)
            {
                try
                {
                    using var k = root.OpenSubKey(sub);
                    var pv = k?.GetValue("pv") as string;
                    if (!string.IsNullOrWhiteSpace(pv) && pv != "0.0.0.0")
                        return true;
                }
                catch { }
            }
        }
        return false;
    }

    private static async Task EnsureWebView2RuntimeAsync(IProgress<(int Percent, string Message)>? progress)
    {
        if (IsWebView2Installed())
            return;

        progress?.Report((25, "Microsoft Edge WebView2 indiriliyor ve kuruluyor..."));
        string tempExe = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");
        using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) })
        {
            var bytes = await http.GetByteArrayAsync("https://go.microsoft.com/fwlink/p/?LinkId=2124703");
            await File.WriteAllBytesAsync(tempExe, bytes);
        }

        using var proc = Process.Start(new ProcessStartInfo
        {
            FileName = tempExe,
            Arguments = "/silent /install",
            UseShellExecute = true
        });
        if (proc is not null)
            await proc.WaitForExitAsync();
    }

    private static void WriteUninstallScript(string installDir, string mainExe, string uninstallCmdPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("chcp 65001 >nul");
        sb.AppendLine($"if exist \"{mainExe}\" \"{mainExe}\" --restore-taskbar");
        sb.AppendLine("taskkill /F /IM Aurora.App.exe >nul 2>&1");
        sb.AppendLine("timeout /t 1 /nobreak >nul");
        sb.AppendLine($"if exist \"{mainExe}\" \"{mainExe}\" --restore-taskbar");
        sb.AppendLine("reg delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /v ProjectAurora /f >nul 2>&1");
        sb.AppendLine("reg delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\ProjectAurora\" /f >nul 2>&1");
        sb.AppendLine("del /f /q \"%USERPROFILE%\\Desktop\\Project Aurora.lnk\" >nul 2>&1");
        sb.AppendLine("rmdir /s /q \"%APPDATA%\\Microsoft\\Windows\\Start Menu\\Programs\\Project Aurora\" >nul 2>&1");
        sb.AppendLine($"start \"\" /b cmd /c \"timeout /t 1 /nobreak >nul & rmdir /s /q \"{installDir}\"\"");
        File.WriteAllText(uninstallCmdPath, sb.ToString(), new UTF8Encoding(false));
    }

    private static void RegisterWindowsUninstaller(string installDir, string mainExe, string uninstallCmdPath)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(UninstallRegPath, writable: true);
            key.SetValue("DisplayName", "Project Aurora");
            key.SetValue("DisplayVersion", Program.AppVersion);
            key.SetValue("Publisher", "Muharrem Didici • Project Aurora");
            key.SetValue("DisplayIcon", mainExe);
            key.SetValue("InstallLocation", installDir);
            key.SetValue("UninstallString", $"cmd.exe /c \"\"{uninstallCmdPath}\"\"");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
        catch { }
    }
}

internal static class ShortcutCreator
{
    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, nint pfd, int fFlags);
        void GetIDList(out nint ppidl);
        void SetIDList(nint pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(nint hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    public static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string workingDir, string description, string iconPath)
    {
        var link = (IShellLinkW)new ShellLink();
        link.SetPath(targetPath);
        link.SetArguments(arguments);
        link.SetWorkingDirectory(workingDir);
        link.SetDescription(description);
        link.SetIconLocation(iconPath, 0);
        ((IPersistFile)link).Save(shortcutPath, false);
    }
}
