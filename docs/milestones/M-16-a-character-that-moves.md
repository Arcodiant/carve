---
description: >-
  A two-mesh coloured character with a skeleton, weights and a clip is saved, exported as a skinned .glb and loads through Empyrean
---
# M-16 — A character that moves

**Status:** Active

**One-liner:** *A two-mesh coloured character with a skeleton, weights and a clip is saved, exported as a skinned .glb and loads through Empyrean*

## What this milestone delivers

The thin slice of Orcward's request, end to end. An agent builds a character of two named meshes, colours it, gives it a skeleton and hand weights, writes a clip, looks at it at any moment, saves it, and exports a skinned `.glb` that Empyrean's loader reads with every piece, bone and clip.

Content: the gardener, an original small character with a hat and a watering can, used through M-19.

Plans against [D-6](../design/D-6-characters-rigging-animation-and-character-export.md), from Orcward's request of 2026-11-06.

**Depends on:** T-21 and M-3. It runs ahead of M-4 (the person, 2026-11-06). Then M-18, M-17 and M-19, in Orcward's order: the first character goes to a look review, so soft shading comes before automatic weights.

**Order.**

| Wave | Tasks | Model |
|---|---|---|
| 1 | T-29 | stronger: every later tool uses its `mesh` argument |
| 2 | T-30, T-31 | Sonnet |
| 3 | T-32 | Sonnet |
| 4 | T-33 | stronger: the sampler must match the engine |
| 5 | T-34, T-35 | stronger: the file layouts are public |
| 6 | T-46 | Sonnet |
| 7 | T-52 | Sonnet: added 2026-11-09, `export`'s `overwrite` rule was never built |
| 8 | T-36 | Sonnet |

Nine tasks, one past the usual range: T-46's clip file joined so this milestone proves retargeting in the game, as Orcward wants. A skinned, faceted, hand-weighted character is enough for Orcward's first use.

There is no fresh-agent acceptance run here: the first is T-49, once the tools are complete.

## Required tasks

- [ ] [T-29](../tasks/T-29-a-model-of-named-meshes.md) — The kernel and the store hold a model of named meshes, and every existing tool works on a named mesh with every frame unchanged
- [ ] [T-30](../tasks/T-30-face-colours-and-groups.md) — An agent colours faces and named face groups, and sees the colours in render and as glTF materials
- [ ] [T-31](../tasks/T-31-skeleton.md) — An agent defines a skeleton of named bones with parents and rest transforms, reads it back, and is refused over 255 bones
- [ ] [T-32](../tasks/T-32-explicit-weights-and-posing.md) — An agent sets per-vertex weights and renders the model in a given pose, skinned by Carve.Core
- [ ] [T-33](../tasks/T-33-clips-and-the-sampler.md) — An agent defines named clips of rotation and translation keys and renders any clip at any time
- [ ] [T-34](../tasks/T-34-skinned-export.md) — export writes a skinned .glb with one mesh, a primitive per piece and colour, skins[0] and the clips, byte-identical run to run, which Empyrean's loader reads
- [ ] [T-35](../tasks/T-35-carve-file-version-2.md) — The whole model saves to and reloads from the Carve file, and version 1 files still open
- [ ] [T-46](../tasks/T-46-clips-only-export.md) — export in clips mode writes an armature, skin and animations with no mesh, which LoadAnimationSet reads with the same bones and clips
- [ ] [T-52](../tasks/T-52-export-refuses-an-existing-file-without-overwrite.md) — export refuses an existing file without `overwrite: true`, in every mode, as D-6 says
- [ ] [T-36](../tasks/T-36-character-scenario.md) — A scenario builds the small test character through the real tools, poses it, saves, reopens and exports it, in its own suite

## Retrospective

**As planned.**

- The slice works end to end. T-36's `scenario-character` builds the gardener through the real tools: two meshes (`Body`, `Hat`), seven colours, 22 Synty-named bones plus a can bone, and clips `Idle` and `Walk`. It poses and plays the gardener, saves and reopens it field for field, and exports it as a skinned model and as a clips file. `tests/Carve.CharacterLoad.Tests` reads both through `IModelGeometryLoader.Load`: 22 bones in order, both clips, no parse warnings, and `HumanoidRoleMap.Synty` resolves every required role with no sidecar. T-46 showed `ClipRetargeter` taking the clips onto a skeleton of other proportions without throwing.
- The order held apart from T-52 (below): T-29; T-30 and T-31 side by side; T-32; T-33; T-34 and T-35 side by side; T-46; T-36. No member failed, and every rebase conflict (T-31 onto T-30; the others were clean) was resolved by the member keeping both sides.
- Every existing frame stayed byte-identical in every member, each compared by hash before and after: 70 frames at T-29, growing to 83 files by T-36.
- The person approved every new frame set that was put to review: T-29's two-mesh frames were looked at by the member only, and the person approved T-30's coloured crate, T-32's bent chain, T-33's wave and T-36's gardener.
- `cairn milestone M-16 verify` (2026-11-09) passes every check but `review`, which comes after the person's go: 10/10 members done and merged, and all nine suites pass (6 ran, 3 carried).
- **Models.** Opus 5.5 on T-29 (3 agents), T-33 (2), T-34 (2) and T-35 (3). Sonnet 5.5 on T-30 (1), T-31 (2: the first, resumed with the tolerance ruling, and a landing agent), T-32 (1), T-46 (1), T-52 (2, the second for a queue wait) and T-36 (1). No rework was traced to the cheaper tier.

**Not as planned.**

- **T-52 was added mid-milestone.** On 2026-11-06 the person ruled that `export` refuses an existing file without `overwrite: true`, and T-16's file, D-6 and D-1 were edited to say so. But T-16 had been built on 2026-11-05, so the rule was never implemented. The orchestrator found it before dispatching T-36, whose Scope used it. The person chose a task of its own. It landed with `export`'s bytes unchanged.
- **Hand-offs.** Seven of the 18 agents stopped at the 60-call checkpoint (T-29 twice, T-33, T-34, T-35 twice, T-52). Every Opus member handed off at least once, and T-29 and T-35 each took three agents.
- **T-31's `finish` did not merge, and its agent reported it landed.** It wrote the `cairn: finish T-31` status commit and then stopped, most likely at the rebase onto T-30. The agent had cut `finish`'s output to its last 12 lines and did not see it, though it said so. The orchestrator found it by checking the milestone log. A landing agent then lost its run when Waystone closed, and was resumed. Every brief after that asked for `finish`'s output read whole and a `Merge task/T-NN` line as proof. Every later member gave one.
- **A 2 h 16 m hang** in `RenderTests.A_queued_render_answers_during_a_stream_of_define_mesh` spoiled T-29's second agent's only regression run. It did not recur in four later runs, and no cause was found. The test's timeout was not changed.
- **The test queue backed up** behind Empyrean's M-142 gate (about 24 jobs). T-52's first agent stopped after 20 minutes of waiting. Its continuation waited about 40 minutes and touched nothing else.
- **`cairn test artifacts <suite>` returns that suite's latest job from any branch.** T-35's first fetch got T-34's files, with a version 1 `crate.carve`. It caught this and fetched by job id, and later briefs said to. M-4 met the same trap.
- **Rulings the person gave during the milestone** (all recorded in the members' As built):
  - T-29: `render` gains `mesh` for `labels` and `highlight`. Expected schemas and unknown-argument texts may be updated for new arguments. Results do not name the mesh.
  - T-31: a unit quaternion within 0.001 of length 1, kept as given.
  - T-32: D-6's framing wording stands (rest-pose bounds; a bend at the model's extent is cut off). Pose values, clip keys and the sampler are absolute local transforms.
  - T-34: material names as built. The rotations are normalised, an empty clip holds bone 0's rest, and "unweighted" counts every vertex. The limit warnings stay on skinned exports only. Because they are Orcward's numbers, not general ones, **T-51** was reserved to make them per game.
  - T-35:
    - `save`/`open` with no `mesh` work on the whole file. With a name they work on that mesh only: `save` adds the skeleton and clips its weights need, and `open` with a name takes a version 1 file's one mesh under that name.
    - Committed `.carve` files are compared by content, not bytes. The shed sample is version 2, with a version 1 fixture.
    - T-35's file-format readings are accepted.
  - Two of these were asked before dispatch (T-35's `save`/`open` and its byte identity), because the orchestrator read ahead and saw the spec collide with what had landed. The rest stopped a member.
  - RV-24 (2026-11-09): a pure clip library (a skeleton and clips, no mesh) is allowed. `export` in `clips` mode skips the mesh check; `save` writes a version 2 file with `"meshes": []` when the model has a skeleton, and `open` reads it back. A file with no meshes and no skeleton is still refused, and `model` mode still needs a mesh.
- **Readings members landed that the spec did not settle** (none was on a stop list):
  - T-30: groups are not carried into a several-mesh render.
  - T-31: `position` and `rotation` are required.
  - T-32: an unweighted vertex stays put, an extrude's new vertices start unweighted, and `capped` counts vertices.
  - T-33: `render` refuses a time outside the clip; `time` is required with `clip`; `loop` defaults to false; lower-case `linear` is refused.
  - T-35: the shed scenario's own reader of the sample learned version 2.
  - T-46: `piece` mode refused until T-50; the clips file is named `<name>.clips.glb` by T-36.
  - T-36: rest pose is arms down; the hat crown has a hidden cap, since an open crown gave 8 export warnings; the load check is a test project of its own.
- **Engine needs 5 and 6** were added to D-1: the slerp's numerics, and which Synty bone fills each humanoid role. Neither blocked anything. T-36 showed need 6's interim names resolve.

**Never verified.**

- **A real game.** Both `.glb` files are proved through the engine's parsers, loader and retargeter in tests, not loaded and played in Orcward's game, nor drawn by the viewer: the export scenario's frames are still the uncoloured crate.
- **Frames:**
  - T-29's two-mesh frames were looked at by the member only.
  - T-30's `crate-coloured-front-again` was not reviewed.
  - T-36's reviewed gardener frames came from a local run, not the final queued jobs. Only the existing frames' hashes were checked against queued artifacts.
- **Hash comparisons that were partial:**
  - T-31 compared cube and crate only, and before its rebase.
  - T-32 and T-33 did not rerun shed and export.
  - T-52 compared the export suite only.
  - Each later member's full comparison covers them.
- **Breaks:**
  - T-31 broke only the Core, not its tools.
  - T-32's mirrored camera showed only a scenario exit.
  - T-34 has no break for rotation keys alone left unnormalised.
  - T-36's mirror break fails at the first colour check, so the can's own mirror checks are not isolated, and most of its checks have no break row.
- **The hang's cause** (above).
- The orchestrator opened no frame. Every look above is a member's or the person's.

**Known and left.**

- **T-51** (per-game export limits) is open, off-milestone. The gardener is at 7 of 8 primitives, so the next colour trips the warning.
- **T-50** owns `piece` mode, which `export` refuses for now.
- `get_mesh` does not report weights, so tests check them through a saved file.
- `CLAUDE.md` is an input of no suite (a `verify` warning).
- Empty folders `branches/task/T-29` to `T-36`, `T-46` and `T-52` remain on disk, unregistered from git, as in earlier milestones. Evidence is in this worktree's ignored `artifacts/review/T-NN/`.

**The test report** (`cairn test report --since 3d`, a window that also takes in M-4's last day) shows no flakes. `server-regression` is the costly suite, at about 37,000 slot-seconds a week, and it caught 4 failures no other suite did. 35 jobs never ran (`cnr`); their causes were not looked into. Nothing was filed.

**For the next plan.**

- **Read each member's Scope against what has landed since it was planned**, before dispatch: M-4's `select` and `check`, T-29's `mesh` on `save`/`open`, and the unbuilt `overwrite` would all have stopped a member. Reading ahead caught two of them for free.
- **When a ruling changes a Done task's file, reserve a task for it**, rather than editing the finished one. T-16's edit hid an unbuilt rule for three days.
- **Plan Opus members for two agents.** Every one of them handed off once; a scope that fits the checkpoint, or a planned split, would save a hand-off's re-reading.
- **Put "read `finish` whole and give the merge line" in the first brief**, not after a miss.
- **Name the artifacts trap and the queue in every brief**: fetch by job id, and expect waits when another repo's gate is running.
