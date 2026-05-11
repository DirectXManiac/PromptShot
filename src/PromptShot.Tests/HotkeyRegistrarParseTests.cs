using PromptShot;
using Xunit;

namespace PromptShot.Tests;

public class HotkeyRegistrarParseTests
{
    [Theory]
    [InlineData("Ctrl+Shift+V", 0x0002u | 0x0004u, (uint)'V')]
    [InlineData("ctrl + alt + a", 0x0002u | 0x0001u, (uint)'A')]
    [InlineData("Win+Shift+S", 0x0008u | 0x0004u, (uint)'S')]
    [InlineData("Ctrl+F12", 0x0002u, 0x7Bu)]
    [InlineData("Ctrl+1", 0x0002u, (uint)'1')]
    public void Parses_known_combos(string combo, uint expectedMods, uint expectedVk)
    {
        var ok = HotkeyRegistrar.TryParse(combo, out var mods, out var vk);
        Assert.True(ok, $"failed to parse '{combo}'");
        Assert.Equal(expectedMods, mods);
        Assert.Equal(expectedVk, vk);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl")]                 // нет основного key
    [InlineData("Ctrl+Shift")]            // нет основного key
    [InlineData("Ctrl+@")]                // невалидный символ
    [InlineData("Ctrl+A+B")]              // два основных key
    [InlineData("Ctrl+F25")]              // F25 не существует
    public void Rejects_invalid(string combo)
    {
        Assert.False(HotkeyRegistrar.TryParse(combo, out _, out _));
    }
}
