using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using UnifiProtectClient.Application.Options;
using UnifiProtectClient.Domain.Events;
using UnifiProtectClient.Infrastructure.WebSocket;

namespace UnifiProtectClient.Core.Tests.Infrastructure;

[TestClass]
public sealed class ProtectEventStreamParseTests
{
    // ── ParseEvent ────────────────────────────────────────────────────────────

    [TestMethod]
    public void ParseEvent_Motion_ReturnsMotionEvent()
    {
        const string json = """
            {"type":"add","item":{"id":"ev1","type":"motion","start":1000,"device":"dev1"}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<MotionEvent>(result, out var ev);
        Assert.AreEqual("ev1", ev.Id);
        Assert.AreEqual(1000L, ev.Start);
        Assert.AreEqual("dev1", ev.DeviceId);
        Assert.AreEqual(ProtectEventUpdateType.Add, ev.UpdateType);
        Assert.IsNull(ev.End);
    }

    [TestMethod]
    public void ParseEvent_MotionWithEnd_HasEndTimestamp()
    {
        const string json = """
            {"type":"update","item":{"id":"ev1","type":"motion","start":1000,"end":2000,"device":"dev1"}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<MotionEvent>(result, out var ev);
        Assert.AreEqual(2000L, ev.End);
        Assert.AreEqual(ProtectEventUpdateType.Update, ev.UpdateType);
    }

    [TestMethod]
    public void ParseEvent_SmartDetectZone_ReturnsWithSmartTypes()
    {
        const string json = """
            {"type":"add","item":{"id":"ev2","type":"smartDetectZone","start":1000,"device":"dev1","smartDetectTypes":["person","vehicle"]}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<SmartDetectZoneEvent>(result, out var ev);
        CollectionAssert.AreEqual(new[] { "person", "vehicle" }, (System.Collections.ICollection)ev.SmartDetectTypes);
    }

    [TestMethod]
    public void ParseEvent_SmartDetectLine_ReturnsWithSmartTypes()
    {
        const string json = """
            {"type":"add","item":{"id":"ev3","type":"smartDetectLine","start":1000,"device":"dev1","smartDetectTypes":["car"]}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<SmartDetectLineEvent>(result, out _);
    }

    [TestMethod]
    public void ParseEvent_SmartDetectLoiterZone_ReturnsEvent()
    {
        const string json = """
            {"type":"add","item":{"id":"ev4","type":"smartDetectLoiterZone","start":1000,"device":"dev1","smartDetectTypes":[]}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<SmartDetectLoiterZoneEvent>(result, out _);
    }

    [TestMethod]
    public void ParseEvent_SmartAudioDetect_ReturnsEvent()
    {
        const string json = """
            {"type":"add","item":{"id":"ev5","type":"smartAudioDetect","start":1000,"device":"dev1","smartDetectTypes":["smoke"]}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<SmartAudioDetectEvent>(result, out _);
    }

    [TestMethod]
    public void ParseEvent_Ring_ReturnsRingEvent()
    {
        const string json = """
            {"type":"add","item":{"id":"ev6","type":"ring","start":1000,"device":"dev1"}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<RingEvent>(result, out _);
    }

    [TestMethod]
    public void ParseEvent_LightMotion_ReturnsLightMotionEvent()
    {
        const string json = """
            {"type":"add","item":{"id":"ev7","type":"lightMotion","start":1000,"device":"dev1"}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<LightMotionEvent>(result, out _);
    }

    [TestMethod]
    public void ParseEvent_SensorMotion_ReturnsSensorMotionEvent()
    {
        const string json = """
            {"type":"add","item":{"id":"ev8","type":"sensorMotion","start":1000,"device":"dev1"}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<SensorMotionEvent>(result, out _);
    }

    [TestMethod]
    public void ParseEvent_SensorTamper_ReturnsSensorTamperEvent()
    {
        const string json = """
            {"type":"add","item":{"id":"ev9","type":"sensorTamper","start":1000,"device":"dev1"}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<SensorTamperEvent>(result, out _);
    }

    [TestMethod]
    public void ParseEvent_SensorSmokeTest_ReturnsSensorSmokeTestEvent()
    {
        const string json = """
            {"type":"add","item":{"id":"ev10","type":"sensorSmokeTest","start":1000,"device":"dev1"}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<SensorSmokeTestEvent>(result, out _);
    }

    [TestMethod]
    public void ParseEvent_SensorAlarm_WithMetadata_ReturnsAlarmType()
    {
        const string json = """
            {"type":"add","item":{"id":"ev11","type":"sensorAlarm","start":1000,"device":"dev1",
            "metadata":{"alarmType":{"text":"smoke"}}}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<SensorAlarmEvent>(result, out var ev);
        Assert.AreEqual("smoke", ev.AlarmType);
    }

    [TestMethod]
    public void ParseEvent_SensorAlarm_WithoutMetadata_ReturnsEmptyAlarmType()
    {
        const string json = """
            {"type":"add","item":{"id":"ev11","type":"sensorAlarm","start":1000,"device":"dev1"}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<SensorAlarmEvent>(result, out var ev);
        Assert.AreEqual(string.Empty, ev.AlarmType);
    }

    [TestMethod]
    public void ParseEvent_SensorOpened_WithMountType_ReturnsMountType()
    {
        const string json = """
            {"type":"add","item":{"id":"ev12","type":"sensorOpened","start":1000,"device":"dev1",
            "metadata":{"sensorMountType":{"text":"door"}}}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<SensorOpenedEvent>(result, out var ev);
        Assert.AreEqual("door", ev.MountType);
    }

    [TestMethod]
    public void ParseEvent_SensorOpened_WithoutMetadata_ReturnsEmptyMountType()
    {
        const string json = """
            {"type":"add","item":{"id":"ev12","type":"sensorOpened","start":1000,"device":"dev1"}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<SensorOpenedEvent>(result, out var ev);
        Assert.AreEqual(string.Empty, ev.MountType);
    }

    [TestMethod]
    public void ParseEvent_SensorClosed_WithMountType_ReturnsMountType()
    {
        const string json = """
            {"type":"add","item":{"id":"ev13","type":"sensorClosed","start":1000,"device":"dev1",
            "metadata":{"sensorMountType":{"text":"window"}}}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<SensorClosedEvent>(result, out var ev);
        Assert.AreEqual("window", ev.MountType);
    }

    [TestMethod]
    public void ParseEvent_SensorClosed_WithoutMetadata_ReturnsEmptyMountType()
    {
        const string json = """
            {"type":"add","item":{"id":"ev13","type":"sensorClosed","start":1000,"device":"dev1"}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<SensorClosedEvent>(result, out var ev);
        Assert.AreEqual(string.Empty, ev.MountType);
    }

    [TestMethod]
    public void ParseEvent_SensorWaterLeak_WithMountType_ReturnsMountType()
    {
        const string json = """
            {"type":"add","item":{"id":"ev14","type":"sensorWaterLeak","start":1000,"device":"dev1",
            "metadata":{"sensorMountType":{"text":"leak"}}}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<SensorWaterLeakEvent>(result, out var ev);
        Assert.AreEqual("leak", ev.MountType);
    }

    [TestMethod]
    public void ParseEvent_SensorWaterLeak_WithoutMetadata_ReturnsEmptyMountType()
    {
        const string json = """
            {"type":"add","item":{"id":"ev14","type":"sensorWaterLeak","start":1000,"device":"dev1"}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<SensorWaterLeakEvent>(result, out var ev);
        Assert.AreEqual(string.Empty, ev.MountType);
    }

    [TestMethod]
    public void ParseEvent_SensorBatteryLow_WithPercentage_ReturnsBatteryPercentage()
    {
        const string json = """
            {"type":"add","item":{"id":"ev15","type":"sensorBatteryLow","start":1000,"device":"dev1",
            "metadata":{"sensorBatteryPercentage":{"number":12.5}}}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<SensorBatteryLowEvent>(result, out var ev);
        Assert.AreEqual(12.5, ev.BatteryPercentage);
    }

    [TestMethod]
    public void ParseEvent_SensorBatteryLow_WithoutMetadata_ReturnsZeroPercentage()
    {
        const string json = """
            {"type":"add","item":{"id":"ev15","type":"sensorBatteryLow","start":1000,"device":"dev1"}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<SensorBatteryLowEvent>(result, out var ev);
        Assert.AreEqual(0d, ev.BatteryPercentage);
    }

    [TestMethod]
    public void ParseEvent_SensorExtremeValues_WithAllMetadata_ReturnsAllFields()
    {
        const string json = """
            {"type":"add","item":{"id":"ev16","type":"sensorExtremeValues","start":1000,"device":"dev1",
            "metadata":{"sensorType":{"text":"temperature"},"sensorValue":{"text":42.5},"status":{"text":"high"}}}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<SensorExtremeValuesEvent>(result, out var ev);
        Assert.AreEqual("temperature", ev.SensorType);
        Assert.AreEqual(42.5, ev.SensorValue);
        Assert.AreEqual("high", ev.Status);
    }

    [TestMethod]
    public void ParseEvent_SensorExtremeValues_WithoutMetadata_ReturnsDefaults()
    {
        const string json = """
            {"type":"add","item":{"id":"ev16","type":"sensorExtremeValues","start":1000,"device":"dev1"}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<SensorExtremeValuesEvent>(result, out var ev);
        Assert.AreEqual(string.Empty, ev.SensorType);
        Assert.AreEqual(0d, ev.SensorValue);
        Assert.AreEqual(string.Empty, ev.Status);
    }

    [TestMethod]
    public void ParseEvent_UnknownType_ReturnsUnknownEvent()
    {
        const string json = """
            {"type":"add","item":{"id":"ev99","type":"mystery","start":1000,"device":"dev1"}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<UnknownEvent>(result, out var ev);
        Assert.AreEqual("mystery", ev.Type);
    }

    [TestMethod]
    public void ParseEvent_NullEndProperty_ReturnsNullEnd()
    {
        const string json = """
            {"type":"add","item":{"id":"ev1","type":"motion","start":1000,"end":null,"device":"dev1"}}
            """;
        var result = ProtectEventStream.ParseEvent(json);
        Assert.IsInstanceOfType<MotionEvent>(result, out var ev);
        Assert.IsNull(ev.End);
    }

    [TestMethod]
    public void ParseEvent_InvalidJson_ReturnsNull()
    {
        var result = ProtectEventStream.ParseEvent("not-valid-json");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ParseEvent_MissingRequiredField_ReturnsNull()
    {
        // Missing "item" property
        var result = ProtectEventStream.ParseEvent("""{"type":"add"}""");
        Assert.IsNull(result);
    }

    // ── BuildWebSocketUri ─────────────────────────────────────────────────────

    [TestMethod]
    public void BuildWebSocketUri_HttpsBaseUrl_UsesWss()
    {
        var options = Options.Create(new UnifiProtectOptions
        {
            BaseUrl = "https://192.168.1.1/proxy/protect/api",
            ApiKey = "key"
        });
        var stream = new ProtectEventStream(options, new Mock<IWebSocketFactory>().Object);
        var uri = stream.BuildWebSocketUri();
        Assert.AreEqual("wss", uri.Scheme);
        Assert.AreEqual("192.168.1.1", uri.Host);
        Assert.IsTrue(uri.AbsolutePath.EndsWith("/v1/subscribe/events"));
    }

    [TestMethod]
    public void BuildWebSocketUri_HttpBaseUrl_UsesWs()
    {
        var options = Options.Create(new UnifiProtectOptions
        {
            BaseUrl = "http://192.168.1.1/api",
            ApiKey = "key"
        });
        var stream = new ProtectEventStream(options, new Mock<IWebSocketFactory>().Object);
        var uri = stream.BuildWebSocketUri();
        Assert.AreEqual("ws", uri.Scheme);
    }

    [TestMethod]
    public void BuildWebSocketUri_TrailingSlash_HandledCorrectly()
    {
        var options = Options.Create(new UnifiProtectOptions
        {
            BaseUrl = "https://host/api/",
            ApiKey = "key"
        });
        var stream = new ProtectEventStream(options, new Mock<IWebSocketFactory>().Object);
        var uri = stream.BuildWebSocketUri();
        Assert.AreEqual("wss", uri.Scheme);
        Assert.IsTrue(uri.AbsolutePath.EndsWith("v1/subscribe/events"));
    }

    // ── SubscribeAsync ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task SubscribeAsync_AlreadyCancelled_YieldsNoEvents()
    {
        // Arrange
        var options = Options.Create(new UnifiProtectOptions { BaseUrl = "https://host", ApiKey = "key" });
        var wsFactoryMock = new Mock<IWebSocketFactory>();
        var stream = new ProtectEventStream(options, wsFactoryMock.Object);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var events = new List<ProtectEvent>();
        await foreach (var e in stream.SubscribeAsync(cts.Token))
            events.Add(e);

        // Assert
        Assert.IsEmpty(events);
        wsFactoryMock.Verify(f => f.Create(It.IsAny<string>()), Times.Never());
    }

    [TestMethod]
    public async Task SubscribeAsync_ConnectFails_RetriesAndYieldsNoEventsAfterCancel()
    {
        // Arrange
        var options = Options.Create(new UnifiProtectOptions { BaseUrl = "https://host", ApiKey = "key" });
        var wsFactoryMock = new Mock<IWebSocketFactory>();
        var wsMock = new Mock<IWebSocketConnection>();
        wsFactoryMock.Setup(f => f.Create(It.IsAny<string>())).Returns(wsMock.Object);

        wsMock.SetupGet(w => w.State).Returns(WebSocketState.Closed);
        wsMock.Setup(w => w.ConnectAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
              .Returns(Task.FromException(new System.Net.WebSockets.WebSocketException("refused")));

        var stream = new ProtectEventStream(options, wsFactoryMock.Object);
        using var cts = new CancellationTokenSource();

        // Cancel after first failed connect + delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(200);
            cts.Cancel();
        });

        var events = new List<ProtectEvent>();
        await foreach (var e in stream.SubscribeAsync(cts.Token))
            events.Add(e);

        Assert.IsEmpty(events);
    }

    [TestMethod]
    public async Task SubscribeAsync_ConnectCancelledImmediately_YieldsNoEvents()
    {
        // Arrange
        var options = Options.Create(new UnifiProtectOptions { BaseUrl = "https://host", ApiKey = "key" });
        var wsFactoryMock = new Mock<IWebSocketFactory>();
        var wsMock = new Mock<IWebSocketConnection>();
        wsFactoryMock.Setup(f => f.Create(It.IsAny<string>())).Returns(wsMock.Object);

        using var cts = new CancellationTokenSource();

        wsMock.Setup(w => w.ConnectAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
              .Returns((Uri _, CancellationToken ct) =>
              {
                  cts.Cancel();
                  return Task.FromCanceled(ct);
              });

        var stream = new ProtectEventStream(options, wsFactoryMock.Object);

        var events = new List<ProtectEvent>();
        await foreach (var e in stream.SubscribeAsync(cts.Token))
            events.Add(e);

        Assert.IsEmpty(events);
    }

    [TestMethod]
    public async Task SubscribeAsync_ConnectsAndReceivesEvent_YieldsEvent()
    {
        // Arrange
        var options = Options.Create(new UnifiProtectOptions { BaseUrl = "https://host", ApiKey = "key" });
        var wsFactoryMock = new Mock<IWebSocketFactory>();
        var wsMock = new Mock<IWebSocketConnection>();
        wsFactoryMock.Setup(f => f.Create(It.IsAny<string>())).Returns(wsMock.Object);

        wsMock.Setup(w => w.ConnectAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Build a ring-event JSON payload
        const string json = """{"type":"add","item":{"id":"ev1","type":"ring","start":1000,"device":"dev1"}}""";
        var bytes = Encoding.UTF8.GetBytes(json);

        var callCount = 0;
        wsMock.SetupGet(w => w.State).Returns(() => callCount == 0 ? WebSocketState.Open : WebSocketState.Closed);
        wsMock.Setup(w => w.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
              .Returns((Memory<byte> buffer, CancellationToken _) =>
              {
                  bytes.CopyTo(buffer);
                  callCount++;
                  return new ValueTask<ValueWebSocketReceiveResult>(
                      new ValueWebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, true));
              });

        var stream = new ProtectEventStream(options, wsFactoryMock.Object);
        using var cts = new CancellationTokenSource(5000);

        // Act
        var events = new List<ProtectEvent>();
        await foreach (var e in stream.SubscribeAsync(cts.Token))
        {
            events.Add(e);
            break; // stop after first event
        }

        // Assert
        Assert.ContainsSingle(events);
        Assert.IsInstanceOfType<RingEvent>(events[0]);
    }

    [TestMethod]
    public async Task SubscribeAsync_ReceivesCloseMessage_ReconnectsWithBackoff()
    {
        // Arrange
        var options = Options.Create(new UnifiProtectOptions { BaseUrl = "https://host", ApiKey = "key" });
        var wsFactoryMock = new Mock<IWebSocketFactory>();
        var wsMock = new Mock<IWebSocketConnection>();
        wsFactoryMock.Setup(f => f.Create(It.IsAny<string>())).Returns(wsMock.Object);

        wsMock.Setup(w => w.ConnectAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var callCount = 0;
        wsMock.SetupGet(w => w.State).Returns(WebSocketState.Open);
        wsMock.Setup(w => w.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
              .Returns(() =>
              {
                  callCount++;
                  return new ValueTask<ValueWebSocketReceiveResult>(
                      new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, true));
              });

        var stream = new ProtectEventStream(options, wsFactoryMock.Object);
        using var cts = new CancellationTokenSource(300);

        var events = new List<ProtectEvent>();
        await foreach (var e in stream.SubscribeAsync(cts.Token))
            events.Add(e);

        // Close message causes yield break → retry loop → eventually cancelled
        Assert.IsEmpty(events);
        Assert.IsTrue(callCount > 0, "At least one receive call was made");
    }
}
