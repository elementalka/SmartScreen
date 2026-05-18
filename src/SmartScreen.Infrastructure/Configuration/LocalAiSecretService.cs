using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using SmartScreen.Application.Abstractions;
using SmartScreen.Domain.Models;

namespace SmartScreen.Infrastructure.Configuration;

public sealed class LocalAiSecretService(IStorageService storageService, ILoggingService loggingService)
    : IAiSecretService
{
    private const string FileName = "secrets.local.json";
    private const string DpapiPrefix = "dpapi:";
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
            settings.ApiKey = Unprotect(apiKey);
        }
    }

    public async Task SaveApiKeyAsync(string providerId, string apiKey, CancellationToken cancellationToken = default)
    {
        var secrets = await LoadAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            secrets.ProviderApiKeys.Remove(providerId);
        }
        else
        {
            secrets.ProviderApiKeys[providerId] = Protect(apiKey);
        }

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
            return await JsonFileStore.ReadAsync<AiSecrets>(path, _jsonOptions, cancellationToken)
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
        await JsonFileStore.WriteAsync(storageService.GetConfigFilePath(FileName), secrets, _jsonOptions, cancellationToken);
    }

    private static string Protect(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return $"{DpapiPrefix}{Convert.ToBase64String(protectedBytes)}";
    }

    private string Unprotect(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(DpapiPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(value[DpapiPrefix.Length..]);
            var bytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            loggingService.Error(exception, "Could not decrypt local AI secret.");
            return string.Empty;
        }
    }
}
