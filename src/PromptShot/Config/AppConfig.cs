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

    public static AppConfig CreateDefault() => new();
}
