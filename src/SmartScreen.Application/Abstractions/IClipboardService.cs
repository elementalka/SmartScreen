using SmartScreen.Domain.Models;

namespace SmartScreen.Application.Abstractions;

public interface IClipboardService
{
    Task CopyImageAsync(ScreenshotResult screenshot, CancellationToken cancellationToken = default);
    Task CopyTextAsync(string text, CancellationToken cancellationToken = default);
}

