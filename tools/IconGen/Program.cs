using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;

// Generador one-shot del app.ico de cada app WPF.
// Rasteriza el SymbolIcon Fluent (blanco) sobre un fondo de marca redondeado,
// auto-ajustando el glifo al mismo "content box" para mantener peso visual
// consistente entre apps. Empaqueta un .ico multi-resolucion (PNG-in-ICO).
//
// Uso: IconGen <SymbolRegular> <#RRGGBB> <ruta-salida.ico>
// Parametros de estilo medidos del app.ico original (frame 256):
//   radio de esquina = 45/256, padding = 62/256, fondo #1565C0.
internal static class Program
{
    private static readonly int[] Sizes = { 16, 24, 32, 48, 64, 128, 256 };
    private const double RadiusRatio = 45.0 / 256.0;
    private const double PadRatio = 62.0 / 256.0;

    [STAThread]
    private static int Main(string[] args)
    {
        string symbolName = args.Length > 0 ? args[0] : "BuildingLighthouse24";
        string hex = args.Length > 1 ? args[1] : "#1565C0";
        string outPath = args.Length > 2 ? args[2] : "app.ico";

        // El SymbolIcon de WPF-UI renderiza "tofu" si no se mergean estos
        // diccionarios en una Application (la fuente Fluent va embebida en Wpf.Ui.dll).
        var app = new Application();
        app.Resources.MergedDictionaries.Add(new ThemesDictionary());
        app.Resources.MergedDictionaries.Add(new ControlsDictionary());

        var symbol = (SymbolRegular)Enum.Parse(typeof(SymbolRegular), symbolName);
        Color bg = ParseHex(hex);

        // 1) Render del glifo blanco a alta resolucion (para escalar nitido a 16px).
        const int src = 1024;
        var icon = new SymbolIcon
        {
            Symbol = symbol,
            Foreground = Brushes.White,
            FontSize = 820,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var host = new Grid { Width = src, Height = src, Background = Brushes.Transparent };
        host.Children.Add(icon);
        host.Measure(new Size(src, src));
        host.Arrange(new Rect(0, 0, src, src));
        host.UpdateLayout();

        var glyph = new RenderTargetBitmap(src, src, 96, 96, PixelFormats.Pbgra32);
        glyph.Render(host);

        // 2) Bounding box de tinta del glifo (alpha > 20).
        int stride = src * 4;
        var px = new byte[src * stride];
        glyph.CopyPixels(px, stride, 0);
        int minX = src, minY = src, maxX = -1, maxY = -1;
        for (int y = 0; y < src; y++)
        {
            int row = y * stride;
            for (int x = 0; x < src; x++)
            {
                if (px[row + x * 4 + 3] > 20)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }
        if (maxX < 0)
        {
            Console.Error.WriteLine($"ERROR: el simbolo '{symbolName}' no produjo tinta (tofu?).");
            return 2;
        }
        int iw = maxX - minX + 1, ih = maxY - minY + 1;
        var cropped = new CroppedBitmap(glyph, new Int32Rect(minX, minY, iw, ih));
        Console.WriteLine($"Glifo '{symbolName}' bbox {iw}x{ih} @ ({minX},{minY})");

        // 3) Componer cada tamano: fondo redondeado + glifo escalado al content box.
        var pngs = new List<byte[]>();
        foreach (int s in Sizes)
        {
            double radius = s * RadiusRatio;
            double pad = s * PadRatio;
            double content = s - 2 * pad;
            double scale = content / Math.Max(iw, ih);
            double dw = iw * scale, dh = ih * scale;
            double dx = (s - dw) / 2, dy = (s - dh) / 2;

            var dv = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.HighQuality);
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(bg), null,
                    new Rect(0, 0, s, s), radius, radius);
                dc.DrawImage(cropped, new Rect(dx, dy, dw, dh));
            }
            var rtb = new RenderTargetBitmap(s, s, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);

            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            enc.Save(ms);
            pngs.Add(ms.ToArray());
        }

        // 4) Ensamblar el .ico (ICONDIR + ICONDIRENTRY[] + frames PNG).
        WriteIco(outPath, Sizes, pngs);
        Console.WriteLine($"OK -> {Path.GetFullPath(outPath)} ({Sizes.Length} frames)");
        return 0;
    }

    private static void WriteIco(string path, int[] sizes, List<byte[]> pngs)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(fs);
        int n = sizes.Length;
        w.Write((ushort)0); // reserved
        w.Write((ushort)1); // type = icon
        w.Write((ushort)n); // count
        int offset = 6 + 16 * n;
        for (int i = 0; i < n; i++)
        {
            byte dim = sizes[i] >= 256 ? (byte)0 : (byte)sizes[i];
            w.Write(dim);              // width
            w.Write(dim);              // height
            w.Write((byte)0);          // color count
            w.Write((byte)0);          // reserved
            w.Write((ushort)1);        // planes
            w.Write((ushort)32);       // bit count
            w.Write((uint)pngs[i].Length);
            w.Write((uint)offset);
            offset += pngs[i].Length;
        }
        foreach (var png in pngs)
            w.Write(png);
    }

    private static Color ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        return Color.FromRgb(
            byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber),
            byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber),
            byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber));
    }
}
