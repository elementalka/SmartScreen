using System.Net;
using System.Text.Json;
using SmartScreen.Infrastructure.Logging;

namespace SmartScreen.Infrastructure.Ai;

internal static class AiProviderErrorFormatter
{
    public static string Format(string providerName, HttpStatusCode statusCode, string responseBody)
    {
        var detail = ExtractErrorDetail(responseBody);
        var status = (int)statusCode;

        return string.IsNullOrWhiteSpace(detail)
            ? $"{providerName} повернув помилку: {status}."
            : $"{providerName} повернув помилку: {status} - {detail}";
    }

    private static string ExtractErrorDetail(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out var error))
            {
                return ExtractFromErrorElement(error);
            }

            if (root.TryGetProperty("message", out var message))
            {
                return Clean(message.GetString());
            }
        }
        catch (JsonException)
        {
            return Clean(responseBody);
        }

        return string.Empty;
    }

    private static string ExtractFromErrorElement(JsonElement error)
    {
        if (error.ValueKind == JsonValueKind.String)
        {
            return Clean(error.GetString());
        }

        if (error.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        var status = error.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString()
            : string.Empty;
        var message = error.TryGetProperty("message", out var messageElement)
            ? messageElement.GetString()
            : string.Empty;

        return string.IsNullOrWhiteSpace(status)
            ? Clean(message)
            : Clean($"{status}: {message}");
    }

    private static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var singleLine = value.ReplaceLineEndings(" ").Trim();
        if (singleLine.Length > 240)
        {
            singleLine = $"{singleLine[..237]}...";
        }

        return SecretMaskingHelper.Mask(singleLine);
    }
}
