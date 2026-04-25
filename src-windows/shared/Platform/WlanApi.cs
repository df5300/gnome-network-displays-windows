using System.Net;
using System.Net.NetworkInformation;

namespace Gnd.Windows.Shared.Platform;

public static class WlanApi {
    public const uint WLAN_API_VERSION = 2;

    [DllImport("Wlanapi.dll", SetLastError = true)]
    public static extern uint WlanOpenHandle(
        uint dwClientVersion,
        IntPtr pReserved,
        out uint pdwNegotiatedVersion,
        out IntPtr phClientHandle);

    [DllImport("Wlanapi.dll", SetLastError = true)]
    public static extern uint WlanCloseHandle(
        IntPtr hClientHandle,
        IntPtr pReserved);

    [DllImport("Wlanapi.dll", SetLastError = true)]
    public static extern uint WlanGetAvailableNetworkList(
        IntPtr hClientHandle,
        ref Guid pInterfaceGuid,
        uint dwFlags,
        IntPtr pReserved,
        out IntPtr ppNetworkList);

    [DllImport("Wlanapi.dll", SetLastError = true)]
    public static extern void WlanFreeMemory(IntPtr pMemory);

    [DllImport("Wlanapi.dll", SetLastError = true)]
    public static extern uint WlanDeviceServices(
        IntPtr hClientHandle,
        uint dwVersion,
        IntPtr pReserved);
}

[StructLayout(LayoutKind.Sequential)]
public struct WLAN_AVAILABLE_NETWORK {
    [MarshalAs(UnmanagedType.LPWStr)]
    public string strNetworkName;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string strProfileName;
    public uint dwFlags;
    public uint dwSecuritySettings;
    public int nPhyTypes;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public uint[] dwPhyTypes;
    public uint dwConfigMembers;
}

[StructLayout(LayoutKind.Sequential)]
public struct WLAN_AVAILABLE_NETWORK_LIST {
    public uint dwNumberOfItems;
    public uint dwIndex;
    public WLAN_AVAILABLE_NETWORK[] availableNetwork;
}

public static class WfdApi {
    public static readonly Guid WFD_DEVICE_TYPE_GUID = new Guid("3C9B07EB-1E3F-4781-8115-8A41C6A5C2E4");

    public const int WFD_DEVICE_TYPE = 1;
    public const int WFD_SOURCE_ROLE = 0x0001;
    public const int WFD_SINK_ROLE = 0x0002;

    public enum WFD_DEVICE_TYPE {
        Source = WFD_SOURCE_ROLE,
        Sink = WFD_SINK_ROLE,
        Dual = WFD_SOURCE_ROLE | WFD_SINK_ROLE
    }
}