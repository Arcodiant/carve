---
description: >-
  loop_cut does not refuse a ring that closes on itself reversed (a Mobius band of quads); found by T-66
---
# B-3 — loop_cut does not refuse a ring that closes on itself reversed (a Mobius band of quads)

**Status:** In Progress

**Severity:** Minor

**Milestone:** M-5

## Symptom

`loop_cut`'s ring walk (`LoopCut` in Carve.Core, T-66) closes when it reaches a quad it has already visited. If the ring of quads returns to its start the other way round (a Mobius band), the last quad would put a second point on the shared edge at the mirrored fraction, instead of joining the first. T-66 could not build a test mesh for it (every quad must be planar and simple), so no refusal was written and the case is untested. Recorded in T-66's As built.

## Cause

The walk checks that it has come back to a visited quad, but not that it came back across the start edge in the same direction.

## Fix

The person ruled on 2026-11-12 ("Refuse it, fix later"): detect a ring that returns reversed and refuse it with its own error, in the knife's facts-only style, naming the start edge. Build the test mesh by hand: a band of planar quads with a half twist, made from enough quads that each stays planar. Add a break that shows the test failing. To be done in M-5's fix round, before the milestone's `finish`.

## Verified by
