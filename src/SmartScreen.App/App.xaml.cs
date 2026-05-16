using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using SmartScreen.Application.Abstractions;
using SmartScreen.App.Services;
using SmartScreen.App.ViewModels;
using SmartScreen.App.Views;
using SmartScreen.Infrastructure.Ai;
using SmartScreen.Infrastructure.Configuration;
using SmartScreen.Infrastructure.Imaging;
using SmartScreen.Infrastructure.Logging;
using SmartScreen.Infrastructure.Storage;

namespace SmartScreen.App;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\SmartScreen.Elementalka.CourseWork";

    private HttpClient? _httpClient;
    private ILoggingService? _loggingService;
    private ITrayService? _trayService;
    private IHotkeyService? _hotkeyService;
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
    private bool _exitRequested;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out _ownsSingleInstanceMutex);
        if (!_ownsSingleInstanceMutex)
        {
            System.Windows.MessageBox.Show(
                "SmartScreen вже запущено. Перевір іконку в системному треї або закрий попередній екземпляр.",
                "SmartScreen",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var storageService = new StorageService();
        var loggingService = new FileLoggingService(storageService);
        _loggingService = loggingService;

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        await storageService.EnsureDirectoriesAsync();

        var settingsService = new JsonSettingsService(storageService, loggingService);
        var hotkeySettingsService = new JsonHotkeySettingsService(storageService, loggingService);
        var aiSecretService = new LocalAiSecretService(storageService, loggingService);
        var promptTemplateService = new PromptTemplateService(storageService, loggingService);
        var settings = await settingsService.LoadAsync();
        var hotkeySettings = await hotkeySettingsService.LoadAsync();
        await promptTemplateService.LoadAsync();

        var screenshotService = new ScreenshotService();
        var imageFileService = new ImageFileService(storageService);
        var clipboardService = new WpfClipboardService();

        _httpClient = new HttpClient();
        var providerFactory = new AiProviderFactory(_httpClient);
        var aiService = new AiService(settingsService, aiSecretService, providerFactory, loggingService);
        var hotkeyService = new WpfHotkeyService(loggingService);
        _hotkeyService = hotkeyService;

        var windowService = new WpfWindowService(
            clipboardService,
            imageFileService,
            settingsService,
            hotkeySettingsService,
            hotkeyService,
            storageService,
            aiService,
            aiSecretService,
            promptTemplateService,
            loggingService);

        var coordinator = new AppInteractionCoordinator(
            screenshotService,
            clipboardService,
            imageFileService,
            settingsService,
            windowService,
            loggingService);

        var mainWindow = new MainWindow
        {
            DataContext = new MainViewModel(coordinator, settingsService, storageService, loggingService)
        };

        MainWindow = mainWindow;
        var firstRunShown = false;
        if (!settings.FirstRunCompleted)
        {
            firstRunShown = true;
            var firstRunWizard = new FirstRunWizardWindow(new FirstRunWizardViewModel(settingsService));
            firstRunWizard.ShowDialog();
            settings = await settingsService.LoadAsync();
        }

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

        hotkeyService.HotkeyPressed += (_, args) => coordinator.HandleHotkey(args.Action, args.PromptTemplateId);
        await hotkeyService.RegisterAsync(hotkeySettings);

        if (settings.StartMinimizedToTray &&
            !firstRunShown &&
            !e.Args.Contains("--show", StringComparer.OrdinalIgnoreCase))
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
        ReleaseSingleInstanceMutex();
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

    private void ReleaseSingleInstanceMutex()
    {
        if (_singleInstanceMutex is null)
        {
            return;
        }

        try
        {
            if (_ownsSingleInstanceMutex)
            {
                _singleInstanceMutex.ReleaseMutex();
            }
        }
        catch (ApplicationException)
        {
            // The process is already exiting; mutex release must not hide the real shutdown path.
        }
        finally
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            _ownsSingleInstanceMutex = false;
        }
    }
}
