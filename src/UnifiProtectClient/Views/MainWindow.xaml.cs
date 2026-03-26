using H.NotifyIcon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using System.Runtime.InteropServices;
using UnifiProtectClient.Application.Options;
using UnifiProtectClient.Application.Ports;
using UnifiProtectClient.Services.Interfaces;
using UnifiProtectClient.ViewModels;
using Windows.Graphics;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace UnifiProtectClient.Views;

public sealed partial class MainWindow
{
    private const int WindowWidth = 565;
    private const int WindowHeight = 755;

    public MainViewModel ViewModel { get; }

    public MainWindow(
        IUnifiProtectApiClient apiClient,
        IProtectEventStream eventStream,
        IDesktopNotifier notifier,
        IConfiguration configuration,
        IOptions<EventNotificationSettings> eventSettings)
    {
        InitializeComponent();
        ResizeAndPosition();

        ViewModel = new MainViewModel(
            this,
            apiClient,
            eventStream,
            notifier,
            configuration,
            eventSettings.Value,
            DispatcherQueue.GetForCurrentThread());
        RootGrid.DataContext = ViewModel;

        Closed += OnWindowClosed;
    }

    public void BringToFront()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
    }

    public void ShowFromBackground() => DispatcherQueue.TryEnqueue(BringToFront);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    private const int SW_RESTORE = 9;

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