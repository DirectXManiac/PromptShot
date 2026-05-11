using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace PromptShot.Storage;

/// <summary>
/// Управление автозапуском через реестр пользователя
/// (HKCU\Software\Microsoft\Windows\CurrentVersion\Run).
/// </summary>
internal static class AutoStartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PromptShot";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var value = key?.GetValue(ValueName) as string;
        return !string.IsNullOrWhiteSpace(value);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException($"Cannot open HKCU\\{RunKeyPath}");

        if (enabled)
        {
            var path = GetExecutablePath();
            // Кавычки на случай пробелов в пути.
            key.SetValue(ValueName, $"\"{path}\"", RegistryValueKind.String);
        }
        else
        {
            if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
    }

    private static string GetExecutablePath()
    {
        // Process.MainModule.FileName возвращает реальный exe (а не dotnet.dll).
        var path = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = Environment.ProcessPath ?? AppContext.BaseDirectory;
        }
        return path!;
    }
}
