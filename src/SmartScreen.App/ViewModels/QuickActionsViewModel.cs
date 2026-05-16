using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;
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
    private readonly IPromptTemplateService _promptTemplateService;
    private readonly ILoggingService _loggingService;
    private ScreenshotResult _screenshot;
    private AiPromptTemplate? _selectedPrompt;
    private string _customPrompt = string.Empty;
    private string _status = "Вибери дію";

    public QuickActionsViewModel(
        ScreenshotResult screenshot,
        IClipboardService clipboardService,
        IImageFileService imageFileService,
        ISettingsService settingsService,
        IStorageService storageService,
        IWindowService windowService,
        IPromptTemplateService promptTemplateService,
        ILoggingService loggingService)
    {
        _screenshot = screenshot;
        _clipboardService = clipboardService;
        _imageFileService = imageFileService;
        _settingsService = settingsService;
        _storageService = storageService;
        _windowService = windowService;
        _promptTemplateService = promptTemplateService;
        _loggingService = loggingService;
        PreviewImage = BitmapSourceFactory.FromScreenshot(screenshot);

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CopyCommand = new AsyncRelayCommand(CopyAsync);
        EditCommand = new AsyncRelayCommand(EditAsync);
        AskAiCommand = new RelayCommand(AskAi);
        OpenFolderCommand = new RelayCommand(OpenFolder);
    }

    public event Action? CloseRequested;
    public ObservableCollection<AiPromptTemplate> Prompts { get; } = [];

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

    public AiPromptTemplate? SelectedPrompt
    {
        get => _selectedPrompt;
        set
        {
            if (SetProperty(ref _selectedPrompt, value) && value is not null && string.IsNullOrWhiteSpace(CustomPrompt))
            {
                CustomPrompt = value.Prompt;
            }
        }
    }

    public string CustomPrompt
    {
        get => _customPrompt;
        set => SetProperty(ref _customPrompt, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand AskAiCommand { get; }
    public ICommand OpenFolderCommand { get; }

    public void Close() => CloseRequested?.Invoke();

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (Prompts.Count > 0)
        {
            return;
        }

        var library = await _promptTemplateService.LoadAsync(cancellationToken);
        foreach (var prompt in library.Templates.OrderBy(prompt => prompt.Order))
        {
            Prompts.Add(prompt);
        }

        SelectedPrompt = Prompts.FirstOrDefault(prompt => prompt.Id == "describe") ?? Prompts.FirstOrDefault();
        if (SelectedPrompt is not null)
        {
            CustomPrompt = SelectedPrompt.Prompt;
        }
    }

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

    private void AskAi()
    {
        var prompt = string.IsNullOrWhiteSpace(CustomPrompt)
            ? SelectedPrompt?.Prompt
            : CustomPrompt;

        if (string.IsNullOrWhiteSpace(prompt))
        {
            Status = "Вибери AI-дію або введи prompt";
            return;
        }

        Status = "Передаю скріншот до AI...";
        _windowService.ShowAiResponse(Screenshot, SelectedPrompt?.Id, prompt, startImmediately: true);
        CloseRequested?.Invoke();
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
