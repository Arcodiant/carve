---
description: >-
  loop_cut does not refuse a ring that closes on itself reversed (a Mobius band of quads); found by T-66
---
# B-3 — loop_cut does not refuse a ring that closes on itself reversed (a Mobius band of quads)

**Status:** Fixed

**Severity:** Minor

**Milestone:** M-5

## Symptom

`loop_cut`'s ring walk (`LoopCut` in Carve.Core, T-66) closes when it reaches a quad it has already visited. If the ring of quads returns to its start the other way round (a Mobius band), the last quad would put a second point on the shared edge at the mirrored fraction, instead of joining the first. T-66 could not build a test mesh for it (every quad must be planar and simple), so no refusal was written and the case is untested. Recorded in T-66's As built.

## Cause

The walk checks that it has come back to a visited quad, but not that it came back across the start edge in the same direction.

## Fix

The person ruled on 2026-11-12 ("Refuse it, fix later"): detect a ring that returns reversed and refuse it with its own error, in the knife's facts-only style, naming the start edge. Build the test mesh by hand: a band of planar quads with a half twist, made from enough quads that each stays planar. Add a break that shows the test failing. To be done in M-5's fix round, before the milestone's `finish`.

As done: `LoopCut`'s walk now checks, when it returns to a visited quad, that it came back across the start edge the same way round (`cur == (a, b)`). A ring that returns with the edge's ends swapped is refused with `edge`: "the ring from edge [a, b] comes back to it with its ends swapped: loop_cut does not cut a ring that closes on itself reversed". `loop_cut`'s tool description lists the refusal among its bad inputs. Test mesh: eight planar quads round a unit circle, rung k turned k*180/8 degrees about the tangent, half-width 3.5e-4 (the warp is about a quarter of the half-width, so each quad stays within `PlanarityTolerance`; a wider band is refused by `Create` as non-planar). The ordinary closed ring (the cube) still cuts unchanged.

## Verified by

Run on Sonnet (claude-sonnet-5-5). `LoopCutTests.ARingThatClosesOnItselfReversedIsRefusedNamingTheStartEdge` (asserts the refusal text from edges 0-1, 1-0 and 6-7) and `AnOrdinaryClosedRingStillCutsUnchanged`; the break (check removed) fails the first test, the band being cut instead of refused, logged in `artifacts/review/B-3/breaks.log` on M-5. Core-only change plus one description sentence: no frame touched.

Suites run by `cairn bug B-3 verify` on the rebased branch (on origin/milestone/M-5 34eae44): tests:core-unit and tests:server-unit, both PASS, none carried. No scenario suite is an input of the changed files, so none ran and no frame was produced or compared; the change touches no frame path. No test or doc pinned the `loop_cut` description text.
