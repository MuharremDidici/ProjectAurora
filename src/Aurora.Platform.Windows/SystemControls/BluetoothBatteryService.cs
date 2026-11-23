using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace Aurora.Platform.Windows.SystemControls;

public sealed record BluetoothBatteryDevice(
    string Id,
    string Name,
    int BatteryPercent,
    string Category,
    string Source);

public sealed record BluetoothBatteryAlert(
    string Kind, // "connected" | "lowBattery"
    BluetoothBatteryDevice Device);

/// <summary>
/// Zero-UAC, high-speed Bluetooth peripheral battery reader for Windows 11.
/// Layer 1: Native Win32 cfgmgr32.dll PnP property {104EA319-6EE2-4701-BD47-8DDBF425BBE5}, 2 (Classic HFP + cached BLE, &lt;3 ms).
/// Layer 2: WinRT BLE GATT Battery Service (0x180F / 0x2A19) fallback for peripherals not cached in PnP.
/// </summary>
public sealed partial class BluetoothBatteryService
{
    private const uint CrSuccess = 0x00000000;
    private const uint CmGetIdListFilterPresent = 0x00000100;
    private const uint CmLocateDevNodeNormal = 0x00000000;
    private const uint DevPropTypeByte = 0x00000003;
    private const uint DevPropTypeString = 0x00000012;
    private const uint DevPropTypeGuid = 0x0000000D;

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVPROPKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    // Windows 10/11 Bluetooth Battery Percentage PnP property
    private static readonly DEVPROPKEY DevpkeyBluetoothBattery = new()
    {
        fmtid = new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"),
        pid = 2
    };

    // DEVPKEY_Device_FriendlyName
    private static readonly DEVPROPKEY DevpkeyFriendlyName = new()
    {
        fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
        pid = 14
    };

    // DEVPKEY_NAME
    private static readonly DEVPROPKEY DevpkeyName = new()
    {
        fmtid = new Guid("B725F130-47EF-101A-A5F1-02608C9EEBAC"),
        pid = 10
    };

    // DEVPKEY_Device_ContainerId
    private static readonly DEVPROPKEY DevpkeyContainerId = new()
    {
        fmtid = new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"),
        pid = 2
    };

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_ID_List_SizeW(out uint pulLen, string? pszFilter, uint ulFlags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_ID_ListW(string? pszFilter, [Out] char[] buffer, uint bufferLen, uint ulFlags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Locate_DevNodeW(out uint pdnDevInst, string pDeviceId, uint ulFlags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_DevNode_PropertyW(
        uint dnDevInst,
        ref DEVPROPKEY propertyKey,
        out uint propertyType,
        byte[]? propertyBuffer,
        ref uint propertyBufferSize,
        uint ulFlags);

    private readonly object _stateLock = new();
    private bool _seeded;
    private Dictionary<string, BluetoothBatteryDevice> _known = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _lowBatteryNotifiedIds = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<BluetoothBatteryDevice>> QueryConnectedDevicesAsync(bool includeBleGattFallback = true, CancellationToken ct = default)
    {
        var byKey = new Dictionary<string, BluetoothBatteryDevice>(StringComparer.OrdinalIgnoreCase);

        // Layer 1: Native cfgmgr32.dll PnP scan (fast, zero PowerShell spawn)
        foreach (var dev in QueryViaNativePnp())
        {
            MergeDevice(byKey, dev);
        }

        // Layer 2: WinRT BLE GATT Battery Service fallback (0x180F -> 0x2A19)
        if (includeBleGattFallback && !ct.IsCancellationRequested)
        {
            try
            {
                var bleDevices = await QueryViaBleGattAsync(ct);
                foreach (var dev in bleDevices)
                {
                    // Avoid duplicating a device already discovered by name in PnP
                    bool alreadyByName = byKey.Values.Any(existing =>
                        string.Equals(existing.Name, dev.Name, StringComparison.OrdinalIgnoreCase));
                    if (!alreadyByName)
                        MergeDevice(byKey, dev);
                }
            }
            catch
            {
                // BLE radio off or permission restricted; PnP results remain valid
            }
        }

        return byKey.Values
            .OrderBy(d => d.BatteryPercent)
            .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Evaluates diffs against the previous poll to emit connection toasts &amp; &lt;=15% low-battery warnings cleanly.
    /// First poll seeds state silently so boot never spams the user.
    /// </summary>
    public IReadOnlyList<BluetoothBatteryAlert> EvaluateAlerts(IReadOnlyList<BluetoothBatteryDevice> current)
    {
        var alerts = new List<BluetoothBatteryAlert>();
        lock (_stateLock)
        {
            if (!_seeded)
            {
                _seeded = true;
                _known = current.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
                foreach (var d in current)
                {
                    if (d.BatteryPercent <= 15)
                        _lowBatteryNotifiedIds.Add(d.Id);
                }
                return alerts;
            }

            foreach (var d in current)
            {
                if (!_known.ContainsKey(d.Id))
                {
                    alerts.Add(new BluetoothBatteryAlert("connected", d));
                    if (d.BatteryPercent <= 15)
                        _lowBatteryNotifiedIds.Add(d.Id);
                }
                else
                {
                    if (d.BatteryPercent <= 15 && !_lowBatteryNotifiedIds.Contains(d.Id))
                    {
                        _lowBatteryNotifiedIds.Add(d.Id);
                        alerts.Add(new BluetoothBatteryAlert("lowBattery", d));
                    }
                    else if (d.BatteryPercent >= 25)
                    {
                        _lowBatteryNotifiedIds.Remove(d.Id);
                    }
                }
            }

            _known = current.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
        }

        return alerts;
    }

    private static List<BluetoothBatteryDevice> QueryViaNativePnp()
    {
        var result = new List<BluetoothBatteryDevice>();
        try
        {
            if (CM_Get_Device_ID_List_SizeW(out uint bufLen, null, CmGetIdListFilterPresent) != CrSuccess || bufLen < 2)
                return result;

            var buf = new char[bufLen];
            if (CM_Get_Device_ID_ListW(null, buf, bufLen, CmGetIdListFilterPresent) != CrSuccess)
                return result;

            int idx = 0;
            while (idx < buf.Length && buf[idx] != '\0')
            {
                int end = Array.IndexOf(buf, '\0', idx);
                if (end < 0) break;
                int spanLen = end - idx;
                if (spanLen > 4)
                {
                    var instanceId = new string(buf, idx, spanLen);
                    if (IsBluetoothInstanceId(instanceId))
                    {
                        if (CM_Locate_DevNodeW(out uint devInst, instanceId, CmLocateDevNodeNormal) == CrSuccess)
                        {
                            byte? batt = TryReadByteProperty(devInst, DevpkeyBluetoothBattery);
                            if (batt.HasValue && batt.Value <= 100)
                            {
                                string rawName = TryReadStringProperty(devInst, DevpkeyFriendlyName)
                                    ?? TryReadStringProperty(devInst, DevpkeyName)
                                    ?? "Bluetooth Cihazı";
                                string cleanName = NormalizeBluetoothDeviceName(rawName);
                                Guid? container = TryReadGuidProperty(devInst, DevpkeyContainerId);
                                string key = container.HasValue && container.Value != Guid.Empty
                                    ? container.Value.ToString("N")
                                    : cleanName.ToLowerInvariant();

                                result.Add(new BluetoothBatteryDevice(
                                    Id: key,
                                    Name: cleanName,
                                    BatteryPercent: batt.Value,
                                    Category: ClassifyCategory(rawName),
                                    Source: "PnP"));
                            }
                        }
                    }
                }
                idx = end + 1;
            }
        }
        catch
        {
            // Ignore transient PnP enumeration exceptions
        }

        return result;
    }

    private static async Task<List<BluetoothBatteryDevice>> QueryViaBleGattAsync(CancellationToken ct)
    {
        var list = new List<BluetoothBatteryDevice>();
        string selector = GattDeviceService.GetDeviceSelectorFromUuid(GattServiceUuids.Battery);
        var infos = await DeviceInformation.FindAllAsync(selector);
        if (infos is null || infos.Count == 0)
            return list;

        foreach (var info in infos.Take(8))
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                using var service = await GattDeviceService.FromIdAsync(info.Id);
                if (service is null) continue;

                var charsRes = await service.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel);
                if (charsRes.Status != GattCommunicationStatus.Success || charsRes.Characteristics.Count == 0)
                    continue;

                var readRes = await charsRes.Characteristics[0].ReadValueAsync(global::Windows.Devices.Bluetooth.BluetoothCacheMode.Cached);
                if (readRes.Status != GattCommunicationStatus.Success || readRes.Value is null || readRes.Value.Length == 0)
                    continue;

                using var reader = DataReader.FromBuffer(readRes.Value);
                byte level = reader.ReadByte();
                if (level > 100) continue;

                string rawName = service.Device?.Name ?? info.Name ?? "BLE Cihazı";
                string cleanName = NormalizeBluetoothDeviceName(rawName);
                string key = !string.IsNullOrWhiteSpace(service.Device?.DeviceId)
                    ? service.Device.DeviceId
                    : cleanName.ToLowerInvariant();

                list.Add(new BluetoothBatteryDevice(
                    Id: key,
                    Name: cleanName,
                    BatteryPercent: level,
                    Category: ClassifyCategory(rawName),
                    Source: "BLE"));
            }
            catch
            {
                // Individual BLE peripheral asleep or out of range
            }
        }

        return list;
    }

    private static void MergeDevice(Dictionary<string, BluetoothBatteryDevice> map, BluetoothBatteryDevice candidate)
    {
        if (!map.TryGetValue(candidate.Id, out var existing))
        {
            map[candidate.Id] = candidate;
            return;
        }

        // Prefer cleaner shorter friendly name (without profile suffixes) and non-zero battery
        string bestName = candidate.Name.Length >= 3 && candidate.Name.Length < existing.Name.Length
            ? candidate.Name
            : existing.Name;
        int bestBatt = candidate.BatteryPercent > 0 ? candidate.BatteryPercent : existing.BatteryPercent;
        string bestCategory = !string.Equals(existing.Category, "device", StringComparison.OrdinalIgnoreCase)
            ? existing.Category
            : !string.Equals(candidate.Category, "device", StringComparison.OrdinalIgnoreCase)
                ? candidate.Category
                : ClassifyCategory(bestName);
        map[candidate.Id] = existing with
        {
            Name = bestName,
            BatteryPercent = bestBatt,
            Category = bestCategory
        };
    }

    private static bool IsBluetoothInstanceId(string id) =>
        id.StartsWith("BTHENUM\\", StringComparison.OrdinalIgnoreCase) ||
        id.StartsWith("BTHLE\\", StringComparison.OrdinalIgnoreCase) ||
        id.StartsWith("BTHLEDEVICE\\", StringComparison.OrdinalIgnoreCase);

    private static byte? TryReadByteProperty(uint devInst, DEVPROPKEY key)
    {
        uint size = 0;
        var k = key;
        CM_Get_DevNode_PropertyW(devInst, ref k, out uint propType, null, ref size, 0);
        if (size == 0 || size > 16 || propType != DevPropTypeByte)
            return null;

        var buf = new byte[size];
        if (CM_Get_DevNode_PropertyW(devInst, ref k, out propType, buf, ref size, 0) == CrSuccess &&
            propType == DevPropTypeByte && size >= 1)
        {
            return buf[0];
        }
        return null;
    }

    private static string? TryReadStringProperty(uint devInst, DEVPROPKEY key)
    {
        uint size = 0;
        var k = key;
        CM_Get_DevNode_PropertyW(devInst, ref k, out uint propType, null, ref size, 0);
        if (size < 2 || size > 1024 || propType != DevPropTypeString)
            return null;

        var buf = new byte[size];
        if (CM_Get_DevNode_PropertyW(devInst, ref k, out propType, buf, ref size, 0) == CrSuccess &&
            propType == DevPropTypeString)
        {
            return System.Text.Encoding.Unicode.GetString(buf).TrimEnd('\0').Trim();
        }
        return null;
    }

    private static Guid? TryReadGuidProperty(uint devInst, DEVPROPKEY key)
    {
        uint size = 16;
        var buf = new byte[16];
        var k = key;
        if (CM_Get_DevNode_PropertyW(devInst, ref k, out uint propType, buf, ref size, 0) == CrSuccess &&
            propType == DevPropTypeGuid && size == 16)
        {
            return new Guid(buf);
        }
        return null;
    }

    public static string NormalizeBluetoothDeviceName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Bluetooth Cihazı";

        string s = SuffixRegex().Replace(raw.Trim(), string.Empty).Trim(' ', '-', '—', '(', ')');
        if (string.IsNullOrWhiteSpace(s))
            s = raw.Trim();

        return s.Length > 26 ? s[..25] + "…" : s;
    }

    public static string ClassifyCategory(string name)
    {
        string n = (name ?? string.Empty).ToLowerInvariant();
        if (n.Contains("airpod") || n.Contains("buds") || n.Contains("earbud") || n.Contains("freebud") || n.Contains("wf-"))
            return "earbuds";
        if (n.Contains("headset") || n.Contains("headphone") || n.Contains("kulak") || n.Contains("wh-") || n.Contains("jbl") || n.Contains("bose") || n.Contains("beats") || n.Contains("cloud") || n.Contains("arctis") || n.Contains("kraken") || n.Contains("g435") || n.Contains("g733") || n.Contains("g535") || n.Contains("barracuda") || n.Contains("blackshark") || n.Contains("hs80") || n.Contains("virtuoso"))
            return "headset";
        if (n.Contains("mouse") || n.Contains("fare") || n.Contains("pebble") || n.Contains("mx master") || n.Contains("mx anywhere") || n.Contains("mx vertical") || n.Contains("superlight") || n.Contains("viper") || n.Contains("deathadder") || n.Contains("basilisk") || n.Contains("aerox") || n.Contains("g305") || n.Contains("g502") || n.Contains("m720") || n.Contains("m650"))
            return "mouse";
        if (n.Contains("keyboard") || n.Contains("klavye") || n.Contains("keychron") || n.Contains("mx keys") || n.Contains("k380") || n.Contains("g915"))
            return "keyboard";
        if (n.Contains("xbox") || n.Contains("dualsense") || n.Contains("dualshock") || n.Contains("wireless controller") || n.Contains("gamepad"))
            return "gamepad";
        if (n.Contains("speaker") || n.Contains("hoparlör") || n.Contains("soundbar") || n.Contains("flip") || n.Contains("charge"))
            return "speaker";
        return "device";
    }

    [GeneratedRegex(@"\s*(Hands-Free\s*AG\s*Audio|Hands-Free\s*HF\s*Audio|Hands-Free|Avrcp\s*Transport|Stereo|Battery\s*Service|LE_)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex SuffixRegex();
}
