using System.Globalization;

namespace GameWarmup.Services;

/// <summary>Small built-in RU/EN language table; no external packages or network calls.</summary>
public static class I18n
{
    private static readonly Dictionary<string, (string Ru, string En)> Strings = new(StringComparer.Ordinal)
    {
        ["tagline"] = ("ПРОГРЕВ ФАЙЛОВ HDD / ALPHA 0.4", "HDD FILE WARMUP / ALPHA 0.4"),
        ["language"] = ("Язык / Language", "Language / Язык"),
        ["library"] = ("Библиотека игр", "Game library"),
        ["game_path"] = ("Папка игры (можно вставить путь)", "Game folder (you can paste a path)"),
        ["add"] = ("+ Добавить", "+ Add"),
        ["browse"] = ("Выбрать папку", "Choose folder"),
        ["find_steam"] = ("Найти игры Steam", "Find Steam games"),
        ["games"] = ("ИГРЫ", "GAMES"),
        ["selected_folder"] = ("ВЫБРАННАЯ ПАПКА", "SELECTED FOLDER"),
        ["select_game"] = ("Выберите игру", "Select a game"),
        ["remove"] = ("Удалить из библиотеки", "Remove from library"),
        ["warmup"] = ("Прогрев игровых файлов", "Game file warmup"),
        ["mode"] = ("Режим чтения", "Read mode"),
        ["budget"] = ("Лимит чтения", "Read limit"),
        ["focus"] = ("Match Focus: слова в пути (через ;)", "Match Focus: path keywords (separate with ;)"),
        ["skip"] = ("Пропускать Cache, ShaderCache, Temp, Mods", "Skip Cache, ShaderCache, Temp, Mods"),
        ["workers"] = ("2 потока для мелких файлов (Original, тест)", "2 workers for small files (Original, test)"),
        ["hint"] = ("Подсказка Windows о последовательном чтении (тест)", "Windows sequential-scan hint (test)"),
        ["warning"] = ("Только чтение. Данные могут попасть в файловый кэш Windows, но ускорение не гарантируется.",
                          "Read-only. Data may enter the Windows file cache; a speedup is not guaranteed."),
        ["progress"] = ("ПРОГРЕСС", "PROGRESS"),
        ["start"] = ("Начать прогрев", "Start warmup"),
        ["cancel"] = ("Отменить", "Cancel"),
        ["footer"] = ("Без прав администратора • Только чтение • История в %LOCALAPPDATA%\\GameWarmup • Запускайте игру после прогрева",
                      "No administrator rights • Read-only • History in %LOCALAPPDATA%\\GameWarmup • Launch the game after warmup"),
        ["mode_original"] = ("Original — 64 КБ / 4 МБ / 8 МБ", "Original — 64 KiB / 4 MiB / 8 MiB"),
        ["mode_fast"] = ("Fast — маленькие файлы первыми", "Fast — smallest files first"),
        ["mode_smart"] = ("Smart — сортировка по размеру (эксперимент)", "Smart — file-size heuristic (experimental)"),
        ["mode_full"] = ("Full — порядок файловой системы", "Full — filesystem enumeration order"),
        ["mode_match"] = ("Match Focus — совпадения в конце (эксперимент)", "Match Focus — matched files last (experimental)"),
        ["unlimited"] = ("Без лимита — прочитать все доступные файлы", "Unlimited — read all available files"),
        ["budget_gib"] = ("{0} ГиБ", "{0} GiB"),
        ["ready"] = ("Готов к запуску", "Ready"),
        ["files_idle"] = ("Файлов: —", "Files: —"),
        ["speed_idle"] = ("Скорость: —", "Speed: —"),
        ["elapsed_idle"] = ("Прошло: —  •  Осталось: —", "Elapsed: —  •  ETA: —"),
        ["errors_idle"] = ("Ошибки: 0", "Errors: 0"),
        ["hint_default"] = ("Для Endfield попробуйте Original. Сравнивайте поведение игры до и после прогрева.",
                            "Try Original for Endfield. Compare behavior with and without warmup."),
        ["hint_focus"] = ("Например: maps;de_mirage. Это поиск по именам файлов, а не внутри архивов.",
                          "Example: maps;de_mirage. Matches filenames, not files inside archives."),
        ["hint_path"] = ("Например: D:\\SteamLibrary\\steamapps\\common\\Rust", "Example: D:\\SteamLibrary\\steamapps\\common\\Rust"),
        ["hint_current_file"] = ("Последний файл", "Last file"),
        ["message_path_required"] = ("Укажите путь к игре.", "Enter a game folder path."),
        ["message_missing"] = ("Папка не найдена.", "Folder not found."),
        ["message_disk"] = ("Выберите папку игры, а не весь диск.", "Select a game folder, not a whole drive."),
        ["message_link"] = ("Папки-ссылки не поддерживаются.", "Folder links are not supported."),
        ["message_duplicate"] = ("Игра уже добавлена.", "Game is already in the library."),
        ["message_path_error"] = ("Некорректный путь или нет доступа: {0}", "Invalid path or access denied: {0}"),
        ["message_select"] = ("Выберите игру.", "Select a game."),
        ["message_found"] = ("Найдено в Steam: {0}. Добавлено: {1}.", "Found in Steam: {0}. Added: {1}."),
        ["message_steam_error"] = ("Ошибка поиска Steam: {0}", "Steam scan failed: {0}"),
        ["message_read_error"] = ("Ошибка прогрева: {0}", "Warmup error: {0}"),
        ["scanning"] = ("Сканирование файлов...", "Scanning game files..."),
        ["starting"] = ("Прогрев: {0}. Настройки применяются только к этому запуску.",
                         "Warming up: {0}. Options apply to this run only."),
        ["scanning_progress"] = ("Сканирование файлов...", "Scanning files..."),
        ["read_progress"] = ("Прочитано {0} / план {1} • {2:F1}%", "Read {0} / planned {1} • {2:F1}%"),
        ["files_progress"] = ("Обработано {0}/{1} файлов • пропущено {2}", "Completed {0}/{1} files • skipped {2}"),
        ["speed_progress"] = ("Сейчас: {0:F1} МиБ/с • средняя: {1:F1} МиБ/с", "Recent: {0:F1} MiB/s • average: {1:F1} MiB/s"),
        ["time_progress"] = ("Прошло: {0} • осталось: {1}", "Elapsed: {0} • ETA: {1}"),
        ["errors_progress"] = ("Ошибок чтения: {0}", "Read errors: {0}"),
        ["finished"] = ("Завершено", "Finished"),
        ["cancelled"] = ("Отменено", "Cancelled"),
        ["finishing"] = ("{0} • прочитано {1}", "{0} • read {1}"),
        ["files_final"] = ("Обработано: {0}, прочитано: {1}, пропущено: {2}", "Completed: {0}, read: {1}, skipped: {2}"),
        ["speed_final"] = ("Средняя скорость: {0:F1} МиБ/с", "Average throughput: {0:F1} MiB/s"),
        ["time_final"] = ("Время прогрева: {0}", "Warmup time: {0}"),
        ["hint_final"] = ("Прогрев завершён. Запустите игру и сравните фризы и время загрузки.",
                          "Warmup finished. Launch the game and compare stutters and load time."),
        ["stop"] = ("Остановка...", "Stopping..."),
        ["failed"] = ("Ошибка прогрева", "Warmup failed"),
        ["format_gib"] = ("{0:F2} ГиБ", "{0:F2} GiB"),
        ["mode_tip"] = ("Original, как в Endfield: файлы по размеру. 2 потока необязательны.",
                        "Endfield-like Original: size groups. Two workers are optional."),
    };

    public static string Language { get; private set; } = "ru";
    public static void SetLanguage(string? language) => Language = language == "en" ? "en" : "ru";
    public static string T(string key) => Strings.TryGetValue(key, out var pair)
        ? Language == "en" ? pair.En : pair.Ru : key;
    public static string F(string key, params object[] args) => string.Format(
        CultureInfo.GetCultureInfo(Language == "en" ? "en-US" : "ru-RU"), T(key), args);
    public static string Gib(long bytes) => F("format_gib", bytes / 1073741824d);
    public static bool HasKey(string key) => Strings.ContainsKey(key);
}
