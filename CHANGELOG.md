# Changelog

All notable changes to PromptShot are documented here.
Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
versioning: [SemVer](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0] - 2026-05-11

### Added
- WPF **Settings** window in the tray menu with live previews for the
  filename and clipboard templates, four built-in presets, a folder
  picker, and validation for the repeat-hotkey combo.
- **FileSystemWatcher** fallback for screenshot folders
  (`Win+PrintScreen`, ShareX-to-file, …). New PNGs are copied into
  the output directory and the rendered path is put on the clipboard.
  A 1.5 s dedup window avoids racing the clipboard branch.
- Global **repeat hotkey** (`Ctrl+Shift+V` by default) re-puts the
  last saved screenshot's path back into the clipboard via
  `RegisterHotKey` / `WM_HOTKEY`.
- **Auto-start with Windows** toggle in the tray menu
  (`HKCU\…\CurrentVersion\Run`).
- Custom tray icon generated in code (rounded blue tile, white "P"),
  no binary `.ico` shipped.
- `ScreenshotPipeline.LastSavedPath` + `RepasteLastPath()`.

### Changed
- "Open config" tray entry renamed to "Open config file"; primary
  configuration entry point is "Settings…".
- `csproj` declares `UseWPF=true` alongside the existing
  `UseWindowsForms`; publish-only properties moved out of csproj so
  `dotnet build` / `dotnet test` no longer require a RID.

### Configuration
- New keys: `watchScreenshotFolders`, `screenshotFolders[]`,
  `autoStart`, `repeatHotkeyEnabled`, `repeatHotkey`.

## [0.1.0] - 2026-05-09

Initial public-ready cut.

### Added
- Background tray app listening to `WM_CLIPBOARDUPDATE` via
  `AddClipboardFormatListener`.
- `ScreenshotPipeline`: extracts the bitmap, saves as PNG into
  `outputDirectory`, renders a clipboard template and writes the
  result back to the clipboard.
- `TemplateRenderer` placeholders: `{path}`, `{path_forward}`,
  `{filename}`, `{timestamp}`, `{ts_unix}`, `{date}`, `{time}`,
  `{rand}`; escape sequences `\n`, `\r`, `\t`, `\\`.
- `ClipboardGuard` — SHA-256 hash + 2 s suppression window to
  prevent self-triggering when writing back to the clipboard.
- `ConfigStore` — JSON config at `%APPDATA%\PromptShot\config.json`,
  resilient to corrupt files (archived as `.corrupt`).
- `RetentionCleaner` — purges PNGs older than `retentionDays` in the
  output directory on startup. PNG-only, top-level, no recursion.
- Tray menu: Enabled toggle, open screenshots folder, open config,
  reload config, About, Exit; session counter; balloon-tip toasts.
- xUnit suite for `TemplateRenderer`, `ConfigStore`,
  `RetentionCleaner`.
