using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PromptShot.Native;

namespace PromptShot;

/// <summary>
/// Регистрирует один глобальный hotkey через RegisterHotKey/WM_HOTKEY и
/// поднимает событие <see cref="Pressed"/>. Парсит строки вида "Ctrl+Shift+V".
/// </summary>
internal sealed class HotkeyRegistrar : NativeWindow, IDisposable
{
    private const int HotkeyId = 0xB001;

    public event EventHandler? Pressed;

    private bool _registered;
    private bool _disposed;

    public HotkeyRegistrar(string combo)
    {
        if (!TryParse(combo, out var modifiers, out var virtualKey))
        {
            throw new ArgumentException($"Cannot parse hotkey '{combo}'", nameof(combo));
        }

        var cp = new CreateParams
        {
            Caption = "PromptShot.HotkeyRegistrar",
            Parent = NativeMethods.HWND_MESSAGE,
        };
        CreateHandle(cp);

        if (!NativeMethods.RegisterHotKey(Handle, HotkeyId, modifiers | NativeMethods.MOD_NOREPEAT, virtualKey))
        {
            var err = Marshal.GetLastWin32Error();
            DestroyHandle();
            throw new Win32Exception(err, $"RegisterHotKey failed for '{combo}'");
        }
        _registered = true;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
        {
            try { Pressed?.Invoke(this, EventArgs.Empty); }
            catch { /* swallow — не валим message loop */ }
        }
        base.WndProc(ref m);
    }

    public static bool TryParse(string combo, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(combo)) return false;

        var parts = combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var raw in parts)
        {
            var part = raw.ToLowerInvariant();
            switch (part)
            {
                case "ctrl":
                case "control": modifiers |= NativeMethods.MOD_CONTROL; continue;
                case "shift": modifiers |= NativeMethods.MOD_SHIFT; continue;
                case "alt": modifiers |= NativeMethods.MOD_ALT; continue;
                case "win":
                case "windows": modifiers |= NativeMethods.MOD_WIN; continue;
            }

            if (virtualKey != 0) return false; // Только один основной key.

            if (part.Length == 1)
            {
                var ch = char.ToUpperInvariant(part[0]);
                if (ch is >= 'A' and <= 'Z' or >= '0' and <= '9')
                {
                    virtualKey = ch;
                    continue;
                }
            }

            if (part.StartsWith('f') && part.Length is >= 2 and <= 3
                && int.TryParse(part.AsSpan(1), out var fn) && fn is >= 1 and <= 24)
            {
                virtualKey = (uint)(0x70 + (fn - 1)); // VK_F1..VK_F24
                continue;
            }

            return false;
        }

        return virtualKey != 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_registered && Handle != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            _registered = false;
        }
        if (Handle != IntPtr.Zero) DestroyHandle();
    }
}
