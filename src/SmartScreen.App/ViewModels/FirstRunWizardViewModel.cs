using System.Collections.ObjectModel;
using System.Windows.Input;
using SmartScreen.Application.Abstractions;
using SmartScreen.App.Commands;
using SmartScreen.Domain.Enums;
using SmartScreen.Domain.Models;

namespace SmartScreen.App.ViewModels;

public sealed class FirstRunWizardViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private AppSettings? _settings;
    private AiProviderSettings? _selectedProvider;
    private string _status = "Швидке налаштування SmartScreen";
    private bool _copyAfterCapture;
    private bool _quickActionsAfterCapture;
    private bool _openEditorAfterCapture;

    public FirstRunWizardViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        ThemeModeOptions =
        [
            new(ThemeMode.System, "Системна"),
            new(ThemeMode.Light, "Світла"),
            new(ThemeMode.Dark, "Темна")
        ];

        LanguageOptions =
        [
            new("uk-UA", "Українська"),
            new("en-US", "English")
        ];

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        CompleteCommand = new AsyncRelayCommand(CompleteAsync);
    }

    public event Action? CloseRequested;

    public ObservableCollection<AiProviderSettings> Providers { get; } = [];
    public IReadOnlyList<Option<ThemeMode>> ThemeModeOptions { get; }
    public IReadOnlyList<Option<string>> LanguageOptions { get; }

    public AppSettings? Settings
    {
        get => _settings;
        private set => SetProperty(ref _settings, value);
    }

    public AiProviderSettings? SelectedProvider
    {
        get => _selectedProvider;
        set => SetProperty(ref _selectedProvider, value);
    }

    public bool CopyAfterCapture
    {
        get => _copyAfterCapture;
        set => SetProperty(ref _copyAfterCapture, value);
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

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public ICommand LoadCommand { get; }
    public ICommand CompleteCommand { get; }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Settings = await _settingsService.LoadAsync(cancellationToken);
        Providers.Clear();

        foreach (var provider in Settings.Ai.Providers.Where(provider => provider.IsEnabled))
        {
            Providers.Add(provider);
        }

        SelectedProvider = Providers.FirstOrDefault(provider => provider.Id == Settings.Ai.ActiveProviderId)
            ?? Providers.FirstOrDefault();

        CopyAfterCapture = Settings.Screenshots.AfterCaptureActions.Contains(AfterCaptureAction.CopyImageToClipboard);
        QuickActionsAfterCapture = Settings.Screenshots.AfterCaptureActions.Contains(AfterCaptureAction.ShowQuickActions);
        OpenEditorAfterCapture = Settings.Screenshots.AfterCaptureActions.Contains(AfterCaptureAction.OpenEditor);
    }

    private async Task CompleteAsync(CancellationToken cancellationToken)
    {
        if (Settings is null)
        {
            await LoadAsync(cancellationToken);
        }

        if (Settings is null)
        {
            return;
        }

        Settings.FirstRunCompleted = true;
        Settings.StartMinimizedToTray = true;
        Settings.MinimizeToTrayOnClose = true;

        if (SelectedProvider is not null)
        {
            Settings.Ai.ActiveProviderId = SelectedProvider.Id;
        }

        var actions = new List<AfterCaptureAction>();
        if (OpenEditorAfterCapture)
        {
            actions.Add(AfterCaptureAction.OpenEditor);
        }

        if (CopyAfterCapture)
        {
            actions.Add(AfterCaptureAction.CopyImageToClipboard);
        }

        if (QuickActionsAfterCapture)
        {
            actions.Add(AfterCaptureAction.ShowQuickActions);
        }

        Settings.Screenshots.AfterCaptureActions = actions;
        Settings.Screenshots.CopyToClipboardAutomatically = CopyAfterCapture;
        Settings.Screenshots.ShowQuickActionsAfterCapture = QuickActionsAfterCapture;

        await _settingsService.SaveAsync(Settings, cancellationToken);
        Status = "Перший запуск завершено";
        CloseRequested?.Invoke();
    }
}
