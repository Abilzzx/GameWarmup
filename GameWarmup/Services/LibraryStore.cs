using System.Text.Json;
using GameWarmup.Models;
namespace GameWarmup.Services;
public static class LibraryStore
{
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameWarmup");
    private static readonly string FilePath = Path.Combine(Folder, "library.json");
    public static List<GameEntry> Load()
    {
        try { return System.IO.File.Exists(FilePath) ? JsonSerializer.Deserialize<List<GameEntry>>(System.IO.File.ReadAllText(FilePath)) ?? new() : new(); }
        catch { return new(); }
    }
    public static void Save(IEnumerable<GameEntry> games)
    {
        Directory.CreateDirectory(Folder);
        var temp = FilePath + ".tmp";
        System.IO.File.WriteAllText(temp, JsonSerializer.Serialize(games, new JsonSerializerOptions { WriteIndented = true }));
        System.IO.File.Move(temp, FilePath, true);
    }
}
