---
description: >-
  unwrap cuts at seams and open edges, flattens each island by LSCM and packs them, shown on the barrel sample
---
# T-56 — Unwrap

**Status:** Open

**Pri:** P1

**Milestone:** M-6

**Batch:** — (standalone)

**Depends on:** T-54

**Verify:** core-unit, server-unit, server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent marks seams and calls `unwrap`: the faces are cut into islands, each is flattened with little distortion and the islands are packed into UV space, all deterministically. The barrel sample shows it.

## Scope

- **Islands** (`src/Carve.Core/Unwrap*.cs`):
  - Faces are joined across an edge that is not a seam, not open, not shared by more than two faces, and has both faces in the chosen set.
  - Each island must be a disc: one boundary loop. An island with no boundary or with several loops is refused, naming its lowest face id, its face count and its loop count, and saying to mark a seam across it with `mark_edges`.
- **Flattening:** least-squares conformal maps (LSCM), written here.
  - Pin the island's two boundary vertices farthest apart in 3D (ties go to the lower id) at `(0, 0)` and `(d, 0)`, where `d` is their distance, so an island keeps its real-world scale.
  - Solve the normal equations by conjugate gradient in double, in a fixed order, with a stated tolerance and iteration cap. No `System.Random`, so two runs are bit-identical.
  - Turn each island so its minimum-area bounding rectangle is axis-aligned and wider than tall. Islands are never mirrored: every face comes out unflipped (rule in T-55).
- **Packing:**
  - Every island keeps one common scale, so texel density is even.
  - Shelf-pack them, sorted by height and then by lowest face id, with `margin` between islands and to the edge (default 1/64 of `into`'s size: four texels of the 256 checker).
  - Then scale uniformly to fit `into` (default the unit square). Fill it as well as a shelf packer can, and report the fill.
- **The tool, `unwrap`** (`src/Carve.Server/UnwrapTool.cs`):
  - Arguments: `mesh`, `faces` (default all), `into`, `margin`.
  - Faces outside the set keep their UVs, and an edge between the set and the rest counts as cut.
  - One undo step. A refused call changes nothing.
  - Returns each island's face count, lowest face id, UV rectangle and **stretch** (the largest ratio of a triangle's two singular values over the island; 1 is none), and the fill.
- **The description is content:** what a seam must do (cut every closed loop until each island is a disc), why a cylinder needs one seam down its side and its caps cut free, and a worked example on the barrel's seams.
- **The barrel sample is this mechanic's content.** `samples/barrel.carve` is written by `CarveFile.Write`: `TestMeshes.Barrel()` with its seams marked and then unwrapped with the defaults.
  - The seams are the vertical edges at -Z, `(2, 10)`, `(10, 18)`, `(18, 26)` and `(26, 34)`, and both cap rings, `(0, 1)` ... `(7, 0)` and `(32, 33)` ... `(39, 32)`.
  - That gives three islands: the side, 32 faces; the bottom cap, face 32; and the top cap, face 33.
  - A `unit` test keeps the sample equal by content to what Core makes, as `MeshHealthTests` does for the shed.
  - `samples/barrel.carve` joins the `core-unit` and `server-regression` inputs in `tests.json`.
- **Tests:**
  - `unit`: a flat grid of quads comes back as itself up to rotation and translation, within a stated tolerance; a bent strip has stretch near 1; a tube with no seam and a closed cube with no seams are refused; two runs are bit-identical; the barrel gives three islands with no overlap and every face unflipped.
  - `regression`: the tool, its refusals and undo.

## Notes

- **Design-heavy:** a numerical method and the packing every model's UVs come out of, so Opus. Plan for two agents.
- Runs side by side with T-55, T-57, T-59 and T-60. It owns `Unwrap*.cs`, `UnwrapTool.cs` and `samples/barrel.carve`. Its only shared edits are the tool list, `TestMeshes` and `tests.json` (all add only); keep both sides in any conflict.
- **Write down for the dependants:** the solver's tolerance and cap, the margin, the result shape, and the barrel's island rectangles. T-61 reads them and T-62's plant is built on the sample.
- **Carried from earlier plans:** as T-53's Notes (the hash guard, a break per new check as a table, `finish` read whole, artifacts by job id, report a long `verify` wait). Evidence goes in the milestone worktree's `artifacts/review/T-56/`.
- **Open rules: stop and ask.** Anything the milestone's *Decided at planning* does not settle, any loosened check, and any tolerance the person has not seen.
- Run `finish` from inside the worktree and leave the folder.
