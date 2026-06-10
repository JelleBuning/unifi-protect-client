using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnifiProtectClient.Services;

namespace UnifiProtectClient.Core.Tests.Services;

[TestClass]
public sealed class SnapshotServiceTests
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"snapshot-tests-{Guid.NewGuid():N}");

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── GetHeroPath ───────────────────────────────────────────────────────────

    [TestMethod]
    public void GetHeroPath_AddsHeroSuffix()
    {
        var path = @"C:\snapshots\snapshot.jpg";
        var hero = SnapshotService.GetHeroPath(path);
        Assert.AreEqual(@"C:\snapshots\snapshot-hero.jpg", hero);
    }

    [TestMethod]
    public void GetHeroPath_PreservesExtension()
    {
        var path = @"C:\snapshots\frame.png";
        var hero = SnapshotService.GetHeroPath(path);
        Assert.IsTrue(hero.EndsWith("-hero.png"));
    }

    [TestMethod]
    public void GetHeroPath_NestedDirectory_HeroIsInSameDirectory()
    {
        var path = Path.Combine(_tempDir, "sub", "shot.jpg");
        var hero = SnapshotService.GetHeroPath(path);
        Assert.AreEqual(Path.GetDirectoryName(path), Path.GetDirectoryName(hero));
        Assert.IsTrue(Path.GetFileName(hero).StartsWith("shot-hero"));
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    [TestMethod]
    public void Constructor_CreatesDirectory()
    {
        var snapshotPath = Path.Combine(_tempDir, "shots", "snapshot.jpg");
        using var service = new SnapshotService(snapshotPath);
        Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(snapshotPath)));
    }

    // ── CropToLandscape ───────────────────────────────────────────────────────

    [TestMethod]
    public void CropToLandscape_AlreadyLandscape_ReturnsSamePixels()
    {
        // 16:9 image — no crop needed
        int w = 1920, h = 1080;
        var pixels = new byte[w * h * 4];
        new Random(42).NextBytes(pixels);

        var (result, rw, rh) = SnapshotService.CropToLandscape(pixels, w, h);

        Assert.AreSame(pixels, result);
        Assert.AreEqual(w, rw);
        Assert.AreEqual(h, rh);
    }

    [TestMethod]
    public void CropToLandscape_SquareImage_CropsToLandscape()
    {
        int w = 100, h = 100;
        var pixels = new byte[w * h * 4];

        var (result, rw, rh) = SnapshotService.CropToLandscape(pixels, w, h);

        Assert.AreEqual(w, rw);
        Assert.IsTrue(rh < h, "Cropped height should be less than original"); // cropped height < original
        // Verify 16:9 ratio
        Assert.IsTrue(Math.Abs((double)rw / rh - 16.0 / 9.0) < 0.1);
    }

    [TestMethod]
    public void CropToLandscape_PortraitImage_CropsToCenterLandscape()
    {
        // 9:16 portrait
        int w = 90, h = 160;
        var stride = w * 4;
        var pixels = new byte[stride * h];

        // Fill rows with row index so we can verify center crop
        for (var row = 0; row < h; row++)
            for (var col = 0; col < stride; col++)
                pixels[row * stride + col] = (byte)(row % 256);

        var (result, rw, rh) = SnapshotService.CropToLandscape(pixels, w, h);

        Assert.AreEqual(w, rw);
        Assert.IsTrue(rh < h, "Cropped height should be less than original");

        // The first row of result should come from somewhere in the middle
        var cropHeight = (int)(w / (16.0 / 9.0));
        var startY = (h - cropHeight) / 2;
        Assert.AreEqual((byte)(startY % 256), result[0]);
    }

    [TestMethod]
    public void CropToLandscape_ExactAspectRatio_ReturnsSamePixels()
    {
        // Exact 16:9 ratio
        int w = 160, h = 90;
        var pixels = new byte[w * h * 4];
        var (result, rw, rh) = SnapshotService.CropToLandscape(pixels, w, h);
        Assert.AreSame(pixels, result);
        Assert.AreEqual(w, rw);
        Assert.AreEqual(h, rh);
    }

    // ── CaptureFrame (throttle logic) ─────────────────────────────────────────

    [TestMethod]
    public void CaptureFrame_FirstCall_DoesNotThrow()
    {
        var snapshotPath = Path.Combine(_tempDir, "snap.jpg");
        using var service = new SnapshotService(snapshotPath);
        var pixels = new byte[4 * 4 * 4]; // 4x4 BGRA

        // Should not throw even if WinRT encoder is not available
        // (fails silently in background task)
        service.CaptureFrame(4, 4, pixels);
    }

    [TestMethod]
    public void CaptureFrame_ThrottlesConsecutiveCalls()
    {
        var snapshotPath = Path.Combine(_tempDir, "snap.jpg");
        using var service = new SnapshotService(snapshotPath);
        var pixels = new byte[4 * 4 * 4];

        // First call triggers a save
        service.CaptureFrame(4, 4, pixels);

        // Second call immediately after should be throttled (no save in < 5 seconds)
        // This is observable only indirectly; verify no exception
        service.CaptureFrame(4, 4, pixels);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    [TestMethod]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var snapshotPath = Path.Combine(_tempDir, "snap.jpg");
        var service = new SnapshotService(snapshotPath);
        service.Dispose();
        service.Dispose(); // Should not throw
    }
}
