using System.IO;
using SmartScreen.Application.Abstractions;
using SmartScreen.Domain.Enums;
using SmartScreen.Domain.Models;

namespace SmartScreen.App.Services;

public sealed class AppInteractionCoordinator(
    IScreenshotService screenshotService,
    IClipboardService clipboardService,
    IImageFileService imageFileService,
    ISettingsService settingsService,
    IWindowService windowService,
    ILoggingService loggingService)
{
    private ScreenshotResult? _currentScreenshot;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<ScreenshotResult?>? CurrentScreenshotChanged;

    public ScreenshotResult? CurrentScreenshot
    {
        get => _currentScreenshot;
        private set
        {
            _currentScreenshot = value;
            CurrentScreenshotChanged?.Invoke(this, value);
        }
    }

    public async Task CaptureRegionAsync(CancellationToken cancellationToken = default)
    {
        SetStatus("Очікую виділення області...");
        var region = await windowService.SelectRegionAsync();

        if (region is null)
        {
            SetStatus("Виділення області скасовано");
            return;
        }

        await HandleScreenshotAsync(
            await screenshotService.CaptureRegionAsync(region.Value, cancellationToken),
            cancellationToken);
    }

    public async Task CaptureFullScreenAsync(CancellationToken cancellationToken = default)
    {
        SetStatus("Створюю скріншот всього екрана...");
        await HandleScreenshotAsync(await screenshotService.CaptureFullScreenAsync(cancellationToken), cancellationToken);
    }

    public async Task CaptureActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        SetStatus("Створюю скріншот активного вікна...");
        await HandleScreenshotAsync(await screenshotService.CaptureActiveWindowAsync(cancellationToken), cancellationToken);
    }

    public void AskAiForCurrentScreenshot(string? promptTemplateId = null)
    {
        if (CurrentScreenshot is null)
        {
            SetStatus("Спочатку зроби скріншот");
            return;
        }

        _ = windowService.ShowQuickActionsAsync(
            CurrentScreenshot,
            CaptureWorkspaceStartupMode.Ai,
            promptTemplateId,
            startAiImmediately: promptTemplateId is not null);
    }

    public void ShowSettings() => windowService.ShowSettings();

    public void HandleHotkey(HotkeyAction action, string? promptTemplateId = null)
    {
        _ = action switch
        {
            HotkeyAction.CaptureRegion => CaptureRegionSafelyAsync(),
            HotkeyAction.CaptureFullScreen => CaptureFullScreenSafelyAsync(),
            HotkeyAction.CaptureActiveWindow => CaptureActiveWindowSafelyAsync(),
            HotkeyAction.AskAiForCurrentScreenshot => RunSafelyAsync(_ =>
            {
                AskAiForCurrentScreenshot(promptTemplateId);
                return Task.CompletedTask;
            }),
            HotkeyAction.OpenSettings => RunSafelyAsync(_ =>
            {
                ShowSettings();
                return Task.CompletedTask;
            }),
            _ => Task.CompletedTask
        };
    }

    private async Task CaptureRegionSafelyAsync() => await RunSafelyAsync(CaptureRegionAsync);

    private async Task CaptureFullScreenSafelyAsync() => await RunSafelyAsync(CaptureFullScreenAsync);

    private async Task CaptureActiveWindowSafelyAsync() => await RunSafelyAsync(CaptureActiveWindowAsync);

    private async Task RunSafelyAsync(Func<CancellationToken, Task> action)
    {
        try
        {
            await action(CancellationToken.None);
        }
        catch (Exception exception)
        {
            loggingService.Error(exception, "Interaction scenario failed.");
            SetStatus("Не вдалося виконати дію. Деталі записано в logs/app.log.");
        }
    }

    private async Task HandleScreenshotAsync(ScreenshotResult screenshot, CancellationToken cancellationToken)
    {
        var settings = await settingsService.LoadAsync(cancellationToken);
        var actions = settings.Screenshots.AfterCaptureActions;

        CurrentScreenshot = screenshot;

        var completedActions = new List<string>();

        if (actions.Contains(AfterCaptureAction.CopyImageToClipboard))
        {
            try
            {
                await clipboardService.CopyImageAsync(screenshot, cancellationToken);
                completedActions.Add("буфер");
            }
            catch (Exception exception)
            {
                loggingService.Error(exception, "Could not copy screenshot to clipboard. Continuing after-capture workflow.");
                completedActions.Add("буфер недоступний");
            }
        }

        if (actions.Contains(AfterCaptureAction.SaveImageToFile))
        {
            var path = await imageFileService.SaveAsync(
                screenshot,
                settings.Screenshots.SaveDirectory,
                settings.Screenshots.DefaultFormat,
                settings.Screenshots.JpegQuality,
                cancellationToken);
            completedActions.Add($"файл {Path.GetFileName(path)}");
        }

        SetStatus(completedActions.Count == 0
            ? $"Скріншот готовий: {screenshot.Width}x{screenshot.Height}"
            : $"Скріншот готовий: {screenshot.Width}x{screenshot.Height}; {string.Join(", ", completedActions)}");

        if (actions.Contains(AfterCaptureAction.ShowQuickActions) ||
            actions.Contains(AfterCaptureAction.OpenEditor) ||
            actions.Contains(AfterCaptureAction.AskAi))
        {
            var startupMode = actions.Contains(AfterCaptureAction.OpenEditor)
                ? CaptureWorkspaceStartupMode.Editor
                : actions.Contains(AfterCaptureAction.AskAi)
                    ? CaptureWorkspaceStartupMode.Ai
                    : CaptureWorkspaceStartupMode.Actions;

            await windowService.ShowQuickActionsAsync(
                screenshot,
                startupMode,
                startAiImmediately: startupMode == CaptureWorkspaceStartupMode.Ai);
        }
    }

    private void SetStatus(string status) => StatusChanged?.Invoke(this, status);
}
