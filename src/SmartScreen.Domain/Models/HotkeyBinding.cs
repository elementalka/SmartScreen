using SmartScreen.Domain.Enums;

namespace SmartScreen.Domain.Models;

public sealed class HotkeyBinding
{
    public HotkeyAction Action { get; set; }
    public string Gesture { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string? PromptTemplateId { get; set; }
}

