---
description: >-
  An agent checks a mesh with check (inside-out shells, coincident vertices, duplicate faces, shell count, stats and scale notes), shown on the leaky shed sample
---
# T-23 — Health check and the leaky shed

**Status:** In Progress

**Pri:** P1

**Milestone:** M-4

**Batch:** — (standalone)

**Depends on:** T-21

**Verify:** core-unit, server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent calls `check` and learns what is wrong with its mesh by element id, what it measures, and what a game will make of it. The milestone's content, the leaky shed, is a small house with every fault once, and `check` finds each of them.

## Scope

**Core: `src/Carve.Core/MeshHealth.cs`** (new; engine-free):

- `MeshHealth.Create(PolygonMesh mesh)` gives `Problems`, `Notes`, `Stats` and the `MeshReport` warnings.
- **Shells**: connected components of faces joined by shared edges (union-find over `Topology.Edges`), faces ascending within a shell, shells ordered by their lowest face id. For each: its faces, whether it is closed (every edge of the shell has exactly two faces), whether its winding is consistent (no edge crossed twice the same way), and, when both, its signed volume: the sum over the shell's triangles (`Triangulator`) of `dot(a, cross(b, c)) / 6` in double, taken relative to the shell's first vertex.
- **Problems**, `HealthProblem(string Check, string Element, string Message)`, in this order:
  - `inside-out`: a closed, consistently wound shell whose signed volume is negative. Element `shells[k]`; message `closed shell of 6 faces (faces 30 to 35) is inside-out: its faces are wound clockwise seen from outside, so a game culls them and sees nothing`. The face ids are given as a range when contiguous, else the first five and a count.
  - `coincident-vertices`: two vertices at exactly the same float position (bit-equal on all three). Element the later one, `vertices[j]`, message `vertex is at the same position as vertices[i]`. One problem per later duplicate.
  - `duplicate-face`: two faces with the same set of vertex ids, in any order or rotation. Element the later one, `faces[j]`, message `face has the same vertices as faces[i]`.
- **Notes** (strings, informational, never a problem): `size 4 x 3.5 x 3 m`; `base at y = 0` or `base at y = -0.2; a prop usually stands on y = 0`; `origin inside the bounds` or `origin outside the bounds`; `2 separate shells; a model is usually one` when there is more than one shell. Numbers culture-invariant, `G` format as elsewhere.
- **Stats**: `vertices`, `faces`, `edges`, `triangles` (the `Triangulator` count), `shells`, `closed`, `bounds`, `size` (max minus min), `area` (the sum of face areas, half the Newell length, in double), `volume` (the sum of the shells' signed volumes when every shell is closed and consistently wound, else null).
- `ok` is true when there are no problems and no warnings.

**Content: the leaky shed.** `tests/Carve.Core.Tests/TestMeshes.cs` gains `LeakyShed()`, with a comment per fault naming the ids it uses, and `samples/leaky-shed.carve` (new folder `samples/` at the repo root) is written by `CarveFile.Write` from the same mesh; a test asserts the file's text equals the code's mesh. The shed is a gabled house, footprint x 0 to 4 and z 0 to 3, walls to y = 2.5, ridge at y = 3.5 running along x, +Y up; a door is a separate quad face set into the +Z wall (the wall around it is several quads, so the door is a real face of the mesh); a chimney is a small closed box floating 0.1 above the roof. Every fault appears exactly once, each found by exactly one check, and the whole still passes `PolygonMesh.Create`:

| Fault | What is wrong | Found by |
|---|---|---|
| missing roof face | the +Z roof slope is absent, so the ridge and eave edges are open | `MeshReport` open edges |
| flipped wall | the -X wall's loop is reversed | inconsistent winding on its four edges |
| lifted eave corner | one corner of the -Z wall is raised 0.05 | non-planar face |
| stray vertex | a vertex at (6, 0, 0) used by no face | unused vertex |
| unmerged seam | the +X wall's two top corners are duplicated and the roof uses the copies | `coincident-vertices` (two problems), and the seam's open edges |
| duplicated door | the door face appears twice | `duplicate-face` |
| inside-out chimney | the chimney's six faces are all reversed | `inside-out` on its shell, and `2 separate shells` in the notes |

Fix the exact coordinates and ids here, write them down (the counts, every problem's element and message, every warning, the stats) in the *As built* section, since T-27's scenario asserts them. **Vertices 3 and 9 must not be an edge** of the shed (T-28's planted mistake); assert it in a test.

**Server: `src/Carve.Server/CheckTool.cs`** (new), registered in `Program.cs`, no arguments, closed schema:

- `check` returns `{"ok":false,"problems":[{"check":"inside-out","element":"shells[1]","message":"..."}],"warnings":["edges[3-7]: edge belongs to only one face", ...],"notes":["size 4 x 3.5 x 3 m", ...],"stats":{"vertices":..,"faces":..,"edges":..,"triangles":..,"shells":..,"closed":false,"bounds":{"min":[..],"max":[..]},"size":[..],"area":..,"volume":null}}`.
- Before `define_mesh` it is a tool error saying so; any argument is a tool error. It changes nothing.
- **The description, as a priority**: what each check and each warning means for a game (an inside-out shell is invisible; open edges and non-manifold edges break collision later; coincident vertices and duplicate faces z-fight and waste), the fix with today's tools (reverse the loops and `define_mesh` again for winding and inside-out; `move` for a lifted corner; `define_mesh` without the extra vertex or face, since there is no delete yet), the notes' meaning (1 unit = 1 m, props stand on y = 0), and a worked example: `check` on the unit cube giving `ok` true with its stats (`triangles` 12, `area` 6, `volume` 1).

**Tests:**

- `tests/Carve.Core.Tests/MeshHealthTests.cs`, `unit`: the cube (`ok`, area 6, volume 1, one shell), the cube reversed (`inside-out`), two cubes apart (two shells, note), the crate (`ok`, volume), coincident and duplicate cases built from the cube, an open box (`volume` null), and **the shed: every problem, warning, note and stat exactly**. The shed file round trip equals the code's mesh.
- `tests/Carve.Server.Tests/CheckToolTests.cs`, `regression`: `check` on the cube through the client equals the worked example; the shed through `define_mesh` then `check` gives the exact problems; before `define_mesh`; with an argument; `tools/list` lists `check`; version and undo unchanged.
- **Show each new check failing** on a broken product, as a table: the volume sign flipped, the shell join by vertex instead of edge, coincidence compared with a tolerance, duplicate faces compared in order, notes with the wrong unit.

## Notes

- **Decided by the person at planning (2026-11-05):** the check set (inside-out shells, coincident vertices, duplicate faces, shell count, scale notes, stats) over the minimal and the maximal sets; the leaky shed over a broken crate.
- **Decided at planning on the planner's recommendation:** coincidence is exact float equality, with no tolerance; T-junctions and face-face intersection are not checked in M-4; the output shape above.
- **This task decides what others build on**: the shed's ids and the `check` output shape. Run it on the stronger model.
- **Open rules: stop and ask.** Any tolerance anywhere. A check not in the table. A fault in the shed that two checks find, or a check that finds nothing in it. If `CarveFile` (M-3's T-15) is not on `main` when this task starts, stop: M-4 assumes M-3 has landed.
- **Writes down for T-27 and T-28:** the shed's counts, every problem, warning and note verbatim, its vertex ids by part (roof, walls, door, chimney, stray), and the path `samples/leaky-shed.carve`.
- **Files owned in this wave:** `MeshHealth.cs`, `CheckTool.cs`, `TestMeshes.cs`, `samples/`, their tests. `Program.cs` on one line, as T-22 (keep both). Not `MeshReport.cs`: its five warnings stay as they are and are passed through.
- Run `finish` from inside the worktree and leave the folder.
