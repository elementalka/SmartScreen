namespace SmartScreen.Domain.Models;

public sealed class AiSettings
{
    public string ActiveProviderId { get; set; } = "gemini";
    public bool SendScreenshotsOnlyAfterConfirmation { get; set; } = true;
    public List<AiProviderSettings> Providers { get; set; } = [];
}

