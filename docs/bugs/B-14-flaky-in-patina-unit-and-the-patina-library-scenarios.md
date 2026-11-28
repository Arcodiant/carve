---
description: >-
  Flaky in patina-unit and the patina library scenarios
---
# B-14 — Flaky in patina-unit and the patina library scenarios

**Status:** Open

**Severity:** Major

**Milestone:** — (off-milestone)

## Symptom

Seen during M-41 (2026-11-27). Each failure passed on the next run, and none touches what M-41 changed.

- `patina-unit`: `ExportToolTests.Other_tools_answer_while_an_export_runs` (`tests/Patina.Server.Tests`, a timing test) failed once in T-238's first `verify` and passed on rerun.
- `cairn test report --since 1d` (2026-11-27) counts in the window: `patina-unit` 1 flaky and 2 failed runs; `scenario-patina-library-1` and `scenario-patina-library-2` 1 flaky and 1 failed run each. The report could not read 203 passes' logs, so a retry into a pass elsewhere is not counted. Which tests failed in the library runs is not known.

## Cause

Not found. Read the failing jobs' logs first. For the export test, check whether its wait assumes an idle machine. For the library scenarios, check whether they share B-13's start-up timeout or engine crash under load.

## Fix

## Verified by
