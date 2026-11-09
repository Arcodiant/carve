---
description: >-
  An agent defines a skeleton of named bones with parents and rest transforms, reads it back, and is refused over 255 bones
---
# T-31 — Skeleton

**Status:** Done

**Pri:** P1

**Milestone:** M-16

**Batch:** — (standalone)

**Depends on:** T-29

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent defines a skeleton exactly as its game needs it and reads it back unchanged.

## Scope

- **Core:** `Skeleton`, as D-6's *Bones as given* says. Refuses: more than 255 bones, a duplicate or empty name, a parent after its child, an unknown parent, a non-unit rotation. All errors are listed at once.
- **Tools:** `define_skeleton(bones)`, each bone `{name, parent, position, rotation}`, with `rotation` as a quaternion `[x, y, z, w]`; `get_skeleton`, which returns the same list plus each bone's rest world position. Defining a skeleton is one undo step.
- **Description:** says rest transforms are local to the parent, with a worked three-bone example.
- **Tests:** `unit` for every refusal and for 255 accepted, 256 refused; order and names round-trip, including a name such as `mixamorig:Hips`.

## Notes

- Changing a skeleton that already has weights or clips: a tool error unless the bone names and order are the same. Stop and ask if an agent's flow needs more.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### As built

One agent, on Sonnet 5.5.

- **Core:** `Skeleton` (`src/Carve.Core/Skeleton.cs`) built by `Skeleton.Create(BoneSpec[])`, which returns the skeleton or every problem as `bones[i]: ...`: none or over 255 bones, empty or duplicate name, unknown parent, parent at or after the child, non-finite position or rotation, rotation not unit. `Model.Skeleton` (null until defined) is kept by `With`; `WithSkeleton` replaces it.
- **Store:** `ModelStore.DefineSkeleton` is one undo step. It is the seam for T-32/T-33: the weights-and-clips rule (names and order unchanged, via `Skeleton.SameNamesAs`) goes there under the lock. No stub tool.
- **Tools:** `define_skeleton(bones)` and `get_skeleton` (`SkeletonTools.cs`), with a three-bone worked example (head rests at y = 1.7). `get_skeleton` adds `restWorldPosition` to each bone.
- **Ruled (2026-11-08, the person):** D-6 gave no tolerance for a non-unit rotation; `Skeleton.UnitTolerance` = 1e-3 on the quaternion length stays as built.
- **Readings the spec left open:** Also: `position` and `rotation` are required per bone; `parent` may be null or left out. A skeleton is not saved by `save`/`open` (not in scope). Defining a skeleton in an empty model has no earlier state, so `undo` straight after it says nothing to undo, as for a first mesh.
- **Checked:** all 25 frames of `scenario-cube` and `scenario-crate` are byte-identical to T-29 `hashes-after.txt` (list in `artifacts/review/T-31/hashes-after.txt`); `core-unit` passes; 7 product breaks each fail the Core tests (`artifacts/review/T-31/break-table.txt` in M-16). No frame added; no render or export code touched.
