namespace SmartScreen.Domain.Models;

public sealed class AppSettings
{
    public bool FirstRunCompleted { get; set; }
    public bool StartMinimizedToTray { get; set; }
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public ScreenshotSettings Screenshots { get; set; } = new();
    public EditorSettings Editor { get; set; } = new();
    public AiSettings Ai { get; set; } = new();
    public ThemeSettings Theme { get; set; } = new();
    public string Language { get; set; } = "uk-UA";
}

