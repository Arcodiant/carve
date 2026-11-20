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

Agent 2, Claude Opus 5.5 (2026-11-20), handing off at the T-124 gate and on a stop. Agent 1 was Claude Opus 5.5. Rebased onto
`origin/milestone/M-24` with T-126 in (conflicts in `BakeTool.cs`, `BakesStages.cs` and the Bakes row of `CLAUDE.md` only
added; both kept, built clean). **T-124 has not landed** (`origin/milestone/M-24` tip `52d3089`), so the final numbers and the
frame review wait for it, as the brief says. Commits `bd1ec56`, `2b74618`.

**Done**

- **The person's rulings (2026-11-20), landed:** `TopGain` 4.7 and `SideLoss` 1.6 in `BevelRenderTests`, "leaning toward
  +X's" dropped; a `bevel` block without `normal` in maps, and a `curvature` block without `curvature`, bake what was asked and
  warn (`bevel: ignored, 'normal' is not in maps`), through one helper `UnusedBlock` in `BakeTool.cs` (T-124 may add
  `occlusion` to it; keep both in the rebase). Two new cases in `BakeNormalToolTests`; the refusal case removed. A malformed
  unused block is still an error.
- **Strip sampling fixed.** Counted apart on edge (112, 113): 0 of 48 points failed `Geo.Inside`; all 48 failed line of sight,
  hidden by the ring head's faces 48, 49 and 64 at t 0.97 to 0.99. The edge is the dark boss's underside (face 78, facing −z
  at z 0.0135), 0.5 mm over the ring's top: buried from every direction. `JawEdge` now takes the longest convex jaw edge whose
  sample points are all on their face, facing the eye and in sight from all three close directions: (124, 125), faces 66 and 79.
  A face shallower than six widths reads its shade at half its depth (face 66 is 5.5 mm deep, so 4.8 mm sat in its far edge's
  strip). `Samples` is shared by the choice and the reading.
- **Seam check** as a Core `unit` test, `tests/Carve.Core.Tests/WrenchBevelSeamTests.cs`, as ruled: texels within a quarter
  width of a hard seam edge and within width of no other break edge, decoded from bake's bytes through their own frame.
- core-unit (1352) and server-unit (970) pass locally. `verify` not run: scenario-bakes fails on the stop below.

**Probed** (local runs `artifacts/scenarios/bakes/4` good, `5` bevel-g, `6` bevel-thin; pre-T-124 renders)

Seam test: 212 seam edges read on both sides, largest 8.33° from the mean; at width 0.00001 every side's largest is at least
32.4° (60.1° at most). `SeamAngle` is 9, provisional.

Close jaw frames, edge (124, 125), luma; `pm` the colour rule's mean-normal shade (`ColourRule.Lit`, `#36393E`):

| view | faces measured | faces predicted | pm | rule | strips good | strips bevel-g | strips bevel-thin | plain |
|---|---|---|---|---|---|---|---|---|
| jaw-1 | 58.78, 59.52 | 58.59, 59.52 | 59.66 | outside | 60.40, 60.37 | 58.00, 59.38 | 58.78, 59.52 | 58.77, 59.52 |
| jaw-2 | 59.39, 59.45 | 58.59, 58.52 | 59.66 | outside | 60.70, 59.66 | 58.32, 60.61 | 59.37, 59.45 | 59.37, 59.45 |
| jaw-3 | 57.96, 60.45 | 57.59, 59.52 | 59.37 | between | 60.56, 59.66 | 58.23, 59.59 | 57.78, 60.45 | 57.77, 60.45 |

Strip less `pm`: good 0.74, 0.72, 1.04, 0.00, 1.18, 0.28 (largest 1.18); bevel-g −1.66, −0.28, −1.34, 0.95, −1.14, 0.22
(largest 1.66); bevel-thin largest 1.59.

**Stop: the stage's strip check, as ruled, cannot tell the map from its break.** The jaw is #36393E steel and the faces' shades
are within 3 luma of each other, so every reading is within 2 luma. (a) The good run fails: on jaw-3, where the rule puts the
mean between, strip A reads 60.56, 0.11 above face B's 60.45. (b) Any tolerance against `pm` that passes the good run (1.18 or
more) also passes bevel-g (1.66 at most) unless it is between 1.18 and 1.34, a 0.16 luma window. (c) The bevel-g run passes
whole; bevel-thin fails only on jaw-3, by 0.18. Options for the person, each for the one check named:

| check | option A | option B | option C |
|---|---|---|---|
| seam test, `SeamAngle` | 9° (probe 8.33 rounded up) | 12° | 16° (half the break's least, 32.4) |
| stage strip, tolerance against `pm` where the rule puts the mean outside | 2 (ColourRule's own, 2026-11-14), and add check D below so the break still fails | 1.25 (between good 1.18 and break 1.34) | another number |
| stage strip, where the rule puts the mean between | slack 0.25 (good is 0.11 out) | judge it against `pm` like the others | drop the "between" reading on the jaw and rely on D |
| stage, a new check D for the bevel-g break | with the map less without (the plain frame), per strip, has the sign the rule gives `pm` less the face's predicted shade (good: +1.63/+0.85, +1.33/+0.21, +2.79/−0.79 against predicted +1.07/+0.14, +1.07/+1.14, +1.78/−0.15; bevel-g: −0.77 on jaw-1 A) | move the strip check to a lighter face (the `steel` grip, #80858A) where shades spread wider | the render test's box (already checks the strips and fails on G and R) stands for it, and the stage's strip check records only |

**Left**

1. The person answers the table above; land it.
2. After T-124 lands: `git rebase origin/milestone/M-24`, rerun the stage, re-probe `render_glb` against `render` with the map
   (pre-T-124: front 47 px, patch 13; three-quarter 15 px, patch 5; the person should see the rise from 10 to 15).
3. `bevel-thin` in the stage now has no check of its own to break (the seam check moved to Core with its break inside the test);
   either drop it from the stage or keep it as the strip check's second break. Ask with the table if kept.
4. Frame review round 1 (`G:\My Drive\artifacts\carve\M-24\T-125\round-1`): the normal `uvs` frame, the zoom-8 jaw frames with
   and without the map, the six sides, the render test's frames; put the numbers above in the request, one option set per check.
5. `verify`, As built, `cairn lint`, `finish`.

**Not checked:** no frame opened (0 of 12 images in either agent). No queued run. `server-regression` not rerun after the rebase.
The new jaw edge's frames are unseen; the stage's frames all changed with the edge.
