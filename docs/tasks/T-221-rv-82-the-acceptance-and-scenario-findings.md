---
description: >-
  RV-82: the acceptance and scenario findings
---
# T-221 — RV-82: the acceptance and scenario findings

**Status:** Open

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** server-unit, scenario-weathered

## Goal

The acceptance crevice check measures the set the person ruled and the record says what it measures; the
weathered scenario checks the cache hit it claims; the round record's figures are right.

## Scope

Each finding of RV-82 named here, by its heading there:

- "The acceptance crevice set counts face 99, a dark socket face, as "drive", against the ruling and the record" (the scenarios)
- "Stage 3's restore is said to check for a cache hit; it only logs one" (the scenarios)
- "T-168's round 2 bake times mark one 512 bake as 1024" (the scenarios)

- **The crevice set, as the person ruled on the review's finding (2026-11-25):** the drive's **four visible side
  faces** (100 to 103) within 0.004 m of the edges of its two ends, `open steel mean − that band's mean >= 10` luma.
  The person was told that the code had counted face 99 (a dark socket face) as drive, that on the set first ruled
  the margins are 48.1 and 24.6, that about 41% of that set lies on the drive's two end faces (flush against the
  socket, never seen, dark under any occlusion), and that on the side faces alone round 2 reads 15.2. Declined: the
  set as first ruled with face 99 removed. Select the faces by what they are in `samples/wrench.carve` (the drive's
  sides), not by a face number below a bound. Measure both rounds, the reference and every negative on the new set,
  record the numbers, and correct the two places where the task's file says faces 99 and 118 are not in the set. If
  a round fails, or the no-dirt negative is no longer caught, stop and report: do not move the distance or margin.
- **The cache hit** after the graph is restored (weathered scenario, stage 3) is asserted, as T-167's record and
  CLAUDE.md say it is.
- The round folders' kept copies under the shared artifacts drive get the regenerated `acceptance/` files.
- **Owns:** `tests/Carve.WeatheringAcceptance.Tests`, the Acceptance scenario's weathering step,
  `scripts/run-weathering-negatives.ps1`, `scenarios/Carve.Scenario.Weathered` (stage 3's check only), and the
  records in T-167's and T-168's files.

## Notes

- **A fix member of RV-82,** the review of M-29 (`docs/reviews/RV-82-review-of-m-29.md`). Each finding listed in the
  Scope is read in full there: where it is, what is wrong, and how the reviewer showed it.
- Every finding gets a fix with a test that fails without it, or, where the finding is about a record, the record
  made true. A finding that turns out not to hold is answered `no change needed: <reason>` with the evidence, and one
  that cannot be fixed inside this member's files without a design change is reported, not forced.
- The member fills the `**Outcome:**` line of each of its findings in RV-82 (`fixed`, with the commit and the test's
  name; or `no change needed: <reason>`), and touches no other finding's line.
