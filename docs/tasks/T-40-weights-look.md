---
description: >-
  render shows one bone's weights as a heat look on the model, so a stray or missing weight can be seen
---
# T-40 — Weights look

**Status:** In Progress

**Pri:** P1

**Milestone:** M-17

**Batch:** — (standalone)

**Depends on:** T-39

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent sees what one bone moves, on the model itself.

## Scope

- **`render` argument `weights`:** a bone name. Each face corner is shaded from the bone's weight, dark blue at 0 through green to red at 1, unlit; a vertex with no weight at all is magenta.
- **Tool description:** says how to read the colours, and that magenta needs fixing.
- **Tests:** frames with known weights and pixel checks at 0, 0.5 and 1; magenta on a planted unweighted vertex; an unknown bone is a tool error listing the names.

## Notes

- Per-vertex colour on a plain mesh is in the engine's materials guide. The look draws on the static path, not the skinned one.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.
