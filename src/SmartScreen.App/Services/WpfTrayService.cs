using SmartScreen.Application.Abstractions;
using Forms = System.Windows.Forms;

namespace SmartScreen.App.Services;

public sealed class WpfTrayService : ITrayService
{
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ToolStripItem? _captureRegionItem;
    private Forms.ToolStripItem? _captureFullScreenItem;
    private Forms.ToolStripItem? _captureActiveWindowItem;
    private Forms.ToolStripItem? _captureMonitorItem;
    private Forms.ToolStripItem? _captureDelayedItem;
    private Forms.ToolStripItem? _openMainWindowItem;
    private Forms.ToolStripItem? _openSettingsItem;
    private Forms.ToolStripItem? _exitItem;

    public event EventHandler? CaptureRegionRequested;
    public event EventHandler? CaptureFullScreenRequested;
    public event EventHandler? CaptureActiveWindowRequested;
    public event EventHandler? CaptureMonitorRequested;
    public event EventHandler? CaptureDelayedRequested;
    public event EventHandler? OpenMainWindowRequested;
    public event EventHandler? OpenSettingsRequested;
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        var menu = new Forms.ContextMenuStrip();
        _captureRegionItem = menu.Items.Add(string.Empty, null, (_, _) => CaptureRegionRequested?.Invoke(this, EventArgs.Empty));
        _captureFullScreenItem = menu.Items.Add(string.Empty, null, (_, _) => CaptureFullScreenRequested?.Invoke(this, EventArgs.Empty));
        _captureActiveWindowItem = menu.Items.Add(string.Empty, null, (_, _) => CaptureActiveWindowRequested?.Invoke(this, EventArgs.Empty));
        _captureMonitorItem = menu.Items.Add(string.Empty, null, (_, _) => CaptureMonitorRequested?.Invoke(this, EventArgs.Empty));
        _captureDelayedItem = menu.Items.Add(string.Empty, null, (_, _) => CaptureDelayedRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new Forms.ToolStripSeparator());
        _openMainWindowItem = menu.Items.Add(string.Empty, null, (_, _) => OpenMainWindowRequested?.Invoke(this, EventArgs.Empty));
        _openSettingsItem = menu.Items.Add(string.Empty, null, (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new Forms.ToolStripSeparator());
        _exitItem = menu.Items.Add(string.Empty, null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "SmartScreen",
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => OpenMainWindowRequested?.Invoke(this, EventArgs.Empty);
        RefreshLocalization();
    }

    public void RefreshLocalization()
    {
        SetText(_captureRegionItem, "action.captureRegion", "Скріншот області");
        SetText(_captureFullScreenItem, "action.captureFullScreen", "Скріншот всього екрана");
        SetText(_captureActiveWindowItem, "action.captureActiveWindow", "Скріншот активного вікна");
        SetText(_captureMonitorItem, "action.captureMonitor", "Скріншот монітора");
        SetText(_captureDelayedItem, "action.captureDelayed", "Скріншот із затримкою");
        SetText(_openMainWindowItem, "tray.openSmartScreen", "Відкрити SmartScreen");
        SetText(_openSettingsItem, "main.settings", "Налаштування");
        SetText(_exitItem, "tray.exit", "Вийти");
    }

    public void ShowReadyNotification()
    {
        _notifyIcon?.ShowBalloonTip(
            2500,
            Text("tray.readyTitle", "SmartScreen працює"),
            Text("tray.readyMessage", "Натисни Ctrl+Shift+S або відкрий меню іконки в треї."),
            Forms.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }

    private static void SetText(Forms.ToolStripItem? item, string key, string fallback)
    {
        if (item is not null)
        {
            item.Text = Text(key, fallback);
        }
    }

    private static string Text(string key, string fallback) =>
        LocalizationResourceService.GetString(key, fallback);
}
