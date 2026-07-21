using System.Drawing;
using System.Runtime.Versioning;
using Seamline.Core;
using Windows.Win32;
using Windows.Win32.Devices.Display;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Seamline.Win32;

[SupportedOSPlatform("windows10.0.14393")]
public sealed unsafe class Win32DisplayBackend : IDisplayBackend
{
    // Runaway guard for the enumeration loops, faithful to the original.
    private const uint MaxEnumIterations = 1000;

    public IReadOnlyList<MonitorInfo> EnumerateMonitors()
    {
        var friendlyNames = QueryFriendlyMonitorNames();
        var monitors = new List<MonitorInfo>();

        for (uint adapterIndex = 0; adapterIndex < MaxEnumIterations; adapterIndex++)
        {
            DISPLAY_DEVICEW adapter = new() { cb = (uint)sizeof(DISPLAY_DEVICEW) };
            // FALSE return is the normal end of enumeration, not an error.
            if (!PInvoke.EnumDisplayDevices(null, adapterIndex, ref adapter, 0))
                break;

            // Mirroring pseudo-adapters are filtered on the adapter's flags.
            if ((adapter.StateFlags & DISPLAY_DEVICE_STATE_FLAGS.DISPLAY_DEVICE_MIRRORING_DRIVER) != 0)
                continue;

            string adapterName = adapter.DeviceName.ToString();

            for (uint monitorIndex = 0; monitorIndex < MaxEnumIterations; monitorIndex++)
            {
                DISPLAY_DEVICEW monitor = new() { cb = (uint)sizeof(DISPLAY_DEVICEW) };
                // EDD_GET_DEVICE_INTERFACE_NAME makes monitor.DeviceID the device-interface
                // path — the join key against QueryDisplayConfig's monitorDevicePath.
                if (!PInvoke.EnumDisplayDevices(adapterName, monitorIndex, ref monitor,
                        PInvoke.EDD_GET_DEVICE_INTERFACE_NAME))
                    break;

                if ((monitor.StateFlags & DISPLAY_DEVICE_STATE_FLAGS.DISPLAY_DEVICE_ACTIVE) == 0)
                    continue;

                // A device without a readable registry mode (ghost adapter, indirect
                // display) skips this adapter's remaining monitors but never aborts
                // discovery of the others — same degradation as the original.
                if (!TryReadRegistryDevMode(adapterName, out DEVMODEW dm))
                    break;
                friendlyNames.TryGetValue(monitor.DeviceID.ToString(), out string? friendlyName);

                monitors.Add(new MonitorInfo
                {
                    DeviceName = adapterName,
                    MonitorName = monitor.DeviceString.ToString(),
                    FriendlyMonitorName = friendlyName ?? "",
                    DisplayAdapter = adapter.DeviceString.ToString(),
                    ScreenWidth = (int)dm.dmPelsWidth,
                    ScreenHeight = (int)dm.dmPelsHeight,
                    PositionX = dm.Anonymous1.Anonymous2.dmPosition.x,
                    PositionY = dm.Anonymous1.Anonymous2.dmPosition.y,
                    Orientation = (MonitorOrientation)(uint)dm.Anonymous1.Anonymous2.dmDisplayOrientation,
                    IsPrimary = (adapter.StateFlags & DISPLAY_DEVICE_STATE_FLAGS.DISPLAY_DEVICE_PRIMARY_DEVICE) != 0,
                });

                // One entry per adapter: DeviceName is the repositioning identity, and
                // in GDI clone mode additional targets share the same source mode anyway.
                break;
            }
        }

        return monitors;
    }

    public Rectangle GetVirtualScreenBounds() => new(
        PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN),
        PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN),
        PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN),
        PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN));

    public DisplayChangeResult StagePosition(string deviceName, int x, int y)
    {
        DEVMODEW dm = ReadRegistryDevMode(deviceName);
        dm.Anonymous1.Anonymous2.dmPosition.x = x;
        dm.Anonymous1.Anonymous2.dmPosition.y = y;
        // Assignment, not OR: only the position is declared valid. OR-ing would
        // re-apply whatever stale mode fields the registry DEVMODE carries.
        dm.dmFields = DEVMODE_FIELD_FLAGS.DM_POSITION;

        fixed (char* deviceNamePtr = deviceName)
        {
            DISP_CHANGE result = PInvoke.ChangeDisplaySettingsEx(deviceNamePtr, &dm, HWND.Null,
                CDS_TYPE.CDS_UPDATEREGISTRY | CDS_TYPE.CDS_GLOBAL | CDS_TYPE.CDS_NORESET, null);
            return Map(result);
        }
    }

    public DisplayChangeResult CommitStaged()
        => Map(PInvoke.ChangeDisplaySettingsEx(null, null, HWND.Null, 0, null));

    // Registry settings on both the read and the write path, matching the original.
    // Known sharp edge: if another tool changed the live mode without a registry
    // write, this sees stale geometry.
    private static bool TryReadRegistryDevMode(string deviceName, out DEVMODEW dm)
    {
        dm = new DEVMODEW { dmSize = (ushort)sizeof(DEVMODEW) };
        return PInvoke.EnumDisplaySettings(deviceName, ENUM_DISPLAY_SETTINGS_MODE.ENUM_REGISTRY_SETTINGS, ref dm);
    }

    private static DEVMODEW ReadRegistryDevMode(string deviceName)
        => TryReadRegistryDevMode(deviceName, out DEVMODEW dm)
            ? dm
            // Failing loudly is correct on the repositioning path.
            : throw new InvalidOperationException($"EnumDisplaySettings failed for '{deviceName}'.");

    /// <summary>Maps device-interface path → human-readable monitor name. Best-effort:
    /// failures degrade to an empty dictionary, never break enumeration.</summary>
    private static Dictionary<string, string> QueryFriendlyMonitorNames()
    {
        // Device-interface paths are case-insensitive identifiers and their casing is
        // not guaranteed identical between the two producing APIs.
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Path/mode counts can grow between the size query and the config query;
        // retry a few times on ERROR_INSUFFICIENT_BUFFER.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (PInvoke.GetDisplayConfigBufferSizes(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS,
                    out uint pathCount, out uint modeCount) != WIN32_ERROR.ERROR_SUCCESS)
                return result;

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            WIN32_ERROR error;
            fixed (DISPLAYCONFIG_PATH_INFO* pathArray = paths)
            fixed (DISPLAYCONFIG_MODE_INFO* modeArray = modes)
            {
                error = PInvoke.QueryDisplayConfig(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS,
                    &pathCount, pathArray, &modeCount, modeArray, null);
            }

            if (error == WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER)
                continue;
            if (error != WIN32_ERROR.ERROR_SUCCESS)
                return result;

            for (int i = 0; i < pathCount; i++)
            {
                DISPLAYCONFIG_TARGET_DEVICE_NAME target = default;
                target.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                target.header.size = (uint)sizeof(DISPLAYCONFIG_TARGET_DEVICE_NAME);
                target.header.adapterId = paths[i].sourceInfo.adapterId;
                target.header.id = paths[i].targetInfo.id;

                if (PInvoke.DisplayConfigGetDeviceInfo(&target.header) == 0)
                    result[target.monitorDevicePath.ToString()] = target.monitorFriendlyDeviceName.ToString();
            }
            return result;
        }

        return result;
    }

    private static DisplayChangeResult Map(DISP_CHANGE code)
        => new((int)code, code.ToString());
}
