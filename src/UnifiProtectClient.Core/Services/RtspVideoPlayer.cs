using LibVLCSharp.Shared;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace UnifiProtectClient.Services;

// ── Abstraction ──────────────────────────────────────────────────────────────

internal interface IVlcPlayerHandle : IDisposable
{
    event EventHandler? Playing;
    event EventHandler? EncounteredError;
    event EventHandler? EndReached;
    event EventHandler<VideoFrame>? FrameReady;
    void Play();
    void Stop();
}

internal interface IVlcPlayerFactory
{
    IVlcPlayerHandle Create(string url, Action<string> onError);
}

// ── State machine (fully testable) ───────────────────────────────────────────

public sealed class RtspVideoPlayer : IDisposable
{
    private readonly string _url;
    private readonly IVlcPlayerFactory _factory;
    private IVlcPlayerHandle? _handle;
    private CancellationTokenSource _cts = new();
    private bool _stopped;
    private int _reconnectPending; // 0 = idle, 1 = reconnect scheduled (Interlocked)
    private DateTime _connectingStart;

    public event EventHandler<VideoFrame>? FrameReady;
    public event EventHandler<string>? StatusChanged;

    public RtspVideoPlayer(string url)
        : this(url, new DefaultVlcPlayerFactory()) { }

    internal RtspVideoPlayer(string url, IVlcPlayerFactory factory)
    {
        _url = url;
        _factory = factory;
    }

    public void Start()
    {
        TearDownPlayer();
        _stopped = false;
        Interlocked.Exchange(ref _reconnectPending, 0);
        _cts = new CancellationTokenSource();
        StartInternal();
    }

    internal void StartInternal()
    {
        if (_stopped) return;

        _connectingStart = DateTime.UtcNow;
        StatusChanged?.Invoke(this, "Connecting...");

        _handle = _factory.Create(_url, msg => StatusChanged?.Invoke(this, msg));
        _handle.Playing += OnHandlePlaying;
        _handle.EncounteredError += (_, _) => ScheduleReconnect("Playback error");
        _handle.EndReached += (_, _) => ScheduleReconnect("Stream ended");
        _handle.FrameReady += (_, frame) => FrameReady?.Invoke(this, frame);
        _handle.Play();
    }

    private void OnHandlePlaying(object? sender, EventArgs e)
    {
        var token = _cts.Token;
        Task.Run(async () =>
        {
            try
            {
                var elapsed = DateTime.UtcNow - _connectingStart;
                var remaining = TimeSpan.FromMilliseconds(1500) - elapsed;
                if (remaining > TimeSpan.Zero)
                    await Task.Delay(remaining, token);
                StatusChanged?.Invoke(this, "Connected");
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    internal void ScheduleReconnect(string reason)
    {
        if (_stopped) return;

        // Guard: only one reconnect task at a time (EncounteredError + EndReached can both fire)
        if (Interlocked.CompareExchange(ref _reconnectPending, 1, 0) != 0) return;

        StatusChanged?.Invoke(this, reason);

        var token = _cts.Token;
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), token);
                TearDownPlayer();
                Interlocked.Exchange(ref _reconnectPending, 0);
                StartInternal();
            }
            catch (OperationCanceledException)
            {
                Interlocked.Exchange(ref _reconnectPending, 0);
            }
        }, token);
    }

    public void Stop()
    {
        _stopped = true;
        _cts.Cancel();
        TearDownPlayer();
    }

    internal void TearDownPlayer()
    {
        _handle?.Dispose();
        _handle = null;
    }

    public void Dispose() => Stop();
}

// ── VLC infrastructure (excluded from coverage — requires native LibVLC) ────

[ExcludeFromCodeCoverage]
internal sealed class DefaultVlcPlayerFactory : IVlcPlayerFactory
{
    static DefaultVlcPlayerFactory()
    {
        // Point LibVLCSharp at the native DLLs deployed by VideoLAN.LibVLC.Windows
        // (layout: <AppDir>\libvlc\win-x64\libvlc.dll + plugins\)
        string arch = Environment.Is64BitProcess ? "win-x64" : "win-x86";
        string vlcDir = Path.Combine(AppContext.BaseDirectory, "libvlc", arch);
        Core.Initialize(vlcDir);
    }

    public IVlcPlayerHandle Create(string url, Action<string> onError) =>
        new VlcPlayerHandle(url, onError);
}

[ExcludeFromCodeCoverage]
internal sealed class VlcPlayerHandle : IVlcPlayerHandle
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _mediaPlayer;
    private readonly Media _media;

    private byte[]? _pixelBuffer;
    private GCHandle _bufferHandle;
    private int _width;
    private int _height;

    // Keep delegates alive to prevent GC collection while VLC is using them
    private readonly MediaPlayer.LibVLCVideoFormatCb _formatCb;
    private readonly MediaPlayer.LibVLCVideoCleanupCb _cleanupCb;
    private readonly MediaPlayer.LibVLCVideoLockCb _lockCb;
    private readonly MediaPlayer.LibVLCVideoUnlockCb _unlockCb;
    private readonly MediaPlayer.LibVLCVideoDisplayCb _displayCb;

    public event EventHandler? Playing;
    public event EventHandler? EncounteredError;
    public event EventHandler? EndReached;
    public event EventHandler<VideoFrame>? FrameReady;

    public VlcPlayerHandle(string url, Action<string> onError)
    {
        _libVlc = new LibVLC(enableDebugLogs: false);

        _libVlc.Log += (_, args) =>
        {
            if (args.Level == LogLevel.Error)
                Debug.WriteLine($"[VLC Error] {args.FormattedLog}");
        };

        _libVlc.SetDialogHandlers(
            error: (title, text) =>
            {
                onError(text ?? title ?? "Unknown error");
                return Task.CompletedTask;
            },
            login: (dialog, title, text, defaultUsername, askStore, token) =>
            {
                dialog.Dismiss();
                return Task.CompletedTask;
            },
            question: (dialog, title, text, type, cancel, first, second, token) =>
            {
                dialog.PostAction(1);
                return Task.CompletedTask;
            },
            displayProgress: (dialog, title, text, indeterminate, position, cancelText, token) =>
                Task.CompletedTask,
            updateProgress: (dialog, position, text) =>
                Task.CompletedTask
        );

        _media = new Media(_libVlc, new Uri(url));
        _media.AddOption(":rtsp-tcp");
        _media.AddOption(":live-caching=300");
        _media.AddOption(":network-caching=1000");
        _media.AddOption(":clock-jitter=0");
        _media.AddOption(":clock-synchro=0");
        _media.AddOption(":no-audio");

        _mediaPlayer = new MediaPlayer(_media);

        _formatCb = OnVideoFormat;
        _cleanupCb = OnVideoCleanup;
        _lockCb = OnLock;
        _unlockCb = OnUnlock;
        _displayCb = OnDisplay;

        _mediaPlayer.SetVideoFormatCallbacks(_formatCb, _cleanupCb);
        _mediaPlayer.SetVideoCallbacks(_lockCb, _unlockCb, _displayCb);

        _mediaPlayer.Playing += (s, e) => Playing?.Invoke(s, e);
        _mediaPlayer.Buffering += (_, _) => { };
        _mediaPlayer.EncounteredError += (s, e) => EncounteredError?.Invoke(s, e);
        _mediaPlayer.EndReached += (s, e) => EndReached?.Invoke(s, e);
    }

    public void Play() => _mediaPlayer.Play();

    public void Stop() => _mediaPlayer.Stop();

    private uint OnVideoFormat(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height,
        ref uint pitches, ref uint lines)
    {
        _width = (int)width;
        _height = (int)height;

        Marshal.WriteByte(chroma, 0, (byte)'R');
        Marshal.WriteByte(chroma, 1, (byte)'V');
        Marshal.WriteByte(chroma, 2, (byte)'3');
        Marshal.WriteByte(chroma, 3, (byte)'2');

        pitches = width * 4;
        lines = height;

        ReleaseBuffer();
        _pixelBuffer = new byte[width * height * 4];
        _bufferHandle = GCHandle.Alloc(_pixelBuffer, GCHandleType.Pinned);

        return 1;
    }

    private void OnVideoCleanup(ref IntPtr opaque) => ReleaseBuffer();

    private IntPtr OnLock(IntPtr opaque, IntPtr planes)
    {
        if (_bufferHandle.IsAllocated)
            Marshal.WriteIntPtr(planes, _bufferHandle.AddrOfPinnedObject());
        return IntPtr.Zero;
    }

    private void OnUnlock(IntPtr opaque, IntPtr picture, IntPtr planes) { }

    private void OnDisplay(IntPtr opaque, IntPtr picture)
    {
        if (_pixelBuffer is null || _width == 0 || _height == 0) return;

        try
        {
            int dataLength = _width * _height * 4;
            byte[] pixels = ArrayPool<byte>.Shared.Rent(dataLength);
            Buffer.BlockCopy(_pixelBuffer, 0, pixels, 0, dataLength);
            FrameReady?.Invoke(this, new VideoFrame(pixels, _width, _height, dataLength));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RtspVideoPlayer] Display error: {ex.Message}");
        }
    }

    private void ReleaseBuffer()
    {
        if (_bufferHandle.IsAllocated)
            _bufferHandle.Free();
        _pixelBuffer = null;
    }

    public void Dispose()
    {
        _mediaPlayer.Stop();
        _mediaPlayer.Dispose();
        _media.Dispose();
        ReleaseBuffer();
        _libVlc.Dispose();
    }
}

// ── VideoFrame ────────────────────────────────────────────────────────────────

public sealed class VideoFrame : IDisposable
{
    private readonly byte[] _pixels;
    private bool _disposed;

    public byte[] Pixels => _pixels;
    public int Width { get; }
    public int Height { get; }
    public int DataLength { get; }

    internal VideoFrame(byte[] pixels, int width, int height, int dataLength)
    {
        _pixels = pixels;
        Width = width;
        Height = height;
        DataLength = dataLength;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ArrayPool<byte>.Shared.Return(_pixels);
    }
}

