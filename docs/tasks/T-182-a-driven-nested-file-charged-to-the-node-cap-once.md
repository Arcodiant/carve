---
description: >-
  A nested file that a parameter drives is charged to the 4,096-node cap once per validation, and a refusal names the right cause
---
# T-182 — A driven nested file charged to the node cap once

**Status:** Open

**Pri:** P3

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** patina-unit

## Goal

A driven nested file costs the node cap what an undriven one does, so the cap refuses a graph only for what it really
takes in.

## Scope

- The read for a drive's range and the expansion share one charge per file per validation (T-179's cache by chain of
  files is the place).
- The fault, when there is one, names what went past the cap.
- A test: a nested file of a little over half the cap loads with and without a drive.

## Notes

- **Filed (2026-11-22, by the person):** RV-78's Nit (M-28 round 3), after T-179's fix for RV-77's drive fan-out. A
  2,103-node nested file loads undriven and is refused when one parameter drives it. Real graphs use 9 to 33 nodes, so
  it costs nothing today. The person chose to file it and close rather than fix it in M-28.
