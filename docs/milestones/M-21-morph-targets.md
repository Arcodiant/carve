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
| 4 | T-108, T-105 | side by side; T-108 needs T-104 and T-103 (its `render` comparison takes `render`'s `targets`; T-103 was added in the building, RV-64), T-105 needs T-100, T-101 and T-103 |
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

**As planned.**

- **The slice works end to end.**
  - A mesh carries named targets as sparse deltas (T-99).
  - It keeps them through `subdivide`, exactly, and through every edit (T-100).
  - It saves them as Carve file version 8 (T-101) and defines and deletes them through the tools (T-102).
  - It is seen and checked at weights (T-103) and exported as glTF morph targets the engine reads back (T-104).
  - It is drawn by the engine at weights with `render_glb` and driven live in the viewer (T-108).
  - The arm sample is built through the tools (T-105), loaded in the engine and driven by the person (T-106), and fresh agents did the whole job from the descriptions (T-107).
- **Exactness held without a loosened check.**
  - Subdivide of base plus delta equals subdivided base plus subdivided delta within 1e-6 of the diagonal, on the arm cage and the creased cylinder at levels 1 and 2 (T-99).
  - Every edit matches the edit of the morphed mesh within 1e-6 (T-100). T-100 measured this on its own per-edit carriage, which the rebase onto T-71 replaced. The same tests now run against `StandardLayers.Targets` on T-71's layer registry and pass there, in `core-unit` at every verify after the person's go.
  - `render_glb` of the export equals `render` with 0 of 262,144 px differing, at Bulge 0, 0.5 and 1 (T-108) and at the arm's Bulk 0, 0.5 and 1 (T-106). A control pair fails D-6's rule in both.
- **Every existing frame stayed byte-identical.** T-103 hashed 419 frames over 13 scenarios against a baseline build. T-105 hashed the cylinder's 12. Files without targets export byte-identically (T-104, four pinned hashes), and save byte-identically (T-101, every committed `.carve` rewritten).
- **Every check was shown failing on a broken product.** Each member records its own, for example:
  - the wrong face stencil (T-99);
  - zero deltas on new vertices (T-100);
  - a double-summed write (T-101);
  - swapped targets and dropped `NORMAL` (T-104);
  - a mirrored camera, ignored weights, and the morph applied after skinning (T-105, catching what T-103's symmetric cylinder could not);
  - `render_glb` given no weights (T-106).
- **The person approved every set put to review:**
  - T-103's 12 frames, with its two departures: a 2 m cylinder, and Pinch reshaped so it can flip faces;
  - T-105's 7 distinct arm frames;
  - T-106's live viewer pass and frames 8–10;
  - T-107's 8 frames.
- **Acceptance (T-107).** One round of two `claude -p` sessions on Opus 5.5, on plain `carve` with `--allowedTools "mcp__carve__*" --tools ""`.
  - The plant fired: `faces[7][0]: vertex 7 here, 13 in the other mesh`.
  - Session 2 opened, subdivided, checked at weights, rendered and exported a `.glb` with both targets, and its `render_glb` frames were byte-identical to `render`'s.
- **`cairn milestone M-21 verify` (2026-11-18, at c5832ab, before this retrospective)** passed every check but `review`, which comes after the person's go. c5832ab is the tree before the rebase onto `main` and T-71, and is on no branch now; the verifies of the code that ships are under *After the person's go*:
  - 10/10 members done and merged;
  - all 18 suites of that tree pass, 16 ran and 2 carried, `server-regression` 294 s on Xellotath.
- **Models:**

  | Member | Model | Agents |
  |---|---|---|
  | T-99 | Opus 5.5 | 1 |
  | T-100 | Sonnet 5.5 | 1 |
  | T-101 | Opus 5.5 | 1 |
  | T-102 | Sonnet 5.5 | 1 |
  | T-103 | Sonnet 5.5 | 2 (the checkpoint, then the review agent) |
  | T-104 | Opus 5.5 | 1 |
  | T-108 | Sonnet 5.5 | 2 (the checkpoint; the continuation held until T-103 landed, for the `render` comparison) |
  | T-105 | Sonnet 5.5 | 2 (the checkpoint, then the review agent) |
  | T-106 | Sonnet 5.5 | 2 (the checkpoint, then the person's pass and review) |
  | T-107 | Sonnet 5.5 (inner sessions Opus 5.5) | 2 (a question) |

  No rework was traced to the cheaper tier. Every second agent was a planned review agent or a stop for the person.

**Rulings the person gave during the milestone** (each recorded in the As built of the member named against it; T-104's record of the vertex split was added in answer to RV-64, and the weight range is D-6's rule since then too):

- **Weight range** (asked before T-103's dispatch): `render`, `check` and `render_glb` refuse a weight outside [0, 1], naming the target. Carve stores no range; a game drives the exported morphs however it likes.
- **Vertex split in the `.glb`** (T-104 landed it, asked afterwards): corners merge in the export only when their deltas agree too, so a model with targets can write more vertices than without. Kept.
- **Not-planar warnings on the arm** (T-106): pinned, so only not-planar warnings are accepted and their folded count equals `select faces where non_planar`, as `scenario-soft` does. The rule for smooth meshes is filed as **T-127**, off-milestone.
- **T-107's recovery**: round 1 counts. The error was understood and the dials were made correctly from positions without guessing, though the second file was not fixed. No second round.
- **`export` names the targets it wrote** (T-107 found it missing): added in T-107, only when the model has targets, so other exports' text is unchanged.

**Not as planned.**

- **Rounding in the delta of the sum shows** (T-99). A target given as absolute positions comes back at weight 1 bit for bit within a factor of 2 of its base (Lean, Bulk), and up to one ulp off further out (x3.7, x0.001). Kept, as storage stays deltas (the person's decision, and glTF's); tests pin both cases.
- **T-103 changed its test content** to make the checks able to fail: the cylinder 2 m, and Pinch reshaped, since a radial push through the axis cannot flip a Newell normal. Approved in review.
- **T-105 departed from the sketch's wording, not its arm:**
  - the arm stage runs before the cylinder in one scenario, as a model cannot be emptied;
  - the Wrist's rest is local 0.3, world 0.6;
  - frame 7's checks near the spike are limited to those the ray test is sure of.

  The person's approval carried no separate ruling on them.
- **T-108 waited on T-103.** Its `render` comparison needed T-103's `targets`, so its continuation was held for one landing.
- **Small product changes outside a member's listed files:** two `ModelStore.cs` overloads (T-101, for open's check under the write lock); `Skinning.Morphed` made public (T-103); `export`'s target names (T-107, by ruling). The rebase onto `main` and the refactor pass added two commits that belong to no member and carry the targets themselves: f3e5e88 moved them onto T-71's layer registry (`LayerRegistry.cs`'s `BlendDeltas`, `ReblendDeltas` and `StandardLayers.Targets`; `MeshEdits.Subdivide`, `EditResult.cs`, `Subdivision.cs`), and 8653926 put the layer in `LayerRegistry.Standard` and edited `AttributeLayerTests`. T-100's own product commit, d456c0d, now holds only its tests and As built.
- **`finish` again could not remove any member's worktree folder.** `branches/task/T-99` to `T-108` are unregistered from git and left on disk for the person to delete.
- **The orchestrator misreported once.** It called T-104 done before it had landed, and corrected it in the next message. Report a member only from `plan`.

**Never verified.**

- **Viewer keys** are covered by `WeightPanelTests` and the person's one live pass only; nothing scripts the window.
- **`scripts/run-viewer.ps1` cannot pass `--Empyrean:Graphics:VirtualSurface=false`** (PowerShell takes it as a parameter). Members ran the viewer's dll with `dotnet exec`. Not fixed.
- **T-107's `limb.glb`** was loaded only through the inner agent's `render_glb`, not by an independent check.
- `server-regression` ran for T-103, T-104 and T-108, and in the milestone's verify; not for T-105, T-106 or T-107's export change on their own.

**Known and left.**

- **T-127**: not-planar warnings on smooth meshes (the arm's 49, the soft gardener's too).
- **M-9's T-92** will need to read version 8, as its Scope now says, and which version holds a file with both parts and targets is left open for the person at M-9's planning (T-101's As built, RV-64).
- Corrective targets and morph weights in clips stay out, as decided at planning.

**After the person's go (2026-11-18).**

- **Rebase onto `main` (Opus 5.5):** `main` had T-71's attribute layers and 0.12.1. Two commits conflicted and were settled with T-71's structure. The targets' carriage moved onto T-71's layer registry in f3e5e88 (`LayerRules.BlendDeltas` and `ReblendDeltas`, `StandardLayers.Targets`, `MeshEdits.Subdivide`), a commit that belongs to no member. Verify then passed every check but `review`: 17 suites ran and 1 carried, `server-regression` 251 s on Mantorok, `scenario-morph` 19 s on Xellotath.
- **Refactor pass (Sonnet 5.5):** by the person's ruling, the targets layer moved into `LayerRegistry.Standard` (8653926, which also edited `AttributeLayerTests`), and T-99's and T-100's As built were corrected for the rebase (efa52d9). Verify passed again: 17 ran and 1 carried, `server-regression` 425 s on Xellotath, `scenario-morph` 20 s on Xellotath.
- **Review round 1, RV-64:** four Opus 5.5 reviewers, split by area (the Core; persistence and export; tools, rendering and the viewer) plus the whole diff, gave 26 findings: 1 Major, 18 Minor, 7 Nits. The Major: this retrospective cited a verify of the tree before the rebase, and left the rebase's product commits out. The claims it named now say which code they are about (*As planned*, *Rulings* and *Not as planned* above), and this part records the verifies after the rebase. Two agents answered the persistence, whole-diff and Core findings, the second (Opus 5.5) writing this part and the outcomes:
  - the code fixes are `FromPositions` refusing a non-finite delta, the dead `MorphTarget.Renamed` and `MorphTargets.Select` removed, a knife-inside-a-face test with a target that is not affine, and a test of the skinned and piece export result lines;
  - the rest corrected the record: T-100's As built and its broken-product table against `StandardLayers.Targets`, T-103's and T-107's Verify lines, T-108's `Depends on:`, the vertex-split ruling in T-104, D-6's Intent and weight-range rule, and T-92's Scope, which now reads version 8 and leaves the parts-plus-targets version open for the person at M-9's planning;
  - the viewer's `--targets` Nit was skipped: only a starting weight cannot be given for a name holding `,` or `=`, and the window's weight panel still lists and drives such a target.
  The tools, rendering and viewer findings are answered by a further agent.
- **`cairn milestone M-21 verify` (2026-11-18, at 2fbdc23, every fix above in it):** passed every check but `review`, still open while RV-64's tools section is answered: 10/10 members done and merged; 17 suites ran and 1 carried, failed 0; `core-unit` 25 s, `server-unit` 19 s, `scenario-morph` 19 s and `server-regression` 255 s, all on Mantorok. `viewer-cube` was carried from c5832ab on Xellotath, its inputs unchanged since.
- **Next time:**
  - Re-run the milestone's verify after a rebase that moves product code, and cite that run, not the one before it.
  - Name the commits a rebase adds outside every member, since no member's As built will.

**Next time.**

- **Ask open rules before dispatch.** The weight range was asked first and T-103 ran without a stop on it. The vertex split and the not-planar count were each a ruling after the fact.
- **Plan a member's frame review as its second agent from the start.** It was here for T-103, T-105 and T-106, and the checkpoint fell where planned.
- **Plant faults the tools cannot route around**, or say what recovery means when they can. T-107's agent sidestepped the mismatched file with `positions`, which the Scope had not foreseen.
- **Put a member's dependence on a sibling's feature in the order table.** T-108's comparison needed T-103's `render` targets, which the waves did not show.
