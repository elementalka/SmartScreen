using System.Globalization;
using SmartScreen.Application.Abstractions;

namespace SmartScreen.App.Services;

public sealed class WpfTextLocalizer : ITextLocalizer
{
    public string GetString(string key, string fallback) =>
        LocalizationResourceService.GetString(key, fallback);

    public string Format(string key, string fallback, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, GetString(key, fallback), args);
}
