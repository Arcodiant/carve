---
description: >-
  loop_cut adds edge loops across a ring of quads at given fractions
---
# T-66 — Loop cut

**Status:** Done

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

### As built

Agent: Sonnet (claude-sonnet-5-5).

- **`LoopCut`** (`src/Carve.Core/LoopCut.cs`): `LoopCut.Cut(mesh, (a, b), at) -> LoopCutResult(EditResult Edit, IReadOnlyList<int> Ring)`. It uses `FaceSplit.Split` as written and adds nothing to `FaceSplit` or `Deleted`. The ring walks both ways from the edge across plain quads (no holes, four corners) and stops at a closing, an open edge, a triangle, an n-gon or a holed face. Each quad gets one `FaceCut` per fraction, from the point on its entry edge to the point on its exit edge, both `OnEdge(s, t, fraction)` with `s` the end on `a`'s side, carried across each quad by its side edges. The stopped-at face is not split but takes the new vertex on the ring edge it shares (FaceSplit's rule). Too near an end: `fraction * shortest <= FaceSplit.Tolerance(mesh)` (or `(1 - fraction)`), `shortest` the shortest ring edge.
- **`loop_cut` tool** (`src/Carve.Server/LoopCutTool.cs`, in `Program.cs`'s list after `plane_cut`): worked example `{"edge":[0,3],"at":[0.5]}` on `define_mesh`'s cube (12 vertices, 10 faces, closed, split faces 0, 1, 4, 5); the description says where the ring stops; one `undo` step.
- **Refused** (each names `edge[a-b]` or `at[i]`): not an edge (names the pair, says `select` with kind `edges` lists the real ones); no quad on the edge; an empty `at`; a fraction not strictly in 0..1 or not finite; not strictly ascending; too near an end.
- **The person's ruling (2026-11-12, after T-66 finished):** an edge on more than two faces, as the start edge or met mid-ring, is refused (`... on 3 faces: loop_cut does not cross a non-manifold edge`). T-66 landed this refusal provisionally, although its spec said to stop and ask; the person kept it as landed. **Not handled, filed as B-3 (the person: refuse it, fix later):** a ring that closes on itself the other way round (a Moebius band of quads). The walk just closes at the visited quad, so the last quad would put a second point on the shared edge at the mirrored fraction. A twisting mesh could not be built for a test (every quad must be planar and simple), so no refusal was written.
- **Tests.** `LoopCutTests` (Core unit, 20) and `LoopCutToolTests` (Server regression, 2). Core: the cube's closed ring; an open strip stopping at its ends, started on an end rung and on a middle rung; a triangle stop; a holed face stop; two fractions; the side kept across a quad whose loop runs the other way (from vertex 0 or 3 on the cube, and a trapezoid strip with a reversed quad); ids (8..11, 6..9, `split` kept id first) and determinism; weights blended at the fraction; each refusal, incl. a mid-ring non-manifold edge.
- **Cuts stage** (`scenarios/Carve.Scenario.Cuts/LoopCutStage.cs`, one line in `CutsStages.cs`): the 1 x 2 x 1 box, `loop_cut {edge:[0,3], at:[0.45,0.55]}` (16 vertices, 14 faces, closed, each split face three pieces, new vertices at y 0.9 and 1.1 from get_mesh), frame `loop-cut-wireframe` from `[0.6, 0.35, 1]` with the 8 loop edges highlighted (the existing `Look`, `Line` and `NotLine` checks, as `PlaneCutStage` uses them), then the band's front face extruded by 0.1 (20 vertices, 18 faces, z = 1.1 from get_mesh) and frame `loop-cut-band-extruded` shaded (probes on the band's front, top and right; the same-shade-per-face and background checks). No new pixel check or threshold.

**Breaks** (each applied alone by `C:/Users/andre/AppData/Local/Temp/t66/breaks.py`, then restored; `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-66/breaks.log`):

  | Check | Break | Result |
  |---|---|---|
  | ring walks across quads | stops after one | 8 fail |
  | ring walks both ways | second direction never walked | 1 fails |
  | a triangle stops the ring | quad test lets triangles in | 2 fail |
  | a holed face stops the ring | hole test dropped | 1 fails |
  | an open edge stops the ring | stop test dropped | 2 fail |
  | each fraction is a loop | first only | 1 fails |
  | side kept (loop one way / the other) | exit edge reversed | 2 / 1 fail |
  | fractions from `a`'s side | from the higher id | 2 fail |
  | weights at the fraction | `1 - t` | 2 fail (also moves the geometry) |
  | ids and determinism | cuts in walk order, not face order | **0 fail**: the order comes from `FaceSplit`, so no break of this member's own shows it; the test stands on `FaceSplit`'s |
  | refused: not an edge | reported as another error | 1 fails (first break did not build, CS8602; rewritten and rerun) |
  | refused: no quad; three faces; mid-ring non-manifold | each check never fires | 1 fails each |
  | refused: empty at; not in 0..1; not ascending; too near an end | each check never fires | 1 / 3 / 2 / 2 fail |
  | tool: edge in order; every fraction; `at` required | each broken | 1 fails each |
  | Cuts: ring round all four sides; both loops; loops where the fractions say; highlight set | each broken | run failed |
  | Cuts: the band out by 0.1 | distance 0.05 | first run passed (the ray test follows the geometry), so the z check from get_mesh was added; rerun failed |

**Frames.** `cairn task T-66 verify` passed (ran 10, carried 1) on `60ed146`. The seven scenario runs (jobs `J-20261112-161347-5a96e6` to `-161409-643ce0`, each `carve@60ed146`) were fetched by job id into `D:/repos/carve/branches/milestone/M-5/artifacts/review/T-66/frames-60ed146/`, 135 frames, listed in `all.sha256` there. Against T-65's `frames-aae1140/all.sha256` (133 frames; T-67 had landed nothing): none differs, none is missing. Two new, the same hashes as the local run:

  | Frame | sha256 |
  |---|---|
  | `cuts/frames/loop-cut-band-extruded.png` | `a1d8d8334ef9f4ae1c9e5247514393e035119c73880674d3c4b93e96f65a1db9` |
  | `cuts/frames/loop-cut-wireframe.png` | `8d510905dd22d92bde53b859c92f4ac7c7b2f92935e6b11cc7b32cf3e9f2f2ab` |

**The person's review:** both new frames were sent through Waystone and approved with no notes.
