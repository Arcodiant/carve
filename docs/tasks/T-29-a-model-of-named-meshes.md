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

Run on Opus 5.5, stopped at the ~60-call checkpoint. Work is committed but **not verified**; nothing is landed.

**Done (in code):**
- `src/Carve.Core/Model.cs`: immutable `Model` of `NamedMesh` in definition order; `With` replaces in place or appends; `Target(name)` resolves a tool's `mesh` (only mesh when omitted; refusal listing `'a', 'b'` when omitted with several or unknown); `Combined` concatenates meshes for render (one mesh returns the same instance, so single-mesh frames take the old path).
- `ModelStore` holds a `Model` per undo step (depth 100 kept); `ModelSnapshot.Mesh` is now `Model.Combined`. New `Define`, `DefineTarget`, named `Edit`; the old `Push(mesh)`/`Replace`/`Edit` overloads stay for existing tests.
- `define_mesh` takes `name` (default `mesh`); two-mesh worked example (crate + lid, lid moved) placed **before** the cube example, which stays last because `ToolList.Cube` parses the cube from its marker to the end.
- `move`, `extrude`, `get_mesh`, `check`, `select`, `save`, `open` take `mesh`. `open` without `mesh` replaces the only mesh keeping its name (or `mesh` when empty); with `mesh` it replaces/adds that name. `save` writes the named/only mesh; omitted with several it is refused listing names.
- `export` with several meshes: tool error saying so. `undo` with several meshes reports `{meshes:[{name, ...report}], undone}`; with one, the old report.
- `render` draws `Combined`. **Reading not settled by the spec:** with several meshes, `labels` and `highlight` are refused (ids are per mesh), rather than giving `render` a `mesh` argument. Needs the person's call.
- Tests: `tests/Carve.Core.Tests/ModelTests.cs` (unit), new cases in `ModelStoreTests.cs` (unit), `tests/Carve.Server.Tests/NamedMeshTests.cs` (regression, incl. a two-mesh frame byte-compared against the same boxes as one mesh, with pixel checks a mirrored or flipped camera fails).

**Checked:** `core-unit` and `server-unit` pass locally (before NamedMesh/ModelTests were added; the new unit tests compile but were not run). Build is clean.

**Left:**
1. `NamedMeshTests` all fail at the last local run; cause not yet read (first suspect: `TwoMeshExample` parsing of the description lines). Read one failure's message and fix.
2. Existing tests that assert the old schemas now fail: `CheckToolTests.Any_argument_is_a_tool_error_and_the_schema_is_closed`, `DefineMeshTests.The_published_input_schema_is_the_typed_one`, `DefineMeshTests.Tools_list_shows_the_schema_and_the_description_rules` (likely also EditTool/SaveOpen/Select schema tests, and reading-tool "any argument is an error" tests). Updating them to include `mesh`/`name` changes test targets: **ask the person first** per the brief.
3. Run `server-regression`, then `scenario-cube`, `scenario-crate`, `scenario-shed`, `scenario-export` and compare frame hashes with `C:\repos\carve\branches\milestone\M-16\artifacts\review\T-29\hashes-before.txt` (70 frames, from the last queued pass of each suite, stored under `before/` there). Add the scenario suites to `**Verify:**` if `verify` asks.
4. Break table (each new check shown failing), look at `artifacts/render-tests/two-meshes-front.png`, `### As built`, `verify`, `lint`, `finish`.

**Surprising:** the first `server-regression` run (166 failures) was spoiled by rebuilding `carve.dll` while it ran; do not build in the worktree while a suite is running. Python heredoc edits of CRLF files mangled `\n` in C# strings once; use the Edit tool for string literals.
