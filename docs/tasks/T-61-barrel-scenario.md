---
description: >-
  The barrel is seamed, unwrapped and seen in both UV looks, and the crate box-projected, saved, reopened and exported, in suite scenario-uvs
---
# T-61 — Barrel scenario

**Status:** Done

**Pri:** P1

**Milestone:** M-6

**Batch:** — (standalone)

**Depends on:** T-55, T-56, T-58, T-59, T-60

**Verify:** server-regression, scenario-cube, scenario-crate, scenario-shed, scenario-export, scenario-character, scenario-uvs

**Perf:** skip: Carve declares no heavy suites

## Goal

The whole of M-6, through the real tools on the real `carve`: a barrel seamed and unwrapped, a crate box-projected, both seen in both looks, a fault made and found, and the result saved, reopened and exported.

## Scope

- **`scenarios/Carve.Scenario.Uvs`**, with suite `scenario-uvs` (`regression`, gpu). It joins `Carve.slnx` and `tests.json` (with `samples/barrel.carve` among its inputs), and gets a row in `CLAUDE.md`'s Layout. Once the suite is in `tests.json`, add `scenario-uvs` to this file's `**Verify:**` line (lint refuses it before then).
- **The barrel**, built from a spec class in the scenario (the one source for its calls), as a mesh named `Barrel`:
  - `define_mesh` with T-53's barrel, then `mark_edges` with T-56's seams and `unwrap` with the defaults;
  - `get_mesh` gives three islands, and `check` gives no UV warning;
  - the saved barrel equals `samples/barrel.carve` by content.
- **The crate** as a second mesh `Crate`, built by the same tool calls as `scenario-crate` from one shared source, never a copy. Then `project_uvs` with `box`.
- **Frames:**
  - `checker` of the barrel: `front`, `back`, `-z` (the seam side), `+y` (the top cap), and a 2 x 3 `views` sheet with face labels;
  - `uvs` of the barrel at 256, 512 and 1024, with face labels, and with a side face highlighted (and the same face highlighted in `checker`);
  - `checker` of the crate `front` and `back`, and its `uvs` layout.
- **A fault made and found:**
  - `move` a middle ring outward: the `checker` frame shows the stretched cells, and `unwrap`'s stretch rises. Then `undo`.
  - `extrude` the top cap: the new walls are magenta in `checker`, `check` lists them, and `export` warns. Then `undo`.
- **Save, reopen and export:**
  - `save` both meshes to the run folder, `open` again, `get_mesh` equal, and the checker and layout frames after reopening byte-identical to before;
  - `export` `models/barrel.glb` with no warning.
- **Pixel checks**, worked from the geometry and the UVs `get_mesh` returns, through `CameraPixels` and the checker's cell rule:
  - the white cell and hue order where predicted, and not at their mirror;
  - magenta on the extruded walls;
  - in the layout, the island fills bright, empty space dim, yellow on each seam's UV segments, and the highlight orange.

  Every frame gets a positive check and one that a mirrored camera or a v-flipped texture would fail. Work every check against the geometry before writing it, and stop if one cannot pass: M-4 stopped eleven times on checks that a hand calculation would have caught.
- **Break table:** a texture uploaded with v flipped; corner UVs rotated by one; `unwrap` ignoring seams (refused, so the scenario fails); the layout drawn v-up; a mirrored camera.
- **Look at the frames**, say what was seen, and put them to the person.

## Notes

- Sonnet: everything it drives is built by now. It draws many frames, which is why it is a task of its own.
- No sound, motion or feel: the person's look at the frames is the whole review.
- **Before the first code**, read this Scope against what T-53 to T-60 landed (their As built sections) and stop on any mismatch.
- **Carried from earlier plans:**
  - the byte-identical guard on every existing frame and file of every suite, by hash before and after;
  - `finish` read whole, artifacts by job id (`cairn test artifacts` returns any branch's latest), report a long `verify` wait;
  - evidence, frames and the break table in the milestone worktree's `artifacts/review/T-61/`, not this worktree's.
- **Open rules: stop and ask.** Anything the milestone's *Decided at planning* does not settle, any spec check that cannot pass, and any loosened check.
- Run `finish` from inside the worktree and leave the folder.


### As built

Built by Sonnet 5.5 (`claude-sonnet-5-5`), one agent, 2026-11-14, on `task/T-61` from `milestone/M-6` at `2284acc`.

**Rulings (the person, 2026-11-14).** (1) The barrel is compared with `samples/barrel.carve` as a mesh only: the sample is `open`ed and its `get_mesh` vertices, faces, UVs and seams are compared with the saved `Barrel`'s, ignoring the name (the sample's mesh is called `mesh`) and the other mesh; the sample is unchanged. (2) The overlap: `Crate` is built by `CrateBuild`'s shared calls (it gained an optional mesh-name parameter, `WithMesh`; scenario-crate's calls and output are unchanged, byte for byte) and then one `move` of all 72 vertices by +3 in x puts it clear of the barrel. Both meshes stay in one model, saved and exported together. Found while building: `render` frames the bounds of every mesh and `mesh` only aims labels and highlight, so each frame is zoomed on one mesh by `zoom` and `target` (`UvsChecker.Fit` picks the largest zoom, 8 down to 1, that keeps the mesh 8% inside the frame), and the ray test treats both meshes as occluders.

**The suite** `scenario-uvs` (`regression`, gpu; `tests.json` with `samples/barrel.carve` an input; the sample copied beside the scenario by a Content link; a Layout row in `CLAUDE.md`), `scenarios/Carve.Scenario.Uvs`: `UvsScenario.cs` (`BarrelSpec`, the one source of the barrel's calls, and the run), `UvsScene.cs` (`get_mesh` read into meshes, islands by seams, ray test), `UvsChecker.cs` (the checker sweep). Linked from the Crate scenario, never copied: `CrateBuild.cs`, `CameraPixels.cs`, `CheckerColours.cs`, `UvLayoutPixels.cs`, `CrateLooks.cs`, `CrateChecker.cs`, `CrateLabels.cs`, `CrateUvLayout.cs` (the last had `Log`, `Sweep`, `Origin` and `Seam` made `internal`, and `Seam` an optional `clearPx`; nothing else changed).

**The run.** `define_mesh` `Barrel`, `mark_edges` seam (T-56's 20 edges), `unwrap` with the defaults: three islands (also by the seams in `get_mesh`), 34 mapped faces, `check` with no UV warning (side stretch 1.0927). `Crate` built, moved, `project_uvs box`: 70 faces mapped. Then, in order: the barrel's checker frames; the 2 x 3 sheet; the highlight; the barrel's layouts; the crate's frames; the two faults; save, open, compare, export.

**Frames** (22, all from the verify run; copies in `artifacts/review/T-61/frames/`):

| Frame | What |
|---|---|
| `barrel-checker-front`, `-back`, `-minus-z`, `-plus-y` | checker, four views, 512 |
| `barrel-checker-sheet` | 2 x 3 sheet, 256 tiles, face labels |
| `barrel-checker-highlight` | front, one side face highlighted |
| `barrel-uvs-256`, `-512`, `-1024`, `-labels`, `-highlight` | the barrel's layout |
| `crate-checker-front`, `-back`, `crate-uvs` | the crate |
| `barrel-moved-checker-front`, `barrel-extruded-checker-front` | the two faults |
| `...-after-moves`, `...-after-extrude`, `...-reopened` (5) | repeats, each byte-equal to its first |

**Checks.**

- **Checker frames** (`UvsChecker.Sweep`): points spread over each triangle of each face the eye faces (about 36 a triangle), kept where a ray test over both meshes finds them in sight, 4 px clear of every projected edge, 4 px inside the frame, clear of the caption and (sheet) the face labels, a fifth of a cell clear of the cell's edge and the same cell under both diagonals of a quad (UV within 0.006). The pixel then must be magenta (no UVs), white at cell (0,0), else not white, not magenta and nearest in hue to its column (`CheckerColours`, T-57's thresholds). Needs per frame: cell (0,0) sampled wherever a face holding uv (0.0625, 0.0625) faces the eye (cosine over 0.3), a minimum of columns, and on frames with a cap a light cell brighter than the dark cell below it on one face. 2971 samples are logged in `checker-samples.txt`, none failing. The white cell reads at the lower left of the `-z` frame, the seam side.
- **Highlight in checker** is the checker tinted orange (T-57's ruling): checked against the plain frame's own samples, the highlighted face's pixels keep red and lose blue, every other sampled pixel is byte-equal. The thresholds (red at least 0.8 of before less 5, blue at most 0.6 of before plus 6) are the scenario's own; the person approved the frame.
- **Layout frames** (`CrateUvLayout.Sweep`, `Origin`, `Seam` over `UvLayoutPixels`, worked from `get_mesh`'s UVs): the sweep (bright texel, dim ground, solid orange for the highlight) at 256, 512, 1024, with labels and with the highlight; the origin cell white with its mirrors not; every seam's segments on both faces by the blend fit at 0.4. **Seams where another island's line is under 7 px away are not checked** (the two caps are 3.5 px apart at 256, the strip and a cap likewise; the check reads 4 px either side) and are logged `not checked`: 11 of 40 at 256, 1 of 40 at 512, none at 1024. This is a precondition, not a looser threshold (the fit still needs 0.4), but it is a check not made.
- **Faults.** Ring 2 moved outward 0.15 m a vertex at a time (8 `move` calls): the checker frame is swept against the moved geometry with the old UVs; the moved faces carry 8.469 m2 per UV unit2 against 6.559 before (ratio 1.291, required 1.15), and `unwrap`'s side stretch rises from 1.0927 to 1.3735. Nine undos (unwrap and the moves): `get_mesh` equal to before and the checker front byte-equal. The top cap extruded 0.2: the 8 new walls are magenta (at least 12 samples), the cap keeps its UVs, `check` lists `faces[34]` to `faces[41]` as no UVs, and `export` warns `Barrel: 8 faces have no UVs; their corners are written as (0, 0)` with nothing of `Crate`; undone, `get_mesh` equal and the front byte-equal.
- **Save and open.** `save` both meshes, `open`: `get_mesh` of both equal as text; the barrel checker front, barrel layout 512, crate layout and crate checker front retaken after, each byte-identical. Then the sample opened and compared (ruling 1), the saved file opened again, and `export` of `content/models/barrel.glb` with no warning.

**Break table** (`artifacts/review/T-61/broken-product.md`; each planted on the committed tree and restored; the first failure):

| Planted break | Fails |
|---|---|
| texture uploaded with v flipped (`CheckerTexture`) | `barrel-checker-front`: a light cell no brighter than the dark below it |
| corner UVs rotated by one (`LookMeshes`) | `barrel-checker-front`: face 8 cell (1, 1) reads column 2's hue |
| `unwrap` ignoring seams (`UnwrapIslands`) | refused at `unwrap`, the scenario fails there |
| layout drawn v-up (`UvLayout`) | `barrel-uvs-256`: a dim ground pixel is wrong |
| mirrored camera (`CameraPixels`, x negated) | `barrel-checker-front`: face 0 cell (1, 0) reads column 4's hue |

**Guard.** `artifacts/review/T-61/hash-guard.md`: the latest queued frames and files of scenario-cube, -crate, -shed, -export and -character at the base (162 files, taken before any code changed) against the verify run: every frame and file is byte-identical; only logs, `run.json` and the two `export-result.txt` differ, by a work or temp path.

**For T-62.** Plant on `samples/barrel.carve` (a seam removed makes `unwrap` refuse). `checker-samples.txt` lines carry the pixel, the colour and the cell expected. The seam precondition above is the one check not made.

**Review.** Round 1 (2026-11-14, Waystone, `G:\My Drive\artifacts\carve\M-6\T-61\round-1`, 16 frames): approved, no notes. I looked at three montages of the frames myself: the four barrel views read as predicted (white cell low left on the seam side), the layouts show the two caps beside the strip with yellow seams, the moved barrel bulges with stretched cells and the extruded cap shows a magenta band. Not looked at by eye: the repeats (byte-equal by hash), the sheet in its final form, the 1024 layout.
