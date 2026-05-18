using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SmartScreen.Application.Abstractions;
using SmartScreen.App;
using SmartScreen.App.Services;
using SmartScreen.App.ViewModels;
using SmartScreen.App.Views;
using SmartScreen.Domain.Enums;
using SmartScreen.Domain.Models;
using DomainThemeMode = SmartScreen.Domain.Enums.ThemeMode;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfInkCanvas = System.Windows.Controls.InkCanvas;
using WpfSlider = System.Windows.Controls.Slider;

namespace SmartScreen.Tests;

[TestClass]
public sealed class UiSmokeTests
{
    [TestMethod]
    public void MainWindowLoadsWithThemeAndLocalizationResources()
    {
        RunOnSta(() =>
        {
            var application = CreateTestApplication(new Dictionary<string, string>
            {
                ["app.title"] = "SmartScreen Test",
                ["main.workspace"] = "Workspace Test",
                ["firstRun.title"] = "First Run Test",
                ["firstRun.start"] = "Begin Test"
            });

            try
            {
                var window = new MainWindow();
                try
                {
                    window.UpdateLayout();

                    Assert.AreEqual("SmartScreen Test", window.Title);
                    Assert.IsNotNull(application.Resources["TextBrush"]);
                    Assert.IsInstanceOfType(application.Resources["QuickWorkspaceScrimBrush"], typeof(System.Windows.Media.SolidColorBrush));
                    Assert.IsInstanceOfType(application.Resources["OverlayHintBrush"], typeof(System.Windows.Media.SolidColorBrush));
                    Assert.IsInstanceOfType(application.Resources["EditorToolButtonBrush"], typeof(System.Windows.Media.SolidColorBrush));
                    Assert.IsNotNull(application.Resources["Loc.main.workspace"]);
                    Assert.AreEqual("Скріншот готовий", application.Resources["Loc.quick.ready"]);
                    Assert.AreEqual("Готово", application.Resources["Loc.quick.status.ready"]);
                    Assert.AreEqual("Готово за {0:N1} с", application.Resources["Loc.ai.status.doneSeconds"]);
                    Assert.AreEqual("Олівець (P)", application.Resources["Loc.editor.pen"]);
                    Assert.AreEqual("AI-панель", application.Resources["Loc.ai.panel"]);
                    Assert.AreEqual("Запускати згорнуто в трей", application.Resources["Loc.settings.general.startMinimized"]);
                    Assert.AreEqual("Комбінація", application.Resources["Loc.settings.hotkeys.gesture"]);
                    Assert.AreEqual("AI-провайдера додано. Заповни endpoint, model і ключ", application.Resources["Loc.settings.status.providerAdded"]);
                    Assert.IsNotNull(window.Content);
                }
                finally
                {
                    window.Close();
                }

                var wizard = new FirstRunWizardWindow(new FirstRunWizardViewModel(new FakeSettingsService()));
                try
                {
                    wizard.UpdateLayout();

                    Assert.AreEqual("First Run Test", wizard.Title);
                    Assert.IsNotNull(wizard.Content);
                }
                finally
                {
                    wizard.Close();
                }

                AssertQuickActionsWorkspaceLoads(application);
                AssertSettingsWindowThemePreview(application);
            }
            finally
            {
                application.Shutdown();
            }
        });
    }

    private static void AssertSettingsWindowThemePreview(System.Windows.Application application)
    {
        var storageService = new FakeStorageService();

        try
        {
            var settings = new AppSettings
            {
                Theme =
                {
                    Mode = DomainThemeMode.Dark,
                    AccentColor = "#38BDF8"
                }
            };

            var viewModel = new SettingsViewModel(
                new FakeSettingsService(settings),
                new FakeHotkeySettingsService(),
                new FakeHotkeyService(),
                storageService,
                new FakeAiService(),
                new FakeAiSecretService(),
                new FakePromptTemplateService(),
                new FakeLocalizationService(),
                new FakeLoggingService());

            var window = new SettingsWindow(viewModel);
            try
            {
                window.Show();
                PumpDispatcher();

                var themeComboBox = FindLogicalDescendants<WpfComboBox>(window)
                    .First(comboBox => comboBox.Name == "ThemeModeComboBox");

                themeComboBox.SelectedValue = DomainThemeMode.Light;
                PumpDispatcher();

                Assert.AreEqual(DomainThemeMode.Light, settings.Theme.Mode);
                Assert.AreEqual(System.Windows.Media.Color.FromRgb(17, 24, 39), GetSolidColor(application.Resources["TextBrush"]));
                Assert.AreEqual(System.Windows.Media.Color.FromRgb(245, 247, 251), GetSolidColor(application.Resources["AppBackgroundBrush"]));
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            storageService.Cleanup();
        }
    }

    private static void AssertQuickActionsWorkspaceLoads(System.Windows.Application application)
    {
        var storageService = new FakeStorageService();

        try
        {
            var viewModel = new QuickActionsViewModel(
                CreateScreenshot(),
                new FakeClipboardService(),
                new FakeImageFileService(),
                new FakeSettingsService(CreateWorkspaceSettings()),
                storageService,
                new FakePromptTemplateService(),
                new FakeAiService(),
                new FakeLoggingService(),
                startupMode: CaptureWorkspaceStartupMode.Ai,
                initialPromptTemplateId: "privacy");

            viewModel.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            var window = new QuickActionsWindow(viewModel)
            {
                WindowState = WindowState.Normal,
                Width = 960,
                Height = 640,
                Topmost = false
            };

            try
            {
                window.UpdateLayout();

                var actionButtons = FindLogicalDescendants<WpfButton>(window).ToList();
                var comboBoxes = FindLogicalDescendants<WpfComboBox>(window).ToList();
                var sliders = FindLogicalDescendants<WpfSlider>(window).ToList();
                var inkCanvases = FindLogicalDescendants<WpfInkCanvas>(window).ToList();

                Assert.AreSame(viewModel, window.DataContext);
                Assert.IsNotNull(window.Content);
                Assert.AreEqual("120 x 80px", viewModel.ScreenshotInfo);
                Assert.AreEqual(Visibility.Visible, viewModel.AiPanelVisibility);
                Assert.AreEqual("#22C55E", viewModel.EditorDefaultColor);
                Assert.AreEqual(5, viewModel.EditorDefaultStrokeThickness);
                Assert.AreEqual(22, viewModel.EditorDefaultTextSize);
                Assert.AreEqual(2, viewModel.Prompts.Count);
                Assert.IsTrue(actionButtons.Count >= 18);
                Assert.IsTrue(actionButtons.Any(button => Equals(button.ToolTip, "Копіювати в буфер")));
                Assert.IsTrue(actionButtons.Any(button => Equals(button.ToolTip, "Застосувати (Ctrl+S або Enter)")));
                Assert.IsTrue(actionButtons.Any(button => Equals(button.ToolTip, "Надіслати AI-запит")));
                Assert.IsTrue(comboBoxes.Count >= 1);
                Assert.IsTrue(sliders.Count >= 2);
                Assert.IsTrue(inkCanvases.Count >= 1);
                AssertSameBrushColor(application.Resources["TextBrush"], comboBoxes[0].Foreground);
                Assert.IsInstanceOfType(application.Resources["QuickWorkspaceScrimBrush"], typeof(SolidColorBrush));
                Assert.IsInstanceOfType(application.Resources["EditorToolButtonBrush"], typeof(SolidColorBrush));
                Assert.IsInstanceOfType(application.Resources["PopupBrush"], typeof(SolidColorBrush));
                Assert.AreEqual("AI-панель", application.Resources["Loc.ai.panel"]);
                Assert.AreEqual("Приватність", viewModel.SelectedPrompt?.Title);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            storageService.Cleanup();
        }
    }

    private static void RunOnSta(Action action)
    {
        ExceptionDispatchInfo? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caught)
            {
                exception = ExceptionDispatchInfo.Capture(caught);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        exception?.Throw();
    }

    private static System.Windows.Application CreateTestApplication(
        IReadOnlyDictionary<string, string>? localizedStrings = null)
    {
        var application = new System.Windows.Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/SmartScreen.App;component/Resources/Styles/AppStyles.xaml",
                UriKind.Absolute)
        });

        LocalizationResourceService.Apply(localizedStrings ?? new Dictionary<string, string>());

        ThemeResourceService.Apply(new ThemeSettings
        {
            Mode = DomainThemeMode.Dark,
            AccentColor = "#38BDF8"
        });

        return application;
    }

    private static ScreenshotResult CreateScreenshot()
    {
        const int width = 120;
        const int height = 80;
        const int bytesPerPixel = 4;
        var stride = width * bytesPerPixel;
        var pixels = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = y * stride + x * bytesPerPixel;
                pixels[offset] = (byte)(40 + x);
                pixels[offset + 1] = (byte)(80 + y);
                pixels[offset + 2] = 180;
                pixels[offset + 3] = 255;
            }
        }

        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);

        return new ScreenshotResult
        {
            ImageBytes = stream.ToArray(),
            MimeType = "image/png",
            Width = width,
            Height = height,
            CreatedAt = DateTimeOffset.UtcNow,
            SuggestedFileName = "smoke.png",
            SourceName = "Smoke test"
        };
    }

    private static AppSettings CreateWorkspaceSettings() =>
        new()
        {
            Editor =
            {
                DefaultColor = "#22C55E",
                DefaultStrokeThickness = 5,
                DefaultTextSize = 22,
                HighlighterOpacity = 0.4
            },
            Theme =
            {
                Mode = DomainThemeMode.Dark,
                AccentColor = "#38BDF8"
            }
        };

    private static IEnumerable<T> FindLogicalDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindLogicalDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void PumpDispatcher() =>
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            new Action(() => { }));

    private static System.Windows.Media.Color GetSolidColor(object resource)
    {
        Assert.IsInstanceOfType(resource, typeof(SolidColorBrush));
        return ((SolidColorBrush)resource).Color;
    }

    private static void AssertSameBrushColor(object expected, WpfBrush actual)
    {
        Assert.IsInstanceOfType(expected, typeof(SolidColorBrush));
        Assert.IsInstanceOfType(actual, typeof(SolidColorBrush));
        Assert.AreEqual(((SolidColorBrush)expected).Color, ((SolidColorBrush)actual).Color);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        private readonly AppSettings _settings;

        public FakeSettingsService()
            : this(new AppSettings
            {
                Screenshots =
                {
                    AfterCaptureActions =
                    [
                        AfterCaptureAction.CopyImageToClipboard,
                        AfterCaptureAction.ShowQuickActions
                    ]
                },
                Ai =
                {
                    Providers =
                    [
                        new AiProviderSettings
                        {
                            Id = "test",
                            DisplayName = "Test Provider",
                            IsEnabled = true
                        }
                    ]
                }
            })
        {
        }

        public FakeSettingsService(AppSettings settings)
        {
            _settings = settings;
        }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_settings);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<AppSettings> ResetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_settings);
    }

    private sealed class FakeHotkeySettingsService : IHotkeySettingsService
    {
        public Task<HotkeySettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new HotkeySettings());

        public Task SaveAsync(HotkeySettings settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeHotkeyService : IHotkeyService
    {
        public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed
        {
            add { }
            remove { }
        }

        public Task RegisterAsync(HotkeySettings settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UnregisterAllAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public Task CopyImageAsync(ScreenshotResult screenshot, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task CopyTextAsync(string text, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeImageFileService : IImageFileService
    {
        public Task<string> SaveAsync(
            ScreenshotResult screenshot,
            string? directory,
            ScreenshotImageFormat format,
            int jpegQuality,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Path.Combine(directory ?? Path.GetTempPath(), screenshot.SuggestedFileName));
    }

    private sealed class FakeStorageService : IStorageService
    {
        public FakeStorageService()
        {
            var baseDirectory = Path.Combine(Path.GetTempPath(), "SmartScreen.UiSmoke", Guid.NewGuid().ToString("N"));
            Paths = new AppPaths
            {
                BaseDirectory = baseDirectory,
                ConfigDirectory = Path.Combine(baseDirectory, "config"),
                ScreenshotsDirectory = Path.Combine(baseDirectory, "screenshots"),
                LogsDirectory = Path.Combine(baseDirectory, "logs"),
                LocalizationDirectory = Path.Combine(baseDirectory, "localization"),
                ThemesDirectory = Path.Combine(baseDirectory, "themes"),
                FallbackDirectory = Path.Combine(baseDirectory, "fallback")
            };
        }

        public AppPaths Paths { get; }

        public Task EnsureDirectoriesAsync(CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(Paths.BaseDirectory);
            Directory.CreateDirectory(Paths.ConfigDirectory);
            Directory.CreateDirectory(Paths.ScreenshotsDirectory);
            Directory.CreateDirectory(Paths.LogsDirectory);
            Directory.CreateDirectory(Paths.LocalizationDirectory);
            Directory.CreateDirectory(Paths.ThemesDirectory);
            return Task.CompletedTask;
        }

        public string ResolveWritableScreenshotsDirectory(string? configuredDirectory) =>
            Paths.ScreenshotsDirectory;

        public string GetConfigFilePath(string fileName) =>
            Path.Combine(Paths.ConfigDirectory, fileName);

        public void Cleanup()
        {
            if (Directory.Exists(Paths.BaseDirectory))
            {
                Directory.Delete(Paths.BaseDirectory, recursive: true);
            }
        }
    }

    private sealed class FakePromptTemplateService : IPromptTemplateService
    {
        private readonly AiPromptLibrary _library = new()
        {
            Categories =
            [
                new AiPromptCategory
                {
                    Id = "general",
                    Name = "Загальні",
                    IsSystem = true,
                    Order = 0
                }
            ],
            Templates =
            [
                new AiPromptTemplate
                {
                    Id = "privacy",
                    CategoryId = "general",
                    Title = "Приватність",
                    Prompt = "Знайди приватні дані на скріншоті.",
                    IsSystem = true,
                    Order = 0
                },
                new AiPromptTemplate
                {
                    Id = "describe",
                    CategoryId = "general",
                    Title = "Опис",
                    Prompt = "Опиши скріншот.",
                    IsSystem = true,
                    Order = 1
                }
            ]
        };

        public Task<AiPromptLibrary> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_library);

        public Task SaveAsync(AiPromptLibrary library, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ResetToDefaultsAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeAiSecretService : IAiSecretService
    {
        public Task ApplySecretsAsync(AiProviderSettings settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveApiKeyAsync(string providerId, string apiKey, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public string GetEnvironmentVariableName(string providerId) =>
            $"SMARTSCREEN_AI_{providerId.ToUpperInvariant()}_KEY";
    }

    private sealed class FakeLocalizationService : ILocalizationService
    {
        public IReadOnlyDictionary<string, string> CurrentStrings { get; } = new Dictionary<string, string>();

        public Task LoadAsync(string cultureName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public string GetString(string key) => key;
    }

    private sealed class FakeAiService : IAiService
    {
        public Task<AiResponse> AnalyzeCurrentScreenshotAsync(
            ScreenshotResult screenshot,
            string prompt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(AiResponse.Ok("AI smoke response", TimeSpan.FromMilliseconds(25)));

        public Task<bool> TestActiveProviderAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class FakeLoggingService : ILoggingService
    {
        public void Info(string message)
        {
        }

        public void Warning(string message)
        {
        }

        public void Error(Exception exception, string message)
        {
        }

        public void Error(string message)
        {
        }
    }
}
