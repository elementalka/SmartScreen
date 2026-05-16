using System.Collections.ObjectModel;
using System.Windows.Input;
using SmartScreen.Application.Abstractions;
using SmartScreen.App.Commands;
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
}
