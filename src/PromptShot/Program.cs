using System;
using System.Threading;
using System.Windows.Forms;

namespace PromptShot;

internal static class Program
{
    private const string SingleInstanceMutexName = "Global\\PromptShot.SingleInstance.{4E0E1A7C-9C25-4F0B-9D7B-7C7C0E2D9B5A}";

    [STAThread]
    private static int Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "PromptShot is already running. Check the system tray.",
                "PromptShot",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return 1;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowFatal(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ShowFatal(e.ExceptionObject as Exception);

        using var trayApp = new TrayApp();
        Application.Run(trayApp);
        return 0;
    }

    private static void ShowFatal(Exception? ex)
    {
        if (ex is null) return;
        MessageBox.Show(
            ex.ToString(),
            "PromptShot — unhandled exception",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
