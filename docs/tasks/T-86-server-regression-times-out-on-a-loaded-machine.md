---
description: >-
  server-regression times out on a loaded machine
---
# T-86 — server-regression times out on a loaded machine

**Status:** Open

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

A loaded machine does not fail `server-regression`. During M-19 (2026-11-12) one run on task/T-47 took 44 m 49 s instead of about 5 m, with four timeouts in `LoaderTests`, `RenderTests` and `StdioTests`; the same commit passed in 338 s on a rerun (job J-20261112-180141-80dffc). `cairn test report --since 3d` counts it as the suite's one flake. M-16 saw a 2 h 16 m hang in `RenderTests` and M-17 a `LoaderTests` timeout under load.

## Scope

To plan: find what the timed-out tests wait on under load (a fixed timeout too tight for a shared GPU or a busy queue, or a real stall), and fix the cause. A timeout is not raised without the person's ruling.

## Notes
