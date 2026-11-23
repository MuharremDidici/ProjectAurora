using System.Runtime.InteropServices;
using Aurora.Core.Models;
using Aurora.Platform.Windows.Win32;
using Microsoft.Win32;

namespace Aurora.Platform.Windows.DisplayAndTaskbar;

public sealed record VirtualDesktopInfo(int Index, Guid Id, string Name, bool IsCurrent);

public sealed record VirtualDesktopSnapshot(
    int CurrentIndex,
    int TotalCount,
    string ActiveName,
    IReadOnlyList<VirtualDesktopInfo> Desktops);

/// <summary>
/// Safe, registry-backed Windows 11 Virtual Desktop reader & SendInput switcher.
/// Avoids fragile undocumented COM vtables that break across Windows 11 22H2/23H2/24H2/25H2 updates.
/// </summary>
public sealed class VirtualDesktopService
{
    private const string VirtualDesktopsRegPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";

    public VirtualDesktopSnapshot GetSnapshot()
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(VirtualDesktopsRegPath, writable: false);
            if (root is null)
                return FallbackSingle();

            var idsBytes = root.GetValue("VirtualDesktopIDs") as byte[];
            var curBytes = root.GetValue("CurrentVirtualDesktop") as byte[];

            if (idsBytes is null || idsBytes.Length < 16)
                return FallbackSingle();

            Guid currentGuid = (curBytes is { Length: >= 16 })
                ? new Guid(curBytes.AsSpan(0, 16))
                : Guid.Empty;

            int count = idsBytes.Length / 16;
            var list = new List<VirtualDesktopInfo>(count);
            int currentIdx = 0;

            for (int i = 0; i < count; i++)
            {
                var g = new Guid(idsBytes.AsSpan(i * 16, 16));
                bool isCur = g == currentGuid;
                if (isCur) currentIdx = i;

                string? customName = null;
                try
                {
                    using var dKey = root.OpenSubKey($@"Desktops\{{{g:D}}}", writable: false);
                    customName = dKey?.GetValue("Name") as string;
                }
                catch { }

                string name = string.IsNullOrWhiteSpace(customName)
                    ? $"Masaüstü {i + 1}"
                    : customName.Trim();

                list.Add(new VirtualDesktopInfo(i, g, name, isCur));
            }

            if (list.Count == 0)
                return FallbackSingle();

            if (!list.Any(d => d.IsCurrent))
            {
                list[0] = list[0] with { IsCurrent = true };
                currentIdx = 0;
            }

            return new VirtualDesktopSnapshot(currentIdx, list.Count, list[currentIdx].Name, list);
        }
        catch
        {
            return FallbackSingle();
        }
    }

    public void SwitchRelative(int delta)
    {
        if (delta == 0) return;
        ushort arrowVk = delta > 0 ? (ushort)0x27 /* VK_RIGHT */ : (ushort)0x25 /* VK_LEFT */;
        int steps = Math.Clamp(Math.Abs(delta), 1, 8);
        for (int i = 0; i < steps; i++)
        {
            SendChord(0xA2 /* VK_LCONTROL */, 0x5B /* VK_LWIN */, arrowVk);
            if (steps > 1) Thread.Sleep(40);
        }
    }

    public void SwitchToIndex(int targetIndex)
    {
        var snap = GetSnapshot();
        if (targetIndex < 0 || targetIndex >= snap.TotalCount || targetIndex == snap.CurrentIndex)
            return;
        SwitchRelative(targetIndex - snap.CurrentIndex);
    }

    public void CreateNewDesktop()
    {
        SendChord(0xA2 /* VK_LCONTROL */, 0x5B /* VK_LWIN */, 0x44 /* 'D' */);
    }

    public void OpenTaskView()
    {
        SendPair(0x5B /* VK_LWIN */, 0x09 /* VK_TAB */);
    }

    private static VirtualDesktopSnapshot FallbackSingle() =>
        new(0, 1, "Masaüstü 1", [new VirtualDesktopInfo(0, Guid.Empty, "Masaüstü 1", true)]);

    private static void SendChord(ushort mod1, ushort mod2, ushort key)
    {
        var inputs = new INPUT[]
        {
            MakeKey(mod1, false),
            MakeKey(mod2, false),
            MakeKey(key, false),
            MakeKey(key, true),
            MakeKey(mod2, true),
            MakeKey(mod1, true)
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void SendPair(ushort mod1, ushort key)
    {
        var inputs = new INPUT[]
        {
            MakeKey(mod1, false),
            MakeKey(key, false),
            MakeKey(key, true),
            MakeKey(mod1, true)
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT MakeKey(ushort vk, bool keyUp) => new()
    {
        type = 1, // INPUT_KEYBOARD
        u = new INPUTUNION
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                dwFlags = keyUp ? 0x0002u /* KEYEVENTF_KEYUP */ : 0u
            }
        }
    };

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}

/// <summary>
/// Clean multi-window smart tiling engine for 1-click workspace layouts
/// (Code 65/35, Balanced 50/50, Studio 3-Pane 50/25/25, Focus Zen 75%).
/// </summary>
public static class WindowLayoutEngine
{
    public static int ApplyPreset(
        string presetId,
        IReadOnlyList<AppWindowEntry> openWindows,
        nint primaryHwnd,
        string? preferredMonitorId = null)
    {
        if (openWindows.Count == 0)
            return 0;

        // Order: primaryHwnd first, then non-minimized windows on the same monitor, then remaining visible windows
        var primaryWin = openWindows.FirstOrDefault(w => w.Hwnd == primaryHwnd)
                         ?? openWindows.FirstOrDefault(w => w.IsActive)
                         ?? openWindows.FirstOrDefault(w => !w.IsMinimized)
                         ?? openWindows[0];

        string targetMonId = !string.IsNullOrWhiteSpace(preferredMonitorId)
            ? preferredMonitorId
            : primaryWin.MonitorDeviceId;

        var candidates = new List<AppWindowEntry> { primaryWin };
        candidates.AddRange(openWindows.Where(w =>
            w.Hwnd != primaryWin.Hwnd &&
            !w.IsMinimized &&
            string.Equals(w.MonitorDeviceId, targetMonId, StringComparison.OrdinalIgnoreCase)));
        candidates.AddRange(openWindows.Where(w =>
            w.Hwnd != primaryWin.Hwnd &&
            !w.IsMinimized &&
            !string.Equals(w.MonitorDeviceId, targetMonId, StringComparison.OrdinalIgnoreCase)));

        nint hMon = NativeMethods.MonitorFromWindow(primaryWin.Hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
        var mi = new NativeMethods.MONITORINFOEXW { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEXW>() };
        if (hMon == 0 || !NativeMethods.GetMonitorInfoW(hMon, ref mi))
            return 0;

        var work = mi.rcWork;
        const int gap = 6;
        int x0 = work.Left + gap;
        int y0 = work.Top + gap;
        int availW = Math.Max(320, work.Width - (gap * 2));
        int availH = Math.Max(240, work.Height - (gap * 2));

        int arranged = 0;

        switch (presetId.ToLowerInvariant())
        {
            case "code6535":
                {
                    int leftW = (int)Math.Round(availW * 0.65) - (gap / 2);
                    int rightW = availW - leftW - gap;
                    arranged += PlaceWindow(candidates[0].Hwnd, x0, y0, leftW, availH);
                    if (candidates.Count >= 2)
                        arranged += PlaceWindow(candidates[1].Hwnd, x0 + leftW + gap, y0, rightW, availH);
                    break;
                }

            case "split5050":
                {
                    int halfW = (availW - gap) / 2;
                    arranged += PlaceWindow(candidates[0].Hwnd, x0, y0, halfW, availH);
                    if (candidates.Count >= 2)
                        arranged += PlaceWindow(candidates[1].Hwnd, x0 + halfW + gap, y0, availW - halfW - gap, availH);
                    break;
                }

            case "studio3":
                {
                    int leftW = (availW - gap) / 2;
                    int rightW = availW - leftW - gap;
                    int halfH = (availH - gap) / 2;
                    arranged += PlaceWindow(candidates[0].Hwnd, x0, y0, leftW, availH);
                    if (candidates.Count >= 2)
                        arranged += PlaceWindow(candidates[1].Hwnd, x0 + leftW + gap, y0, rightW, halfH);
                    if (candidates.Count >= 3)
                        arranged += PlaceWindow(candidates[2].Hwnd, x0 + leftW + gap, y0 + halfH + gap, rightW, availH - halfH - gap);
                    break;
                }

            case "zen75":
            default:
                {
                    int cw = (int)Math.Round(availW * 0.76);
                    int ch = (int)Math.Round(availH * 0.84);
                    arranged += PlaceWindow(candidates[0].Hwnd, x0 + ((availW - cw) / 2), y0 + ((availH - ch) / 2), cw, ch);
                    break;
                }
        }

        NativeMethods.SetForegroundWindow(candidates[0].Hwnd);
        return arranged;
    }

    private static int PlaceWindow(nint hwnd, int x, int y, int w, int h)
    {
        if (hwnd == 0 || w <= 0 || h <= 0) return 0;
        try
        {
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
            NativeMethods.SetWindowPos(hwnd, 0, x, y, w, h, 0x0004 /* SWP_NOZORDER */);
            return 1;
        }
        catch
        {
            return 0;
        }
    }
}
