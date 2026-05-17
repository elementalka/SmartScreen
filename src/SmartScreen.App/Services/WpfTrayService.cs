using SmartScreen.Application.Abstractions;
using Forms = System.Windows.Forms;

namespace SmartScreen.App.Services;

public sealed class WpfTrayService : ITrayService
{
    private Forms.NotifyIcon? _notifyIcon;

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
        menu.Items.Add("Скріншот області", null, (_, _) => CaptureRegionRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Скріншот всього екрана", null, (_, _) => CaptureFullScreenRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Скріншот активного вікна", null, (_, _) => CaptureActiveWindowRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Скріншот монітора", null, (_, _) => CaptureMonitorRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Скріншот із затримкою", null, (_, _) => CaptureDelayedRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Відкрити SmartScreen", null, (_, _) => OpenMainWindowRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Налаштування", null, (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Вийти", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "SmartScreen",
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => OpenMainWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ShowReadyNotification()
    {
        _notifyIcon?.ShowBalloonTip(
            2500,
            "SmartScreen працює",
            "Натисни Ctrl+Shift+S або відкрий меню іконки в треї.",
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
}
