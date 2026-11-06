---
description: >-
  The whole model saves to and reloads from the Carve file, and version 1 files still open
---
# T-35 — Carve file version 2

**Status:** Open

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
