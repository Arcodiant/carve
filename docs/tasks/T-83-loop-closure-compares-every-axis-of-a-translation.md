---
description: >-
  Loop closure compares every axis of a translation
---
# T-83 — Loop closure compares every axis of a translation

**Status:** Done

**Pri:** P1

**Milestone:** M-19

**Batch:** — (standalone)

**Depends on:** T-45

**Verify:** server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

A looping clip whose translation does not close on any axis is warned about, as D-6's *Clips* says.

## Scope

- T-45 compares a looping translation channel's start and end on X and Z only, so a Y bob that does not close (hips ending 5 cm lower) is not warned. The person ruled (2026-11-12) that loop closure compares X, Y and Z, within T-45's 1 mm. Root travel stays X/Z only, on the ground plane.
- **Tests, `unit`:** a looping clip whose translation fails to close on Y alone warns; one within 1 mm on every axis does not; a root that moves on Y but returns in X/Z is not warned as travelling. Show the Y check failing, as a table.
- Update D-6's *Clips* wording and T-45's constants' comments to match.

## Notes

- The person's ruling, 2026-11-12: "Yes, all axes", on the question whether loop closure compares all three axes, root travel staying X/Z.
- Run `finish` from inside the worktree and leave the folder.

### As built

Run on Sonnet 5.5 (claude-sonnet-5-5), one agent.

- **Loop closure** (`ClipChecks.Check`): a looping translation channel closes when its first and last key are within 1 mm (T-45's constant) by 3D distance, so X, Y and Z all count; the warning says "on X/Y/Z". Root travel is unchanged: X/Z only, looping or not. The spec left open per-axis versus 3D distance; I used the 3D distance (within 1 mm on each axis follows from it, and it is T-45's measure extended).
- **Wording**: D-6 *Clips* closing-tolerance paragraph and the constants' comments in `ClipChecks.cs` updated.
- **Tests** (`ClipChecksTests`, unit): Y-only gap warns; within 1 mm on every axis is clean; a looping root that moves on Y but closes on X/Z gets the loop-free result with no travel warning. T-45's `A_clean_looping_clip_has_no_warning` had a Y gap of 0.2 m that it relied on not being compared; its end key now closes.
- **Check shown failing**, tests run against T-45's `ClipChecks`:

| Test | Old logic | New logic |
|---|---|---|
| `A_looping_translation_that_fails_to_close_on_Y_alone_warns` | fails | passes |
| `A_looping_root_that_moves_on_Y_but_returns_on_XZ_is_not_travelling` | fails (no warning, `Assert.Single`) | passes |
| `A_looping_translation_within_a_millimetre_on_every_axis_is_closed` | passes | passes (guards over-warning) |

- **Not checked**: no frame or sound; no render code touched. Engine needs: none.
