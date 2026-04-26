using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Gnd.Windows.Client.Services;
using Gnd.Windows.Client.ViewModels;

namespace Gnd.Windows.Client;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var serviceClient = new GrpcServiceClient();
            var viewModel = new MainViewModel(serviceClient);
            var mainWindow = new MainWindow
            {
                ViewModel = viewModel
            };
            desktop.MainWindow = mainWindow;
            mainWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }
}