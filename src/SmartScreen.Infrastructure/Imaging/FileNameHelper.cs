using SmartScreen.Domain.Enums;

namespace SmartScreen.Infrastructure.Imaging;

internal static class FileNameHelper
{
    public static string CreateScreenshotFileName(DateTimeOffset timestamp, ScreenshotImageFormat format)
    {
        var extension = format == ScreenshotImageFormat.Png ? "png" : "jpg";
        return $"screenshot_{timestamp:yyyy-MM-dd_HH-mm-ss}.{extension}";
    }
}

