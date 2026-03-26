using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using UnifiProtectClient.Domain.Events;
using UnifiProtectClient.Services.Interfaces;

namespace UnifiProtectClient.Services;

public class DesktopNotifier(IConfiguration configuration) : IDesktopNotifier
{
    private readonly string _snapshotPath = configuration["SnapshotPath"]
        ?? Path.Combine(AppContext.BaseDirectory, "snapshots", "snapshot.jpg");

    public void Notify(ProtectEvent protectEvent, string cameraName)
    {
        try
        {
            var notification = BuildAppNotification(protectEvent, cameraName);
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error showing notification: {ex.Message}");
        }
    }

    private AppNotification BuildAppNotification(ProtectEvent protectEvent, string cameraName)
    {
        var title = BuildTitle(protectEvent, cameraName);
        var builder = new AppNotificationBuilder().AddText(title);

        var heroPath = SnapshotService.GetHeroPath(_snapshotPath);
        if (File.Exists(heroPath))
            builder.SetHeroImage(new Uri(heroPath));

        return builder.BuildNotification();
    }

    private static string BuildTitle(ProtectEvent protectEvent, string cameraName) => protectEvent switch
    {
        // Camera
        MotionEvent => $"Motion detected ({cameraName})",
        SmartDetectZoneEvent { SmartDetectTypes.Count: > 0 } e => $"{FormatSmartTypes(e.SmartDetectTypes)} detected ({cameraName})",
        SmartDetectZoneEvent => $"Smart detection ({cameraName})",
        SmartDetectLineEvent { SmartDetectTypes.Count: > 0 } e => $"{FormatSmartTypes(e.SmartDetectTypes)} crossed line ({cameraName})",
        SmartDetectLineEvent => $"Line crossing ({cameraName})",
        SmartDetectLoiterZoneEvent { SmartDetectTypes.Count: > 0 } e => $"{FormatSmartTypes(e.SmartDetectTypes)} loitering ({cameraName})",
        SmartDetectLoiterZoneEvent => $"Loitering detected ({cameraName})",
        SmartAudioDetectEvent { SmartDetectTypes.Count: > 0 } e => $"Audio: {FormatSmartTypes(e.SmartDetectTypes)} ({cameraName})",
        SmartAudioDetectEvent => $"Audio detection ({cameraName})",

        // Doorbell / Light
        RingEvent => $"Doorbell ring ({cameraName})",
        LightMotionEvent => $"Floodlight motion ({cameraName})",

        // Sensor
        SensorMotionEvent => $"Sensor motion ({cameraName})",
        SensorTamperEvent => $"Sensor tampered ({cameraName})",
        SensorSmokeTestEvent => $"Smoke detector test ({cameraName})",
        SensorAlarmEvent { AlarmType: { Length: > 0 } at } => $"Sensor alarm: {at} ({cameraName})",
        SensorAlarmEvent => $"Sensor alarm ({cameraName})",
        SensorOpenedEvent { MountType: { Length: > 0 } mt } => $"{Capitalize(mt)} opened ({cameraName})",
        SensorOpenedEvent => $"Sensor opened ({cameraName})",
        SensorClosedEvent { MountType: { Length: > 0 } mt } => $"{Capitalize(mt)} closed ({cameraName})",
        SensorClosedEvent => $"Sensor closed ({cameraName})",
        SensorWaterLeakEvent => $"Water leak detected ({cameraName})",
        SensorBatteryLowEvent { BatteryPercentage: var pct } => $"Low battery: {pct:F0}% ({cameraName})",
        SensorExtremeValuesEvent { SensorType: var st, SensorValue: var sv, Status: var s }
            => $"{Capitalize(st)} {s}: {sv:F1} ({cameraName})",

        _ => $"Event: {protectEvent.Type} ({cameraName})"
    };

    private static string FormatSmartTypes(IReadOnlyList<string> types) =>
        string.Join(", ", types.Where(t => t.Length > 0).Select(t => char.ToUpper(t[0]) + t[1..]));

    private static string Capitalize(string? input) =>
        string.IsNullOrEmpty(input) ? "" : char.ToUpper(input[0]) + input[1..];
}
