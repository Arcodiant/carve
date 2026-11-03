---
description: >-
  An agent edits the mesh with move and extrude, reads its ids with get_mesh and steps back with undo
---
# T-11 — Edit tools and undo

**Status:** Done

**Pri:** P1

**Milestone:** M-2

**Batch:** — (standalone)

**Depends on:** T-10

**Verify:** server-regression, scenario-cube

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent edits the current mesh through four new MCP tools, `move`, `extrude`, `get_mesh` and `undo`, and learns each from its description and worked example alone.

## Scope

In `src/Carve.Server`, a tool class per tool:

- **`ModelStore` history**:
  - `define_mesh` and each successful edit push a snapshot. `undo` pops one.
  - The **last 100** snapshots are kept. `undo` with nothing older is a tool error saying so.
  - Every change, an undo included, raises `Version` by exactly 1, so `render` redraws. `Current` and `Version` keep their M-1 contract: immutable snapshots, never torn.
  - A failed call pushes nothing.
- **`move(vertices?, edges?, faces?, by)`**: ids as integers, edges as `[a, b]` pairs, `by` as exactly three numbers (hand-built schema with `minItems`/`maxItems`, as `define_mesh` does).
- **`extrude(faces, distance, inset = 0)`**.
- **The edits' return** is JSON: `define_mesh`'s report (`vertices, faces, edges, closed, bounds, warnings`) plus `created: {vertices: [...], faces: [...]}`. For `extrude` it also has `extruded: [{face, cap, walls: [...], vertices: [...]}]`. Errors are tool errors listing every problem by element, with the mesh unchanged, from T-10's `EditResult`.
- **`get_mesh()`** returns JSON:
  - `vertices: [{id, at: [x,y,z]}]`;
  - `faces: [{id, vertices: [...], normal: [...], centre: [...]}]`;
  - `edges: [{vertices: [a,b], faces: [...]}]`;
  - the report fields.

  With no mesh yet, it is a tool error saying to call `define_mesh`.
- **`undo()`** returns the report of the mesh now current and `undone: "<tool name>"`.
- **`define_mesh`'s description** gains one sentence: vertex and face ids are the positions given here, and edits keep them.

**The tool descriptions, as a priority; they are the interface.**

- Each description states its ids, units and sign conventions, what it returns, and its errors.
- Each carries a **worked example on the `define_mesh` example cube**. For `move`: raise the top face, `faces [3] by [0, 0.5, 0]`. For `extrude`: inset then recess the +Z face. For `get_mesh`: a trimmed example output. For `undo`: a one-line example.
- `extrude`'s description says what the cap and walls are and which ids they get, that a negative distance recesses, and that the inset is in the face's plane.
- A test parses each worked example out of its description and runs it against the cube, asserting it is accepted, closed and warning-free, so no example can rot.

**Content: the crate's build as a test.** A `tests/Carve.Server.Tests` test drives the crate's 12-call sequence (T-10's Notes) through the real client. It asserts a closed 72-vertex, 70-face mesh with no warnings, with only `define_mesh` (the example cube) and the edit tools. T-12 films it.

Other tests through the client:

- Each error comes back as a tool error and leaves `get_mesh`'s output unchanged.
- `undo` after an extrude restores the exact `get_mesh` output.
- A 101st `undo` back fails.
- `Version` rises on undo.
- `tools/list` shows each schema.
- **Show the new tests failing** on a broken product and say so in the closing report.

Register the hand-built tools in `Program.cs` beside `define_mesh`.

## Notes

- **Decided by the person:**
  - One tool per operation; `move` takes any mix of vertices, edges and faces.
  - `get_mesh` is how an agent learns ids; no ids are drawn on renders.
  - Plain undo for the last 100 steps, in memory, no redo, `define_mesh` included.
  - A bad edit is an error with the mesh and the undo stack unchanged.
- **Open rules: stop and ask.**
  - Any change to the report's existing fields.
  - A `get_mesh` that trims or pages its output (M-4's queries own selection).
  - A redo, or a history that survives a restart.
  - Any wording that contradicts M-1's `define_mesh` and `render` text.
- Files owned: `ModelStore`, `Program.cs`, `DefineMeshTool`, the new tool files, their tests, and D-1's *Open questions* on **Tool shape** and **Undo and history**. Answer those two with what was decided, leaving the readable history open. T-9 owns the render files and does not touch `ModelStore`.
- Tool text may be sharpened by T-13 after a real agent has used it.
- Writes down for T-12: the exact JSON field names of the edit results.
- Leave the worktree before `finish`.

### As built

Ran on Claude Sonnet 5.5 (`claude-sonnet-5-5`).

**Tools**, all hand-registered in `Program.cs` beside `define_mesh` (`MoveTool`, `ExtrudeTool`, `GetMeshTool`, `UndoTool`; shared plumbing in `MeshToolSupport`). Arguments are bound raw and validated by the tool so every problem comes back in one tool error, `"<tool> failed with N problem(s); the current mesh is unchanged:"` then one line per problem. No mesh yet: `"<tool>: there is no mesh yet; call define_mesh first"`; `undo` with nothing older: `"undo: nothing to undo; there is no older state of the mesh"`.

**JSON field names, for T-12.** Every result carries `define_mesh`'s report: `vertices` (count), `faces` (count), `edges` (count), `closed`, `bounds: {min: [x,y,z], max: [x,y,z]}`, `warnings: [string]`.

- `move({vertices?: [int], edges?: [[a,b]], faces?: [int], by: [dx,dy,dz]})` adds `created: {vertices: [], faces: []}` (always empty) and `moved: [vertex ids, ascending]`.
- `extrude({faces: [int], distance: number, inset?: number = 0})` adds `created: {vertices: [ids], faces: [ids]}` and `extruded: [{face, cap, walls: [face ids], vertices: [ring vertex ids]}]`, one per listed face in listed order (`cap == face`).
- `get_mesh()` returns `vertices: [{id, at: [x,y,z]}]`, `faces: [{id, vertices: [ids], normal: [x,y,z], centre: [x,y,z]}]`, `edges: [{vertices: [a,b], faces: [ids]}]` (a < b), plus `closed`, `bounds`, `warnings`. Here `vertices`/`faces`/`edges` are lists, not counts. Not trimmed.
- `undo()` returns the report and `undone: "<tool name>"` (`define_mesh`, `move` or `extrude`).

**Readings of the spec.**

- *"Last 100 snapshots"* with *"a 101st undo back fails"*: the store keeps the current mesh plus 100 older ones, so exactly 100 undos work and the 101st fails. With a long history the first `define_mesh` falls off the back.
- Undoing the very first `define_mesh` is the "nothing older" error (the store does not go back to empty).
- `ModelStore.Replace` stays (as a `define_mesh` push) so `RenderPump`'s contract and its tests are untouched; new members are `Push`, `Edit` (edit-and-push under one lock) and `Undo`.
- `move` also returns `moved`, an addition beside the spec's fields; no existing field changed.
- The worked examples are lines starting `Example: ` in each description; `get_mesh`'s trimmed output is a line starting `Output: ` and is not run.
- The `define_mesh` sentence added: "Vertex and face ids are the positions given here (...), and move and extrude keep them; get_mesh reads them back and undo steps back."

**D-1.** *Tool shape* and *Undo and history* answered; the readable/replayable history is left open.

**Tests.** `EditToolTests` (regression, through the real client): every worked example run on the cube (closed, warning-free), the crate's 12-call sequence (72 vertices, 70 faces, closed, no warnings), 13 error cases each leaving `get_mesh` unchanged and not counting as an undo step, undo restoring the exact `get_mesh` text, 101st undo failing, `tools/list` schemas. `ModelStoreTests` (unit): version rises by 1 on undo, the 100-step window, a failed edit pushes nothing.

Shown failing on a broken product: `UndoDepth = 99` failed `The_hundred_and_first_undo_back_fails`, `Keeps_the_current_mesh_and_the_last_hundred_older_ones` and `Undo_steps_back_to_the_older_mesh_and_raises_the_version_by_one`; undo not raising the version failed the last of those; `get_mesh` with a flipped normal failed `Get_mesh_lists_vertices_faces_and_edges_with_the_report_fields`. Code restored. The other tests had no break of their own.
