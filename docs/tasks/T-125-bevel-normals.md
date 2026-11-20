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
