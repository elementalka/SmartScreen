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
        var promptTemplateService = new PromptTemplateService(storageService, loggingService);
        await settingsService.LoadAsync();
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

        var mainWindow = new MainWindow
        {
            DataContext = new MainViewModel(
                screenshotService,
                clipboardService,
                settingsService,
                windowService,
                loggingService)
        };

        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _httpClient?.Dispose();
        base.OnExit(e);
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
