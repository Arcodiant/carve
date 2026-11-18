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

### As built

Built by Claude Opus 5.5 (the stronger model the Notes ask for).

- **Version 8.** Version 7 is M-9's (T-92, the part tree, Planned), and nothing else claims 8. `CarveFile.Version` is 8 and
  `ReadableVersions` is 1 to 6 and 8; version 7 is refused as "not one this build reads". Version 8 is version 6 with
  `targets` and is written only when some mesh has a target; every other model is written as before, byte for byte.
  When T-92 lands it takes 7 for the part tree and must read 8 as well (a file of parts and targets then needs a version
  above both, or 8 grows the part keys: T-92's call, recorded here for it).
- **Layout.** In a mesh, last (after `seams`, or `hard_edges` when there are no UVs), left out when the mesh has none:
  one target per line in definition order, deltas by id ascending, each float in the `R` form positions use. A target
  with no delta (possible after edits) is written `"deltas": {}` and read back. The example line for T-105's sample:

  ```
        {"name": "Bulk", "deltas": {"0": [0.3, 0.7, -0.9], "3": [0.1, -0.2, 0.3], "7": [-0, 0.33333334, 0]}}
  ```

- **Reading** refuses, by element (`meshes[0].targets[1].name`, `meshes[0].targets[0].deltas.12`, `...deltas.12[0]`): a
  repeated or bad name, a key that is not a decimal vertex id (`x`, `-1`, `03`), an id the mesh lacks or has deleted, a
  vertex given twice, a delta that is not three numbers, a non-finite number, a zero delta (`[0, 0, 0]` and `[-0, 0, -0]`),
  an unknown key in a target, and `targets` in a file of version 6 or below (unknown key). The final target is made by
  `MorphTarget.FromDeltas`.
- **`open`** takes `drop_targets` (boolean). Without it, a whole-file open over a model with any mesh that has targets, or
  `open mesh=` over a mesh of that name with targets, is a tool error naming the meshes and targets
  (`drop_targets: ... (mesh 'arm': 'Bulk', 'Lean'); pass drop_targets: true ...`). The check runs under the store's
  write lock: two new `ModelStore.OpenModel`/`OpenMesh` overloads take the refusal (`ModelStore.cs`, outside the files
  the Notes name, a small addition only). Undo brings the targets back either way (tested). `save mesh=` carries the
  mesh's targets (`Model.Piece` already did; tested).
- **Tests.** `tests/Carve.Core.Tests/MorphTargetFileTests.cs` (round trip bit-exact by bits, layout pinned, every refusal,
  version 7 refused, every committed `.carve` under `samples/` and the fixtures re-read and re-written byte-equal, found
  by directory so T-105's sample joins it) and `tests/Carve.Server.Tests/OpenTargetsToolTests.cs` (open and save keep
  targets, save mesh=, both refusals, `drop_targets`, undo). The version list in two refusal tests and open's schema
  test were updated for 8 and `drop_targets`.
- **Broken product shown.** With the writer putting each delta through a double sum with its base
  (`(float)((double)base + delta) - base`), the round-trip test failed on the far-from-origin cube: Bulk's vertex 0 came
  back as bits (1050253312, 1060319232, -1083809792) against (1050253722, 1060320051, -1083808154), and the layout test
  failed too. Restored, all pass.
- **Not checked:** nothing visible (no frames). `define_mesh`'s `drop_targets` is T-102's.
