# Інструкція для Codex
## Реалізація SmartScreen

Цей документ використовується як робочий prompt для поетапної розробки застосунку SmartScreen.

Тема проєкту: **«Розробка системи інтелектуального аналізу екрана для автоматизації контекстних дій за допомогою штучного інтелекту»**.

SmartScreen — portable Windows desktop-застосунок для створення скріншотів, їх швидкого редагування, копіювання, збереження та AI-аналізу вмісту екрана.

---

# 1. Роль Codex

Ти працюєш як senior C#/.NET/WPF інженер.

Твоє завдання:

* створити production-oriented, але зрозумілий для курсової код;
* працювати поетапно;
* не залишати непрацюючий каркас без пояснення;
* після кожного етапу перевіряти `dotnet build`;
* не ховати важливу логіку в code-behind;
* не додавати зайвий enterprise-рівень, якщо він не допомагає курсовому проєкту;
* писати код так, щоб його можна було пояснити на захисті.

---

# 2. Технологічні рішення

Базова реалізація:

| Компонент | Рішення |
| --- | --- |
| Мова | C# |
| UI | WPF |
| Архітектура | MVVM |
| Target Framework | `net9.0-windows` |
| Portable-режим | так |
| Конфіги | JSON |
| Тести | окремий `SmartScreen.Tests` для сервісів |

Примітка: якщо в середовищі встановлено .NET 10 SDK і користувач погоджується, можна перейти на .NET 10 LTS. Якщо ні, залишити .NET 9, бо він уже встановлений локально.

---

# 3. Принципи реалізації

## 3.1. Архітектура

Дотримуйся MVVM:

* `Views` — XAML і мінімальний code-behind;
* `ViewModels` — стан, команди, binding;
* `Models` — дані та доменні типи;
* `Services` — бізнес-логіка;
* `Services/Interfaces` — контракти;
* `Services/Providers` — AI-провайдери;
* `Commands` — `RelayCommand`, `AsyncRelayCommand`;
* `Helpers` — дрібні чисті утиліти;
* `Resources` — стилі, іконки, теми.

## 3.2. Якість

Вимоги до коду:

* зрозумілі назви класів, методів і властивостей;
* async/await для IO та AI-запитів;
* `CancellationToken` для довгих операцій;
* контрольовані повідомлення про помилки;
* централізоване логування;
* жодних API key у коді;
* жодних API key у логах;
* fallback для пошкоджених або відсутніх JSON-файлів;
* обмежений, корисний набір коментарів для складних місць.

## 3.3. UX

Інтерфейс має бути:

* простим для новачка;
* швидким для повторного використання;
* без перевантаження AI-налаштуваннями;
* придатним до роботи без інтернету;
* з людськими повідомленнями про помилки.

Базова логіка: спочатку скріншотер, потім AI.

---

# 4. Очікувана структура solution

```text
SmartScreen/
├── SmartScreen.sln
├── SmartScreen.App/
│   ├── App.xaml
│   ├── App.xaml.cs
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   ├── Views/
│   │   ├── ScreenshotOverlayWindow.xaml
│   │   ├── QuickActionsWindow.xaml
│   │   ├── SettingsWindow.xaml
│   │   └── FirstRunWizardWindow.xaml
│   ├── ViewModels/
│   │   ├── MainViewModel.cs
│   │   ├── ScreenshotOverlayViewModel.cs
│   │   ├── QuickActionsViewModel.cs
│   │   ├── SettingsViewModel.cs
│   │   └── FirstRunWizardViewModel.cs
│   ├── Models/
│   │   ├── AppSettings.cs
│   │   ├── ScreenshotResult.cs
│   │   ├── ScreenshotMode.cs
│   │   ├── AiProviderSettings.cs
│   │   ├── AiPromptTemplate.cs
│   │   ├── AiPromptCategory.cs
│   │   ├── AiRequest.cs
│   │   ├── AiResponse.cs
│   │   ├── HotkeySettings.cs
│   │   └── ThemeSettings.cs
│   ├── Services/
│   │   ├── Interfaces/
│   │   ├── Providers/
│   │   ├── ScreenshotService.cs
│   │   ├── ImageEditorService.cs
│   │   ├── AiService.cs
│   │   ├── SettingsService.cs
│   │   ├── HotkeyService.cs
│   │   ├── LocalizationService.cs
│   │   ├── TrayService.cs
│   │   ├── LoggingService.cs
│   │   └── StorageService.cs
│   ├── Commands/
│   │   ├── RelayCommand.cs
│   │   └── AsyncRelayCommand.cs
│   ├── Helpers/
│   │   ├── ImageHelper.cs
│   │   ├── FileNameHelper.cs
│   │   ├── ScreenHelper.cs
│   │   ├── JsonHelper.cs
│   │   └── SecretMaskingHelper.cs
│   └── Resources/
│       ├── Styles/
│       ├── Icons/
│       └── Themes/
├── SmartScreen.Tests/
├── config/
├── localization/
├── themes/
├── screenshots/
├── logs/
└── README.md
```

---

# 5. Реалізаційний план

## Етап 1. Каркас проєкту

Створи:

* solution;
* WPF-проєкт;
* тестовий проєкт;
* структуру папок;
* базові моделі;
* `RelayCommand` та `AsyncRelayCommand`;
* `SettingsService`;
* `StorageService`;
* `LoggingService`;
* дефолтні JSON-конфіги;
* README зі стартовою інструкцією.

Перевірка:

```powershell
dotnet build
dotnet test
```

Готово, якщо застосунок запускається, створює portable-папки та не падає без конфігів.

## Етап 2. Скріншоти

Реалізуй:

* `IScreenshotService`;
* скріншот всього екрана;
* скріншот області через overlay;
* скріншот активного вікна;
* копіювання в буфер;
* збереження PNG/JPG;
* fallback-папку;
* `QuickActionsWindow` як повноекранний capture workspace з панеллю швидких дій.

Перевірка:

* повний екран створюється;
* виділена область зберігається коректно;
* активне вікно не містить зайвих областей;
* PNG/JPG відкриваються;
* clipboard містить зображення.

## Етап 3. Редактор

Реалізуй:

* редактор без окремого вікна всередині capture workspace;
* інструменти олівець, лінія, стрілка, прямокутник, еліпс, текст, маркер;
* undo/redo;
* обрізання;
* збереження фінальної копії.

Якщо час дозволяє:

* розмиття;
* пікселізація;
* гарячі клавіші редактора.

Готово, якщо користувач може відкрити workspace після скріншота, внести позначки, скасувати дію і зберегти результат без переходу в окреме вікно.

## Етап 4. AI-модуль

Реалізуй:

* `IAiProvider`;
* `IAiService`;
* `OpenAiCompatibleProvider` або `CustomApiProvider`;
* `AiRequest`;
* `AiResponse`;
* timeout;
* cancellation;
* безпечне маскування секретів у логах;
* prompt-шаблони;
* AI-панель усередині capture workspace.

Провайдери `OpenAiProvider`, `GeminiProvider`, `ClaudeProvider`, `OpenRouterProvider` можна залишити як адаптери або заготовки, якщо робочий OpenAI-compatible provider уже є.

Важливо:

* AI-запит тільки після явної дії користувача;
* не зберігати історію AI-запитів;
* не логувати prompt, якщо він може містити приватні дані;
* без API key програма працює як скріншотер.

## Етап 5. Налаштування, hotkeys, tray

Реалізуй:

* `SettingsWindow`;
* вкладки налаштувань;
* `HotkeyService`;
* перевірку конфліктів клавіш;
* `TrayService`;
* запуск у трей;
* згортання в трей;
* вихід через меню трею.

Готово, якщо основні сценарії доступні як із UI, так і через hotkeys/tray.

## Етап 6. Локалізація, теми, перший запуск

Реалізуй:

* `LocalizationService`;
* `uk-UA.json`;
* `en-US.json`;
* fallback на українську;
* `ThemeService`;
* світлу, темну і системну тему;
* `FirstRunWizardWindow`;
* швидкий старт.

Готово, якщо користувач може вперше запустити програму без ручного редагування JSON.

## Етап 7. Production polish

Дороби:

* людські тексти помилок;
* перевірку пошкоджених конфігів;
* логування без секретів;
* README;
* мінімальні тести;
* перевірку portable-режиму;
* clean build.

Фінальна перевірка:

```powershell
dotnet clean
dotnet build
dotnet test
```

---

# 6. AI-архітектура

Інтерфейс:

```csharp
public interface IAiProvider
{
    string Name { get; }

    Task<AiResponse> AnalyzeImageAsync(
        AiRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> TestConnectionAsync(
        AiProviderSettings settings,
        CancellationToken cancellationToken = default);
}
```

Моделі:

```csharp
public sealed class AiRequest
{
    public required byte[] ImageBytes { get; init; }
    public required string ImageMimeType { get; init; }
    public required string UserPrompt { get; init; }
    public string? SystemPrompt { get; init; }
    public required AiProviderSettings ProviderSettings { get; init; }
}

public sealed class AiResponse
{
    public bool Success { get; init; }
    public string? Text { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan Duration { get; init; }
}
```

AI-параметри:

* provider;
* endpoint;
* apiKey;
* model;
* systemPrompt;
* timeoutSeconds.

---

# 7. Prompt-шаблони

Файл:

```text
config/prompts.json
```

Категорії:

* Загальні;
* Текст;
* Код;
* Помилки;
* Інтерфейс;
* Переклад;
* Користувацькі.

Стандартні шаблони:

1. Що зображено на скріншоті?
2. Розпізнай текст.
3. Переклади текст українською.
4. Переклади текст англійською.
5. Поясни помилку.
6. Поясни код на скріншоті.
7. Знайди проблему в інтерфейсі.
8. Склади відповідь на основі скріншота.
9. Зроби короткий підсумок.
10. Поясни, що потрібно зробити далі.
11. Користувацький prompt.

Користувач повинен мати можливість:

* додати шаблон;
* редагувати шаблон;
* видалити шаблон;
* відновити стандартні шаблони;
* створити категорію;
* видалити власну категорію.

---

# 8. Обробка помилок

Обробити:

* немає інтернету;
* неправильний API key;
* неправильний endpoint;
* timeout;
* AI provider unavailable;
* помилка збереження файлу;
* недоступна папка;
* конфлікт гарячих клавіш;
* пошкоджений JSON;
* відсутні localization-файли.

Принцип:

* користувач бачить просте повідомлення;
* лог отримує технічну деталь;
* секрети маскуються;
* застосунок продовжує роботу, якщо це можливо.

---

# 9. Що не робити

Не реалізовувати:

* приховане фонове стеження за екраном;
* автоматичну відправку скріншотів;
* історію скріншотів;
* історію AI-запитів;
* автоматичне керування іншими програмами;
* автонатискання кнопок;
* автоматичне введення тексту;
* збереження API key у коді.

---

# 10. Фінальний результат

Після реалізації має бути застосунок, який:

* запускається як portable-програма;
* створює скріншоти;
* копіює їх у буфер;
* дозволяє редагувати;
* зберігає PNG/JPG;
* підтримує AI-аналіз скріншота;
* має prompt-шаблони;
* має налаштування;
* має гарячі клавіші;
* має системний трей;
* має українську та англійську мови;
* має теми;
* працює без інтернету як звичайний скріншотер;
* має README та мінімальні тести.
