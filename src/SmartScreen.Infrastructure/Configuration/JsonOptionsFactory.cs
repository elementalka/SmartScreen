using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartScreen.Infrastructure.Configuration;

internal static class JsonOptionsFactory
{
    public static JsonSerializerOptions Create() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };
}

