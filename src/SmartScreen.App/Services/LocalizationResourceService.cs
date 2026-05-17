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
        ["settings.save"] = "Зберегти",
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
