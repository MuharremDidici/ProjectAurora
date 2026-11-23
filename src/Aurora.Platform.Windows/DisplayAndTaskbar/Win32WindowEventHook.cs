using System.Runtime.InteropServices;

namespace Aurora.Platform.Windows.DisplayAndTaskbar;

/// <summary>
/// Out-of-context Win32 SetWinEventHook listener that delivers instant (&lt; 60 ms)
/// window focus, creation, destruction, minimize, and title-change notifications
/// without DLL injection and without high-frequency polling.
/// </summary>
public sealed class Win32WindowEventHook : IDisposable
{
    private const uint WineventOutOfContext = 0x0000;
    private const uint WineventSkipOwnProcess = 0x0002;

    public const uint EventSystemAlert = 0x0002;
    private const uint EventSystemForeground = 0x0003;
    public const uint EventSystemMoveSizeStart = 0x000A;
    public const uint EventSystemMoveSizeEnd = 0x000B;
    private const uint EventSystemMinimizeStart = 0x0016;
    private const uint EventSystemMinimizeEnd = 0x0017;
    public const uint EventObjectCreate = 0x8000;
    private const uint EventObjectDestroy = 0x8001;
    public const uint EventObjectShow = 0x8002;
    public const uint EventObjectHide = 0x8003;
    public const uint EventObjectNameChange = 0x800C;
    public const uint EventObjectUncloaked = 0x8018;

    private const int ObjidWindow = 0;

    private delegate void WinEventDelegate(
        nint hWinEventHook,
        uint eventType,
        nint hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern nint SetWinEventHook(
        uint eventMin,
        uint eventMax,
        nint hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(nint hWinEventHook);

    private readonly WinEventDelegate _callback;
    private readonly List<nint> _hooks = [];
    private bool _disposed;

    public event Action<uint, nint>? WindowEventObserved;

    public Win32WindowEventHook()
    {
        // Keep delegate strongly rooted for the lifetime of the hooks
        _callback = OnWinEvent;
    }

    public bool IsActive => _hooks.Count > 0 && !_disposed;

    public void Start()
    {
        if (_disposed || _hooks.Count > 0)
            return;

        uint flags = WineventOutOfContext | WineventSkipOwnProcess;
        TryInstallHook(EventSystemAlert, EventSystemForeground, flags);
        TryInstallHook(EventSystemMoveSizeStart, EventSystemMoveSizeEnd, flags);
        TryInstallHook(EventSystemMinimizeStart, EventSystemMinimizeEnd, flags);
        TryInstallHook(EventObjectCreate, EventObjectHide, flags);
        TryInstallHook(EventObjectNameChange, EventObjectNameChange, flags);
        TryInstallHook(EventObjectUncloaked, EventObjectUncloaked, flags);
    }

    private void TryInstallHook(uint min, uint max, uint flags)
    {
        nint h = SetWinEventHook(min, max, 0, _callback, 0, 0, flags);
        if (h != 0)
            _hooks.Add(h);
    }

    private void OnWinEvent(
        nint hWinEventHook,
        uint eventType,
        nint hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (_disposed || hwnd == 0 || idObject != ObjidWindow || idChild != 0)
            return;

        WindowEventObserved?.Invoke(eventType, hwnd);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        foreach (var h in _hooks)
        {
            try { UnhookWinEvent(h); } catch { }
        }
        _hooks.Clear();
    }
}
