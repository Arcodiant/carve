---
description: >-
  An agent defines a skeleton of named bones with parents and rest transforms, reads it back, and is refused over 255 bones
---
# T-31 — Skeleton

**Status:** Open

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
