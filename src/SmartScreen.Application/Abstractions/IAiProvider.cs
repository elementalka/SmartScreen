using SmartScreen.Domain.Models;

namespace SmartScreen.Application.Abstractions;

public interface IAiProvider
{
    string Name { get; }

    Task<AiResponse> AnalyzeImageAsync(
        AiRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> TestConnectionAsync(
        AiProviderSettings settings,
        CancellationToken cancellationToken = default);
}

