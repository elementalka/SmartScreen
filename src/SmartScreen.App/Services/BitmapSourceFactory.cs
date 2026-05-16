using System.IO;
using System.Windows.Media.Imaging;
using SmartScreen.Domain.Models;

namespace SmartScreen.App.Services;

public static class BitmapSourceFactory
{
    public static BitmapImage FromScreenshot(ScreenshotResult screenshot)
    {
        using var stream = new MemoryStream(screenshot.ImageBytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}

