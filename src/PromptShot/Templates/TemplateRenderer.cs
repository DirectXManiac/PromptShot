using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PromptShot.Templates;

/// <summary>
/// Рендеринг шаблонов имени файла и содержимого clipboard.
/// Все плейсхолдеры в формате <c>{name}</c>. Неизвестные оставляются как есть.
/// </summary>
internal static class TemplateRenderer
{
    public static string RenderFilename(string template, DateTimeOffset timestamp)
    {
        var ctx = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["timestamp"] = timestamp.ToLocalTime().ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture),
            ["ts_unix"] = timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["date"] = timestamp.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["time"] = timestamp.ToLocalTime().ToString("HH-mm-ss", CultureInfo.InvariantCulture),
            ["rand"] = RandomHex(6),
        };
        return Substitute(template, ctx);
    }

    public static string RenderClipboard(string template, string filePath, DateTimeOffset timestamp)
    {
        var ctx = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["path"] = filePath,
            ["path_forward"] = filePath.Replace('\\', '/'),
            ["filename"] = Path.GetFileName(filePath),
            ["timestamp"] = timestamp.ToLocalTime().ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture),
            ["ts_unix"] = timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["date"] = timestamp.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["time"] = timestamp.ToLocalTime().ToString("HH-mm-ss", CultureInfo.InvariantCulture),
        };
        // Поддержка escape-последовательностей применяется к ТЕКСТУ ШАБЛОНА (литералам),
        // а не к подставленным значениям — иначе Windows-пути с \t, \n внутри ломались бы.
        var unescapedTemplate = UnescapeBackslashSequences(template);
        return Substitute(unescapedTemplate, ctx);
    }

    private static string Substitute(string template, IReadOnlyDictionary<string, string> ctx)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        var sb = new StringBuilder(template.Length + 32);
        var i = 0;
        while (i < template.Length)
        {
            var ch = template[i];
            if (ch == '{')
            {
                var end = template.IndexOf('}', i + 1);
                if (end > i)
                {
                    var key = template.Substring(i + 1, end - i - 1);
                    if (ctx.TryGetValue(key, out var value))
                    {
                        sb.Append(value);
                        i = end + 1;
                        continue;
                    }
                }
            }
            sb.Append(ch);
            i++;
        }
        return sb.ToString();
    }

    private static string UnescapeBackslashSequences(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf('\\') < 0) return s;
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (ch == '\\' && i + 1 < s.Length)
            {
                var next = s[i + 1];
                switch (next)
                {
                    case 'n': sb.Append('\n'); i++; continue;
                    case 'r': sb.Append('\r'); i++; continue;
                    case 't': sb.Append('\t'); i++; continue;
                    case '\\': sb.Append('\\'); i++; continue;
                }
            }
            sb.Append(ch);
        }
        return sb.ToString();
    }

    private static string RandomHex(int length)
    {
        var byteCount = (length + 1) / 2;
        Span<byte> buf = stackalloc byte[byteCount];
        RandomNumberGenerator.Fill(buf);
        var hex = Convert.ToHexString(buf).ToLowerInvariant();
        return hex.Length > length ? hex[..length] : hex;
    }
}
