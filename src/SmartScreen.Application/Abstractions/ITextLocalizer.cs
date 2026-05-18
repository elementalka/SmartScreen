namespace SmartScreen.Application.Abstractions;

public interface ITextLocalizer
{
    string GetString(string key, string fallback);
    string Format(string key, string fallback, params object[] args);
}
