namespace SmartScreen.Domain.Models;

public sealed class AiSecrets
{
    public Dictionary<string, string> ProviderApiKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

