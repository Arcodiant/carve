---
description: >-
  Server tool checks run in-process as unit tests, and server-regression runs only before landing on main
---
# M-20 — Fast server tests

**Status:** Active

**One-liner:** *Server tool checks run in-process as unit tests, and server-regression runs only before landing on main*

## What this milestone delivers

`server-regression` is the costliest suite by far. Over the four weeks to 2026-11-14, `cairn test report` put it at
about 17,300 slot-seconds a week, more than every other suite together. It ran 47 times a week and was carried
forward only 18% of the time. A run passes 524 cases in about 7 minutes (job J-20261114-054304), against the
`est-seconds: 260` in `tests.json`.

Two causes:

- **Every case starts its own `carve`**: a `dotnet exec`, host build, `initialize` and `tools/list`, and the engine
  thread creating a Vulkan device. The cases run one at a time (`DisableTestParallelization`). Yet 20 of the 28
  regression classes never call `render`; they pay for a process and a device to check a tool's JSON.
- **Nearly every item names it in `**Verify:**`.** cairn's tiers mean a regression suite to run before landing on
  `main`, but 56 items name `server-regression`, so it runs at every task verify and again after every review
  fix. Items name it because a tool's behaviour has no `unit` coverage: every test that calls a tool goes
  through the process.

This milestone fixes the second by fixing the first. Tool definitions and tool behaviour that need no device are
checked in-process at `unit` tier, inside `server-unit`. What stays in `server-regression` is what needs the
process or the GPU: stdio, extension loading, the debug extension, and rendering. Then items stop naming it, and
it runs once per landing on `main`.

Content: none user-facing. This is test infrastructure, so the planning rule that a milestone adds something
reachable from a plain launch does not fit it. Its result is a faster gate and a cheaper slot budget, measured by
`cairn test report` and per-test timings before and after.

**Order.** T-87 first. Then T-88 and T-89 side by side. T-90 last, once `server-unit` covers the tools.

**Ask, don't land.** T-87 must settle how the in-process harness reaches the tools. The test project now
deliberately builds `carve` as a process and never links it (`ReferenceOutputAssembly="false"`: "the tests see
what ships"). Changing that is the person's call. Moving a check down a tier must not loosen it: the same
assertions, on the same inputs.

## Required tasks

- [ ] [T-87](../tasks/T-87-in-process-tool-harness.md) — Call the real MCP tools in the test process with no child process and no engine, decided with the person
- [ ] [T-88](../tasks/T-88-tool-definition-checks-as-unit-tests.md) — Description, worked-example and report-shape checks run on the harness as unit tests
- [ ] [T-89](../tasks/T-89-tool-behaviour-checks-as-unit-tests.md) — Non-rendering tool behaviour checks run on the harness as unit tests; server-regression keeps process, extension and render checks
- [ ] [T-90](../tasks/T-90-server-regression-at-landing-only.md) — A planning rule leaves server-regression out of Verify lines, open items follow it, and the suite estimates are measured again

## Retrospective
