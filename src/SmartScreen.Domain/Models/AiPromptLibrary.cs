namespace SmartScreen.Domain.Models;

public sealed class AiPromptLibrary
{
    public List<AiPromptCategory> Categories { get; set; } = [];
    public List<AiPromptTemplate> Templates { get; set; } = [];
}

