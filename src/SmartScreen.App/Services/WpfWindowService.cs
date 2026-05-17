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
    ILocalizationService localizationService,
    ILoggingService loggingService) : IWindowService
{
    private ScreenshotOverlayWindow? _regionSelectionWindow;
    private QuickActionsWindow? _quickActionsWindow;
    private SettingsWindow? _settingsWindow;

    public Task<ScreenRegion?> SelectRegionAsync()
    {
        if (ActivateExistingWindow(_regionSelectionWindow))
        {
            return Task.FromResult<ScreenRegion?>(null);
        }

        var overlay = new ScreenshotOverlayWindow();
        _regionSelectionWindow = overlay;
        AssignVisibleOwner(overlay);

        try
        {
            var result = overlay.ShowDialog();
            return Task.FromResult(result == true ? overlay.SelectedRegion : null);
        }
        finally
        {
            if (ReferenceEquals(_regionSelectionWindow, overlay))
            {
                _regionSelectionWindow = null;
            }
        }
    }

    public Task ShowQuickActionsAsync(
        ScreenshotResult screenshot,
        CaptureWorkspaceStartupMode startupMode = CaptureWorkspaceStartupMode.Actions,
        string? promptTemplateId = null,
        string? customPrompt = null,
        bool startAiImmediately = false)
    {
        if (ActivateExistingWindow(_quickActionsWindow))
        {
            return Task.CompletedTask;
        }

        var viewModel = new QuickActionsViewModel(
            screenshot,
            clipboardService,
            imageFileService,
            settingsService,
            storageService,
            promptTemplateService,
            aiService,
            loggingService,
            startupMode,
            promptTemplateId,
            customPrompt,
            startAiImmediately);

        var window = new QuickActionsWindow(viewModel);
        _quickActionsWindow = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_quickActionsWindow, window))
            {
                _quickActionsWindow = null;
            }
        };

        AssignVisibleOwner(window);

        window.Opacity = 0;
        window.Show();
        window.UpdateLayout();
        if (window.WindowState != WindowState.Maximized)
        {
            PositionNearCursor(window);
        }

        window.Opacity = 1;
        window.Activate();
        return Task.CompletedTask;
    }

    public void ShowSettings()
    {
        if (ActivateExistingWindow(_settingsWindow))
        {
            return;
        }

        var viewModel = new SettingsViewModel(
            settingsService,
            hotkeySettingsService,
            hotkeyService,
            storageService,
            aiService,
            aiSecretService,
            promptTemplateService,
            localizationService,
            loggingService);
        var window = new SettingsWindow(viewModel);
        _settingsWindow = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_settingsWindow, window))
            {
                _settingsWindow = null;
            }
        };

        AssignVisibleOwner(window);

        window.ShowDialog();
    }

    private static bool ActivateExistingWindow(System.Windows.Window? window)
    {
        if (window is null)
        {
            return false;
        }

        if (!window.IsVisible)
        {
            return false;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
        return true;
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
