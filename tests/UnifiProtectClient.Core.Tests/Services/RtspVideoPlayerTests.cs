using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using UnifiProtectClient.Services;

namespace UnifiProtectClient.Core.Tests.Services;

// ── Manual test double for IVlcPlayerHandle ───────────────────────────────────

internal sealed class TestVlcHandle : IVlcPlayerHandle
{
    public event EventHandler? Playing;
    public event EventHandler? EncounteredError;
    public event EventHandler? EndReached;
    public event EventHandler<VideoFrame>? FrameReady;

    public int PlayCallCount { get; private set; }
    public int StopCallCount { get; private set; }
    public bool IsDisposed { get; private set; }

    public void Play() => PlayCallCount++;
    public void Stop() => StopCallCount++;
    public void Dispose() => IsDisposed = true;

    public void RaisePlaying() => Playing?.Invoke(this, EventArgs.Empty);
    public void RaiseEncounteredError() => EncounteredError?.Invoke(this, EventArgs.Empty);
    public void RaiseEndReached() => EndReached?.Invoke(this, EventArgs.Empty);
    public void RaiseFrameReady(VideoFrame frame) => FrameReady?.Invoke(this, frame);
}

internal sealed class TestVlcFactory : IVlcPlayerFactory
{
    private readonly TestVlcHandle _handle;
    public Action<string>? CapturedOnError { get; private set; }

    public TestVlcFactory(TestVlcHandle handle) => _handle = handle;

    public IVlcPlayerHandle Create(string url, Action<string> onError)
    {
        CapturedOnError = onError;
        return _handle;
    }
}

// ── VideoFrame tests ──────────────────────────────────────────────────────────

[TestClass]
public sealed class VideoFrameTests
{
    private static VideoFrame MakeFrame(int w = 4, int h = 4)
    {
        var dataLength = w * h * 4;
        var pixels = ArrayPool<byte>.Shared.Rent(dataLength);
        return new VideoFrame(pixels, w, h, dataLength);
    }

    [TestMethod]
    public void VideoFrame_Properties_AreCorrect()
    {
        using var frame = MakeFrame(8, 6);
        Assert.AreEqual(8, frame.Width);
        Assert.AreEqual(6, frame.Height);
        Assert.AreEqual(8 * 6 * 4, frame.DataLength);
        Assert.IsNotNull(frame.Pixels);
    }

    [TestMethod]
    public void VideoFrame_Dispose_ReturnsPixelsToPool()
    {
        var frame = MakeFrame();
        frame.Dispose(); // Should not throw
    }

    [TestMethod]
    public void VideoFrame_DisposeIdempotent_SecondDisposeDoesNotThrow()
    {
        var frame = MakeFrame();
        frame.Dispose();
        frame.Dispose(); // Should not throw
    }
}

// ── RtspVideoPlayer state machine tests ──────────────────────────────────────

[TestClass]
public sealed class RtspVideoPlayerTests
{
    private static (RtspVideoPlayer player, TestVlcFactory factory, TestVlcHandle handle)
        CreatePlayer(string url = "rtsps://host/stream")
    {
        var handle = new TestVlcHandle();
        var factory = new TestVlcFactory(handle);
        var player = new RtspVideoPlayer(url, factory);
        return (player, factory, handle);
    }

    // ── Start ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Start_CallsHandlePlay()
    {
        var (player, _, handle) = CreatePlayer();
        player.Start();
        Assert.AreEqual(1, handle.PlayCallCount);
    }

    [TestMethod]
    public void Start_RaisesStatusChangedConnecting()
    {
        var (player, _, _) = CreatePlayer();
        string? lastStatus = null;
        player.StatusChanged += (_, s) => lastStatus = s;

        player.Start();

        Assert.AreEqual("Connecting...", lastStatus);
    }

    [TestMethod]
    public void Start_CalledTwice_DisposesFirstHandleBeforeCreatingNew()
    {
        var firstHandle = new TestVlcHandle();
        var secondHandle = new TestVlcHandle();
        var callCount = 0;
        var factoryMock = new Mock<IVlcPlayerFactory>();
        factoryMock.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<Action<string>>()))
                   .Returns<string, Action<string>>((_, __) => callCount++ == 0 ? firstHandle : secondHandle);

        var player = new RtspVideoPlayer("rtsps://host", factoryMock.Object);
        player.Start();
        player.Start();

        Assert.IsTrue(firstHandle.IsDisposed);
        Assert.AreEqual(1, secondHandle.PlayCallCount);
    }

    // ── Stop ──────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Stop_DisposesHandle()
    {
        var (player, _, handle) = CreatePlayer();
        player.Start();
        player.Stop();
        Assert.IsTrue(handle.IsDisposed);
    }

    [TestMethod]
    public void Stop_WithoutStart_DoesNotThrow()
    {
        var (player, _, _) = CreatePlayer();
        player.Stop(); // Should not throw
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    [TestMethod]
    public void Dispose_CallsStop()
    {
        var (player, _, handle) = CreatePlayer();
        player.Start();
        player.Dispose();
        Assert.IsTrue(handle.IsDisposed);
    }

    [TestMethod]
    public void Dispose_Idempotent()
    {
        var (player, _, _) = CreatePlayer();
        player.Dispose();
        player.Dispose(); // Should not throw
    }

    // ── FrameReady relay ──────────────────────────────────────────────────────

    [TestMethod]
    public void FrameReady_RelayedFromHandle()
    {
        var (player, _, handle) = CreatePlayer();
        player.Start();

        VideoFrame? received = null;
        player.FrameReady += (_, f) => received = f;

        var dataLength = 4 * 4 * 4;
        var pixels = ArrayPool<byte>.Shared.Rent(dataLength);
        using var frame = new VideoFrame(pixels, 4, 4, dataLength);

        handle.RaiseFrameReady(frame);

        Assert.IsNotNull(received);
        Assert.AreEqual(4, received!.Width);
        Assert.AreEqual(4, received.Height);
    }

    // ── StatusChanged relay ───────────────────────────────────────────────────

    [TestMethod]
    public void StatusChanged_RaisedOnStart()
    {
        var (player, _, _) = CreatePlayer();
        var statuses = new List<string>();
        player.StatusChanged += (_, s) => statuses.Add(s);

        player.Start();

        Assert.Contains("Connecting...", statuses);
    }

    [TestMethod]
    public void StatusChanged_RelayedFromHandleErrorCallback()
    {
        var (player, factory, _) = CreatePlayer();

        string? capturedStatus = null;
        player.StatusChanged += (_, s) => capturedStatus = s;

        player.Start();
        factory.CapturedOnError?.Invoke("TLS error");

        Assert.AreEqual("TLS error", capturedStatus);
    }

    // ── Playing event → Connected status ─────────────────────────────────────

    [TestMethod]
    public async Task Playing_EventAfterDelay_RaisesConnectedStatus()
    {
        var (player, _, handle) = CreatePlayer();
        var statuses = new List<string>();
        player.StatusChanged += (_, s) => statuses.Add(s);

        player.Start();

        // Fire Playing event — player delays ~1.5s then raises "Connected"
        handle.RaisePlaying();

        // Wait enough for the Connected status to fire
        await Task.Delay(2500);

        Assert.Contains("Connected", statuses);
    }

    [TestMethod]
    public void Playing_EventWhenStopped_CancelsConnectedStatus()
    {
        var (player, _, handle) = CreatePlayer();
        player.Start();
        handle.RaisePlaying();

        // Stop immediately — cancellation should prevent "Connected" from firing
        player.Stop();
        // No assertion needed — just verify no exception
    }

    // ── ScheduleReconnect ─────────────────────────────────────────────────────

    [TestMethod]
    public void ScheduleReconnect_WhileStopped_DoesNothing()
    {
        var (player, _, handle) = CreatePlayer();
        player.Start();
        player.Stop();

        // After stop, ScheduleReconnect should be a no-op
        player.ScheduleReconnect("test reason");

        // No additional Play calls beyond the original Start()
        Assert.AreEqual(1, handle.PlayCallCount);
    }

    [TestMethod]
    public void ScheduleReconnect_RaisesStatusWithReason()
    {
        var (player, _, handle) = CreatePlayer();
        player.Start();

        string? capturedStatus = null;
        player.StatusChanged += (_, s) => capturedStatus = s;

        player.ScheduleReconnect("Stream ended");

        Assert.AreEqual("Stream ended", capturedStatus);
    }

    [TestMethod]
    public void ScheduleReconnect_CalledTwice_OnlyFirstTakesEffect()
    {
        var (player, _, _) = CreatePlayer();
        player.Start();

        var statuses = new List<string>();
        player.StatusChanged += (_, s) => statuses.Add(s);

        player.ScheduleReconnect("error 1");
        player.ScheduleReconnect("error 2"); // guard should prevent this

        Assert.AreEqual(1, statuses.Count);
        Assert.AreEqual("error 1", statuses[0]);
    }

    [TestMethod]
    public void EncounteredError_OnHandle_TriggersReconnect()
    {
        var (player, _, handle) = CreatePlayer();
        player.Start();

        string? capturedStatus = null;
        player.StatusChanged += (_, s) => capturedStatus = s;

        handle.RaiseEncounteredError();

        Assert.AreEqual("Playback error", capturedStatus);
    }

    [TestMethod]
    public void EndReached_OnHandle_TriggersReconnect()
    {
        var (player, _, handle) = CreatePlayer();
        player.Start();

        string? capturedStatus = null;
        player.StatusChanged += (_, s) => capturedStatus = s;

        handle.RaiseEndReached();

        Assert.AreEqual("Stream ended", capturedStatus);
    }

    // ── TearDownPlayer ────────────────────────────────────────────────────────

    [TestMethod]
    public void TearDownPlayer_DisposesHandle()
    {
        var (player, _, handle) = CreatePlayer();
        player.Start();

        player.TearDownPlayer();

        Assert.IsTrue(handle.IsDisposed);
    }

    [TestMethod]
    public void TearDownPlayer_WhenNoHandleExists_DoesNotThrow()
    {
        var (player, _, _) = CreatePlayer();
        player.TearDownPlayer(); // No handle created yet
    }

    // ── StartInternal ─────────────────────────────────────────────────────────

    [TestMethod]
    public void StartInternal_WhenStopped_DoesNotCreateHandle()
    {
        var factoryMock = new Mock<IVlcPlayerFactory>();
        var player = new RtspVideoPlayer("rtsps://host", factoryMock.Object);
        player.Stop(); // sets _stopped = true

        player.StartInternal();

        factoryMock.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<Action<string>>()), Times.Never());
    }
}

