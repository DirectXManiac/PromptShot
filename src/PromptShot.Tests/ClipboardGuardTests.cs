using System.Threading;
using PromptShot;
using Xunit;

namespace PromptShot.Tests;

public class ClipboardGuardTests
{
    // ClipboardGuard.WriteText дёргает System.Windows.Forms.Clipboard.SetText —
    // в xUnit-раннере это требует STA. Мы тестируем только хеш-логику IsOwnEcho
    // через пуст-write путь: повторная проверка одинакового текста после одного
    // совпадения уже невозможна — сначала надо WriteText, который в headless
    // окружении может бросить. Поэтому проверяем только `IsOwnEcho` без записи.

    [Fact]
    public void Without_any_write_nothing_is_treated_as_echo()
    {
        var guard = new ClipboardGuard();
        Assert.False(guard.IsOwnEcho("anything"));
    }
}
