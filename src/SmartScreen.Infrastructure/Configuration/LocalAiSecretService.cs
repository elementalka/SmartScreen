using System.Text.Json;
using SmartScreen.Application.Abstractions;
using SmartScreen.Domain.Models;

namespace SmartScreen.Infrastructure.Configuration;

public sealed class LocalAiSecretService(IStorageService storageService, ILoggingService loggingService)
    : IAiSecretService
{
    private const string FileName = "secrets.local.json";
    private readonly JsonSerializerOptions _jsonOptions = JsonOptionsFactory.Create();

    public async Task ApplySecretsAsync(AiProviderSettings settings, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return;
        }

        var environmentValue = Environment.GetEnvironmentVariable(GetEnvironmentVariableName(settings.Id));
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            settings.ApiKey = environmentValue;
            return;
        }

        var secrets = await LoadAsync(cancellationToken);
        if (secrets.ProviderApiKeys.TryGetValue(settings.Id, out var apiKey) && !string.IsNullOrWhiteSpace(apiKey))
        {
            settings.ApiKey = apiKey;
        }
    }

    public async Task SaveApiKeyAsync(string providerId, string apiKey, CancellationToken cancellationToken = default)
    {
        var secrets = await LoadAsync(cancellationToken);
        secrets.ProviderApiKeys[providerId] = apiKey;
        await SaveAsync(secrets, cancellationToken);
    }

    public string GetEnvironmentVariableName(string providerId)
    {
        var normalized = providerId
            .Replace('-', '_')
            .Replace('.', '_')
            .ToUpperInvariant();

        return $"SMARTSCREEN_{normalized}_API_KEY";
    }

    private async Task<AiSecrets> LoadAsync(CancellationToken cancellationToken)
    {
        await storageService.EnsureDirectoriesAsync(cancellationToken);
        var path = storageService.GetConfigFilePath(FileName);

        if (!File.Exists(path))
        {
            return new AiSecrets();
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<AiSecrets>(stream, _jsonOptions, cancellationToken)
                ?? new AiSecrets();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            loggingService.Error(exception, "Local AI secrets could not be loaded.");
            return new AiSecrets();
        }
    }

    private async Task SaveAsync(AiSecrets secrets, CancellationToken cancellationToken)
    {
        await storageService.EnsureDirectoriesAsync(cancellationToken);
        await using var stream = File.Create(storageService.GetConfigFilePath(FileName));
        await JsonSerializer.SerializeAsync(stream, secrets, _jsonOptions, cancellationToken);
    }
}

