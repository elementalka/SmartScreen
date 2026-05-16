using SmartScreen.Domain.Enums;
using SmartScreen.Domain.Models;

namespace SmartScreen.Application.Abstractions;

public interface IImageFileService
{
    Task<string> SaveAsync(
        ScreenshotResult screenshot,
        string? directory,
        ScreenshotImageFormat format,
        int jpegQuality,
        CancellationToken cancellationToken = default);
}

