# ShareX UX reference

Цей документ фіксує ідеї, які можна безпечно адаптувати з ShareX без копіювання GPL-коду, UI-ресурсів або реалізації один-в-один.

## Корисні патерни

- Tray-first режим: головне вікно не є основним робочим сценарієм, а служить dashboard для історії, налаштувань і діагностики.
- Task/workflow модель: кожна дія має тип захоплення, after-capture кроки, destination/AI route і власний hotkey.
- After-capture tasks: після скріншоту користувач може швидко копіювати, зберігати, редагувати, запускати AI або відкривати папку.
- Thumbnail history: головне вікно має показувати останні задачі/скріни з прев’ю, розміром, часом і станом.
- Region capture modes: окремо варто розвивати простий region capture, annotation mode, editor mode, one-click window capture, color picker і ruler.
- Dynamic menus: списки провайдерів, workflow presets і after-capture дії мають будуватись із конфігу, а не бути зашитими в UI.

## Адаптація для SmartScreen

- Не дублювати WinForms-естетику ShareX. Залишити ідею продуктивного dashboard, але реалізувати її як сучасний WPF workspace.
- Зробити AI route повноцінним destination типом поряд із clipboard/file/editor.
- Зберігати історію не лише в пам’яті, а згодом у `history.json`, щоб головне вікно відкривалось із попередніми задачами.
- Додати workflow presets: `Quick copy`, `Save + AI`, `Edit first`, `Silent capture`, `Upload/custom action`.
- Для курсової описувати ShareX як аналог і референс, але підкреслити власну архітектуру, AI routing і privacy-first підхід.

## Джерела

- https://github.com/ShareX/ShareX
- https://deepwiki.com/ShareX/ShareX/3-screen-capture-system
- https://deepwiki.com/ShareX/ShareX/3.1-region-capture
- https://deepwiki.com/ShareX/ShareX/5.1-task-settings
- https://getsharex.com/actions
