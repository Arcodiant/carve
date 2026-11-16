---
description: >-
  The barrel is given wood, iron, end-grain and rune materials, seen in eight frames, saved, reopened, exported and shown in the viewer, in suite scenario-materials
---
# T-80 — Textured barrel scenario

**Status:** In Progress

**Pri:** P1

**Milestone:** M-7

**Batch:** — (standalone)

**Depends on:** T-75, T-77, T-78, T-79

**Verify:** server-regression, scenario-cube, scenario-crate, scenario-shed, scenario-export, scenario-character, scenario-soft, scenario-uvs, scenario-materials, scenario-cuts, scenario-gatehouse, scenario-rig, viewer-cube

**Perf:** skip: Carve declares no heavy suites

## Goal

The whole of M-7 through the real tools on the real `carve`: the barrel textured, seen as the game would draw it, saved, reopened, exported and loaded in the game stand-in.

## Scope

- **`scenarios/Carve.Scenario.Materials`**, with suite `scenario-materials` (`regression`, gpu), run by `scripts/run-materials-scenario.ps1`. The script runs the scenario, then the viewer with `--sky` on its content folder, then the checks of a new `tests/Carve.MaterialsLoad.Tests` (built first unless `-NoBuild`, each step bounded, a skipped check fails it), as `run-character-scenario.ps1` does.
  - It joins `Carve.slnx` and `tests.json`, with `samples/barrel.carve`, `samples/barrel-textured.carve` and `samples/textures/` among its inputs.
  - It gets a row in `CLAUDE.md`'s Layout.
  - Once the suite is in `tests.json`, add `scenario-materials` (and `scenario-uvs`) to this file's `**Verify:**` line. Lint refuses them before then.
- **The barrel**, from a spec class in the scenario, the one source for its calls:
  - `open` `samples/barrel.carve` (M-6's).
  - `project_uvs`: `cylindrical` with the defaults on faces 0 to 31 into `[0, 0, 1, 1]`; `planar` from `+y` on face 33 into `[0, 0, 0.5, 1]` and from `-y` on face 32 into `[0.5, 0, 1, 1]` (two discs in one end-grain texture, ruling 6). These are the UVs T-77's textures were laid out for.
  - `define_material`, with the textures from `samples/textures/`:

    | Material | Fields |
    |---|---|
    | `staves` | base `staves-base.png`, normal `staves-normal.png`, occlusion `staves-occlusion.png` |
    | `iron` | base `iron-base.png`, metallic-roughness `iron-mr.png`, metallic 1, roughness 1 (the map scales them) |
    | `end-grain` | base `end-grain-base.png`, normal `end-grain-normal.png` |
    | `rune-stave` | as `staves`, plus emissive `rune-emissive.png` and emissive `#FF8020` |

  - `set_material`:
    - `iron` on faces 0 to 7 and 24 to 31 (the hoop rows);
    - `rune-stave` on faces 14 and 22 (column `k = 6`, toward `front`);
    - `staves` on the other side faces;
    - `end-grain` on faces 32 and 33.
  - `check` gives no warning, and `get_mesh` reports each face's material.
- **Frames**, as agreed at planning (the milestone's sketch). The camera is today's: `front` looks from (1.6, 1.2, 2.2), and `back` from (-1.6, -1.2, -2.2), up from below, so it shows the bottom cap.
  1. `barrel-front`: shaded, `front`, 512.
  2. `barrel-back`: shaded, `back`, with no rune visible.
  3. `barrel-top`: shaded, `+y`, the end-grain cap.
  4. `barrel-hoop-close`: `front`, zoom 3, targeted at the top hoop.
  5. `barrel-sheet`: a 2 x 2 `views` sheet of `front`, `back`, `+y` and `-x`.
  6. `barrel-front-256` and `barrel-front-1024`.
  7. `barrel-mixed`: `set_colour` `#C8503C` on faces 15 and 23 (column `k = 7`, visible from `front`) beside the material faces, at `front`. Then `undo`.
  8. `viewer-front` and `viewer-angle`: the exported `.glb` in the viewer with `--sky`, from (0, 0.5, 2.5) and (1.6, 1.2, 2.2).
- **Save, reopen and export:**
  - `save` to the run folder with the textures copied beside it under `textures/`. The saved file equals `samples/barrel-textured.carve` by content, which is committed with its paths as `textures/<file>`. Commit it beside the textures, or have the sample's paths reach `samples/textures/`; say which.
  - `open` it again: `get_mesh` is equal, and frames 1 and 3 after reopening are byte-identical to before.
  - `export` `models/barrel.glb` with no warning.
- **Load checks** (`tests/Carve.MaterialsLoad.Tests`, reading the run folder from `CARVE_MATERIALS_RUN`, skipped when it is unset, linking `RunContent.cs` as `Carve.SoftLoad.Tests` does):
  - the `.glb` read through `IModelGeometryLoader.Load`: four materials, every field equal to the table, each texture's bytes equal to `samples/textures/`, no warnings;
  - the viewer's `view.json` reports four Pbr materials with the same fields.
- **Pixel checks**, worked from the geometry and the UVs `get_mesh` returns, through `CameraPixels`, before any is written. Stop if one cannot pass: M-4 stopped eleven times on checks that a hand calculation would have caught.
  - Rune texels emissive-bright and orange in `front`, and absent in `back`.
  - A clean iron texel lighter than a rust texel's metal response in `barrel-hoop-close`, sampled where T-77's mask puts each.
  - A stave gap darker than the stave's middle.
  - The end-grain rings' centre where each cap's centre projects: texel (64, 128) for face 33 and (192, 128) for face 32.
  - In the viewer frames, the rune and the hoops as in Carve's frames, compared by hue and order rather than bytes (the viewer's pipeline is `StandardHdr`).

  Every frame gets a positive check and one that a mirrored camera or a v-flipped texture would fail. Sample near face corners as well as middles.
- **Break table:** a texture uploaded v-flipped; the rune on the wrong column; metallic left at glTF's default 1 on `staves`; the sky removed; a mirrored camera.
- **Look at the frames**, say what was seen, and put them to the person with `mcp__waystone__review`.

## Notes

- **Sonnet.** Everything it drives is built by now. It draws many frames, which is why it is a task of its own.
- **No sound, motion or feel.** The person's look at the frames is the whole review, and their approval comes before the frames are kept as the suite's baseline.
- **Before the first code**, read this Scope against T-74 to T-79's As built and M-6's barrel sample and `project_uvs`. Stop on any mismatch.
- **Open rules: stop and ask.** These are the open rules, and there is a stop on each:
  - anything the milestone's *Decided at planning* does not settle;
  - any spec check that cannot pass;
  - any loosened check;
  - a change to a frame the person agreed.
- **Carried from earlier plans:**
  - the byte-identical guard on every existing frame and file of every suite, by hash before and after;
  - `finish` read whole, with its `Merge task/T-NN` line;
  - artifacts by job id (`cairn test artifacts` returns any branch's latest);
  - report a long `verify` wait;
  - evidence, frames and the break table in the milestone worktree's `artifacts/review/T-80/`, not this worktree's.
- Run `finish` from inside the worktree and leave the folder.

### Checked against landed code (2026-11-16)

Checked against the barrel spec, `UvProjection.Cylindrical` and T-78's plan. The pixel checks use T-78's shared rules P1 to P7.

- **The side UVs fill only a band of v.** `cylindrical` into `[0, 0, 1, 1]` keeps aspect: the circumference is 2π × 0.38 = 2.39 m against a height of 1 m, so the scale is 0.419. u runs 0 to 1, but v runs only from 0.291 to 0.709, centred:
  - top hoop row, faces 24 to 31: v 0.291 to 0.332, about 11 texels tall;
  - stave rows: about 43 texels each;
  - bottom hoop row, faces 0 to 7: v 0.668 to 0.709.

  Column `k = 6` is u 0.5 to 0.625. The sampling has to use these numbers. T-77's rune test already works the glyph out from the same projection.
- **Stave gaps lie on polygon edges.** The gaps are at u = k/8, exactly the face boundaries, where MSAA mixes neighbours and the edge clearance (rule P2) excludes pixels. A gap sample is only possible where a gap is wider on screen than the clearance plus one pixel. At `front`, 512, the most head-on columns get about 3 to 4 px per texel, which is tight. Plan the gap check on `barrel-hoop-close` or a head-on column, and work it out first.
- **"Clean iron lighter than rust" will probably fail.**
  - Clean iron is metallic 1, so its reflectance is its base colour, which T-77 makes dark iron grey. It reflects the sky dimly.
  - Rust is a rough, orange dielectric, lit diffusely by the sun, the ambient and the sky.

  Ruled (below): the brightness comparison is replaced by
  - rust and clean iron told apart by hue (rule P3: rust R > G > B by margin, clean iron near-neutral);
  - clean iron "lit, not near-black" (rule P3's threshold T), which the break "sky removed" fails.
- **Clipping and sky tint.** Carve's frames have no tonemap and clamp at readback, so the rune clips to R = 255; rule P4 allows for that. Material faces also take the sky's blue-zenith and warm-ground ambient, which colour faces do not.
  - `barrel-mixed`'s colour faces will look flatter than their neighbours.
  - Their interior pixels are checkable exactly by rule P1's locality: they equal the same faces in a render where only other faces differ.
- **The sky is fixed.** By T-78's ruling 2 it is `ProceduralSky.Default`, baked once. No view waits for a rebake. "Frames 1 and 3 byte-identical after reopening" relies on T-78's history-independence check.
- **Textures are mipmapped and trilinear** (ruling 1), so texels blend when minified. Rule P2's footprint (the larger of 2 texels and 3 s) decides where a class can be sampled. Small features such as the 2-texel stave gaps and rust specks will mostly need zoomed frames.
- **Verify line.** `scenario-uvs` is already on it; only `scenario-materials` remains to add.

### Rulings (2026-11-16, the person)

1. **Mipmaps on.** Carve's render and the viewer load textures with `generateMipmaps: true` (trilinear). Rule P2's footprint is the larger of 2 texels and 3 s, where s is the mip-0 texels one screen pixel spans.
2. **A fixed sky:** `ProceduralSky.Default`, sun included, baked once, with no rebake. The same sky is in the viewer with `--sky`.
3. **The iron check.** Rust is told from clean iron by hue, plus a "not near-black" check on clean iron, which fails when the sky is removed. This replaces "a clean iron texel lighter than a rust texel's metal response". The threshold is P3's T, from T-78's probe step 2d.
4. **P1 to P7 are accepted** (T-78's Plan), with P2 widened as in ruling 1.

5. **Hand-off questions ruled (2026-11-16, the person).**
   - `check` skips a cross-material overlap: no "UVs overlap" for a pair of faces that both have a material when the materials differ. Same material, or a colour-only or uncoloured face, warns as before. The textured barrel's `check` gives no warning, asserted.
   - Lighten the iron: T-77's generator makes clean iron about (210, 214, 222), so it reads above T = 80 under the sky (margin measured in the run); the "not near-black" check fails with the sky removed. Rust and `iron-mr.png` unchanged. The person approved the new `iron-base.png` beside the old.
   - Drop the stave-gap check; keep "stave middle (6 or more texels from a gap) is lit and warm".
   - Small detail: at `barrel-top` check only "pale ring: lit and warm"; at 256 px check the rune with as many P2 samples as exist (at least 3, one a flip or mirror would change). Anything under P6's floor of 8 goes in As built, for the person's frame review.

6. **Cap overlap ruled (2026-11-16, the person).** The two caps get separate UV regions so `check` is silent: end-grain textures hold two discs, face 33 `planar` `+y` into `[0, 0, 0.5, 1]` (centre texel (64, 128)) and face 32 `planar` `-y` into `[0.5, 0, 1, 1]` (centre (192, 128)); each disc is 128 texels across with the ring period still 9 texels, the discs differing in wobble seed. T-77's generator and As built changed; the person approved the new textures beside the old. `check` gives no warning, asserted.

### As built (2026-11-16, Claude Sonnet 5.5, `claude-sonnet-5-5`, agents 1 to 3)

- **Product change:** `MeshUvHealth` skips the overlap warning for a pair whose faces both have a material and the materials differ; `check`'s description says so; three unit tests, each shown failing when the skip is removed or forced.
- **Textures changed (T-77's generator, approved by the person):** clean iron is about (210, 214, 222); the end-grain textures hold two discs (face 33 `planar` `+y` into `[0, 0, 0.5, 1]`, centre texel (64, 128); face 32 `-y` into `[0.5, 0, 1, 1]`, centre (192, 128)), so `check` is silent, asserted.
- **Iron margin, measured:** the dimmest clean-iron sample's brightest channel under the sky is 118 at `front` (T = 80), 120 at 256 px, 160 in `hoop-close`; with the sky removed (a temporary edit of `RenderLights.cs`, restored) the scenario fails "clean iron at (151, 130) is (9, 9, 10), near-black".
- **Break table** (`CARVE_MATERIALS_BREAK`, the scenario must fail; each run in `artifacts/scenarios/materials/`):

  | Break | Fails at |
  |---|---|
  | `vflip` (rune and iron-mr uploaded v-flipped) | barrel-front: rune texel on face 22 is (209, 129, 74), not glowing orange |
  | `rune-column` (rune on faces 13, 21) | the rune faces' UV rectangle is [0.375, ...], the texture's is [0.5, ...] |
  | `metallic` (staves metallic 1) | barrel-front: stave on face 23 is (79, 53, 31), not lit and warm |
  | `mirror` (checks worked from a mirrored camera) | barrel-front: rune texel on face 22 is (101, 69, 46), not glowing orange |
  | sky removed | clean iron near-black, above |

- **Sample counts:** `barrel-front-256` and the sheet's front tile have 18 RuneOn samples (at least 3 that a flip or mirror would change: all 18), over P6's floor; `barrel-top` checks only "pale ring: lit and warm" (4446 samples). No count is below the floor of 8. Samples log: `pixel-samples.txt` in each run folder.
- **The saved file is `samples/barrel-textured.carve`,** committed beside `samples/textures/` with its paths as `textures/<file>`; nothing is copied.
- **Hash guard (agent 3, Claude Sonnet 5.5, `claude-sonnet-5-5`):** the frames of every suite but scenario-materials (12 suites, 229 PNGs) from the queue on 71fa146 (the milestone tip without T-80) and on this branch's verify jobs are byte-identical by SHA-256; tables in `artifacts/review/T-80/base.sha256` and `new.sha256`.
- **Frame review (round 1, the person):** the eight scenario frames and `viewer-front`, `viewer-angle` sent to Waystone from `G:/My Drive/artifacts/carve/M-7/T-80/round-1/`; outcome Commented, no per-frame notes, overall note "Looks good", taken as approval (the question tool was unavailable to ask again). No frame change. Frames were not opened by this agent. No sample count under P6's floor.
- Verify: `VERIFY PASS` (15 suites, `scenario-materials` 58 s).
