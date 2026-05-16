using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using SmartScreen.Application.Abstractions;
using SmartScreen.App.Commands;
using SmartScreen.App.Services;
using SmartScreen.Domain.Models;

namespace SmartScreen.App.ViewModels;

public sealed class AiResponseViewModel : ObservableObject
{
    private readonly ScreenshotResult _screenshot;
    private readonly IAiService _aiService;
    private readonly IClipboardService _clipboardService;
    private readonly IPromptTemplateService _promptTemplateService;
    private readonly IStorageService _storageService;
    private readonly IWindowService _windowService;
    private readonly string? _initialPromptTemplateId;
    private readonly string? _initialCustomPrompt;
    private readonly bool _startImmediately;
    private CancellationTokenSource? _requestCts;
    private AiPromptTemplate? _selectedPrompt;
    private string _customPrompt = string.Empty;
    private string _responseText = string.Empty;
    private string _status = "Готово";
    private bool _isBusy;
    private bool _hasAutoStarted;

    public AiResponseViewModel(
        ScreenshotResult screenshot,
        IAiService aiService,
        IClipboardService clipboardService,
        IPromptTemplateService promptTemplateService,
        IStorageService storageService,
        IWindowService windowService,
        string? initialPromptTemplateId = null,
        string? initialCustomPrompt = null,
        bool startImmediately = false)
    {
        _screenshot = screenshot;
        _aiService = aiService;
        _clipboardService = clipboardService;
        _promptTemplateService = promptTemplateService;
        _storageService = storageService;
        _windowService = windowService;
        _initialPromptTemplateId = initialPromptTemplateId;
        _initialCustomPrompt = initialCustomPrompt;
        _startImmediately = startImmediately;

        AskCommand = new AsyncRelayCommand(AskAsync, () => !IsBusy);
        CopyCommand = new AsyncRelayCommand(CopyAsync, () => !string.IsNullOrWhiteSpace(ResponseText));
        SaveResponseCommand = new AsyncRelayCommand(SaveResponseAsync, () => !string.IsNullOrWhiteSpace(ResponseText));
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        OpenSettingsCommand = new RelayCommand(_windowService.ShowSettings);
        LoadCommand = new AsyncRelayCommand(LoadAsync);
    }

    public ObservableCollection<AiPromptTemplate> Prompts { get; } = [];

    public AiPromptTemplate? SelectedPrompt
    {
        get => _selectedPrompt;
        set
        {
            if (SetProperty(ref _selectedPrompt, value) && value is not null)
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

    public string ResponseText
    {
        get => _responseText;
        private set
        {
            if (SetProperty(ref _responseText, value))
            {
                RaiseResultCommands();
            }
        }
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanAsk));
                if (CancelCommand is RelayCommand cancelCommand)
                {
                    cancelCommand.RaiseCanExecuteChanged();
                }
            }
        }
    }

    public bool CanAsk => !IsBusy;

    public ICommand LoadCommand { get; }
    public ICommand AskCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand SaveResponseCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (Prompts.Count == 0)
        {
            var library = await _promptTemplateService.LoadAsync(cancellationToken);

            foreach (var prompt in library.Templates.OrderBy(prompt => prompt.Order))
            {
                Prompts.Add(prompt);
            }
        }

        SelectedPrompt = Prompts.FirstOrDefault(prompt => prompt.Id == _initialPromptTemplateId)
            ?? Prompts.FirstOrDefault(prompt => prompt.Id == "describe")
            ?? Prompts.FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(_initialCustomPrompt))
        {
            CustomPrompt = _initialCustomPrompt;
        }

        if (_startImmediately && !_hasAutoStarted)
        {
            _hasAutoStarted = true;
            await AskAsync(cancellationToken);
        }
    }

    private async Task AskAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(CustomPrompt))
        {
            Status = "Введи prompt або вибери шаблон";
            return;
        }

        IsBusy = true;
        ResponseText = string.Empty;
        Status = "AI аналізує скріншот...";
        _requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            var response = await _aiService.AnalyzeCurrentScreenshotAsync(_screenshot, CustomPrompt, _requestCts.Token);
            Status = response.Success
                ? $"Готово за {response.Duration.TotalSeconds:N1} с"
                : response.ErrorMessage ?? "AI-помилка";
            ResponseText = response.Success ? response.Text ?? string.Empty : response.ErrorMessage ?? string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CopyAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(ResponseText))
        {
            await _clipboardService.CopyTextAsync(ResponseText, cancellationToken);
            Status = "Відповідь скопійовано";
        }
    }

    private async Task SaveResponseAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ResponseText))
        {
            return;
        }

        await _storageService.EnsureDirectoriesAsync(cancellationToken);
        var directory = Path.Combine(_storageService.Paths.BaseDirectory, "responses");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"ai_response_{DateTimeOffset.Now:yyyy-MM-dd_HH-mm-ss}.txt");
        await File.WriteAllTextAsync(path, ResponseText, cancellationToken);
        Status = $"Відповідь збережено: {Path.GetFileName(path)}";
    }

    private void Cancel()
    {
        _requestCts?.Cancel();
        Status = "Скасування...";
    }

    private void RaiseResultCommands()
    {
        if (CopyCommand is AsyncRelayCommand copyCommand)
        {
            copyCommand.RaiseCanExecuteChanged();
        }

        if (SaveResponseCommand is AsyncRelayCommand saveCommand)
        {
            saveCommand.RaiseCanExecuteChanged();
        }
    }
}
