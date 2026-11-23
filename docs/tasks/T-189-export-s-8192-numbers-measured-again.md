---
description: >-
  export's description, the README and T-115's record carry the 8192 times, PNG sizes and peak memory measured with the new encoder, in Release and Debug
---
# T-189 — export's 8192 numbers measured again

**Status:** Done

**Pri:** P1

**Milestone:** M-33

**Batch:** — (standalone)

**Depends on:** T-188

**Verify:** patina-unit, patina-regression, scenario-patina

## Goal

What patina says about an 8192 export is what it now measures: the time, in Release and Debug, the size of each PNG
and the peak memory, in `export`'s description, the README and T-115's record.

## Scope

- **Measure, on this machine, the planks at 8192, all four outputs, through the real `patina`:**
  - Debug: `ExportBoundedMemoryTests` writes its time and peak to `patina-export-8192.txt` in the temp folder.
  - Release: the same three calls (`initialize`, `open`, `export`) sent over stdio to a Release build's
    `patina.dll`, timing the export call and reading the peak working set as the test does. T-155's Release harness
    called the evaluator alone and had no encoder, so it does not serve. Keep this one in `artifacts/M-33/T-189/`
    under the container's `branches/main`, outside the repo's tracked files, and write down how to run it. Do not add
    a product flag for it.
  - Each twice, giving both figures. The planning bench's two runs differed by 10 to 30%.
  - Record the whole call's time, the encode's share of it, each PNG's bytes and the peak working set. For the
    encode's share, run `artifacts/png-bench` (the container's `branches/main`) again, or time around the write.
- **Before, for the same table:** M-33's Why has the Release figures at `d45ed52` (evaluation 19.6 s, encode 24.2 s,
  2.6 to 3.2 MB a PNG) and T-115 the Debug ones (154.7 s, 1,565 MB).
- **`ExportTool.Text`** (`src/Patina.Server/ExportTool.cs`). The sentence that says 8192 is "minutes and not
  seconds: the planks' four outputs took about 3 minutes (a debug build), and each PNG is some 3 MB" carries the
  measured figures. It still tells a client with a short call timeout what to do, if the measured time still
  warrants it. `NodeToolTests` holds every description at 3,686 characters or less.
- **`README.md`**, the paragraph under "What is slow or bounded": the same figures. Say in the As built what changed.
- **T-115's `### As built`** gets a dated line under its "Measured at 8192" item with the new figures. The old line
  stays.
- **`ExportBoundedMemoryTests`:** its summary comment, if what it says about the encode is no longer so. The 2 GB
  bound is not moved in either direction. Record the new peak.
- **`tests.json`:** `patina-regression`'s `est-seconds`, from this task's own run of the suite. Nothing else there.
- **`CLAUDE.md`:** only a figure it quotes that is now wrong.
- No test asserts a time. The times are a record, as T-155's are.

## Notes

- **A measuring task.** It makes long real runs and little code, and decides nothing others build on.
- Evaluation is untouched by M-33, so the whole call stays tens of seconds in Release and longer in Debug. Write
  what is measured. T-130 owns evaluation, and the person kept it out of this milestone (2026-11-22).
- **Goes back to the person, not into the code:** an encode over a second an output in Release, which is the
  milestone's claim; a peak over the bound; any wish to tighten or loosen the bound or to assert a time.
- `ExportTool.cs` is also T-188's file, so this task starts only once T-188 has landed.
- `patina-regression` runs about 20 minutes. Give `verify` a long limit.
- Paths and C# strings written through a shell lose backslashes. Write files with the file tools.

### As built

Built by Claude Sonnet 5.5 (2026-11-23).

Planks, 8192, four outputs, .NET 10.0.15, 16 cores, `PngWriter` (M-33). CPU is the machine's total, sampled by the harness or
a logger, with everything on it (other repos' test hosts, mostly). "Shared" means other cairn jobs and other processes ran
beside the measurement; "exclusive" means it ran as a cairn `ad-hoc` job with `--resources exclusive` (see below).

| | Whole call | Encode (4 outputs) | Peak working set | Machine CPU | Slot |
|---|---|---|---|---|---|
| Before, Release, `d45ed52` (M-33's Why) | evaluation 19.6 s + encode 24.2 s | 24.2 s | not measured | not recorded | not recorded |
| Before, Debug (T-115) | 154.7 s | about 10 s each | 1565 MB | not recorded | not recorded |
| After, Release, run 1 | 27.8 s | 1.13 s (share harness, own process) | 711 MB | 92% before, 84% during | shared |
| After, Release, run 2 | 31.3 s | 0.59 s | 665 MB | 91% before, 96% during | shared |
| After, Debug, run 1 (`ExportBoundedMemoryTests`) | 233.1 s | 1.40 s | 683 MB | 99% after | shared |
| After, Debug, run 2 | 248.4 s | 1.37 s | 684 MB | 100% after | shared |
| After, Debug, once, from the exclusive `patina-regression`-equivalent run | 168.0 s | not separately measured | 703 MB | 58% mean, 100% max | exclusive |

- PNG bytes (the same in every run, Release and Debug): base_colour 599,340; normal 277,371; roughness 973,204; occlusion
  277,908 (0.28 to 0.97 MB each; before 2.6 to 3.2 MB). Encode per output 0.09 to 0.51 s in Release, 0.23 to 0.46 s in Debug:
  nowhere near a second an output. Evaluation (28.0 and 32.4 s Release, 207 and 218 s Debug, in the share harness) is nearly
  all of the call. Peak is far under the 2 GB bound (not moved).
- The machine was 85 to 100% busy with others' processes during every shared figure (testhosts of other repos); none was from
  this task. The Release runs were taken when none of this task's cairn jobs ran. The run-to-run spread is 12% (Release)
  and 7% (Debug), about as the load moved.
- **Exclusive run.** `cairn test submit --command "dotnet test tests/Patina.Server.Tests/Patina.Server.Tests.csproj -c Debug
  --filter Tier=regression" --resources exclusive --wait` at the committed `279e915` (no `tests.json` change was needed; the
  committed `resources` of `patina-regression` is untouched): job J-20261123-070035-30167e, PASS, **1236.9 s**, after about 35
  minutes queued while the runner drained. It also wrote the 168.0 s export figure above. Machine CPU averaged 58% (max 100%)
  over its window, so other, non-cairn load was there; and the status list still showed other repos' jobs running at times, so
  I do not claim the machine was quiet. `est-seconds` for `patina-regression` is set from it: **200 to 1240**. For comparison, the
  suite took 1920 s and 1852 s as shared `verify` jobs of this task (machine near 100% CPU) and 3,505 s in T-188's.
- **Release harness**, in `artifacts/M-33/T-189/` of the container's `branches/main`: `harness/` (stdio client, the test's three
  calls against a Release `patina.dll`), `share/` (evaluation vs `PngWriter.Write` timed apart, Release or Debug), `HOWTO.txt`
  (how to run), and the results `release-runs.txt`, `release-share.txt`, `debug-share.txt`, `debug-runs.txt`.
- **Text changed.** `ExportTool.Text`: the 8192 sentence now says peak 0.7 GB, 28 to 31 s in release, 3 to 4 min in debug, each PNG 0.3
  to 1 MB, mostly evaluation, and still tells a short-timeout client to ask for one or two outputs (the Text is within the 3,686
  limit after trimming). `README.md` ("What is slow or bounded"): the same figures plus the encode's 0.6 to 1.4 s. T-115: a dated line
  under "Measured at 8192"; the old one stays. `tests.json`: `est-seconds` only. `ExportBoundedMemoryTests` and `CLAUDE.md`: no
  figure in them was wrong, so no change.
  - *Corrected for RV-80 (2026-11-23):* "3 to 4 min in debug" left out two of the three Debug runs (2.8 to 4.1 min), so
    `ExportTool.Text` now says "about 3 to 4 min in debug" (3,679 characters, under 3,686) and the README gives 2.8 to
    4.1 minutes with the three figures. The README and T-115's dated line now say the encode was timed apart from the
    call, by the `share/` harness's `PngWriter.Write` to a file, so it reads as the encoder's cost. The comment in
    `ExportTool.Run` that an export takes "minutes at 8192" now says so of a debug build only.
- **Not checked:** the encode's share inside the export tool's own streamed path (the share harness writes a whole in-memory
  image with the same writer, so it is the writer's time, not the tool's); either build on an idle machine; the first Release
  run's 1.13 s encode (second 0.59 s) is load; no sibling runs; no frame was opened.
- Surprise: the first `verify` failed `NodeToolTests` because my first wording put `export`'s description 48 characters over the limit;
  trimmed.
- Ran on Claude Sonnet 5.5.
