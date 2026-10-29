using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HomeDevops.Services;

public static class ImageService
{
    public const int LogoSize = 128;

    /// <summary>
    /// Lê a imagem escolhida, recorta um quadrado central, reduz para 128x128 e devolve um PNG como data URL.
    /// Retorna nulo se o arquivo não for uma imagem legível.
    /// </summary>
    public static string? LoadAsLogoDataUrl(string path)
    {
        try
        {
            var source = new BitmapImage();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.UriSource = new Uri(path);
            source.EndInit();

            var side = Math.Min(source.PixelWidth, source.PixelHeight);
            var square = new CroppedBitmap(
                source,
                new Int32Rect((source.PixelWidth - side) / 2, (source.PixelHeight - side) / 2, side, side));

            var visual = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
            using (var context = visual.RenderOpen())
            {
                context.DrawImage(square, new Rect(0, 0, LogoSize, LogoSize));
            }

            var target = new RenderTargetBitmap(LogoSize, LogoSize, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(target));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return "data:image/png;base64," + Convert.ToBase64String(stream.ToArray());
        }
        catch (Exception e) when (e is IOException or NotSupportedException or FileFormatException or UriFormatException or ArgumentException)
        {
            return null;
        }
    }
}
