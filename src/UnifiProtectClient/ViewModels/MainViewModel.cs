using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.Extensions.Configuration;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using UnifiProtectClient.Services;
using UnifiProtectClient.Views;

namespace UnifiProtectClient.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly MainWindow _mainWindow;
    private readonly RtspVideoPlayer _player;
    private readonly DispatcherQueue _dispatcherQueue;
    private WriteableBitmap? _videoBitmap;
    private bool _updatePending;

    private WriteableBitmap? _videoSource;
    public WriteableBitmap? VideoSource
    {
        get => _videoSource;
        private set => SetProperty(ref _videoSource, value);
    }

    private string _statusMessage = "Initializing...";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public MainViewModel(MainWindow mainWindow, IConfiguration configuration, DispatcherQueue dispatcherQueue)
    {
        _mainWindow = mainWindow;
        _dispatcherQueue = dispatcherQueue;

        var rtspFeed = configuration["RtspFeed"] ?? throw new Exception("RtspFeed not configured");
        _player = new RtspVideoPlayer(rtspFeed);
        _player.FrameReady += OnFrameReady;
        _player.StatusChanged += OnStatusChanged;
    }

    public void StartStream() => _player.Start();

    public void StopStream() => _player.Stop();

    private void OnStatusChanged(object? sender, string message)
    {
        _dispatcherQueue.TryEnqueue(() => StatusMessage = message);
    }

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
    public void LeftClick() => _mainWindow.Show();

    public void Dispose()
    {
        _player.FrameReady -= OnFrameReady;
        _player.StatusChanged -= OnStatusChanged;
        _player.Dispose();
    }
}