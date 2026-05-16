namespace SmartScreen.Application.Abstractions;

public interface ITrayService : IDisposable
{
    event EventHandler? CaptureRegionRequested;
    event EventHandler? CaptureFullScreenRequested;
    event EventHandler? CaptureActiveWindowRequested;
    event EventHandler? OpenMainWindowRequested;
    event EventHandler? OpenSettingsRequested;
    event EventHandler? ExitRequested;

    void Initialize();
    void ShowReadyNotification();
}

