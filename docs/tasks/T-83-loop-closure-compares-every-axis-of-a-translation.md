---
description: >-
  Loop closure compares every axis of a translation
---
# T-83 — Loop closure compares every axis of a translation

**Status:** In Progress

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
