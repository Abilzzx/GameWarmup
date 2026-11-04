namespace GameWarmup.Models;

public sealed class GameEntry
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public override string ToString() => Name;
}

// Keep the original mode values stable for existing UI assumptions.
public enum WarmupMode { Original, Fast, Smart, Full, MatchFocus }

public sealed record WarmupOptions(
    WarmupMode Mode,
    long? MaxBytes,
    bool SkipCaches,
    string PriorityKeywords = "",
    int SmallFileWorkers = 1,
    bool SequentialScanHint = false);

public sealed record WarmupProgress(
    long BytesRead,
    long BytesCompleted,
    long TotalBytes,
    int FilesRead,
    int FilesCompleted,
    int TotalFiles,
    int SkippedFiles,
    int ReadErrors,
    double AverageMbps,
    double RecentMbps,
    TimeSpan Elapsed,
    string CurrentFile,
    bool Scanning);

public sealed record WarmupResult(
    long BytesRead,
    long BytesCompleted,
    long TotalBytes,
    int FilesRead,
    int FilesCompleted,
    int SkippedFiles,
    int ReadErrors,
    bool Cancelled,
    TimeSpan Elapsed);
