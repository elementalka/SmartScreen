using SmartScreen.Domain.Enums;
using SmartScreen.Domain.Models;

namespace SmartScreen.Application.Defaults;

public static class DefaultHotkeySettingsFactory
{
    public static HotkeySettings Create() => new()
    {
        Bindings =
        [
            new HotkeyBinding { Action = HotkeyAction.CaptureRegion, Gesture = "Ctrl+Shift+S" },
            new HotkeyBinding { Action = HotkeyAction.CaptureFullScreen, Gesture = "Ctrl+Shift+F" },
            new HotkeyBinding { Action = HotkeyAction.CaptureActiveWindow, Gesture = "Ctrl+Shift+W" },
            new HotkeyBinding { Action = HotkeyAction.AskAiForCurrentScreenshot, Gesture = "Ctrl+Shift+A" }
        ]
    };
}
