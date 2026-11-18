---
description: >-
  patina's export writes each output as a PNG at any power of two to 8192, in bounded memory
---
# T-115 — PNG export at a given size

**Status:** Open

**Pri:** P1

**Milestone:** M-27

**Batch:** — (standalone)

**Depends on:** T-112, T-113

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
