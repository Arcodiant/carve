---
description: >-
  patina's swatch: the sheet, flat, tiled 3 x 3 and a sphere through Material.Pbr under Carve's fixed sky
---
# T-114 — Swatch renders

**Status:** In Progress

**Pri:** P1

**Milestone:** M-27

**Batch:** — (standalone)

**Depends on:** T-112, T-113

**Verify:** patina-unit, patina-regression, scenario-patina

## Goal

An agent looks at the open graph with `swatch`: a sheet of every output flat and tiled 3 x 3, beside a sphere wearing
all of them through `Material.Pbr` under Carve's fixed sky, or any one of those views alone.

## Scope

- **`swatch`** on patina. Arguments: `view` `sheet` (default), `flat`, `tiled` or `sphere`; `output` (a role, for
  `flat` and `tiled`); `size`, the evaluation size, a power of two from 32 to 2048 (default the graph's size, capped
  at 2048); `angle`, the sphere's turn about y in degrees (default 0). It refuses while the graph has a fault, and
  above 2048 it says to use `export`.
- **The sheet**, as agreed at planning:

  ```
   planks.texgraph   256 px   seed 7
  +------------+------------+------------+------------+-------------------------+
  | base_colour| normal     | roughness  | occlusion  |                         |
  |            |            |            |            |        .-'''''-.        |
  |   flat     |   flat     |   flat     |   flat     |      .'         '.      |
  |            |            |            |            |     /   sphere    \     |
  +------------+------------+------------+------------+    |  Pbr, fixed   |    |
  |+--+--+--+  |+--+--+--+  |+--+--+--+  |+--+--+--+  |     \   sky      /     |
  ||  |  |  |  ||  |  |  |  ||  |  |  |  ||  |  |  |  |      '.         .'      |
  |+--+--+--+  | tiled 3x3  | tiled 3x3  | tiled 3x3  |        '-.....-'        |
  +------------+------------+------------+------------+-------------------------+
    cells 256 x 256; sphere cell 512 x 512; a 24 px title strip on top
    columns are the graph's outputs, in role order; a missing role has no column
  ```

  Flat and tiled cells are composed on the CPU from the evaluated texels: flat scaled to 256 (nearest), tiled as the
  3 x 3 repeat scaled to 256. Labels are drawn as Carve's contact sheets draw theirs (reuse, don't add a font).
- **The single views:** `flat` at `size` px; `tiled` at 3 x `size`, scaled down to at most 1536 px; `sphere` at
  512 px. Flat and tiled are exact texels (no engine). The sphere is the engine's `PrimitiveMesh.Sphere` (a UV
  sphere of radius 0.5, Y-up; u once around), drawn with `Material.Pbr` built from the outputs: base colour sRGB,
  the roughness image as metallic-roughness, normal and occlusion linear, uploaded through `Carve.Hosting`'s rules
  (mipmaps on). It is lit by `Carve.Hosting`'s fixed sky alone, on Carve's dark slate, from a fixed camera. The
  engine builds the normal map's frame from screen derivatives, so the sphere needs no tangents (probed in the engine
  docs at planning). A missing role uses the material default.
- **Scenario.** `SwatchStage.cs` in `Carve.Scenario.Patina`, plus one line in `PatinaStages.cs`. Build the planks
  through the tools, and check `get_graph` equals `samples/graphs/planks.texgraph` by content. Frames: the sheet at
  256 and at 64; flat `normal` at 256; tiled `base_colour` at 128; the sphere at `angle` 0, 90 and 200. Then a graph
  with a seam (a `noise` whose wrap is broken by a scenario-only break switch, as M-7's `CARVE_MATERIALS_BREAK`)
  must fail its tiled check. `scenario-patina` becomes `gpu`.
- **Checks worked from the evaluated texels and the geometry.** Flat and tiled are compared texel for texel. For each
  sphere pixel, a ray to the radius-0.5 sphere gives (u, v), and so the stave or gap it shows. Gaps must read dark,
  stave middles must read in a band, the silhouette must be where the camera puts it, and turning by 90 degrees must
  move the staves by a quarter of the repeat. **Say first what the sphere check can predict:** which texel and which
  side of a gap, not a lit shade. Name the shared check rules before writing them (M-5, M-6): thresholds for
  "dark", "in band" and the edge margin at the silhouette and at gaps under mip filtering. Probe the thresholds on
  the planks' own colours (M-7), and put them to the person before landing.
- Each new check is shown failing on a broken product (wrong role order, the sphere's u flipped, tiled shifted half
  a tile, roughness bound as base colour): a table of breaks and what caught each.

## Notes

- Decided at planning (2026-11-17, by the person): the sheet layout above; the single views; swatch capped at 2048.
- **Draws many new frames, so plan the frame review as its own agent** (M-6, M-7): the implementer stops at frames
  taken and checks passing, and a fresh agent opens every frame itself (cropped or scaled), then sends the set to
  the person. Treat Waystone's "Commented" as unanswered until the person says approved.
- Opus: the sphere's checks are design work.
- Must not edit `Carve.Hosting` or `Carve.Server`. If the sphere render needs more from the host than T-111's As
  built gives, stop.
- Adds to `Program.cs`'s tool list, `PatinaStages.cs`, the README's patina section and `tests.json`. T-115 runs
  beside it and adds to the same four: a conflict there only adds, so keep both sides.
- There is no sound or motion, so there is no real-time pass. The person's look at the frames is the approval.
- **Verify line:** at planning, `cairn lint` accepts only suites already in `tests.json`, so this file names none it will add. Once patina-unit and scenario-patina (T-113 adds them) are in `tests.json`, add them to this file's `**Verify:**` line (creating the line if there is none), with the task's other suites.

### As built (Opus 5.5)

Built by Claude Opus 5.5 (`claude-opus-5-5`), first agent, to the frame-review checkpoint.

- **`swatch`** in `src/Patina.Server`: `SwatchTool.cs` (arguments, refusals, evaluation, captions), `SwatchImages.cs` (CPU:
  nearest scaling at the pixel centre, flat, tiled, `SheetLayout`, `LabelAtlas`, `SwatchSheet.Compose`),
  `SwatchRendering.cs` (`SwatchComposition`: a 512 sphere target and a 512 label atlas target through
  `OffscreenTarget`; `SwatchSystem`: the scene, the sky entity, the camera, `PrimitiveMesh.Sphere` with a `Material.Pbr`
  of `TextureUpload` textures, held by an `IAssetHolder` and released at the next request; `SwatchCamera`; `SwatchBreak`).
  `Program.cs` adds the tool line, the queue in `AddTools`, and the pipeline and system in `AddServer`. Nothing in
  `Carve.Hosting` or `Carve.Server` changed: T-111's API was enough.
- **Sheet**: 4 x 258 + 512 = 1544 x 538 for the planks (2 px slate gutters; the sphere cell 1 px below the title strip).
  Labels are the engine's font (`surface.Text`, as carve's labels), drawn white on black into a 512 atlas one row each
  and composited on the CPU as coverage over carve's black 0.6 backing (`LabelLayout`'s colours, repeated, since patina
  may not reference `Carve.Server`). Labels sit in each cell's top-left corner, so the cell checks leave out the top 22
  rows (the label band).
- **One surprise:** writing only `Transform3D` turned the sphere a frame late (the transform system had already run that
  tick), so each sphere showed the previous request's angle. `SwatchSystem` writes `WorldTransform` too, as carve does
  for its cameras.
- **The sphere's UVs**, read from the engine (ilspy of `Empyrean.Geometry` 0.129.0, `PrimitiveMeshData.Sphere(32, 16)`):
  u = sector / 32 from +x toward +z, v = stack / 16 from the top pole; `SwatchSphere.cs` restates that mesh and traces
  each pixel to its triangle, so the prediction is the faceted mesh, not an ideal sphere.
- **What the sphere check predicts**: for each pixel, the texel it shows (and so stave or gap) and how far mip filtering
  can blur it; never a lit shade. The rules, named in `SwatchStage`'s remarks: *exact*, *seamless* (mean step across
  tile boundaries at most 3 x the mean inside, + 1), *gap / stave texels* (base red < 90 / >= 120), *edge margin*
  (every ray within 1.5 px hits, facing cosine >= 0.5, every texel within the footprint + 1 of one class), *dark* (luma <=
  52), *in band* (luma 58..110), *silhouette* (all-miss pixels are slate within 1; at most 0.1 % of all-hit pixels slate),
  *stave order* (between two angles, at the same pixels, the lighter-reading stave is the one with lighter texels,
  for stave pairs whose texel tones differ by 3 % or more).
- **Probed on the planks** (`swatch-checks.tsv` of every run): gap pixels luma at most 31.7 / 36.7 / 39.6 (angles 200 /
  0 / 90); stave pixels 70.2 .. 87.5; inside pixels reading slate 9, 6, 0 of about 139 600 (dark lower-limb gap pixels,
  1 level off the slate, which is why that rule is a fraction). The thresholds 52 and 58..110 leave about 12 levels each
  side. **To put to the person before landing** (the Scope's stop): those thresholds, the label band, and the
  fraction rule for the silhouette.
- **Breaks** (`PATINA_SWATCH_BREAK`, read once in `SwatchBreak`; unset changes nothing), each run of `scenario-patina`
  with it set, all caught (`artifacts/T-114/breaks.txt` in this worktree):

  | Break | Caught by |
  |---|---|
  | `role-order` (sheet columns reversed) | sheet flat and tiled *exact*, all 16 cells at 256 and 64 |
  | `u-flip` (textures mirrored before upload) | *stave order* 0 vs 200 (2 of 4 pairs) and 90 vs 200 (1 of 2); the gaps are symmetric, so only the tones see it |
  | `tiled-shift` (half a tile) | tiled 128 *exact*, sheet tiled cells, and the seam graph's *seamless* (the seam moved inside the tile) |
  | `roughness-as-base` | *dark* and *in band* on all three spheres, and *stave order* |
  | the seam graph (noise mixed 0.7 with a once-across ramp) | *seamless* fails, as it must: the scenario asserts it does |

  The seam is always run, not behind a switch: the scenario-only "break" is the graph itself, since a `noise` cannot be
  made not to wrap without editing the node.
- **Tests**: `SwatchTests` (11 + 11 refusal cases, harness with `EngineStatus.Faulted`, so flat and tiled are tested
  with no GPU and the sphere and sheet are refused with the fault), the worked example, the tool lists in
  `GraphToolTests`, `PatinaProcessTests` and `GraphStage`. `scenario-patina` is now `gpu` (and reads
  `samples/graphs/**`); `patina-regression` stays `cpu`: its process test only lists tools, nothing renders.
- **`CLAUDE.md`**: the `Patina.Server` Layout row now lists `swatch` (the person approved it in the round-1 review).
- **Frame review** (Claude Opus 5.5, `claude-opus-5-5`, the frame-review agent), of job `J-20261118-100559-862909`
  (hashes equal to the local run's `artifacts/scenarios/patina/5/frames/`). Every frame opened whole:
  - `swatch-sheet-256` (1544 x 538): title strip `planks.texgraph   256 px   seed 77` (the graph's seed; the planning
    sketch's "seed 7" was a placeholder). Columns in role order: base_colour, normal, roughness, occlusion, each label
    in its cell's top-left on the dark backing; flat above, `<role> 3 x 3` below; 2 px slate gutters. The tiled cells
    show 24 staves with even gaps and no seam at the thirds. Sphere cell on the right, labelled
    `sphere, Pbr, fixed sky, angle 0`, on the slate.
  - `swatch-sheet-64`: the same layout; the cells are 4 x nearest blocks of the 64 texels; the normal is fainter (the
    bevel is about one texel at 64), the sphere softer. Nothing out of place.
  - `swatch-flat-normal-256`: eight bevels; sampled, the texels left of a gap (x 28..30) read red 183..196 (+x tilt) and
    those right of it (x 1..2) red 59 (-x), green 128 throughout: the bevel falls into the gap from both sides.
  - `swatch-tiled-base-128` (384 x 384): 24 staves, gaps one width, no visible seam on either axis.
  - `swatch-sphere-0`, `-90`, `-200` (512): the faceted 32-sector UV sphere, centred, whole, on the slate; staves brown
    with darker gaps (a bluish sky rim along the gap bevels), lit evenly with a little darkening toward the limb and the
    lower half; no highlight to speak of (the planks' roughness is high and narrow). 0 and 90 put the gaps at the same
    pixels, as a quarter turn is two staves; they differ only in the staves' tones (55 357 pixels past 4 levels, at most
    12), which is what *stave order* reads. 200 moves the gaps by 4 4/9 staves: two gaps either side of the centre
    instead of one on it.
  - `swatch-tiled-seam`: grey noise with a hard dark-to-light step at each tile column (x 128, 256) and none across the
    rows: the visible seam the *seamless* check must fail on.
- **The person's review** (round 1, `G:\My Drive\artifacts\carve\M-27\T-114\round-1`, Waystone, 2026-11-18):
  **approved**, frames and the open point as put: *dark* luma <= 52, *in band* 58..110, the 22-row label band left out of
  the exact check, the silhouette's 0.1 % of inside pixels as slate, and the `CLAUDE.md` row. Note on the whole: "Looks
  fine, but the roughness isn't very apparent on the sphere renders" (not a change asked; the planks' roughness texels
  sit in a narrow high band, so the sphere is matte all over).
- **Rulings by the person** (via the orchestrator, 2026-11-18): `PATINA_SWATCH_BREAK` / `SwatchBreak` must move out of the
  patina product to the scenario or test side (CLAUDE.md's no-debug-in-product rule); no engine need for the sphere's UV
  mapping, `SwatchSphere.cs` stands.

### Hand-off

Frame-review agent (Claude Opus 5.5, `claude-opus-5-5`). **Left, first:** the person's ruling that `PATINA_SWATCH_BREAK` /
`SwatchBreak` moves out of the patina product (`src/Patina.Server/SwatchRendering.cs` and the places it is read) to the
scenario or test side, keeping every break of the table above caught; not done here. After it, `cairn task T-114 verify`
and `finish` (not run by this agent, as the orchestrator asked).

Done: all 8 frames of `J-20261118-100559-862909` opened and described above; round 1 sent and **approved** by the person,
with the thresholds, the label band and the silhouette fraction; `swatch` added to the `CLAUDE.md` Layout row (approved
in the same review). Rebased onto `origin/milestone/M-27` (T-115's `export`): conflicts in `Program.cs`'s tool list,
`PatinaStages.cs`, `GraphStage`, `GraphToolTests`, `PatinaProcessTests` and the README, each resolved keeping both, the
tool lists now exact: the seven, then `export`, then `swatch`. `verify` after the rebase: VERIFY PASS (patina-unit, server-unit, core-unit, scenario-patina, patina-regression on Mantorok). Ready to finish once the break switch has moved.

Not checked: the frames after the rebase were not opened again (the rebase touched no swatch code). The
`CLAUDE.md` row does not list T-115's `export` either; left to T-115's owner. The person noted the roughness is not
very apparent on the sphere (the planks' roughness is high and narrow); approved as is.
