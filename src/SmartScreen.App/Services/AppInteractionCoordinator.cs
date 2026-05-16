using SmartScreen.Application.Abstractions;
using SmartScreen.Domain.Enums;
using SmartScreen.Domain.Models;

namespace SmartScreen.App.Services;

public sealed class AppInteractionCoordinator(
    IScreenshotService screenshotService,
    IClipboardService clipboardService,
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

    public void AskAiForCurrentScreenshot()
    {
        if (CurrentScreenshot is null)
        {
            SetStatus("Спочатку зроби скріншот");
            return;
        }

        windowService.ShowAiResponse(CurrentScreenshot);
    }

    public void ShowSettings() => windowService.ShowSettings();

    public void HandleHotkey(HotkeyAction action)
    {
        _ = action switch
        {
            HotkeyAction.CaptureRegion => CaptureRegionSafelyAsync(),
            HotkeyAction.CaptureFullScreen => CaptureFullScreenSafelyAsync(),
            HotkeyAction.CaptureActiveWindow => CaptureActiveWindowSafelyAsync(),
            HotkeyAction.AskAiForCurrentScreenshot => RunSafelyAsync(_ =>
            {
                AskAiForCurrentScreenshot();
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
        CurrentScreenshot = screenshot;
        var settings = await settingsService.LoadAsync(cancellationToken);

        if (settings.Screenshots.CopyToClipboardAutomatically)
        {
            await clipboardService.CopyImageAsync(screenshot, cancellationToken);
        }

        SetStatus($"Скріншот готовий: {screenshot.Width}x{screenshot.Height}");

        if (settings.Screenshots.ShowQuickActionsAfterCapture)
        {
            await windowService.ShowQuickActionsAsync(screenshot);
        }
    }

    private void SetStatus(string status) => StatusChanged?.Invoke(this, status);
}
