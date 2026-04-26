using System.Runtime.InteropServices;
using System.Text;

namespace Gnd.Windows.Shared.SystemIntegration;

/// <summary>
/// Windows Firewall management using INetFwProfile COM interface
/// </summary>
public class WindowsFirewall : IDisposable
{
    private readonly INetFwMgr? _fwMgr;
    private readonly INetFwPolicy2? _fwPolicy2;
    private bool _disposed;

    public WindowsFirewall()
    {
        try
        {
            // Try to create firewall manager
            var type = Type.GetTypeFromProgID("HNetCfg.FwMgr");
            if (type != null)
            {
                _fwMgr = (INetFwMgr?)Activator.CreateInstance(type);
            }

            // Also try the newer INetFwPolicy2 (Windows Vista+)
            var policy2Type = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            if (policy2Type != null)
            {
                _fwPolicy2 = (INetFwPolicy2?)Activator.CreateInstance(policy2Type);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize Windows Firewall: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if firewall is enabled
    /// </summary>
    public bool IsFirewallEnabled
    {
        get
        {
            if (_fwPolicy2 != null)
            {
                return _fwPolicy2.get_FirewallEnabled(NET_FW_PROFILE_TYPE2.NET_FW_PROFILE2_ALL);
            }
            if (_fwMgr?.LocalPolicy != null)
            {
                return _fwMgr.LocalPolicy.FirewallEnabled;
            }
            return false;
        }
    }

    /// <summary>
    /// Add an exception rule for the application
    /// </summary>
    public bool AddAppRule(string appPath, string appName, bool enabled = true)
    {
        if (_fwPolicy2 == null)
        {
            System.Diagnostics.Debug.WriteLine("Firewall policy interface not available");
            return false;
        }

        try
        {
            // Check if rule already exists
            foreach (INetFwRule existingRule in _fwPolicy2.Rules)
            {
                if (existingRule.Name == appName)
                {
                    existingRule.Enabled = enabled;
                    return true;
                }
            }

            // Create new rule
            var ruleType = Type.GetTypeFromProgID("HNetCfg.FWRule");
            if (ruleType == null) return false;

            var rule = (INetFwRule?)Activator.CreateInstance(ruleType);
            if (rule == null) return false;

            rule.Name = appName;
            rule.ApplicationName = appPath;
            rule.Action = (int)NET_FW_ACTION.NET_FW_ACTION_ALLOW;
            rule.Direction = (int)NET_FW_RULE_DIRECTION.NET_FW_RULE_DIR_IN;
            rule.Enabled = enabled;

            _fwPolicy2.Rules.Add(rule);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to add firewall rule: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Add a port exception
    /// </summary>
    public bool AddPortRule(int port, string protocol, string ruleName, bool enabled = true)
    {
        if (_fwPolicy2 == null) return false;

        try
        {
            // Check if rule already exists
            foreach (INetFwRule existingRule in _fwPolicy2.Rules)
            {
                if (existingRule.Name == ruleName)
                {
                    existingRule.Enabled = enabled;
                    return true;
                }
            }

            var ruleType = Type.GetTypeFromProgID("HNetCfg.FWRule");
            if (ruleType == null) return false;

            var rule = (INetFwRule?)Activator.CreateInstance(ruleType);
            if (rule == null) return false;

            rule.Name = ruleName;
            rule.Protocol = protocol == "TCP" ? 6 : 17; // TCP=6, UDP=17
            rule.LocalPorts = port.ToString();
            rule.Action = (int)NET_FW_ACTION.NET_FW_ACTION_ALLOW;
            rule.Direction = (int)NET_FW_RULE_DIRECTION.NET_FW_RULE_DIR_IN;
            rule.Enabled = enabled;

            _fwPolicy2.Rules.Add(rule);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to add port rule: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Remove a rule by name
    /// </summary>
    public bool RemoveRule(string ruleName)
    {
        if (_fwPolicy2 == null) return false;

        try
        {
            foreach (INetFwRule rule in _fwPolicy2.Rules)
            {
                if (rule.Name == ruleName)
                {
                    _fwPolicy2.Rules.Remove(ruleName);
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Release COM objects if needed
            _disposed = true;
        }
    }
}

// COM Interfaces for Windows Firewall
#region COM Interfaces

// These would normally be defined in a type library, but we define them here for P/Invoke

[ComImport, Guid("F7898AF5-CAC4-4633-8B77-18F42A45098E")]
internal class NetFwMgr { }

[ComImport, Guid("71A5506B-3273-4A89-8C93-155291B06C52")]
internal class NetFwPolicy2 { }

[ComImport, Guid("9C4F7BB5-66F8-498B-8868-31CCAC8151C4")]
internal class NetFwRule { }

[ComImport, Guid("E2B3C97F-6EB1-49B3-9315-DB4DA85D0C60"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
internal interface INetFwPolicy
{
    INetFwMgr? CurrentProfile { get; }
    bool FirewallEnabled { get; set; }
}

[ComImport, Guid("A6207B2E-7D3D-4725-9C1F-18AC4870C77E"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
internal interface INetFwMgr
{
    INetFwPolicy? LocalPolicy { get; }
    bool LocalPolicyModified { get; }
    int CurrentProfileTypes { get; set; }
}

[ComImport, Guid("83DA8326-2A9C-46A0-9446-943D4E93F04F"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
internal interface INetFwPolicy2
{
    int CurrentProfileTypes { get; }
    bool get_FirewallEnabled(NET_FW_PROFILE_TYPE2 profileType);
    void set_FirewallEnabled(NET_FW_PROFILE_TYPE2 profileType, bool value);
    INetFwRules Rules { get; }
}

[ComImport, Guid("9C4F7BB5-66F8-498B-8868-31CCAC8151C4"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
internal interface INetFwRule
{
    string Name { get; set; }
    string Description { get; set; }
    string ApplicationName { get; set; }
    string ServiceName { get; set; }
    int Protocol { get; set; }
    string LocalPorts { get; set; }
    string RemotePorts { get; set; }
    string LocalAddresses { get; set; }
    string RemoteAddresses { get; set; }
    string AuthorizedApplications { get; set; }
    int IcmpTypesAndCodes { get; set; }
    int Direction { get; set; }
    int Action { get; set; }
    bool Enabled { get; set; }
    string EdgeTraversal { get; set; }
}

[ComImport, Guid("A6207B2E-7D3D-4725-9C1F-18AC4870C77E"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
internal interface INetFwRules : System.Collections.IEnumerable
{
    int Count { get; }
    INetFwRule get_Item(object identifier);
    System.Collections.IEnumerator GetEnumerator();
    void Add(INetFwRule rule);
    void Remove(string name);
    INetFwRule CreateRule();
}

internal enum NET_FW_PROFILE_TYPE2
{
    NET_FW_PROFILE2_ALL = 0x7FFFFFFF,
}

internal enum NET_FW_ACTION
{
    NET_FW_ACTION_BLOCK = 0,
    NET_FW_ACTION_ALLOW = 1,
}

internal enum NET_FW_RULE_DIRECTION
{
    NET_FW_RULE_DIR_IN = 1,
    NET_FW_RULE_DIR_OUT = 2,
}

#endregion
