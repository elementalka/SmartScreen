using System.Net.Http;
using SmartScreen.Application.Abstractions;
using SmartScreen.Domain.Enums;
using SmartScreen.Domain.Models;

namespace SmartScreen.Infrastructure.Ai;

public sealed class AiProviderFactory(HttpClient httpClient) : IAiProviderFactory
{
    public IAiProvider Create(AiProviderSettings settings) =>
        settings.Kind switch
        {
            AiProviderKind.Gemini => new GeminiProvider(httpClient),
            AiProviderKind.OpenAiCompatible or AiProviderKind.Nvidia or AiProviderKind.OpenRouter or AiProviderKind.OpenAi or AiProviderKind.Custom
                => new OpenAiCompatibleProvider(httpClient),
            _ => new OpenAiCompatibleProvider(httpClient)
        };
}

