---
description: >-
  An agent builds a crate with move and extrude tools on a polygon kernel and views it from any angle
---
# M-2 — Shape it

**Status:** Done

**One-liner:** *An agent builds a crate with move and extrude tools on a polygon kernel and views it from any angle*

## What this milestone delivers

An agent builds a crate with tool calls instead of listing every coordinate: it moves vertices, edges and faces and extrudes faces, on a polygon kernel, and views the result from any angle.

Content: the crate sample built only with these tools, a scenario that shows it, and tool descriptions with examples for each tool.

## Open at planning

- The edge-connected structure: half-edge or another, and its rules for non-manifold input.
- Faces with holes, which M-1 leaves out (an opening in M-1 is built from several faces).
- How elements are addressed: ids that survive edits, or something else (D-1, *Tool shape*).
- One tool per operation, or fewer general tools?
- Does an agent get undo here, or later?
- How a camera is specified in a render call.

**Decided at planning (2026-10-31).** Each decision was the person's, on a recommendation, after a probe against the M-1 build on the planning machine (RTX 5070 Ti, engine 0.117.0). The probe is kept in `artifacts/m2-probe/`, not committed.

- **Structure.** Immutable indexed faces plus an edge table derived per mesh: each edge lists its faces and the direction each crosses it.
  - An edge may have 1, 2 or more faces, so everything `define_mesh` accepts today stays legal with warnings, and the edits work on it.
  - Half-edge was rejected: it needs a manifold mesh or a second representation, and mutable links behind the immutable snapshot.
- **Holed faces** are not in M-2; they move to M-5.
- **Addressing.** Stable integer ids.
  - A vertex's or face's id is its position, as `define_mesh` gives it, and edits never renumber or reuse one (qualified after RV-10: along the current chain of edits; undo frees the undone step's ids). An edge is its vertex pair `[a, b]`.
  - An extruded face keeps its id as the cap; walls and new vertices are appended.
- **Tools.** One per operation:
  - `move(vertices?, edges?, faces?, by)`;
  - `extrude(faces, distance, inset = 0)`, each face separately, where distance 0 with an inset is a plain inset;
  - `get_mesh`, for ids, positions, normals, centres and edges;
  - `undo`.

  Each edit returns `define_mesh`'s report plus the ids it created. No ids are drawn on renders.
- **Undo.** The last 100 steps, `define_mesh` included, in memory, no redo. D-1's readable history stays open.
- **Bad edits.** A tool error naming each element, with the mesh and the undo stack unchanged. This covers an unknown id, an inset that collapses or inverts a face, a zero edit, and any result the validator refuses. A face bent out of plane is legal, with a warning.
- **Camera.** `render(view? | direction?, zoom = 1, target?)`.
  - `direction` runs from the point looked at toward the eye, auto-framed, with the light turning with it. Up is +Y, or -Z within 1° of vertical; `Matrix4x4.CreateLookAt` gives NaN looking along Y with up +Y, as measured.
  - Zoom (1 to 8) narrows the field of view about `target`, which defaults to the bounds' centre. The eye stays outside the bounding sphere.
  - Zoom 1 with no target is M-1's frame, byte for byte.
  - A target outside the bounds is an error.

**The crate**, agreed as a sketch and then on approximate real renders (`artifacts/m2-probe/view-front.png`, `view-top-tilted.png`, `view-bar-zoom3.png`):

- 1 x 0.8 x 1, frame 0.12, panels recessed 0.08, a handle bar on each X side reaching x = -0.02 and 1.02.
- Built from the `define_mesh` example cube with 11 edit calls; the probe gave 72 vertices, 70 faces, closed, no warnings.
- **Views**: `front` (direction (1.6, 1.2, 2.2)), `back` ((-1.6, -1.2, -2.2)), (-2.2, 1.2, 1.6), the tilted top (0.4, 2, 0.8), and `direction (2, 1, 1), zoom 3, target (1.02, 0.4, 0.5)` on the +X bar. The head-on top (0, 1, 0) was rejected as flat.

```text
front, direction (1.6, 1.2, 2.2)          zoom 3 on the +X bar, direction (2, 1, 1)
+------------------------------+          +------------------------------+
|        _.-''''''-._          |          |   frame  |  recessed +X panel|
|    _.-' .--------. '-._      |          |  ________|___________________|
|   |'-._'-.______.-'_.-'|     |          |  ====== bar top (bright) ====|
|   | .--'-._    _.-'--. |     |          |  |||||| bar face (dark) |||||
|   | |      '-..-'  .'| |     |          |  ----------------------------|
|   | |  panel |   .'=|| |     |          |   panel recess wall (mid)    |
|   | |  +Z    |  |==|| | <- +X bar      +------------------------------+
|   | '.____   |   '-'| |     |          caption: 512x512, direction (2, 1, 1), zoom 3,
|    '-._____'.|  .-'_.'      |                   target (1.02, 0.4, 0.5), 72 vertices, 70 faces
|         '-._ |_.-'           |
+------------------------------+
caption: 512x512, view front, 72 vertices, 70 faces
```

**Order.** T-8 and T-9 run side by side: kernel structure, and render. Then T-10 (on T-8), then T-11 (on T-10), then T-12 (on T-9 and T-11), then T-13.

**From M-1's retrospective, carried into every task:**
- a named list of open rules, with a stop;
- a positive pixel check on every frame;
- each new check shown failing on a broken product;
- what a fixer may not change;
- members leave their worktree before `finish`;
- a planted mistake in the acceptance run.

## Required tasks

- [ ] [T-8](../tasks/T-8-edge-table-and-stable-ids.md) — Carve.Core derives an edge table from each mesh, and vertex and face ids stay the same across every edit
- [ ] [T-9](../tasks/T-9-render-from-any-direction.md) — render takes any direction, a zoom and a target, so an agent can look at a mesh from any side and close up
- [ ] [T-10](../tasks/T-10-move-and-extrude-in-the-kernel.md) — Carve.Core moves vertices, edges and faces and extrudes or insets faces, keeping ids and refusing a bad edit by element
- [ ] [T-11](../tasks/T-11-edit-tools-and-undo.md) — An agent edits the mesh with move and extrude, reads its ids with get_mesh and steps back with undo
- [ ] [T-12](../tasks/T-12-crate-scenario.md) — The crate scenario builds a crate from one cube with move and extrude only, and shows it from five views in its own suite
- [ ] [B-1](../bugs/B-1-render-refuses-a-target-on-the-mesh-s-own-float-bound.md) — render refuses a target on the mesh's own float bound (found by T-12; fixed before T-13, the person's call)
- [ ] [T-13](../tasks/T-13-edit-tools-acceptance-run.md) — A fresh agent builds a crate with the edit tools from their descriptions alone, recovering from a planted mistake

## Retrospective

**As planned.**

- The order held: T-8 and T-9 side by side, then T-10, T-11, T-12 and T-13. T-9's long run overlapped T-10 and T-11 with no conflict; its rebase onto three landed siblings was clean.
- No member needed a hand-off, and none failed `verify`.
- The crate came out as planned through Core (T-10's `CrateTests`), through the client (T-11) and on screen (T-12): 72 vertices, 70 faces, closed, no warnings, bounds `(-0.02, 0, 0)` to `(1.02, 0.8, 1)`.
- T-9's guard held: the eight M-1 cube frames are byte-identical, checked by md5 before and after its rebase.
- T-12's two runs gave byte-identical frames, and its re-render, undo/redo and after-error frames each matched byte for byte.
- The person approved T-9's four new cube frames, T-12's eight crate frames and B-1's new zoom frame, all with no notes.
- T-13's fresh agents (Opus 5.5 in `claude -p`, plain `carve`, no Carve docs) built a closed crate in all three rounds. Each round used one `define_mesh` and then edit tools only, and used `direction`, `zoom` and `target` unaided. One description, `move`'s, was sharpened after round 2.
- **Models:**
  - Opus 5.5 on T-8 and T-10, which set the API others built on;
  - Sonnet 5.5 on T-9, T-11, T-12, B-1 and T-13.

  No rework was traced to the cheaper tier. T-10 moved from Sonnet to Opus at dispatch, because its own Notes call `EditResult` design-heavy.

**Not as planned.**

- **B-1 was added mid-milestone.** T-12 found that `render` refused the agreed zoom target `1.02`: the stored float bound is 1.0199999, and the check compared in double. The person had it fixed before T-13. It now compares in float, with no tolerance; the crate's zoom frame changed in 430 pixels, and the person approved it.
- **T-13's planted mistake was never hit.** In all three rounds the agent read `extrude`'s description, saw that an inset of 0.7 cannot fit a unit face, and built a 4-unit cube to avoid it. The person accepted the run. Round 2's unplanted `move` error (a face folding mid-edit), corrected on the next call from its text, stands as the evidence of recovery from error text.
- **Decided by the person during the milestone:**
  - T-10's inset uses a plain mitre at reflex corners, with no limit.
  - T-10's spec arithmetic is corrected: insetting six faces gives 30 faces, and 54 is after the recess. Both are checked.
  - From `[0.4, 2, 0.8]` the camera-turned light reaches only the top, so near-vertical sides share one ambient shade (118). The light was kept as is.
- **Readings members landed that the spec did not settle** (none was on a stop-and-ask list):
  - T-8: `Edit` refuses an unknown id with its own message.
  - T-10: it added two refusals, an extrude of no faces and a non-finite distance.
  - T-11: history is the current mesh plus 100 older ones, and undoing the first `define_mesh` is an error rather than a return to empty. `move` also returns `moved`.
  - T-12: the `back` check uses the -X bar's underside, because `back` looks up from below and hides its top.
  - T-9 captions a `view` with a zoom or target in the view form, e.g. `view back, zoom 2`.
- **The orchestrator's briefs said to leave the worktree before `finish`**, as every member's Notes did. But `finish`'s verify refuses unless it runs from inside the worktree. T-9 stopped on it and needed a second round trip; T-8 ran it from inside on its own reading.
- **Worktree folders.** `finish` run from inside its worktree cannot remove the folder.
  - T-8, T-9, T-10 and T-11 deleted theirs by hand, which took T-9's run frames with it.
  - T-12, T-13 and B-1 left theirs, as later briefs said. Their `artifacts/` was still removed, so the folder is left with nothing in it.
  - The kept evidence is only what was copied to the milestone worktree's `artifacts/review/` and `artifacts/m2-acceptance/`.
- There is no G: drive on this machine, so `G:\My Drive\artifacts\...` was not used for reviews. They went to the milestone worktree's ignored `artifacts/review/`.

**Never verified.**

- **Error recovery:** the planted mistake was never exercised by an agent, and neither was `undo` (no T-13 agent used it; T-11's and T-12's tests cover it).
- **Frames and machines:**
  - Any GPU or machine but the RTX 5070 Ti, and any golden or cross-machine frame comparison.
  - T-9's four new cube frames have no saved baseline: B-1 could not compare them by hash, and their reviewed copies are lost.
- **Checks shown failing on a broken product**, which M-1's retrospective required:
  - T-11 showed only three breaks; most of its new client tests had no break of their own.
  - T-12's centre-not-background check had no break.
  - T-12's recess-wall check was shown failing only on a head-on view, not on a crate built without the recess.
  - T-9's `[0, -1, 0]` break failed as a render error (NaN), not through a pixel check.
- **Shape and size:**
  - A real reflex-corner inset beyond the mitre: only the L-block cap was tested.
  - `get_mesh` on a large mesh: it is never trimmed.
- The orchestrator opened no frame. Every look above is a member's or the person's.

**Known and left.**

- The light from steep directions leaves the sides as one shade. The person kept it.
- T-13's agents could make only a solid bar handle, not a loop with a gap behind it. Bridge or cut is the visible next need (M-5).
- Agents tracked ids from each report's `created` and `extruded` fields and rarely needed `get_mesh`.
- Empty folders `branches/task/T-1` to `T-13` (M-1's and M-2's) and `branches/bug/B-1` remain on disk, unregistered from git.
- `CLAUDE.md` is an input of no suite (a `verify` warning since T-12 edited its Layout table).
- `carve-<pid>.log` in `$env:TEMP` and T-13's run folders in `%TEMP%\carve-m2-acceptance\` are never cleared.

**The review.**

- **Rounds:** four, RV-10 to RV-13. Round 1 split the diff over four Opus reviewers (Core kernel; edit tools and history; render and scenarios; the whole diff) and raised 23 findings: 1 Major, 11 Minor and 11 Nits.
- **The Major:** the cube's `[0, -1, 0]` frame was byte-identical to its `[1, 0, 0]` frame, so it proved nothing about the -Z up. A new L-block corner frame now does, shown failing with up swapped; the person approved it.
- **Fixes in round 1 (two fixers, Opus and Sonnet):**
  - errors are exhaustive;
  - culture-invariant numbers;
  - wall errors named by their source face;
  - a render target re-checked against the mesh actually shown;
  - argument-parsing tests.
- **The person's decisions in review:**
  - the id rule is qualified to the current chain of edits, and undo frees an undone step's ids;
  - unknown argument names are tool errors in all six tools;
  - edit tools accept `3.0` as an id.
- **Later rounds:** rounds 2 and 3 each found one Nit in the same message, the wall error's "too small"/"too large" (judged against the wrong length, then in float). The person chose a fourth round over leaving it. Round 4 found nothing.
- **Frames:** every frame stayed byte-identical through the refactor and the fixes, checked by hash against the approved copies, apart from the one new frame.
- **The review's lesson, as in M-1:** members' `VERIFY PASS` hid checks that could not fail. The Major was a check satisfied by a symmetric frame, and several tests passed on broken code.

**For the next plan.**

- **Plants and acceptance runs:**
  - Plant a mistake that no description can foresee: a wrong face id, a vertex pair that is not an edge, or a target off the mesh. Once a description lists an error, an agent avoids it rather than reading it.
  - Ask the acceptance agent for a final `get_mesh` in the prompt. T-13 had none to keep, because asking would have changed the prompt.
- **Worktrees and `finish`:**
  - Drop "leave the worktree before `finish`" from member Notes. The rule is to run `finish` inside the worktree and leave the folder.
  - Have cairn remove the folder once the shell is out of it, or keep `artifacts/` out of member worktrees entirely: write run frames to the container's worktree from the start.
- **Reviews:** resolve the review-artefact location per machine before the first review (no G: here).
- **Checks:** hold members to a break per new check, and ask for the table, not a summary. T-11 and T-12 left checks unbroken.
- **A check per frame that a wrong camera would fail:** symmetric meshes viewed head-on prove nothing about up or direction.
- **The light:** decide whether steep directions are worth fixing before M-5's renders lean on them.
