using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using SmartScreen.Application.Abstractions;
using SmartScreen.App.Commands;
using SmartScreen.App.Services;
using SmartScreen.Domain.Models;

namespace SmartScreen.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const int MaxRecentCaptures = 12;

    private readonly AppInteractionCoordinator _coordinator;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;
    private readonly ILoggingService _loggingService;
    private string _status = "Готово до роботи";
    private string _activeProviderName = "AI не налаштовано";
    private string _activeProviderModel = "Відкрий налаштування";
    private string _storageSummary = "screenshots";
    private string _workflowSummary = "Ctrl+Shift+S · clipboard · quick menu";
    private ScreenshotResult? _currentScreenshot;
    private CaptureHistoryItemViewModel? _selectedCapture;

    public MainViewModel(
        AppInteractionCoordinator coordinator,
        ISettingsService settingsService,
        IStorageService storageService,
        ILoggingService loggingService)
    {
        _coordinator = coordinator;
        _settingsService = settingsService;
        _storageService = storageService;
        _loggingService = loggingService;

        CaptureFullScreenCommand = new AsyncRelayCommand(CaptureFullScreenAsync);
        CaptureRegionCommand = new AsyncRelayCommand(CaptureRegionAsync);
        CaptureActiveWindowCommand = new AsyncRelayCommand(CaptureActiveWindowAsync);
        AskAiCommand = new RelayCommand(AskAi, () => CurrentScreenshot is not null);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        OpenScreenshotsFolderCommand = new RelayCommand(OpenScreenshotsFolder);

        _coordinator.StatusChanged += (_, status) => Status = status;
        _coordinator.CurrentScreenshotChanged += (_, screenshot) =>
        {
            CurrentScreenshot = screenshot;

            if (screenshot is not null)
            {
                AddRecentCapture(screenshot);
            }
        };

        _ = LoadSettingsSummaryAsync();
    }

    public ObservableCollection<CaptureHistoryItemViewModel> RecentCaptures { get; } = [];

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string ActiveProviderName
    {
        get => _activeProviderName;
        private set => SetProperty(ref _activeProviderName, value);
    }

    public string ActiveProviderModel
    {
        get => _activeProviderModel;
        private set => SetProperty(ref _activeProviderModel, value);
    }

    public string StorageSummary
    {
        get => _storageSummary;
        private set => SetProperty(ref _storageSummary, value);
    }

    public string WorkflowSummary
    {
        get => _workflowSummary;
        private set => SetProperty(ref _workflowSummary, value);
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

    public CaptureHistoryItemViewModel? SelectedCapture
    {
        get => _selectedCapture;
        set
        {
            if (SetProperty(ref _selectedCapture, value))
            {
                OnPropertyChanged(nameof(SelectedCapturePreviewVisibility));
                OnPropertyChanged(nameof(EmptyPreviewVisibility));
            }
        }
    }

    public bool HasRecentCaptures => RecentCaptures.Count > 0;

    public Visibility HistoryListVisibility => HasRecentCaptures ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyHistoryVisibility => HasRecentCaptures ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SelectedCapturePreviewVisibility => SelectedCapture is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility EmptyPreviewVisibility => SelectedCapture is null ? Visibility.Visible : Visibility.Collapsed;

    public ICommand CaptureFullScreenCommand { get; }
    public ICommand CaptureRegionCommand { get; }
    public ICommand CaptureActiveWindowCommand { get; }
    public ICommand AskAiCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand OpenScreenshotsFolderCommand { get; }

    private async Task CaptureFullScreenAsync(CancellationToken cancellationToken)
    {
        await _coordinator.CaptureFullScreenAsync(cancellationToken);
    }

    private async Task CaptureRegionAsync(CancellationToken cancellationToken)
    {
        await _coordinator.CaptureRegionAsync(cancellationToken);
    }

    private async Task CaptureActiveWindowAsync(CancellationToken cancellationToken)
    {
        await _coordinator.CaptureActiveWindowAsync(cancellationToken);
    }

    private void AskAi()
    {
        if (CurrentScreenshot is null)
        {
            Status = "Спочатку зроби скріншот";
            return;
        }

        _coordinator.AskAiForCurrentScreenshot();
    }

    private void OpenSettings()
    {
        _coordinator.ShowSettings();
        _ = LoadSettingsSummaryAsync();
    }

    private void OpenScreenshotsFolder()
    {
        try
        {
            var directory = _storageService.ResolveWritableScreenshotsDirectory(StorageSummary);
            Process.Start(new ProcessStartInfo("explorer.exe", directory) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            _loggingService.Error(exception, "Could not open screenshots folder from main window.");
            Status = "Не вдалося відкрити папку скріншотів";
        }
    }

    private void AddRecentCapture(ScreenshotResult screenshot)
    {
        var item = new CaptureHistoryItemViewModel(screenshot);
        RecentCaptures.Insert(0, item);
        SelectedCapture = item;

        while (RecentCaptures.Count > MaxRecentCaptures)
        {
            RecentCaptures.RemoveAt(RecentCaptures.Count - 1);
        }

        OnPropertyChanged(nameof(HasRecentCaptures));
        OnPropertyChanged(nameof(HistoryListVisibility));
        OnPropertyChanged(nameof(EmptyHistoryVisibility));
    }

    private async Task LoadSettingsSummaryAsync()
    {
        try
        {
            var settings = await _settingsService.LoadAsync();
            var provider = settings.Ai.Providers.FirstOrDefault(item => item.Id == settings.Ai.ActiveProviderId)
                ?? settings.Ai.Providers.FirstOrDefault(item => item.IsEnabled);

            ActiveProviderName = provider?.DisplayName ?? "AI не налаштовано";
            ActiveProviderModel = provider?.Model ?? "Немає активного маршруту";
            StorageSummary = string.IsNullOrWhiteSpace(settings.Screenshots.SaveDirectory)
                ? "screenshots"
                : settings.Screenshots.SaveDirectory;

            var copyPart = settings.Screenshots.CopyToClipboardAutomatically ? "clipboard" : "manual copy";
            var menuPart = settings.Screenshots.ShowQuickActionsAfterCapture ? "quick menu" : "silent";
            WorkflowSummary = $"Ctrl+Shift+S · {copyPart} · {menuPart}";
        }
        catch (Exception exception)
        {
            _loggingService.Error(exception, "Could not load main window settings summary.");
        }
    }
}
