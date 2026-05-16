using SmartScreen.Domain.Models;

namespace SmartScreen.Application.Abstractions;

public interface IHotkeyService : IDisposable
{
    Task RegisterAsync(HotkeySettings settings, CancellationToken cancellationToken = default);
    Task UnregisterAllAsync(CancellationToken cancellationToken = default);
}

