using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
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
    private readonly IAiService _aiService;
    private readonly ILoggingService _loggingService;
    private ScreenshotResult _screenshot;
    private AiPromptTemplate? _selectedPrompt;
    private string _customPrompt = string.Empty;
    private string _aiResponseText = string.Empty;
    private string _aiStatus = "Готово";
    private string _status = "Вибери дію";
    private bool _isAiPanelOpen;
    private bool _isAiBusy;
    private CancellationTokenSource? _aiRequestCts;

    public QuickActionsViewModel(
        ScreenshotResult screenshot,
        IClipboardService clipboardService,
        IImageFileService imageFileService,
        ISettingsService settingsService,
        IStorageService storageService,
        IWindowService windowService,
        IPromptTemplateService promptTemplateService,
        IAiService aiService,
        ILoggingService loggingService)
    {
        _screenshot = screenshot;
        _clipboardService = clipboardService;
        _imageFileService = imageFileService;
        _settingsService = settingsService;
        _storageService = storageService;
        _windowService = windowService;
        _promptTemplateService = promptTemplateService;
        _aiService = aiService;
        _loggingService = loggingService;
        PreviewImage = BitmapSourceFactory.FromScreenshot(screenshot);

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CopyCommand = new AsyncRelayCommand(CopyAsync);
        EditCommand = new AsyncRelayCommand(EditAsync);
        AskAiCommand = new RelayCommand(OpenAiPanel);
        RunAiCommand = new AsyncRelayCommand(RunAiAsync, () => !IsAiBusy);
        CancelAiCommand = new RelayCommand(CancelAi, () => IsAiBusy);
        CopyAiResponseCommand = new AsyncRelayCommand(CopyAiResponseAsync, () => !string.IsNullOrWhiteSpace(AiResponseText));
        SaveAiResponseCommand = new AsyncRelayCommand(SaveAiResponseAsync, () => !string.IsNullOrWhiteSpace(AiResponseText));
        CloseAiPanelCommand = new RelayCommand(CloseAiPanel);
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

    public string AiResponseText
    {
        get => _aiResponseText;
        private set
        {
            if (SetProperty(ref _aiResponseText, value))
            {
                RaiseAiResultCommands();
            }
        }
    }

    public string AiStatus
    {
        get => _aiStatus;
        private set => SetProperty(ref _aiStatus, value);
    }

    public bool IsAiBusy
    {
        get => _isAiBusy;
        private set
        {
            if (SetProperty(ref _isAiBusy, value))
            {
                if (RunAiCommand is AsyncRelayCommand runCommand)
                {
                    runCommand.RaiseCanExecuteChanged();
                }

                if (CancelAiCommand is RelayCommand cancelCommand)
                {
                    cancelCommand.RaiseCanExecuteChanged();
                }
            }
        }
    }

    public bool IsAiPanelOpen
    {
        get => _isAiPanelOpen;
        private set
        {
            if (SetProperty(ref _isAiPanelOpen, value))
            {
                OnPropertyChanged(nameof(AiPanelVisibility));
                OnPropertyChanged(nameof(ActionPanelVisibility));
            }
        }
    }

    public Visibility AiPanelVisibility => IsAiPanelOpen ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ActionPanelVisibility => IsAiPanelOpen ? Visibility.Collapsed : Visibility.Visible;

    public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand AskAiCommand { get; }
    public ICommand RunAiCommand { get; }
    public ICommand CancelAiCommand { get; }
    public ICommand CopyAiResponseCommand { get; }
    public ICommand SaveAiResponseCommand { get; }
    public ICommand CloseAiPanelCommand { get; }
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

    public async Task ApplyEditedScreenshotAsync(ScreenshotResult screenshot, CancellationToken cancellationToken = default)
    {
        Screenshot = screenshot;
        OnPropertyChanged(nameof(PreviewImage));
        await _clipboardService.CopyImageAsync(Screenshot, cancellationToken);
        Status = "Відредаговано і скопійовано";
    }

    private void OpenAiPanel()
    {
        IsAiPanelOpen = true;
        AiStatus = "Вибери AI-дію або зміни prompt";
    }

    private void CloseAiPanel()
    {
        IsAiPanelOpen = false;
        Status = "AI-панель закрито";
    }

    private async Task RunAiAsync(CancellationToken cancellationToken)
    {
        var prompt = string.IsNullOrWhiteSpace(CustomPrompt)
            ? SelectedPrompt?.Prompt
            : CustomPrompt;

        if (string.IsNullOrWhiteSpace(prompt))
        {
            AiStatus = "Вибери AI-дію або введи prompt";
            return;
        }

        IsAiBusy = true;
        AiResponseText = string.Empty;
        AiStatus = "AI аналізує скріншот...";
        _aiRequestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            var response = await _aiService.AnalyzeCurrentScreenshotAsync(Screenshot, prompt, _aiRequestCts.Token);
            AiStatus = response.Success
                ? $"Готово за {response.Duration.TotalSeconds:N1} с"
                : response.ErrorMessage ?? "AI-помилка";
            AiResponseText = response.Success ? response.Text ?? string.Empty : response.ErrorMessage ?? string.Empty;
        }
        finally
        {
            IsAiBusy = false;
        }
    }

    private void CancelAi()
    {
        _aiRequestCts?.Cancel();
        AiStatus = "Скасування...";
    }

    private async Task CopyAiResponseAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(AiResponseText))
        {
            await _clipboardService.CopyTextAsync(AiResponseText, cancellationToken);
            AiStatus = "Відповідь скопійовано";
        }
    }

    private async Task SaveAiResponseAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(AiResponseText))
        {
            return;
        }

        await _storageService.EnsureDirectoriesAsync(cancellationToken);
        var directory = Path.Combine(_storageService.Paths.BaseDirectory, "responses");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"ai_response_{DateTimeOffset.Now:yyyy-MM-dd_HH-mm-ss}.txt");
        await File.WriteAllTextAsync(path, AiResponseText, cancellationToken);
        AiStatus = $"Відповідь збережено: {Path.GetFileName(path)}";
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

    private void RaiseAiResultCommands()
    {
        if (CopyAiResponseCommand is AsyncRelayCommand copyCommand)
        {
            copyCommand.RaiseCanExecuteChanged();
        }

        if (SaveAiResponseCommand is AsyncRelayCommand saveCommand)
        {
            saveCommand.RaiseCanExecuteChanged();
        }
    }
}
