using System.Windows;
using SmartScreen.Application.Abstractions;
using SmartScreen.App.ViewModels;
using SmartScreen.App.Views;
using SmartScreen.Domain.Models;
using Forms = System.Windows.Forms;

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

        PositionNearCursor(window);
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

    private static void PositionNearCursor(System.Windows.Window window)
    {
        var cursor = Forms.Cursor.Position;
        var screen = Forms.Screen.FromPoint(cursor);
        var workArea = screen.WorkingArea;

        window.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
        window.Left = Math.Min(cursor.X + 18, workArea.Right - window.Width - 18);
        window.Top = Math.Min(cursor.Y + 18, workArea.Bottom - 180);

        if (window.Left < workArea.Left)
        {
            window.Left = workArea.Left + 18;
        }

        if (window.Top < workArea.Top)
        {
            window.Top = workArea.Top + 18;
        }
    }
}
