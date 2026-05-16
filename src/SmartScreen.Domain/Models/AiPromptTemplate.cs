namespace SmartScreen.Domain.Models;

public sealed class AiPromptTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CategoryId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public int Order { get; set; }
}

