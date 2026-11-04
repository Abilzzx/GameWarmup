# GameWarmup — experimental game-file warmup for Windows

**English** · [Русская версия / Russian README](README.ru.md)

GameWarmup is a free, open-source **C# / .NET 10 / WPF** utility for Windows 10/11. It reads files from a selected installed game **before you launch it**, giving Windows a chance to keep recently read data in its file cache. It is designed primarily for games installed on **mechanical HDDs** where first-time file access may contribute to stuttering.

> **Read this first:** GameWarmup does not make an HDD faster, does not guarantee any FPS or loading-time improvement, and cannot force Windows to retain all read data in RAM. It can spend minutes reading files with little or no benefit for some games. **Experimental Alpha 0.4 — not a proven optimizer for every game.**

## Who is it for?

- Players with HDD-installed games that stutter when accessing assets for the first time.
- People who want to experiment with **before-game** file-cache warmup and compare results.
- Developers who want to inspect, improve, or test a read-only Windows tool.

If your game runs from an SSD/NVMe drive, the likely benefit is smaller. For persistent HDD streaming problems, **moving the game to SSD is more reliable** than pre-reading its files.

## What it does

1. You choose an **individual game installation folder**, or scan Steam libraries for installed games.
2. The tool recursively finds ordinary readable files. By default it skips folders named `Cache`, `ShaderCache`, `ShaderCaches`, `Temp`, `Mods`, and `__pycache__` (case-insensitive). It skips reparse-point links and offline files.
3. It creates a read plan, then opens files in **read-only mode** (`FileAccess.Read`), reads byte blocks, and closes them. It **does not decode or identify textures**: archives, models, sounds, images and other files are all just bytes.
4. Windows **may** cache some of those bytes in RAM. Windows, not GameWarmup, determines which data stays cached and how long.
5. The UI reports scanned/planned bytes, actual bytes read, processed files, errors, recent/average read throughput, and estimated remaining time. A `100%` bar indicates **planned work completed**, not that 100% of bytes were retained in RAM.
6. When warmup finishes, launch your game normally. The app never launches or injects into a game process.

**Note:** The read throughput displayed by GameWarmup is **not a measurement of physical HDD speed or Windows cache hit rate**. It includes reads served from cache if the files were previously accessed.

### What to expect

- **Possible:** Fewer *first-access* pauses and shorter file-read-related loads in some HDD games.
- **Possible:** No improvement whatsoever; Windows might evict some or all of the warmed data.
- **Possible:** The warmup itself makes total time-to-play *longer*, even if subsequent game loading improves slightly.
- **Not expected:** Higher GPU/CPU performance, higher sustained FPS, fixed shader compilation, reduced network matchmaking latency, extra VRAM, faster internet, or a replacement for an SSD.
- **Not claimed:** “All game assets are loaded into RAM,” "zero stutter", or guaranteed support for a particular game.

## How long will it take?

The time depends mostly on **how much is read, the actual read throughput, number of small files, other disk activity, and HDD condition**. The initial scan also takes time.

**Approximate pure-read examples (before extra scanning/seeking overhead):**

| Data to read | Sustained rate | Approximate read time |
| --- | ---: | ---: |
| 20 GiB | 100 MiB/s | ~3 min 25 sec |
| 65 GiB | 90 MiB/s | ~12 min 20 sec |
| 100 GiB | 90 MiB/s | ~19 min |

Formula: `seconds ≈ data_size_in_GiB × 1024 / throughput_in_MiB_per_second`. The **full** warmup can take more or less than these estimates. A user observed approximately **12 minutes to read ~65 GiB** with the original Arknights: Endfield script. That is a real example, **not** a universal prediction.

**Before every warmup:** close the game (do not run a match during HDD reading), close unnecessary disk-intensive programs, select the right game folder and settings, click **Start warmup**, **wait until it completes**, then launch the game soon afterwards. Restarting Windows between warmup and game launch defeats the purpose of preserving the current cache. Do not claim that repeated warmup is always necessary: Windows may already have useful data cached.

## Getting started on Windows 10/11

You can run the source code using the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0):

```powershell
dotnet run --project .\GameWarmup\GameWarmup.csproj
```

1. Switch the language in the top-right corner: **Русский / English** (saved for future sessions).
2. Click **Find Steam games** or **Choose folder**. For manually added games, choose the **game's own install directory** — never the disk root or a folder containing dozens of unrelated games.
3. Choose **Original** and **Unlimited** for a first full warmup comparison, especially for Arknights: Endfield. By default use **one worker** on HDD.
4. Optionally skip caches/mods; verify the installed game actually resides on an HDD.
5. Click **Start warmup** and wait for **Finished**. The progress bar tracks the file-read plan. Launch the game normally afterwards.
6. Compare the **same scene/map**, using tests with and without warmup. Record the time spent warming up as well as the game's load time and stutters.

**Example Steam folder** (your drive/path may differ): `D:\SteamLibrary\steamapps\common\Arknights Endfield`. The game's data may reside in a subfolder such as `Endfield_Data`; use the appropriate directory for that installation.

## Modes and settings

| Mode | Behavior | Notes |
| --- | --- | --- |
| **Original** | Files grouped by size: `<1 MiB` with **64 KiB** chunks; `1–<10 MiB` with **4 MiB** chunks; `>=10 MiB` with **8 MiB** chunks | *Inspired by* the Endfield warmup approach. Not a byte-for-byte replica of its PowerShell code. |
| Fast | Reads smaller files first | Experimental; may not help game startup. |
| Smart | Groups and sorts by file size | **Simple heuristic**, not machine learning or usage tracing. |
| Full | Uses filesystem enumeration order | No intelligent prioritization. |
| Match Focus | Reads paths matching user keywords **last** | A filename heuristic; it cannot inspect an archive's internal assets. |

- **Read limit:** Unlimited (full planned read) or 2, 4, 8, 16, 32 GiB. With a limit, the last file may be only partially read.
- **Skip cache/mod directories:** Optional. It skips folder names, **not all files with cache-related extensions**.
- **Two small-file workers (Original only):** Optional; may be **slower on HDD** due to extra seeks. Default is one.
- **Windows sequential-scan hint:** Optional experiment. This changes Windows' caching/read-ahead behavior and may help or hurt this particular goal; **off by default**.
- **Cancellation:** Stops safely between/within reads; does not modify the game.
- **Per-run history:** `%LOCALAPPDATA%\GameWarmup\history.csv`; saved game list and language are stored locally as JSON. No telemetry or network requests are implemented.

## Which games have been tested?

**Do not confuse “can select a folder” with “proven to reduce stutter.”**

| Game | Current evidence |
| --- | --- |
| **Arknights: Endfield** | One user reports noticeably fewer freezes after warmup with the **original Endfield PowerShell project**. GameWarmup's equivalent benefit still needs controlled tests. |
| **Counter-Strike 2** | In a user test, ~2:00 to the main menu *without* GameWarmup versus **2:03** after `Original` and **2:03** after `Match Focus`: **no measured startup benefit**. Map-loading time has not been established. |
| **Red Dead Redemption 2, The Last of Us Part I, Cyberpunk 2077, NTE, and others** | Files can be selected manually, but **no performance improvement has been verified**. |

A game may require shader compilation, decompression, CPU processing, online authentication, matchmaking, or loading map data from generic archives. This utility does not accelerate those operations.

## Fair benchmarking

Compare runs on the **same PC, same game settings, same installed game version and same scene/map**. Record both warmup time and actual game time. Test both cold-start conditions (e.g. after a reboot) and already-cached conditions, but do not mix them within one comparison. Repeat each condition at least three times; do not treat a 2–3 second difference as conclusive when the baseline is approximate. Avoid claiming gains without measurements.

## Safety and limitations

- Reads files with `FileAccess.Read`; **never writes, deletes, or patches game files**.
- Does not inject DLLs, scan process memory, modify anti-cheat, or require administrator privileges for ordinary folders.
- Does not force caching or lock RAM, and cannot determine cache residency or cache-hit rates.
- Can't preselect specific resources inside monolithic game archives without format-aware development; we don't currently implement that.
- Full scanning can saturate an HDD for minutes. Cancel if system responsiveness suffers; avoid concurrent game launches.
- Files that are missing, inaccessible or change size can be skipped/counted as errors. `BytesRead` is actual read volume; `BytesCompleted` is completed planned work (including skipped/short files).
- There are **no guaranteed compatibility, speed, or stutter fixes** for any game.
- Source and GitHub workflow are provided for community testing. Unsigned community binaries may prompt Windows reputation checks; if unsure, build from source or review the repository.

## Build, tests, release

Run the package-free engine smoke checks:

```powershell
dotnet run --project .\GameWarmup.Checks\GameWarmup.Checks.csproj -c Release
```

Publish a standalone Windows x64 build with the .NET runtime included:

```powershell
dotnet publish .\GameWarmup\GameWarmup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish
```

The executable appears in `publish\GameWarmup.exe`. A [GitHub Actions Windows build workflow](.github/workflows/windows-build.yml) runs smoke checks and publishes an artifact on pushes and pull requests. **A green build does not prove warmup improves a game.**

### Project structure

```text
GameWarmup/                 WPF desktop app and icon
  MainWindow.xaml(.cs)      Interface, game selection, controls, language switch
  Models/GameEntry.cs       Game information, read options and result models
  Services/WarmupService.cs Read-only scan, planning, streaming and cancellation
  Services/SteamScanner.cs  Steam installation discovery
  Services/I18n.cs          Russian and English text
  Services/SettingsStore.cs Language preference
  Services/LibraryStore.cs  Local library JSON
  Services/HistoryStore.cs  Local CSV run history
GameWarmup.Checks/          Self-contained, package-free checks
.github/workflows/         Windows CI build
README.md                   English documentation
README.ru.md                Russian documentation
LICENSE                     MIT license
```

### Publishing to your own GitHub repository

Create a public repository (suggested name: **GameWarmup**). Upload **the files inside the extracted project folder**, keeping `GameWarmup/`, `GameWarmup.Checks/`, `.github/`, both READMEs, and `LICENSE` in the repository root. Do **not** upload the `bin/`, `obj/`, `publish/` folders or personal `%LOCALAPPDATA%` files. Let GitHub Actions compile and run the checks; download its `GameWarmup-win-x64` artifact after a successful run. When ready, create a GitHub **Release**, add the artifact, and mark it **pre-release / Alpha**. Test on Windows before distributing it broadly.

## Credits and license

GameWarmup is an **independent** C# implementation inspired by [Coco542/ZZZ-Endfield-HDD-Warmup](https://github.com/Coco542/ZZZ-Endfield-HDD-Warmup). The original PowerShell project is licensed under MIT; GameWarmup does **not** include copied PowerShell source. Thank you to Coco542 for the original idea. GameWarmup is published under [MIT](LICENSE).

### Further reading

- [Microsoft: File Caching](https://learn.microsoft.com/en-us/windows/win32/fileio/file-caching)
- [Microsoft: Windows Cache and Memory Management](https://learn.microsoft.com/en-us/windows-server/administration/performance-tuning/subsystem/cache-memory-management/)
- [Microsoft: file access flags and sequential hints](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew)
#   G a m e W a r m u p  
 