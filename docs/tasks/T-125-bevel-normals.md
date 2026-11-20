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

Agent 3, Claude Opus 5.5 (2026-11-20), handing off at the T-124 gate: **T-124 has not landed** (`origin/milestone/M-24` tip
`52d3089`, fetched twice), and the brief puts the `render_glb` re-probe and the frame review (one request with the strip
tolerance question) after it. Agents 1 and 2 were Claude Opus 5.5.

**Done, from earlier agents** (commits `bd1ec56`, `2b74618`): the person's rulings `TopGain` 4.7 and `SideLoss` 1.6 in
`BevelRenderTests`; unused `bevel`/`curvature` blocks warn through `UnusedBlock` in `BakeTool.cs` (T-124's `occlusion` joins it
in the rebase: keep all three); the seam check as the Core `unit` test `WrenchBevelSeamTests`; the close-edge rule (longest
convex hard edge of the group whose sample points are on their face, facing the eye, in sight from all three close directions).

**Done, agent 3: the person's rulings of 2026-11-20 on agent 2's option table, landed**

- **Seam check:** `SeamAngle` 9 degrees, as ruled (probed largest 8.33; the thin-width break's least 32.4).
- **Stage strip check moved to the steel grip.** `JawEdge` is now `CloseEdge(group)`, the same rule, given the faces that are
  `steel` and in no later group of SurfaceStage's table (dark, red, black); predicted shades use `#80858A`. Chosen edge
  **(72, 74), faces 42 and 46**. Close frames `bevel-grip-1..3`, each `-plain` and with the map. **The jaw close frames are
  gone** (their readings are in agent 2's probe, kept in the table below).
- **`bevel-thin` dropped from the stage** and from CLAUDE.md's break list; the Core seam test's thin-width break covers it.

**Probed, grip edge (72, 74)** (local runs `artifacts/scenarios/bakes/7` good, `8` bevel-g; pre-T-124 renders), luma; `pm` the
colour rule's mean-normal shade:

| view | faces measured | faces predicted | pm | rule | strips good | less pm | strips bevel-g | less pm |
|---|---|---|---|---|---|---|---|---|
| grip-1 | 121.96, 123.98 | 121.74, 123.96 | 126.96 | outside | 126.19, 127.03 | -0.77, +0.07 | 115.07, 118.82 | -11.89, -8.14 |
| grip-2 | 125.67, 121.74 | 124.96, 121.03 | 126.96 | outside | 127.14, 127.21 | +0.18, +0.25 | 118.74, 116.82 | -8.21, -10.14 |
| grip-3 | 119.03, 126.96 | 118.82, 126.96 | 126.96 | between (pm on face B's shade) | 125.96, 127.96 | -1.00, +1.00 | 114.82, 121.74 | -12.14, -5.21 |

Good: largest |strip - pm| 1.00. bevel-g: least 5.21. So **a tolerance of 2 against `pm`** (`PredictedSlack`, ColourRule's own
of 2026-11-14) passes the good run with 1.00 to spare and fails every bevel-g strip by at least 3.21. **The "between" rule fails
the good run** on grip-3: strip B 127.96 is 1.00 above face B's 126.96, because the rule puts `pm` exactly on face B's shade;
bevel-g's grip-3 strip A (114.82) is 4.21 below face A, strip B (121.74) lies between.

As the code stands, the good run fails that one check (`bevel-grip-3: strip B reads 127.96, not between 119.03 and 126.96`) and
bevel-g fails five; nothing is landed for the tolerance. **Proposed for the review request** (one option set, for the grip
strip check only):

- A (proposed): every view held to `pm` within 2, the "between" reading dropped (pm sits on a face's shade where it is "between").
- B: `pm` within 2 where the rule puts the mean outside; "between" with a slack of 1.25 luma where it puts it between.
- C: another number.

**Left**

1. When T-124 lands: `git rebase origin/milestone/M-24` (keep `occlusion`, `bevel`, `curvature` in `UnusedBlock`; keep both
   sides in `BakesStages.cs` and the Bakes row), rebuild, rerun good and bevel-g, re-read the table above and `render_glb`
   against `render` with the map (pre-T-124: front 47 px, patch 13; three-quarter 15 px, patch 5).
2. Review round 1 via `mcp__waystone__review`: the option set above plus the `render_glb` numbers, and frames under
   `G:\My Drivertifacts\carve\M-24\T-125ound-1`: `wrench-uvs-normal`, `bevel-grip-1..3` with and without the map, the six
   sides, `BevelRenderTests`' frames. Land the tolerance as answered.
3. `verify`, As built (rulings with the person's earlier ones, numbers, break counts, the review, agents 1 to 3 Opus 5.5),
   `cairn lint`, `finish`.

**Agent 2's jaw probe, for the record** (edge (124, 125), faces 66 and 79, `#36393E`): strips less `pm` good largest 1.18,
bevel-g largest 1.66, bevel-thin largest 1.59; on jaw-3 the good strip read 0.11 outside "between". That is why the check moved.

**Not checked:** no frame opened (0 of 12 images in this agent). No queued run. core-unit and server-unit not rerun (agent 3
changed a comment in Core's test and the stage only; the scenario built and ran). `verify` not run (the good run fails the
pending check by design).
