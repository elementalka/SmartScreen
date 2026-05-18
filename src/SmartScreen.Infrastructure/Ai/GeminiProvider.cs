using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SmartScreen.Application.Abstractions;
using SmartScreen.Domain.Models;

namespace SmartScreen.Infrastructure.Ai;

public sealed class GeminiProvider(HttpClient httpClient, ITextLocalizer textLocalizer) : IAiProvider
{
    public string Name => "Google Gemini";

    public async Task<AiResponse> AnalyzeImageAsync(
        AiRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var settings = request.ProviderSettings;
        var endpoint = BuildEndpoint(settings);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Add("x-goog-api-key", settings.ApiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Content = JsonContent.Create(BuildBody(request));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            return AiResponse.Fail(
                AiProviderErrorFormatter.Format("Gemini", response.StatusCode, body, textLocalizer),
                stopwatch.Elapsed);
        }

        var text = ExtractText(body);
        if (!string.IsNullOrWhiteSpace(text))
        {
            return AiResponse.Ok(text, stopwatch.Elapsed);
        }

        var failureReason = ExtractFailureReason(body);
        return AiResponse.Fail(
            string.IsNullOrWhiteSpace(failureReason)
                ? Text("ai.error.geminiNoText", "Gemini не повернув текстову відповідь.")
                : failureReason,
            stopwatch.Elapsed);
    }

    public async Task<bool> TestConnectionAsync(
        AiProviderSettings settings,
        CancellationToken cancellationToken = default)
    {
        var endpoint = BuildEndpoint(settings);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("x-goog-api-key", settings.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = JsonContent.Create(new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = "Reply with OK." }
                    }
                }
            }
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private static string BuildEndpoint(AiProviderSettings settings)
    {
        var baseEndpoint = string.IsNullOrWhiteSpace(settings.Endpoint)
            ? "https://generativelanguage.googleapis.com/v1beta"
            : settings.Endpoint.TrimEnd('/');

        if (baseEndpoint.Contains(":generateContent", StringComparison.OrdinalIgnoreCase))
        {
            return baseEndpoint;
        }

        return $"{baseEndpoint}/models/{settings.Model}:generateContent";
    }

    private static object BuildBody(AiRequest request)
    {
        var parts = new List<object>
        {
            new { text = request.UserPrompt },
            new
            {
                inline_data = new
                {
                    mime_type = request.ImageMimeType,
                    data = Convert.ToBase64String(request.ImageBytes)
                }
            }
        };

        var body = new Dictionary<string, object?>
        {
            ["contents"] = new[]
            {
                new
                {
                    role = "user",
                    parts
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            body["system_instruction"] = new
            {
                parts = new[]
                {
                    new { text = request.SystemPrompt }
                }
            };
        }

        body["generationConfig"] = new
        {
            temperature = 0.2,
            maxOutputTokens = 2048
        };

        return body;
    }

    private static string ExtractText(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates) ||
                candidates.ValueKind != JsonValueKind.Array ||
                candidates.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            var candidate = candidates[0];
            if (!candidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            var texts = parts.EnumerateArray()
                .Where(part => part.TryGetProperty("text", out _))
                .Select(part => part.GetProperty("text").GetString())
                .Where(text => !string.IsNullOrWhiteSpace(text));

            return string.Join(Environment.NewLine, texts);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private string ExtractFailureReason(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.TryGetProperty("promptFeedback", out var feedback))
            {
                return ExtractPromptFeedback(feedback);
            }

            if (root.TryGetProperty("candidates", out var candidates) &&
                candidates.ValueKind == JsonValueKind.Array &&
                candidates.GetArrayLength() > 0 &&
                candidates[0].TryGetProperty("finishReason", out var finishReason))
            {
                return FormatText(
                    "ai.error.geminiFinishNoText",
                    "Gemini завершив відповідь без тексту: {0}.",
                    finishReason.GetString() ?? string.Empty);
            }
        }
        catch (JsonException)
        {
        }

        return string.Empty;
    }

    private string ExtractPromptFeedback(JsonElement feedback)
    {
        return feedback.TryGetProperty("blockReason", out var blockReason)
            ? textLocalizer.Format(
                "ai.error.geminiBlocked",
                "Gemini заблокував запит: {0}.",
                blockReason.GetString() ?? string.Empty)
            : string.Empty;
    }

    private string Text(string key, string fallback) =>
        textLocalizer.GetString(key, fallback);

    private string FormatText(string key, string fallback, params object[] args) =>
        textLocalizer.Format(key, fallback, args);
}
