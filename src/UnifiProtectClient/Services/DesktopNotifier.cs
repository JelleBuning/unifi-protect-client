using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using UniFiApiProtectWebhookDotnet.Abstraction;
using UnifiProtectClient.Services.Interfaces;

namespace UnifiProtectClient.Services;

public class DesktopNotifier(IConfiguration configuration) : IDesktopNotifier
{
    private readonly string _snapshotPath = configuration["SnapshotPath"]
        ?? Path.Combine(AppContext.BaseDirectory, "snapshots", "snapshot.jpg");

    public void Notify(IAlarmEvent alarmEvent)
    {
        try
        {
            var notification = BuildAppNotification(alarmEvent);
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error showing notification: {ex.Message}");
        }
    }

    private AppNotification BuildAppNotification(IAlarmEvent alarmEvent)
    {
        var title = $"Event triggered ({alarmEvent.Alarm.Name})";

        var builder = new AppNotificationBuilder().AddText(title);

        var heroPath = SnapshotService.GetHeroPath(_snapshotPath);
        if (File.Exists(heroPath))
            builder.SetHeroImage(new Uri(heroPath));

        return builder.BuildNotification();
    }
}
