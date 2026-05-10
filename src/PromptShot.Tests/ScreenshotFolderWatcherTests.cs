using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using PromptShot.Storage;
using Xunit;

namespace PromptShot.Tests;

public class ScreenshotFolderWatcherTests : IDisposable
{
    private readonly string _dir;

    public ScreenshotFolderWatcherTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "PromptShot.Watcher." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void Triggers_callback_after_stable_window()
    {
        var fired = new ConcurrentQueue<string>();
        var ev = new ManualResetEventSlim();
        using var watcher = new ScreenshotFolderWatcher(
            new[] { _dir },
            path => { fired.Enqueue(path); ev.Set(); },
            stableWindow: TimeSpan.FromMilliseconds(150));

        var target = Path.Combine(_dir, "shot.png");
        File.WriteAllBytes(target, new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        Assert.True(ev.Wait(TimeSpan.FromSeconds(3)),
            "callback was not invoked within 3s after file creation");
        Assert.Contains(target, fired);
    }

    [Fact]
    public void Multiple_writes_collapse_into_single_callback()
    {
        var counter = 0;
        var ev = new ManualResetEventSlim();
        using var watcher = new ScreenshotFolderWatcher(
            new[] { _dir },
            _ => { Interlocked.Increment(ref counter); ev.Set(); },
            stableWindow: TimeSpan.FromMilliseconds(200));

        var target = Path.Combine(_dir, "shot.png");
        File.WriteAllBytes(target, new byte[] { 1 });
        Thread.Sleep(50);
        File.WriteAllBytes(target, new byte[] { 1, 2 });
        Thread.Sleep(50);
        File.WriteAllBytes(target, new byte[] { 1, 2, 3 });

        Assert.True(ev.Wait(TimeSpan.FromSeconds(3)));
        Thread.Sleep(400);
        Assert.Equal(1, Volatile.Read(ref counter));
    }

    [Fact]
    public void Skips_non_existent_folders()
    {
        using var watcher = new ScreenshotFolderWatcher(
            new[] { Path.Combine(_dir, "nope-removed-after-creation") },
            _ => { },
            stableWindow: TimeSpan.FromMilliseconds(50));

        Assert.NotNull(watcher.ResolvedFolders);
    }

    [Fact]
    public void Ignores_non_png_files()
    {
        var ev = new ManualResetEventSlim();
        using var watcher = new ScreenshotFolderWatcher(
            new[] { _dir },
            _ => ev.Set(),
            stableWindow: TimeSpan.FromMilliseconds(150));

        File.WriteAllText(Path.Combine(_dir, "notes.txt"), "hello");
        Assert.False(ev.Wait(TimeSpan.FromMilliseconds(600)));
    }
}
