---
description: >-
  Fingerprint linked CameraPixels.cs in the server suites
---
# T-73 — Fingerprint linked CameraPixels.cs in the server suites

**Status:** Open

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

A change to a file the server tests link in from a scenario reruns the suites that use it, rather than carrying an old pass.

## Scope

- Add `scenarios/Carve.Scenario.Crate/CameraPixels.cs` to the hand-listed inputs of `server-unit` and `server-regression` in `tests.json`. `tests/Carve.Server.Tests/Carve.Server.Tests.csproj:50` links it, and `NamedMeshTests.cs:248` uses it.
- Check every other `<Compile ... Link=...>` in the repo against its suites' inputs, since `dotnet-closure` does not follow links.

## Notes

- Found by RV-28 (M-18, round 2). The gap dates from RV-24 (`e6d294e`) on main, not from M-18.
