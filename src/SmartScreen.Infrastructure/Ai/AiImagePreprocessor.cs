using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SmartScreen.Domain.Models;

namespace SmartScreen.Infrastructure.Ai;

internal static class AiImagePreprocessor
{
    private const int MaxDimension = 1800;
    private const int MaxPayloadBytes = 3 * 1024 * 1024;
    private const long InitialJpegQuality = 88;
    private const long MinimumJpegQuality = 68;

    public static PreparedAiImage Prepare(ScreenshotResult screenshot)
    {
        if (screenshot.ImageBytes.Length <= MaxPayloadBytes &&
            Math.Max(screenshot.Width, screenshot.Height) <= MaxDimension)
        {
            return new PreparedAiImage(
                screenshot.ImageBytes,
                screenshot.MimeType,
                screenshot.Width,
                screenshot.Height,
                WasOptimized: false,
                screenshot.ImageBytes.Length);
        }

        try
        {
            return Optimize(screenshot);
        }
        catch (Exception) when (screenshot.ImageBytes.Length > 0)
        {
            return new PreparedAiImage(
                screenshot.ImageBytes,
                screenshot.MimeType,
                screenshot.Width,
                screenshot.Height,
                WasOptimized: false,
                screenshot.ImageBytes.Length);
        }
    }

    private static PreparedAiImage Optimize(ScreenshotResult screenshot)
    {
        using var sourceStream = new MemoryStream(screenshot.ImageBytes);
        using var source = Image.FromStream(sourceStream, useEmbeddedColorManagement: false, validateImageData: false);

        var (targetWidth, targetHeight) = GetTargetSize(source.Width, source.Height, MaxDimension);
        var quality = InitialJpegQuality;

        using var firstBitmap = Resize(source, targetWidth, targetHeight);
        var encoded = EncodeJpeg(firstBitmap, quality);

        while (encoded.Length > MaxPayloadBytes && quality > MinimumJpegQuality)
        {
            quality -= 8;
            encoded = EncodeJpeg(firstBitmap, quality);
        }

        var currentWidth = targetWidth;
        var currentHeight = targetHeight;
        while (encoded.Length > MaxPayloadBytes && Math.Max(currentWidth, currentHeight) > 1200)
        {
            currentWidth = Math.Max(1, (int)Math.Round(currentWidth * 0.86));
            currentHeight = Math.Max(1, (int)Math.Round(currentHeight * 0.86));

            using var smallerBitmap = Resize(source, currentWidth, currentHeight);
            encoded = EncodeJpeg(smallerBitmap, InitialJpegQuality);
            quality = InitialJpegQuality;

            while (encoded.Length > MaxPayloadBytes && quality > MinimumJpegQuality)
            {
                quality -= 8;
                encoded = EncodeJpeg(smallerBitmap, quality);
            }
        }

        return new PreparedAiImage(
            encoded,
            "image/jpeg",
            currentWidth,
            currentHeight,
            WasOptimized: true,
            screenshot.ImageBytes.Length);
    }

    private static (int Width, int Height) GetTargetSize(int width, int height, int maxDimension)
    {
        var largest = Math.Max(width, height);
        if (largest <= maxDimension)
        {
            return (width, height);
        }

        var scale = maxDimension / (double)largest;
        return (
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)));
    }

    private static Bitmap Resize(Image source, int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        bitmap.SetResolution(96, 96);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.DrawImage(source, 0, 0, width, height);

        return bitmap;
    }

    private static byte[] EncodeJpeg(Image image, long quality)
    {
        using var stream = new MemoryStream();
        var encoder = ImageCodecInfo.GetImageEncoders()
            .First(codec => string.Equals(codec.MimeType, "image/jpeg", StringComparison.OrdinalIgnoreCase));
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
        image.Save(stream, encoder, parameters);
        return stream.ToArray();
    }
}
