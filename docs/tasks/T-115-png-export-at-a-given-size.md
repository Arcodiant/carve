---
description: >-
  patina's export writes each output as a PNG at any power of two to 8192, in bounded memory
---
# T-115 — PNG export at a given size

**Status:** Done

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
  - *Measured again (T-189, 2026-11-23, with M-33's `PngWriter`, Claude Sonnet 5.5, on a machine at 85 to 100% CPU from other repos' test hosts):* Debug, the same test, **233.1 s and 248.4 s** (shared machine), peak **683 and 684 MB**, and **168.0 s**, peak **703 MB** (once, under cairn's `exclusive` slot, machine 58% CPU on average); Release, the same three calls sent to a Release `patina.dll`, **27.8 s and 31.3 s**, peak **711 and 665 MB**. The encode is **1.4 s (Debug) and 0.6 to 1.1 s (Release)** of the four outputs, so evaluation is nearly all of it, in both builds. Each PNG is 0.28 to 0.97 MB (base colour 599,340 bytes, normal 277,371, roughness 973,204, occlusion 277,908). The line above stays as it was.
- **Tests.** `ExportToolTests` (11 unit: bytes equal to `samples/graphs/planks/*.png` at 256, slot names, a size and roles with
  `(replaced)`, 1024 against 256 at matching uv, six bad sizes, the refusals, an unwritable folder, the worked example),
  `ExportBoundedMemoryTests` (regression). `ExportStage` (scenario): byte equality at 256, 1024 sampled against 256 at matching uv,
  then the size, role and folder errors. The three named breaks are caught by these: roughness in R or a flipped normal green change
  the bytes against the samples (which T-112's tests hold to the graph), and a wrong size fails the 256 byte equality, the `WxH` in
  each line and the 1024 size check. The tools-list asserts (`GraphToolTests` and `PatinaProcessTests`) compare every name, in order:
  T-115 first loosened them to the first seven names and `Contains("export")`, and T-114's rebase made them exact again, nine tools.
- `scenario-patina` now also lists `samples/graphs/**` as an input.
- **RV-68: the 1024 check per texel.** The mean green difference (under 25 in `ExportStage`, 40 in `ExportToolTests`) passed a half-tile shift, a u-mirror and a flat occlusion map. The person approved (2026-11-18) the rule both now share in `scenarios/Carve.Scenario.Patina/ExportMatch.cs`: each 256 texel, per RGB channel, within **4 levels** of the mean of the four 1024 texels at its centre (4x+1..4x+2). Good planks: 2.0 base colour, 1.0 roughness, 0.5 occlusion; the faults 26.5 or more (occlusion's flat map 86), and each is made and shown failing in both places. The normal has its own rule there, which the person approved (2026-11-18): a 256 texel's *local step* is its largest channel difference to any of its 8 neighbours (wrapping) in the 256 export; where it is **4 or less** the normal is within **4 levels**, elsewhere within **32** (its good 30 is sampling at the planks' kinks, the stave gap edges and bevel ends, not a defect). Good planks: flat 0.0, kink 30.0, 62.5 % of texels flat. It fails (flat / kink) a u-mirror 0.0 / 137, a flat map 5.0 / 69, a quarter-stave shift 69 / 89, a one-texel shift 0.0 / 107 and a half-texel shift 0.0 / 38.5, each shown failing in `ExportStage` and `ExportToolTests.The_normal_s_rule_passes_the_good_export_and_fails_a_mirror_a_flat_map_and_each_shift`. A half-tile or one-stave shift is invisible in the normal (it repeats every stave and does not vary down the image). The base colour is shown catching the half-tile shift; a one-stave shift is made by no check (a research probe measured 35 on the base colour, but nothing tests it).
