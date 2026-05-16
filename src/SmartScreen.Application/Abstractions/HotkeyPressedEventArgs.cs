using SmartScreen.Domain.Enums;

namespace SmartScreen.Application.Abstractions;

public sealed class HotkeyPressedEventArgs(HotkeyAction action, string? promptTemplateId = null) : EventArgs
{
    public HotkeyAction Action { get; } = action;
    public string? PromptTemplateId { get; } = promptTemplateId;
}
