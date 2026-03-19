using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using UniFiApiProtectWebhookDotnet.Abstraction;
using UnifiProtectClient.Services.Interfaces;

namespace UnifiProtectClient.Services;

public class DesktopNotifier : IDesktopNotifier
{
    public void Notify(IAlarmEvent alarmEvent)
    {
        try
        {
            var notification = BuildAppNotification(alarmEvent);
            AppNotificationManager.Default.NotificationInvoked += DefaultOnNotificationInvoked;
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error showing notification: {ex.Message}");
        }
    }

    private void DefaultOnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        throw new NotImplementedException();
    }

    private static AppNotification BuildAppNotification(IAlarmEvent alarmEvent)
    {
        var alarm = alarmEvent.Alarm;
        var trigger = alarm.Triggers.FirstOrDefault();
        var title = $"UniFi Alarm: {alarm.Name}";
        var body = $"Device: {trigger?.Device ?? "N/A"}";

        var notificationBuilder = new AppNotificationBuilder()
            .AddText(title)
            .AddText(body)
            .SetHeroImage(new Uri("https://i.pcmag.com/imagery/reviews/05bQWLMCbAgAqIYBfDbdbHB-3.fit_lim.size_810x456.v_1569469953.jpg"));
        return notificationBuilder.BuildNotification();
    }
}