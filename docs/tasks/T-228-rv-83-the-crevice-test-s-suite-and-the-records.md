---
description: >-
  RV-83: the crevice test's suite and the records
---
# T-228 — RV-83: the crevice test's suite and the records

**Status:** In Progress

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** server-unit

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
