---
description: >-
  The kernel and the store hold a model of named meshes, and every existing tool works on a named mesh with every frame unchanged
---
# T-29 — A model of named meshes

**Status:** Done

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

### As built

Three agents, all on Opus 5.5.

- **Core:** `Model` (`src/Carve.Core/Model.cs`) is immutable: named meshes in definition order, names non-empty and unique. `With` replaces a mesh in its place or adds one at the end. `Target` picks the named mesh, or the only one when no name is given, and its refusal lists the names. `Combined` is every mesh as one, with ids offset; a one-mesh model returns that mesh itself, so old frames take the old path unchanged. `OffsetOf` gives where a mesh starts in `Combined`.
- **Store:** `ModelStore` keeps whole models, so undo and version step across meshes. The depth is still 100.
- **Tools:** `define_mesh` takes `name` (default `mesh`) and has a two-mesh worked example (crate and lid). `move`, `extrude`, `get_mesh`, `check`, `select`, `save` and `open` take `mesh`. `export` refuses several meshes and names them (*superseded by T-34*, which merges them, or skins them with a skeleton; RV-24 A7). `undo` reports every mesh when there are several. `render` draws every mesh, framed over all of them.
- **Ruling 1:** `render` takes `mesh`, which `labels` and `highlight` apply to with that mesh's own ids. Highlight ids are shifted by the mesh's offset, and labels are limited to the mesh by an `ElementWindow` (`RenderSpec.LabelWindow`). If `mesh` is left out while there are several meshes and labels or highlight are asked for, the tool error lists the names.
- **Ruling 2:** expected schemas updated in `CheckToolTests`, `DefineMeshTests`, `EditToolTests`, `SaveOpenToolTests` and `SelectToolTests`. This covers the argument lists in unknown-argument messages (`check`: "its arguments are mesh"; `select`: "kind, where, mesh"), and the `EditToolTests` unknown-argument case now sends `title`. `define_mesh`'s name text uses no quotes or apostrophes, because the schema encoder writes those as `\u0027`/`\u0022`.
- **Readings the spec left open:** `save`/`open` with several meshes use `mesh` to pick which one (*superseded by T-35*: with no `mesh` they work on the whole model, with one on that mesh; RV-24 A7); `export`'s refusal suggests saving each mesh on its own or defining one mesh. Edit and define results do not name the mesh (ruling 3).

**Checked.** `server-regression` passed twice with no hang (257 s; an earlier run's only failure was the schema escape fixed above). `RenderTests.A_queued_render_answers_during_a_stream_of_define_mesh` passed both times. The 2 h 16 m hang did not reproduce, and reading `ModelStore`, `RenderPump` and `RenderTool` found nothing that waits differently from before. `scenario-cube`, `scenario-crate`, `scenario-shed` and `scenario-export` pass, and all 70 existing frames are byte-identical to `hashes-before.txt` (`hashes-after.txt` beside it in `M-16/artifacts/review/T-29/`). The two-mesh highlight and label frames were looked at: orange only on the tall box's front face, and labels 1, 3, 5 only on the tall box. Break table (`break-table.txt`, script `breaks.ps1`, same folder): 14 product breaks, run as one queued job. With no break, all 31 tests of `ModelTests`, `ModelStoreTests` and `NamedMeshTests` pass, and every one of the 20 new tests fails under at least one break.