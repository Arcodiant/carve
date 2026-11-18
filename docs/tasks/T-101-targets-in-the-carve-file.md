---
description: >-
  The Carve file stores each mesh's targets as sparse deltas in a new version, read back bit-exact, and open refuses to replace a mesh's targets unless asked
---
# T-101 — Targets in the Carve file

**Status:** In Progress

**Pri:** P2

**Milestone:** M-21

**Batch:** — (standalone)

**Depends on:** T-99

**Verify:** core-unit, server-unit

## Goal

`save` and `open` keep targets, bit-exact, and a model with no target is written exactly as before.

## Scope

- **The version.** Use the next version after the highest `CarveFile` writes on `main` when this task starts. That
  is 7 if nothing has taken it. M-9's T-92 also plans "version 7" for the part tree, so check, and take the next
  one free. Follow the lowest-version rule (the person, 2026-11-11): the new version is written only when some
  mesh has a target. Every other model's text stays byte for byte as today.
- **Layout.** In a mesh, after the last key the previous version adds, write `"targets"`: one object per target in
  definition order, `{"name": "...", "deltas": {"<id>": [dx, dy, dz], ...}}`, ids ascending, one target per line
  as `materials` is written. Numbers use the round-trip form the file already uses for positions.
- **Reading** refuses, each named by element: a duplicate name, a bad name, an unknown or deleted id, a
  non-finite number, a zero delta (never written, so a hand edit), and a key not in this version.
- **`open`** replacing a mesh that has targets (a named `open mesh=` over an existing mesh, or a whole-file open
  over a model with targets) is refused, naming the targets, unless `drop_targets: true`. The same flag on
  `define_mesh` is T-102's. Undo restores them either way.
- **`save mesh=`** carries that mesh's targets.
- Update `CarveFile`'s remarks and the CLAUDE.md line on the file's versions.
- **Tests:** round trip bit-exact; every refusal; a model without targets written byte-identical to the previous
  build, with every committed sample re-read and re-written equal; `open` refusal and `drop_targets`. Show the
  round-trip test failing on a write that rounds through double, and record it.

## Notes

- **Decides a file format: stronger model.**
- Owns `CarveFile.cs`, `SaveTool.cs`, `OpenTool.cs` and CLAUDE.md's file-version sentence in this wave.
- **Goes back to the person, stop and ask:** the version number if both 7 and 8 are spoken for; any change to a
  file without targets; storing anything but sparse deltas.
- Write in As built: the version taken, and one example `targets` line for T-105's sample.
