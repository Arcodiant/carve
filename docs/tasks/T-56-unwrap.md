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

### Plan

Planned by Claude Opus 5.5 (`claude-opus-5-5`), 2026-11-13, on `milestone/M-6` at `b46b6fa` (T-53 landed; T-54 not yet). Every number below is a proposal until the person rules on the open rules.

**Files.**

- Add `src/Carve.Core/UnwrapIslands.cs` (island finding, disc test), `UnwrapLscm.cs` (local frames, assembly, CG), `UnwrapPack.cs` (orient, hull, shelf, fit) and `Unwrap.cs` (the entry: `Unwrap.Run(PolygonMesh, IReadOnlyList<int>? faces, (double U0, double V0, double U1, double V1) into, double? margin)`, giving either a new `MeshUvs` with one `UnwrapIsland` per island and the fill, or `IReadOnlyList<MeshError>`). Engine-free; no `System.Random`, clock, `Parallel`, `Vector<T>` or LINQ `Sum` over doubles: plain `for` loops in a fixed order.
- No trig in the pipeline (`Math.Sin/Cos/Atan2` may differ by platform): rotations are built from normalised direction vectors with `Math.Sqrt` only, so the sample is bit-identical on every runner.
- Add `src/Carve.Server/UnwrapTool.cs`, and `UnwrapTool.Create()` to `Program.cs`'s tool list (add only).
- The UV write goes through T-54's store method for setting UVs (one undo step, built on `ModelStore.Change` with `WithUvs`). If T-54 lands none, add `ModelStore.Remap(...)` beside `Reshade` (add only).
- `tests/Carve.Core.Tests/TestMeshes.cs` (add only): `Grid(n, m)` (flat quads), `BentStrip()` (a quad strip folded 90 degrees along one rung), `Tube()` (the barrel's faces 0-31 only), `SeamedBarrel()` (the barrel with the Scope's 12 seams by `MeshUvs.WithSeams`).
- Tests: `tests/Carve.Core.Tests/UnwrapTests.cs`, `UnwrapSampleTests.cs`; `tests/Carve.Server.Tests/UnwrapToolTests.cs` (regression through the real `carve`). `samples/barrel.carve`; `tests.json` gains it under `core-unit` and `server-regression` (add only).
- The sample is written once by a helper (`CarveFile.Write(Model.Of("mesh", built))`) and checked as the gatehouse is: `Assert.Equal(CarveFile.Write(built), CarveFile.Write(Read(sample)))`, exact text.

**Islands** (`UnwrapIslands`).

- The set: `faces`, or every live face, ascending. Each face is triangulated by `Triangulator.TriangulateFace` (holes bridged, then ear-clipped); each triangle vertex maps to its corner by `Face.CornerOf(vertex)` (a vertex is in a face once, hole corners included), so a UV lands on every `Face.Corners` entry.
- An edge of `mesh.Topology.Edges` joins its two faces when it has exactly two uses, both faces are in the set, it is not a seam (`MeshUvs.IsSeam`), and `IsManifold` (used in opposite directions; rule 2). Every other edge (open, seam, 3+ faces, set/rest border) is cut. Union-find over face ids, lowest id as root; islands ordered by lowest face id.
- Island vertices: the corners at one mesh vertex within an island are grouped by walking across joined edges around the vertex; each group is one island vertex (a seam vertex on the barrel's side is two; a bowtie vertex splits). Numbered by first appearance, faces ascending, corners in `Corners` order.
- Disc test on the cut island: boundary half-edges (cut edges, and the edges of a hole with nothing across it) chained into loops; V, E, F counted on the triangulation. A disc has one loop and V - E + F = 1. Anything else is refused (rule 1): `faces[f]: an island of n faces with k boundary loops is not a disc; mark a seam across it with mark_edges, cutting every closed loop until each island is a disc`. Every island is checked before any is solved, and every refusal is listed.

**LSCM** (`UnwrapLscm`), Levy et al. 2002, in double.

- Each triangle (a, b, c), wound as its face, gets a local orthonormal frame in its plane: `x0 = (0, 0)`, `x1 = (|b - a|, 0)`, `x2 = (dot(c - a, e1), |cross(e1, c - a)|)`. With `z_j = x_j + i y_j` and `W_j = z_{j+2} - z_{j+1}`, its residual is `sum_j W_j U_j / sqrt(2A)`, two real rows. `U = z` has zero energy, so the map preserves orientation in math axes. A triangle with `2A <= 1e-12 L^2` (L the island's 3D bounding-box diagonal) contributes nothing.
- Pins: the two boundary island vertices farthest apart in 3D (squared distance in double), ties to the lower mesh-vertex pair `(lo, hi)`, then the lower island-vertex index; at `(0, 0)` and `(d, 0)`. A zero `d` is refused ("has no extent").
- `K = A_f^T A_f` assembled as CSR (rows by free unknown, columns ascending, accumulated in triangle order); `b = -A_f^T A_p x_p`.
- **CG:** Jacobi-preconditioned, from zero; stop at `||r||_2 <= 1e-10 ||b||_2`; cap **10 000** iterations. Not converging refuses the call (rule 4): `faces[f]: the flattening did not converge (residual r after 10000 iterations)`.
- Then the screen map `(u, v) = (x, -y)`: CCW in math axes becomes CCW on screen with v down, a negative shoelace sum (T-55's rule). Nothing mirrors after this.
- **Scale** (rule 3): each island is scaled by `sqrt(A_3D / A_uv)` over its triangles, so its area is its real area; packing then applies one common scale.
- **Stretch** per triangle: `sigma_max / sigma_min` of the 2x2 map from its local frame to UV; the island's is the largest. Scale-free, so packing leaves it. Folded triangles (positive shoelace) are counted (rule 5).

**Orient and pack** (`UnwrapPack`).

- Convex hull of the island's UVs (Andrew's monotone chain, from the lowest `(u, v)`). For each hull edge, the bounding rectangle along it; the first edge is kept unless a later one is smaller by more than `1e-9` relative (so the octagon caps' ties go to the first edge). Rotate that edge onto +u; if then taller than wide by more than `1e-9` relative, rotate by 90 degrees (`(u, v) -> (-v, u)`, a rotation). Translate the rectangle's min corner to the origin.
- Order: height descending, then lowest face id. Shelves fill left to right from `(u0 + m, v0 + m)`; a new shelf starts when the next island would pass `u1 - m`. Margin `m` between islands, between shelves and to every edge.
- Fit: the largest common scale `s` at which the shelf packing fits inside `into`, by a fixed 60-step bisection on `[0, s_max]`, `s_max = min((W - 2m) / max w_i, (H - 2m) / max h_i)`; the packing at the final lower bound is the result. Default `m = min(W, H) / 64` (rule 6). Leftover space stays bottom-right (rule 7).
- **Fill** = the islands' UV triangle area / `into`'s area (rule 8). UVs rounded once to `float`.

**The tool** `unwrap(mesh?, faces?, into?, margin?)`.

- `into: [u0, v0, u1, v1]`, default `[0, 0, 1, 1]`, refused as T-55 refuses it (not finite, no area). `margin`: finite, `>= 0`, `< min(W, H) / 2`. `faces`: refused by element (`faces[k]`: unknown, deleted, listed twice), and an empty list. Unknown arguments as every tool.
- Faces outside the set keep their UVs; seams are untouched. One undo step; a refused call changes nothing.
- Returns `{mesh, islands: [{faces, lowest_face, rect: [u0, v0, u1, v1], stretch, flipped}], fill, scale}`, islands by lowest face id; `scale` is the common UV units per metre (rule 10).
- The description: what a seam must do, why a cylinder needs one seam down its side and its caps cut free, the barrel worked example (the four vertical edges and both rings by `mark_edges(edges, seam: true)`, then `unwrap()`), the defaults, the result and every refusal.
- **Relies on T-54** for `mark_edges`' `seam` (the regression and the worked example), the UV store method, and `get_mesh`'s `uvs`/`seams` (the regression reads them back). The Core and the sample need only T-53.

**The barrel.** Three islands: the side (lowest face 0, 32 faces), the bottom cap (face 32), the top cap (face 33). Expected roughly: the side about 2.4 x 1.0 m, first, across the top shelf about 0.97 wide; the two caps (about 0.67 m square) side by side on the second shelf; fill about 0.5. The exact rectangles, scale, fill and CG iteration counts go in As built for T-61 and T-62.

**Tests.**

- `unit`:
  - `Grid(4, 3)` flattened (before packing) equals its XY positions up to a rigid motion: max corner error `<= 1e-9` of the grid size in double, `<= 1e-6` after the float round; stretch `<= 1 + 1e-9`.
  - `BentStrip()`: one island, stretch `<= 1 + 1e-6`.
  - `Tube()` refused (2 loops) and `Cube()` refused (0 loops), naming the lowest face, face count, loop count and `mark_edges`.
  - Two runs on the seamed barrel are bit-identical (`BitConverter.SingleToInt32Bits`).
  - The barrel: three islands as above; rectangles apart by at least `m - 1e-6`; every face unflipped (outer-loop shoelace `< 0`); every UV inside `into`; a partial `faces` leaves the rest's UVs bit for bit; CG below the cap.
  - The sample equals what Core makes.
- `regression`: seams by `mark_edges`, `unwrap`, `get_mesh` shows the UVs; each refusal (bad face, `into`, `margin`, unknown argument, a seamless tube) changes nothing; `undo` restores the previous UVs; the `tools/list` schema.
- Broken-product table as T-53's: drop the `-y` (faces flip), pin by id rather than distance, skip the margin (overlap), join across seams (island count), skip the area rescale (side/cap density).

### Open rules for the person

1. **Holed faces and the disc rule.** A hole with nothing across it (a window with no reveal) gives its island two loops, and no seam can cross one face. *Recommend:* allow extra loops that are a single face's hole loops (LSCM handles them). *Alternatives:* refuse as the Scope says, telling the agent to knife the face to its hole or project it `planar`; or allow any island with a boundary and no handle (but then a seamless tube is accepted, against the Scope's test).
2. **A mis-wound edge** (two faces using it in the same direction). *Recommend:* cut there. *Alternative:* join, as the Scope's wording allows (the island then folds).
3. **Scale.** Pinning at the 3D distance `d` keeps real scale only on a flat island: on the barrel's side the farthest pair is a chord, shorter than its unrolled distance, so the side comes out smaller than the caps and texel density is uneven. *Recommend:* pin as the Scope says, then rescale each island to its real area. *Alternative:* pins only, as written.
4. **CG not converging** (`1e-10` relative, 10 000 iterations). *Recommend:* refuse, naming the island. *Alternative:* keep the last iterate and warn.
5. **Folded triangles** after LSCM. *Recommend:* report `flipped` per island (normally 0), not refuse. *Alternative:* refuse the call.
6. **Margin for a non-square `into`.** *Recommend:* `min(W, H) / 64`. *Alternative:* `W / 64` across and `H / 64` down.
7. **Leftover space.** *Recommend:* packed from the top-left, leftover bottom-right. *Alternative:* centre the packing in `into`.
8. **Fill.** *Recommend:* UV triangle area over `into`'s area. *Alternative:* the islands' rectangles' area.
9. **Tolerances** (not yet seen): grid `1e-9` (double) and `1e-6` (float), bent strip `1 + 1e-6`, CG `1e-10` / 10 000, degenerate triangle `1e-12 L^2`, rectangle ties `1e-9`, sample as exact text. *Recommend:* as listed. *Alternative:* a looser grid tolerance (`1e-5`) and the sample compared within `1e-6` per UV.
10. **Result fields beyond the Scope:** `scale` and `flipped`. *Recommend:* include both. *Alternative:* the Scope's fields only.

### Rulings (the person, 2026-11-13)

All ten as recommended. Rule 1: extra boundary loops are allowed when they are one face's hole loops, and any other island that is not a disc is refused. Rules 2 to 10 as written above.
