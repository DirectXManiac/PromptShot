using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using PromptShot.Config;
using PromptShot.Storage;

namespace PromptShot;

/// <summary>
/// Корневой ApplicationContext. Держит NotifyIcon, ClipboardWatcher и pipeline.
/// </summary>
internal sealed class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ClipboardWatcher _watcher;
    private readonly ClipboardGuard _guard;
    private readonly ScreenshotPipeline _pipeline;
    private readonly ConfigStore _configStore;
    private readonly ToolStripMenuItem _enabledMenuItem;
    private readonly ToolStripMenuItem _statsMenuItem;

    public TrayApp()
    {
        _configStore = new ConfigStore();
        var config = _configStore.LoadOrCreate();

        _guard = new ClipboardGuard();
        _pipeline = new ScreenshotPipeline(_configStore, _guard, config);
        _pipeline.ScreenshotSaved += OnScreenshotSaved;
        _pipeline.PipelineError += OnPipelineError;

        // Чистим старые файлы при старте — не блокируем UI.
        ScheduleRetentionCleanup(config);

        _enabledMenuItem = new ToolStripMenuItem("Enabled", image: null, OnToggleEnabled)
        {
            Checked = config.Enabled,
            CheckOnClick = false,
        };
        _statsMenuItem = new ToolStripMenuItem("Saved this session: 0") { Enabled = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_enabledMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open screenshots folder", null, OnOpenScreenshotsFolder);
        menu.Items.Add("Open config", null, OnOpenConfig);
        menu.Items.Add("Reload config", null, OnReloadConfig);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_statsMenuItem);
        menu.Items.Add("About", null, OnAbout);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, OnExit);

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "PromptShot",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notifyIcon.DoubleClick += (_, _) => OnOpenScreenshotsFolder(this, EventArgs.Empty);

        _watcher = new ClipboardWatcher();
        _watcher.ClipboardChanged += (_, _) => _pipeline.OnClipboardChanged();
    }

    private void OnScreenshotSaved(object? sender, ScreenshotSavedEventArgs e)
    {
        _statsMenuItem.Text = $"Saved this session: {_pipeline.SessionCount}";
        if (_pipeline.Config.ShowToast)
        {
            _notifyIcon.BalloonTipTitle = "Screenshot saved";
            _notifyIcon.BalloonTipText = $"{Path.GetFileName(e.FilePath)} → Ctrl+V to paste";
            _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            _notifyIcon.ShowBalloonTip(2000);
        }
    }

    private void OnPipelineError(object? sender, Exception ex)
    {
        _notifyIcon.BalloonTipTitle = "PromptShot error";
        _notifyIcon.BalloonTipText = ex.Message;
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Error;
        _notifyIcon.ShowBalloonTip(3000);
    }

    private void OnToggleEnabled(object? sender, EventArgs e)
    {
        var cfg = _pipeline.Config;
        cfg.Enabled = !cfg.Enabled;
        _enabledMenuItem.Checked = cfg.Enabled;
        _configStore.Save(cfg);
    }

    private void OnOpenScreenshotsFolder(object? sender, EventArgs e)
    {
        var dir = ConfigStore.ExpandPath(_pipeline.Config.OutputDirectory);
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
    }

    private void OnOpenConfig(object? sender, EventArgs e)
    {
        // Гарантируем существование файла перед открытием.
        if (!File.Exists(_configStore.ConfigPath))
        {
            _configStore.Save(_pipeline.Config);
        }
        Process.Start(new ProcessStartInfo(_configStore.ConfigPath) { UseShellExecute = true });
    }

    private void OnReloadConfig(object? sender, EventArgs e)
    {
        var cfg = _configStore.LoadOrCreate();
        _pipeline.ReplaceConfig(cfg);
        _enabledMenuItem.Checked = cfg.Enabled;
        _notifyIcon.BalloonTipTitle = "PromptShot";
        _notifyIcon.BalloonTipText = "Config reloaded";
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(1500);
    }

    private void OnAbout(object? sender, EventArgs e)
    {
        MessageBox.Show(
            "PromptShot — clipboard-driven screenshot helper\n\n" +
            "1. Take a screenshot (Win+Shift+S, Alt+PrintScreen, etc.)\n" +
            "2. PromptShot saves the PNG and replaces clipboard with the path\n" +
            "3. Ctrl+V into Claude Code / Copilot CLI\n\n" +
            $"Config: {_configStore.ConfigPath}",
            "About PromptShot",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OnExit(object? sender, EventArgs e)
    {
        ExitThread();
    }

    private static void ScheduleRetentionCleanup(AppConfig config)
    {
        if (config.RetentionDays <= 0) return;
        var dir = ConfigStore.ExpandPath(config.OutputDirectory);
        var maxAge = TimeSpan.FromDays(config.RetentionDays);
        System.Threading.Tasks.Task.Run(() =>
        {
            try { RetentionCleaner.CleanOlderThan(dir, maxAge); }
            catch { /* swallow — non-critical */ }
        });
    }

    private static Icon LoadTrayIcon()
    {
        // Используем системную иконку до тех пор пока не добавим свою.
        return SystemIcons.Application;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _watcher.Dispose();
        }
        base.Dispose(disposing);
    }
}
