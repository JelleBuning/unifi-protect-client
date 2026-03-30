using UnifiProtectClient.Domain.Events;

namespace UnifiProtectClient.Application.Options;

/// <summary>
/// Controls which Protect event types trigger a desktop notification.
/// All events default to disabled; only <see cref="Ring"/> is on by default.
/// Bind from appsettings.json under the "EventNotifications" section.
/// </summary>
public sealed class EventNotificationSettings
{
    public const string SectionName = "EventNotifications";

    // ── Camera ────────────────────────────────────────────────────────────────
    public bool Motion                  { get; init; } = false;
    public bool SmartDetectZone         { get; init; } = false;
    public bool SmartDetectLine         { get; init; } = false;
    public bool SmartDetectLoiterZone   { get; init; } = false;
    public bool SmartAudioDetect        { get; init; } = false;

    // ── Doorbell ──────────────────────────────────────────────────────────────
    public bool Ring                    { get; init; } = true;

    // ── Floodlight ────────────────────────────────────────────────────────────
    public bool LightMotion             { get; init; } = false;

    // ── Sensor ────────────────────────────────────────────────────────────────
    public bool SensorMotion            { get; init; } = false;
    public bool SensorTamper            { get; init; } = false;
    public bool SensorSmokeTest         { get; init; } = false;
    public bool SensorAlarm             { get; init; } = false;
    public bool SensorOpened            { get; init; } = false;
    public bool SensorClosed            { get; init; } = false;
    public bool SensorWaterLeak         { get; init; } = false;
    public bool SensorBatteryLow        { get; init; } = false;
    public bool SensorExtremeValues     { get; init; } = false;

    /// <summary>Returns true when the given event type should trigger a notification.</summary>
    public bool IsEnabled(ProtectEvent @event) => @event switch
    {
        MotionEvent                 => Motion,
        SmartDetectZoneEvent        => SmartDetectZone,
        SmartDetectLineEvent        => SmartDetectLine,
        SmartDetectLoiterZoneEvent  => SmartDetectLoiterZone,
        SmartAudioDetectEvent       => SmartAudioDetect,
        RingEvent                   => Ring,
        LightMotionEvent            => LightMotion,
        SensorMotionEvent           => SensorMotion,
        SensorTamperEvent           => SensorTamper,
        SensorSmokeTestEvent        => SensorSmokeTest,
        SensorAlarmEvent            => SensorAlarm,
        SensorOpenedEvent           => SensorOpened,
        SensorClosedEvent           => SensorClosed,
        SensorWaterLeakEvent        => SensorWaterLeak,
        SensorBatteryLowEvent       => SensorBatteryLow,
        SensorExtremeValuesEvent    => SensorExtremeValues,
        _                           => false
    };
}
