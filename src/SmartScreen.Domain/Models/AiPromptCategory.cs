namespace SmartScreen.Domain.Models;

public sealed class AiPromptCategory
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public int Order { get; set; }
}

