using System.Windows.Input;
using SmartScreen.Application.Abstractions;
using SmartScreen.App.Commands;
using SmartScreen.App.Services;
using SmartScreen.Domain.Models;

namespace SmartScreen.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IScreenshotService _screenshotService;
    private readonly IClipboardService _clipboardService;
    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;
    private readonly ILoggingService _loggingService;
    private string _status = "Готово до роботи";
    private ScreenshotResult? _currentScreenshot;

    public MainViewModel(
        IScreenshotService screenshotService,
        IClipboardService clipboardService,
        ISettingsService settingsService,
        IWindowService windowService,
        ILoggingService loggingService)
    {
        _screenshotService = screenshotService;
        _clipboardService = clipboardService;
        _settingsService = settingsService;
        _windowService = windowService;
        _loggingService = loggingService;

        CaptureFullScreenCommand = new AsyncRelayCommand(CaptureFullScreenAsync);
        CaptureRegionCommand = new AsyncRelayCommand(CaptureRegionAsync);
        CaptureActiveWindowCommand = new AsyncRelayCommand(CaptureActiveWindowAsync);
        AskAiCommand = new RelayCommand(AskAi, () => CurrentScreenshot is not null);
        OpenSettingsCommand = new RelayCommand(_windowService.ShowSettings);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public ScreenshotResult? CurrentScreenshot
    {
        get => _currentScreenshot;
        private set
        {
            if (SetProperty(ref _currentScreenshot, value) && AskAiCommand is RelayCommand command)
            {
                command.RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand CaptureFullScreenCommand { get; }
    public ICommand CaptureRegionCommand { get; }
    public ICommand CaptureActiveWindowCommand { get; }
    public ICommand AskAiCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    private async Task CaptureFullScreenAsync(CancellationToken cancellationToken)
    {
        Status = "Створюю скріншот всього екрана...";
        await HandleScreenshotAsync(await _screenshotService.CaptureFullScreenAsync(cancellationToken), cancellationToken);
    }

    private async Task CaptureRegionAsync(CancellationToken cancellationToken)
    {
        Status = "Очікую виділення області...";
        var region = await _windowService.SelectRegionAsync();

        if (region is null)
        {
            Status = "Виділення області скасовано";
            return;
        }

        await HandleScreenshotAsync(await _screenshotService.CaptureRegionAsync(region.Value, cancellationToken), cancellationToken);
    }

    private async Task CaptureActiveWindowAsync(CancellationToken cancellationToken)
    {
        Status = "Створюю скріншот активного вікна...";
        await HandleScreenshotAsync(await _screenshotService.CaptureActiveWindowAsync(cancellationToken), cancellationToken);
    }

    private async Task HandleScreenshotAsync(ScreenshotResult screenshot, CancellationToken cancellationToken)
    {
        CurrentScreenshot = screenshot;
        var settings = await _settingsService.LoadAsync(cancellationToken);

        if (settings.Screenshots.CopyToClipboardAutomatically)
        {
            await _clipboardService.CopyImageAsync(screenshot, cancellationToken);
        }

        Status = $"Скріншот готовий: {screenshot.Width}x{screenshot.Height}";

        if (settings.Screenshots.ShowQuickActionsAfterCapture)
        {
            await _windowService.ShowQuickActionsAsync(screenshot);
        }
    }

    private void AskAi()
    {
        if (CurrentScreenshot is null)
        {
            Status = "Спочатку зроби скріншот";
            return;
        }

        try
        {
            _windowService.ShowAiResponse(CurrentScreenshot);
        }
        catch (Exception exception)
        {
            _loggingService.Error(exception, "Could not open AI response window.");
            Status = "Не вдалося відкрити AI-вікно";
        }
    }
}

