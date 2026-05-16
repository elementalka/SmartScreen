using SmartScreen.Domain.Models;

namespace SmartScreen.Application.Abstractions;

public interface IScreenshotService
{
    Task<ScreenshotResult> CaptureFullScreenAsync(CancellationToken cancellationToken = default);
    Task<ScreenshotResult> CaptureActiveWindowAsync(CancellationToken cancellationToken = default);
    Task<ScreenshotResult> CaptureRegionAsync(ScreenRegion region, CancellationToken cancellationToken = default);
}

