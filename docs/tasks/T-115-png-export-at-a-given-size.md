---
description: >-
  patina's export writes each output as a PNG at any power of two to 8192, in bounded memory
---
# T-115 — PNG export at a given size

**Status:** In Progress

**Pri:** P1

**Milestone:** M-27

**Batch:** — (standalone)

**Depends on:** T-112, T-113

**Verify:** patina-unit, patina-regression, scenario-patina

## Goal

`export` writes the open graph's outputs as PNGs at a size the agent names, from 32 to 8192, ready for a Carve
material's texture slots.

## Scope

- **`export`** on patina. Arguments: `folder`; `size` (a power of two from 32 to 8192; default the graph's);
  `outputs` (roles; default all). It writes `<folder>/<role>.png` with T-110's packing (roughness as a glTF
  metallic-roughness image, so `define_material`'s `metallic_roughness_texture` takes it). It creates the folder,
  overwrites files of those names, and refuses while the graph has a fault. The answer lists each file with its
  size, its bytes and which Carve material slot it belongs in. The description says that 8192 takes tens of seconds
  (the planning probe: about 10 s and 41 MB per 8k output to encode) and that `swatch` is the way to look.
- **Equality with the samples:** exporting `samples/graphs/planks.texgraph` at 256 writes bytes equal to
  `samples/graphs/planks/*.png` (T-112).
- **Bounded memory at 8192:** a `regression`-tier test exports the planks at 8192, all four outputs, and records the
  peak working set and the time in this file. The bound is a peak of 2 GB or less for the process. The planning
  probe found the PNG encoder alone peaks at about 750 MB for one 8k image, so outputs are encoded one at a time.
  The evaluation must not hold any node's full 8k image (T-110's banding). Over the bound is a stop, not a raised
  bound.
- **Errors:** a size that is not a power of two, or is out of range, gives the allowed values; a folder that cannot be
  written names the folder and the reason.
- **Scenario.** `ExportStage.cs` in `Carve.Scenario.Patina`, plus one line in `PatinaStages.cs`. Export the planks
  at 256 into the run folder and compare with the samples byte for byte. Export at 1024 and check, by sampling at
  matching uv, that it is the same picture at four times the resolution. Then an error path.
- Breaks shown failing: roughness packed into R instead of G, normal green flipped, and an export at the wrong size.

## Notes

- Decided at planning (2026-11-17, by the person): export goes to 8192 and swatch stays at 2048 or below.
- Decided at planning by the orchestrator: files are named `<role>.png` in the folder given; roughness is written
  packed for glTF's metallic-roughness slot. Stop if T-116 or T-117 shows Carve needs another layout.
- Sonnet should do: the format and banding are T-110's, and this task applies them.
- Adds to `Program.cs`'s tool list, `PatinaStages.cs`, the README's patina section and `tests.json` beside T-114: a
  conflict there only adds, so keep both sides.
- **Verify line:** at planning, `cairn lint` accepts only suites already in `tests.json`, so this file names none it will add. Once patina-unit and scenario-patina (T-113 adds them) are in `tests.json`, add them to this file's `**Verify:**` line (creating the line if there is none), with the task's other suites.

### As built

Built by Claude Sonnet 5.5 (`claude-sonnet-5-5`).

- **`export`** (`src/Patina.Server/ExportTool.cs`, one line in `Program.Tools`): `folder` (required, relative to patina's working
  directory, created), `size` (default the graph's), `outputs` (default all the graph has). Writes `<folder>/<role>.png` through a
  temporary file moved into place, answers with one line a file (`WxH`, bytes, `(replaced)`, slot: `base_colour_texture`,
  `normal_texture`, `metallic_roughness_texture` for roughness, `occlusion_texture`). Refused, writing nothing, for a graph with a
  fault (the faults listed), a size outside the power-of-two list (T-110's `SizesText`), an unknown role or one the graph has no
  output for, an empty `outputs`, and a folder it cannot create or write (names the folder and the system's reason). The answer
  carries no time, so a result is the same every run.
- **Memory.** One RGBA8 buffer (256 MB at 8192) is made once and reused for each output; each output is evaluated into it by T-110's
  bands and encoded by `ImageWriter` straight into its file (no PNG byte array), then a compacting GC runs before the next output.
  A first version that made an `OutputImage` per output (a new 256 MB array and a PNG array each) peaked at 2097 MB for the process
  and failed the 2 GB bound; the reuse and the collect brought it to 1565 MB, so the bound stayed at 2 GB.
- **Measured at 8192** (`ExportBoundedMemoryTests`, regression tier, the real patina over stdio, the planks, all four outputs, Debug
  build, on Claude Sonnet 5.5's run on this machine): **154.7 s** and a **peak working set of 1565 MB**. Each PNG is 2.6 to 3.2 MB.
  The time is far over the planning probe's "tens of seconds": the encode is about 10 s an output, so most of the time is the
  evaluation (the nodes work through bounds-checked `Band` texel access, as T-110's As built warned). `export`'s description says
  minutes, not seconds, and advises a client with a short call timeout to ask for one or two outputs at a time. Speeding the
  evaluation is a change to `Patina.Graphs` (not this member's); worth a follow-up. `patina-regression`'s `est-seconds` is now 200.
  The test writes its numbers to `patina-export-8192.txt` in the temp folder.
- **Tests.** `ExportToolTests` (11 unit: bytes equal to `samples/graphs/planks/*.png` at 256, slot names, a size and roles with
  `(replaced)`, 1024 against 256 at matching uv, six bad sizes, the refusals, an unwritable folder, the worked example),
  `ExportBoundedMemoryTests` (regression). `ExportStage` (scenario): byte equality at 256, 1024 sampled against 256 at matching uv,
  then the size, role and folder errors. The three named breaks are caught by these: roughness in R or a flipped normal green change
  the bytes against the samples (which T-112's tests hold to the graph), and a wrong size fails the 256 byte equality, the `WxH` in
  each line and the 1024 size check. Tools-list asserts in T-113's tests now compare the first seven names and then `Contains("export")`,
  so a sibling's tool added after does not break them.
- `scenario-patina` now also lists `samples/graphs/**` as an input.
