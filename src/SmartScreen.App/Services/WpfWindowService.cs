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
    IHotkeySettingsService hotkeySettingsService,
    IHotkeyService hotkeyService,
    IStorageService storageService,
    IAiService aiService,
    IAiSecretService aiSecretService,
    IPromptTemplateService promptTemplateService,
    ILoggingService loggingService) : IWindowService
{
    public Task<ScreenRegion?> SelectRegionAsync()
    {
        var overlay = new ScreenshotOverlayWindow();
        AssignVisibleOwner(overlay);

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
            promptTemplateService,
            loggingService);

        var window = new QuickActionsWindow(viewModel);
        AssignVisibleOwner(window);

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
        var editor = new ScreenshotEditorWindow(screenshot);
        AssignVisibleOwner(editor);

        var result = editor.ShowDialog();
        return Task.FromResult(result == true ? editor.EditedScreenshot : null);
    }

    public void ShowAiResponse(
        ScreenshotResult screenshot,
        string? promptTemplateId = null,
        string? customPrompt = null,
        bool startImmediately = false)
    {
        var viewModel = new AiResponseViewModel(
            screenshot,
            aiService,
            clipboardService,
            promptTemplateService,
            storageService,
            this,
            promptTemplateId,
            customPrompt,
            startImmediately);
        var window = new AiResponseWindow(viewModel);
        AssignVisibleOwner(window);

        window.Show();
    }

    public void ShowSettings()
    {
        var viewModel = new SettingsViewModel(
            settingsService,
            hotkeySettingsService,
            hotkeyService,
            storageService,
            aiService,
            aiSecretService,
            promptTemplateService,
            loggingService);
        var window = new SettingsWindow(viewModel);
        AssignVisibleOwner(window);

        window.ShowDialog();
    }

    private static void AssignVisibleOwner(System.Windows.Window window)
    {
        var owner = System.Windows.Application.Current.MainWindow;
        if (owner is null || ReferenceEquals(owner, window) || !owner.IsVisible)
        {
            if (window.WindowStartupLocation == System.Windows.WindowStartupLocation.CenterOwner)
            {
                window.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            }

            return;
        }

        window.Owner = owner;
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
