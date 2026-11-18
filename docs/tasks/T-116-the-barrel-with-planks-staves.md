---
description: >-
  samples/barrel-planks.carve: M-7's barrel with its staves from the planks graph, in a scenario of its own
---
# T-116 — The barrel with planks staves

**Status:** In Progress

**Pri:** P1

**Milestone:** M-27

**Batch:** — (standalone)

**Depends on:** T-112

**Verify:** core-unit, scenario-materials, scenario-planks

## Goal

M-7's barrel wearing staves made by a texture graph instead of T-77's generator: `samples/barrel-planks.carve`,
built through Carve's tools from the planks graph's PNGs, seen and checked in a scenario of its own, with M-7's barrel
left as it is.

## Scope

- **New scenario `scenarios/Carve.Scenario.Planks`** (suite `scenario-planks`, `gpu`), built as
  `Carve.Scenario.Materials` builds the textured barrel. Open `samples/barrel.carve`, `project_uvs` as M-7 does,
  then the materials: `staves` and `rune-stave` take `graphs/planks/base_colour.png`, `normal.png`,
  `occlusion.png` and `roughness.png` (as `metallic_roughness_texture`), and the rune keeps `rune-emissive.png`;
  `iron` and `end-grain` keep `samples/textures/`. `check` must be silent. Save as `samples/barrel-planks.carve`
  (texture paths relative to the file, reaching `samples/graphs/planks/` and `samples/textures/`), reopen it (frames
  byte-identical), and export it.
- **Frames:** front, back, top, a stave-gap close-up, and the sheet. Check every pixel from `get_mesh`'s geometry and
  UVs and the planks' texels, linking `MaterialsSampler.cs` and `MaterialsChecks.cs` (and what they link) from the
  Materials scenario rather than copying them. A gap must read darker than its stave, and staves must differ in tone.
- **Work the checks at planning time, before any frame** (M-7's lesson). The research pass checks, from the sampler
  and the planks' PNGs:
  - that M-7's lit prediction holds with a roughness map in place of a scalar. M-7's staves had none. If it does
    not, say what the check can predict and stop;
  - that every gap is at least 3 texels each side, so P2 can sample it;
  - that the stave colours clear the lit threshold `MaterialsChecks` uses (probed on the planks' own texels, not
    T-77's).
- **Guard:** M-7's `scenario-materials` frames, `samples/barrel-textured.carve` and `samples/textures/` stay
  byte-identical. Nothing of T-77's generator changes.
- A test keeps `samples/barrel-planks.carve` equal by content to the one the scenario saves, in the manner of
  `tests/Carve.MaterialsLoad.Tests`. Add it there, or in a `PlanksLoad` project of its own if the linking is cleaner,
  and record which in CLAUDE.md's Layout.
- Breaks shown failing: the planks' base colour swapped for T-77's staves, roughness bound as base colour, and u
  mirrored on the side.

## Notes

- Decided at planning (2026-11-17, by the person): beside M-7's barrel, not in place of it.
- Content task of the milestone's mechanic (the graph's PNGs on a Carve model). It depends only on T-112's PNGs, so it
  runs beside T-113 and does not wait for the patina server.
- **Draws new frames, so plan the frame review as its own agent**, which opens every frame itself before sending the
  set to the person. Sonnet for the build, as M-7's T-80 was. Treat Waystone's "Commented" as unanswered.
- **Stop and ask** before landing a threshold, a skipped check or a change to the planks' PNGs. The last is T-112's
  file: reserve a fix, don't edit it here.
- Touches no patina file. It adds its suite to `tests.json` and a row to CLAUDE.md, and a conflict with the patina
  tasks there only adds.
- **Verify line:** at planning, `cairn lint` accepts only suites already in `tests.json`, so this file names none it will add. Once scenario-planks is in `tests.json`, add it to this file's `**Verify:**` line (creating the line if there is none), with the task's other suites.

### Research (Sonnet 5.5, before any frame)

- **M-7's lit prediction holds with a roughness map; no new threshold, no skipped check.** `MaterialsChecks` asks of stave wood only
  `Max >= T (80)` and `R >= G + 12 >= B + 24`; it never predicts a specular. The roughness map (G 146..199, so roughness 0.57..0.78
  against M-7's scalar 1; B 0, so metallic 0 with the default factor) only changes the specular, which is added equally to all three
  channels. Probed with `ColourRule.Lit` (its `Roughness` made a variable in a throwaway copy, not in the repo) over the planks'
  darkest tone (0.817 of (170,112,64)), 72 azimuths, three tilts, the front, back, top and viewer cameras, at roughness 1, 0.78
  and 0.57: the worst stave reads (112,81,60), so R-G >= 29, G-B >= 21 and max channel >= 110, against 12, 12 and 80, and the three
  roughnesses give the same worst case. The planks' darkest mid texel is (142,94,53), no darker than that probe.
- **Gaps are 3 texels each side, in every row** (the scenario checks it from the PNG before it opens anything: `PlankTexels.GapProblem`,
  red < 90 as T-112 defines it; and no texel 6 or more from a boundary is dark). A P2 footprint (half-width 2, 5 texels) fits in the
  6-wide gap only where a pixel spans 0.67 texel or less, so the gap is judged in a close-up (zoom 4, head on to the boundary), not
  in the 512 px frames.
- **Stave colours clear the lit threshold.** Mid-stave texels run (142..179, 94..118, 53..67), per-stave tones 0.82..1.18 of the base;
  see the probe above for the lit values.
- Not done, as the Scope says: M-7's `staves` (no roughness map) is untouched.
