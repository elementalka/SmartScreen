using System.Text.Json;
using SmartScreen.Application.Abstractions;

namespace SmartScreen.Infrastructure.Configuration;

public sealed class LocalizationService(IStorageService storageService, ILoggingService loggingService)
    : ILocalizationService
{
    private readonly Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);

    public async Task LoadAsync(string cultureName, CancellationToken cancellationToken = default)
    {
        _strings.Clear();

        var requestedPath = Path.Combine(storageService.Paths.LocalizationDirectory, $"{cultureName}.json");
        var fallbackPath = Path.Combine(storageService.Paths.LocalizationDirectory, "uk-UA.json");
        var path = File.Exists(requestedPath) ? requestedPath : fallbackPath;

        if (!File.Exists(path))
        {
            loggingService.Warning($"Localization file was not found: {path}");
            return;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var values = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(
                stream,
                JsonOptionsFactory.Create(),
                cancellationToken);

            if (values is null)
            {
                return;
            }

            foreach (var pair in values)
            {
                _strings[pair.Key] = pair.Value;
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            loggingService.Error(exception, "Localization file could not be loaded.");
        }
    }

    public string GetString(string key) =>
        _strings.TryGetValue(key, out var value) ? value : key;
}

