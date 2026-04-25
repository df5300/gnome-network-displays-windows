using Microsoft.Windows.AppLifecycle;
using System;
using System.Threading;
using Grpc.Net.Client;
using Gnd.Windows.Client.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Gnd.Windows.Client;

public partial class App : Application
{
    private GrpcChannel? _grpcChannel;
    private IServiceClient? _serviceClient;

    public new static App Current => (App)Application.Current;
    public DispatcherQueue? DispatcherQueue => MainWindow?.DispatcherQueue;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        InitializeGrpcChannel();

        var serviceClient = new GrpcServiceClient(_grpcChannel!);
        var viewModel = new ViewModels.MainViewModel(serviceClient);

        var mainWindow = new MainWindow();
        mainWindow.ViewModel = viewModel;
        mainWindow.DeviceList.DataContext = viewModel;
        mainWindow.Activate();
    }

    private void InitializeGrpcChannel()
    {
        // Connect to the Windows Service via gRPC
        // The service listens on a named pipe or localhost port
        var handler = new SocketsHttpHandler
        {
            PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
            KeepAlivePingDelay = TimeSpan.FromSeconds(60),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
        };

        // Use localhost with a specific port for IPC
        // In production, this would use named pipes or a more secure channel
        _grpcChannel = GrpcChannel.ForAddress("http://localhost:50051", new GrpcChannelOptions
        {
            HttpHandler = handler,
        });
    }
}