---
description: >-
  render draws every part where it is placed, isolates a subtree with part and draws a kept level with level, and get_mesh gives a part's placement
---
# T-94 — Render a part tree

**Status:** In Progress

**Pri:** P1

**Milestone:** M-9

**Batch:** — (standalone)

**Depends on:** T-93

**Verify:** server-unit, server-regression, scenario-cube, scenario-crate, scenario-shed, scenario-cuts, scenario-gatehouse, scenario-uvs, scenario-materials, scenario-character, scenario-soft, scenario-rig, scenario-export

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent sees a part tree as the game will: every part at its placement, instances included. It can see one subtree on its own and the model at a kept level, as the person ruled at M-9's planning (2026-11-17).

## Scope

- **`render`** draws `Model.Combined` (T-91, world-placed) by default, so a model of untransformed top-level parts draws as today, byte for byte.
- **`part`** draws only that part's subtree, at world placement, framed on that subtree's world bounds. This replaces the framing that M-7 found frames on every mesh when `mesh` names one, but only when `part` is given: `mesh`'s framing is unchanged, so existing frames hold. An instance's subtree draws its source's geometry.
- **`level`** draws `CombinedAt(level)`: each part at its newest kept level ≤ N, and a part with none is not drawn. With `part`, the two combine. The caption names both: `part Axle_F, level 0`.
- **`target`** and `direction` are in model space (the world placement). The text says so where it said "the mesh's own coordinates".
- **`labels` and `highlight` with `mesh`** show the named part's own ids on its own occurrence, not on its instances.
- **`get_mesh`** gains the part's `parent` and its world transform, and takes `level` (the newest kept level ≤ N, naming which level was given). Coordinates stay part-local.
- **Content: the rover's look in the server tests.** It is built from `RoverSpec` through the tools. Frames: rover front at live and at level 0; `part: Axle_F` from `[1, 0.3, 0]`, showing both hub bosses outward; and `part: Wheel_FL, labels: faces`. Each frame has pixel checks worked from the geometry with the shared rules named below, including one a mirrored or wrong-up camera fails, worked from the framing's bounds (the rover is asymmetric: the cab at +X, the mast at −X). Plan the frame review as its own agent. That agent opens every frame itself, cropped or scaled, before it asks the person.
- **Tests:** the frames above (`regression`, they need the device); in-process `unit` tests of the argument rules (`part` unknown, `level` negative, `part` with `mesh` naming a part outside the subtree), and of `get_mesh`'s new fields.

## Notes

- **Shared check rules, named at planning** (M-5 and M-6 lessons): the Crate's `ColourRule.cs` for a face's shade; the Cuts ray test (`CutsGeometry.cs`) run on `Combined`, which is already world-placed, so it needs no part logic; `SeenNarrow` for faces a few pixels wide (wheel rims, the mast post); `Drawn` for edges over the background; the gatehouse's 3D in-sight test for occlusion. A new rule goes to the person before it lands.
- **The guard:** every existing frame byte-identical by hash before and after, taken from queued jobs fetched by job id (not `cairn test artifacts <suite>`'s latest, the M-4 and M-16 trap).
- **Opus**, with the frame review planned as a second agent (M-6 and M-7 lessons).
- **File ownership:** `RenderTool.cs`, `RenderSystem.cs`, `ViewFraming.cs`, `LookMeshes.cs`, `GetMeshTool.cs`. M-19 also changed `RenderTool` and `RenderSystem` (`render_glb`). This task starts after M-19 has landed and builds on it.
- **Open rules: stop and ask.** Whether `part` with `views` frames every tile on the subtree (recommendation: yes); any change to an existing frame or caption; a pixel check that cannot pass as specified.
- **Write down for dependents (As built):** the captions and the shared rules as used, for T-96.
- **Carried:** each new check shown failing on a broken product, as a table; evidence at the absolute path the dispatch names; `finish` run inside the worktree, its output read whole and its `Merge task/T-94` line given; report a `verify` that waits more than a few minutes; submit the GPU suites early and keep working rather than hand off at "a real run still to come" (M-5).

### As built

First agent (Opus 5.5), 2026-11-18. The product half and its unit tests; the frames are not written yet (see Hand-off).

- **Core, by addition only** (`Model.cs`): `SubtreeOf(name)` (the part and every part under it, pre-order, itself first), `CombinedOf(part?, level?)` (`Combined` / `CombinedAt(n)` with no part; with one, only its subtree's occurrences, world-placed, live or at the level) and `OffsetOf(name, part?, level?)` (the named part's own occurrence within that draw).
- **render** takes `part` (string) and `level` (integer, last in the schema, after `weights`). `RenderSpec` carries `Part` and `Level`; `RenderPump` puts the selection in the scene again when either changes, as it does for a pose, and frames on what is drawn (with a pose, on the rest subtree). The whole-model path (no part, no level) is the code it was: `snapshot.Mesh`, the old offset and window rule (`Meshes.Count > 1`).
- **Caption:** `part <name>` then `level <n>` just before the counts, single and sheet alike: `512x512, view front, part Axle_F, level 0, 32 vertices, 20 faces`.
- **Refusals** (all `render failed with N problem(s)` lines): `part: there is no part named 'X'; the parts are ...` (checked before the engine is waited on, so no device is needed); `level: -1 is not a level; a kept level is 0 or more (keep_level)`; `level: not with pose|clip|targets|weights; a kept level is drawn as it was kept, at rest`; `mesh: 'Cab' is not drawn with part 'Axle_F'; the parts under it are ...`; `level: nothing is drawn at level N: ...`; `part: 'X' draws nothing: ...`; `mesh: 'X' has no kept level at or below N, so it is not drawn at level N`; in mode uvs, `part` and `level` join the refused list.
- **Labels and highlight with part:** aimed at `mesh`, or with mesh left out at the subtree's one part with geometry of its own (so `part: Wheel_FL, labels: faces` needs no mesh); the ids are that part's own (at a level, the level mesh's), windowed onto its own occurrence, never its instances.
- **Texts:** `RenderTool.PartsText` (public copy `RenderPartTexts.Parts`), `PartArgumentText`, `LevelArgumentText`; "in the mesh's own coordinates" / "own axes" now read model space. `GetMeshTool.PlacementText`.
- **get_mesh** adds `parent` (null at the top), `world:{translation, rotation [x,y,z,w], scale}` (`Matrix4x4.Decompose` of `WorldTransform`), and takes `level` (the newest kept level at or below it, reported as `level:{given, shown}`; refused below 0 or for a part with no such level). Coordinates stay part-local.
- **tools/list guard:** `PartToolsListTests` now also takes out T-94's two paragraphs and three new schema properties and puts the three reworded phrases back; the T-93 digest still matches, so nothing else in tools/list changed.
- **Tests:** `RenderPartArgumentTests` (unit, 8 cases, on `ToolHarness`); `RoverTools.BuildAsync` builds the rover (level 0 blockout, level 1 refined) through the tools on any client. `EditToolDefinitionTests` pins render's property list with `part`, `level` added.

### Hand-off

First agent (Opus 5.5), 2026-11-18, at the call-count checkpoint.

- **Done and checked:** the product code above; `RenderPartArgumentTests` passes, as do `PartToolsTests`, `PartToolsListTests` and `EditToolDefinitionTests` (70 cases). `verify` run at the end of this phase (result in the report).
- **Left:** the four regression frames (rover front live and level 0; `part: Axle_F` from `[1, 0.3, 0]`; `part: Wheel_FL, labels: faces`) with their pixel checks on the shared rules (ColourRule, the Cuts ray test on `Combined`, `SeenNarrow`, `Drawn`, the gatehouse in-sight test). `ColourRule.cs` and `CameraPixels.cs` link into `Carve.Server.Tests` cleanly; `CutsGeometry.cs` needs `ShedGeometry.cs` (its `Camera`) and a stub `Carve.Debug.ScenarioDriver` / `Carve.Scenario.Shed.Checks` for their `ReadAsync`s, which these tests would not call. Then the break table, the frame-hash guard (queued jobs by id, before and after), and the frame-review agent. No frames exist yet, so none has a hash to compare.
- **Questions for the person (not decided here):**
  1. `part` with `views`: built as recommended (every tile frames the subtree), since every tile draws the one shown selection. Not yet confirmed by the person.
  2. The mirrored/wrong-up check for the `Axle_F` and `Wheel_FL` frames: those subtrees are symmetric in z, and about y = 0.4 at both levels (the hub edit is not applied), so a check worked from their own framing bounds cannot tell a mirrored camera from the right one; only the whole-rover frames are asymmetric. This may be "a pixel check that cannot pass as specified".
  3. Rules chosen where the spec is silent, each one line to change: `level` refused with pose, clip, targets and weights; labels with `part` and no `mesh` going to the subtree's one geometry owner; `level` with `mesh` labelling the level's ids, refused when that part has none at the level; get_mesh's `world` as decomposed TRS (exact unless a turned parent is scaled unevenly, which shears).
- **Surprising:** `WeightHeat.Of` runs over `Model.Meshes`, not over `Combined`'s occurrences, so the weights look on a model with instances misaligns (a T-91-era gap, not T-94's). The subtree path here takes each occurrence's run itself; the whole-model path is left as it was.
