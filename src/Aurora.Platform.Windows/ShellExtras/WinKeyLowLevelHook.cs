using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Aurora.Platform.Windows.ShellExtras;

/// <summary>
/// Optional, non-blocking low-level keyboard hook that opens Aurora OmniSearch
/// on a solitary Left-Win tap while preserving 100% of Win+Key system shortcuts
/// (Win+E, Win+D, Win+L, Win+V, Win+Tab, Win+Shift+S, Win+Arrows, etc.).
/// </summary>
public sealed class WinKeyLowLevelHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const uint VkLWin = 0x5B;
    private const uint VkNoName = 0xFC;
    private const uint LlkhfInjected = 0x00000010;

    private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    private readonly LowLevelKeyboardProc _proc;
    private nint _hookHandle;
    private bool _lwinDown;
    private bool _otherKeyPressed;
    private bool _disposed;

    public event EventHandler? SingleWinKeyTapped;

    public bool IsActive => _hookHandle != 0;

    public WinKeyLowLevelHook()
    {
        _proc = HookCallback;
    }

    public void SetEnabled(bool enabled)
    {
        if (_disposed) return;
        if (enabled && _hookHandle == 0)
        {
            using var curProc = Process.GetCurrentProcess();
            using var curMod = curProc.MainModule;
            nint hMod = curMod is not null ? GetModuleHandleW(curMod.ModuleName) : 0;
            _hookHandle = SetWindowsHookExW(WhKeyboardLl, _proc, hMod, 0);
        }
        else if (!enabled && _hookHandle != 0)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = 0;
            _lwinDown = false;
            _otherKeyPressed = false;
        }
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            bool isInjected = (kb.flags & LlkhfInjected) != 0;
            if (!isInjected)
            {
                int msg = (int)wParam;
                bool isDown = msg == WmKeyDown || msg == WmSysKeyDown;
                bool isUp = msg == WmKeyUp || msg == WmSysKeyUp;

                if (kb.vkCode == VkLWin)
                {
                    if (isDown && !_lwinDown)
                    {
                        _lwinDown = true;
                        _otherKeyPressed = false;
                    }
                    else if (isUp)
                    {
                        bool triggerOmni = _lwinDown && !_otherKeyPressed;
                        _lwinDown = false;
                        _otherKeyPressed = false;

                        if (triggerOmni)
                        {
                            // Inject a harmless masked VK_NONAME press+release before LWin goes up
                            // so explorer.exe suppresses the native Start Menu cleanly without losing LWin state.
                            SuppressNativeStartMenuMask();
                            try { SingleWinKeyTapped?.Invoke(this, EventArgs.Empty); } catch { }
                        }
                    }
                }
                else if (_lwinDown && isDown && kb.vkCode != VkNoName)
                {
                    _otherKeyPressed = true;
                }
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static void SuppressNativeStartMenuMask()
    {
        var inputs = new INPUT[]
        {
            new() { type = 1, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = (ushort)VkNoName, dwFlags = 0 } } },
            new() { type = 1, u = new INPUTUNION { ki = new KEYBDINPUT { wVk = (ushort)VkNoName, dwFlags = 0x0002 } } }
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
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
    private struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL, wParamH;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint SetWindowsHookExW(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}
