# SmartScreen

SmartScreen is a portable Windows desktop application for screenshots, quick editing, clipboard workflows, saving PNG/JPG files, and explicit AI analysis through configurable providers.

## Requirements

- Windows 10 or Windows 11
- .NET 9 Desktop Runtime/SDK for development

## Project Structure

```text
SmartScreen/
|-- docs/
|-- src/
|   |-- SmartScreen.Domain/
|   |-- SmartScreen.Application/
|   |-- SmartScreen.Infrastructure/
|   `-- SmartScreen.App/
|-- tests/
|   `-- SmartScreen.Tests/
|-- config/
|-- localization/
|-- themes/
|-- screenshots/
`-- logs/
```

## Run

```powershell
dotnet build
dotnet run --project src/SmartScreen.App/SmartScreen.App.csproj
```

To open settings immediately:

```powershell
dotnet run --project src/SmartScreen.App/SmartScreen.App.csproj -- --settings
```

## Portable Publish

```powershell
.\scripts\publish-portable.ps1
```

The release build is written to two folders:

- `artifacts/SmartScreen Portable/` - ready-to-run portable application. Start it with `SmartScreen.exe`; the folder also includes `config`, `localization`, `themes`, `logs`, `screenshots`, `release.json`, and `checksums.sha256`.
- `artifacts/SmartScreen Setup/` - install package with `Install SmartScreen.ps1`, `SmartScreen.ico`, setup README, and checksums.

Optional local install:

```powershell
.\scripts\install-portable.ps1
```

The installer copies the portable build to `%LOCALAPPDATA%\Programs\SmartScreen`, creates Start Menu shortcuts, and writes `uninstall.ps1` into the install folder. Use `-CreateDesktopShortcut` when a desktop shortcut is needed, `-CreateStartupShortcut` for Windows startup, or `-NoStartMenuShortcut` for a copy-only install.

From the generated setup package you can also run:

```powershell
powershell -ExecutionPolicy Bypass -File "artifacts\SmartScreen Setup\Install SmartScreen.ps1"
```

## Default Hotkeys

- `Ctrl+Shift+S` - capture selected region
- `Ctrl+Shift+F` - capture full screen
- `Ctrl+Shift+W` - capture active window
- `Ctrl+Shift+A` - ask AI about the current screenshot

`PrintScreen` is not registered by default to avoid conflicts with Windows Snipping Tool. It can be added later through the hotkey settings workflow.

## Current Capabilities

- Capture full screen
- Capture selected region
- Capture active window
- Capture selected monitor
- Delayed screenshot capture
- Copy screenshot to clipboard
- Save PNG/JPG
- Fullscreen capture workspace with quick actions
- Configurable after-capture pipeline
- Built-in screenshot editor with pen, shapes, crop, blur, pixelation, undo/redo
- Built-in AI panel for prompt selection, cancellation, copy, and save
- AI image optimization before provider requests to reduce timeouts and payload errors
- Friendly AI provider errors with safe secret masking
- DPAPI-protected local API key storage
- Runtime theme resources for dark/light/system modes
- Runtime localization resources for the main shell, settings, tray, overlay, and workspace
- First-run wizard
- Global hotkey settings with conflict validation
- AI provider add/edit/delete UI
- Prompt template and category management
- Gemini provider
- OpenAI-compatible provider for NVIDIA NIM, OpenRouter, local endpoints, and similar APIs
- Local JSON settings
- Safe logging without API keys
- Unit, smoke, and workflow-level acceptance tests for configuration, AI image pipeline, localization, WPF resource loading, and after-capture pipelines

AI requests are sent only after an explicit user action.

## AI Setup

Open settings and fill in an API key for one of the configured providers. Keys are saved separately from normal JSON settings.

- Google Gemini Pro: `gemini-3-pro-preview`
- Google Gemini Flash: `gemini-flash-latest`
- NVIDIA NIM Vision: `meta/llama-3.2-90b-vision-instruct`
- NVIDIA Nemotron Nano VL: `nvidia/llama-3.1-nemotron-nano-vl-8b-v1`
- OpenRouter or custom OpenAI-compatible endpoints can be added later through the same provider shape.

The app still works as a normal screenshot tool without an API key.

Provider presets are based on the public Gemini API model list, Gemini image understanding documentation, and NVIDIA NIM OpenAI-compatible chat completion documentation.

API keys are not stored in `appsettings.json`. When entered from the settings window, keys are written to a local ignored file:

```text
config/secrets.local.json
```

You can also set provider-specific environment variables, for example:

```text
SMARTSCREEN_GEMINI_PRO_API_KEY
SMARTSCREEN_NVIDIA_API_KEY
SMARTSCREEN_OPENROUTER_API_KEY
```

## Portable Mode

Configuration, logs, localization files, themes, and screenshots live next to the executable. If the executable folder is not writable, screenshots fall back to:

```text
%LOCALAPPDATA%/SmartScreen/screenshots
```

## Tests

```powershell
dotnet test
```

The test suite covers configuration fallback, prompt templates, hotkey validation, secret masking, AI image optimization, localization completeness, WPF resource smoke tests, and coordinator-level after-capture workflows.

Release acceptance:

```powershell
.\scripts\test-release.ps1
```

This runs the automated tests, builds the portable artifact, verifies checksums, and performs install/uninstall smoke checks in a temporary folder.

## Documentation

Course documents are stored in `docs/`:

- `ТЗ.md`
- `Статус реалізації.md`
- `Приймальне тестування.md`
