---
description: >-
  NamedMeshTests stops at the first stale refusal and can skip the labelled lid frame
---
# B-9 — NamedMeshTests stops at the first stale refusal and can skip the labelled lid frame

**Status:** Open

**Severity:** Minor

**Milestone:** — (off-milestone)

## Symptom

In `tests/Carve.Server.Tests/NamedMeshTests` (`server-regression`), the labelled lid frame is drawn on some runs and not on others. T-94 (M-9) found it on 2026-11-19 while checking that every existing frame was byte-identical. Two queued runs (J-20261119-045216-ff23c5, J-20261119-050027-c7c067) drew the plain lid 10 times but never the labelled lid, while three local runs drew it. Because the test still passes without it, a frame guard over the suite can't rely on that frame being there. The person ruled that T-94's local evidence is enough and that the race is filed here.

## Cause

The test's loop of labelled tries stops at its first refusal, and the first try can be refused as stale when the render has not caught up. Whether the frame is drawn then depends on timing.

## Fix

## Verified by
