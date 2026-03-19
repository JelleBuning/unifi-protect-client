using LibVLCSharp.Shared;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace UnifiProtectClient.Services;

public sealed class RtspVideoPlayer : IDisposable
{
    static RtspVideoPlayer()
    {
        // Point LibVLCSharp at the native DLLs deployed by VideoLAN.LibVLC.Windows
        // (layout: <AppDir>\libvlc\win-x64\libvlc.dll + plugins\)
        string arch = Environment.Is64BitProcess ? "win-x64" : "win-x86";
        string vlcDir = Path.Combine(AppContext.BaseDirectory, "libvlc", arch);
        Core.Initialize(vlcDir);
    }

    private readonly LibVLC _libVlc;
    private readonly string _url;
    private MediaPlayer? _mediaPlayer;
    private Media? _media;

    private byte[]? _pixelBuffer;
    private GCHandle _bufferHandle;
    private int _width;
    private int _height;

    private CancellationTokenSource _cts = new();
    private bool _stopped;
    private int _reconnectPending; // 0 = idle, 1 = reconnect scheduled (Interlocked)
    private DateTime _connectingStart;

    // Keep delegates alive to prevent GC collection while VLC is using them
    private MediaPlayer.LibVLCVideoFormatCb? _formatCb;
    private MediaPlayer.LibVLCVideoCleanupCb? _cleanupCb;
    private MediaPlayer.LibVLCVideoLockCb? _lockCb;
    private MediaPlayer.LibVLCVideoUnlockCb? _unlockCb;
    private MediaPlayer.LibVLCVideoDisplayCb? _displayCb;

    public event EventHandler<VideoFrame>? FrameReady;
    public event EventHandler<string>? StatusChanged;

    public RtspVideoPlayer(string url)
    {
        _url = url;
        _libVlc = new LibVLC(enableDebugLogs: false);

        // Capture VLC errors and surface them in the status
        _libVlc.Log += (_, args) =>
        {
            if (args.Level == LogLevel.Error)
                Debug.WriteLine($"[VLC Error] {args.FormattedLog}");
        };

        // Auto-accept all VLC question dialogs (handles self-signed TLS cert prompts)
        _libVlc.SetDialogHandlers(
            error: (title, text) =>
            {
                StatusChanged?.Invoke(this, text ?? title ?? "Unknown error");
                return Task.CompletedTask;
            },
            login: (dialog, title, text, defaultUsername, askStore, token) =>
            {
                dialog.Dismiss();
                return Task.CompletedTask;
            },
            question: (dialog, title, text, type, cancel, first, second, token) =>
            {
                // PostAction(1) = accept the first option (e.g. "Accept certificate")
                dialog.PostAction(1);
                return Task.CompletedTask;
            },
            displayProgress: (dialog, title, text, indeterminate, position, cancelText, token) =>
                Task.CompletedTask,
            updateProgress: (dialog, position, text) =>
                Task.CompletedTask
        );
    }

    public void Start()
    {
        TearDownPlayer();
        _stopped = false;
        Interlocked.Exchange(ref _reconnectPending, 0);
        _cts = new CancellationTokenSource();
        StartInternal();
    }

    private void StartInternal()
    {
        if (_stopped) return;

        _connectingStart = DateTime.UtcNow;
        StatusChanged?.Invoke(this, "Connecting...");

        _media = new Media(_libVlc, new Uri(_url));
        _media.AddOption(":rtsp-tcp");              // Force TCP transport (more reliable than UDP)
        _media.AddOption(":live-caching=300");      // Low-latency buffer for live streams
        _media.AddOption(":network-caching=1000");  // Network jitter buffer
        _media.AddOption(":clock-jitter=0");
        _media.AddOption(":clock-synchro=0");
        _media.AddOption(":no-audio");              // Disable audio (not needed for camera feed)

        _mediaPlayer = new MediaPlayer(_media);

        // Store delegates as fields so they aren't garbage collected
        _formatCb = OnVideoFormat;
        _cleanupCb = OnVideoCleanup;
        _lockCb = OnLock;
        _unlockCb = OnUnlock;
        _displayCb = OnDisplay;

        _mediaPlayer.SetVideoFormatCallbacks(_formatCb, _cleanupCb);
        _mediaPlayer.SetVideoCallbacks(_lockCb, _unlockCb, _displayCb);

        _mediaPlayer.Playing += (_, _) =>
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
        };
        _mediaPlayer.Buffering += (_, _) => { };
        _mediaPlayer.EncounteredError += (_, _) => ScheduleReconnect("Playback error");
        _mediaPlayer.EndReached += (_, _) => ScheduleReconnect("Stream ended");

        _mediaPlayer.Play();
    }

    private void ScheduleReconnect(string reason)
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

    private void TearDownPlayer()
    {
        _mediaPlayer?.Stop();
        _mediaPlayer?.Dispose();
        _mediaPlayer = null;
        _media?.Dispose();
        _media = null;
        ReleaseBuffer();
    }

    // Called by VLC once it detects the video format. Returns number of picture buffers.
    private uint OnVideoFormat(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height,
        ref uint pitches, ref uint lines)
    {
        _width = (int)width;
        _height = (int)height;

        // Write "RV32" (32-bit BGRA in VLC's memory layout = WinUI 3 WriteableBitmap BGRA8)
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

    private void OnVideoCleanup(ref IntPtr opaque)
    {
        ReleaseBuffer();
    }

    private IntPtr OnLock(IntPtr opaque, IntPtr planes)
    {
        // planes[0] = pointer to our pixel buffer
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
        Stop();
        _libVlc.Dispose();
    }
}

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

