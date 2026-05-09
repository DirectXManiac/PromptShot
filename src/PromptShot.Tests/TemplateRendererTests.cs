using System;
using System.Text.RegularExpressions;
using PromptShot.Templates;
using Xunit;

namespace PromptShot.Tests;

public class TemplateRendererTests
{
    private static readonly DateTimeOffset SampleTime =
        new(2026, 5, 9, 15, 32, 1, TimeSpan.FromHours(3));

    [Fact]
    public void Filename_substitutes_timestamp_and_rand()
    {
        var rendered = TemplateRenderer.RenderFilename("{timestamp}_{rand}.png", SampleTime);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}_[0-9a-f]{6}\.png$", rendered);
    }

    [Fact]
    public void Filename_supports_date_time_and_unix()
    {
        var rendered = TemplateRenderer.RenderFilename("{date}_T{time}_U{ts_unix}.png", SampleTime);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}_T\d{2}-\d{2}-\d{2}_U\d+\.png$", rendered);
    }

    [Fact]
    public void Unknown_placeholder_left_as_is()
    {
        var rendered = TemplateRenderer.RenderFilename("{nope}_{rand}.png", SampleTime);
        Assert.StartsWith("{nope}_", rendered);
        Assert.EndsWith(".png", rendered);
    }

    [Fact]
    public void Clipboard_returns_path_for_default_template()
    {
        var path = "C:\\tmp\\PromptShot\\shot.png";
        var rendered = TemplateRenderer.RenderClipboard("{path}", path, SampleTime);
        Assert.Equal(path, rendered);
    }

    [Fact]
    public void Clipboard_path_forward_uses_forward_slashes()
    {
        var path = "C:\\tmp\\PromptShot\\shot.png";
        var rendered = TemplateRenderer.RenderClipboard("{path_forward}", path, SampleTime);
        Assert.Equal("C:/tmp/PromptShot/shot.png", rendered);
    }

    [Fact]
    public void Clipboard_filename_extracts_basename()
    {
        var rendered = TemplateRenderer.RenderClipboard("[{filename}]", "C:\\a\\b\\c.png", SampleTime);
        Assert.Equal("[c.png]", rendered);
    }

    [Fact]
    public void Clipboard_unescapes_newline()
    {
        var rendered = TemplateRenderer.RenderClipboard("Look: {path}\\n", "C:\\x.png", SampleTime);
        Assert.Equal("Look: C:\\x.png\n", rendered);
    }

    [Fact]
    public void Clipboard_preserves_at_prefix()
    {
        var rendered = TemplateRenderer.RenderClipboard("@{path}", "C:\\x.png", SampleTime);
        Assert.Equal("@C:\\x.png", rendered);
    }

    [Fact]
    public void Empty_template_returns_empty()
    {
        Assert.Equal(string.Empty, TemplateRenderer.RenderFilename(string.Empty, SampleTime));
        Assert.Equal(string.Empty, TemplateRenderer.RenderClipboard(string.Empty, "x", SampleTime));
    }
}
