using System.Text.Json;
using SmartScreen.App.Services;

namespace SmartScreen.Tests;

[TestClass]
public sealed class LocalizationResourceTests
{
    [TestMethod]
    public async Task LocalizationFilesContainAllBuiltInResourceKeys()
    {
        var root = FindRepositoryRoot();
        var localizationDirectory = Path.Combine(root, "localization");

        foreach (var culture in new[] { "uk-UA", "en-US" })
        {
            var path = Path.Combine(localizationDirectory, $"{culture}.json");
            var json = await File.ReadAllTextAsync(path);
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? throw new InvalidOperationException($"Localization file is empty: {path}");

            var missingKeys = LocalizationResourceService.BuiltInKeys
                .Where(key => !values.ContainsKey(key))
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Assert.AreEqual(
                0,
                missingKeys.Length,
                $"{culture} is missing localization keys: {string.Join(", ", missingKeys)}");
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SmartScreen.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find SmartScreen repository root.");
    }
}
