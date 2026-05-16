using SmartScreen.Application.Abstractions;
using SmartScreen.Domain.Models;

namespace SmartScreen.Infrastructure.Ai;

public sealed class AiService(
    ISettingsService settingsService,
    IAiSecretService aiSecretService,
    IAiProviderFactory providerFactory,
    ILoggingService loggingService) : IAiService
{
    public async Task<AiResponse> AnalyzeCurrentScreenshotAsync(
        ScreenshotResult screenshot,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.LoadAsync(cancellationToken);
        var providerSettings = settings.Ai.Providers.FirstOrDefault(provider => provider.Id == settings.Ai.ActiveProviderId)
            ?? settings.Ai.Providers.FirstOrDefault(provider => provider.IsEnabled);

        if (providerSettings is null)
        {
            return AiResponse.Fail("AI-провайдера не налаштовано.", TimeSpan.Zero);
        }

        await aiSecretService.ApplySecretsAsync(providerSettings, cancellationToken);

        if (string.IsNullOrWhiteSpace(providerSettings.ApiKey))
        {
            var envName = aiSecretService.GetEnvironmentVariableName(providerSettings.Id);
            return AiResponse.Fail($"API-ключ для {providerSettings.DisplayName} не вказано. Додай ключ у Налаштуваннях або через {envName}.", TimeSpan.Zero);
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, providerSettings.TimeoutSeconds)));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var provider = providerFactory.Create(providerSettings);
            return await provider.AnalyzeImageAsync(new AiRequest
            {
                ImageBytes = screenshot.ImageBytes,
                ImageMimeType = screenshot.MimeType,
                UserPrompt = prompt,
                SystemPrompt = providerSettings.SystemPrompt,
                ProviderSettings = providerSettings
            }, linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            return AiResponse.Fail("AI-запит перевищив ліміт очікування.", TimeSpan.FromSeconds(providerSettings.TimeoutSeconds));
        }
        catch (OperationCanceledException)
        {
            return AiResponse.Fail("AI-запит скасовано.", TimeSpan.Zero);
        }
        catch (Exception exception)
        {
            loggingService.Error(exception, "AI request failed.");
            return AiResponse.Fail("Не вдалося виконати AI-запит. Перевір налаштування провайдера та інтернет.", TimeSpan.Zero);
        }
    }

    public async Task<bool> TestActiveProviderAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.LoadAsync(cancellationToken);
        var providerSettings = settings.Ai.Providers.FirstOrDefault(provider => provider.Id == settings.Ai.ActiveProviderId);

        if (providerSettings is null || string.IsNullOrWhiteSpace(providerSettings.ApiKey))
        {
            if (providerSettings is not null)
            {
                await aiSecretService.ApplySecretsAsync(providerSettings, cancellationToken);
            }
        }

        if (providerSettings is null || string.IsNullOrWhiteSpace(providerSettings.ApiKey))
        {
            return false;
        }

        return await providerFactory.Create(providerSettings).TestConnectionAsync(providerSettings, cancellationToken);
    }
}
