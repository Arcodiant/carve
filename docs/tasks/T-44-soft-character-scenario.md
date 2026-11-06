---
description: >-
  The test character is subdivided and smoothed through the tools and shown soft and with hard creases, in its own suite
---
# T-44 — Soft character scenario

**Status:** Open

**Pri:** P1

**Milestone:** M-18

**Batch:** — (standalone)

**Depends on:** T-43

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

The gardener looks the way the game wants: soft and painterly, with a few deliberate creases.

## Scope

- **`scenarios/Carve.Scenario.Soft`**, suite `scenario-soft`: the gardener subdivided and smoothed through the tools, with the hat's brim creased.
- Frames in rest and mid-clip, flat beside smooth, with checks that shading is continuous across a soft edge and breaks across a hard one.
- Exported, loaded and compared by triangle count and normals.
- `samples/gardener.carve` is updated if the soft version replaces it. Say which was kept.

## Notes

- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.
