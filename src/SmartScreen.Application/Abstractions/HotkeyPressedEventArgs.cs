using SmartScreen.Domain.Enums;

namespace SmartScreen.Application.Abstractions;

public sealed class HotkeyPressedEventArgs(HotkeyAction action) : EventArgs
{
    public HotkeyAction Action { get; } = action;
}

