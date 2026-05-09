using System;
using System.IO;
using PromptShot.Config;
using Xunit;

namespace PromptShot.Tests;

public class ConfigStoreTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PromptShot.Tests." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void LoadOrCreate_creates_file_with_defaults_on_first_run()
    {
        var store = new ConfigStore(_tempDir);
        Assert.False(File.Exists(store.ConfigPath));

        var cfg = store.LoadOrCreate();

        Assert.True(File.Exists(store.ConfigPath));
        Assert.True(cfg.Enabled);
        Assert.Equal("{path}", cfg.ClipboardTemplate);
        Assert.Equal(7, cfg.RetentionDays);
    }

    [Fact]
    public void Save_then_load_round_trips()
    {
        var store = new ConfigStore(_tempDir);
        var cfg = AppConfig.CreateDefault();
        cfg.Enabled = false;
        cfg.ClipboardTemplate = "@{path}";
        cfg.RetentionDays = 30;
        store.Save(cfg);

        var reloaded = store.LoadOrCreate();
        Assert.False(reloaded.Enabled);
        Assert.Equal("@{path}", reloaded.ClipboardTemplate);
        Assert.Equal(30, reloaded.RetentionDays);
    }

    [Fact]
    public void Corrupt_json_falls_back_to_defaults_and_archives_old_file()
    {
        var store = new ConfigStore(_tempDir);
        File.WriteAllText(store.ConfigPath, "{ this is not json");

        var cfg = store.LoadOrCreate();

        Assert.True(cfg.Enabled);
        Assert.True(File.Exists(store.ConfigPath));
        Assert.True(File.Exists(store.ConfigPath + ".corrupt"));
    }

    [Fact]
    public void ExpandPath_resolves_environment_variables()
    {
        var raw = "%TEMP%\\PromptShot";
        var expanded = ConfigStore.ExpandPath(raw);
        Assert.DoesNotContain("%", expanded);
        Assert.EndsWith("PromptShot", expanded);
    }
}
