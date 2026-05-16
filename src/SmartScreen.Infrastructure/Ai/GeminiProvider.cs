using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using SmartScreen.Application.Abstractions;
using SmartScreen.Domain.Models;

namespace SmartScreen.Infrastructure.Ai;

public sealed class GeminiProvider(HttpClient httpClient) : IAiProvider
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
        httpRequest.Content = JsonContent.Create(BuildBody(request));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            return AiResponse.Fail(
                AiProviderErrorFormatter.Format("Gemini", response.StatusCode, body),
                stopwatch.Elapsed);
        }

        var text = ExtractText(body);
        return string.IsNullOrWhiteSpace(text)
            ? AiResponse.Fail("Gemini не повернув текстову відповідь.", stopwatch.Elapsed)
            : AiResponse.Ok(text, stopwatch.Elapsed);
    }

    public async Task<bool> TestConnectionAsync(
        AiProviderSettings settings,
        CancellationToken cancellationToken = default)
    {
        var endpoint = BuildEndpoint(settings);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("x-goog-api-key", settings.ApiKey);
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

        return body;
    }

    private static string ExtractText(string json)
    {
        using var document = JsonDocument.Parse(json);
        var parts = document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts");

        var texts = parts.EnumerateArray()
            .Where(part => part.TryGetProperty("text", out _))
            .Select(part => part.GetProperty("text").GetString())
            .Where(text => !string.IsNullOrWhiteSpace(text));

        return string.Join(Environment.NewLine, texts);
    }
}
