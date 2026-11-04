using System.Text.Json;

namespace GameWarmup.Services;

public static class SettingsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GameWarmup", "settings.json");

    public static string LoadLanguage()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
                if (document.RootElement.ValueKind == JsonValueKind.Object &&
                    document.RootElement.TryGetProperty("language", out var value) &&
                    value.ValueKind == JsonValueKind.String)
                    return value.GetString() == "en" ? "en" : "ru";
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        return "ru";
    }

    public static void SaveLanguage(string language)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { language }));
            File.Move(temporary, FilePath, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
