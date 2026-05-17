using SmartScreen.Domain.Enums;
using SmartScreen.Domain.Models;

namespace SmartScreen.Application.Defaults;

public static class DefaultHotkeySettingsFactory
{
    public static HotkeySettings Create() => new()
    {
        Bindings =
        [
            new HotkeyBinding { Action = HotkeyAction.CaptureDefault, Gesture = "Ctrl+Shift+Space", IsEnabled = false },
            new HotkeyBinding { Action = HotkeyAction.CaptureRegion, Gesture = "Ctrl+Shift+S" },
            new HotkeyBinding { Action = HotkeyAction.CaptureFullScreen, Gesture = "Ctrl+Shift+F" },
            new HotkeyBinding { Action = HotkeyAction.CaptureActiveWindow, Gesture = "Ctrl+Shift+W" },
            new HotkeyBinding { Action = HotkeyAction.CaptureMonitor, Gesture = "Ctrl+Shift+M", IsEnabled = false },
            new HotkeyBinding { Action = HotkeyAction.CaptureDelayed, Gesture = "Ctrl+Shift+D", IsEnabled = false },
            new HotkeyBinding { Action = HotkeyAction.AskAiForCurrentScreenshot, Gesture = "Ctrl+Shift+A" }
        ]
    };
}
