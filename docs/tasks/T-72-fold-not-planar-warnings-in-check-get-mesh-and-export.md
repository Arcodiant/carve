---
description: >-
  Fold not-planar warnings in check, get_mesh and export
---
# T-72 — Fold not-planar warnings in check, get_mesh and export

**Status:** Open

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** T-43

## Goal

A subdivided mesh's report stays readable: Catmull-Clark faces are almost never planar, and one warning per face buries every other warning.

## Scope

- `check`, `get_mesh` and `export` fold not-planar warnings past some count into one line with the count, as `subdivide` already does (T-43). `select faces where non_planar` still names every face.
- The soft gardener's export (T-44: 504 not-planar warnings) then carries one folded line, and `scenario-soft` and `tests/Carve.SoftLoad.Tests` assert that one folded line and no other warning. Today, by the person's ruling in RV-27, they assert that every warning is a not-planar face, with the count equal to `select faces where non_planar`.

## Notes

- From the person's ruling on T-43 (2026-11-10): the fold went into `subdivide` only, and a task of its own if T-44 showed the flood getting in the way in `check` and export. T-44 did: its scenario asserts that every export warning is a not-planar face instead of none.
- The count past which to fold is not settled: ask.
