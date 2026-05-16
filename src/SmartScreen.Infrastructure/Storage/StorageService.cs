using SmartScreen.Application.Abstractions;
using SmartScreen.Domain.Models;

namespace SmartScreen.Infrastructure.Storage;

public sealed class StorageService : IStorageService
{
    public StorageService(string? baseDirectory = null)
    {
        var root = baseDirectory ?? AppContext.BaseDirectory;
        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartScreen");

        Paths = new AppPaths
        {
            BaseDirectory = root,
            ConfigDirectory = Path.Combine(root, "config"),
            ScreenshotsDirectory = Path.Combine(root, "screenshots"),
            LogsDirectory = Path.Combine(root, "logs"),
            LocalizationDirectory = Path.Combine(root, "localization"),
            ThemesDirectory = Path.Combine(root, "themes"),
            FallbackDirectory = fallback
        };
    }

    public AppPaths Paths { get; }

    public Task EnsureDirectoriesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var directory in new[]
                 {
                     Paths.ConfigDirectory,
                     Paths.ScreenshotsDirectory,
                     Paths.LogsDirectory,
                     Paths.LocalizationDirectory,
                     Paths.ThemesDirectory
                 })
        {
            Directory.CreateDirectory(directory);
        }

        return Task.CompletedTask;
    }

    public string ResolveWritableScreenshotsDirectory(string? configuredDirectory)
    {
        var candidate = ResolveDirectory(configuredDirectory, Paths.ScreenshotsDirectory);

        if (CanWriteTo(candidate))
        {
            return candidate;
        }

        var fallback = Path.Combine(Paths.FallbackDirectory, "screenshots");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    public string GetConfigFilePath(string fileName) => Path.Combine(Paths.ConfigDirectory, fileName);

    private string ResolveDirectory(string? configuredDirectory, string defaultDirectory)
    {
        if (string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return defaultDirectory;
        }

        return Path.IsPathRooted(configuredDirectory)
            ? configuredDirectory
            : Path.Combine(Paths.BaseDirectory, configuredDirectory);
    }

    private static bool CanWriteTo(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

