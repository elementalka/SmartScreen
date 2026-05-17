using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using SmartScreen.Domain.Models;
using SmartScreen.Infrastructure.Ai;
using SmartScreen.Infrastructure.Configuration;
using SmartScreen.Infrastructure.Logging;
using SmartScreen.Infrastructure.Storage;

namespace SmartScreen.Tests;

[TestClass]
public sealed class AiInfrastructureTests
{
    [TestMethod]
    public async Task PromptServiceAddsMissingDefaultPromptsToLegacyLibrary()
    {
        var root = CreateTempDirectory();
        try
        {
            var storage = new StorageService(root);
            var logger = new FileLoggingService(storage);
            var service = new PromptTemplateService(storage, logger);

            await storage.EnsureDirectoriesAsync();
            await File.WriteAllTextAsync(
                Path.Combine(root, "config", "prompts.json"),
                """
                {
                  "categories": [
                    { "id": "general", "name": "Загальні", "isSystem": true, "order": 0 }
                  ],
                  "templates": [
                    {
                      "id": "describe",
                      "categoryId": "general",
                      "title": "Custom title",
                      "prompt": "Custom prompt must stay.",
                      "isSystem": true,
                      "order": 0
                    }
                  ]
                }
                """);

            var library = await service.LoadAsync();

            Assert.IsTrue(library.Categories.Any(category => category.Id == "translation"));
            Assert.IsTrue(library.Templates.Any(template => template.Id == "translate-uk"));
            Assert.AreEqual(
                "Custom prompt must stay.",
                library.Templates.First(template => template.Id == "describe").Prompt);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void AiImagePreprocessorDownscalesLargeScreenshots()
    {
        using var bitmap = new Bitmap(2400, 1400);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
            graphics.DrawString("SmartScreen AI optimization test", SystemFonts.DefaultFont, Brushes.Black, 40, 40);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        var screenshot = new ScreenshotResult
        {
            ImageBytes = stream.ToArray(),
            MimeType = "image/png",
            Width = bitmap.Width,
            Height = bitmap.Height,
            CreatedAt = DateTimeOffset.Now,
            SuggestedFileName = "test.png"
        };

        var prepared = AiImagePreprocessor.Prepare(screenshot);

        Assert.IsTrue(prepared.WasOptimized);
        Assert.AreEqual("image/jpeg", prepared.MimeType);
        Assert.IsTrue(Math.Max(prepared.Width, prepared.Height) <= 1800);
        Assert.IsTrue(prepared.Bytes.Length <= 3 * 1024 * 1024);
    }

    [TestMethod]
    public void AiProviderErrorFormatterUsesFriendlyStatusAndMasksSecrets()
    {
        var message = AiProviderErrorFormatter.Format(
            "Provider",
            HttpStatusCode.Unauthorized,
            """
            { "error": { "message": "bad apiKey=SECRET_VALUE_SHOULD_NOT_LEAK" } }
            """);

        Assert.IsTrue(message.Contains("ключ не прийнято", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(message.Contains("SECRET_VALUE_SHOULD_NOT_LEAK", StringComparison.Ordinal));
        Assert.IsTrue(message.Contains("***", StringComparison.Ordinal));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "SmartScreen.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
