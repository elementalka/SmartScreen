using SmartScreen.Domain.Models;

namespace SmartScreen.Application.Abstractions;

public interface IHotkeySettingsService
{
    Task<HotkeySettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(HotkeySettings settings, CancellationToken cancellationToken = default);
}

