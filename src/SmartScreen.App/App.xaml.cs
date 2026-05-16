using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using SmartScreen.Application.Abstractions;
using SmartScreen.App.Services;
using SmartScreen.App.ViewModels;
using SmartScreen.Infrastructure.Ai;
using SmartScreen.Infrastructure.Configuration;
using SmartScreen.Infrastructure.Imaging;
using SmartScreen.Infrastructure.Logging;
using SmartScreen.Infrastructure.Storage;

namespace SmartScreen.App;

public partial class App : System.Windows.Application
{
    private HttpClient? _httpClient;
    private ILoggingService? _loggingService;
    private ITrayService? _trayService;
    private IHotkeyService? _hotkeyService;
    private bool _exitRequested;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var storageService = new StorageService();
        var loggingService = new FileLoggingService(storageService);
        _loggingService = loggingService;

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        await storageService.EnsureDirectoriesAsync();

        var settingsService = new JsonSettingsService(storageService, loggingService);
        var hotkeySettingsService = new JsonHotkeySettingsService(storageService, loggingService);
        var promptTemplateService = new PromptTemplateService(storageService, loggingService);
        var settings = await settingsService.LoadAsync();
        var hotkeySettings = await hotkeySettingsService.LoadAsync();
        await promptTemplateService.LoadAsync();

        var screenshotService = new ScreenshotService();
        var imageFileService = new ImageFileService(storageService);
        var clipboardService = new WpfClipboardService();

        _httpClient = new HttpClient();
        var providerFactory = new AiProviderFactory(_httpClient);
        var aiService = new AiService(settingsService, providerFactory, loggingService);

        var windowService = new WpfWindowService(
            clipboardService,
            imageFileService,
            settingsService,
            storageService,
            aiService,
            promptTemplateService,
            loggingService);

        var coordinator = new AppInteractionCoordinator(
            screenshotService,
            clipboardService,
            settingsService,
            windowService,
            loggingService);

        var mainWindow = new MainWindow
        {
            DataContext = new MainViewModel(coordinator)
        };

        MainWindow = mainWindow;
        mainWindow.Closing += (_, args) =>
        {
            if (_exitRequested || !settings.MinimizeToTrayOnClose)
            {
                return;
            }

            args.Cancel = true;
            mainWindow.Hide();
            _trayService?.ShowReadyNotification();
        };

        var trayService = new WpfTrayService();
        _trayService = trayService;
        trayService.Initialize();
        trayService.CaptureRegionRequested += async (_, _) => await coordinator.CaptureRegionAsync();
        trayService.CaptureFullScreenRequested += async (_, _) => await coordinator.CaptureFullScreenAsync();
        trayService.CaptureActiveWindowRequested += async (_, _) => await coordinator.CaptureActiveWindowAsync();
        trayService.OpenSettingsRequested += (_, _) => coordinator.ShowSettings();
        trayService.OpenMainWindowRequested += (_, _) => ShowMainWindow(mainWindow);
        trayService.ExitRequested += (_, _) =>
        {
            _exitRequested = true;
            Shutdown();
        };

        var hotkeyService = new WpfHotkeyService(loggingService);
        _hotkeyService = hotkeyService;
        hotkeyService.HotkeyPressed += (_, args) => coordinator.HandleHotkey(args.Action);
        await hotkeyService.RegisterAsync(hotkeySettings);

        if (settings.StartMinimizedToTray && !e.Args.Contains("--show", StringComparer.OrdinalIgnoreCase))
        {
            trayService.ShowReadyNotification();
            return;
        }

        ShowMainWindow(mainWindow);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayService?.Dispose();
        _hotkeyService?.Dispose();
        _httpClient?.Dispose();
        base.OnExit(e);
    }

    private static void ShowMainWindow(Window mainWindow)
    {
        if (!mainWindow.IsVisible)
        {
            mainWindow.Show();
        }

        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        mainWindow.Activate();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _loggingService?.Error(e.Exception, "Unhandled UI exception.");
        System.Windows.MessageBox.Show(
            "Сталася неочікувана помилка. Деталі записано в logs/app.log.",
            "SmartScreen",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _loggingService?.Error(exception, "Unhandled application exception.");
        }
    }
}
