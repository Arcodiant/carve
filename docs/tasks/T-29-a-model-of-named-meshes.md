---
description: >-
  The kernel and the store hold a model of named meshes, and every existing tool works on a named mesh with every frame unchanged
---
# T-29 — A model of named meshes

**Status:** In Progress

**Pri:** P1

**Milestone:** M-16

**Batch:** — (standalone)

**Depends on:** T-20

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

Carve holds a model, not one mesh. An agent can name meshes and aim any existing tool at one, and a session that names nothing works exactly as it does today.

## Scope

- **Core:** an immutable `Model` (`src/Carve.Core/Model.cs`), with named meshes in definition order. Names are non-empty and unique. Skeleton and clips arrive in T-31 and T-33.
- **Store:** `ModelStore` holds a `Model`; undo and version step over models, and the 100-step depth stays.
- **`define_mesh`** takes an optional `name` (default `mesh`). Defining an existing name replaces that mesh, and a new name adds one. Each is one undo step.
- **`move`, `extrude`, `get_mesh`, `save`, `open`** take `mesh`, which may be left out when the model has one. Left out with several, the tool error lists the names.
- **`render`** draws every mesh together, bounds taken over all of them.
- **A sample of the mechanic:** a two-mesh worked example in `define_mesh`'s description.
- **Tests:** `unit` for naming and refusals; `regression` for two meshes through the real tools, undo across them, and a two-mesh frame.

## Notes

- This task decides the `mesh` argument every later tool uses, so run it on the stronger model.
- `export` stays single-mesh until T-34: with several meshes it is a tool error saying so. `save` writes a version 1 file for one mesh and refuses several until T-35.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### Hand-off

Two agents so far, both on Opus 5.5. Agent 2 stopped at the ~60-call checkpoint. Work is committed but **not verified**; nothing is landed.

**Done (agent 1, see code):** `Model` (`src/Carve.Core/Model.cs`), `ModelStore` over models, `define_mesh name`, `mesh` on `move`, `extrude`, `get_mesh`, `check`, `select`, `save`, `open`; `export` refuses several meshes; `undo` reports every mesh when there are several; `render` draws `Model.Combined`; two-mesh worked example in `define_mesh`; `ModelTests`, `ModelStoreTests`, `NamedMeshTests`.

**Done (agent 2):**
- Ruling 1: `render` takes `mesh` (schema via `WithMesh`, own text). Labels and highlight apply to that mesh with its own ids; every mesh is drawn. Left out with one mesh, or with neither labels nor highlight: fine. Left out with several and labels/highlight asked: tool error listing the names. Done by `Model.OffsetOf(name)`: highlight ids are shifted by the mesh's offset in `Combined`, and labels get an `ElementWindow` (`LabelLayout.cs`, carried as `RenderSpec.LabelWindow`) that restricts them to the mesh's faces and writes ids counted from its start. With one mesh nothing changes (no window, zero offset), so old frames take the old path.
- Ruling 2: expected schemas updated in `CheckToolTests`, `DefineMeshTests` (count and the exact schema string), `EditToolTests` (render property list), `SaveOpenToolTests`, `SelectToolTests`. **Readings to report:** the argument lists inside unknown-argument messages were treated as expected schemas too (check: `which takes none` -> `its arguments are mesh`; select: `kind, where` -> `kind, where, mesh`), and the `EditToolTests` case that sent `name` to `define_mesh` as an unknown argument now sends `title` (name is an argument now). `define_mesh`'s name text uses `'mesh'`, not `"mesh"`, so the schema string has no escapes.
- `SelectTool` reads `store.Current` and passes the snapshot to a new `MeshToolSupport.Target(ModelSnapshot, ...)` overload, so `Select_reads_the_store_and_never_writes_it` holds unchanged.
- Fixes to agent 1's tests: `ModelStoreTests` moved the lid triangle's vertex along +Z (+X collapsed it onto vertex 1); `NamedMeshTests.TwoMeshExample` stops at the numbered lines; the "between the boxes" pixel moved to x = 1.9 (x = 1.5 lies on the cube's visible +x side, seen in the frame; the frame itself is right: cube left, tall box right).
- New regression test `Render_labels_and_highlights_the_named_mesh_alone_by_its_own_ids`: highlight on `right` face 1 byte-equal to face 7 of the same boxes as one mesh; orange on the tall box, not on the cube; label region differs near the tall box's face centre and not near the cube's (a window ignored would label the cube's face 1 too). Writes `artifacts/render-tests/two-meshes-highlight.png` and `two-meshes-labels.png`. Not yet run.

**Checked:** `core-unit`, `server-unit` pass after all edits. One `server-regression` run (before the test fixes above) failed 12: 10 now addressed above, plus **`RenderTests.A_queued_render_answers_during_a_stream_of_define_mesh` hung 2 h 16 m** ("No response to 111 after 00:00:30", then the run sat), making that run 2 h 20 m. Not diagnosed: the store diff looks equivalent; it may be machine contention or a real stall from the model store. **First thing next:** run `server-regression` again and see whether that test hangs again; if it does, it is a real regression to find (compare `ModelStore`/`RenderSystem` reads of `store.Current` with `dc7d16c`).

**Left:** that regression run; `scenario-cube`, `scenario-crate`, `scenario-shed`, `scenario-export` with frame hashes against `C:\repos\carve\branches\milestone\M-16\artifacts\review\T-29\hashes-before.txt`; look at the three two-mesh frames (one, scaled, already seen: `two-meshes-front-small.png` in the review folder); break table for the new checks; `### As built` (fold this section in); `verify`, `lint`, `finish`.
