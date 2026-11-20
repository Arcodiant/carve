---
description: >-
  bake writes a tangent-space normal map, glTF green up in the engine's own frame, that rounds every shading-break edge over a given width
---
# T-125 — Bevel normals

**Status:** Done

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

### As built

Agents 1 to 3 Claude Opus 5.5 (Core `BevelNormal`, `bake`'s `normal` map and `bevel` block, `BevelRenderTests`, `BevelStage`,
the probes); agent 4 Claude Opus 5.5 (the rebase onto T-124, the strip ruling landed, the runs, the review, finish).

- **Core:** `src/Carve.Core/Baking/BevelNormal.cs` as the Scope says, on T-120's `NearEdges`, `ToTangentSpace` and `Pad`,
  neither changed. Unit tests in `BevelNormalTests`; the seam check is the Core `unit` test `WrenchBevelSeamTests`.
- **`bake`:** map `normal` (`<mesh>.normal.png`, colour, glTF green up), block `bevel: {width}` required with `normal`, no
  default. A block given without a map that reads it warns and is ignored (still validated): one `UnusedBlock` helper for
  `occlusion` (T-124), `bevel` and `curvature`; its single-reader wording now reads "'normal' is not in maps". The
  description's worked example stays T-124's at size 512.
- **Stage:** `BevelStage.cs`, the strip check on the steel grip's edge (72, 74), faces 42 and 46, predicted with `#80858A`.

**The person's rulings (2026-11-20):** `TopGain` 4.7 and `SideLoss` 1.6 in `BevelRenderTests`, with "toward +X" dropped; the
seam check as a Core test with `SeamAngle` 9 degrees (probed largest 8.33, the thin-width break's least 32.4); the stage's strip
check on the steel grip, not the dark jaw; `bevel-thin` dropped from the stage (the Core seam test's thin break covers it);
unused blocks warn; and **every strip in every grip view reads within 2 luma (`PredictedSlack`) of the colour rule's predicted
shade for the mean normal, the "between the two faces" reading dropped**.

**Numbers after the rebase** (queued job `J-20261120-033403-db21c3` good, local run `bakes/9` bevel-g; same as the pre-rebase
probe), strip less predicted mean-normal shade 126.96, luma:

| view | good | bevel-g |
|---|---|---|
| grip-1 | -0.77, +0.07 | -11.89, -8.14 |
| grip-2 | +0.18, +0.25 | -8.21, -10.14 |
| grip-3 | -1.00, +1.00 | -12.14, -5.21 |

Good at most 1.00 off; bevel-g at least 5.21 off. **Breaks:** bevel-g fails 6 of 6 strip checks; `BevelRenderTests` fails G
inverted and R inverted; the seam test's thin width fails (least 32.4 degrees).

**`render_glb` against `render` with the map** (D-6's rule): before T-124's mipmap fix front 47 px (patch 13), three-quarter
15 px (patch 5); after it front 0 px (patch 0), three-quarter 1 px (patch 1). The map introduces no difference.

**Review:** round 1 (`G:\My Drive\artifacts\carve\M-24\T-125\round-1`, 19 frames: the `uvs` normal look, grip close frames with
and without the map, six sides, the box render test's four frames, the two compare triptychs): **approved**, no notes.
