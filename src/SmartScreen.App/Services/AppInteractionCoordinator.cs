using System.Globalization;
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
    private readonly SemaphoreSlim _captureGate = new(1, 1);
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
        await RunCaptureExclusiveAsync(CaptureRegionCoreAsync, cancellationToken);
    }

    private async Task CaptureRegionCoreAsync(CancellationToken cancellationToken)
    {
        SetStatus(Text("capture.status.waitingRegion", "Очікую виділення області..."));
        var region = await windowService.SelectRegionAsync();

        if (region is null)
        {
            SetStatus(Text("capture.status.regionCancelled", "Виділення області скасовано"));
            return;
        }

        await HandleScreenshotAsync(
            await screenshotService.CaptureRegionAsync(region.Value, cancellationToken),
            cancellationToken);
    }

    public async Task CaptureDefaultAsync(CancellationToken cancellationToken = default)
    {
        await RunCaptureExclusiveAsync(CaptureDefaultCoreAsync, cancellationToken);
    }

    private async Task CaptureDefaultCoreAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.LoadAsync(cancellationToken);
        switch (settings.Screenshots.DefaultMode)
        {
            case ScreenshotMode.FullScreen:
                await CaptureFullScreenCoreAsync(cancellationToken);
                break;
            case ScreenshotMode.ActiveWindow:
                await CaptureActiveWindowCoreAsync(cancellationToken);
                break;
            case ScreenshotMode.Monitor:
                await CaptureMonitorCoreAsync(cancellationToken);
                break;
            case ScreenshotMode.Delayed:
                await CaptureDelayedCoreAsync(cancellationToken);
                break;
            case ScreenshotMode.Region:
            default:
                await CaptureRegionCoreAsync(cancellationToken);
                break;
        }
    }

    public async Task CaptureFullScreenAsync(CancellationToken cancellationToken = default)
    {
        await RunCaptureExclusiveAsync(CaptureFullScreenCoreAsync, cancellationToken);
    }

    private async Task CaptureFullScreenCoreAsync(CancellationToken cancellationToken)
    {
        SetStatus(Text("capture.status.creatingFullScreen", "Створюю скріншот всього екрана..."));
        await HandleScreenshotAsync(await screenshotService.CaptureFullScreenAsync(cancellationToken), cancellationToken);
    }

    public async Task CaptureActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        await RunCaptureExclusiveAsync(CaptureActiveWindowCoreAsync, cancellationToken);
    }

    private async Task CaptureActiveWindowCoreAsync(CancellationToken cancellationToken)
    {
        SetStatus(Text("capture.status.creatingActiveWindow", "Створюю скріншот активного вікна..."));
        await HandleScreenshotAsync(await screenshotService.CaptureActiveWindowAsync(cancellationToken), cancellationToken);
    }

    public async Task CaptureMonitorAsync(CancellationToken cancellationToken = default)
    {
        await RunCaptureExclusiveAsync(CaptureMonitorCoreAsync, cancellationToken);
    }

    private async Task CaptureMonitorCoreAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.LoadAsync(cancellationToken);
        SetStatus(FormatText(
            "capture.status.creatingMonitor",
            "Створюю скріншот монітора #{0}...",
            settings.Screenshots.MonitorIndex + 1));
        await HandleScreenshotAsync(
            await screenshotService.CaptureMonitorAsync(settings.Screenshots.MonitorIndex, cancellationToken),
            cancellationToken);
    }

    public async Task CaptureDelayedAsync(CancellationToken cancellationToken = default)
    {
        await RunCaptureExclusiveAsync(CaptureDelayedCoreAsync, cancellationToken);
    }

    private async Task CaptureDelayedCoreAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.LoadAsync(cancellationToken);
        var delaySeconds = Math.Clamp(settings.Screenshots.DelaySeconds, 1, 60);
        SetStatus(FormatText(
            "capture.status.delayCountdown",
            "Скріншот через {0} сек...",
            delaySeconds));
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
        await HandleScreenshotAsync(await screenshotService.CaptureFullScreenAsync(cancellationToken), cancellationToken);
    }

    private async Task RunCaptureExclusiveAsync(
        Func<CancellationToken, Task> captureAction,
        CancellationToken cancellationToken)
    {
        if (!await _captureGate.WaitAsync(0, cancellationToken))
        {
            SetStatus(Text("capture.status.alreadyRunning", "Сценарій скріншота вже виконується"));
            return;
        }

        try
        {
            await captureAction(cancellationToken);
        }
        finally
        {
            _captureGate.Release();
        }
    }

    public void AskAiForCurrentScreenshot(string? promptTemplateId = null)
    {
        if (CurrentScreenshot is null)
        {
            SetStatus(Text("main.status.captureFirst", "Спочатку зроби скріншот"));
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
            HotkeyAction.CaptureDefault => CaptureDefaultSafelyAsync(),
            HotkeyAction.CaptureRegion => CaptureRegionSafelyAsync(),
            HotkeyAction.CaptureFullScreen => CaptureFullScreenSafelyAsync(),
            HotkeyAction.CaptureActiveWindow => CaptureActiveWindowSafelyAsync(),
            HotkeyAction.CaptureMonitor => CaptureMonitorSafelyAsync(),
            HotkeyAction.CaptureDelayed => CaptureDelayedSafelyAsync(),
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

    private async Task CaptureDefaultSafelyAsync() => await RunSafelyAsync(CaptureDefaultAsync);

    private async Task CaptureRegionSafelyAsync() => await RunSafelyAsync(CaptureRegionAsync);

    private async Task CaptureFullScreenSafelyAsync() => await RunSafelyAsync(CaptureFullScreenAsync);

    private async Task CaptureActiveWindowSafelyAsync() => await RunSafelyAsync(CaptureActiveWindowAsync);

    private async Task CaptureMonitorSafelyAsync() => await RunSafelyAsync(CaptureMonitorAsync);

    private async Task CaptureDelayedSafelyAsync() => await RunSafelyAsync(CaptureDelayedAsync);

    private async Task RunSafelyAsync(Func<CancellationToken, Task> action)
    {
        try
        {
            await action(CancellationToken.None);
        }
        catch (Exception exception)
        {
            loggingService.Error(exception, "Interaction scenario failed.");
            SetStatus(Text("capture.status.actionFailedWithLogs", "Не вдалося виконати дію. Деталі записано в logs/app.log."));
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
                completedActions.Add(Text("capture.completed.clipboard", "буфер"));
            }
            catch (Exception exception)
            {
                loggingService.Error(exception, "Could not copy screenshot to clipboard. Continuing after-capture workflow.");
                completedActions.Add(Text("capture.completed.clipboardUnavailable", "буфер недоступний"));
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
            completedActions.Add(FormatText("capture.completed.file", "файл {0}", Path.GetFileName(path)));
        }

        SetStatus(completedActions.Count == 0
            ? FormatText("capture.status.ready", "Скріншот готовий: {0}x{1}", screenshot.Width, screenshot.Height)
            : FormatText(
                "capture.status.readyWithActions",
                "Скріншот готовий: {0}x{1}; {2}",
                screenshot.Width,
                screenshot.Height,
                string.Join(", ", completedActions)));

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

    private static string Text(string key, string fallback) =>
        LocalizationResourceService.GetString(key, fallback);

    private static string FormatText(string key, string fallback, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Text(key, fallback), args);
}
