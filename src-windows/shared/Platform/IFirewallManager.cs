namespace Gnd.Windows.Shared.Platform;

public interface IFirewallManager
{
    Task<bool> AddRuleAsync(string name, int port, string protocol);
    Task<bool> RemoveRuleAsync(string name);
    Task<bool> IsEnabledAsync();
}