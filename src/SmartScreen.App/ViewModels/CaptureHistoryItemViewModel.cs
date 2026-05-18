using System.Windows.Media;
using SmartScreen.App.Services;
using SmartScreen.Domain.Models;

namespace SmartScreen.App.ViewModels;

public sealed class CaptureHistoryItemViewModel : ObservableObject
{
    private readonly string _sourceName;

    public CaptureHistoryItemViewModel(ScreenshotResult screenshot)
    {
        Screenshot = screenshot;
        PreviewImage = BitmapSourceFactory.FromScreenshot(screenshot);
        _sourceName = screenshot.SourceName ?? "Screenshot";
        FileName = screenshot.SuggestedFileName;
        Dimensions = $"{screenshot.Width} x {screenshot.Height}";
        CreatedLabel = screenshot.CreatedAt.ToLocalTime().ToString("HH:mm:ss");
        Summary = $"{Dimensions} · {CreatedLabel}";
    }

    public ScreenshotResult Screenshot { get; }
    public ImageSource PreviewImage { get; }
    public string Title => LocalizeSourceName(_sourceName);
    public string FileName { get; }
    public string Dimensions { get; }
    public string CreatedLabel { get; }
    public string Summary { get; }

    public void RefreshLocalization() => OnPropertyChanged(nameof(Title));

    private static string LocalizeSourceName(string sourceName)
    {
        if (sourceName.StartsWith("Monitor ", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(sourceName["Monitor ".Length..], out var monitorNumber))
        {
            return string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                Text("capture.source.monitor", "Monitor {0}"),
                monitorNumber);
        }

        return sourceName switch
        {
            "Full screen" => Text("capture.source.fullScreen", "Full screen"),
            "Active window" => Text("capture.source.activeWindow", "Active window"),
            "Primary monitor" => Text("capture.source.primaryMonitor", "Primary monitor"),
            "Selected region" => Text("capture.source.selectedRegion", "Selected region"),
            "Saved screenshot" => Text("capture.source.savedScreenshot", "Saved screenshot"),
            "Edited screenshot" => Text("capture.source.editedScreenshot", "Edited screenshot"),
            "Edited cropped screenshot" => Text("capture.source.editedCroppedScreenshot", "Edited cropped screenshot"),
            _ => sourceName
        };
    }

    private static string Text(string key, string fallback) =>
        LocalizationResourceService.GetString(key, fallback);
}
