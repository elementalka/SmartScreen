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
    private AppSettings? _settings;
    private AiProviderSettings? _selectedProvider;
    private string _status = "Налаштування";

    public SettingsViewModel(ISettingsService settingsService, IStorageService storageService, IAiService aiService)
    {
        _settingsService = settingsService;
        _storageService = storageService;
        _aiService = aiService;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        TestAiCommand = new AsyncRelayCommand(TestAiAsync);
    }

    public ObservableCollection<AiProviderSettings> Providers { get; } = [];

    public AiProviderSettings? SelectedProvider
    {
        get => _selectedProvider;
        set => SetProperty(ref _selectedProvider, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string ConfigDirectory => _storageService.Paths.ConfigDirectory;

    public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand TestAiCommand { get; }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        _settings = await _settingsService.LoadAsync(cancellationToken);
        Providers.Clear();

        foreach (var provider in _settings.Ai.Providers)
        {
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

        _settings.Ai.Providers = [.. Providers];
        await _settingsService.SaveAsync(_settings, cancellationToken);
        Status = "Налаштування збережено";
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

