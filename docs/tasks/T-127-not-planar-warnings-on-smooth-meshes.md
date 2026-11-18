---
description: >-
  Decide whether a smooth-shaded mesh's faces warn as not planar, since a subdivided curved surface is almost never flat and export triangulates
---
# T-127 — Not-planar warnings on smooth meshes

**Status:** Open

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

A smooth, subdivided mesh exports with no warning that is only the expected result of subdivision, so a real
warning is not lost among them.

## Scope

- Rule on what `check`, `get_mesh`, `export`, `open` and `move` say about a not-planar face on a smooth-shaded mesh:
  no warning, one summary line, or a looser limit. Put the options to the person before building.
- Then change `MeshReport` and the fold (`MeshWarningFold`, T-72) to match, and the checks that pin today's
  behaviour: `scenario-soft` / `tests/Carve.SoftLoad.Tests` (the soft gardener) and `scenario-morph` /
  `tests/Carve.MorphLoad.Tests` (the arm, T-106).
- `select faces where non_planar` keeps naming every face either way.

## Notes

- Raised in M-21's T-106 (2026-11-18). Exporting the arm (`samples/arm.carve`, subdivided once, smooth) reports 49
  not-planar faces, 0.5 to 1.5 mm off their planes, folded by T-72 into 10 named and one "and 39 more" line. The
  engine's loader gives no warning. The person ruled to pin the folded output and its count to
  `select faces where non_planar` in T-106, and to file the rule as this task, outside M-21.
- Same flood as M-18's soft gardener (T-44, RV-27) and M-19's T-72.
- **Open rules: stop and ask.** The rule itself is the person's.
