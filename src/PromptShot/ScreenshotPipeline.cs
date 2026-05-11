using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using PromptShot.Config;
using PromptShot.Templates;

namespace PromptShot;

/// <summary>
/// Обработчик одного срабатывания clipboard listener.
/// Извлекает картинку из буфера, сохраняет PNG и кладёт в буфер
/// сконфигурированный шаблон с путём.
/// </summary>
internal sealed class ScreenshotPipeline
{
    private static readonly TimeSpan DedupWindow = TimeSpan.FromMilliseconds(1500);

    private readonly ConfigStore _configStore;
    private readonly ClipboardGuard _guard;
    private AppConfig _config;
    private int _sessionCount;
    private DateTimeOffset _lastProcessedUtc = DateTimeOffset.MinValue;

    public event EventHandler<ScreenshotSavedEventArgs>? ScreenshotSaved;
    public event EventHandler<Exception>? PipelineError;

    public int SessionCount => _sessionCount;
    public AppConfig Config => _config;
    public string? LastSavedPath { get; private set; }

    public ScreenshotPipeline(ConfigStore configStore, ClipboardGuard guard, AppConfig config)
    {
        _configStore = configStore;
        _guard = guard;
        _config = config;
    }

    public void ReplaceConfig(AppConfig config) => _config = config;

    /// <summary>
    /// Кладёт в clipboard рендер шаблона для последнего сохранённого скриншота.
    /// Используется hotkey-режимом. Возвращает true если что-то положено.
    /// </summary>
    public bool RepasteLastPath()
    {
        var path = LastSavedPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;

        var rendered = TemplateRenderer.RenderClipboard(_config.ClipboardTemplate, path, DateTimeOffset.Now);
        _guard.WriteText(rendered);
        return true;
    }

    /// <summary>
    /// Точка входа из ClipboardWatcher. Должно вызываться на STA UI-треде.
    /// </summary>
    public void OnClipboardChanged()
    {
        if (!_config.Enabled) return;

        try
        {
            // Если clipboard содержит текст и это эхо нашей записи — выходим тихо.
            if (Clipboard.ContainsText())
            {
                var text = SafeGetText();
                if (text is not null && _guard.IsOwnEcho(text)) return;
                // Текст не наш и картинки нет — нечего делать.
                if (!Clipboard.ContainsImage()) return;
            }

            if (!Clipboard.ContainsImage()) return;

            using var image = GetImageWithRetry();
            if (image is null) return;

            var now = DateTimeOffset.Now;
            var outputDir = ConfigStore.ExpandPath(_config.OutputDirectory);
            Directory.CreateDirectory(outputDir);

            var filename = TemplateRenderer.RenderFilename(_config.FilenameTemplate, now);
            filename = SanitizeFilename(filename);
            var fullPath = Path.Combine(outputDir, filename);

            image.Save(fullPath, ImageFormat.Png);

            var clipboardText = TemplateRenderer.RenderClipboard(_config.ClipboardTemplate, fullPath, now);
            _guard.WriteText(clipboardText);
            _lastProcessedUtc = DateTimeOffset.UtcNow;
            LastSavedPath = fullPath;

            Interlocked.Increment(ref _sessionCount);
            ScreenshotSaved?.Invoke(this, new ScreenshotSavedEventArgs(fullPath, clipboardText));
        }
        catch (Exception ex)
        {
            PipelineError?.Invoke(this, ex);
        }
    }

    /// <summary>
    /// Обработка PNG-файла, появившегося во внешней папке (FileSystemWatcher fallback
    /// для Win+PrintScreen и инструментов, которые сохраняют в файл, но не в clipboard).
    /// Должно вызываться на UI/STA-треде — внутри пишем в Clipboard.
    /// </summary>
    public void ProcessExternalFile(string sourcePath)
    {
        if (!_config.Enabled) return;
        if (string.IsNullOrWhiteSpace(sourcePath)) return;

        try
        {
            if (!File.Exists(sourcePath)) return;

            var outputDir = ConfigStore.ExpandPath(_config.OutputDirectory);
            var fullSource = Path.GetFullPath(sourcePath);
            var fullOutput = Path.GetFullPath(outputDir);

            // Защита от петли: файл уже внутри нашей output-папки.
            if (fullSource.StartsWith(fullOutput + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fullSource, fullOutput, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Дедупликация с clipboard-веткой: если только что обработали скриншот через
            // OnClipboardChanged, скорее всего это тот же кадр продублировался файлом.
            if (DateTimeOffset.UtcNow - _lastProcessedUtc < DedupWindow) return;

            Directory.CreateDirectory(outputDir);
            var now = DateTimeOffset.Now;
            var filename = SanitizeFilename(TemplateRenderer.RenderFilename(_config.FilenameTemplate, now));
            var fullPath = Path.Combine(outputDir, filename);

            CopyWithRetry(fullSource, fullPath);

            var clipboardText = TemplateRenderer.RenderClipboard(_config.ClipboardTemplate, fullPath, now);
            _guard.WriteText(clipboardText);
            _lastProcessedUtc = DateTimeOffset.UtcNow;
            LastSavedPath = fullPath;

            Interlocked.Increment(ref _sessionCount);
            ScreenshotSaved?.Invoke(this, new ScreenshotSavedEventArgs(fullPath, clipboardText));
        }
        catch (Exception ex)
        {
            PipelineError?.Invoke(this, ex);
        }
    }

    private static void CopyWithRetry(string src, string dst)
    {
        const int attempts = 5;
        for (var i = 0; i < attempts - 1; i++)
        {
            try
            {
                File.Copy(src, dst, overwrite: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
        }
        File.Copy(src, dst, overwrite: true);
    }

    private static string? SafeGetText()
    {
        try { return Clipboard.GetText(); } catch { return null; }
    }

    private static Image? GetImageWithRetry()
    {
        const int attempts = 3;
        for (var i = 0; i < attempts; i++)
        {
            try
            {
                return Clipboard.GetImage();
            }
            catch (ExternalException)
            {
                // Покрывает COMException и SEHException — другое приложение держит буфер.
                Thread.Sleep(50);
            }
        }
        return null;
    }

    private static string SanitizeFilename(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var span = name.AsSpan();
        Span<char> buf = stackalloc char[span.Length];
        var any = false;
        for (var i = 0; i < span.Length; i++)
        {
            var ch = span[i];
            if (Array.IndexOf(invalid, ch) >= 0)
            {
                buf[i] = '_';
                any = true;
            }
            else
            {
                buf[i] = ch;
            }
        }
        return any ? new string(buf) : name;
    }
}

internal sealed record ScreenshotSavedEventArgs(string FilePath, string ClipboardText);
