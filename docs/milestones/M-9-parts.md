---
description: >-
  A model is a named tree of parts with their own transforms, pivots and envelopes, instances, and the levels each was refined through
---
# M-9 — Parts

**Status:** Done

**One-liner:** *A model is a named tree of parts with their own transforms, pivots and envelopes, instances, and the levels each was refined through*

## What this milestone delivers

An agent builds a model as a named tree of parts rather than one mesh: a tank is a hull, a turret and two tracks, and a track is a loop and its road wheels. Each part has its own transform, a pivot that is its origin, and an envelope (its intended bounding box in metres). A repeated part is an instance: six wheels are one part. The edit tools act on a named part. Each part keeps every level it was refined through (blockout first), so that M-12 can build LODs from the tree. The Carve file holds the tree, and the `.glb` export writes it as glTF nodes, merging static subtrees.

Content: a sample model built as a part tree (a small vehicle), with its blockout and refined levels, and the exported `.glb` shown through M-3's viewer.

Plans against [D-2](../design/D-2-part-tree-and-hierarchical-modelling.md).

**Depends on:** M-3 (the Carve file and the export, which this extends). Plan it before M-10's primitives become parts, and before M-11, M-12, M-13 and M-15, which assume parts.

**Changed 2026-11-06.** [M-16](M-16-a-character-that-moves.md) adds a flat list of named meshes and a `mesh` argument (D-6). Parts grow from that: a named mesh becomes a part. Whether a tool's `mesh` argument becomes `part` is for this planning.

## Open at planning

- How the part tree extends M-3's version 1 Carve file (`{"carve": 1, "vertices", "faces"}`): a version 2 with a `parts` key, or a file per part (D-2).
- Whether every mesh lives in a part, or a model may still be a bare mesh.
- How a tool names its target part, and what happens to ids within a part when the tree changes.
- What a part's kept level is stored as: a full mesh per level, or a mesh plus the step it came from.
- Which subtrees the export merges: by a `rigid` flag on the part, by the absence of a pivot or socket, or both.
- How `render` and `get_mesh` show a tree: one part, a subtree, or the whole model.

**Decided at planning (2026-11-17).** Every ruling below is the person's, each on the option recommended to them.

- **File.** Version 7 adds a `parts` key, plus `levels` on a mesh. It is written only when a part has a parent, a transform, an envelope, `instance_of`, `moves` or a kept level. Every existing file stays byte-identical. One file, not a file per part.
- **Every mesh is a part.** A plain named mesh is a top-level part at the origin. There are no bare meshes beside the tree.
- **Naming and instances.** Part names are unique in one flat namespace, with no paths. A part owns geometry, has none (an empty), or is `instance_of` a part that owns geometry. Edit tools keep the `mesh` argument, which names a part with geometry; an instance or an empty is refused with a pointer. New part tools use `part`. Ids live in the part's own mesh, in part-local coordinates, so changes to the tree never change them.
- **Kept levels.** A level is a full mesh snapshot. `keep_level {level: N, parts?}` keeps the live geometry as level N, where N is at least the part's newest kept level and an equal N replaces it. "Level N" means each part's newest kept level ≤ N, and a part with none is not drawn. An instance shows its source's levels.
- **Transforms.** Translation in metres, rotation as a unit quaternion `[x, y, z, w]` (the `define_skeleton` rule), and scale per axis with every component > 0. A mirror is refused, because Empyrean's split load refuses a mirrored node. A part's origin is its pivot (D-2).
- **Envelopes.** An envelope is a min/max box in the part's own local space, and covers its whole subtree. `check` and `get_parts` warn when a part's geometry leaves its own envelope, or a child's placed envelope leaves its parent's. Nothing is refused.
- **Export.** One root node named after the file, as today, holds every static part merged. A part marked `moves` keeps its own node, under its nearest moving ancestor or the root, and static descendants merge into it. Moving instances share one glTF mesh, and static instances are merged. This departs from D-2's guideline, which keeps every instance as a node. A model with nothing marked `moves` exports byte-identical to today. A skinned model bakes part transforms and copies instances, and `moves` is ignored with a warning.
- **Render and read.** `render` draws the whole model placed. `part` draws only that subtree, framed on it, and `level` draws a kept level. `get_mesh` gives one part's local geometry plus its placement, and takes `level`. A new `get_parts` reports the tree. `select` and the edits work in part-local space.
- **Pieces.** `save mesh=` and `open mesh=` carry one part's own geometry and levels as a top-level part. `open` into an existing part keeps its placement. An instance or an empty is refused.
- **Order.** M-9 starts only after M-19 and M-20 have landed on `main`. M-19 edits the export and render files M-9 edits, and M-20 changes how tool tests are written: M-9's tool tests are in-process at `unit` tier, and its Verify lines name `server-regression` only for device work.
- **Checked at planning, not left to a task:** the pinned Empyrean 0.119.0 has `ModelLoadOptions.SplitParts` and `ModelParseOptions.SplitParts`. They keep every mesh node, every animated node and their ancestors, as `ModelPart`s in pre-order, and two nodes on one glTF mesh share one `Mesh`. They refuse a skinned file, morph targets and a mirrored node. This settles D-2's "not checked yet", and no engine need follows.
- **Corrections to the sketch, after agreement.** The person ruled the first two. The third follows from the envelope ruling, and the planner made it.
  - The wheels sit at z ±0.72, not ±0.7, so their inner faces clear the chassis sides.
  - The Lamp is refined at level 1. The Mast and the Chassis are kept at level 0 only.
  - The Chassis envelope covers its whole subtree.

**The agreed sample: the rover.**

```
PART TREE   (translation in the parent's space, metres; Y up; +X forward)
Chassis   top-level, static   box x -1..1, y 0.4..0.8, z -0.6..0.6      env [-1.2,-0.05,-0.95]..[1.2,1.75,0.95]
├ Cab     (0.5,0.8,0)  static  box x -0.4..0.4, y 0..0.6, z -0.5..0.5; L1: +X windscreen recess   env [-0.42,-0.02,-0.52]..[0.42,0.62,0.52]
├ Mast    (-0.6,0.8,0) MOVES   post x,z -0.05..0.05, y 0..0.8 (L0 only)    env [-0.4,0,-0.4]..[0.4,0.95,0.4]
│ └ Lamp  (0,0.8,0)    static  box x 0..0.3, y,z -0.1..0.1; L1: yellow lens recess on +X   env [-0.02,-0.12,-0.12]..[0.32,0.12,0.12]
├ Axle_F  (0.7,0.4,0)  empty, static                                       env [-0.45,-0.45,-0.92]..[0.45,0.45,0.92]
│ ├ Wheel_FL (0,0,0.72)              MOVES  L0 8-sided, L1 16-sided prism r 0.4, z -0.1..0.1, hub boss +0.05 on +Z
│ │                                         env [-0.42,-0.42,-0.12]..[0.42,0.42,0.17]
│ └ Wheel_FR (0,0,-0.72) rot [0,1,0,0] MOVES instance_of Wheel_FL   (boss faces -Z: outward)
└ Axle_R  (-0.7,0.4,0) empty, static, env as Axle_F
  ├ Wheel_RL (0,0,0.72)              MOVES instance_of Wheel_FL
  └ Wheel_RR (0,0,-0.72) rot [0,1,0,0] MOVES instance_of Wheel_FL

SIDE VIEW from +Z               Lamp
                       [==]====>        +-----------+
                        ||  Mast        |    Cab    |]  <- windscreen
  y 0.8 +---------------++--------------+-----------+--+
        |                    Chassis                   |
  y 0.4 +----(  RL  )---------------------(  FL  )-----+
             (  o   )                     (  o   )        r 0.4
  y 0   -----(______)---------------------(______)------  ground
       x -1  -0.7  -0.6                   0.5 0.7   1

LEVELS   L0 blockout: boxes, 8-sided wheel, no recesses (every part with geometry)
         L1 (and live): 16-sided wheel with hub boss, cab windscreen, lamp lens
         Mast and Chassis kept at L0 only, so they draw as L0 at level 1
COLOURS  chassis olive, cab sand, wheels dark grey, hub light grey, lens yellow

GLB      rover (Chassis + Cab) ─┬ Mast (+ Lamp)
                                ├ Wheel_FL ┐  Axle transforms folded in;
                                ├ Wheel_FR │  the four nodes share one glTF mesh
                                ├ Wheel_RL │
                                └ Wheel_RR ┘

FRAMES   (scenario-parts, 512 px unless noted)
 1 blockout-front     level 0, view front
 2 blockout-sheet     level 0, views +x +z +y
 3 front              live, view front
 4 back-left          direction [-1, 0.6, -1]
 5 top                +y orthographic
 6 mast-turned        Mast rotation [0, 0.7071068, 0, 0.7071068]: lamp swings from +X to -Z
 7 part-axle-front    part Axle_F alone, direction [1, 0.3, 0]: boss outward on both wheels
 8 part-wheel-labels  part Wheel_FL alone, labels faces, 1024
 9 part-wheel-L0      part Wheel_FL, level 0
10 reopened-front     after save and open: bytes equal to frame 3
11 instance-edit      after one move on Wheel_FL: all four wheels change
12 viewer-front       exported .glb in M-3's viewer (merged load)
13 viewer-angle       (the split load is checked by numbers)
```

**Order the tasks run in.**

| Wave | Tasks | Model |
|---|---|---|
| 1 | T-91, the Core | Opus, two agents planned |
| 2 | T-92 (file), T-93 (tools) and T-95 (export), side by side | Opus each |
| 3 | T-94, render | Opus, with a separate frame-review agent |
| 4 | T-96, the rover through the tools | Sonnet, with a separate frame-review agent |
| 5 | T-97, the rover in a game | Sonnet |
| 6 | T-98, the acceptance run | Sonnet |

In wave 2, `ModelStore.cs` is shared by T-92 and T-93, for adds only. `Program.cs`'s tool list belongs to T-93, and `Model.cs` changes only by addition.

## Required tasks

- [ ] [T-91](../tasks/T-91-part-tree-in-the-core.md) — A model is a named tree of parts with transforms, envelopes, instances and kept levels, placed in world space for drawing, with the sample rover worked in Core
- [ ] [T-92](../tasks/T-92-carve-file-version-7.md) — The Carve file holds the part tree as version 7, written only when a model needs it, and save and open carry one part's geometry and levels as a piece
- [ ] [T-132](../tasks/T-132-carve-file-version-9-parts-beside-morph-targets.md) — The Carve file holds a part tree beside morph targets as version 9, so save no longer refuses a model with both (added 2026-11-18 by the person's ruling on RV-64's open question)
- [ ] [T-93](../tasks/T-93-part-tools.md) — define_part, get_parts and keep_level build and read a part tree, the edit tools act on a named part, and check warns when a part leaves its envelope
- [ ] [T-95](../tasks/T-95-export-a-part-tree.md) — export writes the part tree as glTF nodes, merging static parts into their nearest kept ancestor and sharing one mesh between instances, and a skinned model bakes its parts
- [ ] [T-94](../tasks/T-94-render-a-part-tree.md) — render draws every part where it is placed, isolates a subtree with part and draws a kept level with level, and get_mesh gives a part's placement
- [ ] [T-96](../tasks/T-96-the-rover-through-the-tools.md) — scenario-parts builds the rover through the tools at two levels, turns its mast, edits an instance and shows each in frames worked from the geometry
- [ ] [T-97](../tasks/T-97-the-rover-in-a-game.md) — The rover is saved as samples/rover.carve, exported, shown by the viewer and read back as a split part tree through Empyrean's loader
- [ ] [T-98](../tasks/T-98-parts-acceptance-run.md) — Fresh agents build a part-tree vehicle from the tool descriptions alone, find a planted envelope fault and export it

## Retrospective

**As planned.**

- **The slice works end to end, reached through the tools from a plain launch.**
  - A model is a named tree of parts with transforms, pivots, envelopes, instances and kept levels, in Core (T-91).
  - The Carve file holds it: version 7, and version 9 beside morph targets (T-92, T-132).
  - `define_part`, `get_parts` and `keep_level` build and read it, the edit tools target a part, and `check` warns on an envelope (T-93).
  - `render` draws it placed, by subtree and by level, and `get_mesh` reports placement (T-94).
  - `export` writes it as glTF nodes, with instances sharing a mesh (T-95).
  - The rover is built through the tools in `scenario-parts` (T-96), saved as `samples/rover.carve`, shown in M-3's viewer and read back through Empyrean's split load (T-97).
- **Fresh agents did the job from the descriptions alone** (T-98). One round of `claude -p`, on Opus 5.5:
  - it found the planted low axle from the envelope warning and fixed it;
  - it made a moving instanced wheel pair and kept level 0;
  - it exported a `.glb` that the viewer loads, with the two wheels sharing one mesh in the split load.
- **Nothing existing changed bytes.**
  - Every committed `.carve` hashes the same before and after (T-92, T-132).
  - All 11 existing exported `.glb` files hash the same (T-95, six suites run before and after).
  - Every existing frame is byte-identical, over 14 of 14 suites (T-94) and in cuts and gatehouse after T-96 changed the shared ray test.
  - One frame was checked only locally: `NamedMeshTests`' labelled lid, because of B-9's race. The person ruled that enough.
- **Every new check was shown failing on a broken product.** Each member keeps its break table under `G:\My Drive\artifacts\carve\M-9\T-NN\break-table.tsv`.
- **The person approved every frame set put to them:**
  - T-94's five render frames;
  - T-96's 13 `scenario-parts` frames;
  - T-97's two viewer frames;
  - T-98's three.
- **`cairn milestone M-9 verify` (2026-11-19, at 3fd7c72, before this retrospective)** passed every check but `review`, which comes after the person's go:
  - 9/9 members done and merged;
  - all 23 suites pass, 18 ran and 5 carried;
  - `server-regression` ran 439 s on Xellotath.
- **Models:**

  | Member | Model | Agents |
  |---|---|---|
  | T-91 | Opus 5.5 | 2, as planned (API, then the rover), with three questions |
  | T-92 | Opus 5.5 | 1 |
  | T-93 | Opus 5.5 | 2 (the checkpoint, with two questions) |
  | T-132 | Opus 5.5 | 1 |
  | T-95 | Opus 5.5 | 2 (the checkpoint) |
  | Rebase onto `main` (M-27) | Sonnet 5.5 | 1 |
  | T-94 | Opus 5.5 | 5 (three checkpoints with questions, a fourth for the guard and breaks, then the frame-review agent) |
  | T-96 | Sonnet 5.5 | 2 (as planned: the build, then the frame-review agent) |
  | T-97 | Sonnet 5.5 | 2 (a question) |
  | T-98 | Sonnet 5.5 (inner session Opus 5.5) | 1 |

  No rework was traced to the cheaper tier. Every extra agent was a planned review agent, a checkpoint or a stop for the person.

**Not as planned.**

- **The milestone grew from eight members to nine.** T-132, Carve file version 9, was added on the person's ruling (2026-11-18). RV-64 had left "which version holds parts beside morph targets" for M-9's planning, and the planning never ruled it. T-92 then landed a refusal to save a model with both, without asking. The person chose a version 9 in M-9 over keeping the refusal.
- **Two members landed choices that were the person's.**
  - T-92: the refusal above.
  - T-95 and T-132: re-taking `PartToolsListTests`' tools/list digest when their own spec changed a description. Both checked that only that description moved.

  Both were put to the person afterwards. A brief should name the existing pins a member is likely to hit and say "ask first", rather than leave it to "a change that would loosen a check".
- **M-27 ran beside M-9 and touched the same render files.** By the person's ruling, T-94 was held until M-27 landed. The milestone was then rebased onto `main` with no conflicts, mid-milestone, while no member was in flight. T-95, paused at a hand-off, moved across with `git rebase --onto` from the old tip. Holding T-94 cost little, since T-95 and T-132 ran in the meantime.
- **T-94 took five agents.** The plan's frame list could not all be checked as written:
  - the Axle_F subtree is symmetric, so no mirror check can work from its own framing;
  - from [1, 0.3, 0] neither hub boss is in sight (0 pixels from that and four other directions);
  - three probes sat on the front view's line of symmetry;
  - `ColourRule` misses the dark wheels by 3–4 levels.

  Each came up as a separate stop. As M-19's lesson already said, the frame directions and probes should be worked through the ray test before the frame list is agreed, and every miss put to the person in one question.
- **Rulings the person gave during the milestone,** each recorded in the As built of the member named:
  - T-91:
    - an instance may carry its own envelope;
    - coplanar contacts between parts are checked by facing, with the two hidden contacts pinned.
  - T-93:
    - an existing part's `instance_of` does not change;
    - the empty-part hint reads `define_mesh name=`.
  - T-92: `open mesh=` into an empty or an instance stays refused.
  - T-132: a kept level keeps its targets.
  - T-95: five export rules, chosen through the review window:
    - a sheared fold is refused;
    - nodes are written in pre-order;
    - limits count what is drawn;
    - a moving instance with static children gets its own mesh;
    - targets are written with a split-load warning.
  - T-94:
    - `part` with `views` frames the subtree;
    - mirror and up checks on whole-rover frames only;
    - four unsettled rules accepted as built;
    - the Axle_F frame is judged by its inner caps;
    - tolerance 4 for the dark wheel faces until B-8;
    - the moved probes;
    - the lid frame's local evidence.
  - T-96:
    - the flip rule (500 / under 100);
    - `instance-edit` drawn from above;
    - two helper frames kept.
  - T-97:
    - the sample-version test removed ("why are we testing file version?");
    - the viewer views as chosen;
    - the 12 materials explained (one per submesh) and pinned.

**Never verified.**

- No member ran an ordinary MCP client against `carve` apart from T-98's acceptance session. Every other tool check is in-process (`ToolHarness`) or a scenario.
- The split load was checked through the engine's CPU parser only, not a game loading the rover on a device.
- The windscreen recess is faint in the frames, and T-94's review judged it present from its walls without measuring it. The rear-right wheel is hidden in every agreed frame.
- `tests/Carve.PartsAcceptance.Tests` is in no suite, as T-49's acceptance project is not, so `verify` warns it uncovered. It runs only on a round folder.

**Known and left.** Filed off-milestone:
- **B-7:** the weights look misaligns on a model with instances.
- **B-8:** `ColourRule` predicts low-albedo faces too dark. T-94's and T-96's dark-wheel tolerance goes back to 2 when it is fixed.
- **B-9:** `NamedMeshTests` stops at its first stale refusal and can skip the labelled lid frame.

Old worktree folders that `finish` could not remove are safe to delete by hand: `branches\task\T-91`, `T-92`, `T-93`, `T-95`, `T-97`, `T-98` and `T-132`, plus `T-94` and `T-96` if still there.

**Test report** (`cairn test report --since 2d`).
- The window covers M-27's finish as well as M-9, so the two can't be told apart.
- `server-regression` is most of the slot time: about 27,500 slot-seconds in the window, 31 % carried, with 1 failure found nowhere else (T-94's own new rover tests, real). That is M-20's accepted trade-off at about 440 s a run.
- `scenario-orcward` failed 3 times, each also caught elsewhere.
- No flakes were recorded, though 1126 passes' logs could not be read here.
- Nothing new to file.

**For the next plan.**

- Work every agreed frame through the ray test before the frame list is settled: which features are in sight from each direction, and which checks a symmetric subject can carry. Put every miss to the person in one question.
- Name in each brief the existing pins and guards a member will likely trip (tools/list digests, sample guards, byte-identity baselines), and say "ask before re-taking".
- When a design question is carried into a milestone's planning (as RV-64's was), list it in *Open at planning* so the planning rules it.
- Keep an orchestrator rebase onto `main` for a moment when no member is in flight. A paused member crosses with `--onto` from the old tip.

**After the person's go (2026-11-19).**

- **The rebase onto `main` was a no-op**: `main` had not moved since the mid-milestone rebase.
- **The refactor pass** (Opus 5.5) made four commits:
  - `Model.ParentOf` and `Shown` replace seven repeated walks;
  - one `OffsetOf` path;
  - shared file-test helpers;
  - a dead `OpenMesh` overload removed.

  No output changed.
- **RV-71, round 1:** four Opus 5.5 reviewers, split by area plus one over the whole diff, found 26 findings (3 Major, 14 Minor, 9 Nit). All 26 were fixed by two parallel Opus 5.5 fix agents, A and B, each run as a first agent and a continuation. The three Majors:
  - a bare empty part was lost by save and open;
  - whole-model labels landed on every copy of an instanced mesh;
  - a placed rigged part was skinned in part space by `render` and `weigh`, but in model space by the export.

  The person ruled two:
  - posed `render` and `weigh` match the export;
  - `get_mesh` warns when its `world` is inexact.

  The `instance_of` rule moved into Core. No frame, `.glb` or sample changed bytes: B hashed 155 rigged-suite frames, 16 parts frames and 249 `server-regression` frames, and A rewrote all 10 samples.
- **RV-72, round 2:** 25 of the answers held. Two Nits were new: `get_mesh`'s shear tolerance was looser than export's, and the now-unused `Skinning.Posed` was left. By the person's ruling, both were fixed:
  - `Trs.Decomposes` is one shared check;
  - `Posed` is removed;
  - an always-true material check was replaced by one that catches three breaks.
- **RV-73, round 3:** one reviewer confirmed both answers, with no findings.
