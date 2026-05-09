using System;
using System.IO;
using System.Text.Json;

namespace PromptShot.Config;

/// <summary>
/// Загрузка/сохранение конфига в %APPDATA%\PromptShot\config.json.
/// При первом запуске создаёт файл с дефолтами. Не падает на повреждённом JSON —
/// возвращает дефолты и переименовывает файл в .corrupt.
/// </summary>
internal sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
    };

    public string ConfigDirectory { get; }
    public string ConfigPath { get; }

    public ConfigStore(string? overrideDirectory = null)
    {
        ConfigDirectory = overrideDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PromptShot");
        ConfigPath = Path.Combine(ConfigDirectory, "config.json");
    }

    public AppConfig LoadOrCreate()
    {
        Directory.CreateDirectory(ConfigDirectory);

        if (!File.Exists(ConfigPath))
        {
            var fresh = AppConfig.CreateDefault();
            Save(fresh);
            return fresh;
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            return loaded ?? AppConfig.CreateDefault();
        }
        catch (JsonException)
        {
            var corrupt = ConfigPath + ".corrupt";
            try { File.Move(ConfigPath, corrupt, overwrite: true); } catch { }
            var fresh = AppConfig.CreateDefault();
            Save(fresh);
            return fresh;
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    public static string ExpandPath(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        return Environment.ExpandEnvironmentVariables(raw);
    }
}
