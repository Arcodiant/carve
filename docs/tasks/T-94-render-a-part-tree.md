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
- **The person's rulings on the first hand-off's questions (2026-11-18), each on the recommended option:** (1) `part` with `views`: every tile frames the named subtree, as built. (2) The mirror and up checks run on the whole-rover frames, which are asymmetric; the subtree frames (`Axle_F`, `Wheel_FL`, symmetric in z and about y = 0.4) are checked by their pixels and by the hub boss facing outward, not by a mirror or up check of their own. (3) The four rules the first agent chose are accepted as built: `level` refused with pose, clip, targets or weights; labels with `part` and no `mesh` go to the subtree's one part with its own geometry; labels with `level` show that level's ids; get_mesh's `world` is a decomposed translation, rotation and scale. (4) The weights-look gap (`WeightHeat.Of` over `Model.Meshes`, not occurrences) is filed off-milestone as B-7 and left alone here.
- **Frames (second agent, Opus 5.5, 2026-11-18), not yet passing:** `RoverRenderTests` (regression) builds the rover twice through the tools, in the real process (drawn) and in `ToolHarness` (the expected `Model`), and judges five frames, kept in `artifacts/rover-render-tests/`: `rover-front-live` (`{}`), `rover-front-level-0` (`{"level":0}`), `axle-f` (`{"part":"Axle_F","direction":[1,0.3,0]}`), `wheel-fl` (`{"part":"Wheel_FL"}`, the plain frame the labelled one is compared with) and `wheel-fl-labels-faces`. Captions as drawn: `512x512, view front, 304 vertices, 240 faces`; `512x512, direction (1, 0.3, 0), part Axle_F, 128 vertices, 100 faces`; `512x512, view front, part Wheel_FL, 64 vertices, 50 faces`.
- **Shared rules as used (for T-96):** the Cuts ray test (`CutsGeometry.cs`'s `Solid`, linked) built from `Model.CombinedOf(part, level)`, under the shed's `Camera` (`ShedGeometry.cs`, linked) on the drawn mesh's live-vertex bounds and render's direction; every fourth pixel each way that `Seen` is sure of is the shed's dark background (48..49, 51..53, 55..57) or its face's `ColourRule.Lit` shade (uncoloured faces `ColourRule.Grey`) within 2. `SeenNarrow` for the probes: the cab's +Z side, the mast post and the hub boss in the whole-rover frames, each also required to land on a different shade (by more than 6) at the pixel mirrored left for right and top for bottom, so a mirrored or upside-down camera fails it. `ScenarioStubs.cs` stands in for `Carve.Debug.ScenarioDriver` and the shed's `Checks.CallTextAsync`, which the linked files' `ReadAsync`s name and these tests never call. `Drawn` and the gatehouse in-sight test are not used: no frame here has a lone edge over the background or an occlusion the ray test does not already decide.
- **Rebase onto T-95:** one conflict in `Model.cs`, both sides additions (T-95's `Join`, T-94's `SubtreeOf`, `CombinedOf`, `OffsetOf`); both kept. Build clean, server unit 916 of 916.

### Hand-off

Second agent (Opus 5.5), 2026-11-18, stopped on two checks that cannot pass as specified (the spec's *Open rules: stop and ask*).

- **Done and checked:** the frame tests and the shared rules above, committed; the rebase onto `origin/milestone/M-9` (T-95). The ray test and the frames agree: `rover-front-live` 14 178 of 14 179 sure grid pixels right, `axle-f`'s grid all right, the probes right. The before-run of the frame guard is queued on the milestone tip `44a5e42` (server-regression `J-20261119-035828-9dc149`, scenario-cube `J-20261119-035829-1a54b5`, crate `J-20261119-035831-1937ae`, shed `J-20261119-035832-16962f`, cuts `J-20261119-035833-57c273`, rig `J-20261119-035835-88fa3e`; gatehouse, uvs, materials, character, soft and export carried a pass, so their before frames are those carried jobs').
- **Question 1, the Axle_F frame's bosses.** From `[1, 0.3, 0]` no hub boss is in sight: each boss stands on its wheel's outer cap (z ±0.87), and a camera at z = 0 sees the outer caps edge-on, behind the treads. The ray test finds 0 boss pixels for either wheel, and also from `[1, 0.6, 0]`, `[1, 1, 0]`, `[1, 1.5, 0]` and `[0.5, 1, 0]`; `[1, 1, 0.3]` shows Wheel_FL's boss only. What the frame does show is both inner caps (face 0 of each wheel), flat and dark, where a boss turned inward or an instance drawn unturned would stand. Options: (a) **recommended:** keep `[1, 0.3, 0]` and read "both bosses outward" as both inner caps seen with no boss on them (the grid check already holds every inner-cap pixel to the dark shade; a check of a least count of sure inner-cap pixels per wheel would be added); (b) two Axle_F frames, from `[1, 0.3, 0.6]` and `[1, 0.3, -0.6]`, each showing one boss; (c) another direction the person names.
- **Question 2, ColourRule on the dark wheel.** At ColourRule's tolerance of 2, three of the dark (`#3C3C3C`) wheel faces are drawn 3 to 4 levels brighter than `ColourRule.Lit` predicts: normal (0.98, -0.20, 0) by 4 (55, 58, 62 predicted, 58, 61, 66 drawn; in `rover-front-live` and `wheel-fl`), normal (-0.20, 0.98, 0) by 4 (60, 67, 78 against 64, 71, 81; `wheel-fl` only, the chassis hides it in the whole rover). Every other face judged (68, 28 and 14 faces in the three frames) is within 2. The geometry is right (each face's pixels are one flat colour, on the face the ray test names); it is the light model's prediction that misses. With no grey face of the same normal in these frames, `ColourRule.Predict` (the Cuts way) cannot be used. Options: (a) **recommended:** tolerance 4 for the wheels' dark faces only, the rest at 2, with the measured table kept as evidence; (b) the dark wheel's faces judged by which face the pixel is (the ray test) and not by shade; (c) the rule itself looked at for low albedo, off-milestone. Either way it is a loosening, so it is the person's.
- **Evidence:** `G:\My Drive\artifacts\carve\M-9\T-94\ray-and-shade-diagnostic.txt` (the boss-pixel counts by direction and each frame's worst faces). The frames in `D:\Repos\carve\branches\task\T-94\artifacts\rover-render-tests\`.
- **Left:** apply the two rulings; the labelled frame's check (built: the plain `wheel-fl` frame passes the grid, the labelled frame differs from it in over 200 pixels and its caption counts the subtree; not yet run past question 2); the frame guard's after-run (this hand-off's `verify`) compared with the before-run by hash; the break table at `G:\My Drive\artifacts\carve\M-9\T-94\break-table.tsv`; the frame-review agent's round-1 copies.
- **Verify:** at the end of this phase; server-regression fails on the three `RoverRenderTests` cases, each on one of the two questions above, until they are ruled.
