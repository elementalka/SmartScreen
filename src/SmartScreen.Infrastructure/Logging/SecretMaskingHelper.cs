using System.Text.RegularExpressions;

namespace SmartScreen.Infrastructure.Logging;

internal static partial class SecretMaskingHelper
{
    public static string Mask(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var masked = ApiKeyAssignmentRegex().Replace(value, "$1***");
        masked = BearerRegex().Replace(masked, "Bearer ***");
        masked = GeminiKeyRegex().Replace(masked, "AIza***");
        return masked;
    }

    [GeneratedRegex("(apiKey|api_key|key|token|authorization)\\s*[:=]\\s*['\\\"]?([^'\\\"\\s,}]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ApiKeyAssignmentRegex();

    [GeneratedRegex("Bearer\\s+[A-Za-z0-9._\\-]+", RegexOptions.IgnoreCase)]
    private static partial Regex BearerRegex();

    [GeneratedRegex("AIza[0-9A-Za-z_\\-]{20,}")]
    private static partial Regex GeminiKeyRegex();
}

