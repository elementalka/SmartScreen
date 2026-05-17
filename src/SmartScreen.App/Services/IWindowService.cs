using SmartScreen.Domain.Models;

namespace SmartScreen.App.Services;

public interface IWindowService
{
    Task<ScreenRegion?> SelectRegionAsync();
    Task ShowQuickActionsAsync(
        ScreenshotResult screenshot,
        CaptureWorkspaceStartupMode startupMode = CaptureWorkspaceStartupMode.Actions,
        string? promptTemplateId = null,
        string? customPrompt = null,
        bool startAiImmediately = false);
    void ShowSettings();
}
