using SmartScreen.Domain.Models;

namespace SmartScreen.App.Services;

public interface IWindowService
{
    Task<ScreenRegion?> SelectRegionAsync();
    Task ShowQuickActionsAsync(ScreenshotResult screenshot);
    Task<ScreenshotResult?> ShowEditorAsync(ScreenshotResult screenshot);
    void ShowAiResponse(
        ScreenshotResult screenshot,
        string? promptTemplateId = null,
        string? customPrompt = null,
        bool startImmediately = false);
    void ShowSettings();
}
