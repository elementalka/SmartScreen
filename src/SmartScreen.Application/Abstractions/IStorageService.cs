using SmartScreen.Domain.Models;

namespace SmartScreen.Application.Abstractions;

public interface IStorageService
{
    AppPaths Paths { get; }
    Task EnsureDirectoriesAsync(CancellationToken cancellationToken = default);
    string ResolveWritableScreenshotsDirectory(string? configuredDirectory);
    string GetConfigFilePath(string fileName);
}

