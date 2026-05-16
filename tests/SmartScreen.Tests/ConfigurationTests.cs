using SmartScreen.Infrastructure.Configuration;
using SmartScreen.Infrastructure.Logging;
using SmartScreen.Infrastructure.Storage;

namespace SmartScreen.Tests;

[TestClass]
public sealed class ConfigurationTests
{
    [TestMethod]
    public async Task SettingsServiceCreatesDefaultSettingsWhenFileIsMissing()
    {
        var root = CreateTempDirectory();
        try
        {
            var storage = new StorageService(root);
            var logger = new FileLoggingService(storage);
            var service = new JsonSettingsService(storage, logger);

            var settings = await service.LoadAsync();

            Assert.AreEqual("uk-UA", settings.Language);
            Assert.IsTrue(File.Exists(Path.Combine(root, "config", "appsettings.json")));
            Assert.IsTrue(settings.Ai.Providers.Count >= 2);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task PromptServiceCreatesDefaultPromptsWhenFileIsMissing()
    {
        var root = CreateTempDirectory();
        try
        {
            var storage = new StorageService(root);
            var logger = new FileLoggingService(storage);
            var service = new PromptTemplateService(storage, logger);

            var library = await service.LoadAsync();

            Assert.IsTrue(library.Categories.Count >= 3);
            Assert.IsTrue(library.Templates.Any(template => template.Id == "explain-error"));
            Assert.IsTrue(File.Exists(Path.Combine(root, "config", "prompts.json")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void LoggerMasksApiKeys()
    {
        var root = CreateTempDirectory();
        try
        {
            var storage = new StorageService(root);
            storage.EnsureDirectoriesAsync().GetAwaiter().GetResult();
            var logger = new FileLoggingService(storage);

            logger.Error("apiKey=SECRET_VALUE_SHOULD_NOT_LEAK");

            var log = File.ReadAllText(Path.Combine(root, "logs", "app.log"));
            Assert.IsFalse(log.Contains("SECRET_VALUE_SHOULD_NOT_LEAK", StringComparison.Ordinal));
            Assert.IsTrue(log.Contains("***", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "SmartScreen.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

