using System.Windows.Input;
using SmartScreen.App.Commands;
using SmartScreen.App.Services;
using SmartScreen.Domain.Models;

namespace SmartScreen.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly AppInteractionCoordinator _coordinator;
    private string _status = "Готово до роботи";
    private ScreenshotResult? _currentScreenshot;

    public MainViewModel(
        AppInteractionCoordinator coordinator)
    {
        _coordinator = coordinator;

        CaptureFullScreenCommand = new AsyncRelayCommand(CaptureFullScreenAsync);
        CaptureRegionCommand = new AsyncRelayCommand(CaptureRegionAsync);
        CaptureActiveWindowCommand = new AsyncRelayCommand(CaptureActiveWindowAsync);
        AskAiCommand = new RelayCommand(AskAi, () => CurrentScreenshot is not null);
        OpenSettingsCommand = new RelayCommand(_coordinator.ShowSettings);

        _coordinator.StatusChanged += (_, status) => Status = status;
        _coordinator.CurrentScreenshotChanged += (_, screenshot) => CurrentScreenshot = screenshot;
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
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

    public ICommand CaptureFullScreenCommand { get; }
    public ICommand CaptureRegionCommand { get; }
    public ICommand CaptureActiveWindowCommand { get; }
    public ICommand AskAiCommand { get; }
    public ICommand OpenSettingsCommand { get; }

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
}
