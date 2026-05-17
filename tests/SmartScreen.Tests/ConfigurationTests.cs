using SmartScreen.Domain.Models;
using SmartScreen.Domain.Enums;
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
            Assert.AreEqual("gemini-flash", settings.Ai.ActiveProviderId);
            Assert.AreEqual(
                "gemini-flash-latest",
                settings.Ai.Providers.First(provider => provider.Id == "gemini-flash").Model);
            CollectionAssert.AreEqual(
                new[] { AfterCaptureAction.CopyImageToClipboard, AfterCaptureAction.ShowQuickActions },
                settings.Screenshots.AfterCaptureActions);
            Assert.IsTrue(File.Exists(Path.Combine(root, "config", "appsettings.json")));
            Assert.IsTrue(settings.Ai.Providers.Count >= 2);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task SettingsServiceMigratesLegacyAfterCaptureFlags()
    {
        var root = CreateTempDirectory();
        try
        {
            var storage = new StorageService(root);
            var logger = new FileLoggingService(storage);
            var service = new JsonSettingsService(storage, logger);

            await storage.EnsureDirectoriesAsync();
            await File.WriteAllTextAsync(
                Path.Combine(root, "config", "appsettings.json"),
                """
                {
                  "firstRunCompleted": false,
                  "startMinimizedToTray": true,
                  "minimizeToTrayOnClose": true,
                  "screenshots": {
                    "defaultFormat": "png",
                    "jpegQuality": 90,
                    "fileNameTemplate": "screenshot_{yyyy-MM-dd}_{HH-mm-ss}",
                    "defaultMode": "region",
                    "copyToClipboardAutomatically": true,
                    "showQuickActionsAfterCapture": false,
                    "delaySeconds": 0,
                    "saveDirectory": "screenshots"
                  },
                  "editor": {},
                  "ai": {
                    "activeProviderId": "gemini-flash",
                    "sendScreenshotsOnlyAfterConfirmation": true,
                    "providers": []
                  },
                  "theme": {},
                  "language": "uk-UA"
                }
                """);

            var settings = await service.LoadAsync();

            CollectionAssert.AreEqual(
                new[] { AfterCaptureAction.CopyImageToClipboard },
                settings.Screenshots.AfterCaptureActions);
            Assert.IsTrue(settings.Screenshots.CopyToClipboardAutomatically);
            Assert.IsFalse(settings.Screenshots.ShowQuickActionsAfterCapture);
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

    [TestMethod]
    public async Task LocalAiSecretServiceStoresKeysOutsideAppSettings()
    {
        var root = CreateTempDirectory();
        try
        {
            var storage = new StorageService(root);
            var logger = new FileLoggingService(storage);
            var service = new LocalAiSecretService(storage, logger);
            var settings = new AiProviderSettings
            {
                Id = "gemini-pro",
                DisplayName = "Google Gemini Pro"
            };

            await service.SaveApiKeyAsync(settings.Id, "secret-test-key");
            await service.ApplySecretsAsync(settings);

            Assert.AreEqual("secret-test-key", settings.ApiKey);
            var secretsPath = Path.Combine(root, "config", "secrets.local.json");
            Assert.IsTrue(File.Exists(secretsPath));
            var secretsJson = await File.ReadAllTextAsync(secretsPath);
            Assert.IsFalse(secretsJson.Contains("secret-test-key", StringComparison.Ordinal));
            Assert.IsTrue(secretsJson.Contains("dpapi:", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task HotkeyServiceRemovesLegacyPrintScreenDefault()
    {
        var root = CreateTempDirectory();
        try
        {
            var storage = new StorageService(root);
            var logger = new FileLoggingService(storage);
            var service = new JsonHotkeySettingsService(storage, logger);

            await storage.EnsureDirectoriesAsync();
            await File.WriteAllTextAsync(
                Path.Combine(root, "config", "hotkeys.json"),
                """
                {
                  "bindings": [
                    {
                      "action": "captureRegion",
                      "gesture": "PrintScreen",
                      "isEnabled": true,
                      "promptTemplateId": null
                    },
                    {
                      "action": "captureFullScreen",
                      "gesture": "Ctrl+Shift+F",
                      "isEnabled": true,
                      "promptTemplateId": null
                    }
                  ]
                }
                """);

            var settings = await service.LoadAsync();

            Assert.IsFalse(settings.Bindings.Any(binding =>
                binding.Gesture.Equals("PrintScreen", StringComparison.OrdinalIgnoreCase)));
            Assert.IsTrue(settings.Bindings.Any(binding =>
                binding.Action == Domain.Enums.HotkeyAction.CaptureRegion &&
                binding.Gesture.Equals("Ctrl+Shift+S", StringComparison.OrdinalIgnoreCase)));
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
