---
description: >-
  An agent cuts into a mesh: plane cuts, loop cuts, knife paths, face merge and delete
---
# M-5 — Cut

**Status:** Active

**One-liner:** *An agent cuts into a mesh: plane cuts, loop cuts, knife paths, face merge and delete*

## What this milestone delivers

An agent cuts into a mesh: plane cuts, loop cuts and knife paths, with face merge and delete to tidy up, on a model visibly built this way.

Content: a sample model that needs cuts to exist, with a scenario showing each stage.

**Depends on:** M-18 (the person, 2026-11-09). It starts once M-18 is Done, so cuts carry hard edges and smooth shading.

## Open at planning

- What "cut" means (D-1): which of plane, loop and knife matter, and whether boolean subtraction is wanted. A true boolean would need its own milestone.
- How a knife path is specified by coordinates, with no pointer.
- What happens to selections and element ids across a cut.
- Faces with holes, moved here from M-2 (decided at M-2's planning): a face as an outer loop plus inner loops, its `define_mesh` syntax, triangulation and how the edit tools treat it. M-2's edge table leaves room for it.
- What deleting a face or vertex does to ids, which M-2 promises are never reused.

**Decided at planning (2026-11-09).** Each was decided by the person, from the planner's recommendations.

- **What "cut" means.** `plane_cut`, `loop_cut` and `knife`, with `merge_faces`, `delete` and **`bridge`** added so an opening can go right through a wall. There is no boolean subtraction; a true boolean would be a milestone of its own. One tool per operation, as D-1 decides.
- **Order.** M-18 runs first, keeping the ruling of 2026-11-06, and M-5 starts once M-18 is Done. So cuts carry T-42's hard edges and smooth shading, and T-43's `subdivide` is taught about holed faces in T-63. M-5's place relative to M-17 and M-19 was not asked.
- **Ids across a cut.** A split face keeps its id for the piece holding its first outer-loop corner, or the next corner off the cut if that one lies on it. The other pieces are appended. Every edit's report gains `split: [{face, pieces}]` and `deleted: {vertices, faces}`. The server still holds no selection, so an agent's ids stay valid apart from what `split` and `deleted` name.
- **Delete and ids.** A deleted vertex or face is a tombstone: its slot stays (`null` in `get_mesh` and the file), and its id is never reused along the current chain of edits. Counts are live counts. Render, `select`, `check` and export skip tombstones, and export compacts. There is no `compact` tool. Deleting a vertex deletes every face on it. Deleting or merging faces also deletes vertices no live face uses. Everything removed is listed in `deleted`.
- **Faces with holes.**
  - A face is an outer loop plus hole loops. In `define_mesh` and `get_mesh` it is a plain array or `{"loop": [...], "holes": [[...], ...]}`.
  - Holes are wound clockwise seen from outside, and a wrong winding is refused.
  - Triangulation bridges each hole into the outer loop, then ear clips.
  - `extrude` on a holed face makes walls on every loop (a tube), and an inset on a holed face is refused.
- **The knife.** It takes points `[[x, y, z], ...]` on the surface, within 1e-4 of the bounds diagonal, each snapped to a vertex, then an edge, then a face's interior. Consecutive points share a face. A closed path wholly inside a face cuts out an inner face and leaves the face with a hole. An off-surface point's error gives its distance and the nearest surface point, facts only.
- **The tool API.**
  - `plane_cut(point, normal, keep: both | above | below = both, fill = true, faces?)`, where `above` is the side the normal points to.
  - `loop_cut(edge: [a, b], at: [fractions from a])`. The ring crosses quads and stops at a non-quad or an open edge.
  - `knife(points, closed = false)`.
  - `merge_faces(faces)`: connected faces only, the lowest id survives, and a non-planar result is a warning.
  - `delete(vertices?, faces?)`.
  - `bridge(loops: [[v, ...], [v, ...]])`: two open loops of equal count, paired by least total length over rotations and both directions.
- **What carries.**
  - Pieces take their face's colour and groups.
  - Both pieces of a split hard edge stay hard, and new edges are soft, except a plane cut's fill border on a smooth mesh, which is hard.
  - New vertices blend weights: by fraction on an edge, or barycentrically inside a face, renormalised and capped at 4. UVs blend the same way if M-6 has landed; how seams carry goes back to the person.
- **The file version.** One new Carve file version, owned by T-63 and taken as the next free one at dispatch: 4 after T-42's 3, unless M-6's UVs took it first. It holds holed faces and the `null` tombstones. `CarveFile.Write` writes the lowest version that holds the model: this one only when the model needs it (a holed face, a tombstone), otherwise 2 or 3 byte for byte as T-42's tests require (ruled by the person, 2026-11-11).
- **The sample**, as agreed: a gatehouse wall, built by T-69, coordinates in metres.

  ```text
   front, from +Z            apex (0, 3.4)
                                 /\
                               /    \
                             /        \
                           /            \
               (-1.5,2.6)/                \(1.5,2.6)
                        |                  |
               y=2.2    |==================|  cornice (front band, +0.08)
               y=2.0    |==================|
                        |              /\  |  window: knifed front and back,
                        |  ______     |  | |  faces deleted, loops bridged:
                        | |      |    |__| |  you see through it
                        | | door |         |
                        | |brown |         |  door: open knife path from
               y=0      |_|______|_________|  the floor edge, coloured
                      -1.5 -1.1 -0.4  0.3 1.1 1.5
  ```

  1. The slab: x -1.5..1.5, y 0..4, z -0.15..0.15.
  2. `loop_cut` at y = 2.0 and 2.2, then the front band extruded out by 0.08.
  3. `merge_faces` on the back and end pieces.
  4. Two `plane_cut`s through (±1.5, 2.6) and (0, 3.4), each keeping below with fill. This removes the top face whole.
  5. `knife` along the door, (-1.1, 0) → (-1.1, 1.6) → (-0.4, 1.6) → (-0.4, 0), and the door coloured `#6b4423`.
  6. `knife`, closed, along the window, (0.3, 0.8), (1.1, 0.8), (1.1, 1.5), (0.7, 1.75), (0.3, 1.5), front and back.
  7. `delete` both window faces, then `bridge` the two loops. The mesh is closed.

  Frames are taken from directions (from the model toward the eye) `[0.6, 0.35, 1]` and `[-0.6, 0.35, -1]`, and orthographic `[0, 0, 1]` through the window, with wireframes of the stages.
- **Content with each mechanic.** Each tool task brings its own description and worked example, and a stage of its own in the new Cuts scenario (`scenario-cuts`, created by T-63). The gatehouse is the milestone's sample and needs every tool, so it is a task of its own after them, frames kept apart from code. The milestone is reached through the product's own MCP tools from a plain launch.
- **Order of the tasks:**

  | Wave | Tasks |
  |---|---|
  | 1 | T-63 (holes; owns the file version and creates the Cuts scenario) |
  | 2 | T-64 (tombstones and `delete`) |
  | 3 | T-65 (`FaceSplit` and `plane_cut`) beside T-68 (`merge_faces` and `bridge`) |
  | 4 | T-66 (`loop_cut`) beside T-67 (`knife`), both on T-65's `FaceSplit` |
  | 5 | T-69 (the gatehouse) |
  | 6 | T-70 (the acceptance run) |

  Tasks side by side share only `Program.cs`'s tool list and `CutsStages.cs`, both adds only, kept on both sides.
- **Models.**
  - Opus for T-63, T-64, T-65, T-67 and T-68: design-heavy, or deciding what others build on.
  - Sonnet for T-66, T-69 and T-70.

  T-63, T-64, T-65 and T-68 are planned for two agents each, with a hand-off point named.
- **Not worked at planning.** The gatehouse's counts and pixel checks could not be worked against the geometry, because the tools do not exist yet. T-69's first commit is a Core `GatehouseGeometry` test that works them before any frame, with a stop on any that fails. The planes, the apex and the window's fit were checked by hand.

## Required tasks

- [ ] [T-63](../tasks/T-63-faces-with-holes.md) — A face is an outer loop plus holes: define_mesh's object form, topology, triangulation, check, select, render, export and Carve file version 4
- [ ] [T-64](../tasks/T-64-tombstones-and-delete.md) — delete removes faces and vertices, leaving tombstoned slots so ids are never reused, through get_mesh, the file, render, select, check and export
- [ ] [T-65](../tasks/T-65-plane-cut.md) — plane_cut splits faces along a plane, keeping both sides or one with the cut filled, on Core's shared face-splitting step that reports each split face's pieces
- [ ] [T-66](../tasks/T-66-loop-cut.md) — loop_cut adds edge loops across a ring of quads at given fractions
- [ ] [T-67](../tasks/T-67-knife.md) — knife cuts a path of surface points through faces; a closed path inside a face cuts out an inner face and leaves a hole
- [ ] [T-68](../tasks/T-68-merge-faces-and-bridge.md) — merge_faces dissolves connected faces into one, and bridge joins two open loops with a ring of quads
- [ ] [T-69](../tasks/T-69-gatehouse-scenario.md) — The gatehouse wall built by cuts alone: cornice, gable, door and a see-through window, shown at each stage and kept as samples/gatehouse.carve
- [ ] [T-70](../tasks/T-70-cut-acceptance-run.md) — Fresh agents cut a wall into a gable with a see-through window from the descriptions alone, recovering from a knife point off the surface

## Retrospective

**As planned.**

- The order held: T-63, then T-64, then T-65 beside T-68, then T-66 beside T-67, then T-69 and T-70. Every rebase conflict between siblings was the planned one, adds only in `Program.cs`'s tool list and `CutsStages.cs`, and each member resolved its own with both sides kept. None needed the person.
- Carve file version 4 was free and T-63 took it. Faces with holes, tombstones, `plane_cut`, `loop_cut`, `knife`, `merge_faces`, `delete` and `bridge` are all reachable through the product's own MCP tools.
- The guard held: every existing frame stayed byte-identical by hash through every member. That is 121 frames at T-63, 127 at T-64, 130 at T-68, 133 at T-65, 135 at T-66 and T-67, and 137 at T-69, with the 26 gatehouse frames identical across two runs.
- The person approved every new frame set: T-63's 4, T-64's 2, T-68's 3, T-65's 3, T-66's 2, T-67's 2 (on the second try), T-69's 20, and T-70's final sheet.
- `GatehouseGeometry` came first, before any frame, as planned. The one planned count it could not meet (an edge hidden under the cornice) was a stop, not a change.
- The acceptance run passed in round 2. A fresh `claude -p` session (claude-fable-5-2, the default) cut the gable with two `plane_cut`s, the window with a closed `knife` on each face, then `delete` and `bridge`, and the door with a `knife`. It ended with a closed `check` and no warnings, a brown door, and background through the window. No description changed.
- The milestone's `verify` passes every check but `review`: 8/8 members merged and 12 suites passing.
- **Models and agents per member:**

  | Member | Model | Agents |
  |---|---|---|
  | T-63 | Opus 5.5 | 5 |
  | T-64 | Opus 5.5 | 3 |
  | T-65 | Opus 5.5 | 4 |
  | T-68 | Opus 5.5 | 2 |
  | T-67 | Opus 5.5 | 6 |
  | T-66 | Sonnet 5.5 | 1 |
  | T-69 | Sonnet 5.5 | 4 |
  | T-70 | Sonnet 5.5 | 2 |

  No rework was traced to the cheaper tier. T-66, on Sonnet, was the only member done by one agent. T-69's stops were spec faults, found from the geometry.

**Not as planned.**

- **Hand-offs dominated.** The eight members took 27 agents. Most hand-offs came at "code committed, a real run still to come": the GPU queue was shared with Empyrean, voidmarches, waystone and M-17's Carve members, and a queued `verify` often waited an hour or more. T-63 alone took 5 agents and about 6 hours, mostly in the queue. Twice the orchestrator stopped an agent sitting in a `verify` wait so it could not wake and write beside its continuation. Once it watched the queue itself rather than paying an agent to wait.
- **M-17 ran at the same time.** It was Active on `main` with no worktree on this machine. The orchestrator flagged this in the opening table but did not ask, since no ordering ruling covered it. T-63's first frame baseline came from M-17's queued runs, so it held 15 bone and weight frames this branch does not make.
- **The person ruled 25 times during the milestone.** Each ruling is in its member's As built. The rulings by member: T-63 3, T-64 2, T-65 5, T-68 2, T-66 2, T-67 6, T-69 2, T-70 3. All but two took the recommended option:
  - T-68's bridge example: the spec said "an open-ended box", but two quads facing each other bridge into a closed box facing inward. The person kept it as built, and the spec text was corrected.
  - T-67's recess frame: the person rejected it ("the cuts don't appear to be visible") and asked for the cut faces to be coloured.
- **Two stops came from the orchestrator's own briefs:**
  - T-67's shade prediction: Cuts' ray test predicts which face a pixel shows, not its shade, but the brief said it did. That cost a stop and two more rulings.
  - T-69's "in sight": a pixel rule failed on rounding, and the person chose a 3D ray test. The orchestrator set its nudge, 1e-3 of the bounds diagonal, without asking.
- **Readings landed without asking:**
  - T-66 landed a provisional refusal for a non-manifold ring although its spec said to stop and ask. The person kept it after the fact.
  - T-64 landed its own `Drawn` pixel check. The person kept it after the fact.
  - The orchestrator ruled two things itself: that T-67 owns the additive change to `FaceSplit` (both members' Notes sent a `FaceSplit` need to the orchestrator), and the colours of T-67's knifed faces (`#4a7a9b` for the hexagon).
- **New shared check rules, each ruled by the person:**
  - `Drawn`, for edges over the background (T-64);
  - `SeenNarrow`, for faces a few pixels wide (T-67);
  - the Crate colour rule, shared as `ColourRule.cs` and used in Cuts at 2 steps (T-67);
  - the geometric "in sight" test, local to the gatehouse (T-69).
- **The acceptance prompt changed.** Round 1 found "4 m tall" clashing with the apex at y = 3.4. The agent built a 3.4 m prism in `define_mesh` and cut no gable. The person had the first two sentences reworded to "a slab ... then cut a gable top into it", worded by the orchestrator. They also accepted `--output-format stream-json --verbose` into the run conditions, for the transcript.
- **The plant never fired.** In both rounds the agent worked out from the prompt's own numbers that the front is at z = 0.15, before any call. The person ruled that recovery by understanding counts, as in M-4.
- **One slow-runner failure.** `HoledFaceToolTests.A_bad_hole_is_a_tool_error_naming_it` timed out at server start-up (30 s) in a 20-minute `server-regression` run on Xellotath. It passed on the re-run, and no timeout was changed.

**Never verified.**

- **The knife's off-surface error was never met by an agent.** It is covered by tests and the Cuts scenario only. `loop_cut`, `merge_faces` and `undo` were used by no acceptance agent.
- **Checks shown failing only indirectly:**
  - T-69's `NotLine` on old edges has no break of its own.
  - Two of T-64's Cuts breaks are caught only as a failed run (`get_mesh` erroring, the stage throwing), not by their own check.
  - T-64 has no break for export bounds or wireframe face loops, since a tombstone at the origin cannot move either.
  - T-66's "walk order" break failed nothing; its id order rests on `FaceSplit`'s own rule.
  - T-67's slanted pocket walls are caught only by R5, and the lower-left wall clears the floor by only 3 to 4 steps against the 2-step rule.
- **Carried passes:**
  - `viewer-cube` was carried from `99c846e` (T-63) through most members, though it ran fresh in the milestone's `verify`.
  - T-63 to T-68 each hashed frames from the run of their last code commit, not from their final notes-only commit.
- **A Mobius ring for `loop_cut`.** The person ruled it refused; it is filed as B-3 under M-5, for the fix round before `finish`, and is not done.
- **Machines:** any GPU but Mantorok's and Xellotath's (both RTX 5070), and any client but `claude -p`.
- **The installed carve:** `C:\tools\carve` has not been republished.
- The orchestrator opened no frame. Every look above is a member's or the person's.

**Known and left.**

- B-3: `loop_cut` does not refuse a ring that closes on itself reversed. It is under M-5, open.
- `CLAUDE.md` and `README.md` are inputs of no suite (a `verify` warning).
- `branches/task/T-63` to `T-70` remain on disk, unregistered from git: `finish` ran from inside each one and could not remove it.
- `%TEMP%\carve-m5-acceptance\` (two rounds and a Release publish of `carve`) and the members' scratch folders in `%TEMP%` (`t63` to `t70`) are not cleared.
- Evidence is in this worktree's ignored `artifacts/review/T-63` to `T-69` and `artifacts/m5-acceptance/round-1` and `round-2`.

**The test report** (`cairn test report --since 2d`) covers M-5's and M-17's jobs together, since both share Carve's suites. It shows no flakes:

| Suite | Slot-seconds a week | Failures | Unique catches | cnr |
|---|---|---|---|---|
| `server-regression` | about 79,000 | 7 | 5 | 1 |
| `scenario-crate` | about 2,800 | 1 | — | — |

Four suites have one never-ran job each. The start-up timeout above was a slow runner, not a test fault. Nothing was filed. Whether a 30 s start-up timeout needs a margin under load is for the person.

**For the next plan.**

- **Plan for the GPU queue.** A member's hand-off at "a real run still to come" cost a whole agent each time the queue was long. Let a member submit its GPU suites early and keep working, or let the orchestrator hold a `verify` wait itself, instead of handing off.
- **Name the shared check rules at planning:** the colour rule, narrow faces, edges over the background, and "in sight" with occlusion. Four were invented mid-milestone, each a stop.
- **Work an acceptance prompt's numbers for clashes at planning.** "4 m tall" against the apex cost a round.
- **Plant something the prompt's own numbers cannot reveal.** A coordinate one subtraction away from the truth is solved before any call, as in M-4. The off-surface error is still unexercised.
- **Say in a brief what a check can predict before asking for a check against it.** The orchestrator's T-67 brief asked for "the shade the ray test predicts", which Cuts did not have.
- **Ask before running two milestones at once on shared suites,** as with M-17 here.
- Re-publish `C:\tools\carve` after `main` moves.
