using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using UnifiProtectClient.Application.Options;
using UnifiProtectClient.Application.Ports;
using UnifiProtectClient.Domain.Events;
using UnifiProtectClient.Services;
using UnifiProtectClient.Services.Interfaces;
using UnifiProtectClient.Views;

namespace UnifiProtectClient.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly MainWindow _mainWindow;
    private readonly IUnifiProtectApiClient _apiClient;
    private readonly IProtectEventStream _eventStream;
    private readonly IDesktopNotifier _notifier;
    private readonly EventNotificationSettings _eventSettings;
    private readonly SnapshotService _snapshotService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly CancellationTokenSource _cts = new();

    private WriteableBitmap? _videoBitmap;
    private bool _updatePending;
    private RtspVideoPlayer? _player;
    private string? _cameraName;

    public WriteableBitmap? VideoSource
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string StatusMessage
    {
        get;
        private set => SetProperty(ref field, value);
    } = "Initializing...";

    public MainViewModel(
        MainWindow mainWindow,
        IUnifiProtectApiClient apiClient,
        IProtectEventStream eventStream,
        IDesktopNotifier notifier,
        IOptions<UnifiProtectOptions> options,
        EventNotificationSettings eventSettings,
        DispatcherQueue dispatcherQueue)
    {
        _mainWindow = mainWindow;
        _apiClient = apiClient;
        _eventStream = eventStream;
        _notifier = notifier;
        _eventSettings = eventSettings;
        _dispatcherQueue = dispatcherQueue;

        var snapshotPath = options.Value.SnapshotPath
            ?? Path.Combine(AppContext.BaseDirectory, "snapshots", "snapshot.jpg");
        _snapshotService = new SnapshotService(snapshotPath);

        _ = InitializeCameraAsync(_cts.Token);
        _ = SubscribeToEventsAsync(_cts.Token);
    }

    private async Task InitializeCameraAsync(CancellationToken ct)
    {
        try
        {
            UpdateStatus("Discovering camera...");

            var cameras = await _apiClient.GetCamerasAsync(ct);
            var camera  = cameras.FirstOrDefault(c => c.IsConnected)
                          ?? throw new InvalidOperationException("No connected camera found.");

            _cameraName = camera.Name;
            UpdateStatus($"Found camera: {camera.Name}");

            var streams = await _apiClient.GetRtspsStreamsAsync(camera.Id, ct);
            var stream  = streams.FirstOrDefault()
                          ?? await _apiClient.CreateRtspsStreamAsync(camera.Id, ct);

            // LibVLC 3.x cannot handle RTSPS (TLS) or SRTP.
            // Convert to plain RTSP on the unencrypted media port (7447).
            var url = stream.Url
                .Replace("rtsps://", "rtsp://")
                .Replace(":7441/", ":7447/")
                .Replace("?enableSrtp", "")
                .TrimEnd('?');

            _player = new RtspVideoPlayer(url);
            _player.FrameReady    += OnFrameReady;
            _player.StatusChanged += OnStatusChanged;
            _player.Start();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainViewModel] Camera init failed: {ex.Message}");
            UpdateStatus($"Error: {ex.Message}");
        }
    }

    private async Task SubscribeToEventsAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var @event in _eventStream.SubscribeAsync(ct))
            {
                if (_eventSettings.IsEnabled(@event) && IsNotifiableEvent(@event))
                    _notifier.Notify(@event, _cameraName ?? "Unknown Camera");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainViewModel] Event stream error: {ex.Message}");
        }
    }

    // Notify on Add events for all types, and also on Update events for ring events
    // that have no End timestamp yet (ring is starting, not ending). The integration
    // API at /proxy/protect/integration emits ring events as Update, not Add.
    private static bool IsNotifiableEvent(ProtectEvent @event) =>
        @event.UpdateType == ProtectEventUpdateType.Add ||
        @event is RingEvent { End: null };

    private void OnStatusChanged(object? sender, string message) => UpdateStatus(message);

    private void UpdateStatus(string message) =>
        _dispatcherQueue.TryEnqueue(() => StatusMessage = message);

    private void OnFrameReady(object? sender, VideoFrame frame)
    {
        if (_updatePending)
        {
            frame.Dispose();
            return;
        }

        _updatePending = true;
        bool queued = _dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                EnsureBitmap(frame.Width, frame.Height);
                using var stream = _videoBitmap!.PixelBuffer.AsStream();
                stream.Write(frame.Pixels, 0, frame.DataLength);
                _videoBitmap.Invalidate();
                _snapshotService.CaptureFrame(frame.Width, frame.Height, frame.Pixels);
            }
            finally
            {
                frame.Dispose();
                _updatePending = false;
            }
        });

        if (!queued)
        {
            frame.Dispose();
            _updatePending = false;
        }
    }

    private void EnsureBitmap(int width, int height)
    {
        if (_videoBitmap is null || _videoBitmap.PixelWidth != width || _videoBitmap.PixelHeight != height)
        {
            _videoBitmap = new WriteableBitmap(width, height);
            VideoSource = _videoBitmap;
        }
    }

    [RelayCommand]
    public void LeftClick() => _mainWindow.BringToFront();

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();

        if (_player is not null)
        {
            _player.FrameReady    -= OnFrameReady;
            _player.StatusChanged -= OnStatusChanged;
            _player.Dispose();
        }

        _snapshotService.Dispose();
    }
}