using System.Runtime.InteropServices;
using Aurora.Core.Contracts;
using Aurora.Core.Primitives;

namespace Aurora.Platform.Windows.Audio;

/// <summary>
/// EarTrumpet-grade per-application default audio output routing backed by
/// Windows internal WinRT AudioPolicyConfig activation factory (asInvoker, zero admin).
/// Uses raw COM vtable calls for full .NET 10 compatibility and gracefully falls back
/// to ms-settings:apps-volume if an OS build alters the private interface.
/// </summary>
public sealed class PerAppAudioRoutingService(IAuroraLogger logger)
{
    private const string AudioPolicyClassName = "Windows.Media.Internal.AudioPolicyConfig";
    private const string DevInterfaceAudioRenderGuid = "{e6327cad-dcec-4949-ae8a-991e976a79d2}";
    private const int EDataFlowRender = 0;
    private const int ERoleConsole = 0;
    private const int ERoleMultimedia = 1;

    // Windows 10 21H2+ & Windows 11 22H2/23H2/24H2/25H2 IID
    private static readonly Guid IidAudioPolicyConfig21H2 = new("AB3D4648-E242-459F-B02F-541C70306324");
    // Older Windows 10 (1803–21H1) fallback IID
    private static readonly Guid IidAudioPolicyConfigDownlevel = new("2A59116D-6C4F-45E0-A74F-707E3FEF9258");

    // Vtable slots: IUnknown (0..2) + IInspectable (3..5) + 19 policy methods (6..24)
    private const int SlotSetPersistedDefaultAudioEndpoint = 25;
    private const int SlotGetPersistedDefaultAudioEndpoint = 26;

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
        uint length,
        out nint hstring);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString(nint hstring);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern nint WindowsGetStringRawBuffer(nint hstring, out uint length);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoGetActivationFactory(
        nint activatableClassId,
        ref Guid iid,
        out nint factory);

    /// <summary>
    /// Formats a standard Core Audio MMDevice ID (e.g. "{0.0.0.00000000}.{guid}")
    /// into the WinRT SWD device interface path required by SetPersistedDefaultAudioEndpoint.
    /// Returns null/empty to clear the override and revert the app back to System Default.
    /// </summary>
    public static string? FormatRenderDeviceInterfaceId(string? mmDeviceId)
    {
        if (string.IsNullOrWhiteSpace(mmDeviceId))
            return null;

        string trimmed = mmDeviceId.Trim();
        if (trimmed.StartsWith(@"\\?\SWD#MMDEVAPI#", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return $@"\\?\SWD#MMDEVAPI#{trimmed}#{DevInterfaceAudioRenderGuid}";
    }

    /// <summary>
    /// Extracts the underlying MMDevice ID (e.g. "{0.0.0.00000000}.{guid}") from an SWD path
    /// returned by GetPersistedDefaultAudioEndpoint.
    /// </summary>
    public static string? ExtractMmDeviceId(string? swdPath)
    {
        if (string.IsNullOrWhiteSpace(swdPath))
            return null;

        const string prefix = @"\\?\SWD#MMDEVAPI#";
        if (!swdPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return swdPath.Trim();

        string rest = swdPath[prefix.Length..];
        int hashIdx = rest.IndexOf('#');
        return hashIdx > 0 ? rest[..hashIdx] : rest;
    }

    public unsafe bool IsSupported()
    {
        if (!TryAcquireFactory(out nint factory))
            return false;
        Marshal.Release(factory);
        return true;
    }

    /// <summary>
    /// Routes a specific running application (by ProcessId) to a chosen playback MMDevice ID.
    /// Pass null or empty string to reset that application back to Windows System Default.
    /// </summary>
    public unsafe AuroraResult SetAppOutputDevice(uint processId, string? mmDeviceId)
    {
        if (processId == 0)
            return AuroraResult.Fail("AUDIO_APP_PID_INVALID", "Geçersiz uygulama kimliği.");

        if (!TryAcquireFactory(out nint factory))
        {
            return AuroraResult.Fail(
                "AUDIO_POLICY_UNSUPPORTED",
                "Bu Windows sürümünde uygulama bazlı ses yönlendirme desteklenmiyor.",
                CapabilityState.DeepLinkFallback,
                "ms-settings:apps-volume");
        }

        nint deviceHStr = 0;
        try
        {
            string? swdPath = FormatRenderDeviceInterfaceId(mmDeviceId);
            if (!string.IsNullOrEmpty(swdPath))
            {
                int hrStr = WindowsCreateString(swdPath, (uint)swdPath.Length, out deviceHStr);
                if (hrStr != 0)
                    return AuroraResult.Fail("AUDIO_HSTRING_ERR", "Aygıt yolu hazırlanamadı.", CapabilityState.Error, "ms-settings:apps-volume");
            }

            nint* vtable = *(nint**)factory;
            var setFn = (delegate* unmanaged[Stdcall]<nint, uint, int, int, nint, int>)vtable[SlotSetPersistedDefaultAudioEndpoint];

            int hrConsole = setFn(factory, processId, EDataFlowRender, ERoleConsole, deviceHStr);
            int hrMulti = setFn(factory, processId, EDataFlowRender, ERoleMultimedia, deviceHStr);

            if (hrConsole == 0 || hrMulti == 0)
                return AuroraResult.Ok();

            logger.Warn("Audio", "PER_APP_ROUTE_HR", $"SetPersistedDefaultAudioEndpoint failed (0x{hrMulti:X8}).");
            return AuroraResult.Fail(
                "AUDIO_APP_ROUTE_FAILED",
                "Uygulama ses çıkışı yönlendirilemedi.",
                CapabilityState.DeepLinkFallback,
                "ms-settings:apps-volume");
        }
        catch (Exception ex)
        {
            logger.Warn("Audio", "PER_APP_ROUTE_EX", $"Per-app audio routing exception ({ex.GetType().Name}).");
            return AuroraResult.Fail(
                "AUDIO_APP_ROUTE_EX",
                "Uygulama ses çıkışı değiştirilemedi.",
                CapabilityState.DeepLinkFallback,
                "ms-settings:apps-volume");
        }
        finally
        {
            if (deviceHStr != 0)
                WindowsDeleteString(deviceHStr);
            Marshal.Release(factory);
        }
    }

    /// <summary>
    /// Reads the persisted per-app playback MMDevice ID if overridden, or null when using System Default.
    /// </summary>
    public unsafe string? GetAppOutputDeviceId(uint processId)
    {
        if (processId == 0 || !TryAcquireFactory(out nint factory))
            return null;

        nint outHStr = 0;
        try
        {
            nint* vtable = *(nint**)factory;
            var getFn = (delegate* unmanaged[Stdcall]<nint, uint, int, int, out nint, int>)vtable[SlotGetPersistedDefaultAudioEndpoint];

            int hr = getFn(factory, processId, EDataFlowRender, ERoleMultimedia, out outHStr);
            if (hr != 0 || outHStr == 0)
                return null;

            nint rawPtr = WindowsGetStringRawBuffer(outHStr, out uint len);
            if (rawPtr == 0 || len == 0)
                return null;

            string swd = Marshal.PtrToStringUni(rawPtr, (int)len);
            return ExtractMmDeviceId(swd);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (outHStr != 0)
                WindowsDeleteString(outHStr);
            Marshal.Release(factory);
        }
    }

    private static bool TryAcquireFactory(out nint factory)
    {
        factory = 0;
        nint classHStr = 0;
        try
        {
            if (WindowsCreateString(AudioPolicyClassName, (uint)AudioPolicyClassName.Length, out classHStr) != 0 || classHStr == 0)
                return false;

            var iid21H2 = IidAudioPolicyConfig21H2;
            if (RoGetActivationFactory(classHStr, ref iid21H2, out factory) == 0 && factory != 0)
                return true;

            var iidDown = IidAudioPolicyConfigDownlevel;
            if (RoGetActivationFactory(classHStr, ref iidDown, out factory) == 0 && factory != 0)
                return true;

            return false;
        }
        catch
        {
            factory = 0;
            return false;
        }
        finally
        {
            if (classHStr != 0)
                WindowsDeleteString(classHStr);
        }
    }
}
