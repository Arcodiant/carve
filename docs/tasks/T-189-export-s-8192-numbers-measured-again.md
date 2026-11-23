---
description: >-
  export's description, the README and T-115's record carry the 8192 times, PNG sizes and peak memory measured with the new encoder, in Release and Debug
---
# T-189 — export's 8192 numbers measured again

**Status:** In Progress

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
