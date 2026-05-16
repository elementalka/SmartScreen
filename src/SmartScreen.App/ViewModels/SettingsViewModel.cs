using System.Collections.ObjectModel;
using System.Windows.Input;
using SmartScreen.Application.Abstractions;
using SmartScreen.App.Commands;
using SmartScreen.Domain.Enums;
using SmartScreen.Domain.Models;

namespace SmartScreen.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;
    private readonly IAiService _aiService;
    private readonly IAiSecretService _aiSecretService;
    private AppSettings? _settings;
    private AiProviderSettings? _selectedProvider;
    private string _apiKeyInput = string.Empty;
    private string _status = "Налаштування";
    private bool _copyAfterCapture;
    private bool _saveAfterCapture;
    private bool _quickActionsAfterCapture;
    private bool _openEditorAfterCapture;
    private bool _askAiAfterCapture;

    public SettingsViewModel(
        ISettingsService settingsService,
        IStorageService storageService,
        IAiService aiService,
        IAiSecretService aiSecretService)
    {
        _settingsService = settingsService;
        _storageService = storageService;
        _aiService = aiService;
        _aiSecretService = aiSecretService;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        TestAiCommand = new AsyncRelayCommand(TestAiAsync);
    }

    public ObservableCollection<AiProviderSettings> Providers { get; } = [];

    public AiProviderSettings? SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (SetProperty(ref _selectedProvider, value))
            {
                ApiKeyInput = value?.ApiKey ?? string.Empty;
                OnPropertyChanged(nameof(SelectedProviderEnvironmentVariable));
                OnPropertyChanged(nameof(SelectedProviderHasKey));
            }
        }
    }

    public string ApiKeyInput
    {
        get => _apiKeyInput;
        set
        {
            if (SetProperty(ref _apiKeyInput, value))
            {
                if (SelectedProvider is not null)
                {
                    SelectedProvider.ApiKey = value;
                }

                OnPropertyChanged(nameof(SelectedProviderHasKey));
            }
        }
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string ConfigDirectory => _storageService.Paths.ConfigDirectory;

    public string SelectedProviderEnvironmentVariable =>
        SelectedProvider is null ? string.Empty : _aiSecretService.GetEnvironmentVariableName(SelectedProvider.Id);

    public bool SelectedProviderHasKey => !string.IsNullOrWhiteSpace(SelectedProvider?.ApiKey);

    public bool CopyAfterCapture
    {
        get => _copyAfterCapture;
        set => SetProperty(ref _copyAfterCapture, value);
    }

    public bool SaveAfterCapture
    {
        get => _saveAfterCapture;
        set => SetProperty(ref _saveAfterCapture, value);
    }

    public bool QuickActionsAfterCapture
    {
        get => _quickActionsAfterCapture;
        set => SetProperty(ref _quickActionsAfterCapture, value);
    }

    public bool OpenEditorAfterCapture
    {
        get => _openEditorAfterCapture;
        set => SetProperty(ref _openEditorAfterCapture, value);
    }

    public bool AskAiAfterCapture
    {
        get => _askAiAfterCapture;
        set => SetProperty(ref _askAiAfterCapture, value);
    }

    public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand TestAiCommand { get; }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        _settings = await _settingsService.LoadAsync(cancellationToken);
        Providers.Clear();

        foreach (var provider in _settings.Ai.Providers)
        {
            await _aiSecretService.ApplySecretsAsync(provider, cancellationToken);
            Providers.Add(provider);
        }

        SelectedProvider = Providers.FirstOrDefault(provider => provider.Id == _settings.Ai.ActiveProviderId)
            ?? Providers.FirstOrDefault();
        ApplyWorkflowToView(_settings.Screenshots.AfterCaptureActions);
        Status = "Налаштування завантажено";
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_settings is null)
        {
            await LoadAsync(cancellationToken);
        }

        if (_settings is null)
        {
            return;
        }

        if (SelectedProvider is not null)
        {
            _settings.Ai.ActiveProviderId = SelectedProvider.Id;
        }

        ApplyWorkflowToSettings(_settings.Screenshots);

        foreach (var provider in Providers)
        {
            if (!string.IsNullOrWhiteSpace(provider.ApiKey))
            {
                await _aiSecretService.SaveApiKeyAsync(provider.Id, provider.ApiKey, cancellationToken);
            }
        }

        _settings.Ai.Providers = [.. Providers];
        foreach (var provider in _settings.Ai.Providers)
        {
            provider.ApiKey = string.Empty;
        }

        await _settingsService.SaveAsync(_settings, cancellationToken);
        Status = "Налаштування збережено. API-ключі записано локально в secrets.local.json";
    }

    private async Task TestAiAsync(CancellationToken cancellationToken)
    {
        await SaveAsync(cancellationToken);
        Status = "Перевіряю підключення...";
        Status = await _aiService.TestActiveProviderAsync(cancellationToken)
            ? "Підключення працює"
            : "Підключення не вдалося перевірити";
    }

    private void ApplyWorkflowToView(IReadOnlyCollection<AfterCaptureAction> actions)
    {
        CopyAfterCapture = actions.Contains(AfterCaptureAction.CopyImageToClipboard);
        SaveAfterCapture = actions.Contains(AfterCaptureAction.SaveImageToFile);
        QuickActionsAfterCapture = actions.Contains(AfterCaptureAction.ShowQuickActions);
        OpenEditorAfterCapture = actions.Contains(AfterCaptureAction.OpenEditor);
        AskAiAfterCapture = actions.Contains(AfterCaptureAction.AskAi);
    }

    private void ApplyWorkflowToSettings(ScreenshotSettings screenshots)
    {
        var actions = new List<AfterCaptureAction>();

        if (OpenEditorAfterCapture)
        {
            actions.Add(AfterCaptureAction.OpenEditor);
        }

        if (CopyAfterCapture)
        {
            actions.Add(AfterCaptureAction.CopyImageToClipboard);
        }

        if (SaveAfterCapture)
        {
            actions.Add(AfterCaptureAction.SaveImageToFile);
        }

        if (QuickActionsAfterCapture)
        {
            actions.Add(AfterCaptureAction.ShowQuickActions);
        }

        if (AskAiAfterCapture)
        {
            actions.Add(AfterCaptureAction.AskAi);
        }

        screenshots.AfterCaptureActions = actions;
        screenshots.CopyToClipboardAutomatically = actions.Contains(AfterCaptureAction.CopyImageToClipboard);
        screenshots.ShowQuickActionsAfterCapture = actions.Contains(AfterCaptureAction.ShowQuickActions);
    }
}
