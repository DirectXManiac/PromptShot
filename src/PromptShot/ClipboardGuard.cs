using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace PromptShot;

/// <summary>
/// Подавляет self-trigger петлю. Когда сами пишем в clipboard — запоминаем
/// SHA-256 текста, и WM_CLIPBOARDUPDATE на этот текст игнорируется в течение
/// окна подавления (по умолчанию 2 сек).
/// </summary>
internal sealed class ClipboardGuard
{
    private static readonly TimeSpan SuppressionWindow = TimeSpan.FromSeconds(2);

    private byte[]? _lastWriteHash;
    private DateTimeOffset _lastWriteAt;

    public void WriteText(string text)
    {
        var hash = ComputeHash(text);
        Volatile.Write(ref _lastWriteHash, hash);
        _lastWriteAt = DateTimeOffset.UtcNow;
        Clipboard.SetText(text);
    }

    public bool IsOwnEcho(string text)
    {
        var hash = Volatile.Read(ref _lastWriteHash);
        if (hash is null) return false;
        if (DateTimeOffset.UtcNow - _lastWriteAt > SuppressionWindow) return false;
        var current = ComputeHash(text);
        return CryptographicOperations.FixedTimeEquals(hash, current);
    }

    private static byte[] ComputeHash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return SHA256.HashData(bytes);
    }
}
