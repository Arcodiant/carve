---
description: >-
  RV-83: the crevice test's suite and the records
---
# T-228 — RV-83: the crevice test's suite and the records

**Status:** Done

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** weathering-unit

## Goal

The test that holds the acceptance crevice set runs in a suite, and the records and docs the review found wrong say
what is true.

## Scope

Each finding of RV-83 named here, by its heading there:

- "The test that the crevice set holds only drive side faces runs in no suite, and CLAUDE.md says the project's tests skip without a round"
- "T-168's table and comparison still call the old figures "the ruled set", and the new paragraph misnames them and says the table is below"
- "CLAUDE.md still lists the grip check as part of the steel's base colour bake"
- "README's list of regression suites leaves out the new graphs-regression"
- "T-220's As built names a commit that is not on the branch"

- **The crevice set's test** (`The_crevice_set_holds_no_texel_of_a_face_that_is_not_a_drive_side`) needs no round
  folder, only `samples/wrench.carve`, yet sits where no suite runs it. Put it where `verify` runs it: a `unit`
  class in a project a unit suite runs (if it is fast enough for the tier; say how long it takes), reaching the set's
  code without copying it, with `samples/wrench.carve` and every file it compiles among that suite's inputs in
  `tests.json`. `CLAUDE.md`'s row for the project says which of its tests need a round and which do not.
- **The records:** T-168's table and comparison line name the figures by the set they were measured on (the set
  that counted face 99, the set first ruled, and the side faces ruled on 2026-11-25), and the paragraph points the
  right way; `CLAUDE.md` puts the grip check where the ruling put it (what the grip's faces wear, not the steel's
  base colour bake): this clause has now been found wrong in two rounds, so read the whole row against the code
  before writing it; the README lists `graphs-regression`; T-220's `### As built` names the commit that is on the
  branch.
- Two further findings of RV-83 (the milestone file's missing rulings, and the retrospective's lines the fixes made
  untrue) are the orchestrator's and are not this member's.
- **Owns:** `tests/Carve.WeatheringAcceptance.Tests` and whichever test project the set's test moves to,
  `tests.json` for that, `CLAUDE.md`, `README.md` outside patina's section, T-168's and T-220's records.

## Notes

- **A fix member of RV-83,** round 2 of the review of M-29 (`docs/reviews/RV-83-review-of-m-29-checking-rv-82-s-answers.md`). Each finding listed in the Scope is read in
  full there: where it is, what is wrong, and how the reviewer showed it. RV-82 is round 1; the member whose fix a
  finding is about says what it did in its `### As built`.
- Every finding gets a fix with a test that fails without it, shown failing (in a scratch copy with the fix's hunk
  reverted), or, where the finding is about a record, the record made true. A finding that turns out not to hold is
  answered `no change needed: <reason>` with the evidence.
- The member sets the `**Outcome:**` line of each of its findings in RV-83 to a bare `fixed` (`cairn lint` accepts
  nothing after it) or `no change needed: <reason>`, and touches no other finding's line. Commits and test names go
  in this member's `### As built`.

### As built

Run on Sonnet 5.5 (`claude-sonnet-5-5`). All in this member's one code commit (`git log` on `task/T-228`: "the crevice test runs in weathering-unit, and the records say what is true").

- **The crevice test's suite.** `The_crevice_set_holds_no_texel_of_a_face_that_is_not_a_drive_side` moved to a new class `CreviceSetTests` (`tests/Carve.WeatheringAcceptance.Tests/CreviceSetTests.cs`, `Tier` `unit`), in the same project so it reaches `DriveSides` and `CreviceTexels` (internal) without a copy. It takes about 0.4 s (375 ms in the scratch run), so `unit` is true of it. New suite `weathering-unit` in `tests.json` (`--filter Tier!=regression`; inputs: the project's `dotnet-closure`, the linked `tests/Carve.CharacterLoad.Tests/RunContent.cs`, `samples/wrench.carve`); this member's `**Verify:**` names it. Shown failing: in a scratch copy under `%TEMP%\t228-scratch`, `DriveSides`'s `< 0.5f` made `< 1.5f` (every drive face counts as a side): the test fails ("Collections differ", 398 ms). The worktree was never broken. `verify` passed `weathering-unit` (24 s).
- **T-168's records.** The table row and the comparison line name 49.143 / 24.862 as the set that counted face 99, and the comparison also gives 48.1 / 24.6 (set first ruled) and 40.297 / 15.206 (side faces, 2026-11-25); the paragraph says "above" and names the three sets. I did not re-read the Negatives table's figures against a set (the paragraph says only that they were not re-read).
- **CLAUDE.md.** The acceptance project's row is rewritten from the code: six round checks (session model and no file tool; missing-graph message and next steel `define_material`; saved file v10 with a mesh-reading steel graph; hard-edge and drive-crevice bake of the steel slot; grip held to the starter's grip as the saved file draws it, not a part of the steel bake; glb loads) and `CreviceSetTests` (no round, `unit`, run by `weathering-unit`).
- **README.md.** Unit list adds `weathering-unit`, regression list adds `graphs-regression`. Checked against `tests.json`'s ids: the regression no-GPU line now has both; I did not re-audit the Vulkan list beyond that.
- **T-220's As built** now names `45f7b023`, the commit on `milestone/M-29` (`git log`).
- The five findings' `**Outcome:**` lines in RV-83 are `fixed`.
- Not checked: that the test's name appears in the suite's log (verify reports the suite as passed; I saw the test by name only in the scratch run).
