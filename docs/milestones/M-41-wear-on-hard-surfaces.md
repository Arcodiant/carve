---
description: >-
  Roughen part of a sharp-edged mesh in three calls (define_group, tessellate, displace) without rounding it or bending the faces around it
---
# M-41 — Wear on hard surfaces

**Status:** Done

**One-liner:** *Roughen part of a sharp-edged mesh in three calls (define_group, tessellate, displace) without rounding it or bending the faces around it*

## What this milestone delivers

An agent can give part of a hard-surface model real surface wear (worn flagstones, dented plates, chipped ashlar)
while every other face keeps its exact position: `define_group`, then `tessellate` on the group, then `displace` on
the group with its border pinned. Today it cannot. `subdivide` is Catmull-Clark only (it rounds hard-surface edges),
works on the whole mesh and stops at level 3. `displace` on a group moves the group's border vertices and bends the
faces around it out of plane.

- `tessellate` (T-237): a new edit that cuts the selected faces into triangles on their own plane, none longer
  than `max_edge`, keeping holes, moving no existing vertex, and adding the border points to the neighbours' loops
  so the mesh stays closed.
- `displace` takes `pin_border` (T-238).
- `moved` is short at `summary` verbosity (T-239), so a displace of thousands of vertices is not mostly a list of ids.

## Why

Requested from the Shoalward terrain project (modular sci-fi terrain for resin printing, fits toleranced to
0.2 mm). Tried on its piece `D-DAIS` (1,120 vertices, 777 faces, every edge sharp, units mm):
`displace {"source":{"noise":{"seed":7,"scale":4,"octaves":4}},"amount":0.4}` with no selection moved every vertex
(the peg holes and underside too), left 544 faces non-planar, warned the mesh was too coarse (edges average 7.979
against a scale of 4), and listed all 1,120 moved ids at `summary`.

The non-planar faces have two causes: the border vertices of a selection move, bending the unselected neighbours
(T-238 fixes this), and any face of four or more corners inside the selection goes non-planar once its corners move
by different amounts (only triangles stay flat, so T-237 makes triangles).

## Done when

On a fixture shaped like `D-DAIS` (a flat-topped slab with holed faces, pegs and sharp edges, built in Carve's own
tests; the Shoalward file is not in this repo), with its top faces in a group `wear`:

- `tessellate` at `max_edge` 0.75 leaves the mesh closed with no new warnings; every vertex outside the group is
  unmoved, and no face outside the group is split, only given border points in its loop.
- `displace` on group `wear` with `pin_border: true` then leaves every face outside the group planar and unmoved,
  and the mesh closed.

## Open at planning

- The algorithm for points inside a face: Carve's `Triangulator` only ear-clips the corners a face already has and
  there is no Delaunay code in `src`. A lattice and a constrained Delaunay triangulation is the obvious route; T-237
  settles it.
- Order: T-238 depends on T-237 (its tests and its Cuts stage build on the tessellated slab); T-239 is
  independent but touches `DisplaceTool.cs` beside T-238.

## Required tasks

- [x] [T-237](../tasks/T-237-tessellate-split-selected-faces-into-flat-triangles-down-to-max-edge.md) — A new edit that cuts selected faces into triangles on their own plane no longer than max_edge, keeping holes and moving no existing vertex, with the border points added to the neighbours' loops so the mesh stays closed
- [x] [T-238](../tasks/T-238-displace-pins-the-selection-s-border.md) — displace takes pin_border: a vertex also on a face outside the selected faces or group does not move, so the faces around a worn patch stay flat and unmoved
- [x] [T-239](../tasks/T-239-moved-is-short-at-summary-verbosity.md) — displace and move report moved as {count, first} at summary, the first 10 ids, and the whole list only at full

## Retrospective

Written 2026-11-27, before the person's go. The rebase onto `main`, the refactor pass and the review are still to come.

**As planned.**

- **The three calls work end to end.** `define_group`, `tessellate` and `displace` with `pin_border` are now enough to
  roughen part of a sharp-edged mesh. The Cuts scenario's `TessellateStage` does it on the real `carve`: a holed slab
  with a group tessellated (frame `tessellate-slab-wireframe`), then displaced with its border pinned (frame
  `tessellate-slab-worn`). The stage checks that every border vertex and every face outside the group is unmoved.
- **The *Done when* is held by tests**, on `TestMeshes.Dais()`, a 40 x 30 x 6 mm slab shaped like `D-DAIS`: two holed
  top faces in group `wear`, sockets, pegs, every edge hard.
  - At `max_edge` 0.75, `tessellate` leaves the slab closed with no warning and every vertex unmoved. Only the two
    `wear` faces are split, and no triangle has an edge over 0.75 or an angle under 10 degrees (T-237).
  - `displace` on `wear` with `pin_border` then leaves every border vertex and every face outside the group unmoved,
    and the mesh closed. The same displace unpinned moves the border, as before (T-238).
  - Both are tested in Core and through the tools.
- **The open question from planning is settled** (T-237). Points inside a face come from an equilateral lattice at
  `max_edge`, kept half a spacing clear of the loops. The face is then triangulated as a constrained Delaunay mesh:
  points added one at a time with Lawson flips, the loop edges restored by flipping, holes left out by parity. Any
  inner edge still over `max_edge` is split at its midpoint. The code is `src/Carve.Core/ConstrainedDelaunay.cs`.
- **The order held.** T-237 and T-239 ran side by side and did not collide; T-238 ran after T-237. Every rebase was
  clean.
- **`moved` is short at `summary`** (T-239): `displace` and `move` answer `{count, first}`.
- **`cairn milestone M-41 verify`** (2026-11-27): every check passes but `review`, which is written after the person's
  go. 3 of 3 members done and merged; 31 suites pass, 24 ran and 7 carried.
- **Models:**

  | Member | Model | Agents |
  |---|---|---|
  | T-237 | Opus 5.5 | 2 (a hand-off at the 60-call checkpoint) |
  | T-239 | Sonnet 5.5 | 1 |
  | T-238 | Sonnet 5.5 | 1 |

  Sonnet held on both concrete members with no rework.

**Not as planned.**

- **The person ruled once** (T-237, Waystone review round 1, 2026-11-27). The shared wireframe line check (within 10 of
  237) failed 17 edges in the tessellate stage: at some slopes 4x MSAA covers at most 3 of a 1-px line's 4 samples, so
  those edges come out at 212. The person chose option A: in that stage only, a piece edge also passes within 10 of the
  3/4-coverage blend over the fill. `ShedChecks.Line` is unchanged; `ShedChecks.Closest` became internal.
- **T-239 widened its `**Verify:**` line** to `server-unit, scenario-cuts, scenario-shed`. The Cuts and Shed scenarios
  read `moved` as an array, and `verify`'s coverage check refused the change without their suites.
- **The 127 x 2 strip gets inner points.** At `max_edge` 5 its edge-point rectangles have 5.28 diagonals, so the
  midpoint split puts one point on the middle line of each. Its edges are still cut along the length only, as scoped.

- **The review took four rounds** after the person's go: two reviewers in round 1, then one per round (RV-100 to RV-103, all claude-opus-5-5).
  - **The rebase and the refactor pass:** the rebase onto `main` was clean (Sonnet 5.5). The refactor pass (Opus 5.5) folded the faces-or-group lookup into `MeshToolSupport.FacesOrGroup` and changed no behaviour.
  - **Round 1:** nine findings.
  - **The person's two rulings** (2026-11-27, recorded in T-237):
    - `split`'s `pieces` are short at `summary`;
    - a 200,000 new-vertex budget, refused before any work, with the lattice filter made linear (9.7 s to 3.1 s near the budget).
  - **Round 2:** three findings.
    - The prediction under-counted narrow faces by up to 1.85x.
    - `split` still grew with the number of faces.
    - Thin rotated strips were refused with an internal message.
  - **Three answers from Waystone's filter, not the person** (2026-11-27, recorded in T-237 for the person to confirm or overturn):
    - `split` is `{count, first}` over faces at `summary`;
    - thin strips get a plain refusal, and the tolerance is filed as [B-15](../bugs/B-15-thin-faces-turned-off-the-axes-are-refused-by-the-triangulation.md);
    - the prediction keeps a margin of 3.5 points per `max_edge` of loop and is the gate, so some calls that would fit are refused (a 40 x 0.01 m strip at 0.0025: predicted about 218k, made 196k). A hard stop is only a backstop.
  - **Round 3:** one Nit, a wall-clock assert in a `unit` test, replaced by a timing-free one.
  - **Round 4:** nothing.
- Every round's fixes came from one agent on Opus 5.5, except round 3's (Sonnet 5.5).

**Never verified.**

- Shoalward's own `D-DAIS` file: it is not in this repo, so only the look-alike fixture was tested.
- Whether a real displacement looks good on T-237's uneven density. Triangles are finer between a socket and the
  slab's edge, and there are a few narrow ones by a socket. `check` warns of neither. The worn frame's bumps are
  small (+-0.06 at an oblique view), and the frame was judged only by a subagent.
- No agent session has tried the three calls from a plain prompt.

**Known and left.**

- `ExportToolTests.Other_tools_answer_while_an_export_runs` (`Patina.Server.Tests`, a timing test) failed once during
  T-238's first verify and passed on rerun. It is unrelated to this milestone.
- **The test report** (`cairn test report --since 1d`, 2026-11-27) shows flakes and nothing else to file:
  - `patina-unit`: 1 flaky and 2 failed runs;
  - `scenario-patina-library-1` and `-2`: 1 flaky and 1 failed run each.

  Filed as [B-14](../bugs/B-14-flaky-in-patina-unit-and-the-patina-library-scenarios.md), off-milestone. No suite
  stood out as costly for what it caught, and no launch could be shared.
- `finish` could not remove the folders `branches/task/T-237`, `T-238` and `T-239` (each in use). git no longer tracks
  them, so they can be deleted by hand.
- `falloff` on `displace` stays out, as planned.

**For the next plan.**

- Before a member adds a stage of 1-px lines at new slopes, measure the shared wireframe rule on those slopes. The
  MSAA under-coverage was found only at the first scenario run.
- When the scope changes the shape of a result that scenarios read, name those scenarios' suites in the member's
  `**Verify:**` line at planning.
