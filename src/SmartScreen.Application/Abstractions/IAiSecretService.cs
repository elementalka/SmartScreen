using SmartScreen.Domain.Models;

namespace SmartScreen.Application.Abstractions;

public interface IAiSecretService
{
    Task ApplySecretsAsync(AiProviderSettings settings, CancellationToken cancellationToken = default);
    Task SaveApiKeyAsync(string providerId, string apiKey, CancellationToken cancellationToken = default);
    string GetEnvironmentVariableName(string providerId);
}

