using SmartScreen.Domain.Enums;

namespace SmartScreen.Domain.Models;

public sealed class AiProviderSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = "AI Provider";
    public AiProviderKind Kind { get; set; } = AiProviderKind.OpenAiCompatible;
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = "You are a helpful assistant that analyzes screenshots clearly and safely.";
    public int TimeoutSeconds { get; set; } = 60;
    public bool IsEnabled { get; set; } = true;
}

