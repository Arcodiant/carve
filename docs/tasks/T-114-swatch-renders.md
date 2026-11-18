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
