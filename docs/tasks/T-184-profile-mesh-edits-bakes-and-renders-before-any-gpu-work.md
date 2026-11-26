---
description: >-
  Measure where time goes in edits, subdivide, bakes, graph evaluation and render on the largest samples, to decide whether GPU acceleration is worth designing
---
# T-184 — Profile mesh edits, bakes and renders before any GPU work

**Status:** Open

**Pri:** P3

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

A table of where a session's time goes, so the question "should any of this run on a GPU?" is answered from numbers. [D-8](../design/D-8-scripts-the-call-journal-and-atomic-edits.md) designs nothing for a GPU until this is in.

## Scope

- Time, in Release, each of: the edit tools, `subdivide` at two and three levels, `unwrap`, each `bake`, a graph material's evaluation, `render` and `export`, on the largest models to hand (the soft gardener, the wrench at 1024 and 2048, the fps-arms cage if it can be had, a stone-room-sized scene).
- For each, the share that is whole-array arithmetic (stencils over every vertex, a map over every texel) against element-at-a-time work.
- Report the table and a recommendation to the person. Build nothing.

## Notes

- Raised in the discussion of 2026-11-22. The person's idea, recorded in D-8: operations built only from GPU-translatable parts are GPU-capable by construction. The person agreed a profile comes first.
- The one recorded speed problem is patina's evaluation at 8192 (T-130); M-33 is about its PNG encoding. If a GPU translation is tried anywhere, a texture graph is the likeliest first place.
