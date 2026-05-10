using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using PromptShot.Config;

namespace PromptShot.Storage;

/// <summary>
/// Наблюдает за списком папок (типично — %USERPROFILE%\Pictures\Screenshots) и
/// вызывает <paramref name="onStableFile"/> когда появляется новый PNG и его размер
/// перестаёт меняться в течение окна стабильности.
/// </summary>
internal sealed class ScreenshotFolderWatcher : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly Dictionary<string, System.Threading.Timer> _debouncers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<string> _onStableFile;
    private readonly TimeSpan _stableWindow;
    private readonly object _lock = new();
    private bool _disposed;

    public IReadOnlyList<string> ResolvedFolders { get; }

    public ScreenshotFolderWatcher(
        IEnumerable<string> folders,
        Action<string> onStableFile,
        TimeSpan? stableWindow = null)
    {
        _onStableFile = onStableFile ?? throw new ArgumentNullException(nameof(onStableFile));
        _stableWindow = stableWindow ?? TimeSpan.FromMilliseconds(500);

        var resolved = new List<string>();
        foreach (var raw in folders)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var path = ConfigStore.ExpandPath(raw);
            if (!Directory.Exists(path))
            {
                try { Directory.CreateDirectory(path); }
                catch { continue; }
            }

            FileSystemWatcher? w = null;
            try
            {
                w = new FileSystemWatcher(path, "*.png")
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true,
                };
                w.Created += OnChange;
                w.Renamed += OnRenamed;
                w.Changed += OnChange;
                _watchers.Add(w);
                resolved.Add(path);
            }
            catch
            {
                w?.Dispose();
            }
        }
        ResolvedFolders = resolved;
    }

    private void OnChange(object? sender, FileSystemEventArgs e) => Schedule(e.FullPath);
    private void OnRenamed(object? sender, RenamedEventArgs e) => Schedule(e.FullPath);

    private void Schedule(string path)
    {
        if (_disposed) return;
        lock (_lock)
        {
            if (_debouncers.TryGetValue(path, out var existing))
            {
                existing.Change(_stableWindow, Timeout.InfiniteTimeSpan);
                return;
            }
            var timer = new System.Threading.Timer(OnDebounceFired, path, _stableWindow, Timeout.InfiniteTimeSpan);
            _debouncers[path] = timer;
        }
    }

    private void OnDebounceFired(object? state)
    {
        var path = (string)state!;
        lock (_lock)
        {
            if (_debouncers.Remove(path, out var timer))
            {
                timer.Dispose();
            }
        }
        try
        {
            _onStableFile(path);
        }
        catch
        {
            // Callback ошибки — не валим watcher.
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var w in _watchers)
        {
            try { w.EnableRaisingEvents = false; } catch { }
            w.Dispose();
        }
        _watchers.Clear();

        lock (_lock)
        {
            foreach (var t in _debouncers.Values) t.Dispose();
            _debouncers.Clear();
        }
    }
}
