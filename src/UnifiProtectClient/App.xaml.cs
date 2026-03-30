using CommunityToolkit.Mvvm.DependencyInjection;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using System;
using UnifiProtectClient.Application.Options;
using UnifiProtectClient.Application.Ports;
using UnifiProtectClient.Infrastructure.Http;
using UnifiProtectClient.Infrastructure.WebSocket;
using UnifiProtectClient.Services;
using UnifiProtectClient.Services.Interfaces;
using UnifiProtectClient.Views;

namespace UnifiProtectClient;

public partial class App
{
    private MainWindow? _mainWindow;

    protected override async void OnLaunched(LaunchActivatedEventArgs _)
    {
        var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        var keyInstance = AppInstance.FindOrRegisterForKey("UnifiProtectClient");

        if (!keyInstance.IsCurrent)
        {
            await keyInstance.RedirectActivationToAsync(activationArgs);
            Exit();
            return;
        }

        keyInstance.Activated += OnActivated;

        try
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ContentRootPath = AppContext.BaseDirectory
            });

            builder.Services.Configure<UnifiProtectOptions>(builder.Configuration.GetSection(UnifiProtectOptions.SectionName));
            builder.Services.Configure<EventNotificationSettings>(builder.Configuration.GetSection(EventNotificationSettings.SectionName));

            builder.Services.AddSingleton<IUnifiProtectApiClient, UnifiProtectApiClient>();
            builder.Services.AddSingleton<IProtectEventStream, ProtectEventStream>();
            builder.Services.AddTransient<IDesktopNotifier, DesktopNotifier>();

            builder.Services.AddSingleton<MainWindow>();

            var host = builder.Build();
            Ioc.Default.ConfigureServices(host.Services);

            _mainWindow = host.Services.GetRequiredService<MainWindow>();
            _mainWindow.ShowInTaskbar();

            AppNotificationManager.Default.NotificationInvoked += (_, _) => _mainWindow.ShowFromBackground();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"App startup failed: {ex.Message}");
            throw;
        }
    }

    private void OnActivated(object? sender, AppActivationArguments args)
    {
        if (args.Kind == ExtendedActivationKind.ToastNotification)
            _mainWindow?.DispatcherQueue.TryEnqueue(_mainWindow.BringToFront);
    }
}
