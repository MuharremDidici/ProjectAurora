using System.Diagnostics;
using System.Runtime.InteropServices;
using Aurora.Core.Contracts;
using Aurora.Core.Models;
using Aurora.Core.Primitives;

namespace Aurora.Platform.Windows.Audio;

public sealed class CoreAudioService(IAuroraLogger logger) : IAudioEndpointService, IAudioSessionService
{
    private static readonly Guid ClsidMMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid ClsidPolicyConfigClient = new("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9");
    private static readonly Guid IidIAudioEndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    private static readonly Guid IidIAudioSessionManager2 = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");

    private const int EDataFlowRender = 0;
    private const int EDataFlowCapture = 1;
    private const int ERoleConsole = 0;
    private const int ERoleMultimedia = 1;
    private const int ERoleCommunications = 2;
    private const uint DeviceStateActive = 0x00000001;
    private const uint StgmRead = 0x00000000;
    private const uint ClsctxAll = 23;

    public AuroraResult<IReadOnlyList<AudioEndpointInfo>> GetEndpoints(bool includeCapture = true)
    {
        try
        {
            var list = new List<AudioEndpointInfo>();
            var enumerator = CreateDeviceEnumerator();
            if (enumerator is null)
                return AuroraResult<IReadOnlyList<AudioEndpointInfo>>.Fail("AUDIO_COM_UNAVAILABLE", "Core Audio başlatılamadı.", CapabilityState.Unavailable);

            ReadFlowEndpoints(enumerator, EDataFlowRender, isCapture: false, list);
            if (includeCapture)
                ReadFlowEndpoints(enumerator, EDataFlowCapture, isCapture: true, list);

            Marshal.ReleaseComObject(enumerator);
            return AuroraResult<IReadOnlyList<AudioEndpointInfo>>.Ok(list);
        }
        catch (Exception ex)
        {
            logger.Warn("Audio", "AUDIO_ENUM_WARN", $"Endpoint enumeration warning ({ex.GetType().Name}).");
            return AuroraResult<IReadOnlyList<AudioEndpointInfo>>.Fail("AUDIO_ENUM_FAILED", "Ses aygıtları listelenemedi.", CapabilityState.Error, "ms-settings:sound");
        }
    }

    private static void ReadFlowEndpoints(IMMDeviceEnumerator enumerator, int dataFlow, bool isCapture, List<AudioEndpointInfo> target)
    {
        string? defaultId = null;
        if (enumerator.GetDefaultAudioEndpoint(dataFlow, ERoleMultimedia, out var defDev) == 0 && defDev is not null)
        {
            defDev.GetId(out defaultId);
            Marshal.ReleaseComObject(defDev);
        }

        if (enumerator.EnumAudioEndpoints(dataFlow, DeviceStateActive, out var collection) != 0 || collection is null)
            return;

        collection.GetCount(out uint count);
        for (uint i = 0; i < count; i++)
        {
            if (collection.Item(i, out var dev) != 0 || dev is null)
                continue;

            try
            {
                dev.GetId(out var id);
                string friendly = ReadDeviceFriendlyName(dev) ?? (isCapture ? $"Mikrofon {i + 1}" : $"Hoparlör {i + 1}");
                double vol = 1.0;
                bool muted = false;

                var iidVol = IidIAudioEndpointVolume;
                if (dev.Activate(ref iidVol, ClsctxAll, 0, out var volObj) == 0 && volObj is IAudioEndpointVolume epVol)
                {
                    epVol.GetMasterVolumeLevelScalar(out float v);
                    epVol.GetMute(out muted);
                    vol = Math.Clamp(Math.Round(v, 3), 0.0, 1.0);
                    Marshal.ReleaseComObject(epVol);
                }

                target.Add(new AudioEndpointInfo(
                    Id: id ?? $"ep-{i}",
                    FriendlyName: friendly,
                    IsCapture: isCapture,
                    IsDefault: !string.IsNullOrEmpty(defaultId) && string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase),
                    VolumeScalar: vol,
                    IsMuted: muted));
            }
            finally
            {
                Marshal.ReleaseComObject(dev);
            }
        }

        Marshal.ReleaseComObject(collection);
    }

    public AuroraResult<(double VolumeScalar, bool IsMuted, string DeviceName)> GetDefaultOutputState()
    {
        try
        {
            var enumerator = CreateDeviceEnumerator();
            if (enumerator is null)
                return AuroraResult<(double, bool, string)>.Fail("AUDIO_NO_ENUM", "Ses hizmeti kullanılamıyor.", CapabilityState.Unavailable, "ms-settings:sound");

            try
            {
                if (enumerator.GetDefaultAudioEndpoint(EDataFlowRender, ERoleMultimedia, out var dev) != 0 || dev is null)
                    return AuroraResult<(double, bool, string)>.Fail("AUDIO_NO_DEFAULT", "Aktif ses çıkış aygıtı bulunamadı.", CapabilityState.Unavailable, "ms-settings:sound");

                try
                {
                    var name = ReadDeviceFriendlyName(dev) ?? "Varsayılan Hoparlör";
                    var iid = IidIAudioEndpointVolume;
                    if (dev.Activate(ref iid, ClsctxAll, 0, out var obj) == 0 && obj is IAudioEndpointVolume epVol)
                    {
                        try
                        {
                            epVol.GetMasterVolumeLevelScalar(out float v);
                            epVol.GetMute(out bool muted);
                            return AuroraResult<(double, bool, string)>.Ok((Math.Clamp(v, 0f, 1f), muted, name));
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(epVol);
                        }
                    }

                    return AuroraResult<(double, bool, string)>.Fail("AUDIO_ACTIVATE_FAIL", "Ses denetimi açılamadı.", CapabilityState.Error, "ms-settings:sound");
                }
                finally
                {
                    Marshal.ReleaseComObject(dev);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(enumerator);
            }
        }
        catch (Exception ex)
        {
            logger.Warn("Audio", "AUDIO_STATE_WARN", $"Default audio state query failed ({ex.GetType().Name}).");
            return AuroraResult<(double, bool, string)>.Fail("AUDIO_ERR", "Ses durumu okunamadı.", CapabilityState.Error, "ms-settings:sound");
        }
    }

    public AuroraResult SetMasterVolume(double volumeScalar)
    {
        float clamped = (float)Math.Clamp(volumeScalar, 0.0, 1.0);
        return WithDefaultEndpointVolume(epVol =>
        {
            var emptyCtx = Guid.Empty;
            int hr = epVol.SetMasterVolumeLevelScalar(clamped, ref emptyCtx);
            return hr == 0
                ? AuroraResult.Ok()
                : AuroraResult.Fail("AUDIO_SET_VOL_HR", $"Ses düzeyi değiştirilemedi (0x{hr:X8}).", CapabilityState.Error, "ms-settings:sound");
        });
    }

    public AuroraResult SetMasterMute(bool isMuted)
    {
        return WithDefaultEndpointVolume(epVol =>
        {
            var emptyCtx = Guid.Empty;
            int hr = epVol.SetMute(isMuted, ref emptyCtx);
            return hr == 0
                ? AuroraResult.Ok()
                : AuroraResult.Fail("AUDIO_SET_MUTE_HR", $"Sessize alma işlemi başarısız (0x{hr:X8}).", CapabilityState.Error, "ms-settings:sound");
        });
    }

    public AuroraResult SetDefaultOutputDevice(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return AuroraResult.Fail("AUDIO_INVALID_ID", "Geçersiz ses cihazı.");

        try
        {
            var type = Type.GetTypeFromCLSID(ClsidPolicyConfigClient);
            if (type is null)
                return AuroraResult.Fail("AUDIO_POLICY_MISSING", "Windows ses yönlendiricisi bulunamadı.", CapabilityState.Unavailable, "ms-settings:sound");

            var obj = Activator.CreateInstance(type);
            if (obj is not IPolicyConfig policy)
                return AuroraResult.Fail("AUDIO_POLICY_IFACE", "Ses çıkış değiştirme arayüzü açılamadı.", CapabilityState.Unavailable, "ms-settings:sound");

            try
            {
                int hr1 = policy.SetDefaultEndpoint(deviceId, ERoleConsole);
                int hr2 = policy.SetDefaultEndpoint(deviceId, ERoleMultimedia);
                int hr3 = policy.SetDefaultEndpoint(deviceId, ERoleCommunications);
                if (hr1 == 0 || hr2 == 0 || hr3 == 0)
                    return AuroraResult.Ok();

                return AuroraResult.Fail("AUDIO_SWITCH_HR", $"Ses çıkışı değiştirilemedi (0x{hr2:X8}).", CapabilityState.Error, "ms-settings:sound");
            }
            finally
            {
                Marshal.ReleaseComObject(policy);
            }
        }
        catch (Exception ex)
        {
            logger.Warn("Audio", "AUDIO_SWITCH_WARN", $"Default output switch failed ({ex.GetType().Name}).");
            return AuroraResult.Fail("AUDIO_SWITCH_ERR", "Ses çıkışı değiştirilemedi.", CapabilityState.Error, "ms-settings:sound");
        }
    }

    public (bool Supported, bool IsMuted, string DeviceName) GetDefaultCaptureMuteState()
    {
        try
        {
            var enumerator = CreateDeviceEnumerator();
            if (enumerator is null)
                return (false, false, "Mikrofon");

            try
            {
                int hr = enumerator.GetDefaultAudioEndpoint(EDataFlowCapture, ERoleCommunications, out var dev);
                if (hr != 0 || dev is null)
                    hr = enumerator.GetDefaultAudioEndpoint(EDataFlowCapture, ERoleMultimedia, out dev);
                if (hr != 0 || dev is null)
                    return (false, false, "Mikrofon Yok");

                try
                {
                    string name = ReadDeviceFriendlyName(dev) ?? "Mikrofon";
                    var iid = IidIAudioEndpointVolume;
                    if (dev.Activate(ref iid, ClsctxAll, 0, out var obj) == 0 && obj is IAudioEndpointVolume epVol)
                    {
                        try
                        {
                            epVol.GetMute(out bool muted);
                            return (true, muted, name);
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(epVol);
                        }
                    }
                    return (false, false, name);
                }
                finally
                {
                    Marshal.ReleaseComObject(dev);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(enumerator);
            }
        }
        catch
        {
            return (false, false, "Mikrofon");
        }
    }

    public AuroraResult<bool> ToggleDefaultCaptureMute()
    {
        try
        {
            var enumerator = CreateDeviceEnumerator();
            if (enumerator is null)
                return AuroraResult<bool>.Fail("MIC_NO_ENUM", "Ses hizmeti kullanılamıyor.");

            try
            {
                int hr = enumerator.GetDefaultAudioEndpoint(EDataFlowCapture, ERoleCommunications, out var dev);
                if (hr != 0 || dev is null)
                    hr = enumerator.GetDefaultAudioEndpoint(EDataFlowCapture, ERoleMultimedia, out dev);
                if (hr != 0 || dev is null)
                    return AuroraResult<bool>.Fail("MIC_NO_DEVICE", "Aktif mikrofon bulunamadı.");

                try
                {
                    var iid = IidIAudioEndpointVolume;
                    if (dev.Activate(ref iid, ClsctxAll, 0, out var obj) != 0 || obj is not IAudioEndpointVolume epVol)
                        return AuroraResult<bool>.Fail("MIC_NO_VOL", "Mikrofon denetimine erişilemedi.");

                    try
                    {
                        epVol.GetMute(out bool currentlyMuted);
                        bool nextMuted = !currentlyMuted;
                        var emptyCtx = Guid.Empty;
                        int setHr = epVol.SetMute(nextMuted, ref emptyCtx);
                        return setHr == 0
                            ? AuroraResult<bool>.Ok(nextMuted)
                            : AuroraResult<bool>.Fail("MIC_MUTE_HR", $"Mikrofon durumu değiştirilemedi (0x{setHr:X8}).");
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(epVol);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(dev);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(enumerator);
            }
        }
        catch (Exception ex)
        {
            logger.Warn("Audio", "MIC_MUTE_WARN", $"Mic mute toggle failed ({ex.GetType().Name}).");
            return AuroraResult<bool>.Fail("MIC_ERR", "Mikrofon sessize alınamadı.");
        }
    }

    private AuroraResult WithDefaultEndpointVolume(Func<IAudioEndpointVolume, AuroraResult> action)
    {
        try
        {
            var enumerator = CreateDeviceEnumerator();
            if (enumerator is null)
                return AuroraResult.Fail("AUDIO_UNAVAILABLE", "Ses yöneticisi kullanılamıyor.", CapabilityState.Unavailable, "ms-settings:sound");

            try
            {
                if (enumerator.GetDefaultAudioEndpoint(EDataFlowRender, ERoleMultimedia, out var dev) != 0 || dev is null)
                    return AuroraResult.Fail("AUDIO_NO_ENDPOINT", "Aktif ses çıkış aygıtı yok.", CapabilityState.Unavailable, "ms-settings:sound");

                try
                {
                    var iid = IidIAudioEndpointVolume;
                    if (dev.Activate(ref iid, ClsctxAll, 0, out var obj) != 0 || obj is not IAudioEndpointVolume epVol)
                        return AuroraResult.Fail("AUDIO_VOL_IFACE", "Ses düzeyi arayüzü açılamadı.", CapabilityState.Error, "ms-settings:sound");

                    try
                    {
                        return action(epVol);
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(epVol);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(dev);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(enumerator);
            }
        }
        catch (Exception ex)
        {
            logger.Warn("Audio", "AUDIO_OP_ERR", $"Master audio operation failed ({ex.GetType().Name}).");
            return AuroraResult.Fail("AUDIO_EXCEPTION", "Ses işlemi sırasında hata oluştu.", CapabilityState.Error, "ms-settings:sound");
        }
    }

    public AuroraResult<IReadOnlyList<AudioSessionInfo>> GetActiveSessions()
    {
        var list = new List<AudioSessionInfo>();
        try
        {
            var enumerator = CreateDeviceEnumerator();
            if (enumerator is null)
                return AuroraResult<IReadOnlyList<AudioSessionInfo>>.Ok(list);

            try
            {
                if (enumerator.GetDefaultAudioEndpoint(EDataFlowRender, ERoleMultimedia, out var dev) != 0 || dev is null)
                    return AuroraResult<IReadOnlyList<AudioSessionInfo>>.Ok(list);

                try
                {
                    var iidMgr = IidIAudioSessionManager2;
                    if (dev.Activate(ref iidMgr, ClsctxAll, 0, out var mgrObj) != 0 || mgrObj is not IAudioSessionManager2 mgr)
                        return AuroraResult<IReadOnlyList<AudioSessionInfo>>.Ok(list);

                    try
                    {
                        if (mgr.GetSessionEnumerator(out var sessEnum) != 0 || sessEnum is null)
                            return AuroraResult<IReadOnlyList<AudioSessionInfo>>.Ok(list);

                        try
                        {
                            sessEnum.GetCount(out int count);
                            var seenPids = new HashSet<uint>();
                            for (int i = 0; i < count; i++)
                            {
                                if (sessEnum.GetSession(i, out var ctrl) != 0 || ctrl is null)
                                    continue;

                                try
                                {
                                    if (ctrl is IAudioSessionControl2 ctrl2 && ctrl2 is ISimpleAudioVolume simpleVol)
                                    {
                                        ctrl2.GetProcessId(out uint pid);
                                        ctrl2.GetState(out int state); // 0=Inactive, 1=Active, 2=Expired
                                        if (state == 2)
                                            continue;

                                        if (pid != 0 && !seenPids.Add(pid))
                                            continue;

                                        ctrl2.GetDisplayName(out string? disp);
                                        string label = disp ?? string.Empty;
                                        if (pid == 0 || label.Contains("AudioSrv", StringComparison.OrdinalIgnoreCase))
                                        {
                                            label = "Sistem Sesleri";
                                        }
                                        else if (string.IsNullOrWhiteSpace(label) || label.StartsWith('@'))
                                        {
                                            try
                                            {
                                                using var p = Process.GetProcessById((int)pid);
                                                label = p.ProcessName;
                                            }
                                            catch
                                            {
                                                continue;
                                            }
                                        }

                                        simpleVol.GetMasterVolume(out float sv);
                                        simpleVol.GetMute(out bool sm);

                                        list.Add(new AudioSessionInfo(
                                            SessionIdentifier: $"pid-{pid}-{i}",
                                            ProcessId: pid,
                                            DisplayName: label,
                                            VolumeScalar: Math.Clamp(sv, 0f, 1f),
                                            IsMuted: sm,
                                            IsActive: state == 1));
                                    }
                                }
                                finally
                                {
                                    Marshal.ReleaseComObject(ctrl);
                                }
                            }
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(sessEnum);
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(mgr);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(dev);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(enumerator);
            }

            return AuroraResult<IReadOnlyList<AudioSessionInfo>>.Ok(
                list.OrderByDescending(s => s.IsActive).ThenBy(s => s.DisplayName).ToList());
        }
        catch (Exception ex)
        {
            logger.Warn("Audio", "SESSION_ENUM_WARN", $"Audio session enumeration failed ({ex.GetType().Name}).");
            return AuroraResult<IReadOnlyList<AudioSessionInfo>>.Fail("AUDIO_SESSION_ERR", "Uygulama ses oturumları okunamadı.");
        }
    }

    public AuroraResult SetSessionVolume(uint processId, double volumeScalar) =>
        MutateProcessSession(processId, vol =>
        {
            var ctx = Guid.Empty;
            return vol.SetMasterVolume((float)Math.Clamp(volumeScalar, 0.0, 1.0), ref ctx) == 0;
        });

    public AuroraResult SetSessionMute(uint processId, bool isMuted) =>
        MutateProcessSession(processId, vol =>
        {
            var ctx = Guid.Empty;
            return vol.SetMute(isMuted, ref ctx) == 0;
        });

    private AuroraResult MutateProcessSession(uint targetPid, Func<ISimpleAudioVolume, bool> mutator)
    {
        try
        {
            var enumerator = CreateDeviceEnumerator();
            if (enumerator is null)
                return AuroraResult.Fail("AUDIO_UNAVAILABLE", "Ses hizmeti kullanılamıyor.");

            try
            {
                if (enumerator.GetDefaultAudioEndpoint(EDataFlowRender, ERoleMultimedia, out var dev) != 0 || dev is null)
                    return AuroraResult.Fail("AUDIO_NO_DEV", "Ses çıkış aygıtı bulunamadı.");

                try
                {
                    var iidMgr = IidIAudioSessionManager2;
                    if (dev.Activate(ref iidMgr, ClsctxAll, 0, out var mgrObj) != 0 || mgrObj is not IAudioSessionManager2 mgr)
                        return AuroraResult.Fail("AUDIO_NO_MGR", "Oturum yöneticisi açılamadı.");

                    try
                    {
                        if (mgr.GetSessionEnumerator(out var sessEnum) != 0 || sessEnum is null)
                            return AuroraResult.Fail("AUDIO_NO_SESS", "Oturum listesi alınamadı.");

                        try
                        {
                            sessEnum.GetCount(out int count);
                            bool anyMatched = false;
                            for (int i = 0; i < count; i++)
                            {
                                if (sessEnum.GetSession(i, out var ctrl) != 0 || ctrl is null)
                                    continue;

                                try
                                {
                                    if (ctrl is IAudioSessionControl2 ctrl2 && ctrl2 is ISimpleAudioVolume simpleVol)
                                    {
                                        ctrl2.GetProcessId(out uint pid);
                                        if (pid == targetPid && mutator(simpleVol))
                                            anyMatched = true;
                                    }
                                }
                                finally
                                {
                                    Marshal.ReleaseComObject(ctrl);
                                }
                            }

                            return anyMatched
                                ? AuroraResult.Ok()
                                : AuroraResult.Fail("AUDIO_SESSION_GONE", "Uygulama ses oturumu kapandı veya bulunamadı.");
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(sessEnum);
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(mgr);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(dev);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(enumerator);
            }
        }
        catch (Exception ex)
        {
            logger.Warn("Audio", "SESSION_MUTATE_ERR", $"Session audio update failed ({ex.GetType().Name}).");
            return AuroraResult.Fail("AUDIO_SESSION_EX", "Uygulama sesi değiştirilemedi.");
        }
    }

    private static IMMDeviceEnumerator? CreateDeviceEnumerator()
    {
        var type = Type.GetTypeFromCLSID(ClsidMMDeviceEnumerator);
        return type is null ? null : Activator.CreateInstance(type) as IMMDeviceEnumerator;
    }

    private static string? ReadDeviceFriendlyName(IMMDevice device)
    {
        if (device.OpenPropertyStore(StgmRead, out var store) != 0 || store is null)
            return null;

        try
        {
            var pkeyFriendly = new PROPERTYKEY(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);
            if (store.GetValue(ref pkeyFriendly, out var prop) == 0)
            {
                try
                {
                    if (prop.vt == 31 && prop.pwszVal != 0) // VT_LPWSTR
                        return Marshal.PtrToStringUni(prop.pwszVal);
                }
                finally
                {
                    PropVariantClear(ref prop);
                }
            }

            return null;
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT pvar);

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY(Guid fmtid, uint pid)
    {
        public Guid fmtid = fmtid;
        public uint pid = pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public nint pwszVal;
        public nint pad;
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint dwStateMask, out IMMDeviceCollection? ppDevices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice? ppEndpoint);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint pcDevices);
        [PreserveSig] int Item(uint nDevice, out IMMDevice? ppDevice);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint dwClsCtx, nint pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object? ppInterface);
        [PreserveSig] int OpenPropertyStore(uint stgmAccess, out IPropertyStore? ppProperties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string? ppstrId);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint cProps);
        [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(nint pNotify);
        [PreserveSig] int UnregisterControlChangeNotify(nint pNotify);
        [PreserveSig] int GetChannelCount(out uint pnChannelCount);
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
        [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(nint AudioSessionGuid, uint StreamFlags, out nint SessionControl);
        [PreserveSig] int GetSimpleAudioVolume(nint AudioSessionGuid, uint StreamFlags, out ISimpleAudioVolume AudioVolume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator? SessionEnum);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int SessionCount);
        [PreserveSig] int GetSession(int SessionCount, [MarshalAs(UnmanagedType.IUnknown)] out object? Session);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int pRetVal);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string? pRetVal);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string? pRetVal);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        [PreserveSig] int GetGroupingParam(out Guid pRetVal);
        [PreserveSig] int SetGroupingParam(ref Guid Override, ref Guid EventContext);
        [PreserveSig] int RegisterAudioSessionNotification(nint NewNotifications);
        [PreserveSig] int UnregisterAudioSessionNotification(nint NewNotifications);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string? pRetVal);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string? pRetVal);
        [PreserveSig] int GetProcessId(out uint pRetVal);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float fLevel, ref Guid EventContext);
        [PreserveSig] int GetMasterVolume(out float pfLevel);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid EventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }

    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, nint ppFormat);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bPublic, nint ppFormat);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, nint pEndpointFormat, nint ppFormat);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, nint pmftDefaultPeriod, nint pmftMinimumPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, nint pmftPeriod);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, nint pDeviceShareMode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, nint pDeviceShareMode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, nint pKey, nint ppValue);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, nint pKey, nint ppValue);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, int eRole);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bVisible);
    }
}
