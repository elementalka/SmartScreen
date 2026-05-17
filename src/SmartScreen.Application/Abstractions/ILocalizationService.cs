namespace SmartScreen.Application.Abstractions;

public interface ILocalizationService
{
    IReadOnlyDictionary<string, string> CurrentStrings { get; }
    Task LoadAsync(string cultureName, CancellationToken cancellationToken = default);
    string GetString(string key);
}
