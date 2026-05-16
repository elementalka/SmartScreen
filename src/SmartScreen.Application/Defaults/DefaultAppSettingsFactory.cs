using SmartScreen.Domain.Enums;
using SmartScreen.Domain.Models;

namespace SmartScreen.Application.Defaults;

public static class DefaultAppSettingsFactory
{
    public static AppSettings Create() => new()
    {
        FirstRunCompleted = false,
        StartMinimizedToTray = true,
        MinimizeToTrayOnClose = true,
        Language = "uk-UA",
        Screenshots = new ScreenshotSettings
        {
            DefaultFormat = ScreenshotImageFormat.Png,
            JpegQuality = 90,
            FileNameTemplate = "screenshot_{yyyy-MM-dd}_{HH-mm-ss}",
            DefaultMode = ScreenshotMode.Region,
            CopyToClipboardAutomatically = true,
            ShowQuickActionsAfterCapture = true,
            SaveDirectory = "screenshots"
        },
        Editor = new EditorSettings(),
        Theme = new ThemeSettings
        {
            Mode = ThemeMode.System,
            AccentColor = "#2F7DFF"
        },
        Ai = new AiSettings
        {
            ActiveProviderId = "gemini-pro",
            SendScreenshotsOnlyAfterConfirmation = true,
            Providers =
            [
                new AiProviderSettings
                {
                    Id = "gemini-pro",
                    DisplayName = "Google Gemini Pro",
                    Kind = AiProviderKind.Gemini,
                    Endpoint = "https://generativelanguage.googleapis.com/v1beta",
                    Model = "gemini-3-pro-preview",
                    TimeoutSeconds = 90
                },
                new AiProviderSettings
                {
                    Id = "gemini-flash",
                    DisplayName = "Google Gemini Flash",
                    Kind = AiProviderKind.Gemini,
                    Endpoint = "https://generativelanguage.googleapis.com/v1beta",
                    Model = "gemini-3-flash-preview",
                    TimeoutSeconds = 60
                },
                new AiProviderSettings
                {
                    Id = "nvidia",
                    DisplayName = "NVIDIA NIM Vision",
                    Kind = AiProviderKind.OpenAiCompatible,
                    Endpoint = "https://integrate.api.nvidia.com/v1/chat/completions",
                    Model = "meta/llama-3.2-90b-vision-instruct",
                    TimeoutSeconds = 60
                },
                new AiProviderSettings
                {
                    Id = "nvidia-nano-vl",
                    DisplayName = "NVIDIA Nemotron Nano VL",
                    Kind = AiProviderKind.OpenAiCompatible,
                    Endpoint = "https://integrate.api.nvidia.com/v1/chat/completions",
                    Model = "nvidia/llama-3.1-nemotron-nano-vl-8b-v1",
                    TimeoutSeconds = 60
                },
                new AiProviderSettings
                {
                    Id = "openrouter",
                    DisplayName = "OpenRouter",
                    Kind = AiProviderKind.OpenAiCompatible,
                    Endpoint = "https://openrouter.ai/api/v1/chat/completions",
                    Model = "google/gemini-2.5-flash",
                    TimeoutSeconds = 60
                },
                new AiProviderSettings
                {
                    Id = "custom-openai-compatible",
                    DisplayName = "Custom OpenAI-compatible",
                    Kind = AiProviderKind.OpenAiCompatible,
                    Endpoint = "http://localhost:1234/v1/chat/completions",
                    Model = "local-vision-model",
                    TimeoutSeconds = 60,
                    IsEnabled = false
                }
            ]
        }
    };
}
