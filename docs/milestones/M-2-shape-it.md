---
description: >-
  An agent builds a crate with move and extrude tools on a polygon kernel and views it from any angle
---
# M-2 — Shape it

**Status:** Active

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
  - A vertex's or face's id is its position, as `define_mesh` gives it, and edits never renumber or reuse one. An edge is its vertex pair `[a, b]`.
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
