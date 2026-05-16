using System.Windows;
using System.Windows.Media;
using SmartScreen.Application.Abstractions;
using SmartScreen.App.ViewModels;
using SmartScreen.App.Views;
using SmartScreen.Domain.Models;
using Forms = System.Windows.Forms;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace SmartScreen.App.Services;

public sealed class WpfWindowService(
    IClipboardService clipboardService,
    IImageFileService imageFileService,
    ISettingsService settingsService,
    IStorageService storageService,
    IAiService aiService,
    IAiSecretService aiSecretService,
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

        window.Opacity = 0;
        window.Show();
        window.UpdateLayout();
        PositionNearCursor(window);
        window.Opacity = 1;
        window.Activate();
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
        var viewModel = new SettingsViewModel(settingsService, storageService, aiService, aiSecretService);
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
        var transform = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;

        var cursorDip = transform.Transform(new Point(cursor.X, cursor.Y));
        var workArea = screen.WorkingArea;
        var workAreaDipTopLeft = transform.Transform(new Point(workArea.Left, workArea.Top));
        var workAreaDipBottomRight = transform.Transform(new Point(workArea.Right, workArea.Bottom));
        var workAreaDip = new Rect(workAreaDipTopLeft, workAreaDipBottomRight);

        var width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        var height = window.ActualHeight > 0 ? window.ActualHeight : 160;

        window.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
        window.Left = Math.Min(cursorDip.X + 18, workAreaDip.Right - width - 18);
        window.Top = Math.Min(cursorDip.Y + 18, workAreaDip.Bottom - height - 18);

        if (window.Left < workAreaDip.Left)
        {
            window.Left = workAreaDip.Left + 18;
        }

        if (window.Top < workAreaDip.Top)
        {
            window.Top = workAreaDip.Top + 18;
        }
    }
}
