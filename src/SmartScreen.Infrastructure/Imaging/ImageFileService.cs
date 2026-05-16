using System.Drawing;
using System.Drawing.Imaging;
using SmartScreen.Application.Abstractions;
using SmartScreen.Domain.Enums;
using SmartScreen.Domain.Models;

namespace SmartScreen.Infrastructure.Imaging;

public sealed class ImageFileService(IStorageService storageService) : IImageFileService
{
    public async Task<string> SaveAsync(
        ScreenshotResult screenshot,
        string? directory,
        ScreenshotImageFormat format,
        int jpegQuality,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var targetDirectory = storageService.ResolveWritableScreenshotsDirectory(directory);
        var fileName = FileNameHelper.CreateScreenshotFileName(screenshot.CreatedAt, format);
        var path = Path.Combine(targetDirectory, fileName);

        if (format == ScreenshotImageFormat.Png && screenshot.MimeType.Equals("image/png", StringComparison.OrdinalIgnoreCase))
        {
            await File.WriteAllBytesAsync(path, screenshot.ImageBytes, cancellationToken);
            return path;
        }

        await Task.Run(() => SaveWithEncoder(screenshot.ImageBytes, path, format, jpegQuality), cancellationToken);
        return path;
    }

    private static void SaveWithEncoder(byte[] bytes, string path, ScreenshotImageFormat format, int jpegQuality)
    {
        using var sourceStream = new MemoryStream(bytes);
        using var image = Image.FromStream(sourceStream);

        if (format == ScreenshotImageFormat.Png)
        {
            image.Save(path, ImageFormat.Png);
            return;
        }

        var codec = ImageCodecInfo.GetImageDecoders()
            .FirstOrDefault(decoder => decoder.FormatID == ImageFormat.Jpeg.Guid);

        if (codec is null)
        {
            image.Save(path, ImageFormat.Jpeg);
            return;
        }

        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, Math.Clamp(jpegQuality, 1, 100));
        image.Save(path, codec, parameters);
    }
}

