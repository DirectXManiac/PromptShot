# PromptShot

> Clipboard-driven screenshot helper for Claude Code, GitHub Copilot CLI, and any other terminal that accepts file paths.

[![CI](https://github.com/DirectXManiac/PromptShot/actions/workflows/ci.yml/badge.svg)](https://github.com/DirectXManiac/PromptShot/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/DirectXManiac/PromptShot?include_prereleases&sort=semver)](https://github.com/DirectXManiac/PromptShot/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)

## TL;DR

```
Win+Shift+S  →  select area  →  Ctrl+V into your CLI
```

PromptShot lives in the system tray and replaces the bitmap on the
clipboard with a path to a freshly-saved PNG, so any terminal can
"paste" the screenshot as a regular file reference.

## Why

Sending screenshots to Claude Code / Copilot CLI on Windows normally
takes four or five steps: take a snip, save somewhere, drag-and-drop
or type the path. PromptShot collapses that into the two keystrokes
you already use. It works with any screenshot tool that puts an
image on the clipboard — Snipping Tool (`Win+Shift+S`),
`Alt+PrintScreen`, ShareX, Lightshot, Greenshot. No global hotkey
hijacking, no admin rights, no kernel hooks.

## How it works

1. A message-only window subscribes to `WM_CLIPBOARDUPDATE` via
   `AddClipboardFormatListener` (Windows clipboard subsystem, no admin
   rights).
2. When a screenshot tool puts a bitmap on the clipboard, PromptShot:
   - saves the image as PNG into a configurable directory
     (default: `%TEMP%\PromptShot\`);
   - renders a clipboard template with the saved path
     (default: just `{path}`);
   - writes that text back to the clipboard.
3. A SHA-256 guard prevents the listener from re-triggering on its
   own write.
4. Optionally, a `FileSystemWatcher` covers `Win+PrintScreen` (which
   writes to `%USERPROFILE%\Pictures\Screenshots` instead of the
   clipboard) by copying any new PNG into the output directory and
   feeding the path back the same way.

## Install

### From a release (recommended)

Grab the latest binary from the
[releases page](https://github.com/DirectXManiac/PromptShot/releases/latest):

| File                                              | Size    | Requirements                                                                |
|---------------------------------------------------|---------|-----------------------------------------------------------------------------|
| `PromptShot-{version}-win-x64-self-contained.exe` | ~150 MB | None — .NET 8 runtime + WPF + WinForms bundled                              |
| `PromptShot-{version}-win-x64.exe`                | <1 MB   | [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)  |

Double-click the exe — a blue **P** icon appears in the tray.

> Windows SmartScreen may warn on first launch because the binary
> isn't code-signed. Click **More info** → **Run anyway**.

Optional: right-click the tray icon → **Start with Windows**.

### Build from source

Requirements: **.NET 8 SDK**, Windows 10/11.

```powershell
git clone https://github.com/DirectXManiac/PromptShot.git
cd PromptShot
dotnet build PromptShot.sln -c Release

# Self-contained single-file (bundles the runtime):
dotnet publish src/PromptShot/PromptShot.csproj `
  -c Release -r win-x64 --self-contained true `
  /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true

# Framework-dependent single-file (needs .NET 8 Desktop Runtime):
dotnet publish src/PromptShot/PromptShot.csproj `
  -c Release -r win-x64 --self-contained false `
  /p:PublishSingleFile=true
```

Behind a corporate proxy? Set `HTTP_PROXY` / `HTTPS_PROXY` before
`dotnet restore`, or drop a local `NuGet.Config` next to the
solution with `<config><add key="http_proxy" value="…" /></config>`.

## Configuration

Right-click the tray icon → **Settings…** for a live-preview UI, or
edit JSON directly at `%APPDATA%\PromptShot\config.json`:

```json
{
  "enabled": true,
  "outputDirectory": "%TEMP%\\PromptShot",
  "filenameTemplate": "{timestamp}_{rand}.png",
  "clipboardTemplate": "{path}",
  "showToast": true,
  "retentionDays": 7,
  "imageFormat": "png",
  "watchScreenshotFolders": false,
  "screenshotFolders": ["%USERPROFILE%\\Pictures\\Screenshots"],
  "autoStart": false,
  "repeatHotkeyEnabled": false,
  "repeatHotkey": "Ctrl+Shift+V"
}
```

`%TEMP%`, `%APPDATA%`, `%USERPROFILE%` etc. are expanded at runtime.

### Filename template placeholders

| Token         | Expands to                       |
|---------------|----------------------------------|
| `{timestamp}` | `YYYY-MM-DD_HH-mm-ss`            |
| `{date}`      | `YYYY-MM-DD`                     |
| `{time}`      | `HH-mm-ss`                       |
| `{ts_unix}`   | Unix seconds                     |
| `{rand}`      | 6 random lowercase hex chars     |

### Clipboard template placeholders

| Token            | Expands to                          |
|------------------|-------------------------------------|
| `{path}`         | full Windows path                   |
| `{path_forward}` | path with `/` instead of `\`        |
| `{filename}`     | just the file name                  |
| `{timestamp}`, `{date}`, `{time}`, `{ts_unix}` | timestamp parts |

Escape sequences inside the template: `\n`, `\r`, `\t`, `\\`.

### Built-in presets

| Preset                                | Use case                                |
|---------------------------------------|-----------------------------------------|
| `{path}`                              | Default — Claude Code auto-detects      |
| `@{path}`                             | Explicit file reference                 |
| `{path_forward}`                      | Cross-tool / WSL-friendly               |
| `Look at this screenshot: {path}\n`   | Prefixed phrase + newline               |

## Tips per CLI

### Claude Code

Use `{path}`. Claude Code recognises bare image paths and attaches
the picture automatically. `Ctrl+V` after a screenshot just works.

### GitHub Copilot CLI

Use `@{path}` so the path becomes an explicit `@file` reference.

### WSL / cross-tool

Use `{path_forward}` and point `outputDirectory` at a folder that
exists on both sides (e.g. `C:\\tools\\screenshots`, accessible from
WSL as `/mnt/c/tools/screenshots`).

## Features

- **Clipboard listener** — works with anything that puts an image on
  the clipboard.
- **FileSystemWatcher fallback** — optional, catches `Win+PrintScreen`
  and tools that write to a screenshots folder. 1.5s dedup window
  avoids double-processing when a tool produces both a clipboard
  bitmap and a file.
- **Repeat hotkey** — optional global hotkey (default `Ctrl+Shift+V`)
  re-puts the rendered path of the last screenshot into the clipboard.
- **Auto-start** — toggle in tray menu, written to
  `HKCU\…\CurrentVersion\Run`.
- **Retention cleanup** — purges PNGs older than `retentionDays` in
  the output directory on startup. Touches PNGs only, never recurses.
- **Self-trigger guard** — SHA-256 hash + 2s suppression window stops
  the clipboard listener from looping on its own writes.
- **Single-instance** — global named mutex, second launch shows a
  friendly notice.

## Project layout

```
PromptShot/
├── src/
│   ├── PromptShot/                 # main app (WinForms tray + WPF settings)
│   │   ├── ClipboardWatcher.cs
│   │   ├── ClipboardGuard.cs
│   │   ├── HotkeyRegistrar.cs
│   │   ├── ScreenshotPipeline.cs
│   │   ├── TrayApp.cs
│   │   ├── TrayIconFactory.cs
│   │   ├── Config/                 # AppConfig + ConfigStore
│   │   ├── Native/                 # P/Invoke (user32)
│   │   ├── Storage/                # AutoStartManager, RetentionCleaner, ScreenshotFolderWatcher
│   │   ├── Templates/              # TemplateRenderer
│   │   └── UI/                     # SettingsWindow.xaml(.cs)
│   └── PromptShot.Tests/           # xUnit
├── .github/workflows/              # ci.yml + release.yml
└── PromptShot.sln
```

## Releases & versioning

Push a `vX.Y.Z` tag and the
[release workflow](.github/workflows/release.yml)
builds both flavours and attaches them to a GitHub release with
auto-generated changelog:

```bash
git tag v0.2.0
git push origin v0.2.0
```

See [CHANGELOG.md](CHANGELOG.md) for the human-curated history.

## Contributing

Bug reports, PRs and feature ideas are welcome.

- Make sure `dotnet test PromptShot.sln` passes locally.
- Keep new public surface area minimal — most internals are `internal`
  with `InternalsVisibleTo` for tests.
- Documentation can be in English or Russian; code identifiers and
  commit messages should be in English.

## License

MIT — see [LICENSE](LICENSE).
