---
description: >-
  The whole model saves to and reloads from the Carve file, and version 1 files still open
---
# T-35 — Carve file version 2

**Status:** In Progress

**Pri:** P1

**Milestone:** M-16

**Batch:** — (standalone)

**Depends on:** T-33

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

The whole character survives a restart: `save` and `open` carry meshes, colours, groups, skeleton, weights and clips.

## Scope

- **`CarveFile` version 2:** a `meshes` list of named objects (`vertices`, `faces`, `colours`, `groups`, `weights`), `skeleton` and `clips`, in a fixed key order so the same model writes the same text and diffs mean something. Floats read back bit-exact.
- **Version 1 files** open as one mesh called `mesh`. `Write` always gives version 2.
- **Errors** name the element, as T-15's do. Reading holds a file to exactly what the tools accept: the same refusals for bones, weights and clips.
- **`open`** replaces the whole model as one undo step.
- **Description:** `save` shows a short version 2 file.
- **Tests:** `unit` for round-trip bit-exactness of every field, text stability, version 1 still reading, and each refusal; `regression` for a save, a new process, `open` and an identical `get_mesh`, `get_skeleton` and frame.

## Notes

- The format is public and committed by games, so run on the stronger model.
- T-42 adds its keys as version 3, keeping 1 and 2 readable.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### Hand-off

Written by Opus 5.5 at the 60-call checkpoint (2026-11-09).

**Done (committed, not yet verified by `verify`):**

- `CarveFile` version 2 (`src/Carve.Core/CarveFile.cs`): `Write(Model)` (and `Write(PolygonMesh)` as one mesh named `mesh`); `Read` dispatches on `carve`: 2 is read whole, anything else goes through T-15's version 1 path unchanged except that the version messages now say "versions 1 and 2". `CarveFileResult` has `Model`; `Mesh` is the only mesh when there is one. Layout: keys `carve, meshes, skeleton, clips`; a mesh `name, vertices, faces, colours [{colour, faces}], groups [{name, faces}], weights [{vertex, bones {name: weight}}]`; `skeleton` is `null` or a list of `{name, parent, position, rotation}`; a clip `{name, duration, loop, channels {bone: {rotation?, translation?: {interpolation, keys [{time, value}]}}}}`. Exact text: `CarveFileV2Tests.ArmText` and `.CubeText`.
- Shared refusals moved into the Core and used by both tools and reader: `VertexWeights.Raw` (from `ModelStore.SetWeights`), `VertexWeights.VertexProblem`, `FaceStyle.FaceProblem`, `FaceStyle.GroupNameProblem`; `Model.Create` (names, weights need a skeleton, duplicate clip names), `Skeleton.Create`, `Clip.Create`, `PolygonMesh.Create`.
- Ruling 1: `save` without `mesh` writes the whole model; with `mesh`, `Model.Piece` (the mesh, plus skeleton and all clips when it has weights, else the mesh alone). `open` without `mesh` replaces the model (`ModelStore.OpenModel`, one step); with `mesh`, `ModelStore.OpenMesh` / `Model.WithMeshFrom` (the file's mesh of that name, weights moved onto the model's bones by name, refused when a bone is missing or the file has no such mesh). Descriptions rewritten; `SaveTool.CubeFile` is the version 2 cube.
- Ruling 2: `samples/leaky-shed.carve` rewritten as version 2 (text equal to what `Write` gives, by construction); `MeshHealthTests.The_shed_file_is_the_code_s_mesh` compares by content, for the sample and for the version 1 copy `tests/Carve.Core.Tests/Fixtures/leaky-shed-v1.carve`.
- Tests: `CarveFileV2Tests` (unit, all pass with the rest of `Carve.Core.Tests`, 513), `CarveFileVersion2Tests` (regression: whole model across processes with identical `get_mesh`, `get_skeleton`, rest and clip frames and re-saved bytes; open replacing the whole model; save and open of one mesh). Changed tests by ruling 1: `NamedMeshTests` (save/open rows removed from the refuse-to-guess theory; open by a name the file lacks is refused); `SaveOpenToolTests` and `CarveFileTests` messages ("versions 1 and 2", "the model is unchanged"; version 3 instead of 2 as the unread version).

**Left:**

- `cairn test run server-regression` (expect possibly `EditToolTests.Tools_list_shows_each_schema` to need save's/open's new `mesh` descriptions: an allowed expected-schema update, name it), then `cairn task T-35 verify`; add `scenario-shed`, `scenario-export` to `**Verify:**` if coverage asks.
- Frame hashes after: compare against `M-16/artifacts/review/T-35/hashes-before.txt` (81 frames, matching T-33's list plus its two wave frames). Old export `crate.carve` sha256 `4367b294854572032016715c50ffdd1cafa15aaaf4b6146dd79e5828ade709cc`; record the new one. The export scenario's only check on it is its own re-save byte check, which still holds.
- Break table into `M-16/artifacts/review/T-35/` (not yet done).
- `### As built`, lint, finish.

**Readings not settled by the spec (report them):** weights already normalised (at most 4, sum within 1e-12 of 1, `VertexWeights.Kept`) are kept bit for bit on reading, others normalised and capped as `set_weights` does; duplicates in a file (mesh name, group name, a face coloured twice, a vertex weighted twice, clip name) are refused rather than last-wins; `colours`, `groups`, `weights`, `skeleton`, `clips` and `loop` may be left out; a file with no meshes is refused; `save` with `mesh` of an unweighted mesh writes no skeleton or clips; a version 1 file opened with `mesh` other than `mesh` is refused (its one mesh is named `mesh`); `open`'s report adds `bones` and `clips` when there is a skeleton, and `meshes` when several.

**Surprising:** heredoc Python with `'\n'` in this shell wrote real newlines into C# strings; use `chr(92)`. `get_mesh` does not report weights, so the regression test checks them through a re-saved file.
