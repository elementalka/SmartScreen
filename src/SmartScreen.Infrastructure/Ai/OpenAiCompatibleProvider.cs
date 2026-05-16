using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SmartScreen.Application.Abstractions;
using SmartScreen.Domain.Models;

namespace SmartScreen.Infrastructure.Ai;

public sealed class OpenAiCompatibleProvider(HttpClient httpClient) : IAiProvider
{
    public string Name => "OpenAI-compatible";

    public async Task<AiResponse> AnalyzeImageAsync(
        AiRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var settings = request.ProviderSettings;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(settings.Endpoint));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        httpRequest.Content = JsonContent.Create(BuildBody(request));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            return AiResponse.Fail($"AI-провайдер повернув помилку: {(int)response.StatusCode}.", stopwatch.Elapsed);
        }

        var text = ExtractText(body);
        return string.IsNullOrWhiteSpace(text)
            ? AiResponse.Fail("AI-провайдер не повернув текстову відповідь.", stopwatch.Elapsed)
            : AiResponse.Ok(text, stopwatch.Elapsed);
    }

    public async Task<bool> TestConnectionAsync(
        AiProviderSettings settings,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(settings.Endpoint));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = settings.Model,
            messages = new[]
            {
                new { role = "user", content = "Reply with OK." }
            },
            max_tokens = 8
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private static string BuildEndpoint(string endpoint)
    {
        var trimmed = endpoint.TrimEnd('/');
        return trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/chat/completions";
    }

    private static object BuildBody(AiRequest request)
    {
        var imageUrl = $"data:{request.ImageMimeType};base64,{Convert.ToBase64String(request.ImageBytes)}";
        var messages = new List<object>();

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new { role = "system", content = request.SystemPrompt });
        }

        messages.Add(new
        {
            role = "user",
            content = new object[]
            {
                new { type = "text", text = request.UserPrompt },
                new
                {
                    type = "image_url",
                    image_url = new { url = imageUrl }
                }
            }
        });

        return new
        {
            model = request.ProviderSettings.Model,
            messages,
            max_tokens = 2048
        };
    }

    private static string ExtractText(string json)
    {
        using var document = JsonDocument.Parse(json);
        var message = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message");

        if (!message.TryGetProperty("content", out var content))
        {
            return string.Empty;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        if (content.ValueKind == JsonValueKind.Array)
        {
            var texts = content.EnumerateArray()
                .Where(item => item.TryGetProperty("text", out _))
                .Select(item => item.GetProperty("text").GetString())
                .Where(text => !string.IsNullOrWhiteSpace(text));

            return string.Join(Environment.NewLine, texts);
        }

        return string.Empty;
    }
}

