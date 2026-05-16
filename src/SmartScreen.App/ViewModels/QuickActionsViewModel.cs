using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using SmartScreen.Application.Abstractions;
using SmartScreen.App.Commands;
using SmartScreen.App.Services;
using SmartScreen.Domain.Models;

namespace SmartScreen.App.ViewModels;

public sealed class QuickActionsViewModel : ObservableObject
{
    private readonly IClipboardService _clipboardService;
    private readonly IImageFileService _imageFileService;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;
    private readonly IWindowService _windowService;
    private readonly ILoggingService _loggingService;
    private ScreenshotResult _screenshot;
    private string _status = "Вибери дію";

    public QuickActionsViewModel(
        ScreenshotResult screenshot,
        IClipboardService clipboardService,
        IImageFileService imageFileService,
        ISettingsService settingsService,
        IStorageService storageService,
        IWindowService windowService,
        ILoggingService loggingService)
    {
        _screenshot = screenshot;
        _clipboardService = clipboardService;
        _imageFileService = imageFileService;
        _settingsService = settingsService;
        _storageService = storageService;
        _windowService = windowService;
        _loggingService = loggingService;
        PreviewImage = BitmapSourceFactory.FromScreenshot(screenshot);

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CopyCommand = new AsyncRelayCommand(CopyAsync);
        EditCommand = new AsyncRelayCommand(EditAsync);
        AskAiCommand = new RelayCommand(() => _windowService.ShowAiResponse(Screenshot));
        OpenFolderCommand = new RelayCommand(OpenFolder);
    }

    public event Action? CloseRequested;

    public ScreenshotResult Screenshot
    {
        get => _screenshot;
        private set
        {
            if (SetProperty(ref _screenshot, value))
            {
                PreviewImage = BitmapSourceFactory.FromScreenshot(value);
            }
        }
    }

    public ImageSource PreviewImage { get; private set; }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand AskAiCommand { get; }
    public ICommand OpenFolderCommand { get; }

    public void Close() => CloseRequested?.Invoke();

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken);
        var path = await _imageFileService.SaveAsync(
            Screenshot,
            settings.Screenshots.SaveDirectory,
            settings.Screenshots.DefaultFormat,
            settings.Screenshots.JpegQuality,
            cancellationToken);

        Status = $"Збережено: {Path.GetFileName(path)}";
    }

    private async Task CopyAsync(CancellationToken cancellationToken)
    {
        await _clipboardService.CopyImageAsync(Screenshot, cancellationToken);
        Status = "Скопійовано в буфер";
    }

    private async Task EditAsync(CancellationToken cancellationToken)
    {
        var edited = await _windowService.ShowEditorAsync(Screenshot);

        if (edited is null)
        {
            Status = "Редагування скасовано";
            return;
        }

        Screenshot = edited;
        OnPropertyChanged(nameof(PreviewImage));
        await _clipboardService.CopyImageAsync(Screenshot, cancellationToken);
        Status = "Відредаговано і скопійовано";
    }

    private void OpenFolder()
    {
        try
        {
            var directory = _storageService.ResolveWritableScreenshotsDirectory(null);
            Process.Start(new ProcessStartInfo("explorer.exe", directory) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            _loggingService.Error(exception, "Could not open screenshots folder.");
            Status = "Не вдалося відкрити папку";
        }
    }
}
