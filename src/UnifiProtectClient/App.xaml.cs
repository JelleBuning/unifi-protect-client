using CommunityToolkit.Mvvm.DependencyInjection;
using H.NotifyIcon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly MainWindow _mainWindow = null!;

    public App()
    {
        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.Configure<UnifiProtectOptions>(config.GetSection(UnifiProtectOptions.SectionName));
            services.Configure<EventNotificationSettings>(config.GetSection(EventNotificationSettings.SectionName));
            services.AddSingleton<IUnifiProtectApiClient, UnifiProtectApiClient>();
            services.AddSingleton<IProtectEventStream, ProtectEventStream>();
            services.AddTransient<IDesktopNotifier, DesktopNotifier>();
            services.AddSingleton<MainWindow>();

            var provider = services.BuildServiceProvider();
            Ioc.Default.ConfigureServices(provider);

            _mainWindow = provider.GetRequiredService<MainWindow>();
            _mainWindow.ShowInTaskbar();

            AppNotificationManager.Default.NotificationInvoked += (_, _) => _mainWindow.ShowFromBackground();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"App startup failed: {ex.Message}");
            throw;
        }
    }

    public void OnToastClicked() => _mainWindow.BringToFront();
}