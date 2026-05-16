namespace SmartScreen.Domain.Models;

public sealed class EditorSettings
{
    public string DefaultColor { get; set; } = "#E53935";
    public double DefaultStrokeThickness { get; set; } = 3;
    public double DefaultTextSize { get; set; } = 18;
    public double HighlighterOpacity { get; set; } = 0.35;
}

