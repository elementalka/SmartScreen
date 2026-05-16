namespace SmartScreen.Domain.Models;

public sealed class AiRequest
{
    public required byte[] ImageBytes { get; init; }
    public required string ImageMimeType { get; init; }
    public required string UserPrompt { get; init; }
    public string? SystemPrompt { get; init; }
    public required AiProviderSettings ProviderSettings { get; init; }
}

