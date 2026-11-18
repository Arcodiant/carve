---
description: >-
  A mesh carries named shape variants on one topology, keeps them through subdivision, previews them and exports them as glTF morph targets
---
# M-21 — Morph targets

**Status:** Active

**Depends on:** M-19, M-20

**One-liner:** *A mesh carries named shape variants on one topology, keeps them through subdivision, previews them and exports them as glTF morph targets*

## What this milestone delivers

An agent gives a mesh named targets (a second set of positions on the same vertices), sees the mesh at any weights, and exports them as glTF morph targets a game can drive.

- Define a target from a full or sparse position set, or from a second file with the same topology. A topology mismatch fails and names the first difference.
- Targets survive `subdivide` and the edits that keep vertex identity. An edit that cannot keep them says so before it runs.
- `render` and `check` take weights. `check` reports a target that flips faces or opens the mesh at weight 1.
- Export writes `targets` with position and normal deltas and the target names.

Content: an arm with a weight dial, loaded in an Empyrean game with its slider.

## Why

Raised by the fps-arms project (private repo `Arcodiant/fps-arms`, 2026-11-15), which built a rigged, textured pair of first-person arms and four tools with Carve 0.9.0 through script-written `.carve` files.

- The Carve file (version 5) has no field for a second position set.
- fps-arms wants build, weight, muscle and age dials. Each is its cage script re-run with other ring sizes: 600 vertices in the cage, 2,376 after one `subdivide`.
- Without this, the project must repeat Carve's subdivision in its own script to get matching vertex order, and add the targets to the `.glb` after export.
- Empyrean reads `POSITION` and `NORMAL` deltas and applies morphs before skinning. It drops `TANGENT` deltas.

## Open at planning

- Which edit tools keep targets, and which drop or refuse?
- Dense or sparse storage in the file and in the `.glb`?
- Are normal deltas stored or computed at export?
- Corrective targets (driven by two others) and morphs in clips: in or out?

**Decided at planning (2026-11-16).** Each decision is the person's, made on a recommendation. Before the
questions, the planner checked the engine docs (Empyrean 0.119.0) and SharpGLTF 1.0.7, and found:

- `primitive.targets` parse into `ParsedMorphTargets` (names, default weights, dense position and normal deltas).
- Sparse accessors are made dense on load, and `TANGENT` is thrown away.
- `MorphWeights` drives the weights per entity, morph before skin.
- A parse throws when **two or more glTF meshes** carry targets. Carve writes every mesh as a primitive of one glTF
  mesh (D-6, engine need 4), so this never applies and no engine need is recorded.
- The animation docs show no morph-weight channel in clips.
- SharpGLTF has `MorphTargetBuilder` and `SetMorphWeights`.

The decisions:

- **Order.** M-21 starts after M-19 and M-20 have landed on `main`.
  - M-19's branch rewrites export, render and `subdivide`, and adds `render_glb`, which T-108 extends.
  - M-20 moves tool tests in-process, and T-102 and T-103 test on its harness.
- **Edits.** Every edit keeps targets. Targets are deltas, so `move` carries a target with its base.
  - `extrude`'s new vertices copy their source's delta.
  - `delete` drops a deleted vertex's delta.
  - `merge_faces` and `bridge` keep targets.
  - `plane_cut`, `loop_cut` and `knife` blend deltas by the stencils that blend weights.
  - `subdivide` is exact: Catmull-Clark is linear in positions.
  - `define_mesh` or `open` replacing a mesh that has targets is refused, naming them, unless `drop_targets: true`.
- **Storage.** Sparse deltas by vertex id in the Carve file, in a new version: the next free one, since M-9's T-92
  also plans version 7. The `.glb` uses dense accessors.
- **Normal deltas** are computed at export from `Shading`, at weight 1 minus at rest, and never stored.
- **Out of scope:** corrective targets and morph weights in clips.
- **`check`** reports three things for each target at weight 1:
  - `target-flips-face`: the normal's dot product with the rest normal is below 0;
  - `target-collapses-face`: the face becomes zero-area;
  - `target-opens-mesh`: vertices coincident at rest come apart.
- **The game proof.** `render_glb` takes weights through the engine's `MorphWeights` and is compared with
  `render`. The model viewer gets a live weight slider for the person's real-time pass, which comes before the
  arm's engine frames are approved.
- **Acceptance.** A fresh-agent acceptance run closes the milestone (T-107).
- **Names.** A target name is one dial across the model: the same name on two meshes is one glTF target.
- **Not yet ruled.** The planner set `render`'s weight range to 0–1 by default. T-103 asks the person before it
  lands.

**Agreed sketch** (approved in review, 2026-11-17; the full sketch is kept in
`artifacts/planning/M-21/`, not committed):

```
samples/arm.carve: tube along +X, shoulder x=0 to wrist x=0.6, axis y=0 z=0
  11 rings at x=0.00..0.60 step 0.06, 8 sides, 8-gon caps
  radii .050 .055 .058 .055 .048 .040 .046 .048 .042 .034 .028
  cage 88 V / 168 E / 82 F  ->  subdivide 1: 338 V / 336 quads / 672 tris, smooth
  bones Shoulder(0,0,0) > Elbow(.3,0,0) > Wrist(.6,0,0); clip Curl: Elbow 0->90 deg about +Z, 1 s
  Bulk: sparse, rings 1-3 and 6-8 x1.35 (48 vertices)
  Lean: from samples/arm-lean.carve, every ring x0.8

side view +z (direction [0,0,1]), framed on rest bounds
   rest:     .--._        _.--.            Bulk 1:   .----._       _.---._
            (  upper '-.__.-' fore )=                (  UPPER '-.__.-' FORE  )=
             '--''          ''--'                     '----''        '---''

frames: 1 rest +z | 2 Bulk 1 +z | 3 Bulk .5 Lean .5 +z | 4 Lean 1 +z | 5 Bulk 1 front
        6 Curl t=1 Bulk 1 +z | 7 planted Bad at 1, normals, +z (check names it)
        8-10 render_glb of the export at Bulk 0/.5/1, +z, against render's

viewer window (real-time pass only, not in captures):
+--------------------------------------------------+
| > Bulk  [==========|--------]  0.60              |
|   Lean  [|-------------------]  0.00             |
|                 (the arm, +z)                    |
| Tab: next target    Left / Right: -/+ 0.05       |
| Space: play / stop Curl     0: all weights to 0  |
+--------------------------------------------------+
```

**Order.**

| Wave | Tasks | Run |
|---|---|---|
| 1 | T-99 | alone |
| 2 | T-100, T-101, T-102 | side by side |
| 3 | T-103, T-104 | side by side |
| 4 | T-108, T-105 | side by side; T-108 needs T-104, T-105 needs T-100, T-101 and T-103 |
| 5 | T-106 | after T-105, T-104 and T-108 |
| 6 | T-107 | last |

- **Stronger model** on T-99, T-101 and T-104, which decide the type, the file format and the `.glb`; Sonnet on
  the rest.
- T-103 and T-105 are look-heavy, each with a separate review agent.
- At ten tasks the milestone is above the usual eight. The engine-side weights (T-108) were split from the export
  (T-104) so that neither runs past the checkpoint.

## Required tasks

- [ ] [T-99](../tasks/T-99-morph-targets-in-the-core.md) — A mesh holds named targets as sparse position deltas, defined from a full or sparse position set or a mesh of the same topology, evaluated at any weights and kept exactly through subdivide
- [ ] [T-100](../tasks/T-100-targets-kept-through-every-edit.md) — move, extrude, delete, merge_faces, bridge, plane_cut, loop_cut and knife keep a mesh's targets, new vertices taking their deltas from the vertices they come from
- [ ] [T-101](../tasks/T-101-targets-in-the-carve-file.md) — The Carve file stores each mesh's targets as sparse deltas in a new version, read back bit-exact, and open refuses to replace a mesh's targets unless asked
- [ ] [T-102](../tasks/T-102-target-tools.md) — define_target gives a mesh a named target from positions or a second file, naming the first topology difference; delete_target removes one; get_mesh reports them
- [ ] [T-103](../tasks/T-103-render-and-check-at-target-weights.md) — render and check take target weights, applied before skinning, and check reports a target that flips, collapses or cracks faces at weight 1
- [ ] [T-104](../tasks/T-104-morph-targets-in-the-glb.md) — export writes each target's position and normal deltas and its name as glTF morph targets, read back through the engine's loader
- [ ] [T-108](../tasks/T-108-target-weights-in-render-glb-and-the-viewer.md) — render_glb draws a .glb at chosen target weights through the engine's MorphWeights, and the model viewer takes starting weights and, in its window, a live weight slider
- [ ] [T-105](../tasks/T-105-the-arm-with-dials.md) — samples/arm.carve, an arm with Bulk and Lean targets, built, subdivided, rigged, checked and seen at its weights through the tools in scenario-morph
- [ ] [T-106](../tasks/T-106-the-arm-in-a-game.md) — The arm exported and loaded through the engine, compared with Carve's preview at three weights, and driven live with a weight slider in the model viewer
- [ ] [T-107](../tasks/T-107-morph-targets-acceptance-run.md) — Fresh agents give a mesh dials from the tool descriptions alone, across two sessions, and export it for a game

## Retrospective
