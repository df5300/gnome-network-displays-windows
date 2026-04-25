using System.Net;
using System.Runtime.InteropServices;

namespace Gnd.Windows.Shared.Platform;

/// <summary>
/// Windows Wi-Fi API (Wlanapi.h) P/Invoke bindings
/// </summary>
public static class WlanApi
{
    public const uint WLAN_API_VERSION = 2;
    public const uint WLAN_API_VERSION_2 = 2;

    // WlanOpenHandle dwFlags
    public const uint WLAN_FLAG_NO_AUTO_CONN = 0x00000001;
    public const uint WLAN_FLAG_NO_Old_MEDIA_DOT11_STATE_CHANGE = 0x00000002;

    // WlanGetAvailableNetworkList dwFlags
    public const uint WLAN_AVAILABLE_NETWORK_INCLUDE_ALL_ADHOC_PROFILES = 0x00000001;
    public const uint WLAN_AVAILABLE_NETWORK_INCLUDE_ALL_MANUAL_CONNECT_SCOPE = 0x00000002;

    // Dot11_phy_type values
    public const uint DOT11_PHY_TYPE_UNKNOWN = 0;
    public const uint DOT11_PHY_TYPE_80211A = 1;
    public const uint DOT11_PHY_TYPE_80211B = 2;
    public const uint DOT11_PHY_TYPE_80211G = 3;
    public const uint DOT11_PHY_TYPE_80211N = 4;
    public const uint DOT11_PHY_TYPE_80211AC = 5;
    public const uint DOT11_PHY_TYPE_80211AD = 6;
    public const uint DOT11_PHY_TYPE_80211AH = 7;

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
    public static extern uint WlanEnumInterfaces(
        IntPtr hClientHandle,
        IntPtr pReserved,
        out IntPtr ppInterfaceList);

    [DllImport("Wlanapi.dll", SetLastError = true)]
    public static extern uint WlanGetAvailableNetworkList(
        IntPtr hClientHandle,
        ref Guid pInterfaceGuid,
        uint dwFlags,
        IntPtr pReserved,
        out IntPtr ppNetworkList);

    [DllImport("Wlanapi.dll", SetLastError = true)]
    public static extern uint WlanGetAvailableNetworkList(
        IntPtr hClientHandle,
        IntPtr pInterfaceGuid,
        uint dwFlags,
        IntPtr pReserved,
        out IntPtr ppNetworkList);

    [DllImport("Wlanapi.dll", SetLastError = true)]
    public static extern uint WlanScan(
        IntPtr hClientHandle,
        ref Guid pInterfaceGuid,
        IntPtr ssid,
        IntPtr bssid,
        IntPtr pReserved);

    [DllImport("Wlanapi.dll", SetLastError = true)]
    public static extern void WlanFreeMemory(IntPtr pMemory);

    [DllImport("Wlanapi.dll", SetLastError = true)]
    public static extern uint WlanLookupPostfix(
        IntPtr hClientHandle,
        ref Guid pInterfaceGuid,
        IntPtr pReserved,
        out IntPtr ppPostfix,
        out uint pdwPostfixLength);

    [DllImport("Wlanapi.dll", SetLastError = true)]
    public static extern uint WlanReasonCodeToString(
        IntPtr hClientHandle,
        uint dwReasonCode,
        uint dwBufferSize,
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder stringBuffer,
        IntPtr pReserved);

    [DllImport("Wlanapi.dll", SetLastError = true)]
    public static extern uint WlanRegisterNotification(
        IntPtr hClientHandle,
        uint dwNotifSource,
        bool bIgnoreDuplicate,
        WLAN_NOTIFICATION_CALLBACK callback,
        IntPtr callbackContext,
        IntPtr reserved,
        out uint pdwPrevNotifSource);
}

/// <summary>
/// Callback delegate for WLAN notifications
/// </summary>
public delegate void WLAN_NOTIFICATION_CALLBACK(
    ref WLAN_NOTIFICATION_DATA notificationData,
    IntPtr context);

[StructLayout(LayoutKind.Sequential)]
public struct WLAN_NOTIFICATION_DATA
{
    public uint NotificationSource;
    public uint NotificationCode;
    public Guid InterfaceGuid;
    public IntPtr DataPtr;
    public uint DataSize;
}

/// <summary>
/// WLAN interface information
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct WLAN_INTERFACE_INFO
{
    public Guid InterfaceGuid;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string strInterfaceDescription;
    public WLAN_INTERFACE_STATE isState;
}

public enum WLAN_INTERFACE_STATE
{
    WLAN_INTERFACE_STATE_INVALID = 0,
    WLAN_INTERFACE_STATE_DISCONNECTED = 1,
    WLAN_INTERFACE_STATE_CONNECTING = 2,
    WLAN_INTERFACE_STATE_CONNECTED = 3,
    WLAN_INTERFACE_STATE_DISCONNECTING = 4,
    WLAN_INTERFACE_STATE_AD_HOC_NETWORK_FORMED = 5,
    WLAN_INTERFACE_STATE_MEDIA_DISCONNECTED = 6,
    WLAN_INTERFACE_STATE_MEDIA_CONNECTING = 7,
    WLAN_INTERFACE_STATE_MEDIA_CONNECTED = 8,
    WLAN_INTERFACE_STATE_INVALID_STATE = 9
}

[StructLayout(LayoutKind.Sequential)]
public struct WLAN_INTERFACE_INFO_LIST
{
    public uint dwNumberOfItems;
    public uint dwIndex;
    public IntPtr interfaceInfo; // WLAN_INTERFACE_INFO[]
}

/// <summary>
/// Available network information
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct WLAN_AVAILABLE_NETWORK
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string strNetworkName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
    public string strProfileName;
    public WLAN_AVAILABLE_NETWORK_FLAGS dwFlags;
    public WLAN_AVAILABLE_NETWORK_CONNECTION_MODE dwFlags2;
    public uint dwSignalQuality;
    public bool bNetworkConnectable;
    public uint wlanNotConnectableReason;
    public uint dwNumberOfBssids;
    public bool bSecurityEnabled;
    public DOT11_AUTH_CIPHER_PAIR AuthenticationAndCipher;
    public uint dwChannelWidth;
    public bool bResponded;
}

[Flags]
public enum WLAN_AVAILABLE_NETWORK_FLAGS
{
    WLAN_AVAILABLE_NETWORK_FLAG_CONNECTED = 0x00000001,
    WLAN_AVAILABLE_NETWORK_FLAG_HAS_PROFILE = 0x00000002,
    WLAN_AVAILABLE_NETWORK_FLAG_INTERWORKING_CAPABLE = 0x00000004,
    WLAN_AVAILABLE_NETWORK_FLAG_HOTSPOT2_CAPABLE = 0x00000008,
    WLAN_AVAILABLE_NETWORK_FLAG_HOTSPOT2_DOT1X_SECURITY = 0x00000010,
    WLAN_AVAILABLE_NETWORK_FLAG_AUTOCONF = 0x00000020,
    WLAN_AVAILABLE_NETWORK_FLAG_AUTOCONF_STARTED = 0x00000040,
    WLAN_AVAILABLE_NETWORK_FLAG_HIDDEN = 0x00000080,
    WLAN_AVAILABLE_NETWORK_FLAG_IMMERSIVE_WIFI = 0x00000100,
}

public enum WLAN_AVAILABLE_NETWORK_CONNECTION_MODE
{
    WLAN_CONNECTION_MODE_DYNAMIC = 0,
    WLAN_CONNECTION_MODE_MANUAL = 1,
    WLAN_CONNECTION_MODE_DEDICATED = 2,
    WLAN_CONNECTION_MODE_AUTO = 3,
    WLAN_CONNECTION_MODE_INVALID = 4
}

[StructLayout(LayoutKind.Sequential)]
public struct DOT11_AUTH_CIPHER_PAIR
{
    public DOT11_AUTH_ALGORITHM AuthAlgo;
    public DOT11_CIPHER_ALGORITHM CipherAlgo;
}

public enum DOT11_AUTH_ALGORITHM
{
    DOT11_AUTH_ALGO_80211_OPEN = 1,
    DOT11_AUTH_ALGO_80211_SHARED_KEY = 2,
    DOT11_AUTH_ALGO_WPA = 3,
    DOT11_AUTH_ALGO_WPA_PSK = 4,
    DOT11_AUTH_ALGO_WPA_NONE = 5,
    DOT11_AUTH_ALGO_RSNA = 6,
    DOT11_AUTH_ALGO_RSNA_PSK = 7,
    DOT11_AUTH_ALGO_WPA3 = 8,
    DOT11_AUTH_ALGO_WPA3_PSK = 9,
}

public enum DOT11_CIPHER_ALGORITHM
{
    DOT11_CIPHER_ALGO_NONE = 0x00,
    DOT11_CIPHER_ALGO_WEP40 = 0x01,
    DOT11_CIPHER_ALGO_TKIP = 0x02,
    DOT11_CIPHER_ALGO_CCMP = 0x04,
    DOT11_CIPHER_ALGO_WEP104 = 0x05,
    DOT11_CIPHER_ALGO_BIP = 0x06,
    DOT11_CIPHER_ALGO_GCMP = 0x08,
}

/// <summary>
/// WFD (Wi-Fi Direct) API - Note: Wi-Fi Direct uses the same WlanApi
/// but with additional structures and functions
/// </summary>
public static class WfdApi
{
    // WFD Capability Info bits
    public const int WFD_CAPABILITY_DEVICE_TYPE = 0x0001;
    public const int WFD_CAPABILITY_GC_MANUAL_CONNECTION = 0x0002;
    public const int WFD_CAPABILITY_SERVICE_DISCOVERY = 0x0004;
    public const int WFD_CAPABILITY_PERSISTENT_PSK = 0x0008;
    public const int WFD_CAPABILITY_SESSION MANAGEMENT = 0x0010;
    public const int WFD_CAPABILITY_INVITATION_PROCEDURE = 0x0020;

    // WFD Device Type
    public const int WFD_DEVICE_TYPE_SOURCE = 0x0001;
    public const int WFD_DEVICE_TYPE_PRIMARY_SINK = 0x0002;
    public const int WFD_DEVICE_TYPE_SECONDARY_SINK = 0x0004;
    public const int WFD_DEVICE_TYPE_SOURCE_OR_SINK = 0x0008;

    // WFD Session State
    public const int WFD_SESSION_STATE_NOT_AVAILABLE = 0x0000;
    public const int WFD_SESSION_STATE_AVAILABLE = 0x0001;
    public const int WFD_SESSION_STATE_FORMING = 0x0002;
    public const int WFD_SESSION_STATE_FORMED = 0x0004;
    public const int WFD_SESSION_STATE_CONNECTED = 0x0008;
    public const int WFD_SESSION_STATE_DISCONNECTING = 0x0010;

    // WFD Group Owner Capabilities
    public const int WFD_GROUP_OWNER_CAPABILITY_INTENT = 0x01;
    public const int WFD_GROUP_OWNER_CAPABILITY_MANAGED_DEVICE = 0x02;
    public const int WFD_GROUP_OWNER_CAPABILITY_CAPABLE = 0x04;
    public const int WFD_GROUP_OWNER_CAPABILITY_PERSISTENT_GROUP = 0x08;
    public const int WFD_GROUP_OWNER_CAPABILITY_MAX_STREAMS = 0x10;
    public const int WFD_GROUP_OWNER_CAPABILITY_EDGE_MANAGEMENT = 0x20;
    public const int WFD_GROUP_OWNER_CAPABILITY_WFD_SERVICE_DISCOVERY = 0x40;
    public const int WFD_GROUP_OWNER_CAPABILITY_TDLS_PERSISTENT_GROUP = 0x80;
    public const int WFD_GROUP_OWNER_CAPABILITY_TDLS_PERSISTENT_RECONNECT = 0x100;
    public const int WFD_GROUP_OWNER_CAPABILITY_SESSION_AVAILABLE = 0x200;

    /// <summary>
    /// WFD Device Information
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WFD_DEVICE_INFO
    {
        public ushort Category;
        public ushort OUI;
        public ushort OUI_Subtype;
        public uint DeviceCapability;
        public ushort PrimaryDeviceType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] SecondaryDeviceTypes;
        public ushort WFDDeviceType;
        public ushort SessionManagementCapability;
        public ushort DeviceMaximumLatency;
        public ushort AvailableProtectedSetupMethods;
        public ushort SimpleConfigurationMethods;
        public ushort SupportedCountryStrings;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] SessionID;
        public uint GroupCapability;
    }

    /// <summary>
    /// Parse WFD device capability flags
    /// </summary>
    public static bool HasCapability(WFD_DEVICE_INFO device, int capability) =>
        (device.DeviceCapability & capability) != 0;

    /// <summary>
    /// Check if device is a WFD Source
    /// </summary>
    public static bool IsSource(WFD_DEVICE_INFO device) =>
        (device.WFDDeviceType & WFD_DEVICE_TYPE_SOURCE) != 0;

    /// <summary>
    /// Check if device is a WFD Sink
    /// </summary>
    public static bool IsSink(WFD_DEVICE_INFO device) =>
        (device.WFDDeviceType & (WFD_DEVICE_TYPE_PRIMARY_SINK | WFD_DEVICE_TYPE_SECONDARY_SINK)) != 0;
}

/// <summary>
/// Helper class for Wi-Fi Direct operations
/// </summary>
public class WiFiDirectHelper : IDisposable
{
    private IntPtr _clientHandle;
    private bool _disposed;

    public WiFiDirectHelper()
    {
        var result = WlanApi.WlanOpenHandle(
            WlanApi.WLAN_API_VERSION,
            IntPtr.Zero,
            out uint negotiatedVersion,
            out _clientHandle);

        if (result != 0)
            throw new InvalidOperationException($"WlanOpenHandle failed with error {result}");
    }

    /// <summary>
    /// Get list of Wi-Fi interfaces
    /// </summary>
    public List<WLAN_INTERFACE_INFO> GetInterfaces()
    {
        var result = WlanApi.WlanEnumInterfaces(_clientHandle, IntPtr.Zero, out IntPtr interfaceListPtr);
        if (result != 0)
            throw new InvalidOperationException($"WlanEnumInterfaces failed with error {result}");

        try
        {
            var interfaceList = Marshal.PtrToStructure<WLAN_INTERFACE_INFO_LIST>(interfaceListPtr);
            var interfaces = new List<WLAN_INTERFACE_INFO>();

            IntPtr currentPtr = interfaceList.interfaceInfo;
            for (int i = 0; i < (int)interfaceList.dwNumberOfItems; i++)
            {
                var interfaceInfo = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(currentPtr);
                interfaces.Add(interfaceInfo);
                currentPtr = IntPtr.Add(currentPtr, Marshal.SizeOf<WLAN_INTERFACE_INFO>());
            }

            return interfaces;
        }
        finally
        {
            WlanApi.WlanFreeMemory(interfaceListPtr);
        }
    }

    /// <summary>
    /// Scan for available networks
    /// </summary>
    public void Scan(Guid interfaceGuid)
    {
        var result = WlanApi.WlanScan(_clientHandle, ref interfaceGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (result != 0)
            throw new InvalidOperationException($"WlanScan failed with error {result}");
    }

    /// <summary>
    /// Get available networks on an interface
    /// </summary>
    public List<WLAN_AVAILABLE_NETWORK> GetAvailableNetworks(Guid interfaceGuid)
    {
        var result = WlanApi.WlanGetAvailableNetworkList(
            _clientHandle,
            ref interfaceGuid,
            WlanApi.WLAN_AVAILABLE_NETWORK_INCLUDE_ALL_ADHOC_PROFILES,
            IntPtr.Zero,
            out IntPtr networkListPtr);

        if (result != 0)
            throw new InvalidOperationException($"WlanGetAvailableNetworkList failed with error {result}");

        try
        {
            var networkList = Marshal.PtrToStructure<WLAN_AVAILABLE_NETWORK_LIST>(networkListPtr);
            var networks = new List<WLAN_AVAILABLE_NETWORK>();

            IntPtr currentPtr = networkListPtr + Marshal.SizeOf<uint>() * 2;
            for (int i = 0; i < (int)networkList.dwNumberOfItems; i++)
            {
                var network = Marshal.PtrToStructure<WLAN_AVAILABLE_NETWORK>(currentPtr);
                networks.Add(network);
                currentPtr = IntPtr.Add(currentPtr, Marshal.SizeOf<WLAN_AVAILABLE_NETWORK>());
            }

            return networks;
        }
        finally
        {
            WlanApi.WlanFreeMemory(networkListPtr);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_clientHandle != IntPtr.Zero)
            {
                WlanApi.WlanCloseHandle(_clientHandle, IntPtr.Zero);
                _clientHandle = IntPtr.Zero;
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// WLAN available network list (unmanaged structure)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct WLAN_AVAILABLE_NETWORK_LIST
{
    public uint dwNumberOfItems;
    public uint dwIndex;
}
