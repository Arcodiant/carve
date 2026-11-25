---
description: >-
  The server-regression tests that bake both weathered materials at 1024 through the real process wait longer than 30 s for that call, so they pass on a loaded machine
---
# B-11 — The 1024 bake tests time out under load

**Status:** In Progress

**Severity:** Major

**Milestone:** M-29

**Verify:** server-regression

## Symptom

`server-regression` fails now and then on a loaded machine with `System.TimeoutException : No response to 11 after
00:00:30` in a test of `GraphMaterialRenderTests` that bakes both weathered materials at 1024 through the real `carve`:

- `Weathered_graphs_bake_on_the_barrel_at_1024`: M-29's own verify, 2026-11-25, job J-20261125-180044-cf8761 (250 of
  251 passed).
- `The_barrel_export_bakes_and_says_how_long_it_took(size: 1024)`: B-10's second run, J-20261125-035955-989a9d, with
  four other projects' jobs on the machine; it passed on the next run.

## Cause

Both materials take 6 to 7 s each to bake at 1024 in Debug on a quiet machine (T-163) and about 14 s each when other
jobs run (T-163's second agent measured 14.6 s and 13.7 s), and `CarveProcess` waits 30 s for an answer to a call.
Neither test asserts a time.

## Fix

**Decided by the person (2026-11-25):** the tests that bake both materials at 1024 through the real process give that
call a longer response limit (120 s), and everything they check stays. Declined: baking at 512 in those tests (it
loses the check that render bakes all six slots at 1024), and re-running until it passes.

- Only the calls that bake at 1024 get the longer limit, by an argument on the call; `CarveProcess`'s default of 30 s
  for every other call and test is unchanged.
- Find every test in `tests/Carve.Server.Tests` that bakes at 1024 through the process (these two at least) and give
  each the same limit; name them in `### As built`.
- No assertion, size or tolerance changes.

## Verified by

- `server-regression` passing, and a statement of what else was running on the machine.
- The limit shown to be what the call uses (a unit test of the argument, or the timeout message naming it).
