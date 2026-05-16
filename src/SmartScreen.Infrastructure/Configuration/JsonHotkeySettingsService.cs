using System.Text.Json;
using SmartScreen.Application.Abstractions;
using SmartScreen.Application.Defaults;
using SmartScreen.Domain.Models;

namespace SmartScreen.Infrastructure.Configuration;

public sealed class JsonHotkeySettingsService(IStorageService storageService, ILoggingService loggingService)
    : IHotkeySettingsService
{
    private const string FileName = "hotkeys.json";
    private readonly JsonSerializerOptions _jsonOptions = JsonOptionsFactory.Create();

    public async Task<HotkeySettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await storageService.EnsureDirectoriesAsync(cancellationToken);
        var path = storageService.GetConfigFilePath(FileName);

        if (!File.Exists(path))
        {
            var defaults = DefaultHotkeySettingsFactory.Create();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var settings = await JsonSerializer.DeserializeAsync<HotkeySettings>(stream, _jsonOptions, cancellationToken)
                ?? DefaultHotkeySettingsFactory.Create();
            Normalize(settings);
            await SaveAsync(settings, cancellationToken);
            return settings;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            loggingService.Error(exception, "Hotkey settings could not be loaded. Defaults will be restored.");
            File.Move(path, $"{path}.broken-{DateTimeOffset.Now:yyyyMMddHHmmss}", overwrite: true);

            var defaults = DefaultHotkeySettingsFactory.Create();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }
    }

    public async Task SaveAsync(HotkeySettings settings, CancellationToken cancellationToken = default)
    {
        await storageService.EnsureDirectoriesAsync(cancellationToken);
        await using var stream = File.Create(storageService.GetConfigFilePath(FileName));
        await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions, cancellationToken);
    }

    private static void Normalize(HotkeySettings settings)
    {
        var defaults = DefaultHotkeySettingsFactory.Create();

        foreach (var defaultBinding in defaults.Bindings)
        {
            var alreadyExists = settings.Bindings.Any(binding =>
                binding.Action == defaultBinding.Action &&
                binding.Gesture.Equals(defaultBinding.Gesture, StringComparison.OrdinalIgnoreCase));

            if (!alreadyExists)
            {
                settings.Bindings.Insert(0, defaultBinding);
            }
        }
    }
}
