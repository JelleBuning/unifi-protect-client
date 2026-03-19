using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace UnifiProtectClient.Services;

public sealed class SnapshotService : IDisposable
{
    private readonly string _snapshotPath;
    private readonly string _heroPath;
    private long _nextSaveTicks;
    private int _saving;

    public SnapshotService(string snapshotPath)
    {
        _snapshotPath = snapshotPath;
        _heroPath = GetHeroPath(snapshotPath);
        Directory.CreateDirectory(Path.GetDirectoryName(_snapshotPath)!);
        _nextSaveTicks = DateTime.UtcNow.Ticks;
    }

    /// <summary>Returns the landscape-cropped hero image path derived from the snapshot path.</summary>
    public static string GetHeroPath(string snapshotPath) =>
        Path.Combine(
            Path.GetDirectoryName(snapshotPath)!,
            Path.GetFileNameWithoutExtension(snapshotPath) + "-hero" + Path.GetExtension(snapshotPath));

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
        await SaveJpegAsync(_snapshotPath, width, height, pixels);

        // Save a 16:9 center-cropped version for the toast hero image slot
        var (cropPixels, cropWidth, cropHeight) = CropToLandscape(pixels, width, height);
        await SaveJpegAsync(_heroPath, cropWidth, cropHeight, cropPixels);
    }

    private static async Task SaveJpegAsync(string path, int width, int height, byte[] pixels)
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
        await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        await ras.AsStreamForRead().CopyToAsync(fileStream);
    }

    /// <summary>
    /// Center-crops pixels to a 16:9 landscape rectangle.
    /// If the image is already landscape, returns it unchanged.
    /// </summary>
    private static (byte[] pixels, int width, int height) CropToLandscape(byte[] pixels, int width, int height)
    {
        const double targetAspect = 16.0 / 9.0;
        var currentAspect = (double)width / height;

        if (currentAspect >= targetAspect)
            return (pixels, width, height); // already landscape enough

        // Portrait: keep full width, crop height to 16:9
        int cropHeight = (int)(width / targetAspect);
        int startY = (height - cropHeight) / 2;
        int stride = width * 4;

        var result = new byte[stride * cropHeight];
        Buffer.BlockCopy(pixels, startY * stride, result, 0, result.Length);

        return (result, width, cropHeight);
    }

    public void Dispose() { }
}
