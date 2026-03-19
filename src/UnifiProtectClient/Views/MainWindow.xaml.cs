using H.NotifyIcon;
using Microsoft.Extensions.Configuration;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using UnifiProtectClient.ViewModels;
using Windows.Graphics;
using Microsoft.UI.Xaml;

namespace UnifiProtectClient.Views;

public sealed partial class MainWindow
{
    private const int WindowWidth = 565;
    private const int WindowHeight = 755;

    public MainViewModel ViewModel { get; }

    public MainWindow(IConfiguration configuration)
    {
        InitializeComponent();
        ResizeAndPosition();

        ViewModel = new MainViewModel(this, configuration, DispatcherQueue.GetForCurrentThread());
        RootGrid.DataContext = ViewModel;

        VisibilityChanged += OnVisibilityChanged;
        Closed += OnWindowClosed;
    }

    private void OnVisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        if (!args.Visible)
            ViewModel.StopStream();
        else
            ViewModel.StartStream();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        args.Handled = true;
        this.Hide();
    }

    private void ResizeAndPosition()
    {
        var appWindow = AppWindow.GetFromWindowId(AppWindow.Id);
        var displayArea = DisplayArea.Primary;
        var x = (displayArea.OuterBounds.Width - WindowWidth) / 2;
        var y = (displayArea.OuterBounds.Height - WindowHeight) / 2;
        appWindow.MoveAndResize(new RectInt32(x, y, WindowWidth, WindowHeight));
        appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        appWindow.TitleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;
        appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        appWindow.TitleBar.ButtonForegroundColor = Colors.White;
        appWindow.TitleBar.ButtonInactiveForegroundColor = Colors.Gray;
        appWindow.TitleBar.ButtonHoverBackgroundColor = new Windows.UI.Color { A = 40, R = 255, G = 255, B = 255 };
        TitleBarBackground.Height = appWindow.TitleBar.Height;
    }
}