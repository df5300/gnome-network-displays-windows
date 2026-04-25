using System.Runtime.InteropServices;

namespace Gnd.Windows.Shared.Platform;

public static class DnsSdApi {
    public delegate void DNSServiceDiscoveryCallback(
        uint flags,
        uint interfaceIndex,
        uint errorCode,
        string serviceName,
        string regType,
        string domain,
        IntPtr context);

    [DllImport("dnssd.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern uint DNSServiceDiscover(
        out IntPtr serviceRef,
        uint flags,
        uint interfaceIndex,
        string serviceName,
        string regType,
        string domain,
        bool browseDomains,
        DNSServiceDiscoveryCallback callback,
        IntPtr context);

    [DllImport("dnssd.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern uint DNSServiceRefDestroy(IntPtr serviceRef);

    [DllImport("dnssd.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern uint DNSServiceProcessResult(IntPtr serviceRef);

    public const uint kDNSServiceFlagsBrowseDomains = 0x04;
    public const uint kDNSServiceFlagsRegisterDomains = 0x08;
    public const uint kDNSServiceFlagsAdd = 0x10;

    public const uint kDNSServiceErr_NoError = 0;
    public const uint kDNSServiceErr_Unknown = -65538;
}