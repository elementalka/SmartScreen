# SmartScreen

SmartScreen is a portable Windows desktop application for screenshots, quick editing, clipboard workflows, saving PNG/JPG files, and explicit AI analysis through configurable providers.

## Requirements

- Windows 10 or Windows 11
- .NET 9 Desktop Runtime/SDK for development

## Project Structure

```text
SmartScreen/
├── docs/
├── src/
│   ├── SmartScreen.Domain/
│   ├── SmartScreen.Application/
│   ├── SmartScreen.Infrastructure/
│   └── SmartScreen.App/
├── tests/
│   └── SmartScreen.Tests/
├── config/
├── localization/
├── themes/
├── screenshots/
└── logs/
```

## Run

```powershell
dotnet build
dotnet run --project src/SmartScreen.App/SmartScreen.App.csproj
```

## Current Capabilities

- Capture full screen
- Capture selected region
- Capture active window
- Copy screenshot to clipboard
- Save PNG/JPG
- Quick actions popup
- Basic screenshot editor with pen/highlighter/undo
- Gemini provider
- OpenAI-compatible provider for NVIDIA NIM, OpenRouter, local endpoints, and similar APIs
- Local JSON settings
- Safe logging without API keys

AI requests are sent only after an explicit user action.

## AI Setup

Open settings and fill in an API key for one of the configured providers:

- Google Gemini: `https://generativelanguage.googleapis.com/v1beta`
- NVIDIA NIM: `https://integrate.api.nvidia.com/v1/chat/completions`
- OpenRouter or custom OpenAI-compatible endpoints can be added later through the same provider shape.

The app still works as a normal screenshot tool without an API key.

## Portable Mode

Configuration, logs, localization files, themes, and screenshots live next to the executable. If the executable folder is not writable, screenshots fall back to:

```text
%LOCALAPPDATA%/SmartScreen/screenshots
```

## Documentation

Course documents are stored in `docs/`:

- `ТЗ.md`
- `Інструкція для Codex.md`
- `План звіту.md`

