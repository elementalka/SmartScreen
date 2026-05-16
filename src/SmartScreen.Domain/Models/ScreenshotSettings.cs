using SmartScreen.Domain.Enums;

namespace SmartScreen.Domain.Models;

public sealed class ScreenshotSettings
{
    public ScreenshotImageFormat DefaultFormat { get; set; } = ScreenshotImageFormat.Png;
    public int JpegQuality { get; set; } = 90;
    public string FileNameTemplate { get; set; } = "screenshot_{yyyy-MM-dd}_{HH-mm-ss}";
    public ScreenshotMode DefaultMode { get; set; } = ScreenshotMode.Region;
    public bool CopyToClipboardAutomatically { get; set; } = true;
    public bool ShowQuickActionsAfterCapture { get; set; } = true;
    public List<AfterCaptureAction> AfterCaptureActions { get; set; } = [];

    public int DelaySeconds { get; set; }
    public string SaveDirectory { get; set; } = "screenshots";
}
