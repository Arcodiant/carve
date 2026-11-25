---
description: >-
  RV-82: the acceptance and scenario findings
---
# T-221 — RV-82: the acceptance and scenario findings

**Status:** Done

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

### As built

Claude Sonnet 5.5 (`claude-sonnet-5-5`).

- **The crevice set.** `WeatheringAcceptanceTests.DriveSides` picks the drive box's faces whose normal is perpendicular to its z axis (100 to 103); the ends 104 and 105 are what is left, and the end edges are theirs. `JunctionTexels` and `CreviceTexels` (internal) carry the face of each texel; the checked set is the side faces' texels within `CreviceReach` 0.004 m, `open - band >= 10`, unmoved. No face number below a bound remains. The `numbers.tsv` junction rows now name their set (`sides`, `drive (sides and ends)`, `drive+socket`) and count side, end and socket texels. Test: `The_crevice_set_holds_no_texel_of_a_face_that_is_not_a_drive_side` (on `samples/wrench.carve`: sides are exactly 100 to 103 and the set holds those faces only). It is a plain fact in a `regression`-tier project that no unit suite runs, so it ran inside the round checks and the negatives (7 passed per round); I did not run it against the old code.
- **Numbers** (copies of both rounds' folders from the `G:` copy into `artifacts\T-221\round-N\`, checked with `run-acceptance-check.ps1 -Weathering`; the regenerated `acceptance\` files copied back to the `G:` copies). Open steel mean minus the band's mean: round 1 94.050 - 53.753 = 40.297 (3,939 band texels, 162,126 open); round 2 93.315 - 78.109 = 15.206 (980, 23,646). Both pass, hard-edge unchanged (93.778, 61.501). Negatives on round 2's plant (`run-weathering-negatives.ps1`): reference 135.098 - 67.568 = 67.530 (passes); no-wear 116.400 - 116.323 = 0.077; no-dirt 181.893 - 225.391 = -43.499 (caught by the crevice check; hard-edge still passes at 42.814); grip-wear, unresolved, file-tool and next-define-missing caught by the same tests as before. Distance, margin and set were not moved.
- **Records.** T-168's ruling section and Rulings bullet no longer say faces 99 and 118 are not in the set (they say it was meant, the code counted 99); a new paragraph records the ruling of 2026-11-25, what was declined and what the person was told, and the figures above; the round 2 bake row now marks 1073 as 512. `CLAUDE.md`'s phrase for the crevice set now says the four side faces.
- **Stage 3.** `RebakeStage` now calls `Baked(run, "after the graph's restore", restored, false)`. The weathered scenario passes (2 load checks) and with `CARVE_WEATHERED_BREAK=no-rebake` it fails with 1 check (`after wear_amount 0: expected nothing baked`). The frames of that run are byte-identical (all 58 hashes) to a run of the same worktree without the change; not a run of `origin/milestone/M-29` itself.
- **Not checked.** The frames and images (none opened); that a restore which bakes would fail the new check (no break of my own made); T-168's table figures for the set first ruled were left as history; round 1's bake times beyond what T-168 lists.
- **README.md:** nothing needed.

