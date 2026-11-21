---
description: >-
  scenario-patina runs well inside run-scenario's 300 s guard again, by smaller textures where evaluation is the cost, otherwise by splitting the library stage into a suite of its own, with no check and no approved frame lost
---
# T-172 — scenario-patina back under its time limit

**Status:** Done

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-170, T-171

**Verify:** scenario-patina, scenario-patina-library-1, scenario-patina-library-2, patina-unit

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

### As built

Run on Sonnet 5.5 (claude-sonnet-5-5), which measured, split and committed `df4f877` and `43f2660` and was stopped by a
usage limit; continued on Opus 5.5 (claude-opus-5-5), since Sonnet was unavailable until the limit resets. The second
agent checked the split (every sample in one half, the planted faults in half 2 only, the bricks-variant and metal
breaks each within one half), corrected `PatinaStages.For`'s summary (it said an unknown selection runs everything;
it is refused), found no stale T-172 job to cancel (the first agent's last verify was still running on the current
head and was left to finish), verified and finished.

**Measured** (Xellotath, Debug build, one run of the whole suite as it stood):

- Alone: 220 s. Beside patina-regression started at the same moment: 214 s (the run is not what the regression
  slows; the 303 s of the failed job came with other queue load as well).
- By stage (from the run folder's file times): graph, export, swatch and nested graph 58 s together; library 160 s.
- Tool time over the whole run, from the server's own log: `swatch` 30.0 s (67 calls), `export` 12.8 s (24),
  `check` 6.9 s (10), everything else under 1 s: 50 s in all.
- Library stage: tool time 29 s (`swatch` 19.0, `check` 5.3, `export` 4.9), the rest, about 130 s, is the scenario's
  own code (the sheet's per-pixel cell checks, the sphere's box-filtered tone predictions, the break replays), about
  15 s per sample in a Debug build; the break replays alone take about 2 s.

**Choice by the person's rule.** Evaluating at texture size (export and check) costs about 20 s of 220, so smaller
textures would save little and every size that matters is 256 and compared byte for byte; the time is the stage's own
checking. So: split. Nothing is loosened: no size, tolerance, frame, check or the 300 s guard changed.

- `LibraryStage(part)`: part 0 is the old whole stage; part 1 runs cobblestones, bricks, bricks-grey and fabric;
  part 2 runs painted-metal, floor-tiles, old-wall and grime with the planted faults. The break replays run for
  whichever samples the part holds (the bricks-variant break needs bricks and bricks-grey, the metal break needs
  painted-metal, both in their own parts as built, so every break is still made once).
- `PatinaStages.For(selection)` and the configuration key `Patina:Stages` (`-CarveArguments:--Patina:Stages=base|library-1|library-2` on `run-scenario.ps1`: the colon form, since `pwsh -File` takes `-CarveArguments --Patina:...` for a missing argument;
  unset runs everything, as before). `PatinaScenario` reads it from `IConfiguration`.
- `tests.json`: `scenario-patina` (`base`: every stage but the library's, est 90 s), `scenario-patina-library-1` and
  `scenario-patina-library-2` (est 120 s each). No new project, so `Carve.slnx` is unchanged. The member's `**Verify:**`
  holds all three.
- Expected per suite alone: base about 60 s, each library half about 80 s; a busy runner at 1.5 times is 120 s, inside
  the two thirds (200 s) target.

**Measured after the split** (Xellotath, alone, Debug): base 52 s, library-1 80 s, library-2 82 s, all three passing.
In the second agent's verify on `8c2183a`, the three queued together on Xellotath beside core-unit: base 124 s,
library-1 152 s, library-2 148 s, all passing and inside the 200 s target.

**Frames.** Every one of the 128 files of the three runs together equals by SHA-256 the pre-split whole run on the same
tree, except `library-breaks.txt` and `library-checks.tsv`, which are written per half; their lines taken together are
equal to the whole run's. Against `J-20261120-085227-39ade3` (T-140's job, the last good one by job id that is
fetchable; none of the M-28 jobs holding the library frames was kept, so the library frames are compared with the
pre-split run): 61 of its 63 files equal; `frames/swatch-sheet-grid-tiling-3.png` and `swatch-tiling-checks.tsv`
differ, and equally so in the pre-split run, so they come from work landed after that job (the cube rows of T-157
are in the tsv); not from this member. Not looked at by eye; no frame image was opened.
