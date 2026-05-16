using SmartScreen.Domain.Models;

namespace SmartScreen.Application.Abstractions;

public interface IAiService
{
    Task<AiResponse> AnalyzeCurrentScreenshotAsync(
        ScreenshotResult screenshot,
        string prompt,
        CancellationToken cancellationToken = default);

    Task<bool> TestActiveProviderAsync(CancellationToken cancellationToken = default);
}

