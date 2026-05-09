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
    private readonly ConfigStore _configStore;
    private readonly ClipboardGuard _guard;
    private AppConfig _config;
    private int _sessionCount;

    public event EventHandler<ScreenshotSavedEventArgs>? ScreenshotSaved;
    public event EventHandler<Exception>? PipelineError;

    public int SessionCount => _sessionCount;
    public AppConfig Config => _config;

    public ScreenshotPipeline(ConfigStore configStore, ClipboardGuard guard, AppConfig config)
    {
        _configStore = configStore;
        _guard = guard;
        _config = config;
    }

    public void ReplaceConfig(AppConfig config) => _config = config;

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

            Interlocked.Increment(ref _sessionCount);
            ScreenshotSaved?.Invoke(this, new ScreenshotSavedEventArgs(fullPath, clipboardText));
        }
        catch (Exception ex)
        {
            PipelineError?.Invoke(this, ex);
        }
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
