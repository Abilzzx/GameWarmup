using System.Text;
using System.Security;
using GameWarmup.Models;

namespace GameWarmup.Services;

/// <summary>Run summaries are stored locally, never in a game's folder.</summary>
public static class HistoryStore
{
    private static readonly string PathToHistory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GameWarmup", "history.csv");

    private static string Csv(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";

    public static void Append(GameEntry game, WarmupOptions options, WarmupResult result)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PathToHistory)!);
            if (!File.Exists(PathToHistory))
                File.WriteAllText(PathToHistory,
                    "date_utc,game,path,mode,planned_bytes,read_bytes,seconds,files_read,skipped,errors,cancelled\r\n",
                    new UTF8Encoding(true));
            var line = string.Join(",", Csv(DateTime.UtcNow.ToString("O")), Csv(game.Name),
                Csv(game.Path), Csv(options.Mode.ToString()), result.TotalBytes,
                result.BytesRead, result.Elapsed.TotalSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                result.FilesRead, result.SkippedFiles, result.ReadErrors, result.Cancelled ? "1" : "0") + "\r\n";
            File.AppendAllText(PathToHistory, line, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            // History failure must not invalidate a completed read-only warmup.
        }
    }
}
