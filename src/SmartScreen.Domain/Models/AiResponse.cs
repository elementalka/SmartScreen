namespace SmartScreen.Domain.Models;

public sealed class AiResponse
{
    public bool Success { get; init; }
    public string? Text { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan Duration { get; init; }

    public static AiResponse Ok(string text, TimeSpan duration) =>
        new() { Success = true, Text = text, Duration = duration };

    public static AiResponse Fail(string errorMessage, TimeSpan duration) =>
        new() { Success = false, ErrorMessage = errorMessage, Duration = duration };
}

