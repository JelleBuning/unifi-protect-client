using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnifiProtectClient.Infrastructure.Http;

namespace UnifiProtectClient.Core.Tests.Infrastructure;

/// <summary>Configurable stub for HttpMessageHandler.</summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
        _handler = handler;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(_handler(request));

    public static StubHttpHandler Returning(HttpStatusCode status, string json) =>
        new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
            RequestMessage = new HttpRequestMessage()
        });
}

[TestClass]
public sealed class UnifiProtectApiClientTests
{
    private static UnifiProtectApiClient CreateClient(StubHttpHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://host/api/") };
        return new UnifiProtectApiClient(http);
    }

    // ── GetCamerasAsync ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetCamerasAsync_ValidJson_ReturnsMappedCameras()
    {
        // Arrange
        const string json = """
            [
              {"id":"cam1","name":"Front Door","state":"CONNECTED"},
              {"id":"cam2","name":"Backyard","state":"DISCONNECTED"}
            ]
            """;
        var client = CreateClient(StubHttpHandler.Returning(HttpStatusCode.OK, json));

        // Act
        var cameras = await client.GetCamerasAsync();

        // Assert
        Assert.HasCount(2, cameras);
        Assert.AreEqual("cam1", cameras[0].Id);
        Assert.AreEqual("Front Door", cameras[0].Name);
        Assert.IsTrue(cameras[0].IsConnected);
        Assert.AreEqual("cam2", cameras[1].Id);
        Assert.IsFalse(cameras[1].IsConnected);
    }

    [TestMethod]
    public async Task GetCamerasAsync_EmptyArray_ReturnsEmptyList()
    {
        var client = CreateClient(StubHttpHandler.Returning(HttpStatusCode.OK, "[]"));
        var cameras = await client.GetCamerasAsync();
        Assert.IsEmpty(cameras);
    }

    [TestMethod]
    public async Task GetCamerasAsync_ServerError_ThrowsHttpRequestException()
    {
        var client = CreateClient(StubHttpHandler.Returning(HttpStatusCode.Unauthorized, "Unauthorized"));
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetCamerasAsync());
        Assert.AreEqual(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [TestMethod]
    public async Task GetCamerasAsync_LongErrorBody_TruncatesTo300Chars()
    {
        var longBody = new string('x', 400);
        var client = CreateClient(StubHttpHandler.Returning(HttpStatusCode.InternalServerError, longBody));
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetCamerasAsync());
        // Body truncated to 300 chars + "…"
        Assert.IsTrue(ex.Message.Contains('…'));
    }

    [TestMethod]
    public async Task GetCamerasAsync_InvalidJson_ThrowsInvalidOperationException()
    {
        var client = CreateClient(StubHttpHandler.Returning(HttpStatusCode.OK, "not-json"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetCamerasAsync());
    }

    // ── GetRtspsStreamsAsync ───────────────────────────────────────────────────

    [TestMethod]
    public async Task GetRtspsStreamsAsync_HighQualityPresent_ReturnsHighStream()
    {
        const string json = """{"high":"rtsps://host/high","medium":null,"low":null,"package":null}""";
        var client = CreateClient(StubHttpHandler.Returning(HttpStatusCode.OK, json));

        var streams = await client.GetRtspsStreamsAsync("cam1");

        Assert.ContainsSingle(streams);
        Assert.AreEqual("rtsps://host/high", streams[0].Url);
        Assert.AreEqual("high", streams[0].StreamName);
    }

    [TestMethod]
    public async Task GetRtspsStreamsAsync_NoHighOnlyMedium_ReturnsMediumStream()
    {
        const string json = """{"high":null,"medium":"rtsps://host/medium","low":null,"package":null}""";
        var client = CreateClient(StubHttpHandler.Returning(HttpStatusCode.OK, json));

        var streams = await client.GetRtspsStreamsAsync("cam1");

        Assert.ContainsSingle(streams);
        Assert.AreEqual("medium", streams[0].StreamName);
    }

    [TestMethod]
    public async Task GetRtspsStreamsAsync_OnlyLow_ReturnsLowStream()
    {
        const string json = """{"high":null,"medium":null,"low":"rtsps://host/low","package":null}""";
        var client = CreateClient(StubHttpHandler.Returning(HttpStatusCode.OK, json));

        var streams = await client.GetRtspsStreamsAsync("cam1");

        Assert.ContainsSingle(streams);
        Assert.AreEqual("low", streams[0].StreamName);
    }

    [TestMethod]
    public async Task GetRtspsStreamsAsync_OnlyPackage_ReturnsPackageStream()
    {
        const string json = """{"high":null,"medium":null,"low":null,"package":"rtsps://host/pkg"}""";
        var client = CreateClient(StubHttpHandler.Returning(HttpStatusCode.OK, json));

        var streams = await client.GetRtspsStreamsAsync("cam1");

        Assert.ContainsSingle(streams);
        Assert.AreEqual("package", streams[0].StreamName);
    }

    [TestMethod]
    public async Task GetRtspsStreamsAsync_AllNull_ReturnsEmptyList()
    {
        const string json = """{"high":null,"medium":null,"low":null,"package":null}""";
        var client = CreateClient(StubHttpHandler.Returning(HttpStatusCode.OK, json));

        var streams = await client.GetRtspsStreamsAsync("cam1");
        Assert.IsEmpty(streams);
    }

    [TestMethod]
    public async Task GetRtspsStreamsAsync_ServerError_ThrowsHttpRequestException()
    {
        var client = CreateClient(StubHttpHandler.Returning(HttpStatusCode.NotFound, "Not found"));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetRtspsStreamsAsync("cam1"));
    }

    // ── CreateRtspsStreamAsync ────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateRtspsStreamAsync_SuccessWithHighUrl_ReturnsBestStream()
    {
        const string json = """{"high":"rtsps://host/high","medium":null,"low":null,"package":null}""";
        var client = CreateClient(StubHttpHandler.Returning(HttpStatusCode.OK, json));

        var stream = await client.CreateRtspsStreamAsync("cam1");

        Assert.AreEqual("rtsps://host/high", stream.Url);
        Assert.AreEqual("high", stream.StreamName);
    }

    [TestMethod]
    public async Task CreateRtspsStreamAsync_AllUrlsNull_ThrowsInvalidOperationException()
    {
        const string json = """{"high":null,"medium":null,"low":null,"package":null}""";
        var client = CreateClient(StubHttpHandler.Returning(HttpStatusCode.OK, json));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.CreateRtspsStreamAsync("cam1"));
    }

    [TestMethod]
    public async Task CreateRtspsStreamAsync_ServerError_ThrowsHttpRequestException()
    {
        var client = CreateClient(StubHttpHandler.Returning(HttpStatusCode.BadRequest, "Bad request"));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.CreateRtspsStreamAsync("cam1"));
    }

    [TestMethod]
    public async Task CreateRtspsStreamAsync_WithCancellationToken_PassesToken()
    {
        const string json = """{"high":"rtsps://host/high","medium":null,"low":null,"package":null}""";
        var client = CreateClient(StubHttpHandler.Returning(HttpStatusCode.OK, json));
        using var cts = new CancellationTokenSource();

        var stream = await client.CreateRtspsStreamAsync("cam1", cts.Token);
        Assert.IsNotNull(stream);
    }
}
