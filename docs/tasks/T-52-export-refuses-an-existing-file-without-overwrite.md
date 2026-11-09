---
description: >-
  Export refuses an existing file without overwrite
---
# T-52 — Export refuses an existing file without overwrite

**Status:** In Progress

**Pri:** P2

**Milestone:** M-16

**Batch:** — (standalone)

**Depends on:** T-46

**Verify:** server-regression, scenario-export

**Perf:** skip: Carve declares no heavy suites

## Goal

`export` refuses to replace an existing file unless given `overwrite: true`, as D-6 and D-1 say (the person, 2026-11-06), in every mode.

## Scope

- **`export(path, overwrite?)`:** `overwrite` optional, false by default, in the closed schema beside `allow_unweighted` and `mode`. An existing file without it is a tool error naming the path and `overwrite`, with the file unchanged. With it, the file is replaced and the result says `replaced`. Applies to `model` and `clips` modes alike. `save` is unchanged: it replaces, since the `.carve` file is the working copy.
- **Description:** `export`'s description and its `path` text say so (the path text today says "replaced if it exists").
- **Existing tests and the export scenario** that export twice to one path follow the rule: the second export passes `overwrite: true`, or asserts the refusal. Name each changed test target in As built.
- **Tests:** `unit` (`ToolPaths`): refusal with the file byte-unchanged, `replaced` with `overwrite`; `regression`: the refusal and `overwrite` through the real `carve`, in both modes, with identical bytes on replacement.

## Notes

- Found by the orchestrator on 2026-11-09 before T-36: the person ruled on 2026-11-06 that `export` refuses to overwrite, and T-16's file was edited to say so, but T-16 had been built on 2026-11-05, so the rule was never implemented. The person chose a task of its own in M-16, ahead of T-36.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame and exported file, by hash before and after; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### As built

Built on Sonnet 5.5.

- `ToolPaths.Write` takes `overwrite` (default true, so `save` is unchanged). With it false, an existing file is a tool error `export: <abs path> already exists; pass overwrite: true to replace it. Nothing was written.`, checked before any folder is created or temp file written, the file untouched.
- `export` takes `overwrite` (boolean, default false; non-boolean refused like `allow_unweighted`), in every mode; schema order `path, allow_unweighted, mode, overwrite`. The description, the `path` text and the error list say so. Export bytes are unchanged.
- Test targets changed (only these): `ExportToolTests.Tools_list_lists_export_with_a_closed_schema` (expected schema and unknown-argument text gain `overwrite`); `ExportToolTests.Export_writes_the_cube_...` (second export, to `CUBE.GLB`, passes `overwrite: true`; refusal asserted before it); `SkinnedExportToolTests.Unweighted_vertices_...` (the `allow_unweighted` export over the pre-existing file passes `overwrite: true`); `SkinnedExportToolTests.Clips_mode_...` (the second exports assert the refusal with bytes unchanged, then `overwrite: true` with identical bytes, both modes); `ToolPathsTests` (replace case passes `overwrite: true`, new refusal test); `ExportScenario` (second export asserts the refusal and the bytes, then passes `overwrite: true`).
- Break table and hash lists: `artifacts/review/T-52/` of the milestone worktree.


Two agents ran this member, both on Sonnet 5.5. Verified: `server-unit`, `scenario-export` and `server-regression` PASS on the fixed product (VERIFY PASS). The `scenario-export` run shows the refusal text on the second export and then `overwrite: true` succeeding. Against T-46's `hashes-after.txt`, every `scenario-export` frame, `crate.glb`, `crate.carve` and the viewer files hash identically; only `export-result.txt` (job temp path) and `run.json` (the added `overwrite: true` argument and the refusal text) differ, as expected. Lists in `artifacts/review/T-52/hashes-after.txt` of the milestone worktree.
