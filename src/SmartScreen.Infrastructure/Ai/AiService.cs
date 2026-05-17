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
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return AiResponse.Fail("Prompt порожній. Обери AI-дію або введи власний запит.", TimeSpan.Zero);
        }

        var settings = await settingsService.LoadAsync(cancellationToken);
        var providerSettings = settings.Ai.Providers.FirstOrDefault(provider =>
                provider.Id == settings.Ai.ActiveProviderId && provider.IsEnabled)
            ?? settings.Ai.Providers.FirstOrDefault(provider => provider.IsEnabled);

        if (providerSettings is null)
        {
            return AiResponse.Fail("AI-провайдера не налаштовано.", TimeSpan.Zero);
        }

        await aiSecretService.ApplySecretsAsync(providerSettings, cancellationToken);

        if (string.IsNullOrWhiteSpace(providerSettings.Model))
        {
            return AiResponse.Fail($"Для {providerSettings.DisplayName} не вказано модель.", TimeSpan.Zero);
        }

        if (string.IsNullOrWhiteSpace(providerSettings.ApiKey))
        {
            var envName = aiSecretService.GetEnvironmentVariableName(providerSettings.Id);
            return AiResponse.Fail($"API-ключ для {providerSettings.DisplayName} не вказано. Додай ключ у Налаштуваннях або через {envName}.", TimeSpan.Zero);
        }

        var preparedImage = AiImagePreprocessor.Prepare(screenshot);
        if (preparedImage.WasOptimized)
        {
            loggingService.Info(
                $"AI image optimized: {preparedImage.OriginalByteCount} bytes -> {preparedImage.Bytes.Length} bytes, {preparedImage.Width}x{preparedImage.Height}.");
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, providerSettings.TimeoutSeconds)));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var provider = providerFactory.Create(providerSettings);
            return await provider.AnalyzeImageAsync(new AiRequest
            {
                ImageBytes = preparedImage.Bytes,
                ImageMimeType = preparedImage.MimeType,
                UserPrompt = prompt.Trim(),
                SystemPrompt = providerSettings.SystemPrompt,
                ProviderSettings = providerSettings
            }, linkedCts.Token);
        }
        catch (HttpRequestException exception)
        {
            loggingService.Error(exception, "AI network request failed.");
            return AiResponse.Fail("Не вдалося підключитися до AI-провайдера. Перевір інтернет, endpoint і доступність сервісу.", TimeSpan.Zero);
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
        var providerSettings = settings.Ai.Providers.FirstOrDefault(provider =>
                provider.Id == settings.Ai.ActiveProviderId && provider.IsEnabled)
            ?? settings.Ai.Providers.FirstOrDefault(provider => provider.IsEnabled);

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

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, providerSettings.TimeoutSeconds)));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            return await providerFactory.Create(providerSettings).TestConnectionAsync(providerSettings, linkedCts.Token);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            loggingService.Error(exception, "AI provider test failed.");
            return false;
        }
    }
}
