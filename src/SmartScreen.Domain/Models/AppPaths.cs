namespace SmartScreen.Domain.Models;

public sealed class AppPaths
{
    public required string BaseDirectory { get; init; }
    public required string ConfigDirectory { get; init; }
    public required string ScreenshotsDirectory { get; init; }
    public required string LogsDirectory { get; init; }
    public required string LocalizationDirectory { get; init; }
    public required string ThemesDirectory { get; init; }
    public required string FallbackDirectory { get; init; }
}

