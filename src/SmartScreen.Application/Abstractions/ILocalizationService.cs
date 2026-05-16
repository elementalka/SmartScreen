namespace SmartScreen.Application.Abstractions;

public interface ILocalizationService
{
    Task LoadAsync(string cultureName, CancellationToken cancellationToken = default);
    string GetString(string key);
}

