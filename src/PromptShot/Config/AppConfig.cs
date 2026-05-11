using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PromptShot.Config;

internal sealed class AppConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("outputDirectory")]
    public string OutputDirectory { get; set; } = "%TEMP%\\PromptShot";

    [JsonPropertyName("filenameTemplate")]
    public string FilenameTemplate { get; set; } = "{timestamp}_{rand}.png";

    [JsonPropertyName("clipboardTemplate")]
    public string ClipboardTemplate { get; set; } = "{path}";

    [JsonPropertyName("showToast")]
    public bool ShowToast { get; set; } = true;

    [JsonPropertyName("retentionDays")]
    public int RetentionDays { get; set; } = 7;

    [JsonPropertyName("imageFormat")]
    public string ImageFormat { get; set; } = "png";

    [JsonPropertyName("watchScreenshotFolders")]
    public bool WatchScreenshotFolders { get; set; } = false;

    [JsonPropertyName("screenshotFolders")]
    public List<string> ScreenshotFolders { get; set; } = new()
    {
        "%USERPROFILE%\\Pictures\\Screenshots",
    };

    [JsonPropertyName("autoStart")]
    public bool AutoStart { get; set; } = false;

    [JsonPropertyName("repeatHotkeyEnabled")]
    public bool RepeatHotkeyEnabled { get; set; } = false;

    /// <summary>
    /// Hotkey для повторной вставки последнего пути в clipboard.
    /// Формат: <c>"Ctrl+Shift+V"</c>. Допустимые модификаторы: Ctrl, Shift, Alt, Win.
    /// </summary>
    [JsonPropertyName("repeatHotkey")]
    public string RepeatHotkey { get; set; } = "Ctrl+Shift+V";

    public static AppConfig CreateDefault() => new();
}
