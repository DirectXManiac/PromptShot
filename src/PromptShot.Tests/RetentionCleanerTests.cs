using System;
using System.IO;
using PromptShot.Storage;
using Xunit;

namespace PromptShot.Tests;

public class RetentionCleanerTests : IDisposable
{
    private readonly string _dir;

    public RetentionCleanerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "PromptShot.Retention." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void Deletes_only_old_png_files()
    {
        var oldPng = Path.Combine(_dir, "old.png");
        var newPng = Path.Combine(_dir, "new.png");
        var oldTxt = Path.Combine(_dir, "old.txt");
        File.WriteAllText(oldPng, "x");
        File.WriteAllText(newPng, "x");
        File.WriteAllText(oldTxt, "x");

        var longAgo = DateTime.UtcNow.AddDays(-30);
        File.SetLastWriteTimeUtc(oldPng, longAgo);
        File.SetLastWriteTimeUtc(oldTxt, longAgo);

        var deleted = RetentionCleaner.CleanOlderThan(_dir, TimeSpan.FromDays(7));

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(oldPng));
        Assert.True(File.Exists(newPng));
        Assert.True(File.Exists(oldTxt), "non-PNG must be left alone");
    }

    [Fact]
    public void Returns_zero_for_missing_directory()
    {
        var deleted = RetentionCleaner.CleanOlderThan(Path.Combine(_dir, "does-not-exist"), TimeSpan.FromDays(1));
        Assert.Equal(0, deleted);
    }

    [Fact]
    public void Zero_or_negative_age_is_noop()
    {
        var p = Path.Combine(_dir, "x.png");
        File.WriteAllText(p, "x");
        File.SetLastWriteTimeUtc(p, DateTime.UtcNow.AddYears(-1));

        Assert.Equal(0, RetentionCleaner.CleanOlderThan(_dir, TimeSpan.Zero));
        Assert.Equal(0, RetentionCleaner.CleanOlderThan(_dir, TimeSpan.FromMilliseconds(-1)));
        Assert.True(File.Exists(p));
    }
}
