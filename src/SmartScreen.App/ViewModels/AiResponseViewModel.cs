using System.Collections.ObjectModel;
using System.Windows.Input;
using SmartScreen.Application.Abstractions;
using SmartScreen.App.Commands;
using SmartScreen.Domain.Models;

namespace SmartScreen.App.ViewModels;

public sealed class AiResponseViewModel : ObservableObject
{
    private readonly ScreenshotResult _screenshot;
    private readonly IAiService _aiService;
    private readonly IClipboardService _clipboardService;
    private readonly IPromptTemplateService _promptTemplateService;
    private CancellationTokenSource? _requestCts;
    private AiPromptTemplate? _selectedPrompt;
    private string _customPrompt = string.Empty;
    private string _responseText = string.Empty;
    private string _status = "Готово";
    private bool _isBusy;

    public AiResponseViewModel(
        ScreenshotResult screenshot,
        IAiService aiService,
        IClipboardService clipboardService,
        IPromptTemplateService promptTemplateService)
    {
        _screenshot = screenshot;
        _aiService = aiService;
        _clipboardService = clipboardService;
        _promptTemplateService = promptTemplateService;

        AskCommand = new AsyncRelayCommand(AskAsync, () => !IsBusy);
        CopyCommand = new AsyncRelayCommand(CopyAsync, () => !string.IsNullOrWhiteSpace(ResponseText));
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
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
        private set => SetProperty(ref _responseText, value);
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
            }
        }
    }

    public bool CanAsk => !IsBusy;

    public ICommand LoadCommand { get; }
    public ICommand AskCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand CancelCommand { get; }

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

        var response = await _aiService.AnalyzeCurrentScreenshotAsync(_screenshot, CustomPrompt, _requestCts.Token);

        IsBusy = false;
        Status = response.Success ? $"Готово за {response.Duration.TotalSeconds:N1} с" : response.ErrorMessage ?? "AI-помилка";
        ResponseText = response.Success ? response.Text ?? string.Empty : response.ErrorMessage ?? string.Empty;
    }

    private async Task CopyAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(ResponseText))
        {
            await _clipboardService.CopyTextAsync(ResponseText, cancellationToken);
            Status = "Відповідь скопійовано";
        }
    }

    private void Cancel()
    {
        _requestCts?.Cancel();
        Status = "Скасування...";
    }
}

