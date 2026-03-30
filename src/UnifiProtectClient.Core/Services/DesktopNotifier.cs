using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using UnifiProtectClient.Domain.Events;
using UnifiProtectClient.Services.Interfaces;

namespace UnifiProtectClient.Services;

public class DesktopNotifier : IDesktopNotifier
{
    private readonly string _snapshotPath;
    private readonly IAppNotificationSender _sender;

    public DesktopNotifier(IConfiguration configuration)
        : this(configuration, new AppNotificationSender()) { }

    internal DesktopNotifier(IConfiguration configuration, IAppNotificationSender sender)
    {
        _snapshotPath = configuration["SnapshotPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "snapshots", "snapshot.jpg");
        _sender = sender;
    }

    public void Notify(ProtectEvent protectEvent, string cameraName)
    {
        try
        {
            var title = BuildTitle(protectEvent, cameraName);
            var heroPath = SnapshotService.GetHeroPath(_snapshotPath);
            _sender.Notify(title, File.Exists(heroPath) ? heroPath : null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error showing notification: {ex.Message}");
        }
    }

    internal static string BuildTitle(ProtectEvent protectEvent, string cameraName) => protectEvent switch
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

    internal static string FormatSmartTypes(IReadOnlyList<string> types) =>
        string.Join(", ", types.Where(t => t.Length > 0).Select(t => char.ToUpper(t[0]) + t[1..]));

    internal static string Capitalize(string? input) =>
        string.IsNullOrEmpty(input) ? "" : char.ToUpper(input[0]) + input[1..];
}
