using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnifiProtectClient.Application.Options;
using UnifiProtectClient.Domain.Events;

namespace UnifiProtectClient.Core.Tests.Application;

[TestClass]
public sealed class UnifiProtectOptionsTests
{
    [TestMethod]
    public void SectionName_IsUnifiProtect()
    {
        Assert.AreEqual("UnifiProtect", UnifiProtectOptions.SectionName);
    }

    [TestMethod]
    public void Options_Properties_AreSetViaInitializer()
    {
        var opts = new UnifiProtectOptions
        {
            BaseUrl = "https://192.168.1.1/proxy/protect/api",
            ApiKey = "my-api-key"
        };

        Assert.AreEqual("https://192.168.1.1/proxy/protect/api", opts.BaseUrl);
        Assert.AreEqual("my-api-key", opts.ApiKey);
    }
}

[TestClass]
public sealed class EventNotificationSettingsTests
{
    [TestMethod]
    public void SectionName_IsEventNotifications()
    {
        Assert.AreEqual("EventNotifications", EventNotificationSettings.SectionName);
    }

    [TestMethod]
    public void Defaults_OnlyRingIsTrue()
    {
        var settings = new EventNotificationSettings();

        Assert.IsFalse(settings.Motion);
        Assert.IsFalse(settings.SmartDetectZone);
        Assert.IsFalse(settings.SmartDetectLine);
        Assert.IsFalse(settings.SmartDetectLoiterZone);
        Assert.IsFalse(settings.SmartAudioDetect);
        Assert.IsTrue(settings.Ring);
        Assert.IsFalse(settings.LightMotion);
        Assert.IsFalse(settings.SensorMotion);
        Assert.IsFalse(settings.SensorTamper);
        Assert.IsFalse(settings.SensorSmokeTest);
        Assert.IsFalse(settings.SensorAlarm);
        Assert.IsFalse(settings.SensorOpened);
        Assert.IsFalse(settings.SensorClosed);
        Assert.IsFalse(settings.SensorWaterLeak);
        Assert.IsFalse(settings.SensorBatteryLow);
        Assert.IsFalse(settings.SensorExtremeValues);
    }

    // Helper to build an event of each type
    private static MotionEvent Motion() => new("id", 0, null, "dev", ProtectEventUpdateType.Add);
    private static SmartDetectZoneEvent SmartZone() => new("id", 0, null, "dev", ProtectEventUpdateType.Add, []);
    private static SmartDetectLineEvent SmartLine() => new("id", 0, null, "dev", ProtectEventUpdateType.Add, []);
    private static SmartDetectLoiterZoneEvent SmartLoiter() => new("id", 0, null, "dev", ProtectEventUpdateType.Add, []);
    private static SmartAudioDetectEvent SmartAudio() => new("id", 0, null, "dev", ProtectEventUpdateType.Add, []);
    private static RingEvent Ring() => new("id", 0, null, "dev", ProtectEventUpdateType.Add);
    private static LightMotionEvent LightMotion() => new("id", 0, "dev", ProtectEventUpdateType.Add);
    private static SensorMotionEvent SensorMotion() => new("id", 0, null, "dev", ProtectEventUpdateType.Add);
    private static SensorTamperEvent SensorTamper() => new("id", 0, null, "dev", ProtectEventUpdateType.Add);
    private static SensorSmokeTestEvent SensorSmokeTest() => new("id", 0, null, "dev", ProtectEventUpdateType.Add);
    private static SensorAlarmEvent SensorAlarm() => new("id", 0, null, "dev", ProtectEventUpdateType.Add, "smoke");
    private static SensorOpenedEvent SensorOpened() => new("id", 0, null, "dev", ProtectEventUpdateType.Add, "door");
    private static SensorClosedEvent SensorClosed() => new("id", 0, null, "dev", ProtectEventUpdateType.Add, "window");
    private static SensorWaterLeakEvent SensorWaterLeak() => new("id", 0, null, "dev", ProtectEventUpdateType.Add, "leak");
    private static SensorBatteryLowEvent SensorBatteryLow() => new("id", 0, null, "dev", ProtectEventUpdateType.Add, 15.0);
    private static SensorExtremeValuesEvent SensorExtremeValues() => new("id", 0, null, "dev", ProtectEventUpdateType.Add, "temperature", 45.0, "high");
    private static UnknownEvent Unknown() => new("id", "weird", 0, null, "dev", ProtectEventUpdateType.Add);

    [TestMethod]
    public void IsEnabled_MotionDisabledByDefault_ReturnsFalse()
        => Assert.IsFalse(new EventNotificationSettings().IsEnabled(Motion()));

    [TestMethod]
    public void IsEnabled_MotionEnabled_ReturnsTrue()
        => Assert.IsTrue(new EventNotificationSettings { Motion = true }.IsEnabled(Motion()));

    [TestMethod]
    public void IsEnabled_SmartDetectZoneDisabledByDefault()
        => Assert.IsFalse(new EventNotificationSettings().IsEnabled(SmartZone()));

    [TestMethod]
    public void IsEnabled_SmartDetectZoneEnabled_ReturnsTrue()
        => Assert.IsTrue(new EventNotificationSettings { SmartDetectZone = true }.IsEnabled(SmartZone()));

    [TestMethod]
    public void IsEnabled_SmartDetectLineDisabledByDefault()
        => Assert.IsFalse(new EventNotificationSettings().IsEnabled(SmartLine()));

    [TestMethod]
    public void IsEnabled_SmartDetectLineEnabled_ReturnsTrue()
        => Assert.IsTrue(new EventNotificationSettings { SmartDetectLine = true }.IsEnabled(SmartLine()));

    [TestMethod]
    public void IsEnabled_SmartDetectLoiterZoneDisabledByDefault()
        => Assert.IsFalse(new EventNotificationSettings().IsEnabled(SmartLoiter()));

    [TestMethod]
    public void IsEnabled_SmartDetectLoiterZoneEnabled_ReturnsTrue()
        => Assert.IsTrue(new EventNotificationSettings { SmartDetectLoiterZone = true }.IsEnabled(SmartLoiter()));

    [TestMethod]
    public void IsEnabled_SmartAudioDetectDisabledByDefault()
        => Assert.IsFalse(new EventNotificationSettings().IsEnabled(SmartAudio()));

    [TestMethod]
    public void IsEnabled_SmartAudioDetectEnabled_ReturnsTrue()
        => Assert.IsTrue(new EventNotificationSettings { SmartAudioDetect = true }.IsEnabled(SmartAudio()));

    [TestMethod]
    public void IsEnabled_RingEnabledByDefault_ReturnsTrue()
        => Assert.IsTrue(new EventNotificationSettings().IsEnabled(Ring()));

    [TestMethod]
    public void IsEnabled_RingDisabled_ReturnsFalse()
        => Assert.IsFalse(new EventNotificationSettings { Ring = false }.IsEnabled(Ring()));

    [TestMethod]
    public void IsEnabled_LightMotionDisabledByDefault()
        => Assert.IsFalse(new EventNotificationSettings().IsEnabled(LightMotion()));

    [TestMethod]
    public void IsEnabled_LightMotionEnabled_ReturnsTrue()
        => Assert.IsTrue(new EventNotificationSettings { LightMotion = true }.IsEnabled(LightMotion()));

    [TestMethod]
    public void IsEnabled_SensorMotionDisabledByDefault()
        => Assert.IsFalse(new EventNotificationSettings().IsEnabled(SensorMotion()));

    [TestMethod]
    public void IsEnabled_SensorMotionEnabled_ReturnsTrue()
        => Assert.IsTrue(new EventNotificationSettings { SensorMotion = true }.IsEnabled(SensorMotion()));

    [TestMethod]
    public void IsEnabled_SensorTamperDisabledByDefault()
        => Assert.IsFalse(new EventNotificationSettings().IsEnabled(SensorTamper()));

    [TestMethod]
    public void IsEnabled_SensorTamperEnabled_ReturnsTrue()
        => Assert.IsTrue(new EventNotificationSettings { SensorTamper = true }.IsEnabled(SensorTamper()));

    [TestMethod]
    public void IsEnabled_SensorSmokeTestDisabledByDefault()
        => Assert.IsFalse(new EventNotificationSettings().IsEnabled(SensorSmokeTest()));

    [TestMethod]
    public void IsEnabled_SensorSmokeTestEnabled_ReturnsTrue()
        => Assert.IsTrue(new EventNotificationSettings { SensorSmokeTest = true }.IsEnabled(SensorSmokeTest()));

    [TestMethod]
    public void IsEnabled_SensorAlarmDisabledByDefault()
        => Assert.IsFalse(new EventNotificationSettings().IsEnabled(SensorAlarm()));

    [TestMethod]
    public void IsEnabled_SensorAlarmEnabled_ReturnsTrue()
        => Assert.IsTrue(new EventNotificationSettings { SensorAlarm = true }.IsEnabled(SensorAlarm()));

    [TestMethod]
    public void IsEnabled_SensorOpenedDisabledByDefault()
        => Assert.IsFalse(new EventNotificationSettings().IsEnabled(SensorOpened()));

    [TestMethod]
    public void IsEnabled_SensorOpenedEnabled_ReturnsTrue()
        => Assert.IsTrue(new EventNotificationSettings { SensorOpened = true }.IsEnabled(SensorOpened()));

    [TestMethod]
    public void IsEnabled_SensorClosedDisabledByDefault()
        => Assert.IsFalse(new EventNotificationSettings().IsEnabled(SensorClosed()));

    [TestMethod]
    public void IsEnabled_SensorClosedEnabled_ReturnsTrue()
        => Assert.IsTrue(new EventNotificationSettings { SensorClosed = true }.IsEnabled(SensorClosed()));

    [TestMethod]
    public void IsEnabled_SensorWaterLeakDisabledByDefault()
        => Assert.IsFalse(new EventNotificationSettings().IsEnabled(SensorWaterLeak()));

    [TestMethod]
    public void IsEnabled_SensorWaterLeakEnabled_ReturnsTrue()
        => Assert.IsTrue(new EventNotificationSettings { SensorWaterLeak = true }.IsEnabled(SensorWaterLeak()));

    [TestMethod]
    public void IsEnabled_SensorBatteryLowDisabledByDefault()
        => Assert.IsFalse(new EventNotificationSettings().IsEnabled(SensorBatteryLow()));

    [TestMethod]
    public void IsEnabled_SensorBatteryLowEnabled_ReturnsTrue()
        => Assert.IsTrue(new EventNotificationSettings { SensorBatteryLow = true }.IsEnabled(SensorBatteryLow()));

    [TestMethod]
    public void IsEnabled_SensorExtremeValuesDisabledByDefault()
        => Assert.IsFalse(new EventNotificationSettings().IsEnabled(SensorExtremeValues()));

    [TestMethod]
    public void IsEnabled_SensorExtremeValuesEnabled_ReturnsTrue()
        => Assert.IsTrue(new EventNotificationSettings { SensorExtremeValues = true }.IsEnabled(SensorExtremeValues()));

    [TestMethod]
    public void IsEnabled_UnknownEvent_ReturnsFalse()
        => Assert.IsFalse(new EventNotificationSettings { Ring = true }.IsEnabled(Unknown()));
}
