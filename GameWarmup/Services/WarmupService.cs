using System.Buffers;
using System.Diagnostics;
using System.Security;
using GameWarmup.Models;

namespace GameWarmup.Services;

/// <summary>
/// Read-only, best-effort file-cache warmup. No game process access and no game-file writes.
/// Reading bytes is NOT equivalent to keeping them resident in the Windows cache.
/// </summary>
public static class WarmupService
{
    private const int SmallBuffer = 64 * 1024;
    private const int MediumBuffer = 4 * 1024 * 1024;
    private const int LargeBuffer = 8 * 1024 * 1024;
    private const long MiB = 1024L * 1024;
    private static readonly HashSet<string> CacheFolders = new(StringComparer.OrdinalIgnoreCase)
    { "ShaderCache", "ShaderCaches", "Cache", "Temp", "Mods", "__pycache__" };

    private sealed record Candidate(string Path, long Length, DateTime ModifiedUtc, long PlannedLength);

    private static bool IsFileException(Exception e) =>
        e is IOException or UnauthorizedAccessException or SecurityException;

    private static IEnumerable<string> EnumerateFiles(string root, bool skipCaches, CancellationToken ct)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count != 0)
        {
            ct.ThrowIfCancellationRequested();
            var folder = pending.Pop();
            string[] files;
            try { files = Directory.GetFiles(folder); }
            catch (Exception ex) when (IsFileException(ex)) { continue; }
            foreach (var path in files)
            {
                ct.ThrowIfCancellationRequested();
                yield return path;
            }
            string[] subdirs;
            try { subdirs = Directory.GetDirectories(folder); }
            catch (Exception ex) when (IsFileException(ex)) { continue; }
            foreach (var path in subdirs)
            {
                ct.ThrowIfCancellationRequested();
                if (skipCaches && CacheFolders.Contains(Path.GetFileName(path))) continue;
                try
                {
                    if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Offline)) == 0)
                        pending.Push(path);
                }
                catch (Exception ex) when (IsFileException(ex)) { /* inaccessible folder */ }
            }
        }
    }

    private static int Group(long size) => size < MiB ? 0 : size < 10 * MiB ? 1 : 2;

    private static List<Candidate> OrderAndPlan(List<Candidate> candidates, WarmupOptions options)
    {
        var keywords = options.PriorityKeywords.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(s => s.Length >= 2).Take(16).ToArray();
        // A named path can be targeted, but archives may contain map data with no identifying filename.
        bool Priority(Candidate c) => keywords.Length != 0 &&
            keywords.Any(k => c.Path.Contains(k, StringComparison.OrdinalIgnoreCase));

        IEnumerable<Candidate> ordered = options.Mode switch
        {
            WarmupMode.Original => candidates.OrderBy(c => Group(c.Length)),
            WarmupMode.Fast => candidates.OrderBy(c => c.Length),
            WarmupMode.Smart => candidates.OrderBy(c => Group(c.Length)).ThenBy(c => c.Length),
            WarmupMode.MatchFocus => candidates.OrderBy(c => Priority(c) ? 1 : 0).ThenBy(c => Group(c.Length)),
            _ => candidates
        };
        long budget = options.MaxBytes ?? long.MaxValue;
        var result = new List<Candidate>();
        foreach (var item in ordered)
        {
            if (budget <= 0) break;
            var planned = Math.Min(item.Length, budget);
            result.Add(item with { PlannedLength = planned });
            budget -= planned;
        }
        return result;
    }

    public static async Task<WarmupResult> RunAsync(
        string gameFolder, WarmupOptions options, IProgress<WarmupProgress> progress, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(progress);
        // File enumeration and metadata access can be slow on HDD, so run off the UI thread.
        return await Task.Run(async () =>
        {
            var root = Path.GetFullPath(gameFolder);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Папка игры не найдена.");
            if (string.Equals(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetPathRoot(root)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Выберите папку игры, а не весь диск.");
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Папки-ссылки не поддерживаются.");
            if (options.MaxBytes is <= 0)
                throw new ArgumentOutOfRangeException(nameof(options), "Лимит должен быть положительным.");

            var elapsed = Stopwatch.StartNew();
            long bytesRead = 0, bytesCompleted = 0;
            int filesRead = 0, filesCompleted = 0, skipped = 0, errors = 0;
            long totalBytes = 0;
            List<Candidate> plan = new();
            long lastReportTicks = 0;
            long previousBytes = 0;
            double previousSeconds = 0, recentSpeed = 0;
            var reportLock = new object();

            void Report(string file, bool scanning = false, bool force = false)
            {
                var nowTicks = Stopwatch.GetTimestamp();
                // Limit UI updates, including while two small-file workers are active.
                if (!force && lastReportTicks != 0 &&
                    (nowTicks - Interlocked.Read(ref lastReportTicks)) < Stopwatch.Frequency / 4)
                    return;
                lock (reportLock)
                {
                    nowTicks = Stopwatch.GetTimestamp();
                    if (!force && lastReportTicks != 0 &&
                        (nowTicks - lastReportTicks) < Stopwatch.Frequency / 4) return;
                    lastReportTicks = nowTicks;
                    var count = Interlocked.Read(ref bytesRead);
                    var seconds = elapsed.Elapsed.TotalSeconds;
                    if (seconds > previousSeconds + 0.01)
                    {
                        recentSpeed = Math.Max(0, (count - previousBytes) / MiB / (seconds - previousSeconds));
                        previousBytes = count;
                        previousSeconds = seconds;
                    }
                    progress.Report(new WarmupProgress(
                        count, Interlocked.Read(ref bytesCompleted), totalBytes,
                        Volatile.Read(ref filesRead), Volatile.Read(ref filesCompleted), plan.Count,
                        Volatile.Read(ref skipped), Volatile.Read(ref errors),
                        seconds > 0 ? count / MiB / seconds : 0, recentSpeed,
                        elapsed.Elapsed, file, scanning));
                }
            }

            try
            {
                Report("Сканирование файлов...", scanning: true, force: true);
                var candidates = new List<Candidate>();
                foreach (var path in EnumerateFiles(root, options.SkipCaches, token))
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var info = new FileInfo(path);
                        if ((info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline | FileAttributes.Directory)) != 0 ||
                            info.Length <= 0)
                        { skipped++; continue; }
                        candidates.Add(new Candidate(path, info.Length, info.LastWriteTimeUtc, info.Length));
                    }
                    catch (Exception ex) when (IsFileException(ex)) { skipped++; }
                }
                plan = OrderAndPlan(candidates, options);
                foreach (var candidate in plan) totalBytes += candidate.PlannedLength;
                Report("Подготовка завершена", force: true);

                async ValueTask ReadOne(Candidate item, byte[] buffer, CancellationToken ct)
                {
                    var size = Group(item.Length) switch
                    {
                        0 => SmallBuffer,
                        1 => MediumBuffer,
                        _ => LargeBuffer
                    };
                    // Reuse one rented buffer across files to reduce allocation/pool churn.
                    long fileRead = 0;
                    bool failed = false;
                    try
                    {
                        await using var stream = new FileStream(item.Path, new FileStreamOptions
                        {
                            Mode = FileMode.Open,
                            Access = FileAccess.Read,
                            Share = FileShare.ReadWrite | FileShare.Delete,
                            BufferSize = SmallBuffer,
                            Options = FileOptions.Asynchronous |
                                (options.SequentialScanHint ? FileOptions.SequentialScan : FileOptions.None)
                        });
                        while (fileRead < item.PlannedLength)
                        {
                            ct.ThrowIfCancellationRequested();
                            int wanted = (int)Math.Min(size, Math.Min(buffer.Length, item.PlannedLength - fileRead));
                            int n = await stream.ReadAsync(buffer.AsMemory(0, wanted), ct);
                            if (n <= 0) break; // File shrank while reading.
                            fileRead += n;
                            Interlocked.Add(ref bytesRead, n);
                            Interlocked.Add(ref bytesCompleted, n);
                            Report(Path.GetFileName(item.Path));
                        }
                    }
                    catch (Exception ex) when (IsFileException(ex))
                    {
                        failed = true;
                        Interlocked.Increment(ref errors);
                    }
                    finally
                    {
                        if (fileRead > 0) Interlocked.Increment(ref filesRead);
                        // An unreadable/short file is completed in the progress bar but NOT in BytesRead.
                        if (!ct.IsCancellationRequested)
                        {
                            if (!failed && fileRead < item.PlannedLength) Interlocked.Increment(ref errors);
                            if (failed || fileRead < item.PlannedLength) Interlocked.Increment(ref skipped);
                            Interlocked.Add(ref bytesCompleted, item.PlannedLength - fileRead);
                            Interlocked.Increment(ref filesCompleted);
                        }
                        Report(Path.GetFileName(item.Path));
                    }
                }

                // Rent at most one 8 MiB buffer for all sequential reads. Optional second
                // worker uses 64 KiB; two concurrent HDD seeks can be SLOWER than one.
                int largestBlock = plan.Any(c => Group(c.Length) == 2) ? LargeBuffer
                    : plan.Any(c => Group(c.Length) == 1) ? MediumBuffer : SmallBuffer;
                var primaryBuffer = ArrayPool<byte>.Shared.Rent(largestBlock);
                byte[]? secondBuffer = null;
                try
                {
                    if (options.SmallFileWorkers == 2 && options.Mode == WarmupMode.Original)
                    {
                        secondBuffer = ArrayPool<byte>.Shared.Rent(SmallBuffer);
                        var firstLargeIndex = plan.FindIndex(c => Group(c.Length) != 0);
                        if (firstLargeIndex < 0) firstLargeIndex = plan.Count;
                        int smallCount = firstLargeIndex;

                        async Task ReadAlternatingSmall(int offset, byte[] buf)
                        {
                            for (int i = offset; i < smallCount; i += 2)
                            {
                                token.ThrowIfCancellationRequested();
                                await ReadOne(plan[i], buf, token);
                            }
                        }

                        await Task.WhenAll(ReadAlternatingSmall(0, primaryBuffer),
                            ReadAlternatingSmall(1, secondBuffer));
                        for (int i = smallCount; i < plan.Count; i++)
                        {
                            token.ThrowIfCancellationRequested();
                            await ReadOne(plan[i], primaryBuffer, token);
                        }
                    }
                    else
                    {
                        foreach (var item in plan)
                        {
                            token.ThrowIfCancellationRequested();
                            await ReadOne(item, primaryBuffer, token);
                        }
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(primaryBuffer);
                    if (secondBuffer is not null) ArrayPool<byte>.Shared.Return(secondBuffer);
                }
                elapsed.Stop();
                Report("Готово", force: true);
                return new WarmupResult(bytesRead, bytesCompleted, totalBytes,
                    filesRead, filesCompleted, skipped, errors, false, elapsed.Elapsed);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                elapsed.Stop();
                Report("Отменено", force: true);
                return new WarmupResult(bytesRead, bytesCompleted, totalBytes,
                    filesRead, filesCompleted, skipped, errors, true, elapsed.Elapsed);
            }
        }, CancellationToken.None);
    }
}
