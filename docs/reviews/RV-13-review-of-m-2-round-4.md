---
description: >-
  Review of M-2, round 4
---
# RV-13 — Review of M-2, round 4

**Status:** Answered

**Reviews:** M-2

**Round:** 4

**Previous:** RV-12

**Diff:** 34534a54378a07e4ea154a805bfe69dc6b7f274e against origin/main

**Reviewer:** claude-opus-5-5 (one reviewer, checking the RV-12 fix)

## Findings: the RV-12 fix, c637d21 (claude-opus-5-5)

The fix holds. `ToSourceFace` widens corner k and corner (k+1) % n to double, takes the length in double and compares it with `Math.Abs((double)value)`. Only those lines and one two-row theory changed; no other text, tolerance, rule or test row. `core-unit`: 129 passed, 0 failed.

- **Old code:** both new rows fail on it (checked in a temp copy with the parent's `MeshEdits.cs`).
- **Zero-length edge:** `Create` refuses one, so no wall is ever measured over it.
- **Extreme scales:** a 1e-30 edge, spans of ±3e38 and denormal squares all behave.
- **Ring vertices:** these still say "large".
- **Wall index:** it always names the edge measured, and nothing can throw.

No new finding.

## Summary

A re-review round of one Opus reviewer, checking only c637d21. It raised no finding, so the review of M-2 closes here: RV-10 (23 findings), RV-11 (1), RV-12 (1) and RV-13 (none), each answered.
