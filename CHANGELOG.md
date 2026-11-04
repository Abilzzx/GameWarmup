# Changelog

## Alpha 0.4 (source release candidate)

- Added English and Russian UI translations with a live language switch and persistent preference.
- Added comprehensive bilingual GitHub documentation, estimated warmup times, tested/unverified game status, and release instructions.
- Reused pooled read buffers across files rather than renting for every file; allocates a buffer sized for the largest file class in the current plan.
- Kept read-only game-file access, cancellable warmup, one-worker default, size-based Original mode, Steam discovery, and local history.
- Corrected handling of short reads: unfinished file data are counted as read errors without overstating actual bytes read.
- Extended package-free engine checks to cover all three chunk-size classes, two-worker mode, Match Focus, read limits, cancellation, and RU/EN translation.
- Added project version metadata and a Windows release helper script.

**Testing note:** Static XML/asset/package checks are performed outside Windows. Full WPF compilation, console checks, and game performance should be validated on Windows / GitHub Actions before publishing the binary.

## Alpha 0.3

- First graphical prototype with Original, Fast, Smart, Full and experimental Match Focus modes.
- Steam scanner, progress, cancellation, history.csv and application icon.
