using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace Gnd.Windows.Shared.Platform;

/// <summary>
/// DNS Service Discovery (Bonjour/mDNS) P/Invoke bindings for Windows
/// Uses the Apple Bonjour SDK dnssd.dll
/// </summary>
public static class DnsSdApi
{
    // DNS-SD Error codes
    public const int kDNSServiceErr_NoError = 0;
    public const int kDNSServiceErr_Unknown = unchecked((int)0xFFFFFE00); // -65538
    public const int kDNSServiceErr_NoSuchName = unchecked((int)0xFFFFFE01); // -65537
    public const int kDNSServiceErr_NoMemory = unchecked((int)0xFFFFFE02); // -65536
    public const int kDNSServiceErr_BadParam = unchecked((int)0xFFFFFE03); // -65535
    public const int kDNSServiceErr_BadReference = unchecked((int)0xFFFFFE04); // -65534
    public const int kDNSServiceErr_BadState = unchecked((int)0xFFFFFE05); // -65533
    public const int kDNSServiceErr_BadFlags = unchecked((int)0xFFFFFE06); // -65532
    public const int kDNSServiceErr_Unsupported = unchecked((int)0xFFFFFE07); // -65531
    public const int kDNSServiceErr_NotInitialized = unchecked((int)0xFFFFFE08); // -65530
    public const int kDNSServiceErr_AlreadyRegistered = unchecked((int)0xFFFFFE09); // -65529
    public const int kDNSServiceErr_NameConflict = unchecked((int)0xFFFFFE0A); // -65528
    public const int kDNSServiceErr_Invalid = unchecked((int)0xFFFFFE0B); // -65527

    // DNS-SD Flags
    public const uint kDNSServiceFlagsBrowse = 0x00000000;
    public const uint kDNSServiceFlagsLost = 0x00000010;
    public const uint kDNSServiceFlagsMoreComing = 0x00000001;
    public const uint kDNSServiceFlagsAdd = 0x00000002;
    public const uint kDNSServiceFlagsDefault = 0x00000004;
    public const uint kDNSServiceFlagsBrowseDomains = 0x00000008;
    public const uint kDNSServiceFlagsRegisterDomains = 0x00000010;
    public const uint kDNSServiceFlagsLongLivedQuery = 0x00000020;
    public const uint kDNSServiceFlagsAllowRemoteQuery = 0x00000040;
    public const uint kDNSServiceFlagsForceMulticast = 0x00000080;
    public const uint kDNSServiceFlagsForceMCast2 = 0x00000080;
    public const uint kDNSServiceFlagsShareConnection = 0x00000100;

    // DNS-SD Service Types
    public const string kDNSServiceType_AirPlay = "_airplay._tcp";
    public const string kDNSServiceType_Chromecast = "_googlecast._tcp";
    public const string kDNSServiceType_MiraCast = "_wfd._tcp";

    // Well-known service types
    public const string kDNSServiceType_AFP = "_afpovertcp._tcp";
    public const string kDNSServiceType_FTP = "_ftp._tcp";
    public const string kDNSServiceType_HTTP = "_http._tcp";
    public const string kDNSServiceType_HTTPS = "_https._tcp";
    public const string kDNSServiceType_Printer = "_printer._tcp";
    public const string kDNSServiceType_SSH = "_ssh._tcp";
    public const string kDNSServiceType_SMB = "_smb._tcp";

    // DNS-SD Callback delegate
    public delegate void DNSServiceDiscoveryCallback(
        IntPtr sdRef,
        ref DNSServiceFlags flags,
        uint interfaceIndex,
        DNSServiceErrorType errorCode,
        string serviceName,
        string regType,
        string domain,
        IntPtr context);

    public delegate void DNSServiceResolveCallback(
        IntPtr sdRef,
        ref DNSServiceFlags flags,
        uint interfaceIndex,
        DNSServiceErrorType errorCode,
        string fullname,
        string hosttarget,
        ushort port,
        ushort txtLen,
        IntPtr txtRecord,
        IntPtr context);

    public delegate void DNSServiceGetAddrInfoCallback(
        IntPtr sdRef,
        ref DNSServiceFlags flags,
        uint interfaceIndex,
        DNSServiceErrorType errorCode,
        string hostname,
        ref sockaddr addr,
        uint ttl,
        IntPtr context);

    // P/Invoke declarations
    [DllImport("dnssd.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern DNSServiceErrorType DNSServiceBrowse(
        out IntPtr serviceRef,
        ref DNSServiceFlags flags,
        uint interfaceIndex,
        string regType,
        string domain,
        DNSServiceDiscoveryCallback callback,
        IntPtr context);

    [DllImport("dnssd.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern DNSServiceErrorType DNSServiceResolve(
        out IntPtr serviceRef,
        ref DNSServiceFlags flags,
        uint interfaceIndex,
        string serviceName,
        string regType,
        string domain,
        DNSServiceResolveCallback callback,
        IntPtr context);

    [DllImport("dnssd.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern DNSServiceErrorType DNSServiceProcessResult(IntPtr serviceRef);

    [DllImport("dnssd.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern DNSServiceErrorType DNSServiceRefDestroy(IntPtr serviceRef);

    [DllImport("dnssd.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern IntPtr DNSServiceRefSockFD(IntPtr serviceRef);

    [DllImport("dnssd.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern DNSServiceErrorType DNSServiceGetAddrInfo(
        out IntPtr serviceRef,
        ref DNSServiceFlags flags,
        uint interfaceIndex,
        DNSServiceAddressProtocol addressProtocol,
        string hostname,
        DNSServiceGetAddrInfoCallback callback,
        IntPtr context);

    [DllImport("dnssd.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern ushort DNSServicePortMakeCanonical(
        IntPtr serviceRef);

    [DllImport("dnssd.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern IntPtr DNSServiceConstructFullName(
        IntPtr fullNameBuffer,
        string serviceName,
        string regType,
        string domain);
}

#region DNS-SD Types

[StructLayout(LayoutKind.Sequential)]
public struct DNSServiceFlags
{
    private uint _value;

    public bool Add
    {
        get => (_value & DnsSdApi.kDNSServiceFlagsAdd) != 0;
        set => _value = value ? (_value | DnsSdApi.kDNSServiceFlagsAdd) : (_value & ~DnsSdApi.kDNSServiceFlagsAdd);
    }

    public bool Lost
    {
        get => (_value & DnsSdApi.kDNSServiceFlagsLost) != 0;
        set => _value = value ? (_value | DnsSdApi.kDNSServiceFlagsLost) : (_value & ~DnsSdApi.kDNSServiceFlagsLost);
    }

    public bool MoreComing
    {
        get => (_value & DnsSdApi.kDNSServiceFlagsMoreComing) != 0;
        set => _value = value ? (_value | DnsSdApi.kDNSServiceFlagsMoreComing) : (_value & ~DnsSdApi.kDNSServiceFlagsMoreComing);
    }

    public uint RawValue => _value;

    public static implicit operator uint(DNSServiceFlags flags) => flags._value;
    public static implicit operator DNSServiceFlags(uint value) => new DNSServiceFlags { _value = value };
}

public enum DNSServiceErrorType : int
{
    NoError = 0,
    Unknown = -65538,
    NoSuchName = -65537,
    NoMemory = -65536,
    BadParam = -65535,
    BadReference = -65534,
    BadState = -65533,
    BadFlags = -65532,
    Unsupported = -65531,
    NotInitialized = -65530,
    AlreadyRegistered = -65529,
    NameConflict = -65528,
    Invalid = -65527
}

public enum DNSServiceAddressProtocol
{
    IPv4 = 0,
    IPv6 = 1,
    Both = 2
}

[StructLayout(LayoutKind.Sequential)]
public struct sockaddr
{
    public ushort sa_family;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
    public byte[] sa_data;
}

#endregion

/// <summary>
/// DNS Service Discovery context for managing browse operations
/// </summary>
public class DnsSdContext : IDisposable
{
    private IntPtr _browseRef;
    private readonly Dictionary<string, ServiceInfo> _services = new();
    private bool _disposed;

    public event EventHandler<ServiceInfo>? ServiceFound;
    public event EventHandler<string>? ServiceLost;

    public DnsSdContext()
    {
    }

    /// <summary>
    /// Start browsing for services of a specific type
    /// </summary>
    public void StartBrowse(string serviceType, string domain = "local.")
    {
        var flags = new DNSServiceFlags();

        var callback = new DnsSdApi.DNSServiceDiscoveryCallback((sdRef, flags, interfaceIndex, errorCode, serviceName, regType, domain, context) =>
        {
            if (errorCode == (int)DNSServiceErrorType.NoError)
            {
                if (flags.Lost)
                {
                    lock (_services)
                    {
                        if (_services.Remove(serviceName))
                        {
                            ServiceLost?.Invoke(this, serviceName);
                        }
                    }
                }
                else if (flags.Add)
                {
                    lock (_services)
                    {
                        if (!_services.ContainsKey(serviceName))
                        {
                            var info = new ServiceInfo
                            {
                                Name = serviceName,
                                Type = regType,
                                Domain = domain,
                                InterfaceIndex = interfaceIndex
                            };
                            _services[serviceName] = info;
                            ServiceFound?.Invoke(this, info);
                        }
                    }
                }
            }
        });

        var error = DnsSdApi.DNSServiceBrowse(
            out _browseRef,
            ref flags,
            0,
            serviceType,
            domain,
            callback,
            IntPtr.Zero);

        if (error != (int)DNSServiceErrorType.NoError)
            throw new InvalidOperationException($"DNSServiceBrowse failed with error {error}");
    }

    /// <summary>
    /// Process DNS-SD events (call this in your message loop)
    /// </summary>
    public void ProcessEvents()
    {
        if (_browseRef != IntPtr.Zero)
        {
            DnsSdApi.DNSServiceProcessResult(_browseRef);
        }
    }

    /// <summary>
    /// Get all discovered services
    /// </summary>
    public IReadOnlyDictionary<string, ServiceInfo> Services
    {
        get
        {
            lock (_services)
            {
                return new Dictionary<string, ServiceInfo>(_services);
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_browseRef != IntPtr.Zero)
            {
                DnsSdApi.DNSServiceRefDestroy(_browseRef);
                _browseRef = IntPtr.Zero;
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// Service information from DNS-SD discovery
/// </summary>
public class ServiceInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public uint InterfaceIndex { get; set; }
    public string? HostName { get; set; }
    public int Port { get; set; }
    public Dictionary<string, string> TxtRecord { get; set; } = new();

    /// <summary>
    /// Get the full service name
    /// </summary>
    public string FullName => $"{Name}.{Type}.{Domain}";

    /// <summary>
    /// Check if this is a Chromecast device
    /// </summary>
    public bool IsChromecast => Type.Contains("googlecast");

    /// <summary>
    /// Check if this is an AirPlay device
    /// </summary>
    public bool IsAirPlay => Type.Contains("airplay");
}

/// <summary>
/// DNS TXT record parser
/// </summary>
public static class TxtRecordParser
{
    /// <summary>
    /// Parse a DNS TXT record from raw bytes
    /// </summary>
    public static Dictionary<string, string> Parse(byte[] txtData)
    {
        var result = new Dictionary<string, string>();
        int offset = 0;

        while (offset < txtData.Length)
        {
            int len = txtData[offset++];
            if (len == 0 || offset + len > txtData.Length)
                break;

            var entry = Encoding.UTF8.GetString(txtData, offset, len);
            var parts = entry.Split(new[] { '=' }, 2);
            if (parts.Length == 2)
            {
                result[parts[0]] = parts[1];
            }
            else
            {
                result[parts[0]] = string.Empty;
            }

            offset += len;
        }

        return result;
    }
}
