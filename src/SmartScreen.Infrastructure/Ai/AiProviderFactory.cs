using System.Net.Http;
using SmartScreen.Application.Abstractions;
using SmartScreen.Domain.Enums;
using SmartScreen.Domain.Models;

namespace SmartScreen.Infrastructure.Ai;

public sealed class AiProviderFactory(HttpClient httpClient, ITextLocalizer textLocalizer) : IAiProviderFactory
{
    public IAiProvider Create(AiProviderSettings settings) =>
        settings.Kind switch
        {
            AiProviderKind.Gemini => new GeminiProvider(httpClient, textLocalizer),
            AiProviderKind.OpenAiCompatible or AiProviderKind.Nvidia or AiProviderKind.OpenRouter or AiProviderKind.OpenAi or AiProviderKind.Custom
                => new OpenAiCompatibleProvider(httpClient, textLocalizer),
            _ => new OpenAiCompatibleProvider(httpClient, textLocalizer)
        };
}
