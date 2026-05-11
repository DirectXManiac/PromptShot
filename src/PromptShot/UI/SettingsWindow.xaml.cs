using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PromptShot.Config;
using PromptShot.Storage;
using PromptShot.Templates;
using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = System.Windows.MessageBox;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;
using WinFormsFolderBrowser = System.Windows.Forms.FolderBrowserDialog;

namespace PromptShot.UI;

internal partial class SettingsWindow : Window
{
    private static readonly Regex DigitsOnly = new(@"^\d+$", RegexOptions.Compiled);

    private readonly AppConfig _working;

    public AppConfig? Result { get; private set; }
    public event EventHandler<AppConfig>? Applied;

    public SettingsWindow(AppConfig current)
    {
        InitializeComponent();
        _working = Clone(current);
        LoadFromConfig(_working);
        UpdatePreviews();
    }

    private void LoadFromConfig(AppConfig c)
    {
        EnabledCheck.IsChecked = c.Enabled;
        OutputDirBox.Text = c.OutputDirectory;
        FilenameTemplateBox.Text = c.FilenameTemplate;
        ClipboardTemplateBox.Text = c.ClipboardTemplate;
        RetentionDaysBox.Text = c.RetentionDays.ToString();
        ShowToastCheck.IsChecked = c.ShowToast;
        WatchFoldersCheck.IsChecked = c.WatchScreenshotFolders;
        WatchFoldersBox.Text = string.Join(Environment.NewLine, c.ScreenshotFolders ?? new());
        AutoStartCheck.IsChecked = AutoStartManager.IsEnabled();
        RepeatHotkeyCheck.IsChecked = c.RepeatHotkeyEnabled;
        HotkeyBox.Text = c.RepeatHotkey;
    }

    private bool TryCommitToConfig(AppConfig c, out string? error)
    {
        error = null;

        if (!int.TryParse(RetentionDaysBox.Text, out var retention) || retention < 0 || retention > 3650)
        {
            error = "Retention days must be a non-negative integer (0–3650).";
            return false;
        }
        if (RepeatHotkeyCheck.IsChecked == true &&
            !HotkeyRegistrar.TryParse(HotkeyBox.Text, out _, out _))
        {
            error = "Hotkey combo cannot be parsed. Example: 'Ctrl+Shift+V'.";
            return false;
        }

        c.Enabled = EnabledCheck.IsChecked == true;
        c.OutputDirectory = (OutputDirBox.Text ?? string.Empty).Trim();
        c.FilenameTemplate = (FilenameTemplateBox.Text ?? string.Empty).Trim();
        c.ClipboardTemplate = ClipboardTemplateBox.Text ?? string.Empty;
        c.RetentionDays = retention;
        c.ShowToast = ShowToastCheck.IsChecked == true;
        c.WatchScreenshotFolders = WatchFoldersCheck.IsChecked == true;
        c.ScreenshotFolders = (WatchFoldersBox.Text ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
        c.RepeatHotkeyEnabled = RepeatHotkeyCheck.IsChecked == true;
        c.RepeatHotkey = (HotkeyBox.Text ?? string.Empty).Trim();
        c.AutoStart = AutoStartCheck.IsChecked == true;
        return true;
    }

    private void UpdatePreviews()
    {
        var now = DateTimeOffset.Now;
        try
        {
            var filename = TemplateRenderer.RenderFilename(FilenameTemplateBox.Text ?? string.Empty, now);
            var dir = ConfigStore.ExpandPath(OutputDirBox.Text ?? string.Empty);
            var fullPath = string.IsNullOrEmpty(dir) ? filename : Path.Combine(dir, filename);
            FilenamePreview.Text = fullPath;
            ClipboardPreview.Text = TemplateRenderer.RenderClipboard(
                ClipboardTemplateBox.Text ?? string.Empty, fullPath, now);
        }
        catch (Exception ex)
        {
            FilenamePreview.Text = $"<error: {ex.Message}>";
            ClipboardPreview.Text = string.Empty;
        }
    }

    private void OnPreviewSourceChanged(object sender, TextChangedEventArgs e) => UpdatePreviews();

    private void OnDigitsOnlyInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !DigitsOnly.IsMatch(e.Text);
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton b && b.Tag is string preset)
        {
            ClipboardTemplateBox.Text = preset;
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new WinFormsFolderBrowser
        {
            Description = "Select output directory for screenshots",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = ConfigStore.ExpandPath(OutputDirBox.Text ?? string.Empty),
        };
        if (dlg.ShowDialog() == WinFormsDialogResult.OK)
        {
            OutputDirBox.Text = dlg.SelectedPath;
            UpdatePreviews();
        }
    }

    private bool DoApply()
    {
        if (!TryCommitToConfig(_working, out var error))
        {
            WpfMessageBox.Show(this, error, "Invalid input", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        try
        {
            AutoStartManager.SetEnabled(_working.AutoStart);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(this, $"Auto-start could not be updated:\n{ex.Message}",
                "Auto-start", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        Applied?.Invoke(this, _working);
        return true;
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e) => DoApply();

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!DoApply()) return;
        Result = _working;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static AppConfig Clone(AppConfig c) => new()
    {
        Enabled = c.Enabled,
        OutputDirectory = c.OutputDirectory,
        FilenameTemplate = c.FilenameTemplate,
        ClipboardTemplate = c.ClipboardTemplate,
        ShowToast = c.ShowToast,
        RetentionDays = c.RetentionDays,
        ImageFormat = c.ImageFormat,
        WatchScreenshotFolders = c.WatchScreenshotFolders,
        ScreenshotFolders = new List<string>(c.ScreenshotFolders ?? new()),
        AutoStart = c.AutoStart,
        RepeatHotkeyEnabled = c.RepeatHotkeyEnabled,
        RepeatHotkey = c.RepeatHotkey,
    };
}
