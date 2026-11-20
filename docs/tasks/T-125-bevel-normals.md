---
description: >-
  bake writes a tangent-space normal map, glTF green up in the engine's own frame, that rounds every shading-break edge over a given width
---
# T-125 — Bevel normals

**Status:** In Progress

**Pri:** P1

**Milestone:** M-24

**Batch:** — (standalone)

**Depends on:** T-123

**Verify:** core-unit, server-unit, server-regression, scenario-bakes

## Goal

`bake` writes a normal map that rounds the wrench's hard edges by a width in metres, as fps-arms' 1.6 mm bevel did.
Render shows a rounded edge the way the game will, because the map is written in the frame the engine reads.

## Scope

- **Core, `src/Carve.Core/Baking/BevelNormal.cs`:**
  - for each covered texel, start from the interpolated shading normal N, as render lights it (T-120);
  - for each break edge within `width` from T-120's `NearEdges`, the target is `normalize(nFace + nOther)`, the mean
    of the two faces' normals, with weight `w = smoothstep(width, 0, d)` (1 on the edge, 0 at `width`);
  - the bent normal is `normalize(N + Σ wᵢ · (targetᵢ − N))`, the edges taken in a fixed order;
  - it is encoded with T-120's `ToTangentSpace`, `127.5 + 127.5 * n`, RGB;
  - a texel with no edge within `width` encodes N itself, which is (128, 128, 255) or one level from it;
  - padding uses T-120's `Pad`.
- **`bake`:** a new map `normal` (`<mesh>.normal.png`) and a new block `bevel: {width}`. `width` is in metres, greater
  than 0, and required when `normal` is asked for, with no default. The description gains a paragraph: what the map
  rounds, that it is glTF green up, and that it goes in `normal_texture`.
- **Reconciled against render** (a `regression` test in `tests/Carve.Server.Tests`; this is the probe at planning made
  permanent):
  - bake `normal` with width 0.1 on a unit box, with UVs laid so that the +Z face is `FrontUvs`'s;
  - render it from the front with the map as `normal_texture`;
  - the +Z face's strip within 0.05 of its top edge reads brighter than the face's middle, leaning toward the top
    face's shade, and the strip by its +X edge reads darker, leaning toward +X's;
  - breaks: the same map with G inverted, and then with R inverted, each fails;
  - the margins are worked from the probe's numbers in T-120's Notes and the colour rule, and brought to the person
    if they cannot be.
- **Stage `BevelStage.cs`** in the Bakes scenario:
  - bake `normal` with width 0.0016 at 1024 (5 texels at the wrench's 3.1 texels per mm), and add it to the material;
  - **frames:** the `uvs` look with the normal map under it; zoom 8 on a convex jaw edge from three directions, each
    with and without the normal map; the wrench from the six sides;
  - **checks**, each with a break:
    - in the PNG, at texels on a hard edge's two sides, both decode, in world space through the frame, to within a
      probed angle of the edge's mean normal, across a UV seam. Break: width 0.00001;
    - texels farther than 0.0016 from every break edge decode to N;
    - in the close frames, the edge strip's shade lies between the two faces' shades, as the render test above
      predicts. Break: G inverted.
  - Every number is probed and brought to the person before landing.
- **Tests** (`unit`):
  - a box's top edge: the texel on the edge decodes to (0, 0.707, 0.707), the mean, and a texel at `width` or farther
    decodes to the face normal;
  - a corner texel near two edges;
  - a concave L-block edge;
  - a mirrored island;
  - two bakes byte-identical;
  - each error.

## Notes

- Decided at planning (2026-11-18), by the person: rounded edges are shading breaks (the marked hard edges of a smooth
  mesh, every non-coplanar edge of a flat one); at the edge the normal is the mean of the two faces', easing by
  smoothstep to the shading normal at `width`, measured in 3D; the frame is the engine's, green up, with no tangents
  exported.
- Chosen by the orchestrator, each a stop if it proves wrong: summing the bends of several edges; `width` having no
  default.
- **Stop and ask** before landing: a frame or sign other than the probe's, any threshold, or a shading difference
  between render and `render_glb` that the map introduces.
- Opus: the frame and the reconciliation with render are where this goes wrong, and the stage is a look stage, so plan
  for two agents.
- Runs beside T-124 and T-126. All three add a map and a block to `bake`, a paragraph to its description, a stage file
  and a line in `BakesStages.cs`; keep both sides. It shares T-120's `NearEdges` with T-126 and changes neither it nor
  the frame. A change to either is a stop, since a sibling relies on it.
- The person looks at the close frames before this lands (a Waystone review). Treat "Commented" as unanswered.
- **Verify line:** `server-regression` is named because the reconciliation test renders. Once scenario-bakes is in
  `tests.json`, add it to this file's `**Verify:**` line.

### Hand-off

Agent 1, Claude Opus 5.5 (2026-11-19), handing off at the call checkpoint. Commits `efb8e31`, the render test, `90c8498`.

**Done**

- `src/Carve.Core/Baking/BevelNormal.cs`: `Bake.BevelNormal(layout, width)` (3 channels, `ToTangentSpace / 255`, uncovered 0)
  and `Bake.BevelWorldNormal(texel, edges, width)` as Scope says: in double, edges in `NearEdges`' order, smoothstep, a sum
  that cancels gives N. `NearEdges`, the frame and the layout are unchanged. `BakeLayoutTests.Celled` went from private to
  internal so the new tests could reuse it.
- `tests/Carve.Core.Tests/BevelNormalTests.cs` (10, unit): the top edge's mean (0, 0.707, 0.707), N at or beyond the width,
  a corner summing two edges, the L-block's concave edge, a mirrored island (red flips, green kept, the same world normal),
  byte-identical bakes, each error.
- `bake`: map `normal` (`<mesh>.normal.png`, RGB), block `bevel: {width}`, the description paragraph, the schema. `maps`
  left out still means masks and base_colour (`DefaultMaps`), since `normal` has no default width. **Chosen here, and a
  stop if it is wrong:** `bevel` without `normal` is refused, the way render refuses `image` with another mode.
  `tests/Carve.Server.Tests/BakeNormalToolTests.cs` (11, unit).
- `tests/Carve.Server.Tests/BevelRenderTests.cs` (regression): the reconciliation, with frames and `bevel-readings.tsv` in
  `artifacts/bevel-tests/`. The other faces get a grey #808080 material with no map, so the top and +X shades are like
  for like. Without it they render white.
- `scenarios/Carve.Scenario.Bakes/BevelStage.cs` and its line in `BakesStages.cs`: the bake, the material, 14 frames, the
  N check, the strip check, the glb comparison and both breaks wired (`bevel-thin`, `bevel-g`). The `CLAUDE.md` row clause.
  `scenario-bakes` is on the Verify line. `tests.json` is unchanged: the stage is inside the suite's `dotnet-closure`.

**Probed** (on this machine; evidence in `G:\My Drive\artifacts\carve\M-24\T-125\probe`)

Render test, front view, mean Rec. 709 luma:

| map | middle | top strip | +X strip | top face | +X face |
|---|---|---|---|---|---|
| flat | 117.54 | 117.52 | 117.49 | 127.76 | 116.53 |
| baked | 117.54 | 125.74 (+8.20) | 113.22 (−4.33) | 127.76 | 116.53 |
| G inverted | 117.47 | 107.15 (−10.33) | 113.12 | | |
| R inverted | 117.67 | 125.75 | 119.61 (+1.94) | | |

**Surprising: the +X strip overshoots +X's shade.** The mean normal reads darker than either face: 113.2 against 116.5
and 117.5. T-120's probe already says so, since R+ is that normal and read 114.3. So "leaning toward +X's" fails as
written. The test currently checks "darker by `SideLoss`" only.

Options for the person (each names the one check it applies to):

| check | option A | option B | option C |
|---|---|---|---|
| render test, top strip (`TopGain`, now 4.7 provisional) | half the probe's edge gain, 4.7 | 4 | another number |
| render test, +X strip (`SideLoss`, now 1.6 provisional) | darker by ≥ 1.6 and drop "toward +X" (the probe puts the mean below both faces) | within 2 of the probe's R+ reading, 114.3 | another rule |
| stage, strip between the faces (`BetweenSlack`, now 0) | 0, as written | a slack worked from the close frames once sampled | judge only edges where the colour rule puts the mean between |

Stage, run 1 (`artifacts/scenarios/bakes/1`): N check 448 texels, all flat within 1 level. `render_glb` against `render` with
the map: front 47 px differ (patch 13), three-quarter 15 px (patch 5). Both pass D-6's rule, against stage 1's 48 (15) and
10 (3) without the map. So the map introduces no shading difference past the rule. The person should still see the
three-quarter rise from 10 to 15.

**Left**

1. **The strip check samples nothing.** On jaw edge (112, 113), faces 66 and 78, only face 66's 3 × width point in view 2
   got a reading (`bevel.tsv`); every other point fails `Geo.Inside` or `Geo.InSight`. Likely causes: the edge is short,
   so the points at ±0.15 of its length sit near the face corners; or the ray test hits the neighbour face for a point
   0.4 mm from the edge. Count the two failures apart, then fix the sampling. The frames `bevel-jaw-{1,2,3}[-plain].png`
   are in `probe\frames` and have not been opened yet.
2. **The PNG check across a seam (Scope's first check) is not written.** It needs the engine's frame at a texel, scenario
   side: the angle-weighted smooth corner normals (`Shading`) and `Triangulator`'s triangles for T and B. `get_mesh` gives
   neither normals nor hard edges; the stage reads the hard edges from `wrench.carve`. One option is to write the stage's
   own restatement. Another is to put this check in `tests/Carve.Core.Tests` on the wrench (as `WrenchSampleTests` does),
   with the layout's own frames, and ask the person whether that place will do. Its break is `bevel-thin`. The probed angle
   then goes to the person.
3. Run both breaks and record the failures they cause. Bring all the numbers above. Then the Waystone frame review
   (`round-N` under the evidence folder), verify, As built, `cairn lint`, and `finish`.

**Not checked:** no frame has been opened (0 of 12 images). No queued run has been made. `server-regression` has run
only the new test, not the whole suite.
