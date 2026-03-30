using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace UnifiProtectClient.Infrastructure.WebSocket;

internal interface IWebSocketConnection : IDisposable
{
    WebSocketState State { get; }
    Task ConnectAsync(Uri uri, CancellationToken ct);
    ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken ct);
}

internal interface IWebSocketFactory
{
    IWebSocketConnection Create(string apiKey);
}

[ExcludeFromCodeCoverage]
internal sealed class ClientWebSocketFactory : IWebSocketFactory
{
    public IWebSocketConnection Create(string apiKey) => new ClientWebSocketWrapper(apiKey);
}

[ExcludeFromCodeCoverage]
internal sealed class ClientWebSocketWrapper : IWebSocketConnection
{
    private readonly ClientWebSocket _inner;

    public ClientWebSocketWrapper(string apiKey)
    {
        _inner = new ClientWebSocket();
        _inner.Options.SetRequestHeader("X-API-KEY", apiKey);
        _inner.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
    }

    public WebSocketState State => _inner.State;

    public Task ConnectAsync(Uri uri, CancellationToken ct) =>
        _inner.ConnectAsync(uri, ct);

    public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken ct) =>
        _inner.ReceiveAsync(buffer, ct);

    public void Dispose() => _inner.Dispose();
}
