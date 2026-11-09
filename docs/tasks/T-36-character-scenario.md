---
description: >-
  A scenario builds the small test character through the real tools, poses it, saves, reopens and exports it, in its own suite
---
# T-36 — Character scenario

**Status:** Open

**Pri:** P1

**Milestone:** M-16

**Batch:** — (standalone)

**Depends on:** T-34, T-35, T-46, T-52

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

The thin slice is proved end to end through the real tools, on a character someone could recognise as one.

## Scope

- **The test character** (`samples/gardener.carve`, built as code in the scenario too, and kept equal by a test): an original small round gardener. Two meshes, `Body` and `Hat`, four or more colours, bones named by the engine's Synty preset as Orcward's are (about 22), deliberately asymmetric (a watering can in one hand) so a mirrored frame fails. Clips `Idle` and `Walk`, both looping and in place.
- **`scenarios/Carve.Scenario.Character`**, suite `scenario-character`: build it through the tools; capture rest, a pose, and each clip at three times; `save`, reopen, compare field for field; `export`; export again without `overwrite` (a tool error) and with it (identical bytes); export in `clips` mode; export with a vertex unweighted (a tool error, then `allow_unweighted`).
- **Load check:** a `regression` test reads the exported file through `IModelGeometryLoader.Load` from a content folder and asserts bone count, bone order and clip names, that `HumanoidRoleMap.Synty` resolves on it with no sidecar, and that the T-46 clips file loads beside it.
- **Look at the frames** and say what was seen.

## Notes

- The gardener is the sample for M-17, M-18 and M-19 too: T-41, T-44 and T-48 build on it.
- Sonnet is enough once T-34 and T-35 have landed.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.
