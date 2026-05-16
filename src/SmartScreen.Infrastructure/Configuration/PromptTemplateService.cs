using System.Text.Json;
using SmartScreen.Application.Abstractions;
using SmartScreen.Application.Defaults;
using SmartScreen.Domain.Models;

namespace SmartScreen.Infrastructure.Configuration;

public sealed class PromptTemplateService(IStorageService storageService, ILoggingService loggingService)
    : IPromptTemplateService
{
    private const string FileName = "prompts.json";
    private readonly JsonSerializerOptions _jsonOptions = JsonOptionsFactory.Create();

    public async Task<AiPromptLibrary> LoadAsync(CancellationToken cancellationToken = default)
    {
        await storageService.EnsureDirectoriesAsync(cancellationToken);
        var path = storageService.GetConfigFilePath(FileName);

        if (!File.Exists(path))
        {
            var defaults = DefaultPromptLibraryFactory.Create();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var library = await JsonSerializer.DeserializeAsync<AiPromptLibrary>(stream, _jsonOptions, cancellationToken);
            return library ?? DefaultPromptLibraryFactory.Create();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            loggingService.Error(exception, "Prompt templates could not be loaded. Defaults will be restored.");
            File.Move(path, $"{path}.broken-{DateTimeOffset.Now:yyyyMMddHHmmss}", overwrite: true);

            var defaults = DefaultPromptLibraryFactory.Create();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }
    }

    public async Task SaveAsync(AiPromptLibrary library, CancellationToken cancellationToken = default)
    {
        await storageService.EnsureDirectoriesAsync(cancellationToken);
        await using var stream = File.Create(storageService.GetConfigFilePath(FileName));
        await JsonSerializer.SerializeAsync(stream, library, _jsonOptions, cancellationToken);
    }

    public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default) =>
        await SaveAsync(DefaultPromptLibraryFactory.Create(), cancellationToken);
}

