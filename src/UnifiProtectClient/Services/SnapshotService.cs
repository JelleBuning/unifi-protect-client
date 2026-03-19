using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace UnifiProtectClient.Services;

public sealed class SnapshotService : IDisposable
{
    private readonly string _snapshotPath;
    private long _nextSaveTicks;
    private int _saving; // 0 = idle, 1 = in-progress (Interlocked)

    public SnapshotService(string snapshotPath)
    {
        _snapshotPath = snapshotPath;
        Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
        _nextSaveTicks = DateTime.UtcNow.Ticks;
    }

    /// <summary>
    /// Called for every decoded frame. At most one save every 5 seconds is triggered.
    /// Pixel data must be in BGRA32 format.
    /// </summary>
    public void CaptureFrame(int width, int height, byte[] pixels)
    {
        var now = DateTime.UtcNow.Ticks;
        if (now < Interlocked.Read(ref _nextSaveTicks)) return;
        if (Interlocked.CompareExchange(ref _saving, 1, 0) != 0) return;

        Interlocked.Exchange(ref _nextSaveTicks, now + TimeSpan.FromSeconds(5).Ticks);

        var copy = new byte[pixels.Length];
        Buffer.BlockCopy(pixels, 0, copy, 0, pixels.Length);

        _ = Task.Run(async () =>
        {
            try { await SaveAsync(width, height, copy); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Snapshot failed: {ex.Message}"); }
            finally { Interlocked.Exchange(ref _saving, 0); }
        });
    }

    private async Task SaveAsync(int width, int height, byte[] pixels)
    {
        using var ras = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, ras);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Ignore,
            (uint)width, (uint)height,
            96, 96,
            pixels);
        await encoder.FlushAsync();

        ras.Seek(0);
        using var fileStream = new FileStream(_snapshotPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await ras.AsStreamForRead().CopyToAsync(fileStream);
    }

    public void Dispose() { /* no resources to release */ }
}
