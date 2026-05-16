using SmartScreen.Domain.Models;

namespace SmartScreen.Application.Defaults;

public static class DefaultPromptLibraryFactory
{
    public static AiPromptLibrary Create()
    {
        var categories = new[]
        {
            Category("general", "Загальні", 0),
            Category("text", "Текст", 1),
            Category("code", "Код", 2),
            Category("errors", "Помилки", 3),
            Category("ui", "Інтерфейс", 4),
            Category("translation", "Переклад", 5),
            Category("custom", "Користувацькі", 6)
        };

        return new AiPromptLibrary
        {
            Categories = [.. categories],
            Templates =
            [
                Template("describe", "general", "Що зображено?", "Опиши, що зображено на скріншоті. Відповідай структуровано і коротко.", 0),
                Template("ocr", "text", "Розпізнай текст", "Розпізнай увесь видимий текст на скріншоті. Збережи структуру, якщо вона важлива.", 1),
                Template("translate-uk", "translation", "Переклади українською", "Розпізнай текст на скріншоті та переклади його українською мовою.", 2),
                Template("translate-en", "translation", "Переклади англійською", "Розпізнай текст на скріншоті та переклади його англійською мовою.", 3),
                Template("explain-error", "errors", "Поясни помилку", "Поясни помилку на скріншоті простою мовою. Додай ймовірну причину та конкретні кроки виправлення.", 4),
                Template("explain-code", "code", "Поясни код", "Поясни код на скріншоті: що він робить, які є проблеми та як його можна покращити.", 5),
                Template("ui-problem", "ui", "Знайди проблему в UI", "Проаналізуй інтерфейс на скріншоті. Знайди проблеми з UX, доступністю або візуальною ієрархією.", 6),
                Template("compose-reply", "general", "Склади відповідь", "На основі скріншота склади доречну відповідь користувачу. Враховуй контекст і тон повідомлення.", 7),
                Template("summary", "general", "Короткий підсумок", "Зроби короткий підсумок інформації на скріншоті у 3-5 пунктах.", 8),
                Template("next-steps", "general", "Що робити далі?", "Поясни, що користувачу потрібно зробити далі. Дай конкретні наступні кроки.", 9)
            ]
        };
    }

    private static AiPromptCategory Category(string id, string name, int order) =>
        new() { Id = id, Name = name, IsSystem = true, Order = order };

    private static AiPromptTemplate Template(string id, string categoryId, string title, string prompt, int order) =>
        new() { Id = id, CategoryId = categoryId, Title = title, Prompt = prompt, IsSystem = true, Order = order };
}

