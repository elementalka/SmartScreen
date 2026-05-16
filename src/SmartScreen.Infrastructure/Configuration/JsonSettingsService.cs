using System.Text.Json;
using SmartScreen.Application.Abstractions;
using SmartScreen.Application.Defaults;
using SmartScreen.Domain.Models;

namespace SmartScreen.Infrastructure.Configuration;

public sealed class JsonSettingsService(IStorageService storageService, ILoggingService loggingService) : ISettingsService
{
    private const string FileName = "appsettings.json";
    private readonly JsonSerializerOptions _jsonOptions = JsonOptionsFactory.Create();

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await storageService.EnsureDirectoriesAsync(cancellationToken);
        var path = storageService.GetConfigFilePath(FileName);

        if (!File.Exists(path))
        {
            var defaults = DefaultAppSettingsFactory.Create();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _jsonOptions, cancellationToken)
                ?? DefaultAppSettingsFactory.Create();
            Normalize(settings);
            await SaveAsync(settings, cancellationToken);
            return settings;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            loggingService.Error(exception, "App settings could not be loaded. Defaults will be restored.");
            MoveBrokenFile(path);

            var defaults = DefaultAppSettingsFactory.Create();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await storageService.EnsureDirectoriesAsync(cancellationToken);
        var path = storageService.GetConfigFilePath(FileName);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions, cancellationToken);
    }

    public async Task<AppSettings> ResetAsync(CancellationToken cancellationToken = default)
    {
        var defaults = DefaultAppSettingsFactory.Create();
        await SaveAsync(defaults, cancellationToken);
        return defaults;
    }

    private static void MoveBrokenFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var brokenPath = $"{path}.broken-{DateTimeOffset.Now:yyyyMMddHHmmss}";
        File.Move(path, brokenPath, overwrite: true);
    }

    private static void Normalize(AppSettings settings)
    {
        var defaults = DefaultAppSettingsFactory.Create();

        settings.StartMinimizedToTray = true;
        settings.MinimizeToTrayOnClose = true;

        foreach (var defaultProvider in defaults.Ai.Providers)
        {
            var existing = settings.Ai.Providers.FirstOrDefault(provider => provider.Id == defaultProvider.Id);

            if (existing is null)
            {
                settings.Ai.Providers.Add(defaultProvider);
                continue;
            }

            if (string.IsNullOrWhiteSpace(existing.Endpoint))
            {
                existing.Endpoint = defaultProvider.Endpoint;
            }

            if (string.IsNullOrWhiteSpace(existing.Model) ||
                existing.Model.Equals("gemini-2.5-flash", StringComparison.OrdinalIgnoreCase) ||
                existing.Model.Equals("meta/llama-3.2-11b-vision-instruct", StringComparison.OrdinalIgnoreCase))
            {
                existing.Model = defaultProvider.Model;
            }
        }

        if (settings.Ai.ActiveProviderId.Equals("gemini", StringComparison.OrdinalIgnoreCase))
        {
            settings.Ai.ActiveProviderId = "gemini-pro";
        }
    }
}
