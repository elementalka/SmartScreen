using System.Windows;
using SmartScreen.Application.Abstractions;
using SmartScreen.App.ViewModels;
using SmartScreen.App.Views;
using SmartScreen.Domain.Models;

namespace SmartScreen.App.Services;

public sealed class WpfWindowService(
    IClipboardService clipboardService,
    IImageFileService imageFileService,
    ISettingsService settingsService,
    IStorageService storageService,
    IAiService aiService,
    IPromptTemplateService promptTemplateService,
    ILoggingService loggingService) : IWindowService
{
    public Task<ScreenRegion?> SelectRegionAsync()
    {
        var overlay = new ScreenshotOverlayWindow
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        var result = overlay.ShowDialog();
        return Task.FromResult(result == true ? overlay.SelectedRegion : null);
    }

    public Task ShowQuickActionsAsync(ScreenshotResult screenshot)
    {
        var viewModel = new QuickActionsViewModel(
            screenshot,
            clipboardService,
            imageFileService,
            settingsService,
            storageService,
            this,
            loggingService);

        var window = new QuickActionsWindow(viewModel)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        window.Show();
        return Task.CompletedTask;
    }

    public Task<ScreenshotResult?> ShowEditorAsync(ScreenshotResult screenshot)
    {
        var editor = new ScreenshotEditorWindow(screenshot)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        var result = editor.ShowDialog();
        return Task.FromResult(result == true ? editor.EditedScreenshot : null);
    }

    public void ShowAiResponse(ScreenshotResult screenshot)
    {
        var viewModel = new AiResponseViewModel(screenshot, aiService, clipboardService, promptTemplateService);
        var window = new AiResponseWindow(viewModel)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        window.Show();
    }

    public void ShowSettings()
    {
        var viewModel = new SettingsViewModel(settingsService, storageService, aiService);
        var window = new SettingsWindow(viewModel)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        window.ShowDialog();
    }
}
