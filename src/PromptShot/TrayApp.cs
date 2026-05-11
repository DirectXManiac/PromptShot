using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
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
    private readonly ToolStripMenuItem _autoStartMenuItem;
    private readonly SynchronizationContext _uiContext;
    private readonly Icon _trayIcon;
    private ScreenshotFolderWatcher? _folderWatcher;
    private HotkeyRegistrar? _hotkey;

    public TrayApp()
    {
        _configStore = new ConfigStore();
        var config = _configStore.LoadOrCreate();

        // WindowsFormsSynchronizationContext ставится автоматически при Application.Run,
        // но мы можем создаваться раньше — подстрахуемся.
        if (SynchronizationContext.Current is null)
        {
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        }
        _uiContext = SynchronizationContext.Current!;

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
        _autoStartMenuItem = new ToolStripMenuItem("Start with Windows", image: null, OnToggleAutoStart)
        {
            Checked = AutoStartManager.IsEnabled(),
            CheckOnClick = false,
        };
        _statsMenuItem = new ToolStripMenuItem("Saved this session: 0") { Enabled = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_enabledMenuItem);
        menu.Items.Add(_autoStartMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open screenshots folder", null, OnOpenScreenshotsFolder);
        menu.Items.Add("Settings…", null, OnOpenSettings);
        menu.Items.Add("Open config file", null, OnOpenConfig);
        menu.Items.Add("Reload config", null, OnReloadConfig);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_statsMenuItem);
        menu.Items.Add("About", null, OnAbout);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, OnExit);

        _trayIcon = TrayIconFactory.Create();
        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon,
            Text = "PromptShot",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notifyIcon.DoubleClick += (_, _) => OnOpenScreenshotsFolder(this, EventArgs.Empty);

        _watcher = new ClipboardWatcher();
        _watcher.ClipboardChanged += (_, _) => _pipeline.OnClipboardChanged();

        StartFolderWatcher(config);
        StartHotkey(config);
    }

    private void OnToggleAutoStart(object? sender, EventArgs e)
    {
        try
        {
            var newState = !_autoStartMenuItem.Checked;
            AutoStartManager.SetEnabled(newState);
            _autoStartMenuItem.Checked = newState;
            var cfg = _pipeline.Config;
            cfg.AutoStart = newState;
            _configStore.Save(cfg);
        }
        catch (Exception ex)
        {
            OnPipelineError(this, ex);
        }
    }

    private void StartHotkey(AppConfig config)
    {
        _hotkey?.Dispose();
        _hotkey = null;

        if (!config.RepeatHotkeyEnabled || string.IsNullOrWhiteSpace(config.RepeatHotkey)) return;
        try
        {
            _hotkey = new HotkeyRegistrar(config.RepeatHotkey);
            _hotkey.Pressed += (_, _) => OnHotkeyPressed();
        }
        catch (Exception ex)
        {
            OnPipelineError(this, ex);
        }
    }

    private void OnHotkeyPressed()
    {
        var ok = _pipeline.RepasteLastPath();
        if (!_pipeline.Config.ShowToast) return;

        _notifyIcon.BalloonTipTitle = "PromptShot";
        _notifyIcon.BalloonTipText = ok
            ? $"Re-pasted: {Path.GetFileName(_pipeline.LastSavedPath ?? "")}"
            : "No screenshot in this session yet";
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(1500);
    }

    private void StartFolderWatcher(AppConfig config)
    {
        _folderWatcher?.Dispose();
        _folderWatcher = null;

        if (!config.WatchScreenshotFolders) return;
        if (config.ScreenshotFolders is null || config.ScreenshotFolders.Count == 0) return;

        _folderWatcher = new ScreenshotFolderWatcher(
            config.ScreenshotFolders,
            path => _uiContext.Post(_ => _pipeline.ProcessExternalFile(path), null));
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

    private void OnOpenSettings(object? sender, EventArgs e)
    {
        var window = new UI.SettingsWindow(_pipeline.Config);
        window.Applied += (_, updated) => ApplyConfigChanges(updated);
        window.ShowDialog();
    }

    private void ApplyConfigChanges(AppConfig updated)
    {
        _configStore.Save(updated);
        _pipeline.ReplaceConfig(updated);
        _enabledMenuItem.Checked = updated.Enabled;
        _autoStartMenuItem.Checked = AutoStartManager.IsEnabled();
        StartFolderWatcher(updated);
        StartHotkey(updated);
    }

    private void OnReloadConfig(object? sender, EventArgs e)
    {
        var cfg = _configStore.LoadOrCreate();
        _pipeline.ReplaceConfig(cfg);
        _enabledMenuItem.Checked = cfg.Enabled;
        _autoStartMenuItem.Checked = AutoStartManager.IsEnabled();
        StartFolderWatcher(cfg);
        StartHotkey(cfg);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _watcher.Dispose();
            _folderWatcher?.Dispose();
            _hotkey?.Dispose();
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
