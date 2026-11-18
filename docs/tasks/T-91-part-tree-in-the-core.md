---
description: >-
  A model is a named tree of parts with transforms, envelopes, instances and kept levels, placed in world space for drawing, with the sample rover worked in Core
---
# T-91 — Part tree in the Core

**Status:** In Progress

**Pri:** P1

**Milestone:** M-9

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** core-unit

**Perf:** skip: Carve declares no heavy suites

## Goal

`Carve.Core` holds a model as a named tree of parts (D-2), as the person ruled at M-9's planning (2026-11-17), so every later M-9 task builds on one engine-free API. The sample rover is built and checked here first, so every number a later frame or export check uses is worked out before any frame exists.

## Scope

- **`Part`** (Core, immutable): `Name`, `Parent` (a part name or null for top-level), `Translation` (`Vector3`, metres), `Rotation` (unit quaternion, the `define_skeleton` rule: within 0.001 of length 1, kept as given), `Scale` (`Vector3`, each component > 0, default 1,1,1), `Envelope` (optional min/max box in the part's **own local space**), `InstanceOf` (optional part name), `Moves` (bool, default false), and `Levels` (kept levels: level number to a full `PolygonMesh` snapshot, ascending).
- **What a part is:** it owns geometry (its live mesh, a `NamedMesh` of the same name, as today), has none (an *empty*, for grouping), or is `InstanceOf` a part that owns geometry and shows that part's geometry and levels. A part never both owns geometry and is an instance. Names are unique across the whole model (one flat namespace, the existing `Model.NameProblem` rule). The parent chain has no cycles, and every parent exists.
- **Every named mesh is a part.** A mesh made by `Model.With` with no part record yet is a top-level part at the origin: identity rotation, scale 1, no envelope, no levels, static. A model whose parts are all like that is exactly today's model, and every existing API (`Meshes`, `Find`, `Target`, `Piece`, `WithMeshFrom`, `OffsetOf`, `Combined`) gives exactly what it gives today.
- **Model API, additive:** `Parts` (pre-order: a parent before its children, siblings in definition order); `FindPart`; `WithPart(Part)` (create or replace, with refusals that name the field, e.g. `parent: no part named 'Axel_F'; the parts are ...`, `rotation: not a unit quaternion`, `scale: each component must be > 0 (a mirroring scale is refused: Empyrean's split load refuses a mirrored node)`, `instance_of: 'Axle_F' has no geometry`, `instance_of: an instance cannot also own geometry`, `parent: 'Wheel_FL' would be its own ancestor`); `WithLevel(name, level)` (snapshot the live mesh as level N; N must be ≥ the part's newest kept level, and an equal N replaces it; refused on an instance or an empty, naming them); `WorldTransform(name)` (the matrix from the part's space to the model's); `LevelMesh(name, n)` (the part's newest kept level ≤ N, or null); `Model.Create` gaining parts, with every problem named by where it is (`parts[3].parent`).
- **`Target` refuses an instance or an empty**, so every edit tool does: `mesh: 'Wheel_FR' is an instance of 'Wheel_FL'; edit 'Wheel_FL', and every instance follows`, and `mesh: 'Axle_F' is an empty part with no geometry; define_mesh mesh=Axle_F gives it some`.
- **`Combined` is world-placed.** It joins every drawn occurrence: each part with geometry at its world transform, and each instance as a copy of its source at the instance's world transform, with colours, materials, UVs, seams and shading carried as today. `CombinedAt(level)` does the same for `LevelMesh(n)` (a part with no level ≤ N is not drawn). `OffsetOf(name)` gives where the named part's **own** occurrence starts. A model of untransformed top-level parts gives the same `Combined` as today, bit for bit. Normals of a smooth mesh under a non-uniform scale transform by the inverse transpose.
- **Envelope warnings (Core, read by `check` and `get_parts` in T-93):** a part's live geometry (all live vertices, in its local space) leaves its own envelope; a child's envelope, placed in its parent's space (the axis-aligned box of the transformed box), leaves its parent's envelope. Both only where both envelopes exist. On the bound counts as inside (M-4's rule). Each warning names the part, the axis and side, and by how much in metres.
- **The rover (content):** `RoverSpec` (Core-side, in `tests/Carve.Core.Tests`, written so the T-96 scenario can link the same file) builds the agreed rover from `Model` calls: the part tree, both levels, the colours, the turned-mast and instance-edit stages. Every number is in the milestone's *Decided at planning* sketch, and RoverSpec is the one place they are typed.
- **`RoverGeometry` tests (`unit`), before anything else:** per part and level, vertex, face and triangle counts; world bounds of each part and of the whole at level 0, level 1 and live; that the wheels clear the chassis sides (inner faces at |z| 0.62 against the chassis side at 0.6) and that no two faces of different parts are coplanar and overlapping; that each instance's world bounds are the source's moved, with the 180° wheels' hub boss on the outside (−Z for `Wheel_FR` and `Wheel_RR`); that the turned mast puts the lamp at −Z; that there are no envelope warnings on the rover, and exactly the expected warning when `Axle_F` is lowered 0.3 m (`Axle_F`'s placed envelope then reaches y −0.35, below `Chassis`'s −0.05, so the one warning names `Axle_F`, axis y, below, by 0.3 m); and that `Combined` at level 1 draws the Mast at its level 0. Record the counts and bounds in As built for T-94, T-95 and T-96.
- **Other tests (`unit`):** every refusal above; pre-order with siblings in definition order; `WorldTransform` through three levels with rotation and non-uniform scale; levels kept, replaced and refused; an instance edit seen in every instance's occurrence in `Combined`; the identity guard: every `TestMeshes` model and every sample's model gives the same `Combined` (by content) as before this task.

## Notes

- **Design-heavy: it sets the public Core API** that T-92 to T-97 build on. Opus, and plan for two agents (the M-16 lesson): the API, refusals and identity guard first, the rover and `RoverGeometry` second.
- **Bound by the person's rulings at M-9's planning (2026-11-17)**, all in the milestone file: every mesh is a part; a flat namespace with `InstanceOf`; a level is a full snapshot with an explicit number and the newest-≤-N rule; transforms as translation, unit quaternion and positive per-axis scale; envelopes in local space, warnings only; the rover as sketched with the wheels at z ±0.72, the Lamp refined at level 1 and the Mast and Chassis kept at level 0 only.
- **Chassis envelope:** the sketch's illustrative box around the chassis geometry alone contradicts the agreed rule that a child's placed envelope must sit inside its parent's. RoverSpec gives each envelope around its whole subtree: `Chassis` [-1.2, -0.05, -0.95] to [1.2, 1.75, 0.95], and the others as the milestone lists them. If a number does not hold under `RoverGeometry`, stop and ask; do not move a part to make a check pass.
- **Write down for dependents (As built):** the `Part` and `Model` API as landed, `Combined`'s occurrence order, and the rover's counts and bounds per level.
- **Open rules: stop and ask.** Anything the rulings do not settle, including: what `Combined`'s order of occurrences is, if keeping today's order for untransformed models forces something odd; whether an instance's own envelope is allowed (recommendation: yes, checked against its source's geometry); and any change to an existing API's result.
- **Carried from earlier plans:** the identity guard by content on every existing model; each new check shown failing on a broken product, as a table (break → the check that fails); evidence at the absolute path the dispatch names, never in the worktree's `artifacts/`; run `finish` inside the worktree, read its output whole and give the `Merge task/T-91` line; report a `verify` that waits more than a few minutes.

### As built

First agent (Opus 5.5), 2026-11-18: the Core API, refusals, world-placed drawing, envelope warnings and the identity guard. `RoverSpec` and `RoverGeometry` are the second agent's.

- **`Part`** (`src/Carve.Core/Part.cs`), an immutable record `Part(Name)` with `init` properties `Parent`, `Translation`, `Rotation` (default identity, kept as given), `Scale` (default 1,1,1), `Envelope` (`PartEnvelope(Min, Max)`, local space), `InstanceOf`, `Moves`, `Levels` (`IReadOnlyList<PartLevel(Number, Mesh)>`, ascending). Helpers: `Part.Of(name)` (a plain mesh's part), `IsPlain`, `HasTransform`, `LocalTransform` (scale, then the rotation normalised, then translation; row vectors as System.Numerics), `LevelAt(n)`, `NewestLevel`, and `Part.Problems(part)` (its own fields: name, translation, rotation, scale, envelope, levels).
- **`Model`**, additive: `Parts` (pre-order), `FindPart`, `ListPartNames`, `PartProblems(part)`, `WithPart(part)` (throws `field: problem`) and `WithPart(part, out problem)`, `WithLevel(name, n)` and `WithLevel(name, n, out problem)`, `WorldTransform(name)` (`Matrix4x4?`, part space to model space, `Vector3.Transform(p, m)`), `LevelMesh(name, n)` (an instance gives its source's), `CombinedAt(n)`, `EnvelopeWarnings()` (`EnvelopeWarning(Part, Parent, Axis, Above, By)` with `Message`), and `Create(meshes, skeleton, clips, materials, parts)`.
- **`WithPart` replaces the whole record, `Levels` included.** T-93's `define_part` should start from `FindPart(name)` and change only the given fields, or it drops the kept levels.
- **`Create`'s part order:** the given records in order (the first of each name), then each mesh with no record, in mesh order, as `Part.Of`. T-92 should write every part's record whenever it writes `parts`, so the order survives a round trip. Problems are listed part by part, each part's own fields before how it sits in the model.
- **Refusals added to existing calls:** `Target` refuses an instance or an empty with the spec's messages. `With(name, mesh)` throws on an instance's name. `WithMeshFrom` returns its problem for an instance. `With` on an empty gives it geometry and keeps its placement.
- **`Combined`'s occurrence order:** `Parts` in pre-order. Each part with geometry is drawn once at its world transform, and each instance as a copy of its source at the instance's transform. An empty draws nothing. `OffsetOf(name)` gives where the named part's own occurrence starts (an instance has one too; an empty gives null). One occurrence at the identity gives that mesh itself, as before. For a model of plain parts the pre-order is the definition order, so `Combined` is today's.
- **Normals and targets under a transform:** a transformed occurrence of a smooth mesh carries its corner normals (`Shading.CornersOf`) by the inverse transpose, normalised, through the posed-corners path. Flat faces take their normals from the placed positions. Morph deltas turn and scale by the linear part. Tombstoned vertices are not moved.
- **`Skinning.Posed` and `Skinning.Morphed`** keep the source's part records (`Model.WithPartsFrom`, internal), so a posed copy is placed as its source is.
- **Envelope warnings:** parts in pre-order, own geometry (live vertices; an instance's is its source's) first, then the placed envelope against the parent's, axes x, y, z, below before above. They compare exactly in float, with on the bound counting as inside, and give `By` in float (so a 0.3 m drop reads as about 0.3; compare it within a tolerance).
- **`Piece`** still gives a plain part. Carrying levels in a piece is T-92's.
- **Identity guard:** `CombinedIdentityGuardTests` holds the SHA-256 of every combined mesh's full content (positions and tombstones to the bit, loops, holes, shading, drawn triangles with their normals, looks, UVs, seams, targets, weights). The digests were taken on the code before T-91 (commit `dded586`, before any `Model` change) for each `TestMeshes` mesh, all of them in one model, and the 8 samples. Evidence is in `G:\My Drive\artifacts\carve\M-9\T-91\`: `t91-combined-digests.txt`, and `break-table.tsv`, where each of 14 breaks fails at least one check.

### Hand-off

From the first agent (Opus 5.5) to the second.

- **Done and committed:** everything in *As built*, with `PartTreeTests` (16 tests) and the identity guard. `cairn task T-91 verify` passes.
- **Left:** `RoverSpec` (in `tests/Carve.Core.Tests`, linkable by T-96) and the `RoverGeometry` tests from Scope; the rover's counts and bounds per level in *As built*; the break rows for the rover checks; then commit, `cairn lint`, `finish`.
- **Checked:** the break table (14 breaks, each caught); the whole Core suite green. **Not checked:** any server or tool use of the API (T-93), and any frames (none in this phase).
- **Open question for the person:** whether an instance may have its own envelope. The task asks for a ruling, and none was given. It is built as the task recommends: allowed, and checked against the source's geometry (one test in `Envelope_warnings_...` pins it). The rover gives no instance an envelope, so the rover does not depend on the answer.
