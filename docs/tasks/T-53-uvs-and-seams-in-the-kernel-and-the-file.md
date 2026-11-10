---
description: >-
  A mesh holds UVs per face corner and marked seams, carried through every edit, undo and the Carve file
---
# T-53 — UVs and seams in the kernel and the file

**Status:** Open

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
