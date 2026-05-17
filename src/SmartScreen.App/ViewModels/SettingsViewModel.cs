using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using SmartScreen.Application.Abstractions;
using SmartScreen.Application.Defaults;
using SmartScreen.App.Commands;
using SmartScreen.App.Services;
using SmartScreen.Domain.Enums;
using SmartScreen.Domain.Models;
using Forms = System.Windows.Forms;
using Visibility = System.Windows.Visibility;

namespace SmartScreen.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IHotkeySettingsService _hotkeySettingsService;
    private readonly IHotkeyService _hotkeyService;
    private readonly IStorageService _storageService;
    private readonly IAiService _aiService;
    private readonly IAiSecretService _aiSecretService;
    private readonly IPromptTemplateService _promptTemplateService;
    private readonly ILoggingService _loggingService;
    private AppSettings? _settings;
    private SettingsSectionViewModel? _selectedSection;
    private AiProviderSettings? _selectedProvider;
    private AiPromptCategory? _selectedPromptCategory;
    private AiPromptTemplate? _selectedPromptTemplate;
    private string _apiKeyInput = string.Empty;
    private string _status = "Налаштування";
    private bool _copyAfterCapture;
    private bool _saveAfterCapture;
    private bool _quickActionsAfterCapture;
    private bool _openEditorAfterCapture;
    private bool _askAiAfterCapture;

    public SettingsViewModel(
        ISettingsService settingsService,
        IHotkeySettingsService hotkeySettingsService,
        IHotkeyService hotkeyService,
        IStorageService storageService,
        IAiService aiService,
        IAiSecretService aiSecretService,
        IPromptTemplateService promptTemplateService,
        ILoggingService loggingService)
    {
        _settingsService = settingsService;
        _hotkeySettingsService = hotkeySettingsService;
        _hotkeyService = hotkeyService;
        _storageService = storageService;
        _aiService = aiService;
        _aiSecretService = aiSecretService;
        _promptTemplateService = promptTemplateService;
        _loggingService = loggingService;

        Sections =
        [
            new("general", "Загальні", "запуск, portable, папки"),
            new("screenshots", "Скріншоти", "формат, файл, pipeline"),
            new("editor", "Редактор", "колір, товщина, текст"),
            new("ai", "AI-провайдери", "маршрути, ключі, моделі"),
            new("prompts", "Prompt-шаблони", "дії для AI-аналізу"),
            new("hotkeys", "Гарячі клавіші", "глобальні комбінації"),
            new("interface", "Інтерфейс", "тема та акцент"),
            new("languages", "Мови", "локалізація UI"),
            new("security", "Безпека", "приватність і секрети"),
            new("logs", "Логи", "діагностика і конфіги")
        ];

        ScreenshotModeOptions =
        [
            new(ScreenshotMode.Region, "Область"),
            new(ScreenshotMode.FullScreen, "Весь екран"),
            new(ScreenshotMode.ActiveWindow, "Активне вікно"),
            new(ScreenshotMode.Monitor, "Монітор"),
            new(ScreenshotMode.Delayed, "Із затримкою")
        ];

        ImageFormatOptions =
        [
            new(ScreenshotImageFormat.Png, "PNG"),
            new(ScreenshotImageFormat.Jpeg, "JPG")
        ];

        AiProviderKindOptions =
        [
            new(AiProviderKind.Gemini, "Gemini"),
            new(AiProviderKind.OpenAiCompatible, "OpenAI-compatible"),
            new(AiProviderKind.OpenRouter, "OpenRouter"),
            new(AiProviderKind.Nvidia, "NVIDIA"),
            new(AiProviderKind.OpenAi, "OpenAI"),
            new(AiProviderKind.Claude, "Claude"),
            new(AiProviderKind.Custom, "Custom")
        ];

        ThemeModeOptions =
        [
            new(ThemeMode.System, "Системна"),
            new(ThemeMode.Light, "Світла"),
            new(ThemeMode.Dark, "Темна")
        ];

        LanguageOptions =
        [
            new("uk-UA", "Українська"),
            new("en-US", "English")
        ];

        SelectedSection = Sections.First();

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        TestAiCommand = new AsyncRelayCommand(TestAiAsync);
        AddProviderCommand = new RelayCommand(AddProvider);
        DeleteProviderCommand = new RelayCommand(DeleteProvider, () => Providers.Count > 1 && SelectedProvider is not null);
        RestoreDefaultHotkeysCommand = new RelayCommand(RestoreDefaultHotkeys);
        ValidateHotkeysCommand = new RelayCommand(ValidateHotkeys);
        ApplyQuickCopyWorkflowCommand = new RelayCommand(() => ApplyWorkflowPreset(
            copy: true,
            save: false,
            quickActions: false,
            openEditor: false,
            askAi: false,
            status: "Сценарій: копіювати скріншот у буфер"));
        ApplyEditWorkflowCommand = new RelayCommand(() => ApplyWorkflowPreset(
            copy: false,
            save: true,
            quickActions: true,
            openEditor: true,
            askAi: false,
            status: "Сценарій: відкрити редактор після скріншоту"));
        ApplyAiWorkflowCommand = new RelayCommand(() => ApplyWorkflowPreset(
            copy: false,
            save: true,
            quickActions: true,
            openEditor: false,
            askAi: true,
            status: "Сценарій: зберегти та запитати AI"));
        ApplySilentSaveWorkflowCommand = new RelayCommand(() => ApplyWorkflowPreset(
            copy: false,
            save: true,
            quickActions: false,
            openEditor: false,
            askAi: false,
            status: "Сценарій: тихе збереження у файл"));
        AddPromptCommand = new RelayCommand(AddPrompt);
        DeletePromptCommand = new RelayCommand(DeletePrompt, () => SelectedPromptTemplate is not null);
        AddPromptCategoryCommand = new RelayCommand(AddPromptCategory);
        DeletePromptCategoryCommand = new RelayCommand(DeletePromptCategory, () => SelectedPromptCategory is { IsSystem: false });
        ResetPromptsCommand = new AsyncRelayCommand(ResetPromptsAsync);
        OpenConfigFolderCommand = new RelayCommand(() => OpenFolder(ConfigDirectory));
        OpenLogsFolderCommand = new RelayCommand(() => OpenFolder(LogDirectory));
        OpenScreenshotsFolderCommand = new RelayCommand(() => OpenFolder(ScreenshotsDirectory));
    }

    public ObservableCollection<SettingsSectionViewModel> Sections { get; }
    public ObservableCollection<AiProviderSettings> Providers { get; } = [];
    public ObservableCollection<HotkeyBindingViewModel> Hotkeys { get; } = [];
    public ObservableCollection<string> ValidationMessages { get; } = [];
    public ObservableCollection<AiPromptCategory> PromptCategories { get; } = [];
    public ObservableCollection<AiPromptTemplate> PromptTemplates { get; } = [];
    public ObservableCollection<Option<int>> MonitorOptions { get; } = [];
    public IReadOnlyList<Option<ScreenshotMode>> ScreenshotModeOptions { get; }
    public IReadOnlyList<Option<ScreenshotImageFormat>> ImageFormatOptions { get; }
    public IReadOnlyList<Option<AiProviderKind>> AiProviderKindOptions { get; }
    public IReadOnlyList<Option<ThemeMode>> ThemeModeOptions { get; }
    public IReadOnlyList<Option<string>> LanguageOptions { get; }

    public SettingsSectionViewModel? SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value))
            {
                NotifySectionVisibilityChanged();
            }
        }
    }

    public AppSettings? Settings
    {
        get => _settings;
        private set => SetProperty(ref _settings, value);
    }

    public AiProviderSettings? SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (SetProperty(ref _selectedProvider, value))
            {
                ApiKeyInput = value?.ApiKey ?? string.Empty;
                OnPropertyChanged(nameof(SelectedProviderEnvironmentVariable));
                OnPropertyChanged(nameof(SelectedProviderHasKey));
                if (DeleteProviderCommand is RelayCommand command)
                {
                    command.RaiseCanExecuteChanged();
                }
            }
        }
    }

    public AiPromptCategory? SelectedPromptCategory
    {
        get => _selectedPromptCategory;
        set
        {
            if (SetProperty(ref _selectedPromptCategory, value) &&
                DeletePromptCategoryCommand is RelayCommand command)
            {
                command.RaiseCanExecuteChanged();
            }
        }
    }

    public AiPromptTemplate? SelectedPromptTemplate
    {
        get => _selectedPromptTemplate;
        set
        {
            if (SetProperty(ref _selectedPromptTemplate, value))
            {
                if (value is not null)
                {
                    SelectedPromptCategory = PromptCategories.FirstOrDefault(category => category.Id == value.CategoryId)
                        ?? SelectedPromptCategory;
                }

                if (DeletePromptCommand is RelayCommand command)
                {
                    command.RaiseCanExecuteChanged();
                }
            }
        }
    }

    public string ApiKeyInput
    {
        get => _apiKeyInput;
        set
        {
            if (SetProperty(ref _apiKeyInput, value))
            {
                if (SelectedProvider is not null)
                {
                    SelectedProvider.ApiKey = value;
                }

                OnPropertyChanged(nameof(SelectedProviderHasKey));
            }
        }
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string ConfigDirectory => _storageService.Paths.ConfigDirectory;
    public string LogDirectory => _storageService.Paths.LogsDirectory;
    public string ScreenshotsDirectory => _storageService.Paths.ScreenshotsDirectory;
    public string LocalizationDirectory => _storageService.Paths.LocalizationDirectory;
    public string ThemesDirectory => _storageService.Paths.ThemesDirectory;
    public string SecretsFilePath => Path.Combine(ConfigDirectory, "secrets.local.json");

    public string SelectedProviderEnvironmentVariable =>
        SelectedProvider is null ? string.Empty : _aiSecretService.GetEnvironmentVariableName(SelectedProvider.Id);

    public bool SelectedProviderHasKey => !string.IsNullOrWhiteSpace(SelectedProvider?.ApiKey);

    public bool CopyAfterCapture
    {
        get => _copyAfterCapture;
        set => SetProperty(ref _copyAfterCapture, value);
    }

    public bool SaveAfterCapture
    {
        get => _saveAfterCapture;
        set => SetProperty(ref _saveAfterCapture, value);
    }

    public bool QuickActionsAfterCapture
    {
        get => _quickActionsAfterCapture;
        set => SetProperty(ref _quickActionsAfterCapture, value);
    }

    public bool OpenEditorAfterCapture
    {
        get => _openEditorAfterCapture;
        set => SetProperty(ref _openEditorAfterCapture, value);
    }

    public bool AskAiAfterCapture
    {
        get => _askAiAfterCapture;
        set => SetProperty(ref _askAiAfterCapture, value);
    }

    public int PromptTemplateCount => PromptTemplates.Count;
    public int EnabledHotkeyCount => Hotkeys.Count(hotkey => hotkey.IsEnabled);
    public Visibility ValidationMessagesVisibility => ValidationMessages.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility GeneralSectionVisibility => SectionVisibility("general");
    public Visibility ScreenshotsSectionVisibility => SectionVisibility("screenshots");
    public Visibility EditorSectionVisibility => SectionVisibility("editor");
    public Visibility AiSectionVisibility => SectionVisibility("ai");
    public Visibility PromptsSectionVisibility => SectionVisibility("prompts");
    public Visibility HotkeysSectionVisibility => SectionVisibility("hotkeys");
    public Visibility InterfaceSectionVisibility => SectionVisibility("interface");
    public Visibility LanguagesSectionVisibility => SectionVisibility("languages");
    public Visibility SecuritySectionVisibility => SectionVisibility("security");
    public Visibility LogsSectionVisibility => SectionVisibility("logs");

    public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand TestAiCommand { get; }
    public ICommand AddProviderCommand { get; }
    public ICommand DeleteProviderCommand { get; }
    public ICommand RestoreDefaultHotkeysCommand { get; }
    public ICommand ValidateHotkeysCommand { get; }
    public ICommand ApplyQuickCopyWorkflowCommand { get; }
    public ICommand ApplyEditWorkflowCommand { get; }
    public ICommand ApplyAiWorkflowCommand { get; }
    public ICommand ApplySilentSaveWorkflowCommand { get; }
    public ICommand AddPromptCommand { get; }
    public ICommand DeletePromptCommand { get; }
    public ICommand AddPromptCategoryCommand { get; }
    public ICommand DeletePromptCategoryCommand { get; }
    public ICommand ResetPromptsCommand { get; }
    public ICommand OpenConfigFolderCommand { get; }
    public ICommand OpenLogsFolderCommand { get; }
    public ICommand OpenScreenshotsFolderCommand { get; }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Settings = await _settingsService.LoadAsync(cancellationToken);
        RefreshMonitorOptions();
        Providers.Clear();

        foreach (var provider in Settings.Ai.Providers)
        {
            await _aiSecretService.ApplySecretsAsync(provider, cancellationToken);
            Providers.Add(provider);
        }

        SelectedProvider = Providers.FirstOrDefault(provider => provider.Id == Settings.Ai.ActiveProviderId)
            ?? Providers.FirstOrDefault();

        ApplyWorkflowToView(Settings.Screenshots.AfterCaptureActions);
        await LoadHotkeysAsync(cancellationToken);
        await LoadPromptsAsync(cancellationToken);
        Status = "Налаштування завантажено";
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (Settings is null)
        {
            await LoadAsync(cancellationToken);
        }

        if (Settings is null || !TryBuildHotkeySettings(out var hotkeySettings))
        {
            return;
        }

        if (SelectedProvider is not null)
        {
            Settings.Ai.ActiveProviderId = SelectedProvider.Id;
        }

        ApplyWorkflowToSettings(Settings.Screenshots);

        foreach (var provider in Providers)
        {
            if (!string.IsNullOrWhiteSpace(provider.ApiKey))
            {
                await _aiSecretService.SaveApiKeyAsync(provider.Id, provider.ApiKey, cancellationToken);
            }
        }

        Settings.Ai.Providers = Providers
            .Select(provider => new AiProviderSettings
            {
                Id = provider.Id,
                DisplayName = provider.DisplayName,
                Kind = provider.Kind,
                Endpoint = provider.Endpoint,
                ApiKey = string.Empty,
                Model = provider.Model,
                SystemPrompt = provider.SystemPrompt,
                TimeoutSeconds = provider.TimeoutSeconds,
                IsEnabled = provider.IsEnabled
            })
            .ToList();

        await _settingsService.SaveAsync(Settings, cancellationToken);
        ThemeResourceService.Apply(Settings.Theme);
        await _hotkeySettingsService.SaveAsync(hotkeySettings, cancellationToken);
        await _hotkeyService.RegisterAsync(hotkeySettings, cancellationToken);
        await SavePromptsAsync(cancellationToken);

        OnPropertyChanged(nameof(EnabledHotkeyCount));
        Status = "Налаштування збережено, hotkeys перереєстровано";
    }

    private async Task TestAiAsync(CancellationToken cancellationToken)
    {
        await SaveAsync(cancellationToken);
        Status = "Перевіряю підключення...";
        Status = await _aiService.TestActiveProviderAsync(cancellationToken)
            ? "Підключення працює"
            : "Підключення не вдалося перевірити";
    }

    private async Task LoadHotkeysAsync(CancellationToken cancellationToken)
    {
        var hotkeySettings = await _hotkeySettingsService.LoadAsync(cancellationToken);
        Hotkeys.Clear();

        foreach (var binding in hotkeySettings.Bindings.OrderBy(binding => binding.Action))
        {
            Hotkeys.Add(HotkeyBindingViewModel.FromModel(binding));
        }

        OnPropertyChanged(nameof(EnabledHotkeyCount));
        ClearValidationMessages();
    }

    private async Task LoadPromptsAsync(CancellationToken cancellationToken)
    {
        var library = await _promptTemplateService.LoadAsync(cancellationToken);
        PromptCategories.Clear();
        PromptTemplates.Clear();

        foreach (var category in library.Categories.OrderBy(category => category.Order))
        {
            PromptCategories.Add(category);
        }

        foreach (var template in library.Templates.OrderBy(template => template.Order))
        {
            PromptTemplates.Add(template);
        }

        SelectedPromptTemplate = PromptTemplates.FirstOrDefault();
        OnPropertyChanged(nameof(PromptTemplateCount));
        SelectedPromptCategory = PromptCategories.FirstOrDefault(category => category.Id == SelectedPromptTemplate?.CategoryId)
            ?? PromptCategories.FirstOrDefault();
    }

    private async Task SavePromptsAsync(CancellationToken cancellationToken)
    {
        var library = new AiPromptLibrary
        {
            Categories = PromptCategories.ToList(),
            Templates = PromptTemplates.ToList()
        };

        await _promptTemplateService.SaveAsync(library, cancellationToken);
    }

    private void RestoreDefaultHotkeys()
    {
        Hotkeys.Clear();

        foreach (var binding in DefaultHotkeySettingsFactory.Create().Bindings)
        {
            Hotkeys.Add(HotkeyBindingViewModel.FromModel(binding));
        }

        ClearValidationMessages();
        OnPropertyChanged(nameof(EnabledHotkeyCount));
        Status = "Стандартні hotkeys відновлено. Натисни «Зберегти», щоб застосувати";
    }

    private void ValidateHotkeys()
    {
        if (TryBuildHotkeySettings(out _))
        {
            Status = "Hotkeys валідні";
        }
    }

    private void AddPrompt()
    {
        var category = SelectedPromptCategory ?? EnsureCustomPromptCategory();
        var nextOrder = PromptTemplates.Count == 0 ? 0 : PromptTemplates.Max(template => template.Order) + 1;
        var template = new AiPromptTemplate
        {
            CategoryId = category.Id,
            Title = "Новий prompt",
            Prompt = "Опиши, що потрібно зробити зі скріншотом.",
            IsSystem = false,
            Order = nextOrder
        };

        PromptTemplates.Add(template);
        SelectedPromptTemplate = template;
        OnPropertyChanged(nameof(PromptTemplateCount));
        Status = "Prompt додано. Натисни «Зберегти», щоб записати";
    }

    private void AddProvider()
    {
        var nextNumber = Providers.Count(provider =>
            provider.Id.StartsWith("custom-provider", StringComparison.OrdinalIgnoreCase)) + 1;
        var provider = new AiProviderSettings
        {
            Id = $"custom-provider-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            DisplayName = $"Custom provider {nextNumber}",
            Kind = AiProviderKind.OpenAiCompatible,
            Endpoint = "http://localhost:1234/v1/chat/completions",
            Model = "vision-model",
            TimeoutSeconds = 60,
            IsEnabled = true
        };

        Providers.Add(provider);
        SelectedProvider = provider;
        Status = "AI-провайдера додано. Заповни endpoint, model і ключ";
        if (DeleteProviderCommand is RelayCommand command)
        {
            command.RaiseCanExecuteChanged();
        }
    }

    private void DeleteProvider()
    {
        if (SelectedProvider is null || Providers.Count <= 1)
        {
            return;
        }

        var index = Math.Max(0, Providers.IndexOf(SelectedProvider) - 1);
        Providers.Remove(SelectedProvider);
        SelectedProvider = Providers.ElementAtOrDefault(index);
        Status = "AI-провайдера видалено. Натисни «Зберегти», щоб застосувати";
        if (DeleteProviderCommand is RelayCommand command)
        {
            command.RaiseCanExecuteChanged();
        }
    }

    private void AddPromptCategory()
    {
        var nextOrder = PromptCategories.Count == 0 ? 0 : PromptCategories.Max(category => category.Order) + 1;
        var category = new AiPromptCategory
        {
            Id = $"custom-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            Name = "Нова категорія",
            IsSystem = false,
            Order = nextOrder
        };

        PromptCategories.Add(category);
        SelectedPromptCategory = category;
        Status = "Категорію prompt-ів додано. Перейменуй і натисни «Зберегти»";
    }

    private void DeletePromptCategory()
    {
        if (SelectedPromptCategory is null || SelectedPromptCategory.IsSystem)
        {
            return;
        }

        var fallback = PromptCategories.FirstOrDefault(category => category.Id == "custom" && !ReferenceEquals(category, SelectedPromptCategory))
            ?? PromptCategories.FirstOrDefault(category => category.IsSystem)
            ?? EnsureCustomPromptCategory();

        foreach (var template in PromptTemplates.Where(template => template.CategoryId == SelectedPromptCategory.Id))
        {
            template.CategoryId = fallback.Id;
        }

        PromptCategories.Remove(SelectedPromptCategory);
        SelectedPromptCategory = fallback;
        Status = "Категорію видалено, її prompt-и перенесено в іншу категорію";
    }

    private void DeletePrompt()
    {
        if (SelectedPromptTemplate is null)
        {
            return;
        }

        var nextSelectionIndex = Math.Max(0, PromptTemplates.IndexOf(SelectedPromptTemplate) - 1);
        PromptTemplates.Remove(SelectedPromptTemplate);
        SelectedPromptTemplate = PromptTemplates.ElementAtOrDefault(nextSelectionIndex);
        OnPropertyChanged(nameof(PromptTemplateCount));
        Status = "Prompt видалено. Стандартні можна повернути кнопкою «Відновити»";
    }

    private async Task ResetPromptsAsync(CancellationToken cancellationToken)
    {
        await _promptTemplateService.ResetToDefaultsAsync(cancellationToken);
        await LoadPromptsAsync(cancellationToken);
        Status = "Стандартні prompt-шаблони відновлено";
    }

    private AiPromptCategory EnsureCustomPromptCategory()
    {
        var existing = PromptCategories.FirstOrDefault(category => category.Id == "custom");
        if (existing is not null)
        {
            return existing;
        }

        var nextOrder = PromptCategories.Count == 0 ? 0 : PromptCategories.Max(category => category.Order) + 1;
        var category = new AiPromptCategory
        {
            Id = "custom",
            Name = "Користувацькі",
            IsSystem = false,
            Order = nextOrder
        };
        PromptCategories.Add(category);
        return category;
    }

    private void RefreshMonitorOptions()
    {
        MonitorOptions.Clear();
        var screens = Forms.Screen.AllScreens
            .OrderByDescending(screen => screen.Primary)
            .ThenBy(screen => screen.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        for (var index = 0; index < screens.Length; index++)
        {
            var screen = screens[index];
            var primary = screen.Primary ? " · основний" : string.Empty;
            MonitorOptions.Add(new Option<int>(
                index,
                $"{index + 1}: {screen.Bounds.Width}x{screen.Bounds.Height}{primary}"));
        }

        if (MonitorOptions.Count == 0)
        {
            MonitorOptions.Add(new Option<int>(0, "1: основний монітор"));
        }
    }

    private bool TryBuildHotkeySettings(out HotkeySettings hotkeySettings)
    {
        hotkeySettings = new HotkeySettings();
        ClearValidationMessages();
        var seenGestures = new Dictionary<string, HotkeyBindingViewModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var hotkey in Hotkeys)
        {
            var gesture = hotkey.Gesture.Trim();

            if (!hotkey.IsEnabled)
            {
                hotkeySettings.Bindings.Add(hotkey.ToModel(gesture));
                continue;
            }

            if (!HotkeyGestureParser.TryParse(gesture, out var parsed))
            {
                ValidationMessages.Add($"{hotkey.ActionDisplayName}: комбінація має бути на кшталт Ctrl+Shift+S або Ctrl+Alt+F1.");
                continue;
            }

            if (parsed.NormalizedGesture.Equals("PrintScreen", StringComparison.OrdinalIgnoreCase))
            {
                ValidationMessages.Add($"{hotkey.ActionDisplayName}: PrintScreen без модифікаторів не використовуємо, щоб не конфліктувати з Windows.");
                continue;
            }

            if (seenGestures.TryGetValue(parsed.NormalizedGesture, out var conflictingHotkey))
            {
                ValidationMessages.Add($"{hotkey.ActionDisplayName}: комбінація вже використовується для «{conflictingHotkey.ActionDisplayName}».");
                continue;
            }

            hotkey.Gesture = parsed.NormalizedGesture;
            seenGestures[parsed.NormalizedGesture] = hotkey;
            hotkeySettings.Bindings.Add(hotkey.ToModel(parsed.NormalizedGesture));
        }

        OnPropertyChanged(nameof(ValidationMessagesVisibility));
        OnPropertyChanged(nameof(EnabledHotkeyCount));

        if (ValidationMessages.Count == 0)
        {
            return true;
        }

        Status = "Виправ hotkeys перед збереженням";
        return false;
    }

    private void ApplyWorkflowToView(IReadOnlyCollection<AfterCaptureAction> actions)
    {
        CopyAfterCapture = actions.Contains(AfterCaptureAction.CopyImageToClipboard);
        SaveAfterCapture = actions.Contains(AfterCaptureAction.SaveImageToFile);
        QuickActionsAfterCapture = actions.Contains(AfterCaptureAction.ShowQuickActions);
        OpenEditorAfterCapture = actions.Contains(AfterCaptureAction.OpenEditor);
        AskAiAfterCapture = actions.Contains(AfterCaptureAction.AskAi);
    }

    private void ApplyWorkflowPreset(
        bool copy,
        bool save,
        bool quickActions,
        bool openEditor,
        bool askAi,
        string status)
    {
        CopyAfterCapture = copy;
        SaveAfterCapture = save;
        QuickActionsAfterCapture = quickActions;
        OpenEditorAfterCapture = openEditor;
        AskAiAfterCapture = askAi;
        Status = $"{status}. Натисни «Зберегти», щоб застосувати";
    }

    private void ApplyWorkflowToSettings(ScreenshotSettings screenshots)
    {
        var actions = new List<AfterCaptureAction>();

        if (OpenEditorAfterCapture)
        {
            actions.Add(AfterCaptureAction.OpenEditor);
        }

        if (CopyAfterCapture)
        {
            actions.Add(AfterCaptureAction.CopyImageToClipboard);
        }

        if (SaveAfterCapture)
        {
            actions.Add(AfterCaptureAction.SaveImageToFile);
        }

        if (QuickActionsAfterCapture)
        {
            actions.Add(AfterCaptureAction.ShowQuickActions);
        }

        if (AskAiAfterCapture)
        {
            actions.Add(AfterCaptureAction.AskAi);
        }

        screenshots.AfterCaptureActions = actions;
        screenshots.CopyToClipboardAutomatically = actions.Contains(AfterCaptureAction.CopyImageToClipboard);
        screenshots.ShowQuickActionsAfterCapture = actions.Contains(AfterCaptureAction.ShowQuickActions);
    }

    private void ClearValidationMessages()
    {
        ValidationMessages.Clear();
        OnPropertyChanged(nameof(ValidationMessagesVisibility));
    }

    private Visibility SectionVisibility(string sectionKey) =>
        SelectedSection?.Key == sectionKey ? Visibility.Visible : Visibility.Collapsed;

    private void NotifySectionVisibilityChanged()
    {
        OnPropertyChanged(nameof(GeneralSectionVisibility));
        OnPropertyChanged(nameof(ScreenshotsSectionVisibility));
        OnPropertyChanged(nameof(EditorSectionVisibility));
        OnPropertyChanged(nameof(AiSectionVisibility));
        OnPropertyChanged(nameof(PromptsSectionVisibility));
        OnPropertyChanged(nameof(HotkeysSectionVisibility));
        OnPropertyChanged(nameof(InterfaceSectionVisibility));
        OnPropertyChanged(nameof(LanguagesSectionVisibility));
        OnPropertyChanged(nameof(SecuritySectionVisibility));
        OnPropertyChanged(nameof(LogsSectionVisibility));
    }

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            _loggingService.Error(exception, $"Could not open folder: {path}");
            Status = "Не вдалося відкрити папку";
        }
    }
}

public sealed class SettingsSectionViewModel(string key, string title, string description)
{
    public string Key { get; } = key;
    public string Title { get; } = title;
    public string Description { get; } = description;
}

public sealed class HotkeyBindingViewModel : ObservableObject
{
    private string _gesture = string.Empty;
    private bool _isEnabled = true;
    private string? _promptTemplateId;

    private HotkeyBindingViewModel(HotkeyAction action)
    {
        Action = action;
    }

    public HotkeyAction Action { get; }

    public string Gesture
    {
        get => _gesture;
        set => SetProperty(ref _gesture, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public string? PromptTemplateId
    {
        get => _promptTemplateId;
        set => SetProperty(ref _promptTemplateId, value);
    }

    public string ActionDisplayName => Action switch
    {
        HotkeyAction.CaptureDefault => "Скріншот за замовчуванням",
        HotkeyAction.CaptureRegion => "Скріншот області",
        HotkeyAction.CaptureFullScreen => "Весь екран",
        HotkeyAction.CaptureActiveWindow => "Активне вікно",
        HotkeyAction.CaptureMonitor => "Монітор",
        HotkeyAction.CaptureDelayed => "Із затримкою",
        HotkeyAction.AskAiForCurrentScreenshot => "AI для поточного скріншота",
        HotkeyAction.OpenMainWindow => "Відкрити головне вікно",
        HotkeyAction.OpenSettings => "Відкрити налаштування",
        _ => Action.ToString()
    };

    public string Description => Action switch
    {
        HotkeyAction.CaptureDefault => "Запускає режим, вибраний у налаштуваннях скріншотів.",
        HotkeyAction.CaptureRegion => "Основний сценарій: виділення області та quick actions.",
        HotkeyAction.CaptureFullScreen => "Захоплення всіх екранів одним натисканням.",
        HotkeyAction.CaptureActiveWindow => "Знімок активного вікна без ручного виділення.",
        HotkeyAction.CaptureMonitor => "Захоплення монітора, вибраного в налаштуваннях.",
        HotkeyAction.CaptureDelayed => "Таймер перед скріншотом, корисно для меню і hover-станів.",
        HotkeyAction.AskAiForCurrentScreenshot => "AI-запит тільки для вже створеного скріншота.",
        HotkeyAction.OpenMainWindow => "Повернення до dashboard.",
        HotkeyAction.OpenSettings => "Швидкий доступ до конфігурації.",
        _ => string.Empty
    };

    public static HotkeyBindingViewModel FromModel(HotkeyBinding binding) => new(binding.Action)
    {
        Gesture = binding.Gesture,
        IsEnabled = binding.IsEnabled,
        PromptTemplateId = binding.PromptTemplateId
    };

    public HotkeyBinding ToModel(string gesture) => new()
    {
        Action = Action,
        Gesture = gesture,
        IsEnabled = IsEnabled,
        PromptTemplateId = PromptTemplateId
    };
}

public sealed class Option<T>(T value, string label)
{
    public T Value { get; } = value;
    public string Label { get; } = label;
}
