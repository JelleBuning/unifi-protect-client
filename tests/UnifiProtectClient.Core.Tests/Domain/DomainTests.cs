using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnifiProtectClient.Domain.Cameras;
using UnifiProtectClient.Domain.Events;

namespace UnifiProtectClient.Core.Tests.Domain;

[TestClass]
public sealed class CameraTests
{
    [TestMethod]
    public void Camera_Constructor_SetsAllProperties()
    {
        // Arrange & Act
        var camera = new Camera("id-1", "Front Door", true);

        // Assert
        Assert.AreEqual("id-1", camera.Id);
        Assert.AreEqual("Front Door", camera.Name);
        Assert.IsTrue(camera.IsConnected);
    }

    [TestMethod]
    public void Camera_WithIsConnectedFalse_ReturnsCorrectValue()
    {
        var camera = new Camera("id-2", "Backyard", false);
        Assert.IsFalse(camera.IsConnected);
    }

    [TestMethod]
    public void Camera_EqualityByValue_WorksCorrectly()
    {
        var a = new Camera("id-1", "Front Door", true);
        var b = new Camera("id-1", "Front Door", true);
        var c = new Camera("id-2", "Front Door", true);

        Assert.AreEqual(a, b);
        Assert.AreNotEqual(a, c);
    }

    [TestMethod]
    public void Camera_ToString_ContainsFields()
    {
        var camera = new Camera("abc", "Garage", false);
        var str = camera.ToString();
        Assert.IsNotNull(str);
        Assert.IsTrue(str.Contains("abc"));
    }
}

[TestClass]
public sealed class RtspsStreamTests
{
    [TestMethod]
    public void RtspsStream_Constructor_SetsProperties()
    {
        var stream = new RtspsStream("rtsps://host/stream", "high");
        Assert.AreEqual("rtsps://host/stream", stream.Url);
        Assert.AreEqual("high", stream.StreamName);
    }

    [TestMethod]
    public void RtspsStream_EqualityByValue_WorksCorrectly()
    {
        var a = new RtspsStream("rtsps://host/stream", "high");
        var b = new RtspsStream("rtsps://host/stream", "high");
        var c = new RtspsStream("rtsps://host/other", "high");

        Assert.AreEqual(a, b);
        Assert.AreNotEqual(a, c);
    }
}

[TestClass]
public sealed class ProtectEventTests
{
    private const string Id = "event-1";
    private const string DeviceId = "device-1";
    private const long Start = 1_700_000_000_000L;
    private const long End = 1_700_000_005_000L;

    // ── Camera events ─────────────────────────────────────────────────────────

    [TestMethod]
    public void MotionEvent_TypeIsMotion()
    {
        var e = new MotionEvent(Id, Start, End, DeviceId, ProtectEventUpdateType.Add);
        Assert.AreEqual("motion", e.Type);
        Assert.AreEqual(Id, e.Id);
        Assert.AreEqual(Start, e.Start);
        Assert.AreEqual(End, e.End);
        Assert.AreEqual(DeviceId, e.DeviceId);
        Assert.AreEqual(ProtectEventUpdateType.Add, e.UpdateType);
    }

    [TestMethod]
    public void MotionEvent_WithNullEnd_HasNullEnd()
    {
        var e = new MotionEvent(Id, Start, null, DeviceId, ProtectEventUpdateType.Update);
        Assert.IsNull(e.End);
    }

    [TestMethod]
    public void SmartDetectZoneEvent_TypeAndSmartDetectTypes()
    {
        var types = new List<string> { "person", "vehicle" }.AsReadOnly();
        var e = new SmartDetectZoneEvent(Id, Start, End, DeviceId, ProtectEventUpdateType.Add, types);
        Assert.AreEqual("smartDetectZone", e.Type);
        CollectionAssert.AreEqual(new[] { "person", "vehicle" }, (System.Collections.ICollection)e.SmartDetectTypes);
    }

    [TestMethod]
    public void SmartDetectLineEvent_TypeIsSmartDetectLine()
    {
        var types = new List<string> { "car" }.AsReadOnly();
        var e = new SmartDetectLineEvent(Id, Start, End, DeviceId, ProtectEventUpdateType.Add, types);
        Assert.AreEqual("smartDetectLine", e.Type);
        Assert.AreEqual(1, e.SmartDetectTypes.Count);
    }

    [TestMethod]
    public void SmartDetectLoiterZoneEvent_TypeIsSmartDetectLoiterZone()
    {
        var types = new List<string>().AsReadOnly();
        var e = new SmartDetectLoiterZoneEvent(Id, Start, End, DeviceId, ProtectEventUpdateType.Update, types);
        Assert.AreEqual("smartDetectLoiterZone", e.Type);
        Assert.IsEmpty(e.SmartDetectTypes);
    }

    [TestMethod]
    public void SmartAudioDetectEvent_TypeIsSmartAudioDetect()
    {
        var types = new List<string> { "smoke" }.AsReadOnly();
        var e = new SmartAudioDetectEvent(Id, Start, End, DeviceId, ProtectEventUpdateType.Add, types);
        Assert.AreEqual("smartAudioDetect", e.Type);
    }

    // ── Doorbell ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void RingEvent_TypeIsRing()
    {
        var e = new RingEvent(Id, Start, null, DeviceId, ProtectEventUpdateType.Add);
        Assert.AreEqual("ring", e.Type);
        Assert.IsNull(e.End);
    }

    // ── Floodlight ────────────────────────────────────────────────────────────

    [TestMethod]
    public void LightMotionEvent_TypeIsLightMotionAndEndIsNull()
    {
        var e = new LightMotionEvent(Id, Start, DeviceId, ProtectEventUpdateType.Add);
        Assert.AreEqual("lightMotion", e.Type);
        Assert.IsNull(e.End);
    }

    // ── Sensor events ─────────────────────────────────────────────────────────

    [TestMethod]
    public void SensorMotionEvent_TypeIsSensorMotion()
    {
        var e = new SensorMotionEvent(Id, Start, End, DeviceId, ProtectEventUpdateType.Add);
        Assert.AreEqual("sensorMotion", e.Type);
    }

    [TestMethod]
    public void SensorTamperEvent_TypeIsSensorTamper()
    {
        var e = new SensorTamperEvent(Id, Start, End, DeviceId, ProtectEventUpdateType.Update);
        Assert.AreEqual("sensorTamper", e.Type);
    }

    [TestMethod]
    public void SensorSmokeTestEvent_TypeIsSensorSmokeTest()
    {
        var e = new SensorSmokeTestEvent(Id, Start, null, DeviceId, ProtectEventUpdateType.Add);
        Assert.AreEqual("sensorSmokeTest", e.Type);
    }

    [TestMethod]
    public void SensorAlarmEvent_TypeAndAlarmType()
    {
        var e = new SensorAlarmEvent(Id, Start, End, DeviceId, ProtectEventUpdateType.Add, "smoke");
        Assert.AreEqual("sensorAlarm", e.Type);
        Assert.AreEqual("smoke", e.AlarmType);
    }

    [TestMethod]
    public void SensorOpenedEvent_TypeAndMountType()
    {
        var e = new SensorOpenedEvent(Id, Start, End, DeviceId, ProtectEventUpdateType.Add, "door");
        Assert.AreEqual("sensorOpened", e.Type);
        Assert.AreEqual("door", e.MountType);
    }

    [TestMethod]
    public void SensorClosedEvent_TypeAndMountType()
    {
        var e = new SensorClosedEvent(Id, Start, End, DeviceId, ProtectEventUpdateType.Add, "window");
        Assert.AreEqual("sensorClosed", e.Type);
        Assert.AreEqual("window", e.MountType);
    }

    [TestMethod]
    public void SensorWaterLeakEvent_TypeAndMountType()
    {
        var e = new SensorWaterLeakEvent(Id, Start, End, DeviceId, ProtectEventUpdateType.Add, "leak");
        Assert.AreEqual("sensorWaterLeak", e.Type);
        Assert.AreEqual("leak", e.MountType);
    }

    [TestMethod]
    public void SensorBatteryLowEvent_TypeAndBatteryPercentage()
    {
        var e = new SensorBatteryLowEvent(Id, Start, End, DeviceId, ProtectEventUpdateType.Add, 12.5);
        Assert.AreEqual("sensorBatteryLow", e.Type);
        Assert.AreEqual(12.5, e.BatteryPercentage);
    }

    [TestMethod]
    public void SensorExtremeValuesEvent_AllFields()
    {
        var e = new SensorExtremeValuesEvent(Id, Start, End, DeviceId, ProtectEventUpdateType.Add, "temperature", 42.1, "high");
        Assert.AreEqual("sensorExtremeValues", e.Type);
        Assert.AreEqual("temperature", e.SensorType);
        Assert.AreEqual(42.1, e.SensorValue);
        Assert.AreEqual("high", e.Status);
    }

    [TestMethod]
    public void UnknownEvent_TypeReflectsRawType()
    {
        var e = new UnknownEvent(Id, "customType", Start, End, DeviceId, ProtectEventUpdateType.Add);
        Assert.AreEqual("customType", e.Type);
        Assert.AreEqual(Id, e.Id);
    }

    [TestMethod]
    public void ProtectEventUpdateType_Values()
    {
        Assert.AreEqual(0, (int)ProtectEventUpdateType.Add);
        Assert.AreEqual(1, (int)ProtectEventUpdateType.Update);
    }
}
