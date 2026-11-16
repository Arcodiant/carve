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

**Verify:** server-regression, scenario-cube, scenario-crate, scenario-shed, scenario-export, scenario-character, scenario-soft, scenario-uvs, scenario-cuts, scenario-gatehouse, scenario-rig, viewer-cube

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
  - `project_uvs`: `cylindrical` with the defaults on faces 0 to 31 into `[0, 0, 1, 1]`; `planar` from `+y` on face 33 and from `-y` on face 32, each into `[0, 0, 1, 1]`. These are the UVs T-77's textures were laid out for.
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
  - The end-grain rings' centre where face 33's centre projects.
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

### Hand-off (2026-11-16, Claude Sonnet 5.5, `claude-sonnet-5-5`, agent 1)

**Stopped on three rules the Scope, Notes and Rulings do not settle. Nothing was loosened; the scenario fails at the first of them, as the spec is written.** About 45 tool calls, no image opened.

**Done (committed, builds, runs on the real `carve`):**
- `scenarios/Carve.Scenario.Materials` (joins `Carve.slnx`): `MaterialsScenario.cs` (the calls from `MaterialsSpec`: open `samples/barrel.carve`, three `project_uvs`, four `define_material`, four `set_material`, `check`, the frames, `set_colour` and `undo`, save, reopen, export to `game/Content/models/barrel.glb`, `viewer-samples.tsv`), `MaterialsSampler.cs` (a ray test over every face, the UV and texel footprint of each pixel by P2, edge clearance), `MaterialsChecks.cs` (the texel classes read from `TextureSet.Build()`, P3 to P6 relations).
- Linked, not copied: `BarrelSpec.cs` (moved out of `UvsScenario.cs` into its own file in the Uvs scenario so it can be linked; no change to that scenario's calls), `UvsScene.cs`, `CrateBuild.cs`, `CameraPixels.cs` and `TextureSet.cs`. `CameraPixels.Unproject` was added (the inverse of `Perspective`; the existing methods are untouched).
- The sampler uses the product's own triangulation: `Triangulator` clips the ear at corner 0 first, so a quad splits along its 1-3 diagonal (the fan from corner 1); a cap's planar UVs are affine, so the split does not matter there.
- Run `pwsh scripts/run-scenario.ps1 materials` (folder `artifacts/scenarios/materials/<n>/`). Before the stop: define, set and `get_mesh` face materials passed; the rune-UV rectangle and face 33's centre texel (128, 128) matched; in `barrel-front` the rune check passed (at least 8 glowing glyph samples, each brighter than the plain wood of its face, and at least 3 that a v-flip or u-mirror would change).

**Not done:** `tests/Carve.MaterialsLoad.Tests`, `scripts/run-materials-scenario.ps1`, the `tests.json` suite, the CLAUDE.md Layout row, `samples/barrel-textured.carve` (it will be committed beside `samples/textures/`, whose `textures/<file>` paths it reaches as they are; nothing to copy), the `**Verify:**` line, the break table, the hash guard, `### As built`, the frame review. The viewer's pixel checks are planned as relations (P7) over `viewer-samples.tsv`, which the scenario already writes.

**Questions for the person (each with a recommendation):**
1. **`check` cannot give no warning.** `check` returns 65 warnings, all `UVs overlap`: the two caps' planar UVs (`[0, 0, 1, 1]`, as the textures are laid out) cover the same square as the side's cylindrical UVs, so each of the 32 side faces overlaps face 32 and face 33, and the caps overlap each other (32 x 2 + 1). No other warning. The overlap is harmless here (the faces use different textures). Recommend: the scenario asserts that the warnings are exactly these 65 overlap pairs (side face with cap, and cap with cap) and nothing else, and says so in As built. The other way, giving the caps their own UV region, would change T-77's textures. I did not land the narrowed check.
2. **Clean iron is below T = 80, so the "not near-black" check cannot pass.** T = 80 came from T-78's probe on a bright metal (base 0.8, metallic 1, roughness 0.35: 150 with the sky, 11 without). T-77's iron is dark grey (58, 61, 66), metallic 1, so its reflection of the sky is dim: the one clean pixel the first check reached read (56, 63, 71), under 80. Without the sky it would be near 0. Recommend: the threshold for iron is worked the way T-78 did, from a probe of this iron material (metallic 1, roughness 1 times the map) with and without the sky, halfway between the two (about 35), and written into As built; the "sky removed" break then fails it. This needs your ruling because Ruling 3 fixes T as P3's T = 80.
3. **The stave-gap check cannot pass P2.** A gap is texels x % 32 = 0 and 31, one texel wide on each face, centred on the face boundary, which is a drawn edge (clear of 3 px) and by P2's footprint (at least 2 texels each way, inside one class) it is wider than the region. This holds at every zoom, so a zoomed frame does not help. Recommend: replace the check with the relation the textures can bear at a P2 footprint, "stave middle (6 or more texels from a gap) is lit and warm (R > G > B by M)", which `barrel-front`, `barrel-hoop-close` and the others already carry (`Pool.Wood`), and record the gap as not checkable. The alternative is a sample at a gap texel's centre with a smaller footprint (bilinear weight at a centre is about 1 when s is under 0.1), which is a P2 exception that you would have to rule.

**Surprises:** (a) the end-grain rings are not resolved at `barrel-top`: s is about 0.7 texels per pixel, so P2's footprint is about 4 texels, and the dark latewood band is under 2 texels wide, so only "pale ring: lit and warm" is checkable there (`Pool.Rings`), with the ring centre checked from the UVs (face 33's centre texel is (128, 128)), not from a frame. Not yet run, so it may fail the floor of 8 samples. (b) At 256 the rune strokes (4.3 texels wide) fit P2's footprint (at least 4 texels) only where the pixel centre is within about 0.15 texel of the stroke's line; whether 8 such pixels exist at `barrel-front-256` and in the sheet's front tile is not yet known. If one cannot pass, it is another question.
