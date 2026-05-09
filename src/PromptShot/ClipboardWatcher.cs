using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PromptShot.Native;

namespace PromptShot;

/// <summary>
/// Скрытое message-only окно, подписанное на WM_CLIPBOARDUPDATE.
/// Вызывает <see cref="ClipboardChanged"/> при каждом изменении буфера обмена.
/// </summary>
internal sealed class ClipboardWatcher : NativeWindow, IDisposable
{
    public event EventHandler? ClipboardChanged;

    private bool _listening;
    private bool _disposed;

    public ClipboardWatcher()
    {
        var cp = new CreateParams
        {
            Caption = "PromptShot.ClipboardWatcher",
            Parent = NativeMethods.HWND_MESSAGE,
        };
        CreateHandle(cp);

        if (!NativeMethods.AddClipboardFormatListener(Handle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                "AddClipboardFormatListener failed");
        }
        _listening = true;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            try
            {
                ClipboardChanged?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                // Никогда не выкидываем исключение из WndProc — это убьёт Application loop.
                // Pipeline сам логирует ошибки.
            }
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_listening && Handle != IntPtr.Zero)
        {
            NativeMethods.RemoveClipboardFormatListener(Handle);
            _listening = false;
        }
        if (Handle != IntPtr.Zero)
        {
            DestroyHandle();
        }
    }
}
