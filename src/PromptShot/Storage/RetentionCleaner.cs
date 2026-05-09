using System;
using System.IO;

namespace PromptShot.Storage;

/// <summary>
/// Удаляет PNG-файлы старше указанного возраста из подконтрольной папки.
/// Безопасность: трогает только *.png, не рекурсивно, не следует симлинкам.
/// </summary>
internal static class RetentionCleaner
{
    public static int CleanOlderThan(string directory, TimeSpan maxAge, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return 0;
        if (maxAge <= TimeSpan.Zero) return 0;

        var threshold = (now ?? DateTimeOffset.Now).UtcDateTime - maxAge;
        var deleted = 0;

        foreach (var path in Directory.EnumerateFiles(directory, "*.png", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var info = new FileInfo(path);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                if (info.LastWriteTimeUtc < threshold)
                {
                    info.Delete();
                    deleted++;
                }
            }
            catch
            {
                // Файл могли удалить параллельно или он залочен — пропускаем.
            }
        }
        return deleted;
    }
}
