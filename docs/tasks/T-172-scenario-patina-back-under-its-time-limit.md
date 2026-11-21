---
description: >-
  scenario-patina runs well inside run-scenario's 300 s guard again, by smaller textures where evaluation is the cost, otherwise by splitting the library stage into a suite of its own, with no check and no approved frame lost
---
# T-172 — scenario-patina back under its time limit

**Status:** In Progress

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-170, T-171

**Verify:** scenario-patina, patina-unit

## Goal

Every scenario suite of patina finishes well inside `run-scenario.ps1`'s 300 s guard, including on a runner that is
busy with another suite, and every check and every approved frame it holds today is still held.

## Scope

- **Measure first.** Time each stage of scenario-patina on one runner, alone and beside patina-regression. Within the
  library stage, split the time into evaluation (export, check), drawing (swatch sheets and spheres) and the
  scenario's own predictions. Write the figures here.
- **Then the person's rule (2026-11-20):** if evaluating at texture size is what costs the time, use smaller textures;
  otherwise split the test.
  - **Smaller textures** only where nothing approved or committed changes: the committed `samples/graphs/*/*.png` and
    `swatch.png` sheets stay 256 and are still compared byte for byte, and every approved frame stays as it is.
    A scenario-only check (a variant, a planted fault, an edited graph) may run at a smaller size if it still catches
    its break.
  - **Split** otherwise: the library stage (or the stage that dominates) becomes a scenario suite of its own in
    `tests.json`, joined to `Carve.slnx` as CLAUDE.md says, each suite with an honest `est-seconds`. Its frames and
    checks are unchanged, and its breaks are still caught.
  - Both, if that is what the figures call for.
- **Target:** each suite's run on a runner busy with patina-regression at no more than about two thirds of 300 s.
  The 300 s guard itself does not change.
- Frames byte-identical by hash against the milestone's last good scenario-patina job, fetched by job id.

## Notes

- **Decided (2026-11-20, by the person):** M-28's verify failed scenario-patina, not on a check but on
  run-scenario's 300 s guard ("patina still running after 300 s; stopped"), after T-142's library stage brought the run
  to 182 to 303 s and the run shared Xellotath with patina-regression. Asked to raise the guard, split the stage or
  re-run, the person asked whether smaller textures would do, and otherwise to split the test.
- **Stop and ask:** a smaller size that would change a committed PNG, a committed sheet or an approved frame; a check
  that would be dropped; a raise of the 300 s guard.
- Sonnet: concrete once measured.
