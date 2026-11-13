---
description: >-
  A mesh holds UVs per face corner and marked seams, carried through every edit, undo and the Carve file
---
# T-53 — UVs and seams in the kernel and the file

**Status:** In Progress

**Pri:** P1

**Milestone:** M-6

**Batch:** — (standalone)

**Depends on:** T-42, T-43

**Verify:** core-unit, server-unit, server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

The kernel holds a mesh's UVs and seams, keeps them through every edit and undo, and writes and reads them in the Carve file. Every later M-6 task builds on this one model of what a UV is.

## Scope

- **Core type** (`src/Carve.Core/FaceUvs.cs`; the name is free): UVs per **face corner**, mapping a face id to one `Vector2` per corner in the face's loop order.
  - A face may have none ("unmapped"), so a mesh may have UVs on some faces only.
  - Coordinates follow glTF: (0,0) is the texture's top-left, u runs right and v runs down. Any finite value is legal; values outside 0..1 tile.
  - Immutable, and held on `PolygonMesh` the way `Style` and `Weights` are (`Uvs`, `WithUvs`), null when empty.
- **Seams:** a marked set of undirected edges, as vertex pairs `(min, max)`, held on the mesh beside T-42's hard edges and stored the same way.
  - A seam is only an input to `unwrap` (T-56) and a line in the layout look (T-58).
  - A split in the texture needs no seam: wherever an edge's two faces give its ends different UVs, the UVs are split there anyway.
- **Carried through every edit** (in `EditResult`, and wherever T-42 and T-43 carry hard edges):
  - `move` keeps every face's UVs, so the checker shows the stretch.
  - `extrude`: a face that keeps its id (the cap) keeps its corner UVs in loop order. The new wall faces are unmapped.
  - `subdivide` (T-43): new corners take UVs **linearly** from their own face. An edge midpoint's corner is the mean of that face's two corner UVs, and the face centre's is the mean of all its corners. A face with no UVs gives children with none.
  - Seams follow the rule T-42 and T-43 set for hard edges: an edge split by subdivide keeps the mark on both halves, and a pair that stops being an edge is handled as a hard edge is. If T-42 or T-43 left that rule unsettled, stop and ask.
  - Colours, groups, weights, hard edges and shading are untouched by UV changes, and UVs by changes to them.
  - Undo restores UVs and seams with the rest of the mesh.
- **The Carve file:** the next version after T-42's (version 4 if T-42 left it at 3) adds two keys per mesh, each left out when empty:
  - `"uvs": [{"face": f, "uv": [[u, v], ...]}, ...]`, in face-id order;
  - `"seams": [[a, b], ...]`, sorted by `a`, then `b`.

  Floats are written and read bit-exact, as vertices are. Every older version still reads, with no UVs and no seams. A file with no UVs or seams writes as before apart from its version. Errors are named by element, as the file's other errors are: `meshes[i].uvs[k].uv[c]`; a corner count that does not match the face; a face id out of range or repeated; a non-finite value; a seam pair that is not an edge of the mesh.
- **Tests** (`unit`, in `tests/Carve.Core.Tests`):
  - a round trip of a cube with UVs on some faces and two seams, bit-exact;
  - older fixtures read unchanged;
  - move, extrude (cap kept, walls unmapped), subdivide (linear values worked by hand on a unit quad) and undo each keep the rules above;
  - each file error, named.

  `TestMeshes` gains `Barrel()`: the barrel below, with no UVs and no seams.

## Notes

- **Design-heavy:** it decides a file format and the type every M-6 task uses, so Opus. Plan for two agents: every Opus member of M-16 handed off at least once.
- **The barrel** (decided at planning, and checked through `carve` on 2026-11-09: 40 vertices, 34 faces, 72 edges, closed, no warnings, `check` ok, volume 0.4398 m³):
  - eight sides and five rings, at y = 0, 0.1, 0.5, 0.9 and 1.0 with radii 0.36, 0.38, 0.42, 0.38 and 0.36;
  - ring `i`'s vertex `k` (id `8i + k`) is at angle `a = 2πk/8`, placed at `(r cos a, y, -r sin a)` rounded to 6 decimals;
  - side faces are `[8i+k, 8i+(k+1)%8, 8(i+1)+(k+1)%8, 8(i+1)+k]` for i = 0..3 and k = 0..7 (ids 0 to 31);
  - the bottom cap is `[7, 6, 5, 4, 3, 2, 1, 0]` (id 32) and the top cap `[32, 33, ..., 39]` (id 33).
- **Write down for the dependants**, in this file's As built: the type's name and API, the file version and keys, and how an edit reports UVs, if it does.
- **Out of scope here:** the tools (T-54), projections (T-55), unwrap (T-56), render (T-57, T-58), export (T-59) and check (T-60).
- **Before the first code**, read this Scope against what M-18 actually landed (`Shading.cs`, `mark_edges`, the file's version 3 keys, subdivide's carrying rules). Stop if they disagree.
- **Carried from earlier plans:**
  - the byte-identical guard on every existing frame and committed file, before and after (by hash, or by content as M-16 ruled);
  - each new check shown failing on a broken product, as a table;
  - read `finish`'s output whole and give its `Merge task/T-NN` line;
  - fetch any queued artifacts by job id;
  - report a `verify` that waits more than a few minutes;
  - keep evidence in the milestone worktree's `artifacts/review/T-53/`, at the absolute path the brief gives.
- **Open rules: stop and ask.** Anything the milestone's *Decided at planning* does not settle, any loosened check, and any change to a D-1 or D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### Plan

Written 2026-11-13 by the research agent (Opus 5.5) against `milestone/M-6` at `72049e7`, after M-5 (T-63..T-70) had landed. Nothing here is code yet. The points marked *(open rule n)* wait on the person; build with the recommended option only once it has been confirmed.

**What the code holds now, read against the Scope:**

- **File versions.** `CarveFile.Version` is 4 (T-63: holed faces as `{"loop", "holes"}`, `null` tombstones in `vertices` and `faces`). `Write` writes the lowest version that holds the model (the person's ruling, 2026-11-11): 4 for a holed face or a tombstone, else 3 for a mesh with a `Shade`, else 2. All of these versions are committed: `samples/gardener.carve` and `leaky-shed.carve` are v2, `gardener-soft.carve` v3, `gatehouse.carve` v4, and `tests/Carve.Core.Tests/Fixtures/leaky-shed-v1.carve` v1. So UVs take **version 5**, not 4 *(open rule 1)*.
- **"Writes as before apart from its version"** clashes with the lowest-version ruling. Under that ruling, a model with no UVs and no seams writes exactly as before, *including* its version (2, 3 or 4). Only a model with UVs or seams on some mesh is written as 5 *(open rule 1)*.
- **Corners.** A face is `Face` (outer `Loop` plus `Holes`). Its corners are `Face.Corners`: the outer loop, then each hole in order. Corner normals (`Shading.Corners`) and `MeshTopology.FaceEdges` already use this order, and a face never repeats a vertex across its loops (`Face.CornerOf`). So "one `Vector2` per corner in loop order" means one per `Face.Corners` entry, hole corners included *(open rule 2)*. A tombstone (`Face.Deleted`) has no corners and never has UVs.
- **Hard edges.** They live in `MeshShade` (`Shading.cs`): mode plus a sorted `(Lo, Hi)[]`, with `IsHard`, `WithEdges(edges, hard)` and `Of`. `PolygonMesh.Shade` holds it, null when flat with no mark, and `PolygonMesh.HardEdges` exposes it. `Shading.MarkEdges` refuses a pair that is not an edge. The file writes `shading` and `hard_edges` (always present, `[]` when empty) as the last two mesh keys of v3+.
- **The rule for an edge that stops being an edge is settled.** T-42 As built says "stale marks are dropped", and `EditResult.Carried` drops every hard pair the edited topology no longer has. `Subdivision.Once` drops a hard pair it cannot find and carries a mark to both halves. `FaceSplit.Carry` gives the pieces of a split hard edge the mark and leaves the segments' new edges soft. `FaceMerge` lets a mark on a dissolved edge go with the edge. `PlaneCut` adds one exception: on a smooth mesh the caps' border edges become hard. Seams can mirror all of this except that exception *(open rule 4)*.
- **Where attributes live and how they carry.**
  - `PolygonMesh` has one private constructor `(vertices, faces, style, weights, shade, corners, flatFaces, deletedVertices)`. `WithStyle`, `WithWeights`, `WithShade` and `WithVertices` each pass the others through, and empty normalises to null.
  - `PolygonMesh.Edit` returns a bare mesh, and each caller carries the attributes onto it. There are three carry sites:
    1. `EditResult.Carried`, used through `EditResult.From` by move, extrude, delete, merge_faces, bridge and plane_cut's delete/cap stage;
    2. `FaceSplit.Carry`, used by plane_cut's split stage, loop_cut and knife;
    3. `Subdivision.Level`/`Once`, used by subdivide, which assembles its own result with `EditResult.Made`.
  - Undo (`Carve.Server/ModelStore`) keeps whole `Model` snapshots, so anything on `PolygonMesh` is undone with no extra code.
- **Edits that exist now:** `MeshEdits.Move`, `Extrude` (now with `inset` and holed faces; walls run over all corners, holes included), `Subdivide` (refuses holed faces), `MeshDelete.Delete`, `PlaneCut.Cut`, `LoopCut.Cut`, `Knife.Cut`, `FaceMerge.Merge` and `Bridge.Join`. There is no weld or mirror.
- **The milestone has a precedent for M-5's cuts.** M-5's "What carries" says "UVs blend the same way [as weights] if M-6 has landed; how seams carry goes back to the person". That is the basis for open rule 3.

**Files to add or change:**

1. **`src/Carve.Core/MeshUvs.cs` (new).** The Scope leaves the name free, and *(open rule 5)* covers whether seams go in this type. It holds a sealed, immutable `MeshUvs` with:
   - `static MeshUvs None`, `bool IsEmpty` (no mapped face and no seam);
   - `IReadOnlyList<int> MappedFaces`: face ids ascending;
   - `IReadOnlyList<Vector2>? Of(int face)`: the face's corner UVs in `Face.Corners` order, or null when unmapped. `bool IsMapped(int face)`;
   - `IReadOnlyList<(int Lo, int Hi)> Seams`: sorted, lower id first. `bool IsSeam(int a, int b)`;
   - `MeshUvs WithFace(int face, IReadOnlyList<Vector2>? corners)`: null unmaps. `MeshUvs WithFaces(IEnumerable<KeyValuePair<int, IReadOnlyList<Vector2>?>>)`;
   - `MeshUvs WithSeams(IEnumerable<(int A, int B)> edges, bool seam)`, as `MeshShade.WithEdges`;
   - `static IReadOnlyList<MeshError> Check(PolygonMesh mesh, MeshUvs uvs, string at)`, the one validator shared by the file reader and T-54's tools. It checks: the face is live and in range; corner count = `Face.Corners.Count`; every value is finite; every seam is an edge of the mesh.
   - `internal MeshUvs Without(IEnumerable<int> faces)` for tombstones, and `internal MeshUvs WithoutSeams(IEnumerable<(int, int)>)`.
   - Storage is a `SortedDictionary<int, Vector2[]>` (copied in, never exposed mutable) plus a sorted `(int, int)[]`. Uses `System.Numerics.Vector2`. glTF axes are stated in the doc comment.
2. **`src/Carve.Core/PolygonMesh.cs`.**
   - The constructor gains `MeshUvs? uvs`, normalised to null when `IsEmpty`.
   - Add `public MeshUvs? Uvs`, `public PolygonMesh WithUvs(MeshUvs? uvs)` (unchecked, as `WithStyle` is; `MeshUvs.Check` is the guard) and `public IReadOnlyList<(int Lo, int Hi)> Seams => Uvs?.Seams ?? []`.
   - `WithStyle`, `WithWeights`, `WithShade` and `WithVertices` pass `Uvs` through. A posed copy keeps them, as it keeps `Style` *(open rule 7)*. `Assemble` and `Edit` give none.
3. **`src/Carve.Core/EditResult.cs` (`Carried`).** After weights and shading:
   - Each face id `< source.Faces.Count` that is mapped in the source and still live in the edited mesh **with the same corner count** keeps its UVs by position. That covers move (identical faces), extrude's cap (new ring in the same corner order) and every face an edit leaves alone.
   - A face whose corner count changed, a tombstoned face, and every appended face end up unmapped.
   - Seams carry, and a seam pair the edited topology no longer has is dropped, exactly as the hard-edge line there does.
   - Add an optional `MeshUvs? uvs` override parameter to `From`/`Carried` for an edit that works out its own UVs (merge_faces, open rule 3c).
4. **`src/Carve.Core/FaceSplit.cs` (`Carry`).**
   - Every face id in `reshaped`, and every piece in `made`, takes UVs from its source face `f`, if `f` was mapped. A kept corner's vertex `v` takes `uv_f(v)` (looked up through `Face.CornerOf`). A new vertex `source.Vertices.Count + k` takes `stencils[k]` applied to `f`'s per-vertex UVs:
     - an edge vertex is `(1 - t)·uv_f(lo) + t·uv_f(hi)`, worked separately in each face on the edge, so a split in the texture stays split;
     - an interior knife vertex is the barycentric blend over `f`'s own triangle.
   - This also covers a neighbour that only gains a vertex on its edge: it keeps its id and gets the inserted corner interpolated.
   - Add to `Subdivision.Stencil` an `Apply(Func<int, Vector2>)` (or an `ApplyUv(IReadOnlyDictionary<int, Vector2>)`), summed in double and rounded once to float.
   - Seams follow the `chains` loop exactly as hard edges do: the pieces of a split seam stay seams, and the segments' edges are not seams *(open rule 4)*.
5. **`src/Carve.Core/Subdivision.cs`.**
   - `Level` gains `MeshUvs? Uvs`. Child `k` of a mapped face `f` with corner UVs `u0..un-1` is `[u_k, (u_k + u_k+1)/2, mean(u), (u_k-1 + u_k)/2]`, in double and rounded once. An unmapped face gives unmapped children.
   - Seams go to both halves through the same `edgeIndex` lookup as hard edges, and a pair that is not an edge is dropped.
   - A seam does **not** make an edge sharp: subdivision positions are unchanged by UVs.
   - `MeshEdits.Subdivide` passes `mesh.Uvs` in and `.WithUvs(level.Uvs)` out.
6. **`src/Carve.Core/PlaneCut.cs`.** No UV code beyond what the two stages already carry. The split goes through `FaceSplit.Carry`, and the delete and caps through `Carried`, so caps come out unmapped and deleted faces drop their UVs. A cap-border seam is added only if open rule 4 says so (beside the hard-border line at `PlaneCut.cs:130`).
7. **`src/Carve.Core/FaceMerge.cs`.** Applies open rule 3c through the override and adds its warning beside the colour warnings. `src/Carve.Core/Bridge.cs` and `MeshDelete.cs` need no change: `Carried` already leaves bridge quads unmapped and drops tombstoned faces' UVs.
8. **`src/Carve.Core/CarveFile.cs`.**
   - Set `Version = 5` and `ReadableVersions = [1, 2, 3, 4, 5]`, let the version dispatch at line ~293 accept 5, and add `MeshKeysV5 = [.. MeshKeysV3, "uvs", "seams"]`. In v ≤ 4 these keys stay unknown and are refused, as now.
   - **Write:**
     - Version 5 iff any mesh has `Uvs`, else the existing rule, byte for byte.
     - In a v5 file each mesh writes `shading` and `hard_edges` as v3/v4 do, then `"uvs"` and `"seams"`, each **left out when empty**. `hard_edges`' trailer becomes `,` when one follows.
     - `uvs` has one face per line, `{"face": 3, "uv": [[0, 0], [1, 0], [1, 1], [0, 1]]}`, in face-id order, each float through the existing shortest-round-trip `Number`. `seams` has one `[a, b]` per line, as `hard_edges`.
     - Faces keep v4's form (plain array, holed object, `null`).
     - Update the class remarks with a "Version 5 (T-53)" paragraph.
   - **Read** (new `ReadUvs`, beside `ReadShade` and called after it, only for version ≥ 5; the mesh is assembled with `.WithUvs(...)` at line ~535). Errors are named by element:
     - `meshes[i].uvs` not a list;
     - `meshes[i].uvs[k]` not an object, or with unknown or missing keys (`face`, `uv`);
     - `meshes[i].uvs[k].face` out of range, a tombstone (`deleted`), or listed earlier;
     - `meshes[i].uvs[k].uv` the wrong corner count: "face 3 has 8 corners (its loop 4, holes 4); write 8 UVs";
     - `meshes[i].uvs[k].uv[c]` not a pair of numbers, or a number that is not finite as a float (e.g. `1e39`);
     - `meshes[i].seams[k]` with the same three errors as `hard_edges[k]` (not a pair, not an edge of the mesh, listed earlier).
   - Read order: `uvs` entries in any order (written ascending); seams in any order.
9. **`tests/Carve.Core.Tests/TestMeshes.cs`:** `Barrel()` as the Notes define it, with no UVs and no seams. One test pins 40 vertices, 34 faces, 72 edges, closed, and the volume 0.4398 to 4 places.
10. **Server.** No change is needed for this task. `save` and `open` already go through `CarveFile`, and undo through snapshots. `get_mesh` and `mark_edges(..., seam)` are T-54's. `Model.Combined` (render) dropping UVs is left to T-57 *(open rule 7)*. If `CarveFile.Version` is asserted anywhere in `Carve.Server` or its tests, update it.

**How an edit reports UVs:** it does not. No new `EditResult` field, since `NewFaces`, `Split` and `Extruded` already say which faces are new. Record this in As built for T-54.

**Tests** (`unit`, `tests/Carve.Core.Tests`; new files `MeshUvsTests.cs`, `UvFileTests.cs`, `UvCarryTests.cs`):

- `MeshUvs`: empty normalises to null on the mesh; `WithFace`, `Of` and `IsMapped`; seams sorted, deduplicated and order-free; `Check` names each fault.
- **Independence:** `WithStyle`, `WithWeights` and `WithShade` (and `Shading.MarkEdges`) keep `Uvs` reference-equal, and `WithUvs` keeps `Style`, `Weights` and `Shade` reference-equal.
- **File:**
  - a round trip of a cube with UVs on faces 0, 2 and 5 (one value outside 0..1, one negative, one subnormal) and two seams, with text equal on re-write and every float bit-equal (`BitConverter.SingleToInt32Bits`);
  - the written text pinned: version 5, key order, one item per line, `uvs`/`seams` left out on a second mesh with none;
  - a holed face's UVs (8 corners) round trip;
  - a v5 mesh with UVs beside tombstones round trips.
- **Older files:** each committed `.carve` (the v1 fixture and the v2, v3 and v4 samples) reads with `Uvs` null, and the v2, v3 and v4 samples re-write byte-identical to their committed text. A model with no UVs still writes 2, 3 or 4 as before. `uvs` in a v4 file is refused as an unknown key.
- **File errors,** each named exactly: corner count, face out of range, face a tombstone, face repeated, non-finite (`1e39`), not a pair, seam not an edge, seam repeated, unknown key in a `uvs` entry.
- **Carry:**
  - `move` keeps all UVs bit-exact;
  - `extrude` (with and without inset, and a holed face) keeps the cap's UVs by corner and leaves the walls unmapped;
  - `subdivide` on a unit quad worked by hand, `[(0,0),(1,0),(1,1),(0,1)]`: child 0 is `[(0,0),(0.5,0),(0.5,0.5),(0,0.5)]` and children 1..3 likewise. An unmapped face gives unmapped children. Two levels equal one level twice;
  - seams: subdivide puts a seam on both halves; `delete` drops UVs of tombstoned faces and seams no live face has;
  - the cuts, per open rule 3:
    - `plane_cut` `both`: pieces interpolated, and a neighbour's inserted corner interpolated on its own side of a texture split;
    - `plane_cut` `above`: caps unmapped;
    - `loop_cut` on a mapped ring at `t = 0.25`: exact values;
    - `knife` with an interior point: barycentric value;
    - `merge_faces` with continuous and with discontinuous UVs;
    - `bridge` quads unmapped;
  - every edit's result passes `MeshUvs.Check` (one theory over all edits on a mapped, seamed cube);
  - **undo:** a `ModelStore` push of a mapped mesh, an edit, then `Undo`, gives back the same `Uvs` instance. This one is a server-unit test, in `tests/Carve.Server.Tests` beside the existing undo tests.
- **Broken-product table:** each new check shown failing on a planted break, recorded in As built. The breaks: drop `Uvs` in `WithShade`; write `uv` in the wrong order; skip the hole corners; forget the cap; use subdivide's smoothed stencil for UVs; leave the seam on only one half.

**Frame and file guard** (T-53 adds no frame and changes no committed file):

1. Before the first code, at `72049e7`, run `cairn task T-53 verify` from `D:\Repos\carve\branches\task\T-53`. Save its frames' `all.sha256` (path-relative, as T-68 did) to `D:\Repos\carve\branches\milestone\M-6\artifacts\review\T-53\frames-before\all.sha256`, and the SHA-256 of every `git ls-files '*.carve'` file to `...\T-53\carve-before.sha256`.
2. At the hand-off commit, run the same again into `frames-after\` and `carve-after.sha256`. Compare by path: every frame byte-identical, none missing, none new; every `.carve` hash unchanged; `git diff --stat 72049e7 -- samples tests/Carve.Core.Tests/Fixtures` empty.
3. Give the job ids and the counts in As built.

### Open rules for the person

1. **The file version and when it is written.** *Recommended:* version 5, written only when some mesh has UVs or seams. Every other model writes 2, 3 or 4 exactly as now, which keeps the 2026-11-11 lowest-version ruling and reads the Scope's "as before apart from its version" as "as before". *Alternatives:* (a) always write 5 from now on, which breaks the lowest-version ruling and every byte-for-byte test of v2, v3 and v4; (b) renumber, so that UVs are 4 and holes 5, which would break `gatehouse.carve` and is not sensible.
2. **Corners of a face with holes.** *Recommended:* one UV per `Face.Corners` entry (the outer loop, then each hole), as corner normals already are, so a holed face's `uv` has `Loop.Count + Σ hole counts` entries. A tombstone can never be mapped, and the file refuses `face` naming one. *Alternatives:* (a) the outer loop only, with hole corners getting no UV, which leaves them undrawable in a checker; (b) a nested `uv` per loop (`{"loop": [...], "holes": [[...]]}`), which mirrors the face form but complicates every consumer.
3. **What M-5's cuts do with UVs.** *Recommended* on each, following the spirit that pieces of an old face keep its mapping and faces made from nothing are unmapped:
   - a. **plane_cut (split), loop_cut and knife** (all through `FaceSplit`): each piece and each neighbour gaining a vertex take UVs from their source face. An edge vertex is linear by its fraction, worked per face. An interior knife point is barycentric in the face's triangle, the same stencil the weights use (M-5 already says "UVs blend the same way"). *Alternative:* split pieces unmapped, which is simpler and loses mapping on every cut.
   - b. **plane_cut caps** (`fill`): unmapped, like extrude's walls. *Alternative:* a planar projection onto the cut plane, which puts a projection into the kernel ahead of T-55.
   - c. **merge_faces:** the survivor keeps a mapping when every listed face is mapped and every outline vertex has the same UV (bit-equal) in every listed face that uses it. Otherwise it is unmapped, with a warning `faces[n]: its UVs ... are dropped` beside the colour warnings. *Alternatives:* (i) always unmapped; (ii) take each outline vertex's UV from the lowest listed face that has it, even across a texture split, with no warning.
   - d. **bridge:** the new quads are unmapped, like extrude's walls. *Alternative:* none worth taking.
   - e. **delete** (and plane_cut's `keep` side): a tombstoned face loses its UVs, as it loses its colour. This is not really open, since it mirrors `FaceStyle.Without`.
4. **How seams carry through the cuts** (M-5 sent this back to the person). *Recommended:* exactly as hard edges, minus the fill exception. Both pieces of a split seam stay seams; the segments' new edges are not seams; a seam on an edge that stops being an edge is dropped (settled for hard edges by T-42); a merge's dissolved seam goes with the edge; the plane-cut cap border is **not** seamed. *Alternative:* seam the cap border (on any mesh, not only smooth), since unwrap would want a cut there. But a cap borders open UV space anyway, as it is unmapped, and seams stay the agent's choice.
5. **Where seams live.** *Recommended:* in the same `MeshUvs` object as the face UVs (`PolygonMesh.Uvs`, with `Seams` as a convenience, as `HardEdges` is over `Shade`). Both are UV data, edited by the same T-54/T-56 tools, and untouched by shading. *Alternative:* a separate `EdgeSet`-style type held on its own (`PolygonMesh.SeamEdges`, `WithSeams`), which adds a second constructor parameter and a second carry line at each site.
6. **Interpolation precision.** *Recommended:* sum in double and round once to float, as positions and the `Stencil` already do, so values such as 0.5 and 0.25 are exact. *Alternative:* float arithmetic, which is not bit-stable across a re-order.
7. **Copies that are not edits.** *Recommended:* a posed copy (`WithVertices`, skinning) keeps `Uvs`, as it keeps `Style`, and `Model.Combined` (render) carrying UVs offset by face id is left to T-57, which first needs it. *Alternative:* add `Combined`'s UV carrying here so that T-57 only draws.

### Rulings (the person, 2026-11-13)

All seven as recommended:

1. Version 5, written only when some mesh has UVs or seams. Every other model writes 2, 3 or 4 exactly as now.
2. One UV per `Face.Corners` entry: the outer loop, then each hole. "A tombstone can never be mapped" means a deleted face's slot (`Face.Deleted`, no corners) holds no UVs while it is deleted, as it holds no colour or groups. `undo` restores the face with its UVs. The file refuses a `uvs` entry naming a tombstone.
3. The cuts: a, split pieces interpolated (linear by fraction on an edge, barycentric for an interior knife point); b, caps unmapped; c, merge_faces keeps UVs only when they agree bit-for-bit at every shared vertex, otherwise unmapped with a warning; d, bridge quads unmapped; e, deleted faces lose theirs.
4. Seams carry as hard edges do, and the plane-cut cap border is not seamed.
5. to 7. As recommended: seams in `MeshUvs`, double arithmetic rounded once, posed copies keep UVs, `Model.Combined` left to T-57. These three were taken without objection rather than put as questions.
