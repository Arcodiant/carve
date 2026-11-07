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

**Content: the leaky shed.** `tests/Carve.Core.Tests/TestMeshes.cs` gains `LeakyShed()`, with a comment per fault naming the ids it uses, and `samples/leaky-shed.carve` (new folder `samples/` at the repo root) is written by `CarveFile.Write` from the same mesh; a test asserts the file's text equals the code's mesh. The shed is a gabled house, footprint x 0 to 4 and z 0 to 3, walls to y = 2.5, ridge at y = 3.5 running along x, +Y up; a door is a separate quad face set into the +Z wall (the wall around it is several quads, so the door is a real face of the mesh); a chimney is a small closed box floating 0.1 above the roof. Every fault appears exactly once, each found by the checks its row names, and the whole still passes `PolygonMesh.Create`:

| Fault | What is wrong | Found by |
|---|---|---|
| missing roof face | the +Z roof slope is absent, so the ridge and eave edges are open | `MeshReport` open edges |
| flipped wall | the -X wall's loop is reversed | inconsistent winding on its four edges |
| lifted eave corner | one corner of the -Z wall is raised 0.05 | non-planar face |
| stray vertex | a vertex at (6, 0, 0) used by no face | unused vertex |
| unmerged seam | the +X wall's two top corners are duplicated and the roof uses the copies | `coincident-vertices` (two problems), and the seam's open edges |
| duplicated door | the door face appears twice | `duplicate-face`, and non-manifold and inconsistent winding on its four edges |
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
- **Ruled by the person (2026-11-07):** the duplicated door cannot be found by `duplicate-face` alone. Any edge of a doubled face is non-manifold, or crossed twice the same way, or, if the second copy is reversed and the door joined to nothing, the pair is a third shell. So its row names `duplicate-face` plus non-manifold and inconsistent winding on its four edges, the way the seam and chimney rows name a second finder. The non-manifold warning's finding in the shed is the door, so "a check that finds nothing in it" does not apply to it. The shed keeps its 2 shells.

### As built

Built on Opus 5.5 (`claude-opus-5-5`).

`MeshHealth.Create` also exposes `Shells` (`MeshShell(Faces, Closed, ConsistentWinding, SignedVolume)`). `check` is `CheckTool`, in `Program.cs`'s tool list. The sample is `samples/leaky-shed.carve`, with `samples/.gitattributes` pinning LF so the text stays equal to `CarveFile.Write` on every checkout. It is an input of `core-unit` and `server-regression` in `tests.json`, so a changed sample does not carry an old pass. `CLAUDE.md`'s *Layout* gains the `samples/` row.

**The shed's vertices (25).**

| Ids | Part | Positions |
|---|---|---|
| 0-3 | floor corners | (0,0,0) (4,0,0) (4,0,3) (0,0,3) |
| 4 | lifted eave corner, -Z and -X walls | (0,2.55,0) |
| 5-7 | other wall tops | (4,2.5,0) (4,2.5,3) (0,2.5,3) |
| 8-9 | ridge | (0,3.5,1.5) (4,3.5,1.5) |
| 10-11 | seam copies of 5 and 6 | (4,2.5,0) (4,2.5,3) |
| 12-15 | door | (2.5,0,3) (3.5,0,3) (3.5,2,3) (2.5,2,3) |
| 16-23 | chimney, the cube's corner order | x 1 to 1.4, y 3.6 to 3.9, z 1.3 to 1.7 |
| 24 | stray | (6,0,0) |

**The shed's faces (18).**

| Id | Part | Loop |
|---|---|---|
| 0 | floor | 0 1 2 13 12 3 |
| 1 | -Z wall | 0 4 5 1 |
| 2 | -X wall, flipped | 4 7 3 0 |
| 3 | +X wall | 1 5 6 2 |
| 4, 5, 6 | +Z wall around the door | 3 12 15 7; 15 14 6 7; 13 2 6 14 |
| 7, 8 | door, and its duplicate | 12 13 14 15, both |
| 9 | -X gable | 4 7 8 |
| 10 | +X gable | 11 10 9 |
| 11 | -Z roof slope | 4 8 9 10 |
| 12-17 | chimney, reversed: its -z, +z, -y, +y, -x, +x sides | the cube's loops reversed, ids + 16 |

Vertices 3 and 9 are not an edge; a test asserts it.

**`check` on the shed, verbatim.** `ok` is false.

Problems:

| check | element | message |
|---|---|---|
| `inside-out` | `shells[1]` | `closed shell of 6 faces (faces 12 to 17) is inside-out: its faces are wound clockwise seen from outside, so a game culls them and sees nothing` |
| `coincident-vertices` | `vertices[10]` | `vertex is at the same position as vertices[5]` |
| `coincident-vertices` | `vertices[11]` | `vertex is at the same position as vertices[6]` |
| `duplicate-face` | `faces[8]` | `face has the same vertices as faces[7]` |

Warnings (22, in this order):

```text
edges[0-3]: two faces cross this edge in the same direction
edges[0-4]: two faces cross this edge in the same direction
edges[3-7]: two faces cross this edge in the same direction
edges[4-5]: edge belongs to only one face
edges[4-7]: two faces cross this edge in the same direction
edges[4-10]: edge belongs to only one face
edges[5-6]: edge belongs to only one face
edges[6-7]: edge belongs to only one face
edges[7-8]: edge belongs to only one face
edges[8-9]: edge belongs to only one face
edges[9-11]: edge belongs to only one face
edges[10-11]: edge belongs to only one face
edges[12-13]: edge is shared by 3 faces
edges[12-13]: two faces cross this edge in the same direction
edges[12-15]: edge is shared by 3 faces
edges[12-15]: two faces cross this edge in the same direction
edges[13-14]: edge is shared by 3 faces
edges[13-14]: two faces cross this edge in the same direction
edges[14-15]: edge is shared by 3 faces
edges[14-15]: two faces cross this edge in the same direction
faces[11]: face is not planar: a vertex is 0.0105 off its plane (limit 0.000439)
vertices[24]: no face uses this vertex
```

Which warning belongs to which fault:

| Fault | Warnings |
|---|---|
| flipped -X wall | winding on `edges[0-3]`, `edges[0-4]`, `edges[3-7]`, `edges[4-7]`, with **no** non-manifold warning: each has exactly two faces |
| duplicated door | winding on `edges[12-13]`, `edges[12-15]`, `edges[13-14]`, `edges[14-15]`, **each also** `edge is shared by 3 faces`, which is how to tell them from the wall's |
| missing +Z slope | open `edges[6-7]` (eave), `edges[7-8]`, `edges[9-11]` (rakes), `edges[8-9]` (ridge) |
| unmerged seam | open `edges[4-5]`, `edges[4-10]` (along the eave), `edges[5-6]`, `edges[10-11]` (along the +X gable's foot) |
| lifted eave corner | `faces[11]` |
| stray vertex | `vertices[24]` |

Notes: `size 4 x 3.9 x 3 m`, `base at y = 0`, `origin inside the bounds`, `2 separate shells; a model is usually one`.

Stats: `{"vertices":25,"faces":18,"edges":38,"triangles":36,"shells":2,"closed":false,"bounds":{"min":[0,0,0],"max":[4,3.9,3]},"size":[4,3.9,3],"area":60.09371484553011,"volume":null}`. The area was also worked by hand, face by face. The stray at x = 6 is outside the bounds, which are of the vertices faces use. The chimney's signed volume is -0.04800003910065175.

**Readings, for the person to see.**

- **The chimney is above the ridge, so the size is 4 x 3.9 x 3, not 4 x 3.5 x 3.** T-27 selects it with `faces where min.y >= 3.6`, which only a chimney clear of the 3.5 ridge can pass, so its base is at y = 3.6, 0.1 above the roof's highest line, straddling the ridge. The Scope's `size 4 x 3.5 x 3 m` is the house without the chimney; the milestone says "about". A chimney on the slope would have kept 3.5 and made T-27's selection empty. If 3.5 is wanted, only the chimney's y and T-27's query change.
- **The lifted corner is on the -X side, and the non-planar face is the -Z roof slope, not the -Z wall.** Raising a corner in y keeps the wall in z = 0, so the wall stays planar; the slope using the corner is what bends. It has to be the -X corner because the +X corners must stay bit-equal with their copies.
- **Quad walls with separate gable triangles.** With the +Z slope missing, a pentagon +X wall would leave the copy of (4, 2.5, 3) on no face, a second unused vertex. As the foot of a gable triangle both copies are used, and the seam runs along that foot and the -Z eave.
- **A floor face.** The table does not name one, but the flipped wall's four winding edges need a partner along its foot. Its +Z side runs through 13 and 12, so the door's foot edge is shared.
- **The +Z wall is three quads around the door**, with no extra vertices (not all rectangles), so its outer edges stay `3-7` and `2-6`, shared with the side walls. **The door is off centre**, x 2.5 to 3.5, because T-27's pixel checks rely on that.
- **"Origin inside the bounds" includes on them**, or no prop standing on y = 0 would pass. The comparison is exact, with no tolerance.
- **A scattered shell's faces** read `faces 1, 3, 5, 7, 9 and 1 more`, or all the ids when there are five or fewer.
- **A later duplicate names the first** vertex or face with that position or set, so three coincident vertices give two problems that both name the first.
- **The volume sums, then divides by 6**: the same formula with less rounding, so the cube's is exactly 1.

**For T-27, to check before asserting** (nothing changed there):

- `faces where normal.y > 0.5` gives faces **11 and 14**, not the slope alone: the chimney's reversed underside points up.
- `front`'s eye is on the +X side, so the flipped -X wall is seen from inside there, where its reversed loop faces the camera. By the rule it is drawn and labelled, not culled, in the front views (not rendered here). It is culled from outside, as in the `-x` tile.

**Each new check shown failing.** Core tests in `MeshHealthTests`; each break was applied, run and reverted.

| The break | The check that failed |
|---|---|
| volume sign flipped | `The_cube_reversed_is_inside_out`, `The_cube_is_ok_with_area_6_volume_1_and_one_shell`, `The_shed_gives_every_problem_warning_note_and_stat_exactly` and 8 more |
| shells joined by vertex, not edge | `Cubes_sharing_only_a_vertex_are_two_shells` |
| coincidence compared with a tolerance (rounded to 3 places) | `Coincidence_has_no_tolerance_one_float_step_apart_is_not_coincident` |
| duplicate faces compared in order | `A_face_with_the_same_vertices_is_a_duplicate`, the rotated, reversed and reversed-rotated cases |
| notes with the wrong unit (cm) | `The_shed_gives_every_problem_warning_note_and_stat_exactly`, `The_cube_is_ok_with_area_6_volume_1_and_one_shell`, `Two_cubes_apart_are_two_shells_and_a_note`, `Notes_do_not_follow_the_culture` |
| volume taken on a closed shell of mixed winding | `A_closed_shell_of_mixed_winding_is_not_inside_out_and_has_no_volume` |
| sample file drifted from the code (stray moved) | `The_shed_file_is_the_code_s_mesh` |

`It_changes_nothing_the_mesh_and_undo_are_as_they_were` guards against a `check` that pushed a version: the store's version moves only with a step, and undo would take that step back. A client cannot see the version, so the test reads it through undo; this break was not run.
