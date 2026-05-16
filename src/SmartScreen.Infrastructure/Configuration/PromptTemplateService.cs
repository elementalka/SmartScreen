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
            var library = await JsonFileStore.ReadAsync<AiPromptLibrary>(path, _jsonOptions, cancellationToken);
            return library ?? DefaultPromptLibraryFactory.Create();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            loggingService.Error(exception, "Prompt templates could not be loaded. Defaults will be restored.");
            JsonFileStore.MoveBrokenFile(path);

            var defaults = DefaultPromptLibraryFactory.Create();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }
    }

    public async Task SaveAsync(AiPromptLibrary library, CancellationToken cancellationToken = default)
    {
        await storageService.EnsureDirectoriesAsync(cancellationToken);
        await JsonFileStore.WriteAsync(storageService.GetConfigFilePath(FileName), library, _jsonOptions, cancellationToken);
    }

    public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default) =>
        await SaveAsync(DefaultPromptLibraryFactory.Create(), cancellationToken);
}
