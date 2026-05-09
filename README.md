# PromptShot

Фоновый помощник для Windows: ловит скриншот в буфере обмена, сохраняет PNG во временную папку и кладёт в clipboard готовую строку с путём — для мгновенной вставки (`Ctrl+V`) в Claude Code, GitHub Copilot CLI или любую другую консоль.

**Идея в одну строку:** `Win+Shift+S → выделил → Ctrl+V в CLI`. Никаких ручных «сохранить как» и перетаскиваний.

## Как это работает

1. PromptShot живёт в трее и подписан на изменения буфера обмена (`AddClipboardFormatListener`, без админ-прав).
2. Любой инструмент скриншотов (Win+Shift+S, Alt+PrintScreen, ShareX, Lightshot) кладёт картинку в буфер.
3. PromptShot сохраняет PNG в `%TEMP%\PromptShot\<timestamp>_<rand>.png` и подменяет содержимое буфера на сконфигурированный шаблон с путём.
4. `Ctrl+V` в окне CLI — путь вставляется, Claude Code распознаёт его как ссылку на изображение.

## Требования

- Windows 10/11
- .NET 8 Runtime (или single-file self-contained сборка из релизов)

## Конфиг

`%APPDATA%\PromptShot\config.json` создаётся при первом запуске:

```json
{
  "enabled": true,
  "outputDirectory": "%TEMP%\\PromptShot",
  "filenameTemplate": "{timestamp}_{rand}.png",
  "clipboardTemplate": "{path}",
  "showToast": true,
  "retentionDays": 7,
  "imageFormat": "png"
}
```

### Плейсхолдеры

`filenameTemplate`: `{timestamp}` (`yyyy-MM-dd_HH-mm-ss`), `{ts_unix}`, `{date}`, `{time}`, `{rand}` (6-символьный hex).

`clipboardTemplate`: `{path}` (Windows-путь), `{path_forward}` (с прямыми слэшами), `{filename}`, `{timestamp}`.

### Готовые пресеты `clipboardTemplate`

| Шаблон | Когда |
|--------|-------|
| `{path}` | Голый путь — Claude Code распознаёт сам |
| `@{path}` | Явный референс на файл |
| `Look at this screenshot: {path}\n` | С префиксной фразой |
| `{path_forward}` | Cross-tool совместимость |

## Сборка

```powershell
dotnet build src/PromptShot/PromptShot.csproj
dotnet run --project src/PromptShot/PromptShot.csproj

# Single-file release
dotnet publish src/PromptShot/PromptShot.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## Лицензия

MIT — см. [LICENSE](LICENSE).
