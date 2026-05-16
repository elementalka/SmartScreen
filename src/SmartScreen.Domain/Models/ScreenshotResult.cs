namespace SmartScreen.Domain.Models;

public sealed class ScreenshotResult
{
    public required byte[] ImageBytes { get; init; }
    public required string MimeType { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string SuggestedFileName { get; init; }
    public string? SourceName { get; init; }
}

