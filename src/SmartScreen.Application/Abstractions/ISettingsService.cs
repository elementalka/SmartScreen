using SmartScreen.Domain.Models;

namespace SmartScreen.Application.Abstractions;

public interface ISettingsService
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task<AppSettings> ResetAsync(CancellationToken cancellationToken = default);
}

