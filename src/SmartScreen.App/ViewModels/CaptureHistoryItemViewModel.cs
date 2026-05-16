using System.Windows.Media;
using SmartScreen.App.Services;
using SmartScreen.Domain.Models;

namespace SmartScreen.App.ViewModels;

public sealed class CaptureHistoryItemViewModel
{
    public CaptureHistoryItemViewModel(ScreenshotResult screenshot)
    {
        Screenshot = screenshot;
        PreviewImage = BitmapSourceFactory.FromScreenshot(screenshot);
        Title = screenshot.SourceName ?? "Screenshot";
        FileName = screenshot.SuggestedFileName;
        Dimensions = $"{screenshot.Width} x {screenshot.Height}";
        CreatedLabel = screenshot.CreatedAt.ToLocalTime().ToString("HH:mm:ss");
        Summary = $"{Dimensions} · {CreatedLabel}";
    }

    public ScreenshotResult Screenshot { get; }
    public ImageSource PreviewImage { get; }
    public string Title { get; }
    public string FileName { get; }
    public string Dimensions { get; }
    public string CreatedLabel { get; }
    public string Summary { get; }
}
