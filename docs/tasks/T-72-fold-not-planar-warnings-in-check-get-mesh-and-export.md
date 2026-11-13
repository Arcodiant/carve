---
description: >-
  Fold not-planar warnings in check, get_mesh, export, open and move
---
# T-72 — Fold not-planar warnings in check, get_mesh, export, open and move

**Status:** Open

**Pri:** P1

**Milestone:** M-19

**Batch:** — (standalone)

**Depends on:** T-43

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

A subdivided mesh's report stays readable: Catmull-Clark faces are almost never planar, and one warning per face buries every other warning.

## Scope

- `check`, `get_mesh`, `export`, `open` and `move` fold not-planar warnings by the person's rule (2026-11-13): up to 10 not-planar faces are listed one per line as today; past 10, the first 10 are listed and the rest become one line: "and N more faces are not planar; select faces where non_planar names them". `subdivide` keeps its own fold (T-43). `select faces where non_planar` still names every face.
- The soft gardener's export (T-44: 504 not-planar warnings) then carries one folded line, and `scenario-soft` and `tests/Carve.SoftLoad.Tests` assert that one folded line and no other warning. Today, by the person's ruling in RV-27, they assert that every warning is a not-planar face, with the count equal to `select faces where non_planar`.

## Notes

- From the person's ruling on T-43 (2026-11-10): the fold went into `subdivide` only, and a task of its own if T-44 showed the flood getting in the way in `check` and export. T-44 did: its scenario asserts that every export warning is a not-planar face instead of none.
- **Pulled into M-19 (2026-11-13)** on the person's ruling, after T-49's acceptance run: on the subdivided character, `move`, `check`, `open` and `export` returned 74 to 120 thousand characters of not-planar warnings, which the client cut. The scope was widened to `open` and `move` then.
- **The fold count, the person's ruling (2026-11-13):** "Name 10, fold the rest", as the Scope says. The leaky shed's planted faults stay named.
- **Open rules: stop and ask.** Anything else the spec does not settle, and any loosened check.
- Run `finish` from inside the worktree and leave the folder.
