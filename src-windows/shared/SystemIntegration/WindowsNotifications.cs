using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Gnd.Windows.Shared.SystemIntegration;

/// <summary>
/// Windows Toast Notifications management
/// </summary>
public class WindowsNotifications : IDisposable
{
    private const string APP_ID = "gnome-network-displays";
    private bool _disposed;

    public WindowsNotifications()
    {
        // Register the app for toast notifications
        // In production, this would use the app's AUMID
    }

    /// <summary>
    /// Show a simple toast notification
    /// </summary>
    public void ShowToast(string title, string message, NotificationSeverity severity = NotificationSeverity.Info)
    {
        try
        {
            var toastXml = ToastNotificationManager.GetTemplateContent(GetTemplateType(severity));

            var textNodes = toastXml.GetElementsByTagName("text");
            if (textNodes.Length > 0)
                textNodes[0].InnerText = title;
            if (textNodes.Length > 1)
                textNodes[1].InnerText = message;

            var toast = new ToastNotification(toastXml);

            // Set expiration time (optional)
            toast.ExpirationTime = DateTimeOffset.Now.AddMinutes(5);

            ToastNotificationManager.CreateToastNotifier(APP_ID).Show(toast);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to show toast: {ex.Message}");
        }
    }

    /// <summary>
    /// Show a progress toast
    /// </summary>
    public void ShowProgressToast(string title, string status, double progress)
    {
        try
        {
            var toastXml = ToastNotificationManager.GetTemplateContent(
                ToastTemplateType.ToastText02);

            var textNodes = toastXml.GetElementsByTagName("text");
            if (textNodes.Length > 0)
                textNodes[0].InnerText = title;
            if (textNodes.Length > 1)
                textNodes[1].InnerText = $"{status}: {progress:F0}%";

            var toast = new ToastNotification(toastXml);
            ToastNotificationManager.CreateToastNotifier(APP_ID).Show(toast);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to show progress toast: {ex.Message}");
        }
    }

    /// <summary>
    /// Show an error notification
    /// </summary>
    public void ShowError(string title, string message)
    {
        ShowToast(title, message, NotificationSeverity.Error);
    }

    /// <summary>
    /// Show a success notification
    /// </summary>
    public void ShowSuccess(string title, string message)
    {
        ShowToast(title, message, NotificationSeverity.Success);
    }

    /// <summary>
    /// Clear all notifications for this app
    /// </summary>
    public void ClearAll()
    {
        try
        {
            ToastNotificationManager.History.Clear(APP_ID);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to clear notifications: {ex.Message}");
        }
    }

    private static ToastTemplateType GetTemplateType(NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Error => ToastTemplateType.ToastText02,
            NotificationSeverity.Warning => ToastTemplateType.ToastText02,
            NotificationSeverity.Success => ToastTemplateType.ToastText02,
            _ => ToastTemplateType.ToastText02
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

public enum NotificationSeverity
{
    Info,
    Warning,
    Error,
    Success
}
