using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using UnifiProtectClient.Application.Options;
using UnifiProtectClient.Application.Ports;
using UnifiProtectClient.Domain.Events;

namespace UnifiProtectClient.Infrastructure.WebSocket;

public sealed class ProtectEventStream(IOptions<UnifiProtectOptions> options) : IProtectEventStream
{
    private readonly UnifiProtectOptions _options = options.Value;

    public async IAsyncEnumerable<ProtectEvent> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var wsUri = BuildWebSocketUri();
        Debug.WriteLine($"[ProtectEventStream] Connecting to {wsUri}");

        var backoffMs = 1_000;

        while (!ct.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();
            ws.Options.SetRequestHeader("X-API-KEY", _options.ApiKey);
            ws.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;

            bool connected = false;
            bool cancelled = false;

            try
            {
                await ws.ConnectAsync(wsUri, ct);
                connected = true;
                backoffMs = 1_000;
                Debug.WriteLine($"[ProtectEventStream] Connected to {wsUri}");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                cancelled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProtectEventStream] Connect failed ({ex.GetType().Name}): {ex.Message} — URI: {wsUri}");
            }

            if (cancelled) yield break;

            if (connected)
            {
                await foreach (var ev in ReceiveAsync(ws, ct))
                    yield return ev;

                Debug.WriteLine($"[ProtectEventStream] Disconnected (ws state: {ws.State}), retrying in {backoffMs}ms");
            }

            if (ct.IsCancellationRequested) yield break;

            bool delayCancelled = false;
            try { await Task.Delay(backoffMs, ct); }
            catch (OperationCanceledException) { delayCancelled = true; }

            if (delayCancelled) yield break;

            backoffMs = Math.Min(backoffMs * 2, 30_000);
        }
    }

    private Uri BuildWebSocketUri()
    {
        // Preserve the full path from BaseUrl (e.g. /proxy/protect/api)
        // so the WebSocket URL is wss://host/proxy/protect/api/v1/subscribe/events.
        var baseUri = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        var builder = new UriBuilder(baseUri)
        {
            Scheme = baseUri.Scheme == "https" ? "wss" : "ws"
        };
        return new Uri(builder.Uri, "v1/subscribe/events");
    }

    private static async IAsyncEnumerable<ProtectEvent> ReceiveAsync(
        ClientWebSocket ws,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        var sb = new StringBuilder();

        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            sb.Clear();
            bool endOfMessage;

            do
            {
                var result = await ws.ReceiveAsync(buffer.AsMemory(), ct);
                if (result.MessageType == WebSocketMessageType.Close) yield break;
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                endOfMessage = result.EndOfMessage;
            }
            while (!endOfMessage);

            var ev = ParseEvent(sb.ToString());
            if (ev is not null)
                yield return ev;
        }
    }

    private static ProtectEvent? ParseEvent(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var updateType = root.GetProperty("type").GetString() == "add"
                ? ProtectEventUpdateType.Add
                : ProtectEventUpdateType.Update;

            var item   = root.GetProperty("item");
            var id     = item.GetProperty("id").GetString()     ?? string.Empty;
            var type   = item.GetProperty("type").GetString()   ?? string.Empty;
            var start  = item.GetProperty("start").GetInt64();
            var device = item.GetProperty("device").GetString() ?? string.Empty;

            Debug.WriteLine($"[ProtectEventStream] Event: {updateType} {type} device={device}");

            long? end = item.TryGetProperty("end", out var endProp) && endProp.ValueKind != JsonValueKind.Null
                ? endProp.GetInt64()
                : null;

            var smartTypes = item.TryGetProperty("smartDetectTypes", out var stProp)
                             && stProp.ValueKind == JsonValueKind.Array
                ? stProp.EnumerateArray()
                         .Select(e => e.GetString() ?? string.Empty)
                         .ToList()
                         .AsReadOnly()
                : (IReadOnlyList<string>)[];

            return type switch
            {
                // Camera
                "motion"                => new MotionEvent(id, start, end, device, updateType),
                "smartDetectZone"       => new SmartDetectZoneEvent(id, start, end, device, updateType, smartTypes),
                "smartDetectLine"       => new SmartDetectLineEvent(id, start, end, device, updateType, smartTypes),
                "smartDetectLoiterZone" => new SmartDetectLoiterZoneEvent(id, start, end, device, updateType, smartTypes),
                "smartAudioDetect"      => new SmartAudioDetectEvent(id, start, end, device, updateType, smartTypes),

                // Doorbell
                "ring"                  => new RingEvent(id, start, end, device, updateType),

                // Floodlight
                "lightMotion"           => new LightMotionEvent(id, start, device, updateType),

                // Sensors — simple (no relevant metadata)
                "sensorMotion"          => new SensorMotionEvent(id, start, end, device, updateType),
                "sensorTamper"          => new SensorTamperEvent(id, start, end, device, updateType),
                "sensorSmokeTest"       => new SensorSmokeTestEvent(id, start, end, device, updateType),

                // Sensors — with metadata
                "sensorAlarm"           => ParseSensorAlarm(id, start, end, device, updateType, item),
                "sensorOpened"          => ParseSensorMountType<SensorOpenedEvent>(id, start, end, device, updateType, item,
                                              (i, s, e, d, u, m) => new SensorOpenedEvent(i, s, e, d, u, m)),
                "sensorClosed"          => ParseSensorMountType<SensorClosedEvent>(id, start, end, device, updateType, item,
                                              (i, s, e, d, u, m) => new SensorClosedEvent(i, s, e, d, u, m)),
                "sensorWaterLeak"       => ParseSensorMountType<SensorWaterLeakEvent>(id, start, end, device, updateType, item,
                                              (i, s, e, d, u, m) => new SensorWaterLeakEvent(i, s, e, d, u, m)),
                "sensorBatteryLow"      => ParseSensorBatteryLow(id, start, end, device, updateType, item),
                "sensorExtremeValues"   => ParseSensorExtremeValues(id, start, end, device, updateType, item),

                _                       => new UnknownEvent(id, type, start, end, device, updateType)
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProtectEventStream] Parse error: {ex.Message}");
            Debug.WriteLine($"[ProtectEventStream] Raw JSON: {json[..Math.Min(json.Length, 500)]}");
            return null;
        }
    }

    private static SensorAlarmEvent ParseSensorAlarm(string id, long start, long? end, string device,
        ProtectEventUpdateType updateType, JsonElement item)
    {
        var alarmType = item.TryGetProperty("metadata", out var meta)
                        && meta.TryGetProperty("alarmType", out var at)
                        && at.TryGetProperty("text", out var txt)
            ? txt.GetString() ?? string.Empty
            : string.Empty;
        return new SensorAlarmEvent(id, start, end, device, updateType, alarmType);
    }

    private static T ParseSensorMountType<T>(string id, long start, long? end, string device,
        ProtectEventUpdateType updateType, JsonElement item,
        Func<string, long, long?, string, ProtectEventUpdateType, string, T> factory)
    {
        var mountType = item.TryGetProperty("metadata", out var meta)
                        && meta.TryGetProperty("sensorMountType", out var mt)
                        && mt.TryGetProperty("text", out var txt)
            ? txt.GetString() ?? string.Empty
            : string.Empty;
        return factory(id, start, end, device, updateType, mountType);
    }

    private static SensorBatteryLowEvent ParseSensorBatteryLow(string id, long start, long? end, string device,
        ProtectEventUpdateType updateType, JsonElement item)
    {
        var pct = item.TryGetProperty("metadata", out var meta)
                  && meta.TryGetProperty("sensorBatteryPercentage", out var bp)
                  && bp.TryGetProperty("number", out var num)
            ? num.GetDouble()
            : 0d;
        return new SensorBatteryLowEvent(id, start, end, device, updateType, pct);
    }

    private static SensorExtremeValuesEvent ParseSensorExtremeValues(string id, long start, long? end, string device,
        ProtectEventUpdateType updateType, JsonElement item)
    {
        var sensorType  = string.Empty;
        var sensorValue = 0d;
        var status      = string.Empty;

        if (item.TryGetProperty("metadata", out var meta))
        {
            if (meta.TryGetProperty("sensorType", out var st) && st.TryGetProperty("text", out var stTxt))
                sensorType = stTxt.GetString() ?? string.Empty;
            if (meta.TryGetProperty("sensorValue", out var sv) && sv.TryGetProperty("text", out var svTxt))
                sensorValue = svTxt.GetDouble();
            if (meta.TryGetProperty("status", out var s) && s.TryGetProperty("text", out var sTxt))
                status = sTxt.GetString() ?? string.Empty;
        }

        return new SensorExtremeValuesEvent(id, start, end, device, updateType, sensorType, sensorValue, status);
    }
}
