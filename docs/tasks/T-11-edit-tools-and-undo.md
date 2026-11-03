---
description: >-
  An agent edits the mesh with move and extrude, reads its ids with get_mesh and steps back with undo
---
# T-11 — Edit tools and undo

**Status:** In Progress

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
