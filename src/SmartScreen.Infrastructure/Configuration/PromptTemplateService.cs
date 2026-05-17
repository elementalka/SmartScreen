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
            var library = await JsonFileStore.ReadAsync<AiPromptLibrary>(path, _jsonOptions, cancellationToken)
                ?? DefaultPromptLibraryFactory.Create();

            if (Normalize(library))
            {
                await SaveAsync(library, cancellationToken);
            }

            return library;
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

    private static bool Normalize(AiPromptLibrary library)
    {
        var changed = false;
        var defaults = DefaultPromptLibraryFactory.Create();

        foreach (var category in defaults.Categories)
        {
            if (library.Categories.Any(existing => existing.Id.Equals(category.Id, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            library.Categories.Add(category);
            changed = true;
        }

        foreach (var template in defaults.Templates)
        {
            if (library.Templates.Any(existing => existing.Id.Equals(template.Id, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            library.Templates.Add(template);
            changed = true;
        }

        if (changed)
        {
            library.Categories = library.Categories
                .OrderBy(category => category.Order)
                .ThenBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            library.Templates = library.Templates
                .OrderBy(template => template.Order)
                .ThenBy(template => template.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return changed;
    }
}
