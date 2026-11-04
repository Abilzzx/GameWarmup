using GameWarmup.Models;
using GameWarmup.Services;

// Package-free file-read smoke checks: no game installation required.
string root = Path.Combine(Path.GetTempPath(), "gamewarmup-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var a = Path.Combine(root, "sample_1.bin");
    var b = Path.Combine(root, "sample_2.bin");
    var d = Path.Combine(root, "sample_1b.bin");
    var c = Path.Combine(root, "sample_3.bin");
    var cache = Path.Combine(root, "Cache");
    Directory.CreateDirectory(cache);
    File.WriteAllBytes(a, new byte[64 * 1024]);
    File.WriteAllBytes(d, new byte[128 * 1024]);
    File.WriteAllBytes(b, new byte[2 * 1024 * 1024]);
    File.WriteAllBytes(c, new byte[11 * 1024 * 1024]);
    File.WriteAllBytes(Path.Combine(cache, "skip.bin"), new byte[200 * 1024]);

    const long expected = 64L * 1024 + 128L * 1024 + 2L * 1024 * 1024 + 11L * 1024 * 1024;
    var original = await WarmupService.RunAsync(root,
        new WarmupOptions(WarmupMode.Original, null, true), new Collector(), CancellationToken.None);
    Check(!original.Cancelled, "Original completed");
    Check(original.ReadErrors == 0, "No read errors");
    Check(original.FilesRead == 4 && original.FilesCompleted == 4, "Cache directory excluded");
    Check(original.BytesRead == expected, "All three buffer categories read");
    Check(original.BytesCompleted == original.TotalBytes, "100% progress on success");

    var limited = await WarmupService.RunAsync(root,
        new WarmupOptions(WarmupMode.Fast, 128L * 1024, true), new Collector(), CancellationToken.None);
    Check(limited.BytesRead == 128L * 1024, "Budget never exceeded");
    Check(limited.TotalBytes == 128L * 1024, "Planned total equals budget");

    var twoWorkers = await WarmupService.RunAsync(root,
        new WarmupOptions(WarmupMode.Original, null, true, SmallFileWorkers: 2),
        new Collector(), CancellationToken.None);
    Check(twoWorkers.BytesRead == original.BytesRead, "Optional two-worker mode preserves bytes");

    var matchFocus = await WarmupService.RunAsync(root,
        new WarmupOptions(WarmupMode.MatchFocus, null, true, "sample_2"),
        new Collector(), CancellationToken.None);
    Check(matchFocus.BytesRead == original.BytesRead, "Match Focus remains read-only and complete");

    using var cts = new CancellationTokenSource();
    cts.Cancel();
    var cancelled = await WarmupService.RunAsync(root,
        new WarmupOptions(WarmupMode.Original, null, true), new Collector(), cts.Token);
    Check(cancelled.Cancelled, "Cancellation handled without corrupting data");

    I18n.SetLanguage("ru");
    Check(I18n.T("start") == "Начать прогрев", "Russian UI string");
    I18n.SetLanguage("en");
    Check(I18n.T("start") == "Start warmup", "English UI string");
    Check(I18n.HasKey("mode_match") && I18n.HasKey("warning"), "Localization entries present");
    I18n.SetLanguage("ru");

    Console.WriteLine("OK: GameWarmup 0.4 engine and language checks passed.");
}
finally
{
    Directory.Delete(root, recursive: true);
}

static void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL: " + label);
    Console.WriteLine("PASS: " + label);
}

sealed class Collector : IProgress<WarmupProgress>
{
    public void Report(WarmupProgress value) { }
}
