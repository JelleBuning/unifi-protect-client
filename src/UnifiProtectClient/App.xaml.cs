using CommunityToolkit.Mvvm.DependencyInjection;
using H.NotifyIcon;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
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
            var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(config =>
                {
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    _ = config.Build();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls("http://0.0.0.0:5000");
                    webBuilder.ConfigureServices(services => { services.AddControllers(); });

                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
                    });
                })
                .ConfigureServices((_, services) =>
                {
                    services.AddSingleton<MainWindow>();
                    services.AddTransient<IDesktopNotifier, DesktopNotifier>();
                })
                .Build();

            Ioc.Default.ConfigureServices(host.Services);
            
            _mainWindow = host.Services.GetRequiredService<MainWindow>();
            _mainWindow.ShowInTaskbar();
            host.Start();

        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"App startup failed: {ex.Message}");
            throw;
        }
    }

    public void OnToastClicked() => _mainWindow.Show();
}