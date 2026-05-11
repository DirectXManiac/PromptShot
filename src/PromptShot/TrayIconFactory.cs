using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace PromptShot;

/// <summary>
/// Генерация иконки трея в коде, чтобы не таскать с собой бинарный .ico.
/// Делает 32×32 значок: тёмно-синий скруглённый фон + белая буква "P".
/// </summary>
internal static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon Create()
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            using (var path = RoundedRect(new Rectangle(0, 0, size, size), 6))
            using (var brush = new LinearGradientBrush(
                new Point(0, 0), new Point(0, size),
                Color.FromArgb(255, 32, 110, 220),
                Color.FromArgb(255, 18, 70, 160)))
            {
                g.FillPath(brush, path);
            }

            using var font = new Font("Segoe UI", 18, FontStyle.Bold, GraphicsUnit.Pixel);
            using var textBrush = new SolidBrush(Color.White);
            using var fmt = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            g.DrawString("P", font, textBrush, new RectangleF(0, 1, size, size), fmt);
        }

        var hIcon = bmp.GetHicon();
        try
        {
            // Icon.FromHandle не владеет хендлом — клонируем в managed-копию и
            // освобождаем оригинал, чтобы не утекал GDI handle.
            using var raw = Icon.FromHandle(hIcon);
            return (Icon)raw.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
