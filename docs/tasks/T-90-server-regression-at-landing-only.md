---
description: >-
  A planning rule leaves server-regression out of Verify lines, open items follow it, and the suite estimates are measured again
---
# T-90 — Server regression at landing only

**Status:** In Progress

**Pri:** P2

**Milestone:** M-20

**Batch:** — (standalone)

**Depends on:** T-88, T-89

**Verify:** server-unit

## Goal

`server-regression` runs before work lands on `main`, as cairn's tiers intend, rather than at every task verify.

## Scope

- Add a rule to CLAUDE.md's *Build, test and verify*: a task or bug's `**Verify:**` names `server-regression`
  only when it changes something the suite alone covers, which is the stdio transport, extension loading, the
  debug extension or rendering. Tool behaviour is covered by `server-unit`, and the milestone's landing on `main`
  runs `server-regression` anyway.
- Bring the open items not yet started into line: drop `server-regression` from their `**Verify:**` where the rule
  says so, and leave items already in progress to their owners. Use `cairn list`, not grep, to find them.
- Measure both server suites again through `cairn test run` and set `est-seconds` in `tests.json` from the
  results.
- Record the before and after in the M-20 retrospective: `cairn test report` slot-seconds per week, and the
  per-run time of each server suite.

## Notes
