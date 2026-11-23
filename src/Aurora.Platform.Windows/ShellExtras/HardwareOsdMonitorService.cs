using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace Aurora.Platform.Windows.ShellExtras;

public sealed record HardwareOsdEvent(
    string Kind,        // "volume" | "mute" | "brightness" | "mic" | "capslock" | "numlock"
    string Label,
    string ValueText,
    int? Percent,
    bool Active,
    int DurationMs = 1450);

/// <summary>
/// Tracks real-time hardware OSD triggers (Volume, Mute, Brightness, Microphone Mute,
/// Caps Lock, and Num Lock) using WH_KEYBOARD_LL + integer delta tracking.
/// When Live OSD is enabled, hardware volume keys (VK_VOLUME_UP/DOWN/MUTE) are handled
/// directly via CoreAudio and consumed so Windows 11's native bottom volume flyout
/// never appears over Aurora; legacy/XAML hardware OSD windows are also safely suppressed
/// on brightness/HID changes and restored cleanly when disabled or on exit.
/// </summary>
public sealed class HardwareOsdMonitorService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const uint LlkhfInjected = 0x00000010;

    public const uint VkCapital = 0x14;
    public const uint VkNumLock = 0x90;
    public const uint VkVolumeMute = 0xAD;
    public const uint VkVolumeDown = 0xAE;
    public const uint VkVolumeUp = 0xAF;

    private const int SwHide = 0;

    private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);
    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    private readonly LowLevelKeyboardProc _proc;
    private readonly HashSet<nint> _suppressedNativeOsdHwnds = [];
    private nint _hookHandle;
    private bool _disposed;

    private bool _initialized;
    private int _lastVolume;
    private bool _lastMuted;
    private bool _lastMicSupported;
    private bool _lastMicMuted;
    private bool _lastBrightSupported;
    private int _lastBrightness;
    private bool _lastCapsOn;
    private bool _lastNumOn;

    /// <summary>
    /// Fired passively for CapsLock / NumLock (and non-intercepted volume keys).
    /// </summary>
    public event Action<uint>? HardwareKeyObserved;

    /// <summary>
    /// Fired when SuppressNativeVolumeKeys is true and a hardware VK_VOLUME_UP / DOWN / MUTE
    /// keydown is intercepted & swallowed so Aurora drives CoreAudio directly with zero Windows flyout.
    /// </summary>
    public event Action<uint>? VolumeKeyIntercepted;

    public bool SuppressNativeVolumeKeys { get; set; } = true;

    public bool IsHookActive => _hookHandle != 0;

    public HardwareOsdMonitorService()
    {
        _proc = HookCallback;
    }

    public void SetHookEnabled(bool enabled)
    {
        if (_disposed) return;
        if (enabled && _hookHandle == 0)
        {
            using var curProc = Process.GetCurrentProcess();
            using var curMod = curProc.MainModule;
            nint hMod = curMod is not null ? GetModuleHandleW(curMod.ModuleName) : 0;
            _hookHandle = SetWindowsHookExW(WhKeyboardLl, _proc, hMod, 0);
        }
        else if (!enabled)
        {
            if (_hookHandle != 0)
            {
                UnhookWindowsHookEx(_hookHandle);
                _hookHandle = 0;
            }
            RestoreSuppressedNativeOsdWindows();
        }
    }

    public static bool IsCapsLockOn() => (GetKeyState((int)VkCapital) & 0x0001) != 0;
    public static bool IsNumLockOn() => (GetKeyState((int)VkNumLock) & 0x0001) != 0;

    /// <summary>
    /// Computes Windows 11's exact 2%-aligned volume step up/down (e.g., 89 -> 90 -> 92, 91 -> 90 -> 88).
    /// </summary>
    public static int ComputeNextVolumeStep(int currentPercent, bool stepUp, int step = 2)
    {
        currentPercent = Math.Clamp(currentPercent, 0, 100);
        step = Math.Max(1, step);
        if (stepUp)
        {
            int aligned = ((currentPercent / step) + 1) * step;
            return Math.Clamp(aligned, 0, 100);
        }
        else
        {
            int aligned = ((currentPercent + step - 1) / step - 1) * step;
            return Math.Clamp(aligned, 0, 100);
        }
    }

    /// <summary>
    /// Synchronizes the internal baseline immediately after Aurora directly changes CoreAudio
    /// volume/mute so the periodic delta poller does not double-emit the same OSD event.
    /// </summary>
    public void SyncVolumeBaseline(int volumePercent, bool masterMuted)
    {
        _initialized = true;
        _lastVolume = Math.Clamp(volumePercent, 0, 100);
        _lastMuted = masterMuted;
    }

    /// <summary>
    /// Compares current hardware states against the last observed snapshot and returns
    /// any newly triggered OSD HUD event (or null if unchanged). The very first call seeds
    /// baseline values without emitting spurious startup notifications.
    /// </summary>
    public HardwareOsdEvent? ObserveState(
        int volumePercent,
        bool masterMuted,
        bool micSupported,
        bool micMuted,
        bool brightnessSupported,
        int brightnessPercent,
        bool capsOn,
        bool numOn)
    {
        volumePercent = Math.Clamp(volumePercent, 0, 100);
        brightnessPercent = Math.Clamp(brightnessPercent, 0, 100);

        if (!_initialized)
        {
            _initialized = true;
            _lastVolume = volumePercent;
            _lastMuted = masterMuted;
            _lastMicSupported = micSupported;
            _lastMicMuted = micMuted;
            _lastBrightSupported = brightnessSupported;
            _lastBrightness = brightnessPercent;
            _lastCapsOn = capsOn;
            _lastNumOn = numOn;
            return null;
        }

        HardwareOsdEvent? evt = null;

        if (masterMuted != _lastMuted || volumePercent != _lastVolume)
        {
            _lastMuted = masterMuted;
            _lastVolume = volumePercent;
            evt = masterMuted || volumePercent == 0
                ? new HardwareOsdEvent("mute", "Ses", "Sessiz", 0, Active: false)
                : new HardwareOsdEvent("volume", "Ses", $"%{volumePercent}", volumePercent, Active: true);
        }
        else if (brightnessSupported && _lastBrightSupported && brightnessPercent != _lastBrightness)
        {
            _lastBrightness = brightnessPercent;
            evt = new HardwareOsdEvent("brightness", "Parlaklık", $"%{brightnessPercent}", brightnessPercent, Active: true);
        }
        else if (micSupported && _lastMicSupported && micMuted != _lastMicMuted)
        {
            _lastMicMuted = micMuted;
            evt = new HardwareOsdEvent(
                "mic",
                "Mikrofon",
                micMuted ? "KAPALI" : "AÇIK",
                Percent: null,
                Active: !micMuted);
        }
        else if (capsOn != _lastCapsOn)
        {
            _lastCapsOn = capsOn;
            evt = new HardwareOsdEvent(
                "capslock",
                "Caps Lock",
                capsOn ? "AÇIK" : "KAPALI",
                Percent: null,
                Active: capsOn,
                DurationMs: 1200);
        }
        else if (numOn != _lastNumOn)
        {
            _lastNumOn = numOn;
            evt = new HardwareOsdEvent(
                "numlock",
                "Num Lock",
                numOn ? "AÇIK" : "KAPALI",
                Percent: null,
                Active: numOn,
                DurationMs: 1200);
        }

        _lastMicSupported = micSupported;
        _lastBrightSupported = brightnessSupported;
        if (brightnessSupported) _lastBrightness = brightnessPercent;
        if (micSupported) _lastMicMuted = micMuted;

        return evt;
    }

    /// <summary>
    /// Precision check for Windows 11's compact bottom-center hardware slider pill
    /// (volume/brightness flyout), avoiding Start Menu, Alt+Tab, Snap Layouts, and Quick Settings.
    /// </summary>
    public static bool IsCompactBottomCenterOsdGeometry(Rectangle winRect, IReadOnlyList<Rectangle> monitorBounds)
    {
        if (winRect.Width < 120 || winRect.Width > 540 || winRect.Height < 28 || winRect.Height > 140)
            return false;

        int winCenterX = winRect.Left + (winRect.Width / 2);
        foreach (var mon in monitorBounds)
        {
            if (winCenterX < mon.Left || winCenterX > mon.Right)
                continue;
            if (winRect.Top < mon.Top || winRect.Bottom > mon.Bottom + 16)
                continue;

            int monCenterX = mon.Left + (mon.Width / 2);
            bool horizontallyCentered = Math.Abs(winCenterX - monCenterX) <= Math.Max(140, mon.Width / 10);
            bool nearBottomEdge = winRect.Bottom >= mon.Bottom - Math.Max(220, mon.Height / 4);
            if (horizontallyCentered && nearBottomEdge)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Safely hides Windows' native hardware volume/brightness flyout windows when triggered
    /// by non-keyboard paths (laptop Fn brightness keys, USB HID wheels). Never kills or injects explorer.
    /// </summary>
    public int TrySuppressNativeHardwareOsdWindows(IReadOnlyList<Rectangle> monitorBounds)
    {
        int hiddenCount = 0;
        try
        {
            var clsBuf = new StringBuilder(128);
            var titleBuf = new StringBuilder(64);

            // 1. Legacy Windows 10/11 NativeHWNDHost -> DirectUIHWND (classic volume/brightness OSD host)
            nint cur = 0;
            while ((cur = FindWindowExW(0, cur, "NativeHWNDHost", null)) != 0)
            {
                if (FindWindowExW(cur, 0, "DirectUIHWND", null) != 0)
                {
                    titleBuf.Clear();
                    GetWindowTextW(cur, titleBuf, titleBuf.Capacity);
                    if (titleBuf.Length == 0 && IsWindowVisible(cur))
                    {
                        ShowWindow(cur, SwHide);
                        _suppressedNativeOsdHwnds.Add(cur);
                        hiddenCount++;
                    }
                }
            }

            // 2. Modern Windows 11 XamlExplorerHostIslandWindow hardware confirmation slider pill
            if (monitorBounds.Count > 0)
            {
                EnumWindows((hwnd, _) =>
                {
                    if (!IsWindowVisible(hwnd))
                        return true;

                    clsBuf.Clear();
                    if (GetClassNameW(hwnd, clsBuf, clsBuf.Capacity) <= 0)
                        return true;

                    string cls = clsBuf.ToString();
                    if (!cls.Equals("XamlExplorerHostIslandWindow", StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (!GetWindowRect(hwnd, out var rc))
                        return true;

                    var rect = Rectangle.FromLTRB(rc.Left, rc.Top, rc.Right, rc.Bottom);
                    if (!IsCompactBottomCenterOsdGeometry(rect, monitorBounds))
                        return true;

                    GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid == 0 || pid == (uint)Environment.ProcessId)
                        return true;

                    string? procName = null;
                    try
                    {
                        using var p = Process.GetProcessById((int)pid);
                        procName = p.ProcessName;
                    }
                    catch { }

                    if (procName is not null &&
                        (procName.Equals("explorer", StringComparison.OrdinalIgnoreCase) ||
                         procName.Equals("ShellHost", StringComparison.OrdinalIgnoreCase) ||
                         procName.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase)))
                    {
                        ShowWindow(hwnd, SwHide);
                        _suppressedNativeOsdHwnds.Add(hwnd);
                        hiddenCount++;
                    }

                    return true;
                }, 0);
            }
        }
        catch { }

        return hiddenCount;
    }

    public void RestoreSuppressedNativeOsdWindows()
    {
        if (_suppressedNativeOsdHwnds.Count == 0)
            return;

        foreach (var hwnd in _suppressedNativeOsdHwnds)
        {
            try
            {
                if (IsWindow(hwnd))
                {
                    // Legacy NativeHWNDHost stays hidden until next volume key; only ensure we don't leave it disabled
                    EnableWindow(hwnd, true);
                }
            }
            catch { }
        }
        _suppressedNativeOsdHwnds.Clear();
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            // Intercept hardware volume keys directly so Windows 11's native volume flyout NEVER flashes
            if (SuppressNativeVolumeKeys &&
                (kb.flags & LlkhfInjected) == 0 &&
                kb.vkCode is VkVolumeMute or VkVolumeDown or VkVolumeUp)
            {
                if (msg is WmKeyDown or WmSysKeyDown)
                {
                    try { VolumeKeyIntercepted?.Invoke(kb.vkCode); } catch { }
                    return new nint(1);
                }
                if (msg is WmKeyUp or WmSysKeyUp)
                {
                    return new nint(1);
                }
            }

            if (msg is WmKeyDown or WmSysKeyDown)
            {
                if (kb.vkCode is VkCapital or VkNumLock or VkVolumeMute or VkVolumeDown or VkVolumeUp)
                {
                    try { HardwareKeyObserved?.Invoke(kb.vkCode); } catch { }
                }
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hookHandle != 0)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = 0;
        }
        RestoreSuppressedNativeOsdWindows();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookExW(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindowExW(nint hwndParent, nint hwndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(nint hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(nint hWnd, [MarshalAs(UnmanagedType.Bool)] bool bEnable);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? lpModuleName);
}
