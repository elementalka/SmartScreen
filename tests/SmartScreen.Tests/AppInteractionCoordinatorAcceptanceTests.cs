using SmartScreen.Application.Abstractions;
using SmartScreen.App.Services;
using SmartScreen.Domain.Enums;
using SmartScreen.Domain.Models;

namespace SmartScreen.Tests;

[TestClass]
public sealed class AppInteractionCoordinatorAcceptanceTests
{
    [TestMethod]
    public async Task CaptureFullScreenRunsCopySaveAndQuickActionsPipeline()
    {
        var settings = CreateSettings(
            AfterCaptureAction.CopyImageToClipboard,
            AfterCaptureAction.SaveImageToFile,
            AfterCaptureAction.ShowQuickActions);
        settings.Screenshots.DefaultFormat = ScreenshotImageFormat.Jpeg;
        settings.Screenshots.JpegQuality = 74;
        settings.Screenshots.SaveDirectory = "custom-output";
        var harness = new CoordinatorHarness(settings);

        await harness.Coordinator.CaptureFullScreenAsync();

        Assert.AreEqual(1, harness.ScreenshotService.FullScreenCalls);
        Assert.AreEqual(1, harness.ClipboardService.CopyImageCalls);
        Assert.AreEqual(1, harness.ImageFileService.SaveCalls);
        Assert.AreSame(harness.ScreenshotService.FullScreenScreenshot, harness.ClipboardService.LastCopiedImage);
        Assert.AreEqual("custom-output", harness.ImageFileService.LastDirectory);
        Assert.AreEqual(ScreenshotImageFormat.Jpeg, harness.ImageFileService.LastFormat);
        Assert.AreEqual(74, harness.ImageFileService.LastJpegQuality);
        Assert.AreEqual(1, harness.WindowService.ShowQuickActionsCalls);
        Assert.AreEqual(CaptureWorkspaceStartupMode.Actions, harness.WindowService.LastStartupMode);
        Assert.IsFalse(harness.WindowService.LastStartAiImmediately);
        Assert.AreSame(harness.ScreenshotService.FullScreenScreenshot, harness.Coordinator.CurrentScreenshot);
        StringAssert.Contains(harness.Statuses.Last(), "Скріншот готовий");
        StringAssert.Contains(harness.Statuses.Last(), "буфер");
        StringAssert.Contains(harness.Statuses.Last(), "file.png");
    }

    [TestMethod]
    public async Task EditorWorkflowOpensWorkspaceInEditorBeforeAi()
    {
        var harness = new CoordinatorHarness(CreateSettings(
            AfterCaptureAction.OpenEditor,
            AfterCaptureAction.AskAi));

        await harness.Coordinator.CaptureFullScreenAsync();

        Assert.AreEqual(1, harness.WindowService.ShowQuickActionsCalls);
        Assert.AreEqual(CaptureWorkspaceStartupMode.Editor, harness.WindowService.LastStartupMode);
        Assert.IsFalse(harness.WindowService.LastStartAiImmediately);
        Assert.IsNull(harness.WindowService.LastPromptTemplateId);
    }

    [TestMethod]
    public async Task AskAiWorkflowStartsAiPanelOnlyWhenConfiguredOrRequested()
    {
        var harness = new CoordinatorHarness(CreateSettings(AfterCaptureAction.AskAi));

        await harness.Coordinator.CaptureFullScreenAsync();

        Assert.AreEqual(CaptureWorkspaceStartupMode.Ai, harness.WindowService.LastStartupMode);
        Assert.IsTrue(harness.WindowService.LastStartAiImmediately);

        harness.WindowService.Reset();
        harness.Coordinator.AskAiForCurrentScreenshot("ocr");

        Assert.AreEqual(1, harness.WindowService.ShowQuickActionsCalls);
        Assert.AreEqual(CaptureWorkspaceStartupMode.Ai, harness.WindowService.LastStartupMode);
        Assert.AreEqual("ocr", harness.WindowService.LastPromptTemplateId);
        Assert.IsTrue(harness.WindowService.LastStartAiImmediately);
    }

    [TestMethod]
    public async Task CaptureDefaultUsesConfiguredMonitor()
    {
        var settings = CreateSettings();
        settings.Screenshots.DefaultMode = ScreenshotMode.Monitor;
        settings.Screenshots.MonitorIndex = 2;
        var harness = new CoordinatorHarness(settings);

        await harness.Coordinator.CaptureDefaultAsync();

        Assert.AreEqual(1, harness.ScreenshotService.MonitorCalls);
        Assert.AreEqual(2, harness.ScreenshotService.LastMonitorIndex);
        Assert.AreSame(harness.ScreenshotService.MonitorScreenshot, harness.Coordinator.CurrentScreenshot);
    }

    [TestMethod]
    public void AskAiWithoutScreenshotShowsFriendlyStatusAndDoesNotOpenWorkspace()
    {
        var harness = new CoordinatorHarness(CreateSettings());

        harness.Coordinator.AskAiForCurrentScreenshot();

        Assert.AreEqual(0, harness.WindowService.ShowQuickActionsCalls);
        Assert.AreEqual("Спочатку зроби скріншот", harness.Statuses.Last());
    }

    private static AppSettings CreateSettings(params AfterCaptureAction[] actions)
    {
        var settings = new AppSettings();
        settings.Screenshots.AfterCaptureActions = actions.ToList();
        return settings;
    }

    private sealed class CoordinatorHarness
    {
        public CoordinatorHarness(AppSettings settings)
        {
            SettingsService = new FakeSettingsService(settings);
            ScreenshotService = new FakeScreenshotService();
            ClipboardService = new FakeClipboardService();
            ImageFileService = new FakeImageFileService();
            WindowService = new FakeWindowService();
            LoggingService = new FakeLoggingService();
            Coordinator = new AppInteractionCoordinator(
                ScreenshotService,
                ClipboardService,
                ImageFileService,
                SettingsService,
                WindowService,
                LoggingService);
            Coordinator.StatusChanged += (_, status) => Statuses.Add(status);
        }

        public AppInteractionCoordinator Coordinator { get; }
        public FakeSettingsService SettingsService { get; }
        public FakeScreenshotService ScreenshotService { get; }
        public FakeClipboardService ClipboardService { get; }
        public FakeImageFileService ImageFileService { get; }
        public FakeWindowService WindowService { get; }
        public FakeLoggingService LoggingService { get; }
        public List<string> Statuses { get; } = [];
    }

    private sealed class FakeSettingsService(AppSettings settings) : ISettingsService
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(settings);

        public Task SaveAsync(AppSettings updatedSettings, CancellationToken cancellationToken = default)
        {
            settings = updatedSettings;
            return Task.CompletedTask;
        }

        public Task<AppSettings> ResetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(settings);
    }

    private sealed class FakeScreenshotService : IScreenshotService
    {
        public ScreenshotResult FullScreenScreenshot { get; } = CreateScreenshot("Full screen", 1200, 800);
        public ScreenshotResult ActiveWindowScreenshot { get; } = CreateScreenshot("Active window", 640, 480);
        public ScreenshotResult MonitorScreenshot { get; } = CreateScreenshot("Monitor 3", 1024, 768);
        public ScreenshotResult RegionScreenshot { get; } = CreateScreenshot("Selected region", 320, 240);
        public int FullScreenCalls { get; private set; }
        public int ActiveWindowCalls { get; private set; }
        public int MonitorCalls { get; private set; }
        public int RegionCalls { get; private set; }
        public int? LastMonitorIndex { get; private set; }

        public Task<ScreenshotResult> CaptureFullScreenAsync(CancellationToken cancellationToken = default)
        {
            FullScreenCalls++;
            return Task.FromResult(FullScreenScreenshot);
        }

        public Task<ScreenshotResult> CaptureActiveWindowAsync(CancellationToken cancellationToken = default)
        {
            ActiveWindowCalls++;
            return Task.FromResult(ActiveWindowScreenshot);
        }

        public Task<ScreenshotResult> CaptureMonitorAsync(int monitorIndex, CancellationToken cancellationToken = default)
        {
            MonitorCalls++;
            LastMonitorIndex = monitorIndex;
            return Task.FromResult(MonitorScreenshot);
        }

        public Task<ScreenshotResult> CaptureRegionAsync(ScreenRegion region, CancellationToken cancellationToken = default)
        {
            RegionCalls++;
            return Task.FromResult(RegionScreenshot);
        }

        private static ScreenshotResult CreateScreenshot(string sourceName, int width, int height) => new()
        {
            ImageBytes = [1, 2, 3, 4],
            MimeType = "image/png",
            Width = width,
            Height = height,
            CreatedAt = DateTimeOffset.Now,
            SuggestedFileName = "screenshot.png",
            SourceName = sourceName
        };
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public int CopyImageCalls { get; private set; }
        public ScreenshotResult? LastCopiedImage { get; private set; }

        public Task CopyImageAsync(ScreenshotResult screenshot, CancellationToken cancellationToken = default)
        {
            CopyImageCalls++;
            LastCopiedImage = screenshot;
            return Task.CompletedTask;
        }

        public Task CopyTextAsync(string text, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeImageFileService : IImageFileService
    {
        public int SaveCalls { get; private set; }
        public ScreenshotResult? LastSavedImage { get; private set; }
        public string? LastDirectory { get; private set; }
        public ScreenshotImageFormat LastFormat { get; private set; }
        public int LastJpegQuality { get; private set; }

        public Task<string> SaveAsync(
            ScreenshotResult screenshot,
            string? directory,
            ScreenshotImageFormat format,
            int jpegQuality,
            CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            LastSavedImage = screenshot;
            LastDirectory = directory;
            LastFormat = format;
            LastJpegQuality = jpegQuality;
            return Task.FromResult(Path.Combine(directory ?? "screenshots", "file.png"));
        }
    }

    private sealed class FakeWindowService : IWindowService
    {
        public int ShowQuickActionsCalls { get; private set; }
        public CaptureWorkspaceStartupMode? LastStartupMode { get; private set; }
        public string? LastPromptTemplateId { get; private set; }
        public bool LastStartAiImmediately { get; private set; }

        public Task<ScreenRegion?> SelectRegionAsync() =>
            Task.FromResult<ScreenRegion?>(new ScreenRegion(10, 20, 300, 200));

        public Task ShowQuickActionsAsync(
            ScreenshotResult screenshot,
            CaptureWorkspaceStartupMode startupMode = CaptureWorkspaceStartupMode.Actions,
            string? promptTemplateId = null,
            string? customPrompt = null,
            bool startAiImmediately = false)
        {
            ShowQuickActionsCalls++;
            LastStartupMode = startupMode;
            LastPromptTemplateId = promptTemplateId;
            LastStartAiImmediately = startAiImmediately;
            return Task.CompletedTask;
        }

        public void ShowSettings()
        {
        }

        public void Reset()
        {
            ShowQuickActionsCalls = 0;
            LastStartupMode = null;
            LastPromptTemplateId = null;
            LastStartAiImmediately = false;
        }
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
