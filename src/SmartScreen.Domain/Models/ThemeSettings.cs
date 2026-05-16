using SmartScreen.Domain.Enums;

namespace SmartScreen.Domain.Models;

public sealed class ThemeSettings
{
    public ThemeMode Mode { get; set; } = ThemeMode.System;
    public string AccentColor { get; set; } = "#2F7DFF";
}

