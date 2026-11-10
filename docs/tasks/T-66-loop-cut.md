---
description: >-
  loop_cut adds edge loops across a ring of quads at given fractions
---
# T-66 — Loop cut

**Status:** Open

**Pri:** P1

**Milestone:** M-5

**Batch:** — (standalone)

**Depends on:** T-65

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent adds edge loops round a shape, a band to extrude or a place to bend, by naming one edge and where along it the loops go.

## Scope

**Core: `LoopCut`** (its own file), on T-65's `FaceSplit`. `loop_cut(edge: [a, b], at: [fractions])`.

- **The ring.** It starts at the given edge. It walks both ways, across each quad on the current edge to that quad's opposite edge. It stops where it closes, at an open edge, or at a face that is not a plain quad: a triangle, an n-gon, or a holed face.
- **The fractions.** Each one is a new loop, at that fraction of every ring edge, measured from the end on `a`'s side. The side carries across each quad by its side edges, so the loops stay parallel.
- `at` must be strictly ascending, and each fraction strictly between 0 and 1. A fraction too near an end is refused, by the planarity tolerance of the shortest ring edge.
- Ids, `split` and what carries all follow `FaceSplit`: colours, groups, hard edges and weights.
- **Refused:**
  - a pair that is not an edge of the mesh (the error names the pair, and says `select` with kind `edges` lists the real ones, facts only, as `move`'s does);
  - an edge with no quad on it;
  - a bad `at`.

**Server.** The `loop_cut` tool, with the description and a worked example on `define_mesh`'s unit cube: one loop at 0.5 round the four sides, starting on a vertical edge.

- The description says where the ring stops.
- The result's `split` lists each quad's two or more pieces.
- One `undo` step.

**Tests.**

- `unit`, each with a break shown failing:
  - a ring round a cube's sides (closed);
  - a ring on an open strip (stops at the open ends);
  - a ring stopped by a triangle and by a holed face;
  - two fractions;
  - the side kept across a quad whose loop runs the other way;
  - ids and determinism;
  - carried weights;
  - each refusal.
- `regression`: `loop_cut` through the client, then `check` closed.

**Content.** `LoopCutStage.cs` in the Cuts scenario:

- a 1 x 2 x 1 box with two loops at 0.45 and 0.55 round its sides, from `[0.6, 0.35, 1]` in the wireframe with the new edges highlighted;
- the band's front face extruded out by 0.1, shaded from the same direction.

Ray-test pixel checks, each shown failing on a break.

## Notes

- **Decided by the person at planning (2026-11-09):** `loop_cut(edge, at)` as in Scope, and the ring stopping at a non-quad or an open edge.
- Uses `FaceSplit` and `Deleted` as T-65 and T-64 wrote them down, and adds nothing to them. A need in `FaceSplit` goes back to the orchestrator, because T-67 shares it.
- T-67 runs beside this task. Both add a tool to `Program.cs` and a line to `CutsStages.cs`, so keep both sides. Neither edits the other's files.
- **Open rules: stop and ask.** A ring through a non-manifold edge. Any `at` form other than fractions.
- Every existing frame stays byte-identical by hash, listed.
- Sonnet. It is a client of `FaceSplit`, not a design.
- Evidence goes to the milestone worktree's `artifacts/review/T-66/` by absolute path.
- Run `finish` from inside the worktree and leave the folder.
