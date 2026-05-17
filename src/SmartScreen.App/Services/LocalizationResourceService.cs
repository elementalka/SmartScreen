using System.Windows;
using SmartScreen.Application.Abstractions;

namespace SmartScreen.App.Services;

public static class LocalizationResourceService
{
    private const string ResourcePrefix = "Loc.";

    private static readonly IReadOnlyDictionary<string, string> BuiltInFallback = new Dictionary<string, string>
    {
        ["app.title"] = "SmartScreen",
        ["app.subtitle"] = "центр захоплень",
        ["main.captureSection"] = "Захоплення",
        ["main.region"] = "Область",
        ["main.fullScreen"] = "Весь екран",
        ["main.activeWindow"] = "Активне вікно",
        ["main.monitor"] = "Монітор",
        ["main.delay"] = "Затримка",
        ["main.askAi"] = "Запитати AI",
        ["main.settings"] = "Налаштування",
        ["main.primaryHotkey"] = "Основний хоткей",
        ["main.workspace"] = "Робочий центр",
        ["main.inTray"] = "у треї",
        ["main.openScreenshotsFolder"] = "Відкрити папку скріншотів",
        ["main.currentScreenshot"] = "Поточний скріншот",
        ["main.waitingCapture"] = "Очікує захоплення",
        ["main.noScreenshot"] = "Немає скріншоту",
        ["main.captureRegionTooltip"] = "Скріншот області",
        ["main.captureActiveWindowTooltip"] = "Скріншот активного вікна",
        ["main.captureMonitorTooltip"] = "Скріншот монітора",
        ["main.captureDelayedTooltip"] = "Скріншот із затримкою",
        ["main.askCurrentScreenshotTooltip"] = "Запитати AI про поточний скріншот",
        ["main.window"] = "Вікно",
        ["main.timer"] = "Таймер",
        ["main.recentScreenshots"] = "Останні скріншоти",
        ["main.sessionSuffix"] = "у сесії",
        ["main.emptySession"] = "Історія сесії порожня",
        ["main.emptySessionHint"] = "Після захоплення тут з'явиться прев'ю.",
        ["main.activeAiRoute"] = "Активний AI-маршрут",
        ["main.userActionOnly"] = "лише за дією користувача",
        ["main.workflow"] = "Сценарій",
        ["main.workflowStepCapture"] = "1. Захоплення",
        ["main.workflowStepOutput"] = "2. Буфер або файл",
        ["main.workflowStepTools"] = "3. Редактор, AI, папка",
        ["settings.title"] = "Налаштування",
        ["settings.subtitle"] = "Керування скріншотами, AI-маршрутами, prompt-шаблонами та глобальними клавішами",
        ["settings.save"] = "Зберегти",
        ["settings.sections"] = "Розділи",
        ["settings.sectionsHint"] = "Усе важливе без ручного JSON",
        ["settings.section.general.title"] = "Загальні",
        ["settings.section.general.description"] = "запуск, portable, папки",
        ["settings.section.screenshots.title"] = "Скріншоти",
        ["settings.section.screenshots.description"] = "формат, файл, pipeline",
        ["settings.section.editor.title"] = "Редактор",
        ["settings.section.editor.description"] = "колір, товщина, текст",
        ["settings.section.ai.title"] = "AI-провайдери",
        ["settings.section.ai.description"] = "маршрути, ключі, моделі",
        ["settings.section.prompts.title"] = "Prompt-шаблони",
        ["settings.section.prompts.description"] = "дії для AI-аналізу",
        ["settings.section.hotkeys.title"] = "Гарячі клавіші",
        ["settings.section.hotkeys.description"] = "глобальні комбінації",
        ["settings.section.interface.title"] = "Інтерфейс",
        ["settings.section.interface.description"] = "тема та акцент",
        ["settings.section.languages.title"] = "Мови",
        ["settings.section.languages.description"] = "локалізація UI",
        ["settings.section.security.title"] = "Безпека",
        ["settings.section.security.description"] = "приватність і секрети",
        ["settings.section.logs.title"] = "Логи",
        ["settings.section.logs.description"] = "діагностика і конфіги",
        ["settings.promptEditing"] = "Редагування prompt",
        ["settings.logsAndFiles"] = "Логи та файли",
        ["firstRun.title"] = "Перший запуск SmartScreen",
        ["firstRun.status"] = "Швидке налаштування SmartScreen",
        ["firstRun.behaviorTitle"] = "Базова поведінка",
        ["firstRun.behaviorDescription"] = "Програма працює з трею, а основне вікно відкривається для налаштувань.",
        ["firstRun.language"] = "Мова",
        ["firstRun.theme"] = "Тема",
        ["firstRun.screenshotsFolder"] = "Папка скріншотів",
        ["firstRun.afterCaptureTitle"] = "Після скріншоту",
        ["firstRun.afterCaptureDescription"] = "Це можна змінити пізніше в налаштуваннях.",
        ["firstRun.copyClipboard"] = "Копіювати в буфер",
        ["firstRun.showQuickMenu"] = "Показувати швидке меню",
        ["firstRun.openEditor"] = "Одразу відкривати редактор",
        ["firstRun.aiRouteTitle"] = "AI-маршрут",
        ["firstRun.aiRouteDescription"] = "Ключ можна додати після майстра в AI settings.",
        ["firstRun.hotkeys"] = "Default hotkeys: Ctrl+Shift+S, Ctrl+Shift+F, Ctrl+Shift+W, Ctrl+Shift+A",
        ["firstRun.start"] = "Почати",
        ["status.ready"] = "Готово до роботи"
    };

    public static async Task ApplyAsync(
        ILocalizationService localizationService,
        string cultureName,
        CancellationToken cancellationToken = default)
    {
        await localizationService.LoadAsync(cultureName, cancellationToken);
        Apply(localizationService.CurrentStrings);
    }

    public static void Apply(IReadOnlyDictionary<string, string> localizedStrings)
    {
        if (System.Windows.Application.Current?.Resources is null)
        {
            return;
        }

        foreach (var pair in BuiltInFallback)
        {
            System.Windows.Application.Current.Resources[ToResourceKey(pair.Key)] = pair.Value;
        }

        foreach (var pair in localizedStrings)
        {
            System.Windows.Application.Current.Resources[ToResourceKey(pair.Key)] = pair.Value;
        }
    }

    public static string ToResourceKey(string key) => $"{ResourcePrefix}{key}";
}
