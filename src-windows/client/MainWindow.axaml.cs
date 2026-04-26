using Avalonia.Controls;
using Gnd.Windows.Client.ViewModels;

namespace Gnd.Windows.Client;

public partial class MainWindow : Window
{
    public MainViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly StyledProperty<MainViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<MainWindow, MainViewModel?>(nameof(ViewModel));

    public MainWindow()
    {
        InitializeComponent();
    }
}